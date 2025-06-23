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
}

/// <summary>
/// Types of approval providers
/// </summary>
public enum ApprovalProviderType
{
    Console,
    File,
    Rest
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

        return config;
    }
}