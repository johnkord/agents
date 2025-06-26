using System;

namespace MCPServer.ToolApproval;

/// <summary>
/// Configuration for tool approval providers
/// </summary>
public class ApprovalProviderConfiguration
{
    /// <summary>
    /// The type of approval provider to use
    /// </summary>
    public ApprovalProviderType ProviderType { get; set; } = ApprovalProviderType.Console;

    /// <summary>
    /// Configuration for file-based approval provider
    /// </summary>
    public FileProviderConfig? FileProvider { get; set; }

    /// <summary>
    /// Configuration for REST-based approval provider
    /// </summary>
    public RestProviderConfig? RestProvider { get; set; }

    /// <summary>
    /// Configuration for LLM-based approval provider
    /// </summary>
    public LlmProviderConfig? LlmProvider { get; set; }
}

/// <summary>
/// Types of approval providers
/// </summary>
public enum ApprovalProviderType
{
    Console,
    File,
    Rest,
    Llm
}

/// <summary>
/// Configuration for file-based approval provider
/// </summary>
public class FileProviderConfig
{
    public string ApprovalDirectory { get; set; } = "./approvals";
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// Configuration for REST-based approval provider
/// </summary>
public class RestProviderConfig
{
    public string BaseUrl { get; set; } = "http://localhost:5000";
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(10);
    public string? AuthToken { get; set; }
}

/// <summary>
/// Configuration for LLM-based approval provider
/// </summary>
public class LlmProviderConfig
{
    /// <summary>
    /// Type of LLM service to use
    /// </summary>
    public LlmServiceType ServiceType { get; set; } = LlmServiceType.Mock;

    /// <summary>
    /// API key for the LLM service
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Base URL for the LLM service (for custom endpoints)
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Model name to use (e.g., "gpt-4", "gpt-3.5-turbo")
    /// </summary>
    public string Model { get; set; } = "gpt-4";

    /// <summary>
    /// Minimum confidence required for auto-approval
    /// </summary>
    public double AutoApprovalMinConfidence { get; set; } = 0.85;

    /// <summary>
    /// Maximum confidence for requiring human approval
    /// Below this threshold, human approval is required
    /// </summary>
    public double HumanFallbackMaxConfidence { get; set; } = 0.50;

    /// <summary>
    /// Enable caching of LLM decisions
    /// </summary>
    public bool CacheEnabled { get; set; } = true;

    /// <summary>
    /// Cache time-to-live for decisions
    /// </summary>
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Timeout for LLM requests
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Fallback to human approval when LLM is unavailable
    /// </summary>
    public bool FallbackToHuman { get; set; } = true;
}

/// <summary>
/// Types of LLM services
/// </summary>
public enum LlmServiceType
{
    Mock,
    OpenAI,
    AzureOpenAI,
    Anthropic,
    Custom
}

/// <summary>
/// Extension methods for ApprovalProviderConfiguration
/// </summary>
public static class ApprovalProviderConfigurationExtensions
{
    /// <summary>
    /// Load configuration from environment variables (overrides defaults).
    /// </summary>
    public static ApprovalProviderConfiguration FromEnvironment()
    {
        var config = new ApprovalProviderConfiguration();
        string? v;

        // Provider type
        if ((v = Environment.GetEnvironmentVariable("APPROVAL_PROVIDER_TYPE")) != null &&
            Enum.TryParse<ApprovalProviderType>(v, true, out var pt))
        {
            config.ProviderType = pt;
        }

        // File provider settings
        config.FileProvider ??= new FileProviderConfig();
        if ((v = Environment.GetEnvironmentVariable("FILE_APPROVAL_DIRECTORY")) != null)
            config.FileProvider.ApprovalDirectory = v;
        if ((v = Environment.GetEnvironmentVariable("FILE_APPROVAL_POLL_INTERVAL")) != null)
            config.FileProvider.PollInterval = TimeSpan.Parse(v);
        if ((v = Environment.GetEnvironmentVariable("FILE_APPROVAL_TIMEOUT")) != null)
            config.FileProvider.Timeout = TimeSpan.Parse(v);

        // REST provider settings
        config.RestProvider ??= new RestProviderConfig();
        if ((v = Environment.GetEnvironmentVariable("REST_BASE_URL")) != null)
            config.RestProvider.BaseUrl = v;
        if ((v = Environment.GetEnvironmentVariable("REST_POLL_INTERVAL")) != null)
            config.RestProvider.PollInterval = TimeSpan.Parse(v);
        if ((v = Environment.GetEnvironmentVariable("REST_TIMEOUT")) != null)
            config.RestProvider.Timeout = TimeSpan.Parse(v);
        if ((v = Environment.GetEnvironmentVariable("REST_AUTH_TOKEN")) != null)
            config.RestProvider.AuthToken = v;

        // LLM provider settings
        config.LlmProvider ??= new LlmProviderConfig();
        if ((v = Environment.GetEnvironmentVariable("LLM_SERVICE_TYPE")) != null &&
            Enum.TryParse<LlmServiceType>(v, true, out var llmType))
        {
            config.LlmProvider.ServiceType = llmType;
        }
        if ((v = Environment.GetEnvironmentVariable("LLM_API_KEY")) != null)
            config.LlmProvider.ApiKey = v;
        if ((v = Environment.GetEnvironmentVariable("LLM_BASE_URL")) != null)
            config.LlmProvider.BaseUrl = v;
        if ((v = Environment.GetEnvironmentVariable("LLM_MODEL")) != null)
            config.LlmProvider.Model = v;
        if ((v = Environment.GetEnvironmentVariable("LLM_AUTO_APPROVAL_MIN_CONFIDENCE")) != null)
            config.LlmProvider.AutoApprovalMinConfidence = double.Parse(v);
        if ((v = Environment.GetEnvironmentVariable("LLM_HUMAN_FALLBACK_MAX_CONFIDENCE")) != null)
            config.LlmProvider.HumanFallbackMaxConfidence = double.Parse(v);
        if ((v = Environment.GetEnvironmentVariable("LLM_CACHE_ENABLED")) != null)
            config.LlmProvider.CacheEnabled = bool.Parse(v);
        if ((v = Environment.GetEnvironmentVariable("LLM_CACHE_TTL")) != null)
            config.LlmProvider.CacheTtl = TimeSpan.Parse(v);
        if ((v = Environment.GetEnvironmentVariable("LLM_TIMEOUT")) != null)
            config.LlmProvider.Timeout = TimeSpan.Parse(v);
        if ((v = Environment.GetEnvironmentVariable("LLM_FALLBACK_TO_HUMAN")) != null)
            config.LlmProvider.FallbackToHuman = bool.Parse(v);

        return config;
    }
}