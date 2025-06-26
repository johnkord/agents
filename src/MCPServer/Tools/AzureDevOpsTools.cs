using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace MCPServer.Tools;

[McpServerToolType]
public class AzureDevOpsTools
{
    private static readonly HttpClient _client = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    static AzureDevOpsTools()
    {
        _client.DefaultRequestHeaders.Add("User-Agent", "AgentAlpha-MCP-Server/1.0");
    }

    [McpServerTool(Name = "azdo_get_pull_request"), Description("Get details of a specific pull request from Azure DevOps repository.")]
    public static string GetPullRequest(
        string organization,
        string project,
        string repository,
        int pullRequestId)
    {


        try
        {
            var token = ApiCredentialsManager.Instance.GetAzureDevOpsToken();
            if (string.IsNullOrEmpty(token))
            {
                return "Error: Azure DevOps access token not configured. Please set AZURE_DEVOPS_ACCESS_TOKEN environment variable.";
            }
            var url = $"https://dev.azure.com/{organization}/{project}/_apis/git/repositories/{repository}/pullrequests/{pullRequestId}?api-version=7.0";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            
            var authBytes = Encoding.ASCII.GetBytes($":{token}");
            request.Headers.Add("Authorization", $"Basic {Convert.ToBase64String(authBytes)}");

            var response = _client.Send(request);
            var content = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
            {
                return $"Error: Azure DevOps API returned {response.StatusCode}: {content}";
            }

            var prData = JsonSerializer.Deserialize<JsonElement>(content);
            
            var result = new StringBuilder();
            result.AppendLine($"Pull Request #{pullRequestId} in {organization}/{project}/{repository}");
            result.AppendLine($"Title: {prData.GetProperty("title").GetString()}");
            result.AppendLine($"Status: {prData.GetProperty("status").GetString()}");
            
            if (prData.TryGetProperty("createdBy", out var createdBy))
            {
                result.AppendLine($"Author: {createdBy.GetProperty("displayName").GetString()}");
            }
            
            result.AppendLine($"Created: {prData.GetProperty("creationDate").GetString()}");
            
            if (prData.TryGetProperty("description", out var description) && description.ValueKind == JsonValueKind.String)
            {
                result.AppendLine($"Description: {description.GetString()}");
            }
            
            result.AppendLine($"Source Branch: {prData.GetProperty("sourceRefName").GetString()}");
            result.AppendLine($"Target Branch: {prData.GetProperty("targetRefName").GetString()}");

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Error getting pull request: {ex.Message}";
        }
    }

    [McpServerTool(Name = "azdo_get_pull_request_commits"), Description("Get commits in an Azure DevOps pull request.")]
    public static string GetPullRequestCommits(
        string organization,
        string project,
        string repository,
        int pullRequestId,
        string token)
    {


        try
        {
            var url = $"https://dev.azure.com/{organization}/{project}/_apis/git/repositories/{repository}/pullrequests/{pullRequestId}/commits?api-version=7.0";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            
            var authBytes = Encoding.ASCII.GetBytes($":{token}");
            request.Headers.Add("Authorization", $"Basic {Convert.ToBase64String(authBytes)}");

            var response = _client.Send(request);
            var content = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
            {
                return $"Error: Azure DevOps API returned {response.StatusCode}: {content}";
            }

            var commitsData = JsonSerializer.Deserialize<JsonElement>(content);
            var commits = commitsData.GetProperty("value").EnumerateArray();
            
            var result = new StringBuilder();
            result.AppendLine($"Commits in PR #{pullRequestId}:");
            result.AppendLine();

            foreach (var commit in commits)
            {
                var commitId = commit.GetProperty("commitId").GetString()?[..8]; // First 8 chars
                var comment = commit.GetProperty("comment").GetString();
                var author = commit.GetProperty("author").GetProperty("name").GetString();
                var date = commit.GetProperty("author").GetProperty("date").GetString();

                result.AppendLine($"📝 {commitId}: {comment}");
                result.AppendLine($"   Author: {author}");
                result.AppendLine($"   Date: {date}");
                result.AppendLine();
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Error getting pull request commits: {ex.Message}";
        }
    }

    [McpServerTool(Name = "azdo_get_pull_request_changes"), Description("Get file changes in an Azure DevOps pull request.")]
    public static string GetPullRequestChanges(
        string organization,
        string project,
        string repository,
        int pullRequestId,
        string token)
    {


        try
        {
            // First get the last merge source commit
            var prUrl = $"https://dev.azure.com/{organization}/{project}/_apis/git/repositories/{repository}/pullrequests/{pullRequestId}?api-version=7.0";
            using var prRequest = new HttpRequestMessage(HttpMethod.Get, prUrl);
            
            var authBytes = Encoding.ASCII.GetBytes($":{token}");
            prRequest.Headers.Add("Authorization", $"Basic {Convert.ToBase64String(authBytes)}");

            var prResponse = _client.Send(prRequest);
            var prContent = prResponse.Content.ReadAsStringAsync().Result;

            if (!prResponse.IsSuccessStatusCode)
            {
                return $"Error: Azure DevOps API returned {prResponse.StatusCode}: {prContent}";
            }

            var prData = JsonSerializer.Deserialize<JsonElement>(prContent);
            var sourceCommit = prData.GetProperty("lastMergeSourceCommit").GetProperty("commitId").GetString();
            var targetCommit = prData.GetProperty("lastMergeTargetCommit").GetProperty("commitId").GetString();

            // Now get the changes between commits
            var changesUrl = $"https://dev.azure.com/{organization}/{project}/_apis/git/repositories/{repository}/diffs/commits?baseVersionDescriptor.version={targetCommit}&targetVersionDescriptor.version={sourceCommit}&api-version=7.0";
            using var changesRequest = new HttpRequestMessage(HttpMethod.Get, changesUrl);
            changesRequest.Headers.Add("Authorization", $"Basic {Convert.ToBase64String(authBytes)}");

            var changesResponse = _client.Send(changesRequest);
            var changesContent = changesResponse.Content.ReadAsStringAsync().Result;

            if (!changesResponse.IsSuccessStatusCode)
            {
                return $"Error: Azure DevOps API returned {changesResponse.StatusCode}: {changesContent}";
            }

            var changesData = JsonSerializer.Deserialize<JsonElement>(changesContent);
            var changes = changesData.GetProperty("changes").EnumerateArray();
            
            var result = new StringBuilder();
            result.AppendLine($"File changes in PR #{pullRequestId}:");
            result.AppendLine();

            foreach (var change in changes)
            {
                var changeType = change.GetProperty("changeType").GetString();
                var item = change.GetProperty("item");
                var path = item.GetProperty("path").GetString();

                result.AppendLine($"📄 {path}");
                result.AppendLine($"   Change Type: {changeType}");
                result.AppendLine();
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Error getting pull request changes: {ex.Message}";
        }
    }

    [McpServerTool(Name = "azdo_get_pull_request_threads"), Description("Get comment threads on an Azure DevOps pull request.")]
    public static string GetPullRequestThreads(
        string organization,
        string project,
        string repository,
        int pullRequestId,
        string token)
    {


        try
        {
            var url = $"https://dev.azure.com/{organization}/{project}/_apis/git/repositories/{repository}/pullrequests/{pullRequestId}/threads?api-version=7.0";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            
            var authBytes = Encoding.ASCII.GetBytes($":{token}");
            request.Headers.Add("Authorization", $"Basic {Convert.ToBase64String(authBytes)}");

            var response = _client.Send(request);
            var content = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
            {
                return $"Error: Azure DevOps API returned {response.StatusCode}: {content}";
            }

            var threadsData = JsonSerializer.Deserialize<JsonElement>(content);
            var threads = threadsData.GetProperty("value").EnumerateArray();
            
            var result = new StringBuilder();
            result.AppendLine($"Comment threads on PR #{pullRequestId}:");
            result.AppendLine();

            foreach (var thread in threads)
            {
                var threadId = thread.GetProperty("id").GetInt32();
                var status = thread.GetProperty("status").GetString();
                
                if (thread.TryGetProperty("threadContext", out var context) && 
                    context.TryGetProperty("filePath", out var filePath))
                {
                    result.AppendLine($"🧵 Thread #{threadId} (Status: {status})");
                    result.AppendLine($"   File: {filePath.GetString()}");
                    
                    if (context.TryGetProperty("rightFileStart", out var lineStart))
                    {
                        var line = lineStart.GetProperty("line").GetInt32();
                        result.AppendLine($"   Line: {line}");
                    }
                }
                else
                {
                    result.AppendLine($"🧵 Thread #{threadId} (Status: {status}) - General comment");
                }

                if (thread.TryGetProperty("comments", out var comments))
                {
                    foreach (var comment in comments.EnumerateArray())
                    {
                        var author = comment.GetProperty("author").GetProperty("displayName").GetString();
                        var commentContent = comment.GetProperty("content").GetString();
                        var publishedDate = comment.GetProperty("publishedDate").GetString();

                        result.AppendLine($"   💬 {author} at {publishedDate}:");
                        result.AppendLine($"      {commentContent}");
                    }
                }
                result.AppendLine();
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Error getting pull request threads: {ex.Message}";
        }
    }

    [McpServerTool(Name = "azdo_list_pull_requests"), Description("List pull requests in an Azure DevOps repository.")]
    public static string ListPullRequests(
        string organization,
        string project,
        string repository,
        string status = "active",
        int top = 10,
        string token = "")
    {


        try
        {
            var url = $"https://dev.azure.com/{organization}/{project}/_apis/git/repositories/{repository}/pullrequests?searchCriteria.status={status}&$top={top}&api-version=7.0";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            
            var authBytes = Encoding.ASCII.GetBytes($":{token}");
            request.Headers.Add("Authorization", $"Basic {Convert.ToBase64String(authBytes)}");

            var response = _client.Send(request);
            var content = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
            {
                return $"Error: Azure DevOps API returned {response.StatusCode}: {content}";
            }

            var pullsData = JsonSerializer.Deserialize<JsonElement>(content);
            var pulls = pullsData.GetProperty("value").EnumerateArray();
            
            var result = new StringBuilder();
            result.AppendLine($"Pull Requests in {organization}/{project}/{repository} (status: {status}):");
            result.AppendLine();

            foreach (var pr in pulls)
            {
                var pullRequestId = pr.GetProperty("pullRequestId").GetInt32();
                var title = pr.GetProperty("title").GetString();
                var author = pr.GetProperty("createdBy").GetProperty("displayName").GetString();
                var creationDate = pr.GetProperty("creationDate").GetString();
                var sourceRefName = pr.GetProperty("sourceRefName").GetString();
                var targetRefName = pr.GetProperty("targetRefName").GetString();

                result.AppendLine($"#{pullRequestId}: {title}");
                result.AppendLine($"   Author: {author}");
                result.AppendLine($"   Branches: {sourceRefName} → {targetRefName}");
                result.AppendLine($"   Created: {creationDate}");
                result.AppendLine();
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Error listing pull requests: {ex.Message}";
        }
    }

    [McpServerTool(Name = "azdo_post_pull_request_comment"), Description("Post a comment thread on an Azure DevOps pull request.")]
    public static string PostPullRequestComment(
        string organization,
        string project,
        string repository,
        int pullRequestId,
        string content,
        string token,
        string? filePath = null,
        int? line = null)
    {


        try
        {
            var url = $"https://dev.azure.com/{organization}/{project}/_apis/git/repositories/{repository}/pullrequests/{pullRequestId}/threads?api-version=7.0";

            var threadData = new Dictionary<string, object>
            {
                ["comments"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["parentCommentId"] = 0,
                        ["content"] = content,
                        ["commentType"] = 1
                    }
                },
                ["status"] = 1  // Active
            };

            // Add thread context for file-specific comments
            if (!string.IsNullOrEmpty(filePath) && line.HasValue)
            {
                threadData["threadContext"] = new Dictionary<string, object>
                {
                    ["filePath"] = filePath,
                    ["rightFileStart"] = new Dictionary<string, object>
                    {
                        ["line"] = line.Value,
                        ["offset"] = 1
                    },
                    ["rightFileEnd"] = new Dictionary<string, object>
                    {
                        ["line"] = line.Value,
                        ["offset"] = 1
                    }
                };
            }

            var jsonContent = JsonSerializer.Serialize(threadData);
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
            };
            
            var authBytes = Encoding.ASCII.GetBytes($":{token}");
            request.Headers.Add("Authorization", $"Basic {Convert.ToBase64String(authBytes)}");

            var response = _client.Send(request);
            var responseContent = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
            {
                return $"Error: Azure DevOps API returned {response.StatusCode}: {responseContent}";
            }

            var responseData = JsonSerializer.Deserialize<JsonElement>(responseContent);
            var threadId = responseData.GetProperty("id").GetInt32();

            return $"Successfully posted comment thread (ID: {threadId}) on PR #{pullRequestId}";
        }
        catch (Exception ex)
        {
            return $"Error posting pull request comment: {ex.Message}";
        }
    }
}