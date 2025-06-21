namespace Forge.Core;

/// <summary>
/// Tracks whether the agent follows up file-modifying tool calls with appropriate verification.
///
/// Research basis:
///   - DeepVerifier (2026): Structured verification checklists give 8-11% accuracy gain
///   - CoRefine (2026): Cheap confidence routing — only intervene when verification is missing
///
/// Design: Data-driven verification rules define what follow-ups are expected after each
/// tool type. The tracker monitors tool calls per step and detects when verification is
/// overdue. AgentLoop can then inject a reminder (targeted intervention, not blanket injection).
///
/// This is the CoRefine-inspired approach: track cheaply → intervene selectively.
/// We don't inject after EVERY edit (expensive, ~30% token overhead). We inject only
/// when the agent skips verification (rare with good prompts, but critical to catch).
/// </summary>
public sealed class VerificationTracker
{
    /// <summary>
    /// Defines what verification follow-ups are expected after a tool call.
    /// </summary>
    private static readonly VerificationRule[] Rules =
    [
        new("replace_string_in_file", ["read_file"], WithinSteps: 2),
        new("create_file", ["read_file"], WithinSteps: 2),
    ];

    /// <summary>
    /// Patterns in run_bash_command arguments that indicate file modification.
    /// These bypass replace_string_in_file but still modify files and should trigger verification.
    /// </summary>
    private static readonly string[] FileWritingPatterns =
        ["sed -i", "> /", ">> /", "tee ", "write_text", "write(", "File.Write"];

    private readonly List<PendingVerification> _pending = [];
    private int _totalEdits;
    private int _verifiedEdits;

    /// <summary>
    /// Record the tools called in a step. Updates pending verification state.
    /// Returns a reminder message if verification is overdue, null otherwise.
    /// </summary>
    public string? RecordStep(int stepNumber, IReadOnlyList<ToolCallRecord> toolCalls)
    {
        // Check if any pending verifications are now satisfied or overdue
        string? reminder = null;
        var satisfied = new List<PendingVerification>();

        foreach (var pending in _pending)
        {
            // Check if any tool call in this step satisfies the pending verification
            var followUpSatisfied = toolCalls.Any(tc =>
                !tc.IsError && pending.Rule.RequiredFollowUps.Any(req =>
                    string.Equals(tc.ToolName, req, StringComparison.OrdinalIgnoreCase)));

            if (followUpSatisfied)
            {
                satisfied.Add(pending);
                _verifiedEdits++;
            }
            else if (stepNumber - pending.TriggerStep >= pending.Rule.WithinSteps)
            {
                // Overdue — verification was expected but not performed
                reminder ??= BuildReminder(pending, stepNumber);
                satisfied.Add(pending); // Remove from pending (don't nag repeatedly)
            }
        }

        foreach (var s in satisfied)
            _pending.Remove(s);

        // Check if this step contains file-modifying tool calls that need verification
        foreach (var tc in toolCalls)
        {
            if (tc.IsError) continue;

            var matchingRule = Rules.FirstOrDefault(r =>
                string.Equals(r.TriggerTool, tc.ToolName, StringComparison.OrdinalIgnoreCase));

            // Also detect file-modifying run_bash_command calls (sed -i, python writes, etc.)
            if (matchingRule is null && tc.ToolName is "run_bash_command")
            {
                var argsLower = tc.Arguments.ToLowerInvariant();
                if (FileWritingPatterns.Any(p => argsLower.Contains(p)))
                {
                    matchingRule = new VerificationRule("run_bash_command", ["read_file"], WithinSteps: 2);
                }
            }

            if (matchingRule is not null)
            {
                _pending.Add(new PendingVerification(matchingRule, stepNumber, tc.ToolName));
                _totalEdits++;
            }
        }

        return reminder;
    }

    /// <summary>
    /// Get compliance statistics for the session.
    /// </summary>
    public VerificationReport GetStats() => new()
    {
        TotalEdits = _totalEdits,
        VerifiedEdits = _verifiedEdits,
        PendingVerifications = _pending.Count,
        ComplianceRate = _totalEdits > 0 ? (double)_verifiedEdits / _totalEdits : 1.0,
    };

    private static string BuildReminder(PendingVerification pending, int currentStep)
    {
        var followUps = string.Join(" or ", pending.Rule.RequiredFollowUps);
        return $"⚠️ You modified a file at step {pending.TriggerStep} ({pending.ToolUsed}) "
            + $"but haven't verified with {followUps} in {currentStep - pending.TriggerStep} steps. "
            + "Read back the changed region to confirm the edit applied correctly, then run tests if applicable.";
    }

    private sealed record PendingVerification(VerificationRule Rule, int TriggerStep, string ToolUsed);
}

/// <summary>
/// Defines what verification follow-ups are expected after a specific tool type.
/// </summary>
public sealed record VerificationRule(
    string TriggerTool,
    string[] RequiredFollowUps,
    int WithinSteps);

/// <summary>
/// Verification compliance statistics for a session.
/// </summary>
public sealed record VerificationReport
{
    /// <summary>
    /// The total number of file-modifying actions recorded.
    /// </summary>
    public int TotalEdits { get; init; }

    /// <summary>
    /// The number of edits that received the expected verification follow-up.
    /// </summary>
    public int VerifiedEdits { get; init; }

    /// <summary>
    /// The number of verification follow-ups that are still pending.
    /// </summary>
    public int PendingVerifications { get; init; }

    /// <summary>
    /// The fraction of edits that were verified.
    /// </summary>
    public double ComplianceRate { get; init; }
}
