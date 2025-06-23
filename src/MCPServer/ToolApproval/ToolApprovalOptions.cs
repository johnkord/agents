using System;

namespace MCPServer.ToolApproval;

/// <summary>
/// Configuration options for the tool approval system.
/// </summary>
public class ToolApprovalOptions
{
    /// <summary>
    /// The type of approval backend to use.
    /// </summary>
    public ApprovalBackendType BackendType { get; set; } = ApprovalBackendType.Console;

    /// <summary>
    /// Configuration for remote approval backend (when BackendType is Remote).
    /// </summary>
    public RemoteApprovalConfig? RemoteConfig { get; set; }

    /// <summary>
    /// Create tool approval options from environment variables
    /// </summary>
    public static ToolApprovalOptions FromEnvironment()
    {
        var backendType = Environment.GetEnvironmentVariable("TOOL_APPROVAL_BACKEND")?.ToLowerInvariant() switch
        {
            "remote" => ApprovalBackendType.Remote,
            "console" => ApprovalBackendType.Console,
            _ => ApprovalBackendType.Console
        };

        var options = new ToolApprovalOptions
        {
            BackendType = backendType
        };

        if (backendType == ApprovalBackendType.Remote)
        {
            options.RemoteConfig = new RemoteApprovalConfig
            {
                BaseUrl = Environment.GetEnvironmentVariable("TOOL_APPROVAL_BASE_URL") ?? "http://localhost:3001",
                ApiKey = Environment.GetEnvironmentVariable("TOOL_APPROVAL_API_KEY"),
                ApprovalTimeout = TimeSpan.FromSeconds(
                    int.TryParse(Environment.GetEnvironmentVariable("TOOL_APPROVAL_TIMEOUT"), out var timeout) 
                        ? timeout 
                        : 300)
            };
        }

        return options;
    }

    /// <summary>
    /// Creates an approval backend based on the current configuration.
    /// </summary>
    /// <returns>An instance of IApprovalBackend</returns>
    public IApprovalBackend CreateBackend()
    {
        return BackendType switch
        {
            ApprovalBackendType.Console => new ConsoleApprovalBackend(),
            ApprovalBackendType.Remote => new RemoteApprovalBackend(RemoteConfig ?? throw new InvalidOperationException("RemoteConfig is required when using Remote backend")),
            _ => throw new ArgumentException($"Unknown backend type: {BackendType}")
        };
    }
}

/// <summary>
/// Types of approval backends available.
/// </summary>
public enum ApprovalBackendType
{
    /// <summary>
    /// Console-based approval (local, synchronous)
    /// </summary>
    Console,

    /// <summary>
    /// Remote HTTP-based approval (cloud-friendly, asynchronous)
    /// </summary>
    Remote
}