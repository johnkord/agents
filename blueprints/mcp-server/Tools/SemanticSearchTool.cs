using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace McpServer.Tools;

/// <summary>
/// Phase 1 semantic search: multi-keyword expansion with synonym matching.
/// Decomposes a natural language query into keywords, adds common code synonyms,
/// runs multiple grep searches, and ranks results by keyword overlap.
///
/// This is a lightweight approach per the semantic-search-design.md Phase 1
/// recommendation. True embedding-based semantic search is Phase 3.
/// </summary>
[McpServerToolType]
public static class SemanticSearchTool
{
    // Common code synonym groups — if the query contains one word, also search for its synonyms
    private static readonly Dictionary<string, string[]> Synonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["auth"] = ["login", "authenticate", "credential", "session", "token", "oauth"],
        ["login"] = ["auth", "authenticate", "credential", "sign-in"],
        ["error"] = ["exception", "catch", "throw", "fault", "failure"],
        ["exception"] = ["error", "catch", "throw", "try"],
        ["test"] = ["assert", "xunit", "fact", "theory", "spec"],
        ["config"] = ["configuration", "settings", "appsettings", "options"],
        ["settings"] = ["config", "configuration", "appsettings", "options"],
        ["database"] = ["db", "sql", "repository", "connection", "query"],
        ["db"] = ["database", "sql", "repository", "connection"],
        ["log"] = ["logging", "serilog", "logger", "trace", "debug"],
        ["http"] = ["request", "response", "api", "endpoint", "controller", "fetch"],
        ["api"] = ["http", "endpoint", "controller", "route", "rest"],
        ["cache"] = ["caching", "memory", "store", "redis"],
        ["file"] = ["read", "write", "stream", "path", "directory"],
        ["async"] = ["await", "task", "concurrent", "parallel"],
        ["retry"] = ["backoff", "resilience", "polly", "circuit"],
        ["security"] = ["auth", "encrypt", "permission", "guard", "sanitize"],
        ["validate"] = ["validation", "check", "guard", "sanitize", "verify"],
    };

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "is", "are", "was", "were", "be", "been", "being",
        "have", "has", "had", "do", "does", "did", "will", "would", "could",
        "should", "may", "might", "shall", "can", "need", "dare", "ought",
        "used", "to", "of", "in", "for", "on", "with", "at", "by", "from",
        "as", "into", "through", "during", "before", "after", "above", "below",
        "between", "out", "off", "over", "under", "again", "further", "then",
        "once", "here", "there", "when", "where", "why", "how", "all", "each",
        "every", "both", "few", "more", "most", "other", "some", "such", "only",
        "own", "same", "than", "too", "very", "just", "because", "but", "and",
        "or", "nor", "not", "so", "if", "this", "that", "these", "those", "what",
        "which", "who", "whom", "its", "it", "i", "me", "my", "we", "our",
        "you", "your", "he", "him", "his", "she", "her", "they", "them", "their",
    };

    private static readonly HashSet<string> SkipDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "node_modules", ".git", ".vs", "packages", "sessions", ".venv", "papers",
    };

    [McpServerTool, Description("Searches the codebase by meaning using natural language. Finds code matching conceptual queries like 'where is authentication handled?' or 'find the error recovery logic'. More flexible than grep_search for exploratory queries.")]
    public static string SemanticSearch(
        [Description("Natural language query describing what you're looking for.")] string query,
        [Description("Root directory to search in. Optional.")] string? rootPath = null,
        [Description("Maximum results to return. Optional, defaults to 20.")] int? maxResults = null)
    {
        var root = rootPath ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(root))
            return $"Error: Directory not found at '{root}'.";

        var limit = maxResults ?? 20;

        // Step 1: Extract keywords from the query
        var keywords = ExtractKeywords(query);
        if (keywords.Count == 0)
            return "Error: Could not extract meaningful search terms from the query.";

        // Step 2: Expand with synonyms
        var expandedTerms = ExpandWithSynonyms(keywords);

        // Step 3: Search for each term and score files by match count
        var fileScores = new Dictionary<string, (int Score, List<string> MatchedTerms, string FirstMatch)>();

        foreach (var term in expandedTerms)
        {
            SearchForTerm(root, term, fileScores, limit * 3);
        }

        if (fileScores.Count == 0)
            return $"No results found for: {query}\nSearched terms: {string.Join(", ", expandedTerms)}";

        // Step 4: Rank by score (number of distinct matched terms) and return
        var ranked = fileScores
            .OrderByDescending(kv => kv.Value.Score)
            .ThenBy(kv => kv.Key)
            .Take(limit)
            .ToList();

        var result = $"Semantic search for: \"{query}\"\n";
        result += $"Expanded terms: {string.Join(", ", expandedTerms)}\n";
        result += $"{ranked.Count} result(s):\n\n";

        foreach (var (file, (score, terms, firstMatch)) in ranked)
        {
            result += $"  {file} (matched {score} terms: {string.Join(", ", terms)})\n";
            result += $"    {firstMatch.Trim()}\n";
        }

        return result;
    }

    private static List<string> ExtractKeywords(string query)
    {
        // Split on non-alphanumeric, filter stopwords, keep words ≥ 2 chars
        return Regex.Split(query.ToLowerInvariant(), @"[^a-z0-9_-]+")
            .Where(w => w.Length >= 2 && !StopWords.Contains(w))
            .Distinct()
            .ToList();
    }

    private static HashSet<string> ExpandWithSynonyms(List<string> keywords)
    {
        var expanded = new HashSet<string>(keywords, StringComparer.OrdinalIgnoreCase);
        foreach (var keyword in keywords)
        {
            if (Synonyms.TryGetValue(keyword, out var syns))
            {
                foreach (var syn in syns.Take(3)) // limit synonym expansion
                    expanded.Add(syn);
            }
        }
        return expanded;
    }

    private static void SearchForTerm(string root, string term, Dictionary<string, (int, List<string>, string)> fileScores, int maxFilesPerTerm)
    {
        int filesChecked = 0;
        foreach (var file in Directory.EnumerateFiles(root, "*", new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
        }))
        {
            var relativePath = Path.GetRelativePath(root, file);
            if (ShouldSkip(relativePath)) continue;

            string[] lines;
            try { lines = File.ReadAllLines(file); }
            catch { continue; }

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    var key = $"{relativePath}:{i + 1}";
                    if (!fileScores.ContainsKey(relativePath))
                    {
                        fileScores[relativePath] = (1, [term], $"{i + 1}: {lines[i]}");
                    }
                    else
                    {
                        var (score, terms, firstMatch) = fileScores[relativePath];
                        if (!terms.Contains(term))
                        {
                            terms.Add(term);
                            fileScores[relativePath] = (score + 1, terms, firstMatch);
                        }
                    }
                    break; // one match per file per term is enough for ranking
                }
            }

            if (++filesChecked > maxFilesPerTerm) break;
        }
    }

    private static bool ShouldSkip(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var part in parts)
            if (SkipDirs.Contains(part)) return true;

        var ext = Path.GetExtension(path);
        return ext is ".dll" or ".exe" or ".png" or ".jpg" or ".gif" or ".pdf" or ".zip" or ".woff" or ".ico";
    }
}
