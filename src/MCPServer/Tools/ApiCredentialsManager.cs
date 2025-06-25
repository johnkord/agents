using MCPServer.Configuration;

namespace MCPServer.Tools;

/// <summary>
/// Singleton manager for API credentials configuration
/// </summary>
public sealed class ApiCredentialsManager
{
    public static ApiCredentialsManager Instance { get; } = new();

    public ApiCredentialsConfiguration Configuration { get; }

    private ApiCredentialsManager()
    {
        Configuration = ApiCredentialsConfiguration.FromEnvironment();
    }

    /// <summary>
    /// Get GitHub access token, with optional fallback
    /// </summary>
    public string? GetGitHubToken(string? fallback = null)
    {
        return !string.IsNullOrEmpty(Configuration.GitHubAccessToken) 
            ? Configuration.GitHubAccessToken 
            : fallback;
    }

    /// <summary>
    /// Get Azure DevOps access token, with optional fallback
    /// </summary>
    public string? GetAzureDevOpsToken(string? fallback = null)
    {
        return !string.IsNullOrEmpty(Configuration.AzureDevOpsAccessToken) 
            ? Configuration.AzureDevOpsAccessToken 
            : fallback;
    }

    /// <summary>
    /// Get OpenAI API key, with optional fallback
    /// </summary>
    public string? GetOpenAiApiKey(string? fallback = null)
    {
        return !string.IsNullOrEmpty(Configuration.OpenAiApiKey) 
            ? Configuration.OpenAiApiKey 
            : fallback;
    }
}