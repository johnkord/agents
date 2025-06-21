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
    /// Tracks which file paths have been read, and at which step.
    /// Used to detect re-reads of files that are likely still in compressed context
    /// (sticky file summaries provide breadcrumbs, so the agent may not need to re-read).
    /// Research basis: FuseSearch (arXiv:2601.19568) — tool efficiency = unique info gain / invocation count.
    /// </summary>
    private readonly Dictionary<string, int> _filesRead = new(StringComparer.OrdinalIgnoreCase);

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
    /// </summary>
    public void RecordFileRead(string filePath, int stepNumber)
    {
        _filesRead[filePath] = stepNumber;
    }

    /// <summary>
    /// Invalidate the read cache for a specific file (e.g., after editing it).
    /// </summary>
    public void InvalidateFileRead(string filePath)
    {
        _filesRead.Remove(filePath);
    }

    /// <summary>
    /// Check if a read_file call targets a file that was already read at an earlier step.
    /// Returns a hint if the file was previously read, null otherwise.
    /// The hint informs the agent that this content was seen before, and the sticky summary
    /// in the compressed context may be sufficient — reducing unnecessary re-reads.
    /// </summary>
    public string? CheckFileReRead(string filePath)
    {
        if (_filesRead.TryGetValue(filePath, out var previousStep))
        {
            return $"Note: This file was previously read at step {previousStep}. "
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
