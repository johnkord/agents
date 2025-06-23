using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MCPServer.ToolApproval;

/// <summary>
/// Interface for approval backends that can handle tool approval requests.
/// Supports both synchronous (console) and asynchronous (remote) approval mechanisms.
/// </summary>
public interface IApprovalBackend
{
    /// <summary>
    /// Request approval for a tool invocation.
    /// </summary>
    /// <param name="token">The approval token containing tool name, arguments, and metadata</param>
    /// <param name="cancellationToken">Cancellation token for the approval request</param>
    /// <returns>True if approved, false if denied</returns>
    Task<bool> RequestApprovalAsync(ApprovalInvocationToken token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the name of this approval backend for logging and configuration purposes.
    /// </summary>
    string Name { get; }
}