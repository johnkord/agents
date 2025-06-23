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