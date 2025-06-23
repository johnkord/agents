using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using AgentAlpha.Interfaces;
using MCPClient;

namespace AgentAlpha.Services;

/// <summary>
/// Implementation of MCP server connection management
/// </summary>
public class ConnectionManager : IConnectionManager
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<ConnectionManager> _logger;
    private McpClientService? _mcpClient;

    public ConnectionManager(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<ConnectionManager>();
    }

    public bool IsConnected => _mcpClient != null;

    public async Task ConnectAsync(McpTransportType transport, string serverName, string? serverUrl = null, string? command = null, string[]? args = null)
    {
        if (_mcpClient != null)
        {
            await _mcpClient.DisposeAsync();
        }

        _mcpClient = new McpClientService(_loggerFactory);

        try
        {
            if (transport == McpTransportType.Http)
            {
                var url = serverUrl ?? "http://localhost:3000";
                await _mcpClient.ConnectAsync(McpTransportType.Http, serverName, serverUrl: url);
            }
            else
            {
                var cmd = command ?? "dotnet";
                var arguments = args ?? ["run", "--project", "../../MCPServer/MCPServer.csproj"];
                await _mcpClient.ConnectAsync(McpTransportType.Stdio, serverName, cmd, arguments);
            }

            _logger.LogInformation("Successfully connected to MCP server using {Transport}", transport);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to MCP server");
            await DisposeInternalAsync();
            throw;
        }
    }

    public async Task<IList<McpClientTool>> ListToolsAsync()
    {
        if (_mcpClient == null)
            throw new InvalidOperationException("Not connected to MCP server");

        try
        {
            return await _mcpClient.ListToolsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list tools from MCP server");
            throw;
        }
    }

    public async Task<CallToolResult> CallToolAsync(string toolName, Dictionary<string, object?> arguments)
    {
        if (_mcpClient == null)
            throw new InvalidOperationException("Not connected to MCP server");

        try
        {
            _logger.LogDebug("Calling tool {ToolName} with arguments: {Arguments}", toolName, arguments);
            return await _mcpClient.CallToolAsync(toolName, arguments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call tool {ToolName}", toolName);
            throw;
        }
    }

    private async Task DisposeInternalAsync()
    {
        if (_mcpClient != null)
        {
            await _mcpClient.DisposeAsync();
            _mcpClient = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeInternalAsync();
        GC.SuppressFinalize(this);
    }
}