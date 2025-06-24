using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using MCPServer.ToolApproval;

namespace MCPServer.Tools;

[McpServerToolType]
public class GitHubTools
{
    private static readonly HttpClient _client = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    static GitHubTools()
    {
        _client.DefaultRequestHeaders.Add("User-Agent", "AgentAlpha-MCP-Server/1.0");
        _client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
    }

    [McpServerTool(Name = "github_get_pull_request"), Description("Get details of a specific pull request from GitHub repository.")]
    [RequiresApproval]
    public static string GetPullRequest(
        string owner,
        string repo,
        int pullNumber,
        string? token = null)
    {
        var args = new Dictionary<string, object?>
        {
            ["owner"] = owner,
            ["repo"] = repo,
            ["pullNumber"] = pullNumber,
            ["token"] = token != null ? "***" : null
        };

        var approved = ToolApprovalManager.Instance.EnsureApproved("github_get_pull_request", args);
        if (!approved)
        {
            return "Error: Tool execution was denied by approval system.";
        }

        try
        {
            var url = $"https://api.github.com/repos/{owner}/{repo}/pulls/{pullNumber}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Add("Authorization", $"Bearer {token}");
            }

            var response = _client.Send(request);
            var content = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
            {
                return $"Error: GitHub API returned {response.StatusCode}: {content}";
            }

            // Parse and format the response
            var prData = JsonSerializer.Deserialize<JsonElement>(content);
            
            var result = new StringBuilder();
            result.AppendLine($"Pull Request #{pullNumber} in {owner}/{repo}");
            result.AppendLine($"Title: {prData.GetProperty("title").GetString()}");
            result.AppendLine($"State: {prData.GetProperty("state").GetString()}");
            result.AppendLine($"Author: {prData.GetProperty("user").GetProperty("login").GetString()}");
            result.AppendLine($"Created: {prData.GetProperty("created_at").GetString()}");
            result.AppendLine($"Updated: {prData.GetProperty("updated_at").GetString()}");
            
            if (prData.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.String)
            {
                result.AppendLine($"Description: {body.GetString()}");
            }
            
            result.AppendLine($"Base Branch: {prData.GetProperty("base").GetProperty("ref").GetString()}");
            result.AppendLine($"Head Branch: {prData.GetProperty("head").GetProperty("ref").GetString()}");
            result.AppendLine($"Commits: {prData.GetProperty("commits").GetInt32()}");
            result.AppendLine($"Additions: {prData.GetProperty("additions").GetInt32()}");
            result.AppendLine($"Deletions: {prData.GetProperty("deletions").GetInt32()}");
            result.AppendLine($"Changed Files: {prData.GetProperty("changed_files").GetInt32()}");

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Error getting pull request: {ex.Message}";
        }
    }

    [McpServerTool(Name = "github_get_pull_request_files"), Description("Get list of files changed in a pull request.")]
    [RequiresApproval]
    public static string GetPullRequestFiles(
        string owner,
        string repo,
        int pullNumber,
        string? token = null)
    {
        var args = new Dictionary<string, object?>
        {
            ["owner"] = owner,
            ["repo"] = repo,
            ["pullNumber"] = pullNumber,
            ["token"] = token != null ? "***" : null
        };

        var approved = ToolApprovalManager.Instance.EnsureApproved("github_get_pull_request_files", args);
        if (!approved)
        {
            return "Error: Tool execution was denied by approval system.";
        }

        try
        {
            var url = $"https://api.github.com/repos/{owner}/{repo}/pulls/{pullNumber}/files";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Add("Authorization", $"Bearer {token}");
            }

            var response = _client.Send(request);
            var content = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
            {
                return $"Error: GitHub API returned {response.StatusCode}: {content}";
            }

            var filesData = JsonSerializer.Deserialize<JsonElement[]>(content);
            if (filesData == null)
            {
                return "Error: Failed to parse GitHub API response";
            }
            
            var result = new StringBuilder();
            result.AppendLine($"Files changed in PR #{pullNumber}:");
            result.AppendLine();

            foreach (var file in filesData)
            {
                var filename = file.GetProperty("filename").GetString() ?? "";
                var status = file.GetProperty("status").GetString() ?? "";
                var additions = file.GetProperty("additions").GetInt32();
                var deletions = file.GetProperty("deletions").GetInt32();
                var changes = file.GetProperty("changes").GetInt32();

                result.AppendLine($"📄 {filename}");
                result.AppendLine($"   Status: {status}");
                result.AppendLine($"   Changes: +{additions} -{deletions} ({changes} total)");
                
                if (file.TryGetProperty("patch", out var patch) && patch.ValueKind == JsonValueKind.String)
                {
                    result.AppendLine($"   Patch preview: {patch.GetString()?.Substring(0, Math.Min(200, patch.GetString()?.Length ?? 0))}...");
                }
                result.AppendLine();
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Error getting pull request files: {ex.Message}";
        }
    }

    [McpServerTool(Name = "github_get_pull_request_diff"), Description("Get the full diff of a pull request.")]
    [RequiresApproval]
    public static string GetPullRequestDiff(
        string owner,
        string repo,
        int pullNumber,
        string? token = null)
    {
        var args = new Dictionary<string, object?>
        {
            ["owner"] = owner,
            ["repo"] = repo,
            ["pullNumber"] = pullNumber,
            ["token"] = token != null ? "***" : null
        };

        var approved = ToolApprovalManager.Instance.EnsureApproved("github_get_pull_request_diff", args);
        if (!approved)
        {
            return "Error: Tool execution was denied by approval system.";
        }

        try
        {
            var url = $"https://api.github.com/repos/{owner}/{repo}/pulls/{pullNumber}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Accept", "application/vnd.github.diff");
            
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Add("Authorization", $"Bearer {token}");
            }

            var response = _client.Send(request);
            var diff = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
            {
                return $"Error: GitHub API returned {response.StatusCode}: {diff}";
            }

            return $"Diff for PR #{pullNumber} in {owner}/{repo}:\n\n{diff}";
        }
        catch (Exception ex)
        {
            return $"Error getting pull request diff: {ex.Message}";
        }
    }

    [McpServerTool(Name = "github_get_pull_request_comments"), Description("Get comments on a pull request.")]
    [RequiresApproval]
    public static string GetPullRequestComments(
        string owner,
        string repo,
        int pullNumber,
        string? token = null)
    {
        var args = new Dictionary<string, object?>
        {
            ["owner"] = owner,
            ["repo"] = repo,
            ["pullNumber"] = pullNumber,
            ["token"] = token != null ? "***" : null
        };

        var approved = ToolApprovalManager.Instance.EnsureApproved("github_get_pull_request_comments", args);
        if (!approved)
        {
            return "Error: Tool execution was denied by approval system.";
        }

        try
        {
            var url = $"https://api.github.com/repos/{owner}/{repo}/pulls/{pullNumber}/comments";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Add("Authorization", $"Bearer {token}");
            }

            var response = _client.Send(request);
            var content = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
            {
                return $"Error: GitHub API returned {response.StatusCode}: {content}";
            }

            var commentsData = JsonSerializer.Deserialize<JsonElement[]>(content);
            if (commentsData == null)
            {
                return "Error: Failed to parse GitHub API response";
            }
            
            var result = new StringBuilder();
            result.AppendLine($"Comments on PR #{pullNumber}:");
            result.AppendLine();

            foreach (var comment in commentsData)
            {
                var author = comment.GetProperty("user").GetProperty("login").GetString() ?? "";
                var body = comment.GetProperty("body").GetString() ?? "";
                var createdAt = comment.GetProperty("created_at").GetString() ?? "";
                var path = comment.TryGetProperty("path", out var pathProp) ? pathProp.GetString() : null;
                var line = comment.TryGetProperty("line", out var lineProp) ? lineProp.GetInt32() : (int?)null;

                result.AppendLine($"💬 {author} at {createdAt}");
                if (!string.IsNullOrEmpty(path))
                {
                    result.AppendLine($"   File: {path}" + (line.HasValue ? $" (line {line})" : ""));
                }
                result.AppendLine($"   {body}");
                result.AppendLine();
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Error getting pull request comments: {ex.Message}";
        }
    }

    [McpServerTool(Name = "github_list_pull_requests"), Description("List pull requests in a repository.")]
    [RequiresApproval]
    public static string ListPullRequests(
        string owner,
        string repo,
        string state = "open",
        int page = 1,
        int perPage = 10,
        string? token = null)
    {
        var args = new Dictionary<string, object?>
        {
            ["owner"] = owner,
            ["repo"] = repo,
            ["state"] = state,
            ["page"] = page,
            ["perPage"] = perPage,
            ["token"] = token != null ? "***" : null
        };

        var approved = ToolApprovalManager.Instance.EnsureApproved("github_list_pull_requests", args);
        if (!approved)
        {
            return "Error: Tool execution was denied by approval system.";
        }

        try
        {
            var url = $"https://api.github.com/repos/{owner}/{repo}/pulls?state={state}&page={page}&per_page={perPage}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Add("Authorization", $"Bearer {token}");
            }

            var response = _client.Send(request);
            var content = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
            {
                return $"Error: GitHub API returned {response.StatusCode}: {content}";
            }

            var pullsData = JsonSerializer.Deserialize<JsonElement[]>(content);
            if (pullsData == null)
            {
                return "Error: Failed to parse GitHub API response";
            }
            
            var result = new StringBuilder();
            result.AppendLine($"Pull Requests in {owner}/{repo} (state: {state}):");
            result.AppendLine();

            foreach (var pr in pullsData)
            {
                var number = pr.GetProperty("number").GetInt32();
                var title = pr.GetProperty("title").GetString() ?? "";
                var author = pr.GetProperty("user").GetProperty("login").GetString() ?? "";
                var createdAt = pr.GetProperty("created_at").GetString() ?? "";
                var baseBranch = pr.GetProperty("base").GetProperty("ref").GetString() ?? "";
                var headBranch = pr.GetProperty("head").GetProperty("ref").GetString() ?? "";

                result.AppendLine($"#{number}: {title}");
                result.AppendLine($"   Author: {author}");
                result.AppendLine($"   Branches: {headBranch} → {baseBranch}");
                result.AppendLine($"   Created: {createdAt}");
                result.AppendLine();
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Error listing pull requests: {ex.Message}";
        }
    }

    [McpServerTool(Name = "github_post_pull_request_comment"), Description("Post a comment on a pull request.")]
    [RequiresApproval]
    public static string PostPullRequestComment(
        string owner,
        string repo,
        int pullNumber,
        string body,
        string token,
        string? path = null,
        int? line = null,
        string? side = "RIGHT")
    {
        var args = new Dictionary<string, object?>
        {
            ["owner"] = owner,
            ["repo"] = repo,
            ["pullNumber"] = pullNumber,
            ["body"] = body,
            ["token"] = "***",
            ["path"] = path,
            ["line"] = line,
            ["side"] = side
        };

        var approved = ToolApprovalManager.Instance.EnsureApproved("github_post_pull_request_comment", args);
        if (!approved)
        {
            return "Error: Tool execution was denied by approval system.";
        }

        try
        {
            var url = path != null && line != null 
                ? $"https://api.github.com/repos/{owner}/{repo}/pulls/{pullNumber}/comments"
                : $"https://api.github.com/repos/{owner}/{repo}/issues/{pullNumber}/comments";

            var commentData = new Dictionary<string, object>
            {
                ["body"] = body
            };

            if (path != null && line != null)
            {
                commentData["path"] = path;
                commentData["line"] = line.Value;
                commentData["side"] = side ?? "RIGHT";
            }

            var jsonContent = JsonSerializer.Serialize(commentData);
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
            };
            
            request.Headers.Add("Authorization", $"Bearer {token}");

            var response = _client.Send(request);
            var content = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
            {
                return $"Error: GitHub API returned {response.StatusCode}: {content}";
            }

            var responseData = JsonSerializer.Deserialize<JsonElement>(content);
            var commentId = responseData.GetProperty("id").GetInt64();

            return $"Successfully posted comment (ID: {commentId}) on PR #{pullNumber}";
        }
        catch (Exception ex)
        {
            return $"Error posting pull request comment: {ex.Message}";
        }
    }
}