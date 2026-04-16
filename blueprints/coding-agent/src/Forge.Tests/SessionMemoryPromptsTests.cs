using Forge.Core;
using Forge.Core.SessionMemory;

namespace Forge.Tests;

public class SessionMemoryPromptsTests
{
    [Fact]
    public void ParseSnapshot_extracts_json_from_summary_block()
    {
        var response = """
            <analysis>
            Thinking about what happened…
            </analysis>
            <summary>
            {
              "taskDescription": "Refactor the auth module",
              "currentState": "Mid-refactor; tests failing",
              "filesTouched": ["auth.cs - split into AuthService + AuthStore"],
              "errorsAndFixes": ["null ref in AuthStore → added guard"],
              "pending": ["wire DI in Program.cs"],
              "worklog": ["step 1: read auth.cs", "step 2: split module"]
            }
            </summary>
            """;
        var snap = SessionMemoryPrompts.ParseSnapshot(response, lastExtractedStep: 2, extractionCount: 1, updatedAt: DateTimeOffset.UtcNow);
        Assert.Equal("Refactor the auth module", snap.TaskDescription);
        Assert.Equal("Mid-refactor; tests failing", snap.CurrentState);
        Assert.Single(snap.FilesTouched);
        Assert.Single(snap.Pending);
        Assert.Equal(2, snap.Worklog.Count);
        Assert.Equal(2, snap.LastExtractedStep);
    }

    [Fact]
    public void ParseSnapshot_falls_back_to_balanced_braces_without_summary_tags()
    {
        var response = """
            Here you go:
            {"taskDescription":"T","currentState":"S","filesTouched":[],"errorsAndFixes":[],"pending":[],"worklog":[]}
            That's it.
            """;
        var snap = SessionMemoryPrompts.ParseSnapshot(response, 0, 1, DateTimeOffset.UtcNow);
        Assert.Equal("T", snap.TaskDescription);
    }

    [Fact]
    public void ParseSnapshot_handles_nested_braces_in_strings()
    {
        var response = """
            <summary>
            {"taskDescription":"work on {foo: bar}","currentState":"s"}
            </summary>
            """;
        var snap = SessionMemoryPrompts.ParseSnapshot(response, 0, 1, DateTimeOffset.UtcNow);
        Assert.Equal("work on {foo: bar}", snap.TaskDescription);
    }

    [Fact]
    public void ParseSnapshot_throws_on_missing_required_fields()
    {
        var response = """<summary>{"currentState":"s"}</summary>""";
        Assert.Throws<SessionMemoryParseException>(
            () => SessionMemoryPrompts.ParseSnapshot(response, 0, 1, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ParseSnapshot_throws_on_empty_response()
    {
        Assert.Throws<SessionMemoryParseException>(
            () => SessionMemoryPrompts.ParseSnapshot("", 0, 1, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ParseSnapshot_throws_on_no_json()
    {
        Assert.Throws<SessionMemoryParseException>(
            () => SessionMemoryPrompts.ParseSnapshot("just some prose", 0, 1, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ParseSnapshot_sanitizes_blank_list_entries()
    {
        var response = """
            <summary>
            {"taskDescription":"t","currentState":"s","filesTouched":["a.cs","","  "],"worklog":["x"," "]}
            </summary>
            """;
        var snap = SessionMemoryPrompts.ParseSnapshot(response, 0, 1, DateTimeOffset.UtcNow);
        Assert.Single(snap.FilesTouched);
        Assert.Single(snap.Worklog);
    }

    [Fact]
    public void BuildUserPrompt_includes_task_and_new_steps_only_when_watermark_provided()
    {
        var steps = new List<StepRecord>
        {
            new() { StepNumber = 0, Timestamp = DateTimeOffset.UtcNow, Thought = "old", PromptTokens = 0, CompletionTokens = 0 },
            new() { StepNumber = 1, Timestamp = DateTimeOffset.UtcNow, Thought = "fresh", PromptTokens = 0, CompletionTokens = 0 },
        };
        var req = new SessionMemoryExtractionRequest
        {
            Task = "my task",
            Steps = steps,
            FromStepIndex = 0,
            PreviousSummary = new SessionMemorySnapshot
            {
                TaskDescription = "my task",
                CurrentState = "prior state",
                LastExtractedStep = 0,
                UpdatedAt = DateTimeOffset.UtcNow,
                ExtractionCount = 1,
            },
        };
        var prompt = SessionMemoryPrompts.BuildUserPrompt(req);
        Assert.Contains("my task", prompt);
        Assert.Contains("fresh", prompt);
        Assert.DoesNotContain("Step 0 — ", prompt); // old step excluded via watermark
        Assert.Contains("Previous Snapshot", prompt);
    }
}
