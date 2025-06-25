using System;

namespace MCPServer.Configuration;

/// <summary>
/// Configuration for API credentials loaded from environment variables
/// </summary>
public class ApiCredentialsConfiguration
{
    /// <summary>
    /// GitHub access token
    /// </summary>
    public string? GitHubAccessToken { get; set; }

    /// <summary>
    /// Azure DevOps access token
    /// </summary>
    public string? AzureDevOpsAccessToken { get; set; }

    /// <summary>
    /// OpenAI API key
    /// </summary>
    public string? OpenAiApiKey { get; set; }

    /// <summary>
    /// Load configuration from environment variables
    /// </summary>
    public static ApiCredentialsConfiguration FromEnvironment()
    {
        return new ApiCredentialsConfiguration
        {
            GitHubAccessToken = Environment.GetEnvironmentVariable("GITHUB_ACCESS_TOKEN"),
            AzureDevOpsAccessToken = Environment.GetEnvironmentVariable("AZURE_DEVOPS_ACCESS_TOKEN"),
            OpenAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
        };
    }
}