using System;
using AgentAlpha.Configuration;
using AgentAlpha.Interfaces;
using AgentAlpha.Models;
using Microsoft.Extensions.Logging;

namespace AgentAlpha.Services;

/// <summary>
/// Iteratively refines a plan until it meets the quality target
/// or the maximum number of attempts is reached.
/// </summary>
public class PlanRefinementLoop
{
    private readonly IPlanner        _planner;
    private readonly IPlanEvaluator  _evaluator;
    private readonly AgentConfiguration _cfg;
    private readonly ILogger<PlanRefinementLoop> _log;

    public PlanRefinementLoop(IPlanner planner,
                              IPlanEvaluator evaluator,
                              AgentConfiguration cfg,
                              ILogger<PlanRefinementLoop> log)
    {
        _planner   = planner;
        _evaluator = evaluator;
        _cfg       = cfg;
        _log       = log;
    }

    public async Task<string> RefinePlanAsync(string plan, string task)
    {
        var attempts  = 0;
        var stagnation = 0;

        var eval = await _evaluator.EvaluateAsync(plan, task);

        var lastScore = eval.Score;                 // initialise with first score

        while (eval.Score < _cfg.PlanQualityTarget
               && attempts < _cfg.MaxPlanRefinements
               && stagnation < 1)                   // ← stop after first stagnant score
        {
            attempts++;
            _log.LogInformation("Plan score {Score:F2} < target {Target}. Refining… (Attempt {Attempt})",
                                eval.Score, _cfg.PlanQualityTarget, attempts);

            plan = await _planner.RefinePlanAsync(plan, eval.Feedback);
            eval = await _evaluator.EvaluateAsync(plan, task);

            // stagnation detection – no meaningful improvement
            if (Math.Abs(eval.Score - lastScore) < 0.001)
            {
                stagnation++;
                _log.LogInformation("No score improvement detected (still {Score:F2}) – stopping refinement.", eval.Score);
            }
            else
            {
                stagnation = 0;
            }
            lastScore = eval.Score;
        }

        _log.LogInformation("Refinement loop finished: attempts={Attempts}, finalScore={Score:F2}",
                            attempts, eval.Score);
        return plan;
    }
}
