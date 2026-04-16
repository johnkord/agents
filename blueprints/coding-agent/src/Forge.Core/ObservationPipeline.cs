using System.Text.RegularExpressions;

namespace Forge.Core;

/// <summary>
/// Processes raw tool output before it enters the conversation history.
/// Implements size gating and error compaction from the design's Observation Pipeline.
///
/// Why: Phase 1 data showed prompt tokens are 95-99% of cost. Every character
/// of tool output that enters the message list stays there for ALL subsequent
/// LLM calls. A single large file read can dominate the entire context budget.
/// </summary>
public static class ObservationPipeline
{
    /// <summary>
    /// Outcome of <see cref="Process"/>, including the processed text plus
    /// structured metadata about what size-gating action (if any) was applied.
    /// </summary>
    public readonly record struct ObservationResult(string Text, string? SpillPath, bool WasTruncated)
    {
        /// <summary>Classifies the outcome for logging/analysis: <c>"spilled"</c>, <c>"truncated"</c>, or null.</summary>
        public string? Tag =>
            SpillPath is not null ? "spilled"
            : WasTruncated ? "truncated"
            : null;

        /// <summary>Implicit conversion so legacy callers that expected a string still compile.</summary>
        public static implicit operator string(ObservationResult r) => r.Text;
    }

    /// <summary>
    /// Process a raw tool result, applying size gating and formatting.
    /// Returns the (possibly truncated) text that should enter the conversation
    /// along with structured metadata about what action was applied. When
    /// <paramref name="spillStore"/> is provided AND the raw result exceeds
    /// <see cref="AgentOptions.ToolResultSpillThresholdChars"/>, the full output
    /// is persisted to disk and the truncation footer points at the spill path.
    /// </summary>
    public static ObservationResult Process(
        string toolName,
        string rawResult,
        AgentOptions options,
        ToolResultSpillStore? spillStore = null)
    {
        if (string.IsNullOrEmpty(rawResult))
            return new ObservationResult("(no output)", null, false);

        var maxChars = Math.Max(1, options.ObservationMaxChars);
        var maxLines = Math.Max(1, options.ObservationMaxLines);

        // Error compaction: if it looks like a stack trace, compact it
        if (IsStackTrace(rawResult))
            return new ObservationResult(CompactStackTrace(rawResult), null, false);

        // Spill large raw results to disk BEFORE truncation so the agent can retrieve arbitrary slices.
        string? spillPath = null;
        if (spillStore is not null
            && options.ToolResultSpillThresholdChars > 0
            && rawResult.Length > options.ToolResultSpillThresholdChars)
        {
            try { spillPath = spillStore.Spill(toolName, rawResult); }
            catch { /* spill is best-effort; fall through to normal truncation */ }
        }

        // Size gate by character count
        if (rawResult.Length > maxChars)
        {
            var truncated = rawResult[..maxChars];
            var totalLines = rawResult.AsSpan().Count('\n') + 1;
            var pointer = spillPath is not null
                ? $"Full output saved to: {spillPath}. Use read_file on this path with startLine/endLine for specific sections."
                : "Use read_file with startLine/endLine for specific sections.";
            var text = $"{truncated}\n\n... truncated ({rawResult.Length:N0} total characters, {totalLines} total lines). {pointer}";
            return new ObservationResult(text, spillPath, WasTruncated: true);
        }

        // Size gate by line count
        var lines = rawResult.Split('\n');
        if (lines.Length > maxLines)
        {
            var kept = string.Join('\n', lines[..maxLines]);
            var pointer = spillPath is not null
                ? $"Full output saved to: {spillPath}. Use read_file on this path with startLine/endLine for specific sections."
                : "Use read_file with startLine/endLine for specific sections.";
            var text = $"{kept}\n\n... truncated (showing {maxLines} of {lines.Length} lines). {pointer}";
            return new ObservationResult(text, spillPath, WasTruncated: true);
        }

        // Below both size gates — but a spill might still have happened if the raw
        // exceeded the spill threshold but not the observation cap. Carry the path
        // through so it's still tagged "spilled".
        return new ObservationResult(rawResult, spillPath, WasTruncated: false);
    }

    private static bool IsStackTrace(string text)
    {
        // Require "SomeException:" pattern (not just the word "Exception" in code/docs)
        if (!Regex.IsMatch(text, @"\w+Exception\s*:"))
            return false;
        var lines = text.Split('\n');
        return lines.Count(l => l.TrimStart().StartsWith("at ")) >= 3;
    }

    /// <summary>
    /// Compacts a stack trace into: type, message, and the first 5 frames.
    /// ~100 tokens vs ~2000 for a typical .NET stack trace.
    /// </summary>
    private static string CompactStackTrace(string text)
    {
        var lines = text.Split('\n');

        // Extract the exception line (usually first non-empty line or the one containing "Exception")
        var exceptionLine = lines.FirstOrDefault(l => l.Contains("Exception")) ?? lines[0];

        // Extract frame lines (start with "   at ")
        var frames = lines.Where(l => l.TrimStart().StartsWith("at ")).Take(5).ToList();

        var result = $"EXCEPTION: {exceptionLine.Trim()}";
        if (frames.Count > 0)
        {
            result += "\nStack (top 5 frames):";
            foreach (var frame in frames)
                result += $"\n  {frame.Trim()}";

            var totalFrames = lines.Count(l => l.TrimStart().StartsWith("at "));
            if (totalFrames > 5)
                result += $"\n  ... ({totalFrames - 5} more frames)";
        }

        return result;
    }
}
