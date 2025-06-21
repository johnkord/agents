using System.Text.RegularExpressions;

namespace McpServer.Tools;

/// <summary>
/// Shared infrastructure for codebase search and exploration tools.
/// Centralizes constants, patterns, and utilities to prevent duplication
/// between SearchCodebaseTool and ExploreCodebaseTool.
/// </summary>
public static class CodeSearchUtils
{
    public static readonly HashSet<string> SkipDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "node_modules", ".git", ".vs", "packages", "sessions", ".venv",
        "papers", "dist", "build", "out", "__pycache__", ".mypy_cache", ".pytest_cache",
    };

    public static readonly HashSet<string> CodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".ts", ".tsx", ".js", ".jsx", ".py", ".rs", ".go", ".java", ".kt",
        ".rb", ".swift", ".c", ".cpp", ".h", ".hpp", ".fs", ".fsx",
        ".json", ".yaml", ".yml", ".toml", ".xml", ".csproj", ".sln",
        ".md", ".txt", ".sh", ".bash", ".ps1", ".sql",
    };

    public static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "is", "are", "was", "were", "be", "been",
        "have", "has", "had", "do", "does", "did", "will", "would",
        "could", "should", "may", "can", "to", "of", "in", "for",
        "on", "with", "at", "by", "from", "as", "and", "or", "not",
        "this", "that", "what", "where", "how", "find", "look",
        "search", "get", "all", "any", "each", "which", "who",
    };

    /// <summary>
    /// Matches structural declarations across multiple languages.
    /// C#: public/private/class/interface/struct/enum/record/namespace
    /// Python: def, JS/TS: function/export, Rust: fn, Go: func
    /// Uses \b word boundary instead of trailing spaces (fixes multi-language matching).
    /// </summary>
    public static readonly Regex StructurePattern = new(
        @"^\s*(public|private|protected|internal|static|abstract|sealed|async|override|virtual|partial|export|def|fn|func|function|class|interface|struct|enum|record|trait|type|module|namespace)\b",
        RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>
    /// Extract meaningful search terms from a query, filtering stop words.
    /// </summary>
    public static List<string> ExtractTerms(string text)
    {
        return Regex.Split(text.ToLowerInvariant(), @"[^a-z0-9_-]+")
            .Where(w => w.Length >= 2 && !StopWords.Contains(w))
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Enumerate code files under a root directory, skipping build output and VCS directories.
    /// </summary>
    public static IEnumerable<string> EnumerateCodeFiles(string root)
    {
        return Directory.EnumerateFiles(root, "*", new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
        })
        .Where(f =>
        {
            var relative = Path.GetRelativePath(root, f);
            var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (parts.Any(p => SkipDirs.Contains(p))) return false;
            return CodeExtensions.Contains(Path.GetExtension(f));
        });
    }

    /// <summary>
    /// Extract structural declarations (classes, methods, namespaces) from file lines.
    /// </summary>
    public static List<(int LineNum, string Declaration)> ExtractStructure(string[] lines)
    {
        var results = new List<(int, string)>();
        for (int i = 0; i < lines.Length; i++)
        {
            if (StructurePattern.IsMatch(lines[i]))
            {
                var decl = lines[i].Trim();
                if (decl.Length > 100) decl = decl[..100] + "...";
                results.Add((i + 1, decl));
            }
        }
        return results;
    }
}

/// <summary>
/// Structured search result returned by SearchCodebaseTool.SearchInternal().
/// Public so ExploreCodebaseTool can consume it directly without text parsing.
/// </summary>
public sealed class CodeSearchResult
{
    public string RelativePath { get; set; } = "";
    public int Score { get; set; }
    public HashSet<string> MatchedTerms { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<(int LineNum, string Text)> MatchLines { get; set; } = [];
    public List<string> NearbyStructure { get; set; } = [];
}
