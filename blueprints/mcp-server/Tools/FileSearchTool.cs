using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpServer.Tools;

[McpServerToolType]
public static class FileSearchTool
{
    private static readonly HashSet<string> SkipDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", "bin", "obj", "node_modules", ".venv", "packages", "sessions",
    };

    [McpServerTool, Description("Searches for files by name using a glob-like pattern. Returns matching file paths relative to the root directory. Use '**' for recursive matching, '*' for wildcards.")]
    public static string FileSearch(
        [Description("Glob pattern to match against file paths (e.g. '**/*.cs', 'src/**/Program.cs', '*.json').")] string query,
        [Description("The root directory to search in. Optional — defaults to the server's current directory.")] string? rootPath = null,
        [Description("Maximum number of results to return. Optional, defaults to 50.")] int? maxResults = null)
    {
        var root = rootPath ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(root))
            return $"Error: Directory not found at '{root}'.";

        var limit = maxResults ?? 50;

        // Convert simple glob to a search pattern
        // Support: *.cs, **/*.cs, src/**/Program.cs, etc.
        var pattern = query.Replace("**/", "").Replace("**", "");
        if (string.IsNullOrEmpty(pattern)) pattern = "*";

        var results = new List<string>();
        try
        {
            SearchDirectory(root, root, query, pattern, results, limit);
        }
        catch (Exception ex)
        {
            return $"Error: Unable to search '{root}'. {ex.Message}";
        }

        if (results.Count == 0)
            return $"No files found matching '{query}' in '{root}'.";

        var header = results.Count >= limit
            ? $"Showing {limit} of {limit}+ matches:\n"
            : $"{results.Count} file(s) found:\n";

        return header + string.Join("\n", results);
    }

    private static void SearchDirectory(string root, string current, string glob, string filePattern,
        List<string> results, int limit)
    {
        if (results.Count >= limit) return;

        var dirName = Path.GetFileName(current);
        if (SkipDirs.Contains(dirName)) return;

        // Match files in current directory
        try
        {
            foreach (var file in Directory.GetFiles(current, filePattern))
            {
                if (results.Count >= limit) return;
                var relative = Path.GetRelativePath(root, file);
                if (glob.Contains("**/") || MatchesGlob(relative, glob))
                    results.Add(relative);
            }
        }
        catch { /* permission denied, etc. */ }

        // Recurse into subdirectories if glob pattern is recursive
        if (glob.Contains("**") || glob.Contains("/"))
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(current))
                {
                    if (results.Count >= limit) return;
                    SearchDirectory(root, dir, glob, filePattern, results, limit);
                }
            }
            catch { /* permission denied */ }
        }
    }

    private static bool MatchesGlob(string path, string glob)
    {
        // Simple glob matching — if the glob has a directory component, match the full path
        // Otherwise just match the filename
        if (glob.Contains('/'))
        {
            return path.Contains(glob.Replace("*", ""), StringComparison.OrdinalIgnoreCase);
        }
        return true; // already matched by Directory.GetFiles pattern
    }
}
