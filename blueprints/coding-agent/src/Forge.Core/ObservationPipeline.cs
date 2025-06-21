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
    /// Process a raw tool result, applying size gating and formatting.
    /// Returns the (possibly truncated) text that should enter the conversation.
    /// </summary>
    public static string Process(string toolName, string rawResult, AgentOptions options)
    {
        if (string.IsNullOrEmpty(rawResult))
            return "(no output)";

        var maxChars = Math.Max(1, options.ObservationMaxChars);
        var maxLines = Math.Max(1, options.ObservationMaxLines);

        // Error compaction: if it looks like a stack trace, compact it
        if (IsStackTrace(rawResult))
            return CompactStackTrace(rawResult);

        // Size gate by character count
        if (rawResult.Length > maxChars)
        {
            var truncated = rawResult[..maxChars];
            var totalLines = rawResult.AsSpan().Count('\n') + 1;
            return $"{truncated}\n\n... truncated ({rawResult.Length:N0} total characters, {totalLines} total lines). Use read_file with startLine/endLine for specific sections.";
        }

        // Size gate by line count
        var lines = rawResult.Split('\n');
        if (lines.Length > maxLines)
        {
            var kept = string.Join('\n', lines[..maxLines]);
            return $"{kept}\n\n... truncated (showing {maxLines} of {lines.Length} lines). Use read_file with startLine/endLine for specific sections.";
        }

        return rawResult;
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
