namespace Forge.Tests;

using Forge.Core;

public class EpisodeSegmenterTests
{
    [Fact]
    public void Segment_EmptySteps_ReturnsEmpty()
    {
        var episodes = EpisodeSegmenter.Segment([]);
        Assert.Empty(episodes);
    }

    [Fact]
    public void Segment_SingleExplorationStep_ReturnsSingleEpisode()
    {
        var steps = new List<StepRecord>
        {
            MakeStep(0, "read_file", "/src/File.cs"),
        };

        var episodes = EpisodeSegmenter.Segment(steps);

        Assert.Single(episodes);
        Assert.Equal("exploration", episodes[0].Type);
        Assert.Equal(0, episodes[0].StartStep);
        Assert.Equal(0, episodes[0].EndStep);
        Assert.Equal("success", episodes[0].Outcome);
    }

    [Fact]
    public void Segment_ExplorationThenImplementation_TwoEpisodes()
    {
        var steps = new List<StepRecord>
        {
            MakeStep(0, "grep_search"),
            MakeStep(1, "read_file", "/src/Auth.cs"),
            MakeStep(2, "replace_string_in_file", "/src/Auth.cs"),
            MakeStep(3, "replace_string_in_file", "/src/Auth.cs"),
        };

        var episodes = EpisodeSegmenter.Segment(steps);

        Assert.Equal(2, episodes.Count);
        Assert.Equal("exploration", episodes[0].Type);
        Assert.Equal(0, episodes[0].StartStep);
        Assert.Equal(1, episodes[0].EndStep);
        Assert.Equal("implementation", episodes[1].Type);
        Assert.Equal(2, episodes[1].StartStep);
        Assert.Equal(3, episodes[1].EndStep);
    }

    [Fact]
    public void Segment_ExploreImplVerify_ThreeEpisodes()
    {
        var steps = new List<StepRecord>
        {
            MakeStep(0, "file_search"),
            MakeStep(1, "read_file", "/src/File.cs"),
            MakeStep(2, "replace_string_in_file", "/src/File.cs"),
            MakeStep(3, "run_tests"),
        };

        var episodes = EpisodeSegmenter.Segment(steps);

        Assert.Equal(3, episodes.Count);
        Assert.Equal("exploration", episodes[0].Type);
        Assert.Equal("implementation", episodes[1].Type);
        Assert.Equal("verification", episodes[2].Type);
    }

    [Fact]
    public void Segment_FailedStepsMarkedAsFailure()
    {
        var steps = new List<StepRecord>
        {
            MakeStep(0, "replace_string_in_file", "/src/File.cs", isError: true),
            MakeStep(1, "replace_string_in_file", "/src/File.cs", isError: true),
        };

        var episodes = EpisodeSegmenter.Segment(steps);

        Assert.Single(episodes);
        Assert.Equal("failure", episodes[0].Outcome);
    }

    [Fact]
    public void Segment_PlanningStepWithNoTools()
    {
        var steps = new List<StepRecord>
        {
            new()
            {
                StepNumber = 0,
                Timestamp = DateTimeOffset.UtcNow,
                Thought = "Let me plan this...",
                ToolCalls = [],
            },
            MakeStep(1, "read_file", "/src/File.cs"),
        };

        var episodes = EpisodeSegmenter.Segment(steps);

        Assert.Equal(2, episodes.Count);
        Assert.Equal("planning", episodes[0].Type);
        Assert.Equal("exploration", episodes[1].Type);
    }

    [Fact]
    public void Segment_FilesInvolved_CappedAtFive()
    {
        var steps = new List<StepRecord>
        {
            MakeStepMultiFiles(0, "read_file",
                "/a.cs", "/b.cs", "/c.cs", "/d.cs", "/e.cs", "/f.cs", "/g.cs"),
        };

        var episodes = EpisodeSegmenter.Segment(steps);

        Assert.True(episodes[0].FilesInvolved.Count <= 5);
    }

    [Fact]
    public void ClassifyStep_RunBashBuild_IsVerification()
    {
        var step = new StepRecord
        {
            StepNumber = 0,
            Timestamp = DateTimeOffset.UtcNow,
            ToolCalls =
            [
                new ToolCallRecord
                {
                    ToolName = "run_bash_command",
                    Arguments = """{"command":"dotnet build"}""",
                    ResultSummary = "Build succeeded.",
                    DurationMs = 5000,
                },
            ],
        };

        Assert.Equal("verification", EpisodeSegmenter.ClassifyStep(step));
    }

    // ── BuildTrajectoryLine tests ──────────────────────────────────

    [Fact]
    public void BuildTrajectoryLine_ShortSession_ReturnsNull()
    {
        var steps = new List<StepRecord>
        {
            MakeStep(0, "read_file"),
            MakeStep(1, "read_file"),
        };

        Assert.Null(EpisodeSegmenter.BuildTrajectoryLine(steps));
    }

    [Fact]
    public void BuildTrajectoryLine_SingleEpisode_ReturnsNull()
    {
        var steps = new List<StepRecord>
        {
            MakeStep(0, "read_file"),
            MakeStep(1, "grep_search"),
            MakeStep(2, "file_search"),
        };

        Assert.Null(EpisodeSegmenter.BuildTrajectoryLine(steps));
    }

    [Fact]
    public void BuildTrajectoryLine_MultiEpisode_ReturnsCompactChain()
    {
        var steps = new List<StepRecord>
        {
            MakeStep(0, "grep_search"),
            MakeStep(1, "read_file"),
            MakeStep(2, "replace_string_in_file"),
            MakeStep(3, "run_tests"),
        };

        var trajectory = EpisodeSegmenter.BuildTrajectoryLine(steps);

        Assert.NotNull(trajectory);
        Assert.Contains("explore", trajectory);
        Assert.Contains("impl", trajectory);
        Assert.Contains("verify", trajectory);
        Assert.Contains("→", trajectory);
    }

    [Fact]
    public void BuildTrajectoryLine_FailedEpisode_IncludesFAILMarker()
    {
        var steps = new List<StepRecord>
        {
            MakeStep(0, "read_file"),
            MakeStep(1, "replace_string_in_file", isError: true),
            MakeStep(2, "run_tests"),
        };

        var trajectory = EpisodeSegmenter.BuildTrajectoryLine(steps);

        Assert.NotNull(trajectory);
        Assert.Contains("FAIL", trajectory);
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static StepRecord MakeStep(int stepNum, string toolName,
        string? filePath = null, bool isError = false)
    {
        var args = filePath is not null
            ? $"{{\"filePath\":\"{filePath}\"}}"
            : "{}";

        return new StepRecord
        {
            StepNumber = stepNum,
            Timestamp = DateTimeOffset.UtcNow,
            ToolCalls =
            [
                new ToolCallRecord
                {
                    ToolName = toolName,
                    Arguments = args,
                    ResultSummary = isError ? "Error: something failed" : "OK",
                    IsError = isError,
                    DurationMs = 100,
                },
            ],
        };
    }

    private static StepRecord MakeStepMultiFiles(int stepNum, string toolName, params string[] files)
    {
        return new StepRecord
        {
            StepNumber = stepNum,
            Timestamp = DateTimeOffset.UtcNow,
            ToolCalls = files.Select(f => new ToolCallRecord
            {
                ToolName = toolName,
                Arguments = $"{{\"filePath\":\"{f}\"}}",
                ResultSummary = "OK",
                DurationMs = 50,
            }).ToList(),
        };
    }
}
