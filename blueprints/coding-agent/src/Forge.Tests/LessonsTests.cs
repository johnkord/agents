namespace Forge.Tests;

using Forge.Core;

public class LessonsTests : IDisposable
{
    private readonly string _testDir;

    public LessonsTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "forge-lessons-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void GenerateLesson_FailedSession_ReturnsLessonWithFailureInfo()
    {
        var result = new AgentResult
        {
            Success = false,
            Output = "Stopped: maximum steps reached.",
            Steps =
            [
                new StepRecord
                {
                    StepNumber = 0,
                    Timestamp = DateTimeOffset.UtcNow,
                    ToolCalls =
                    [
                        new ToolCallRecord
                        {
                            ToolName = "replace_string_in_file",
                            Arguments = "{}",
                            ResultSummary = "Error: oldString not found",
                            IsError = true,
                            DurationMs = 10,
                        },
                    ],
                    PromptTokens = 1000,
                    CompletionTokens = 500,
                    DurationMs = 2000,
                },
            ],
            TotalPromptTokens = 50_000,
            TotalCompletionTokens = 10_000,
            TotalDurationMs = 30_000,
            FailureReason = "Stopped: maximum steps reached.",
        };

        var lesson = AgentLoop.GenerateLesson("Fix the broken test in MyService", result);

        Assert.NotNull(lesson);
        Assert.Contains("fail", lesson);
        Assert.Contains("Fix the broken test", lesson);
        // Budget-exhaustion failures ("maximum steps") should NOT blame individual
        // tool errors — the cause is budget, not tool failure (Phase 6A fix).
        Assert.Contains("maximum steps", lesson);
        Assert.DoesNotContain("replace_string_in_file", lesson);
    }

    [Fact]
    public void GenerateLesson_CostlySuccess_ReturnsLessonAboutCost()
    {
        var result = new AgentResult
        {
            Success = true,
            Output = "Done.",
            Steps = [],
            TotalPromptTokens = 280_000,
            TotalCompletionTokens = 50_000,
            TotalDurationMs = 120_000,
        };

        var lesson = AgentLoop.GenerateLesson("Refactor authentication module", result);

        Assert.NotNull(lesson);
        Assert.Contains("costly", lesson);
        Assert.Contains("330,000", lesson); // 280K + 50K
    }

    [Fact]
    public void GenerateLesson_CheapSuccess_ReturnsNull()
    {
        var result = new AgentResult
        {
            Success = true,
            Output = "Done.",
            Steps = [],
            TotalPromptTokens = 10_000,
            TotalCompletionTokens = 2_000,
            TotalDurationMs = 5_000,
        };

        var lesson = AgentLoop.GenerateLesson("Simple task", result);

        Assert.Null(lesson);
    }
}
