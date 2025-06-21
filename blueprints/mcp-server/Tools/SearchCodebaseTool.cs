using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;

namespace McpServer.Tools;

/// <summary>
/// Iterative codebase exploration combining file pattern matching, text search,
/// and structure analysis. Returns a navigational map of relevant locations.
///
/// Research basis:
///   - Agent Skills Architecture (2026) SAGE: "sequential rollout"
///   - AriadneMem (2026): "algorithmic bridge discovery"
///   - SWE-Adept: effective search combines filename, text, and semantic patterns
/// </summary>
[McpServerToolType]
public static class SearchCodebaseTool
{
    private const int MaxResults = 30;
    private const int MaxFilesPerPass = 500;

    [McpServerTool, Description(
        "Search the codebase using multi-pass file matching, content search, and " +
        "structure analysis. Returns a navigational map of relevant files with code structure context. " +
        "More thorough than grep_search for understanding WHERE code lives and HOW it connects.")]
    public static string SearchCodebase(
        [Description("What you're looking for (e.g. 'authentication middleware', 'database connection setup').")] string query,
        [Description("Short 3-5 word label (e.g. 'Find auth handlers').")] string description,
        [Description("Optional 2-3 sentence description of the search objective and what you expect to find.")] string? details = null,
        [Description("Root directory to search. Defaults to current directory.")] string? rootPath = null)
    {
        var root = rootPath ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(root))
            return $"Error: Directory not found: '{root}'.";

        var (ranked, bridges, queryTerms) = SearchInternal(root, query, details);

        if (ranked.Count == 0)
            return $"No results found for: {query}\nSearch terms: {string.Join(", ", queryTerms)}\nRoot: {root}";

        // Format results for MCP text output
        var sb = new StringBuilder();
        sb.AppendLine($"Search: \"{description}\"");
        sb.AppendLine($"Query: {query}");
        sb.AppendLine($"Terms: {string.Join(", ", queryTerms)}");
        sb.AppendLine($"Found {ranked.Count} relevant files (showing top {Math.Min(ranked.Count, MaxResults)}):\n");

        foreach (var r in ranked.Take(MaxResults))
        {
            sb.AppendLine($"  {r.RelativePath} (score: {r.Score}, matches: {string.Join(", ", r.MatchedTerms)})");

            if (r.MatchLines.Count > 0)
            {
                foreach (var (lineNum, lineText) in r.MatchLines.Take(3))
                {
                    var preview = lineText.Length > 120 ? lineText[..120] + "..." : lineText;
                    sb.AppendLine($"    L{lineNum}: {preview.Trim()}");
                }
            }

            if (r.NearbyStructure.Count > 0)
                sb.AppendLine($"    Structure: {string.Join(", ", r.NearbyStructure.Take(5))}");

            sb.AppendLine();
        }

        if (bridges.Count > 0)
        {
            sb.AppendLine("Potential bridge files (connect multiple matched components):");
            foreach (var (file, connections) in bridges.Take(5))
                sb.AppendLine($"  {file} (connects: {string.Join(", ", connections)})");
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Internal structured search. Returns ranked results + bridge files.
    /// Used by ExploreCodebaseTool to avoid fragile text parsing.
    /// </summary>
    public static (List<CodeSearchResult> Ranked, List<(string File, List<string> Connections)> Bridges, List<string> QueryTerms)
        SearchInternal(string root, string query, string? details = null)
    {
        var queryTerms = CodeSearchUtils.ExtractTerms(query + " " + (details ?? ""));
        if (queryTerms.Count == 0)
            return ([], [], queryTerms);

        var results = new Dictionary<string, CodeSearchResult>(StringComparer.OrdinalIgnoreCase);

        // ── Pass 1: File name matching ────────────────────────────────────
        FileNamePass(root, queryTerms, results);

        // ── Pass 2: Content search across code files ──────────────────────
        ContentPass(root, queryTerms, results);

        // ── Pass 3: Structure extraction for top results ──────────────────
        StructurePass(root, results);

        if (results.Count == 0)
            return ([], [], queryTerms);

        var ranked = results.Values
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.RelativePath)
            .ToList();

        var bridges = FindBridgeFiles(root, ranked.Take(MaxResults).ToList());

        return (ranked, bridges, queryTerms);
    }

    // ── Search passes ──────────────────────────────────────────────────────

    private static void FileNamePass(string root, List<string> terms, Dictionary<string, CodeSearchResult> results)
    {
        int checked_ = 0;
        foreach (var file in CodeSearchUtils.EnumerateCodeFiles(root))
        {
            if (++checked_ > MaxFilesPerPass) break;

            var relative = Path.GetRelativePath(root, file);
            var fileName = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();

            foreach (var term in terms)
            {
                if (fileName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    relative.Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    var result = GetOrAdd(results, relative);
                    result.Score += 3; // File name matches are high-signal
                    result.MatchedTerms.Add(term);
                }
            }
        }
    }

    private static void ContentPass(string root, List<string> terms, Dictionary<string, CodeSearchResult> results)
    {
        int checked_ = 0;
        foreach (var file in CodeSearchUtils.EnumerateCodeFiles(root))
        {
            if (++checked_ > MaxFilesPerPass) break;

            var relative = Path.GetRelativePath(root, file);

            string[] lines;
            try { lines = File.ReadAllLines(file); }
            catch { continue; }

            var matchedTermsInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < lines.Length; i++)
            {
                foreach (var term in terms)
                {
                    if (lines[i].Contains(term, StringComparison.OrdinalIgnoreCase))
                    {
                        var result = GetOrAdd(results, relative);
                        if (matchedTermsInFile.Add(term))
                        {
                            result.Score += 2; // Content match
                            result.MatchedTerms.Add(term);
                        }
                        if (result.MatchLines.Count < 5) // Keep top 5 matching lines
                        {
                            result.MatchLines.Add((i + 1, lines[i]));
                        }
                    }
                }
            }
        }
    }

    private static void StructurePass(string root, Dictionary<string, CodeSearchResult> results)
    {
        // Only extract structure for the top-scoring files
        var topFiles = results.Values
            .OrderByDescending(r => r.Score)
            .Take(15)
            .ToList();

        foreach (var result in topFiles)
        {
            var fullPath = Path.Combine(root, result.RelativePath);
            if (!File.Exists(fullPath)) continue;

            string[] lines;
            try { lines = File.ReadAllLines(fullPath); }
            catch { continue; }

            // Find structure declarations near match lines
            var matchLineNums = result.MatchLines.Select(m => m.LineNum).ToHashSet();

            for (int i = 0; i < lines.Length; i++)
            {
                if (CodeSearchUtils.StructurePattern.IsMatch(lines[i]))
                {
                    var decl = lines[i].Trim();
                    // Keep if near a match (within 20 lines) or at file top
                    if (i < 5 || matchLineNums.Any(m => Math.Abs(m - (i + 1)) < 20))
                    {
                        // Extract just the declaration name
                        var shortDecl = decl.Length > 80 ? decl[..80] + "..." : decl;
                        result.NearbyStructure.Add(shortDecl);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Find "bridge" files — files that import/reference types from multiple matched files.
    /// These are high-value for understanding how components connect.
    /// </summary>
    private static List<(string File, List<string> Connections)> FindBridgeFiles(
        string root, List<CodeSearchResult> topResults)
    {
        if (topResults.Count < 2) return [];

        // Extract likely type/module names from top results
        var componentNames = topResults
            .Select(r => Path.GetFileNameWithoutExtension(r.RelativePath))
            .Where(n => n.Length > 2)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var bridges = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var topPaths = topResults.Select(r => r.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int checked_ = 0;

        foreach (var file in CodeSearchUtils.EnumerateCodeFiles(root))
        {
            if (++checked_ > MaxFilesPerPass / 2) break;

            var relative = Path.GetRelativePath(root, file);
            if (topPaths.Contains(relative)) continue; // skip files already in results

            string content;
            try { content = File.ReadAllText(file); }
            catch { continue; }

            var referencedComponents = componentNames
                .Where(name => content.Contains(name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (referencedComponents.Count >= 2)
            {
                bridges[relative] = referencedComponents;
            }
        }

        return bridges
            .OrderByDescending(kv => kv.Value.Count)
            .Select(kv => (kv.Key, kv.Value))
            .ToList();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static CodeSearchResult GetOrAdd(Dictionary<string, CodeSearchResult> dict, string key)
    {
        if (!dict.TryGetValue(key, out var value))
        {
            value = new CodeSearchResult { RelativePath = key };
            dict[key] = value;
        }
        return value;
    }
}
