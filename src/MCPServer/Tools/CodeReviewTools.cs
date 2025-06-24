using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using MCPServer.ToolApproval;

namespace MCPServer.Tools;

[McpServerToolType]
public class CodeReviewTools
{
    [McpServerTool(Name = "analyze_pull_request_for_review"), Description("Analyze a pull request and provide comprehensive review insights combining file analysis with AI-powered code review.")]
    [RequiresApproval]
    public static string AnalyzePullRequestForReview(
        string platform, // "github" or "azuredevops"
        string organization,
        string project,
        string repository,
        int pullRequestId,
        string token,
        string? openaiApiKey = null,
        string? vectorStoreId = null)
    {
        var args = new Dictionary<string, object?>
        {
            ["platform"] = platform,
            ["organization"] = organization,
            ["project"] = project,
            ["repository"] = repository,
            ["pullRequestId"] = pullRequestId,
            ["token"] = "***",
            ["openaiApiKey"] = openaiApiKey != null ? "***" : null,
            ["vectorStoreId"] = vectorStoreId
        };

        var approved = ToolApprovalManager.Instance.EnsureApproved("analyze_pull_request_for_review", args);
        if (!approved)
        {
            return "Error: Tool execution was denied by approval system.";
        }

        try
        {
            var result = new StringBuilder();
            result.AppendLine($"🔍 Comprehensive Pull Request Analysis");
            result.AppendLine($"Platform: {platform.ToUpper()}");
            result.AppendLine($"Repository: {organization}/{project}/{repository}");
            result.AppendLine($"Pull Request ID: {pullRequestId}");
            result.AppendLine();

            // Get PR details based on platform
            string prDetails = "";
            string prFiles = "";
            string prComments = "";

            if (platform.ToLower() == "github")
            {
                prDetails = GitHubTools.GetPullRequest(organization, repository, pullRequestId, token);
                prFiles = GitHubTools.GetPullRequestFiles(organization, repository, pullRequestId, token);
                prComments = GitHubTools.GetPullRequestComments(organization, repository, pullRequestId, token);
            }
            else if (platform.ToLower() == "azuredevops")
            {
                prDetails = AzureDevOpsTools.GetPullRequest(organization, project, repository, pullRequestId, token);
                prFiles = AzureDevOpsTools.GetPullRequestChanges(organization, project, repository, pullRequestId, token);
                prComments = AzureDevOpsTools.GetPullRequestThreads(organization, project, repository, pullRequestId, token);
            }
            else
            {
                return $"Error: Unsupported platform '{platform}'. Supported platforms: github, azuredevops";
            }

            // Basic Analysis
            result.AppendLine("📋 PULL REQUEST DETAILS");
            result.AppendLine("=".PadRight(50, '='));
            result.AppendLine(prDetails);
            result.AppendLine();

            result.AppendLine("📁 FILES CHANGED");
            result.AppendLine("=".PadRight(50, '='));
            result.AppendLine(prFiles);
            result.AppendLine();

            result.AppendLine("💬 EXISTING COMMENTS");
            result.AppendLine("=".PadRight(50, '='));
            result.AppendLine(prComments);
            result.AppendLine();

            // Code Quality Analysis
            result.AppendLine("🔍 CODE QUALITY ANALYSIS");
            result.AppendLine("=".PadRight(50, '='));
            
            var codeAnalysis = AnalyzeCodeQuality(prFiles);
            result.AppendLine(codeAnalysis);
            result.AppendLine();

            // AI-Powered Analysis (if OpenAI key and vector store provided)
            if (!string.IsNullOrEmpty(openaiApiKey) && !string.IsNullOrEmpty(vectorStoreId))
            {
                result.AppendLine("🤖 AI-POWERED INSIGHTS");
                result.AppendLine("=".PadRight(50, '='));
                
                var aiQuery = $@"
Please analyze this pull request for potential issues, code quality improvements, and best practices:

PR Details:
{prDetails}

Files Changed:
{prFiles}

Existing Comments:
{prComments}

Provide specific, actionable feedback focusing on:
1. Code quality and maintainability
2. Potential bugs or security issues
3. Performance considerations
4. Best practices adherence
5. Documentation needs
";

                var aiAnalysis = OpenAIVectorStoreTools.QueryVectorStore(
                    vectorStoreId, 
                    aiQuery, 
                    openaiApiKey, 
                    5, 
                    "You are an expert code reviewer. Provide detailed, constructive feedback on code changes in pull requests."
                );
                result.AppendLine(aiAnalysis);
                result.AppendLine();
            }

            // Review Recommendations
            result.AppendLine("📝 REVIEW RECOMMENDATIONS");
            result.AppendLine("=".PadRight(50, '='));
            var recommendations = GenerateReviewRecommendations(prDetails, prFiles, prComments);
            result.AppendLine(recommendations);

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Error analyzing pull request: {ex.Message}";
        }
    }

    [McpServerTool(Name = "generate_review_comment"), Description("Generate a contextual review comment for a specific file and line in a pull request.")]
    [RequiresApproval]
    public static string GenerateReviewComment(
        string filePath,
        string codeSnippet,
        string issueType, // "bug", "performance", "style", "security", "maintainability"
        string description,
        string? suggestion = null)
    {
        var args = new Dictionary<string, object?>
        {
            ["filePath"] = filePath,
            ["codeSnippet"] = codeSnippet,
            ["issueType"] = issueType,
            ["description"] = description,
            ["suggestion"] = suggestion
        };

        var approved = ToolApprovalManager.Instance.EnsureApproved("generate_review_comment", args);
        if (!approved)
        {
            return "Error: Tool execution was denied by approval system.";
        }

        try
        {
            var result = new StringBuilder();
            
            // Add emoji based on issue type
            var emoji = issueType.ToLower() switch
            {
                "bug" => "🐛",
                "performance" => "⚡",
                "style" => "💅",
                "security" => "🔒",
                "maintainability" => "🔧",
                _ => "💡"
            };

            result.AppendLine($"{emoji} **{issueType.ToUpper()} ISSUE**");
            result.AppendLine();
            result.AppendLine($"**File:** `{filePath}`");
            result.AppendLine();
            result.AppendLine($"**Issue:** {description}");
            result.AppendLine();
            
            if (!string.IsNullOrEmpty(codeSnippet))
            {
                result.AppendLine("**Code:**");
                result.AppendLine("```");
                result.AppendLine(codeSnippet);
                result.AppendLine("```");
                result.AppendLine();
            }

            if (!string.IsNullOrEmpty(suggestion))
            {
                result.AppendLine("**Suggested Fix:**");
                result.AppendLine(suggestion);
                result.AppendLine();
            }

            result.AppendLine("---");
            result.AppendLine("*This comment was generated by AgentAlpha code review tools.*");

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Error generating review comment: {ex.Message}";
        }
    }

    [McpServerTool(Name = "extract_code_patterns"), Description("Extract and analyze code patterns from pull request changes for review insights.")]
    [RequiresApproval]
    public static string ExtractCodePatterns(string prFilesContent)
    {
        var args = new Dictionary<string, object?>
        {
            ["prFilesContent"] = prFilesContent?.Substring(0, Math.Min(200, prFilesContent?.Length ?? 0)) + "..."
        };

        var approved = ToolApprovalManager.Instance.EnsureApproved("extract_code_patterns", args);
        if (!approved)
        {
            return "Error: Tool execution was denied by approval system.";
        }

        try
        {
            var result = new StringBuilder();
            result.AppendLine("🔍 CODE PATTERN ANALYSIS");
            result.AppendLine();

            // Simple pattern detection (this could be enhanced with more sophisticated analysis)
            var patterns = new List<string>();

            if (prFilesContent?.Contains("TODO") == true || prFilesContent?.Contains("FIXME") == true)
            {
                patterns.Add("⚠️ Contains TODO/FIXME comments - may indicate incomplete work");
            }

            if (prFilesContent?.Contains("console.log") == true || prFilesContent?.Contains("System.Console.WriteLine") == true ||
                prFilesContent?.Contains("console.error") == true || prFilesContent?.Contains("print(") == true)
            {
                patterns.Add("🖨️ Contains debug/console output statements - consider removing for production");
            }

            if (prFilesContent?.Contains("catch") == true && prFilesContent?.Contains("throw") != true)
            {
                patterns.Add("🔧 Exception handling detected - verify proper error handling and logging");
            }

            if (prFilesContent?.Contains("async") == true || prFilesContent?.Contains("await") == true || prFilesContent?.Contains("Task") == true)
            {
                patterns.Add("⚡ Asynchronous code detected - review for proper async/await patterns");
            }

            if (prFilesContent?.Contains("string.Format") == true || prFilesContent?.Contains("String.format") == true ||
                prFilesContent?.Contains("+ \"") == true)
            {
                patterns.Add("📝 String manipulation detected - consider using string interpolation or StringBuilder for better performance");
            }

            if (prFilesContent?.Contains("SELECT") == true || prFilesContent?.Contains("INSERT") == true ||
                prFilesContent?.Contains("UPDATE") == true || prFilesContent?.Contains("DELETE") == true)
            {
                patterns.Add("🛡️ SQL operations detected - verify parameterized queries to prevent injection attacks");
            }

            if (prFilesContent?.Contains("password") == true || prFilesContent?.Contains("apikey") == true ||
                prFilesContent?.Contains("secret") == true || prFilesContent?.Contains("token") == true)
            {
                patterns.Add("🔒 Potential sensitive data detected - ensure secrets are not hardcoded");
            }

            if (patterns.Any())
            {
                result.AppendLine("**Detected Patterns:**");
                foreach (var pattern in patterns)
                {
                    result.AppendLine($"• {pattern}");
                }
            }
            else
            {
                result.AppendLine("✅ No concerning patterns detected in the code changes.");
            }

            result.AppendLine();
            result.AppendLine("**File Extensions Analysis:**");
            
            var extensions = ExtractFileExtensions(prFilesContent ?? "");
            foreach (var ext in extensions)
            {
                result.AppendLine($"• {ext.Key}: {ext.Value} files");
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Error extracting code patterns: {ex.Message}";
        }
    }

    private static string AnalyzeCodeQuality(string prFilesContent)
    {
        var result = new StringBuilder();
        
        // Count different types of changes
        var addedLines = (prFilesContent ?? "").Split('\n').Count(line => line.StartsWith("+"));
        var deletedLines = (prFilesContent ?? "").Split('\n').Count(line => line.StartsWith("-"));
        var totalLines = addedLines + deletedLines;

        result.AppendLine($"📊 **Change Statistics:**");
        result.AppendLine($"   • Added Lines: {addedLines}");
        result.AppendLine($"   • Deleted Lines: {deletedLines}");
        result.AppendLine($"   • Total Changes: {totalLines}");
        result.AppendLine();

        // Analyze change size
        if (totalLines > 500)
        {
            result.AppendLine("⚠️ **Large PR Warning:** This PR contains over 500 line changes. Consider breaking it into smaller, focused PRs for easier review.");
        }
        else if (totalLines < 10)
        {
            result.AppendLine("✅ **Small PR:** This is a focused, small change that should be easy to review.");
        }
        else
        {
            result.AppendLine("✅ **Moderate PR:** This PR has a reasonable size for code review.");
        }

        result.AppendLine();

        // Extract and analyze file extensions
        var extensions = ExtractFileExtensions(prFilesContent ?? "");
        if (extensions.Count > 5)
        {
            result.AppendLine("⚠️ **Multiple File Types:** This PR touches many different file types. Ensure changes are cohesive.");
        }

        return result.ToString();
    }

    private static string GenerateReviewRecommendations(string prDetails, string prFiles, string prComments)
    {
        var result = new StringBuilder();
        
        result.AppendLine("**Priority Actions:**");
        
        // Check if there are already comments
        if (prComments.Contains("💬") || prComments.Contains("Thread #"))
        {
            result.AppendLine("1. 💬 Review existing comments and address feedback");
        }
        else
        {
            result.AppendLine("1. 👀 This appears to be a fresh review - conduct thorough code examination");
        }

        result.AppendLine("2. 🧪 Verify that appropriate tests are included or updated");
        result.AppendLine("3. 📚 Check that documentation is updated if public APIs changed");
        result.AppendLine("4. 🔍 Review code for security vulnerabilities and performance impacts");
        result.AppendLine("5. ✅ Ensure code follows team coding standards and best practices");
        result.AppendLine();

        result.AppendLine("**Review Checklist:**");
        result.AppendLine("- [ ] Code logic is correct and handles edge cases");
        result.AppendLine("- [ ] Error handling is appropriate");
        result.AppendLine("- [ ] Performance implications are considered");
        result.AppendLine("- [ ] Security best practices are followed");
        result.AppendLine("- [ ] Code is well-documented and readable");
        result.AppendLine("- [ ] Tests provide adequate coverage");
        result.AppendLine("- [ ] Dependencies are appropriate and necessary");

        return result.ToString();
    }

    private static Dictionary<string, int> ExtractFileExtensions(string content)
    {
        var extensions = new Dictionary<string, int>();
        var lines = content.Split('\n');
        
        foreach (var line in lines)
        {
            if (line.Contains("📄") && line.Contains("."))
            {
                var parts = line.Split('.');
                if (parts.Length > 1)
                {
                    var extension = parts.Last().Split(' ')[0].ToLower();
                    if (!string.IsNullOrEmpty(extension))
                    {
                        extensions[extension] = extensions.GetValueOrDefault(extension, 0) + 1;
                    }
                }
            }
        }

        return extensions;
    }
}