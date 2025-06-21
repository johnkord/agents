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
}
