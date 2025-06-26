using System;
using System.Net.Http;
using MCPServer.ToolApproval.LlmApproval;

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
            
            ApprovalProviderType.Llm => CreateLlmProvider(config.LlmProvider),
            
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

    private static LlmApprovalProvider CreateLlmProvider(LlmProviderConfig? llmConfig)
    {
        var config = llmConfig ?? new LlmProviderConfig();
        
        // Create the appropriate LLM service
        ILlmService llmService = config.ServiceType switch
        {
            LlmServiceType.Mock => new MockLlmService(),
            LlmServiceType.OpenAI => throw new NotImplementedException("OpenAI service not yet implemented"),
            LlmServiceType.AzureOpenAI => throw new NotImplementedException("Azure OpenAI service not yet implemented"),
            LlmServiceType.Anthropic => throw new NotImplementedException("Anthropic service not yet implemented"),
            LlmServiceType.Custom => throw new NotImplementedException("Custom service not yet implemented"),
            _ => throw new ArgumentException($"Unknown LLM service type: {config.ServiceType}")
        };

        // Create the approval policy
        var policy = new LlmApprovalPolicy
        {
            AutoApprovalMinConfidence = config.AutoApprovalMinConfidence,
            HumanRequiredMaxConfidence = config.HumanFallbackMaxConfidence,
            CacheEnabled = config.CacheEnabled,
            CacheTtl = config.CacheTtl,
            LlmTimeout = config.Timeout,
            FallbackToHuman = config.FallbackToHuman
        };

        // Create the fallback provider
        var fallbackProvider = new ConsoleApprovalProvider();

        return new LlmApprovalProvider(llmService, policy, fallbackProvider: fallbackProvider);
    }
}