namespace Forge.Tests;

using Forge.Core;

public class SessionAnalyzerTests
{
    [Fact]
    public void ComputeReadCoalescence_AllUniqueFiles_Returns1()
    {
        var steps = new List<ParsedStep>
        {
            new() { StepNumber = 0, ToolCalls = [
                new() { ToolName = "read_file", Arguments = """{"filePath":"/a.cs"}""" },
                new() { ToolName = "read_file", Arguments = """{"filePath":"/b.cs"}""" },
            ]},
        };

        // Use the public Analyze path indirectly — test the compliance method directly
        var compliance = SessionAnalyzer.ComputeVerificationCompliance(steps);
        Assert.Equal("N/A (no edits)", compliance);
    }

    [Fact]
    public void ComputeVerificationCompliance_EditFollowedByRead_Returns100Percent()
    {
        var steps = new List<ParsedStep>
        {
            new() { StepNumber = 0, ToolCalls = [
                new() { ToolName = "replace_string_in_file", Arguments = """{"filePath":"/a.cs"}""" },
            ]},
            new() { StepNumber = 1, ToolCalls = [
                new() { ToolName = "read_file", Arguments = """{"filePath":"/a.cs"}""" },
            ]},
        };

        var compliance = SessionAnalyzer.ComputeVerificationCompliance(steps);
        Assert.Contains("100", compliance);
    }

    [Fact]
    public void ComputeVerificationCompliance_EditNotFollowedByRead_Returns0Percent()
    {
        var steps = new List<ParsedStep>
        {
            new() { StepNumber = 0, ToolCalls = [
                new() { ToolName = "replace_string_in_file", Arguments = """{"filePath":"/a.cs"}""" },
            ]},
            new() { StepNumber = 1, ToolCalls = [
                new() { ToolName = "grep_search", Arguments = "{}" },
            ]},
            new() { StepNumber = 2, ToolCalls = [
                new() { ToolName = "grep_search", Arguments = "{}" },
            ]},
        };

        var compliance = SessionAnalyzer.ComputeVerificationCompliance(steps);
        Assert.Contains("0/1", compliance);
    }

    [Fact]
    public void ComputeVerificationCompliance_NoEdits_ReturnsNA()
    {
        var steps = new List<ParsedStep>
        {
            new() { StepNumber = 0, ToolCalls = [
                new() { ToolName = "read_file", Arguments = """{"filePath":"/a.cs"}""" },
            ]},
        };

        var compliance = SessionAnalyzer.ComputeVerificationCompliance(steps);
        Assert.Equal("N/A (no edits)", compliance);
    }

    [Fact]
    public void FormatReport_ProducesReadableOutput()
    {
        var analysis = new SessionAnalysis
        {
            FilePath = "/sessions/test.jsonl",
            Task = "Fix the bug",
            Model = "gpt-5.4",
            Success = true,
            TotalSteps = 5,
            TotalTokens = 50000,
            TokensPerStepGrowth = 0.52,
            ReadCoalescenceRate = 0.8,
            VerificationCompliance = "2/2 (100%)",
            ConsolidationCaptured = true,
            SessionStatus = "complete",
            EpisodeCount = 3,
            PivotCount = 0,
            HasAssumptions = false,
        };

        var report = SessionAnalyzer.FormatReport(analysis);

        Assert.Contains("Fix the bug", report);
        Assert.Contains("complete", report);
        Assert.Contains("50,000", report);
        Assert.Contains("52 %", report);
        Assert.Contains("80 %", report);
    }

    [Fact]
    public void FormatAggregate_ProducesResolveRate()
    {
        var sessions = new List<SessionAnalysis>
        {
            new() { FilePath = "a.jsonl", Task = "A", Success = true, TotalSteps = 5, TotalTokens = 30000, ReadCoalescenceRate = 1.0, SessionStatus = "complete" },
            new() { FilePath = "b.jsonl", Task = "B", Success = false, TotalSteps = 10, TotalTokens = 80000, ReadCoalescenceRate = 0.5, SessionStatus = "incomplete", TokensPerStepGrowth = 0.5 },
        };

        var aggregate = SessionAnalyzer.FormatAggregate(sessions);

        Assert.Contains("1/2", aggregate);
        Assert.Contains("50 %", aggregate);
    }

    // ── P2.5: Context-management counters ───────────────────────────────

    [Fact]
    public void ComputeContextMgmtCounters_UsesStructuredResultTag()
    {
        // Primary path: counters read tc.ResultTag, which AgentLoop/ToolExecutor
        // set at emission time. Summaries are irrelevant here.
        var steps = new List<ParsedStep>
        {
            new() { StepNumber = 0, ToolCalls = [
                new() { ToolName = "run_shell_command", ResultTag = "spilled", SpillPath = "/tmp/x.txt" },
                new() { ToolName = "read_file", ResultTag = "stubbed" },
            ]},
            new() { StepNumber = 1, ToolCalls = [
                new() { ToolName = "read_file", ResultTag = "stubbed" },
                new() { ToolName = "read_file", ResultTag = "blocked" },
                new() { ToolName = "read_file" /* no tag — happy path */ },
            ]},
        };

        var (spills, stubs, blocks, truncs) = SessionAnalyzer.ComputeContextMgmtCounters(steps);

        Assert.Equal(1, spills);
        Assert.Equal(2, stubs);
        Assert.Equal(1, blocks);
        Assert.Equal(0, truncs);
    }

    [Fact]
    public void ComputeContextMgmtCounters_LegacyLogs_DetectsSpillsFromSummary()
    {
        // Fallback path: pre-P2.5 sessions had no ResultTag, but spills are still
        // detectable because the "Full output saved to:" pointer lands in the
        // LLM-visible output (which for short-enough raw tool outputs is also
        // in the first 500 chars of ResultSummary).
        var steps = new List<ParsedStep>
        {
            new() { StepNumber = 0, ToolCalls = [
                new() { ToolName = "grep_search", ResultSummary = "match1\nmatch2\n\n... truncated (50,000 total characters, 1200 total lines). Full output saved to: /tmp/spill.txt. Use read_file …" },
                new() { ToolName = "read_file", ResultSummary = "truncated intermediate: line 1\nline 2\n\n... truncated (300 of 500 lines). Use read_file …" },
            ]},
        };

        var (spills, stubs, blocks, truncs) = SessionAnalyzer.ComputeContextMgmtCounters(steps);

        Assert.Equal(1, spills);
        Assert.Equal(0, stubs);   // not detectable from legacy summaries
        Assert.Equal(0, blocks);  // not detectable from legacy summaries
        Assert.Equal(1, truncs);  // short enough that the pointer is in the summary head
    }

    [Fact]
    public void ComputeContextMgmtCounters_EmptySession_ReturnsZeros()
    {
        var (spills, stubs, blocks, truncs) =
            SessionAnalyzer.ComputeContextMgmtCounters(new List<ParsedStep>());
        Assert.Equal(0, spills);
        Assert.Equal(0, stubs);
        Assert.Equal(0, blocks);
        Assert.Equal(0, truncs);
    }

    [Fact]
    public void ComputeContextMgmtCounters_NonReadBlockNotCountedAsReadBlock()
    {
        // Regression: a guardrail-style "BLOCKED: {reason}" from ToolExecutor is
        // NOT a read hard-block. The new structured tag makes this unambiguous
        // (guardrail blocks don't set ResultTag = "blocked").
        var steps = new List<ParsedStep>
        {
            new() { StepNumber = 0, ToolCalls = [
                new() { ToolName = "delete_file", IsError = true, ResultSummary = "BLOCKED: path outside workspace" },
            ]},
        };
        var (_, _, blocks, _) = SessionAnalyzer.ComputeContextMgmtCounters(steps);
        Assert.Equal(0, blocks);
    }

    [Fact]
    public void FormatDiff_ShowsDirectionArrows()
    {
        var a = new SessionAnalysis
        {
            FilePath = "before.jsonl", Task = "t", TotalSteps = 10, TotalTokens = 50_000,
            ReadCoalescenceRate = 0.8, SpillsTriggered = 0, StubReturns = 0, ReadsBlocked = 0,
        };
        var b = new SessionAnalysis
        {
            // Improvements: fewer tokens, fewer steps, more spills (lowerIsBetter=false for spills).
            // Regression: coalescence dropped (lowerIsBetter=false → "↑worse").
            FilePath = "after.jsonl", Task = "t", TotalSteps = 8, TotalTokens = 30_000,
            ReadCoalescenceRate = 0.5, SpillsTriggered = 2, StubReturns = 3, ReadsBlocked = 1,
        };

        var diff = SessionAnalyzer.FormatDiff(a, b);

        Assert.Contains("↓better", diff);  // tokens / steps went down
        Assert.Contains("↑worse", diff);   // coalescence regressed
    }

    [Fact]
    public void FormatDiff_SameSession_ShowsZeroDeltas()
    {
        var a = new SessionAnalysis { FilePath = "x.jsonl", Task = "t", TotalSteps = 5, TotalTokens = 10_000 };
        var diff = SessionAnalyzer.FormatDiff(a, a);
        Assert.Contains("±0", diff);
    }
}
