using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MCPServer.ToolApproval;

/// <summary>
/// Abstraction for different approval mechanisms (console, REST, file-based, etc.)
/// </summary>
public interface IApprovalProvider
{
    /// <summary>
    /// Request approval for a tool invocation
    /// </summary>
    /// <param name="token">The approval invocation token containing tool details</param>
    /// <param name="cancellationToken">Cancellation token for the approval request</param>
    /// <returns>True if approved, false if denied</returns>
    Task<bool> RequestApprovalAsync(ApprovalInvocationToken token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the provider name for logging and configuration
    /// </summary>
    string ProviderName { get; }
}