using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace McpServer.Tools;

[McpServerToolType]
public static class GrepSearchTool
{
    [McpServerTool, Description("Performs a fast text or regex search across files in a directory tree. Returns matching lines and locations.")]
    public static string GrepSearch(
        [Description("The text or regex pattern to search for.")] string query,
        [Description("Whether the query is a regex pattern.")] bool isRegexp,
        [Description("The root directory to search in. Optional — defaults to the server's current directory.")] string? rootPath = null,
        [Description("Glob pattern to filter which files to search within. Optional.")] string? includePattern = null,
        [Description("Maximum number of results to return. Optional.")] int? maxResults = null)
    {
        var workDir = rootPath ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(workDir))
        {
            return $"Error: Directory not found at '{workDir}'.";
        }
        var limit = maxResults ?? 30;
        var results = new List<string>();
        int totalMatches = 0;

        Regex? regex = null;
        if (isRegexp)
        {
            try
            {
                regex = new Regex(query, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(5));
            }
            catch (RegexParseException ex)
            {
                return $"Error: Invalid regex pattern: {ex.Message}";
            }
        }

        var files = Directory.EnumerateFiles(workDir, "*", new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
        });

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(workDir, file);

            // Skip binary-looking files and common non-code dirs
            if (ShouldSkip(relativePath)) continue;

            string[] lines;
            try { lines = File.ReadAllLines(file); }
            catch { continue; }

            for (int i = 0; i < lines.Length; i++)
            {
                bool match = regex != null
                    ? regex.IsMatch(lines[i])
                    : lines[i].Contains(query, StringComparison.OrdinalIgnoreCase);

                if (match)
                {
                    totalMatches++;
                    if (results.Count < limit)
                    {
                        results.Add($"{relativePath}:{i + 1}: {lines[i].TrimStart()}");
                    }
                }
            }
        }

        if (results.Count == 0)
        {
            return $"No matches found for '{query}'.";
        }

        var header = totalMatches > limit
            ? $"Showing {limit} of {totalMatches} matches:\n"
            : $"{totalMatches} match(es):\n";

        return header + string.Join("\n", results);
    }

    private static readonly string[] SkipDirs = ["bin/", "obj/", "node_modules/", ".git/", ".vs/", "packages/", "sessions/", ".venv/", "papers/"];
    private static readonly string[] SkipExtensions = [".dll", ".exe", ".png", ".jpg", ".gif", ".ico", ".woff", ".pdf", ".zip"];

    private static bool ShouldSkip(string path)
    {

        foreach (var dir in SkipDirs)
            if (path.Contains(dir, StringComparison.OrdinalIgnoreCase)) return true;

        foreach (var ext in SkipExtensions)
            if (path.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) return true;

        return false;
    }
}
