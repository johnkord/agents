using Forge.Core;

namespace Forge.Tests;

/// <summary>
/// Tests for VerificationTracker — tracks whether the agent follows up
/// file-modifying tool calls with appropriate verification.
///
/// Research basis:
///   - DeepVerifier (2026): 8-11% accuracy gain from structured verification
///   - CoRefine (2026): cheap confidence routing — only intervene when needed
/// </summary>
public class VerificationTrackerTests
{
    private static ToolCallRecord MakeToolCall(string name, bool isError = false) => new()
    {
        ToolName = name,
        Arguments = "{}",
        ResultSummary = isError ? "Error" : "OK",
        IsError = isError,
        DurationMs = 10,
    };

    [Fact]
    public void RecordStep_NoEdits_ReturnsNull()
    {
        var tracker = new VerificationTracker();

        var reminder = tracker.RecordStep(0, [MakeToolCall("read_file")]);

        Assert.Null(reminder);
    }

    [Fact]
    public void RecordStep_EditFollowedByReadFile_NoReminder()
    {
        var tracker = new VerificationTracker();

        // Step 0: edit a file
        tracker.RecordStep(0, [MakeToolCall("replace_string_in_file")]);

        // Step 1: read back the file (verification)
        var reminder = tracker.RecordStep(1, [MakeToolCall("read_file")]);

        Assert.Null(reminder);
    }

    [Fact]
    public void RecordStep_EditNotVerified_ReturnsReminder()
    {
        var tracker = new VerificationTracker();

        // Step 0: edit a file
        tracker.RecordStep(0, [MakeToolCall("replace_string_in_file")]);

        // Step 1: do something else (not verification)
        tracker.RecordStep(1, [MakeToolCall("grep_search")]);

        // Step 2: still no verification — should trigger reminder
        var reminder = tracker.RecordStep(2, [MakeToolCall("grep_search")]);

        Assert.NotNull(reminder);
        Assert.Contains("modified a file at step 0", reminder);
        Assert.Contains("read_file", reminder);
    }

    [Fact]
    public void RecordStep_CreateFileVerifiedWithReadFile_NoReminder()
    {
        var tracker = new VerificationTracker();

        // Step 0: create a file
        tracker.RecordStep(0, [MakeToolCall("create_file")]);

        // Step 1: read it back
        var reminder = tracker.RecordStep(1, [MakeToolCall("read_file")]);

        Assert.Null(reminder);
    }

    [Fact]
    public void RecordStep_ErrorToolCallsNotTracked()
    {
        var tracker = new VerificationTracker();

        // Step 0: failed edit — shouldn't require verification
        tracker.RecordStep(0, [MakeToolCall("replace_string_in_file", isError: true)]);

        // Step 1: no verification needed
        tracker.RecordStep(1, [MakeToolCall("grep_search")]);
        var reminder = tracker.RecordStep(2, [MakeToolCall("grep_search")]);

        Assert.Null(reminder);
    }

    [Fact]
    public void RecordStep_MultipleEdits_TracksEachSeparately()
    {
        var tracker = new VerificationTracker();

        // Step 0: two edits
        tracker.RecordStep(0, [
            MakeToolCall("replace_string_in_file"),
            MakeToolCall("create_file"),
        ]);

        // Step 1: one read_file verifies both (since both need read_file)
        var reminder = tracker.RecordStep(1, [MakeToolCall("read_file")]);

        Assert.Null(reminder);
    }

    [Fact]
    public void RecordStep_ReminderOnlyFiresOnce()
    {
        var tracker = new VerificationTracker();

        // Step 0: edit
        tracker.RecordStep(0, [MakeToolCall("replace_string_in_file")]);
        // Step 1: no verification
        tracker.RecordStep(1, [MakeToolCall("grep_search")]);
        // Step 2: reminder fires
        var reminder1 = tracker.RecordStep(2, [MakeToolCall("grep_search")]);
        Assert.NotNull(reminder1);

        // Step 3: should NOT fire again for the same edit
        var reminder2 = tracker.RecordStep(3, [MakeToolCall("grep_search")]);
        Assert.Null(reminder2);
    }

    [Fact]
    public void GetStats_TracksComplianceCorrectly()
    {
        var tracker = new VerificationTracker();

        // Edit 1: verified
        tracker.RecordStep(0, [MakeToolCall("replace_string_in_file")]);
        tracker.RecordStep(1, [MakeToolCall("read_file")]);

        // Edit 2: not verified
        tracker.RecordStep(2, [MakeToolCall("create_file")]);
        tracker.RecordStep(3, [MakeToolCall("grep_search")]);
        tracker.RecordStep(4, [MakeToolCall("grep_search")]); // triggers overdue

        var stats = tracker.GetStats();

        Assert.Equal(2, stats.TotalEdits);
        Assert.Equal(1, stats.VerifiedEdits);
        Assert.Equal(0.5, stats.ComplianceRate);
    }

    [Fact]
    public void GetStats_NoEdits_FullCompliance()
    {
        var tracker = new VerificationTracker();
        tracker.RecordStep(0, [MakeToolCall("read_file")]);

        var stats = tracker.GetStats();

        Assert.Equal(0, stats.TotalEdits);
        Assert.Equal(1.0, stats.ComplianceRate);
    }

    [Fact]
    public void RecordStep_RunBashCommandWithSedI_TrackedAsEdit()
    {
        var tracker = new VerificationTracker();

        tracker.RecordStep(0, [new ToolCallRecord
        {
            ToolName = "run_bash_command",
            Arguments = """{"command": "sed -i 's/old/new/g' /workspace/file.cs"}""",
            ResultSummary = "OK",
            DurationMs = 50,
        }]);

        // Should need verification
        tracker.RecordStep(1, [MakeToolCall("grep_search")]);
        var reminder = tracker.RecordStep(2, [MakeToolCall("grep_search")]);

        Assert.NotNull(reminder);
        Assert.Contains("run_bash_command", reminder);
    }

    [Fact]
    public void RecordStep_RunBashCommandWithoutFileWrite_NotTracked()
    {
        var tracker = new VerificationTracker();

        tracker.RecordStep(0, [new ToolCallRecord
        {
            ToolName = "run_bash_command",
            Arguments = """{"command": "dotnet build agents.sln"}""",
            ResultSummary = "OK",
            DurationMs = 3000,
        }]);

        tracker.RecordStep(1, [MakeToolCall("grep_search")]);
        var reminder = tracker.RecordStep(2, [MakeToolCall("grep_search")]);

        Assert.Null(reminder); // build commands don't need read-file verification
    }
}
