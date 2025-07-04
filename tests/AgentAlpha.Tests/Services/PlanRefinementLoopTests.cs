using Xunit;
using AgentAlpha.Services;
using AgentAlpha.Interfaces;
using AgentAlpha.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Threading.Tasks;
using System.Collections.Generic;
using AgentAlpha.Models;   // ← NEW
using System;                         // NEW

namespace AgentAlpha.Tests.Services;

internal sealed class StubPlanner : IPlanner
{
    public int RefineCalls { get; private set; }

    public Task<string> CreatePlanAsync(string task, IList<string>? _ = null, string? __ = null)
        => Task.FromResult($"initial:{task}");

    public Task<string> RefinePlanAsync(string existing, string __, string? ___ = null)
    {
        RefineCalls++;
        return Task.FromResult(existing + $"|ref{RefineCalls}");
    }
}

internal sealed class StubEvaluator(double[] scores) : IPlanEvaluator
{
    private int _idx;
    public Task<EvaluationResult> EvaluateAsync(string plan, string task)
        => Task.FromResult(new EvaluationResult(scores[Math.Min(_idx, scores.Length - 1)], $"fb{_idx++}"));
}

public class PlanRefinementLoopTests
{
    [Fact]
    public async Task RefinePlanAsync_StopsWhenTargetReached()
    {
        // Arrange
        var planner    = new StubPlanner();
        var evaluator  = new StubEvaluator(new double[] { 0.4, 0.7, 0.85 }); // FIX
        var cfg        = new AgentConfiguration { PlanQualityTarget = 0.8, MaxPlanRefinements = 5 };
        var loop       = new PlanRefinementLoop(planner, evaluator, cfg, NullLogger<PlanRefinementLoop>.Instance);

        // Act
        var finalPlan = await loop.RefinePlanAsync("plan0", "task");

        // Assert
        Assert.Equal(2, planner.RefineCalls);   // refined twice (0.4→0.7→0.85≥target)
        Assert.Contains("|ref2", finalPlan);    // ensure final refinement applied
    }

    [Fact]
    public async Task RefinePlanAsync_StopsWhenScoreStagnates()
    {
        // Arrange – score never improves
        var planner   = new StubPlanner();
        var evaluator = new StubEvaluator(new double[] { 0.3, 0.3, 0.3 });    // FIX
        var cfg       = new AgentConfiguration { PlanQualityTarget = 0.9, MaxPlanRefinements = 5 };
        var loop      = new PlanRefinementLoop(planner, evaluator, cfg, NullLogger<PlanRefinementLoop>.Instance);

        // Act
        await loop.RefinePlanAsync("plan0", "task");

        // Assert – only 1 refinement because score did not improve
        Assert.Equal(1, planner.RefineCalls);
    }
}
