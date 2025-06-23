using System;
using System.Net.Http;

namespace MCPServer.ToolApproval;

/// <summary>
/// Factory for creating approval providers based on configuration
/// </summary>
public static class ApprovalProviderFactory
{
    /// <summary>
    /// Create an approval provider based on the configuration
    /// </summary>
    /// <param name="config">The configuration to use</param>
    /// <param name="httpClient">Optional HTTP client for REST providers</param>
    /// <returns>An approval provider instance</returns>
    public static IApprovalProvider CreateProvider(ApprovalProviderConfiguration config, HttpClient? httpClient = null)
    {
        return config.ProviderType switch
        {
            ApprovalProviderType.Console => new ConsoleApprovalProvider(),
            
            ApprovalProviderType.File => new FileApprovalProvider(
                config.FileProvider?.ApprovalDirectory ?? "./approvals",
                config.FileProvider?.PollInterval ?? TimeSpan.FromSeconds(1),
                config.FileProvider?.Timeout ?? TimeSpan.FromMinutes(5)),
            
            ApprovalProviderType.Rest => CreateRestProvider(config.RestProvider, httpClient),
            
            _ => throw new ArgumentException($"Unknown approval provider type: {config.ProviderType}")
        };
    }

    private static RestApprovalProvider CreateRestProvider(RestProviderConfig? restConfig, HttpClient? httpClient)
    {
        var config = restConfig ?? new RestProviderConfig();
        
        // Configure HTTP client with auth token if provided
        if (httpClient == null)
        {
            httpClient = new HttpClient();
        }

        if (!string.IsNullOrEmpty(config.AuthToken))
        {
            httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.AuthToken);
        }

        return new RestApprovalProvider(
            config.BaseUrl,
            httpClient,
            config.PollInterval,
            config.Timeout);
    }
}