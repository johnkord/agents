namespace Forge.Core;

/// <summary>
/// Tracks what verification has already been performed in the current session.
/// Used to detect redundant verification actions (e.g., running dotnet build
/// after run_tests already passed).
///
/// Research basis:
///   - FuseSearch (arXiv:2601.19568): 34.9% redundant invocation rate in coding agents.
///     "Tool efficiency = unique information gain / invocation count."
///   - SWE-Effi (arXiv:2509.09853): "expensive failures" and "token snowball"
///     show verification overhead compounds across steps.
/// </summary>
public sealed class VerificationState
{
    /// <summary>True if compilation has been verified (via build or test pass) since the last edit.</summary>
    public bool CompilationVerified { get; private set; }

    /// <summary>True if tests have been run since the last edit.</summary>
    public bool TestsVerified { get; private set; }

    /// <summary>Step number of the last file-modifying action.</summary>
    public int? LastEditStep { get; private set; }

    /// <summary>Step number of the last successful compilation check.</summary>
    public int? LastCompilationStep { get; private set; }

    /// <summary>Step number of the last successful test run.</summary>
    public int? LastTestStep { get; private set; }

    /// <summary>
    /// Per-file read record. MtimeTicks/Size are 0 when metadata is unknown (tests or legacy callers).
    /// A later read with matching (MtimeTicks, Size) can be stubbed in place of re-sending the content.
    /// </summary>
    internal readonly record struct FileReadRecord(int Step, long MtimeTicks, long Size);

    /// <summary>
    /// Tracks which file paths have been read, and at which step + with what file metadata.
    /// Used to (a) detect re-reads and (b) replace unchanged re-read content with a stub,
    /// saving prompt tokens on subsequent turns.
    /// Research basis: FuseSearch (arXiv:2601.19568) — tool efficiency = unique info gain / invocation count.
    /// </summary>
    private readonly Dictionary<string, FileReadRecord> _filesRead = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Count of consecutive stub-returns per path. Incremented each time
    /// <see cref="TryGetReReadStub"/> emits a stub for a path; cleared on edit
    /// (see <see cref="InvalidateFileRead"/>). Used to escalate from a polite
    /// stub to a hard-block message when the agent ignores the stub (P1.4).
    /// </summary>
    private readonly Dictionary<string, int> _stubReturnCounts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns true when no file-modifying action has been recorded in this session.
    /// </summary>
    public bool IsEmpty() => !LastEditStep.HasValue;

    /// <summary>
    /// Record that a file was modified, invalidating prior verification.
    /// </summary>
    public void RecordEdit(int stepNumber)
    {
        LastEditStep = stepNumber;
        CompilationVerified = false;
        TestsVerified = false;
        // Invalidate read cache for modified files — the content has changed
        // We don't know WHICH file was edited at this point, so we don't clear all reads.
        // The caller should use InvalidateFileRead() for specific files.
    }

    /// <summary>
    /// Record that a file was read at a given step. Used to detect re-reads.
    /// Metadata unknown — cannot be used for content-stub replacement.
    /// </summary>
    public void RecordFileRead(string filePath, int stepNumber)
    {
        _filesRead[filePath] = new FileReadRecord(stepNumber, 0, 0);
    }

    /// <summary>
    /// Record that a file was read at a given step, with observed file metadata.
    /// Enables content-stub replacement on future re-reads when the file is unchanged.
    /// </summary>
    public void RecordFileRead(string filePath, int stepNumber, long mtimeTicks, long size)
    {
        _filesRead[filePath] = new FileReadRecord(stepNumber, mtimeTicks, size);
    }

    /// <summary>
    /// If the file was previously read AND the current (mtime, size) match the recorded values,
    /// returns a replacement string for the tool result. Callers REPLACE the tool-result content
    /// with the return value to save prompt tokens on all subsequent turns.
    ///
    /// Behavior ramps based on how many times the agent has ignored previous stubs on this path
    /// (P1.4). Stub returns are counted; threshold defaults to 2 — after that many stubs, the
    /// third+ re-read returns a hard-block message instead.
    /// Returns null if no prior read, metadata is unknown, or the file has changed.
    /// </summary>
    public string? TryGetReReadStub(string filePath, long currentMtimeTicks, long currentSize, int stubThresholdBeforeBlock = 2)
    {
        if (!_filesRead.TryGetValue(filePath, out var rec)) return null;
        if (rec.MtimeTicks == 0 && rec.Size == 0) return null; // metadata unknown
        if (rec.MtimeTicks != currentMtimeTicks || rec.Size != currentSize) return null;

        var priorStubs = _stubReturnCounts.TryGetValue(filePath, out var c) ? c : 0;
        _stubReturnCounts[filePath] = priorStubs + 1;

        if (priorStubs >= stubThresholdBeforeBlock)
        {
            // Hard-block: the agent has already been told N times that this re-read is redundant
            // and kept calling anyway. Escalate to a BLOCKED-style message matching ToolExecutor's
            // duplicate-detection wording so it's recognizable as a hard failure.
            return $"BLOCKED: read_file on '{filePath}' has been suppressed {priorStubs} consecutive times "
                + $"with unchanged-content stubs. This is call #{priorStubs + 1} on the same path. "
                + $"The file has not been modified since step {rec.Step}. "
                + "Stop re-reading — use the earlier result, or edit the file if you need fresh content.";
        }

        // Adversarial wording (P1.4): framed as a suppressed-call error rather than a polite
        // hint, because smoke-test data showed agents ignore hint-style stubs and re-issue the
        // same call anyway.
        return $"[read_file SUPPRESSED — identical content was returned at step {rec.Step} "
            + "(mtime/size match, no edits in between). Re-reading wastes tool calls and will not "
            + "refresh the content. If you had edited this file, the cache would have been invalidated "
            + "and you would see new content. DO NOT call read_file on this path again this session unless you edit it first.]";
    }

    /// <summary>Current stub-return count for a path. Exposed for tests + event logging.</summary>
    public int GetStubReturnCount(string filePath)
        => _stubReturnCounts.TryGetValue(filePath, out var c) ? c : 0;

    /// <summary>
    /// Invalidate the read cache for a specific file (e.g., after editing it).
    /// Also resets the stub-return counter so the next genuine re-read is not
    /// immediately hard-blocked.
    /// </summary>
    public void InvalidateFileRead(string filePath)
    {
        _filesRead.Remove(filePath);
        _stubReturnCounts.Remove(filePath);
    }

    /// <summary>
    /// Check if a read_file call targets a file that was already read at an earlier step.
    /// Returns a hint if the file was previously read, null otherwise.
    /// The hint informs the agent that this content was seen before, and the sticky summary
    /// in the compressed context may be sufficient — reducing unnecessary re-reads.
    /// </summary>
    public string? CheckFileReRead(string filePath)
    {
        if (_filesRead.TryGetValue(filePath, out var rec))
        {
            return $"Note: This file was previously read at step {rec.Step}. "
                + "Consider whether you need the full content again or if earlier context is sufficient.";
        }
        return null;
    }

    /// <summary>
    /// Record that tests passed (which also implies compilation succeeded).
    /// </summary>
    public void RecordTestsPassed(int stepNumber)
    {
        TestsVerified = true;
        CompilationVerified = true; // Tests can't pass without compilation
        LastTestStep = stepNumber;
        LastCompilationStep = stepNumber;
    }

    /// <summary>
    /// Record that a build succeeded.
    /// </summary>
    public void RecordBuildPassed(int stepNumber)
    {
        CompilationVerified = true;
        LastCompilationStep = stepNumber;
    }

    /// <summary>
    /// Check if a tool call is a redundant verification action.
    /// Returns a hint message if redundant, null otherwise.
    /// </summary>
    public string? CheckRedundancy(string toolName, string arguments)
    {
        // dotnet build after tests already passed
        if (toolName is "run_bash_command"
            && CompilationVerified
            && IsBuildCommand(arguments))
        {
            return $"Note: Compilation was already verified at step {LastCompilationStep} "
                + (TestsVerified ? "(tests passed, which confirms compilation). " : ". ")
                + "This build check is redundant unless files changed since then.";
        }

        // run_tests when tests were already run (and no edits since)
        if (toolName is "run_tests" && TestsVerified)
        {
            return $"Note: Tests already passed at step {LastTestStep} and no edits have been made since. "
                + "This test run will produce the same results.";
        }

        return null;
    }

    private static bool IsBuildCommand(string arguments)
    {
        var lower = arguments.ToLowerInvariant();
        return lower.Contains("dotnet build") || lower.Contains("dotnet msbuild")
            || lower.Contains("msbuild");
    }
}
