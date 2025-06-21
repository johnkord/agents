using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;

namespace McpServer.Tools;

/// <summary>
/// Compound exploration tool that combines search + file reading + structure
/// extraction into a single step. Returns a structured summary of a code area.
///
/// This is the "Tier 0 subagent" — it replaces the common pattern of calling
/// grep_search + read_file 5-10 times sequentially with a single tool call.
/// No LLM call, no child process. Pure deterministic search + formatting.
///
/// Research basis:
///   - SWE-Adept (2026): skeleton-first navigation (+5.4% localization)
///   - SWE-Pruner (2026): read ops are 76% of token cost — be surgical
///   - Theory of Code Space (2026): structured probing discovers +26 correct edges
///   - Agent Skills Architecture (2026): compound tools reduce step count
/// </summary>
[McpServerToolType]
public static class ExploreCodebaseTool
{
    private const int QuickFileLimit = 3;
    private const int MediumFileLimit = 8;
    private const int ThoroughFileLimit = 15;
    private const int MaxLinesPerFile = 60;
    private const int MaxTotalChars = 10_000;

    [McpServerTool, Description(
        "Explore an unfamiliar code area and return a structured summary. " +
        "Combines file search, content reading, and structure extraction in one step — " +
        "replaces manually searching and reading 5+ files. " +
        "Returns: relevant files with key classes/functions, matching code lines, " +
        "and structural context. " +
        "Use when you need to understand a code area but don't know which files are relevant. " +
        "Skip this when the task already names specific files — use read_file directly instead.")]
    public static string ExploreCodebase(
        [Description("What you want to understand (e.g. 'authentication flow', " +
            "'database connection setup', 'how tests are organized')")]
        string query,

        [Description("Short 3-5 word label for tracking")]
        string description,

        [Description("Optional: specific directory to focus on (defaults to workspace root)")]
        string? focusPath = null,

        [Description("Depth: 'quick' (3 files), 'medium' (8 files, default), 'thorough' (15 files)")]
        string? depth = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            return "Error: query is required.";

        var root = focusPath ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(root))
            return $"Error: Directory not found: '{root}'.";

        // Use SearchCodebaseTool's structured search (no text parsing)
        var (ranked, bridges, queryTerms) = SearchCodebaseTool.SearchInternal(root, query);

        if (ranked.Count == 0)
            return $"No results found for: {query}\nRoot: {root}";

        var fileLimit = (depth?.ToLowerInvariant()) switch
        {
            "quick" => QuickFileLimit,
            "thorough" => ThoroughFileLimit,
            _ => MediumFileLimit,
        };

        var topResults = ranked.Take(fileLimit).ToList();

        // ── SECTION 1: Compact navigational map (always survives truncation) ──
        // Research: RIG (2026) — header first, flat references, readability > compression.
        // SWE-Adept — lightweight structural returns before full content.
        // "A well-structured 1000-token context outperforms an unstructured 5000-token dump."
        var sb = new StringBuilder();
        sb.AppendLine($"# Exploration: {description}");
        sb.AppendLine($"Query: {query}");
        sb.AppendLine($"Depth: {depth ?? "medium"} ({topResults.Count} files examined)");
        sb.AppendLine();

        // Load file contents for Section 2
        var fileInfos = new List<(CodeSearchResult Result, string[] Lines, List<(int LineNum, string Decl)> Structures)>();
        foreach (var r in topResults)
        {
            var fullPath = Path.Combine(root, r.RelativePath);
            if (!File.Exists(fullPath)) continue;
            string[] lines;
            try { lines = File.ReadAllLines(fullPath); }
            catch { continue; }
            fileInfos.Add((r, lines, CodeSearchUtils.ExtractStructure(lines)));
        }

        // File map: one line per file with key structures (RIG-style flat listing)
        sb.AppendLine("## File Map");
        foreach (var (r, lines, structures) in fileInfos)
        {
            var structPreview = structures.Count > 0
                ? " → " + string.Join(", ", structures.Take(3).Select(s =>
                    s.Decl.Length > 50 ? s.Decl[..50] + "..." : s.Decl))
                : "";
            sb.AppendLine($"  {r.RelativePath} ({lines.Length} lines, score: {r.Score}){structPreview}");
        }

        // Bridge files (connections between components) — high architectural value
        if (bridges.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Potential bridge files (connect multiple matched components):");
            foreach (var (file, connections) in bridges.Take(5))
                sb.AppendLine($"  {file} (connects: {string.Join(", ", connections)})");
        }
        sb.AppendLine();

        // ── SECTION 2: Detailed code excerpts (gracefully degrades if truncated) ──
        // Research: SWE-Adept two-stage filtering — full content deferred until
        // lightweight map orients the agent. Focus (2026) — structured phases.
        sb.AppendLine("## Details");

        int totalChars = sb.Length;
        foreach (var (r, lines, structures) in fileInfos)
        {
            if (totalChars >= MaxTotalChars) break;

            var beforeLen = sb.Length;
            sb.AppendLine($"### {r.RelativePath}");

            if (structures.Count > 0)
            {
                foreach (var (lineNum, decl) in structures.Take(8))
                    sb.AppendLine($"  L{lineNum}: {decl}");
                sb.AppendLine();
            }

            var matchingRanges = FindMatchingRanges(lines, queryTerms);
            var linesShown = 0;

            foreach (var (start, end) in matchingRanges)
            {
                if (linesShown >= MaxLinesPerFile) break;
                var contextStart = Math.Max(0, start - 2);
                var contextEnd = Math.Min(lines.Length - 1, end + 2);
                if (linesShown > 0) sb.AppendLine("  ...");
                for (int i = contextStart; i <= contextEnd && linesShown < MaxLinesPerFile; i++)
                {
                    var marker = (i >= start && i <= end) ? ">" : " ";
                    var line = lines[i].Length > 120 ? lines[i][..120] + "..." : lines[i];
                    sb.AppendLine($"  {marker} L{i + 1}: {line}");
                    linesShown++;
                }
            }

            if (matchingRanges.Count == 0 && lines.Length > 0)
            {
                var previewEnd = Math.Min(lines.Length, 10);
                for (int i = 0; i < previewEnd; i++)
                {
                    var line = lines[i].Length > 120 ? lines[i][..120] + "..." : lines[i];
                    sb.AppendLine($"    L{i + 1}: {line}");
                }
                if (lines.Length > previewEnd)
                    sb.AppendLine($"    ... ({lines.Length - previewEnd} more lines)");
            }

            sb.AppendLine();
            totalChars += sb.Length - beforeLen;
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Find line ranges where query terms match, merging adjacent ranges.
    /// </summary>
    private static List<(int Start, int End)> FindMatchingRanges(string[] lines, List<string> terms)
    {
        if (terms.Count == 0) return [];

        var matchLines = new SortedSet<int>();
        for (int i = 0; i < lines.Length; i++)
        {
            var lower = lines[i].ToLowerInvariant();
            if (terms.Any(t => lower.Contains(t)))
                matchLines.Add(i);
        }

        if (matchLines.Count == 0) return [];

        // Merge adjacent lines into ranges (gap ≤ 3 lines)
        var ranges = new List<(int Start, int End)>();
        int rangeStart = -1, rangeEnd = -1;

        foreach (var line in matchLines)
        {
            if (rangeStart < 0)
            {
                rangeStart = rangeEnd = line;
            }
            else if (line - rangeEnd <= 3)
            {
                rangeEnd = line;
            }
            else
            {
                ranges.Add((rangeStart, rangeEnd));
                rangeStart = rangeEnd = line;
            }
        }
        if (rangeStart >= 0) ranges.Add((rangeStart, rangeEnd));

        return ranges;
    }
}
