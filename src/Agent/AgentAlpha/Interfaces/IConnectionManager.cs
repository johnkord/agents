using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using MCPClient;

namespace AgentAlpha.Interfaces;

/// <summary>
/// Manages MCP server connections and lifecycle
/// </summary>
public interface IConnectionManager : IAsyncDisposable
{
    /// <summary>
    /// Connect to an MCP server
    /// </summary>
    Task ConnectAsync(McpTransportType transport, string serverName, string? serverUrl = null, string? command = null, string[]? args = null);
    
    /// <summary>
    /// Whether the connection is currently active
    /// </summary>
    bool IsConnected { get; }
    
    /// <summary>
    /// List all available tools from the connected server
    /// </summary>
    Task<IList<McpClientTool>> ListToolsAsync();
    
    /// <summary>
    /// Execute a tool with the given arguments
    /// </summary>
    Task<CallToolResult> CallToolAsync(string toolName, Dictionary<string, object?> arguments);
}