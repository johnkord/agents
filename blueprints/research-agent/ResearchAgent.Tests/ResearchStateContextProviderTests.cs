using Microsoft.Extensions.Logging.Abstractions;
using ResearchAgent.App;
using ResearchAgent.Core.Memory;
using ResearchAgent.Core.Models;

namespace ResearchAgent.Tests;

public class ResearchStateContextProviderTests
{
    private static ResearchMemory NewMemory() => new();

    [Fact]
    public void Render_returns_empty_when_memory_has_nothing()
    {
        var provider = new ResearchStateContextProvider(NewMemory(), NullLogger.Instance);
        Assert.Equal(string.Empty, provider.Render());
    }

    [Fact]
    public void Render_includes_question_when_set()
    {
        var mem = NewMemory();
        mem.SetResearchQuestion("What is the impact of X on Y?");
        var provider = new ResearchStateContextProvider(mem, NullLogger.Instance);
        var render = provider.Render();
        Assert.Contains("## Research Question", render);
        Assert.Contains("What is the impact of X on Y?", render);
    }

    [Fact]
    public void Render_includes_plan_when_set()
    {
        var mem = NewMemory();
        mem.SetResearchQuestion("Q?");
        mem.SetPlannerOutput("SQ1. foo\nSQ2. bar");
        var render = new ResearchStateContextProvider(mem, NullLogger.Instance).Render();
        Assert.Contains("## Research Plan (from Planner)", render);
        Assert.Contains("SQ1. foo", render);
        Assert.Contains("SQ2. bar", render);
    }

    [Fact]
    public void Render_includes_accumulated_state_when_findings_exist()
    {
        var mem = NewMemory();
        mem.SetResearchQuestion("Q?");
        mem.StoreFinding(new ResearchFinding
        {
            Id = "f1",
            SubQuestionId = "SQ1",
            Content = "Fact about foo.",
            SourceId = "s1",
            Confidence = 0.85,
        });
        var render = new ResearchStateContextProvider(mem, NullLogger.Instance).Render();
        Assert.Contains("## Accumulated Research State", render);
        Assert.Contains("Fact about foo.", render);
    }

    [Fact]
    public void Render_can_omit_findings_when_includeFindings_is_false()
    {
        var mem = NewMemory();
        mem.SetResearchQuestion("Q?");
        mem.StoreFinding(new ResearchFinding
        {
            Id = "f1",
            SubQuestionId = "SQ1",
            Content = "Should not appear.",
            SourceId = "s1",
            Confidence = 0.9,
        });
        var provider = new ResearchStateContextProvider(mem, NullLogger.Instance, includeFindings: false);
        var render = provider.Render();
        Assert.DoesNotContain("Should not appear.", render);
        Assert.Contains("## Research Question", render);
    }

    [Fact]
    public void Render_includes_provenance_footer_when_non_empty()
    {
        var mem = NewMemory();
        mem.SetResearchQuestion("Q?");
        var render = new ResearchStateContextProvider(mem, NullLogger.Instance).Render();
        Assert.Contains("ResearchStateContextProvider", render);
        Assert.Contains("transient context", render);
    }

    [Fact]
    public void ResearchMemory_Set_and_Get_round_trip()
    {
        var mem = NewMemory();
        Assert.Null(mem.ResearchQuestion);
        Assert.Null(mem.PlannerOutput);
        mem.SetResearchQuestion("Q1");
        mem.SetPlannerOutput("P1");
        Assert.Equal("Q1", mem.ResearchQuestion);
        Assert.Equal("P1", mem.PlannerOutput);
        // Last write wins
        mem.SetResearchQuestion("Q2");
        Assert.Equal("Q2", mem.ResearchQuestion);
    }
}
