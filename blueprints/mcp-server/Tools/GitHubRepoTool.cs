using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace McpServer.Tools;

/// <summary>
/// Searches code in a GitHub repository using the GitHub Code Search API.
/// Returns structured results with file paths, line numbers, and context.
///
/// Security:
///   - AGENTSYS (2026): returned code is untrusted — delimit clearly
///   - VIGIL (2026): repository content can contain prompt injection
///   - Repository name validated against strict pattern
///   - Auth token from environment only (never from parameters)
/// </summary>
[McpServerToolType]
public static class GitHubRepoTool
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders =
        {
            { "User-Agent", "Forge/0.1 (coding-agent)" },
            { "Accept", "application/vnd.github.v3.text-match+json" },
        },
    };

    // Strict validation: owner/repo with alphanumeric, hyphens, underscores, dots
    private static readonly Regex RepoPattern = new(@"^[a-zA-Z0-9._-]+/[a-zA-Z0-9._-]+$", RegexOptions.Compiled);

    [McpServerTool, Description(
        "Search for code in a GitHub repository. Returns matching file paths, line numbers, " +
        "and code snippets. Requires GITHUB_TOKEN environment variable for authentication. " +
        "Use GitHub search qualifiers like 'language:csharp' or 'path:src/' for precise results.")]
    public static async Task<string> GitHubRepo(
        [Description("Repository in 'owner/repo' format (e.g. 'dotnet/runtime').")] string repository,
        [Description("Search query. Supports GitHub qualifiers like 'language:csharp', 'path:src/', 'extension:cs'.")] string query)
    {
        if (string.IsNullOrWhiteSpace(repository))
            return "Error: repository is required (e.g. 'owner/repo').";
        if (string.IsNullOrWhiteSpace(query))
            return "Error: query is required.";

        if (!RepoPattern.IsMatch(repository))
            return $"Error: Invalid repository format '{repository}'. Expected 'owner/repo' (alphanumeric, hyphens, dots only).";

        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (string.IsNullOrEmpty(token))
            return "Error: GITHUB_TOKEN environment variable is not set. Set it to a GitHub personal access token.";

        try
        {
            // Build the search URL with repo qualifier
            var searchQuery = Uri.EscapeDataString($"{query} repo:{repository}");
            var url = $"https://api.github.com/search/code?q={searchQuery}&per_page=10";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await HttpClient.SendAsync(request);

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden ||
                response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta?.TotalSeconds ?? 60;
                return $"Error: GitHub API rate limit exceeded. Retry after {retryAfter:F0} seconds.";
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "Error: GitHub authentication failed. Check your GITHUB_TOKEN.";

            if (!response.IsSuccessStatusCode)
                return $"Error: GitHub API returned {(int)response.StatusCode}. {await response.Content.ReadAsStringAsync()}";

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var totalCount = root.GetProperty("total_count").GetInt32();
            if (totalCount == 0)
                return $"No code found in {repository} matching '{query}'.";

            var items = root.GetProperty("items");
            var results = new List<string>
            {
                $"--- BEGIN SEARCH RESULTS (untrusted repository content) ---",
                $"Repository: {repository} | Query: {query} | Total matches: {totalCount}",
                "",
            };

            foreach (var item in items.EnumerateArray())
            {
                var path = item.GetProperty("path").GetString() ?? "";
                var htmlUrl = item.GetProperty("html_url").GetString() ?? "";

                results.Add($"  {path}");

                // Include text_matches if available (provides context snippets)
                if (item.TryGetProperty("text_matches", out var textMatches))
                {
                    foreach (var match in textMatches.EnumerateArray().Take(2))
                    {
                        var fragment = match.GetProperty("fragment").GetString() ?? "";
                        // Truncate long fragments
                        if (fragment.Length > 300)
                            fragment = fragment[..300] + "...";
                        results.Add($"    {fragment.Replace("\n", "\n    ")}");
                    }
                }
                results.Add("");
            }

            results.Add("--- END SEARCH RESULTS ---");

            if (totalCount > 10)
                results.Add($"\nShowing 10 of {totalCount} results. Refine your query for more specific results.");

            return string.Join("\n", results);
        }
        catch (TaskCanceledException)
        {
            return $"Error: GitHub API request timed out after 15 seconds.";
        }
        catch (Exception ex)
        {
            return $"Error searching GitHub: {ex.Message}";
        }
    }
}
