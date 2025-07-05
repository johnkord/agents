using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace MCPClient
{
    /// <summary>
    /// Represents the transport mode for MCP client connections
    /// </summary>
    public enum McpTransportType
    {
        /// <summary>
        /// Standard input/output transport for local process communication
        /// </summary>
        Stdio,
        
        /// <summary>
        /// HTTP transport using Server-Sent Events (SSE)
        /// </summary>
        Http
    }
    /// <summary>
    /// Service class that provides MCP client functionality for connecting to MCP servers
    /// </summary>
    public class McpClientService : IAsyncDisposable
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<McpClientService> _logger;
        private IMcpClient? _mcpClient;

        public McpClientService(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<McpClientService>();
        }

        /// <summary>
        /// Connect to an MCP server using the specified transport type
        /// </summary>
        /// <param name="transportType">The transport type to use (Stdio or Http)</param>
        /// <param name="serverName">Name of the server</param>
        /// <param name="command">Command to start the server (required for Stdio transport)</param>
        /// <param name="arguments">Arguments for the server command (required for Stdio transport)</param>
        /// <param name="serverUrl">URL of the HTTP server (required for Http transport)</param>
        public async Task ConnectAsync(McpTransportType transportType, string serverName, string? command = null, string[]? arguments = null, string? serverUrl = null)
        {
            if (_mcpClient != null)
            {
                throw new InvalidOperationException("Already connected to an MCP server. Disconnect first.");
            }

            switch (transportType)
            {
                case McpTransportType.Stdio:
                    if (string.IsNullOrEmpty(command))
                        throw new ArgumentException("Command is required for Stdio transport", nameof(command));
                    if (arguments == null)
                        throw new ArgumentException("Arguments are required for Stdio transport", nameof(arguments));
                    
                    await ConnectStdioAsync(serverName, command, arguments);
                    break;
                    
                case McpTransportType.Http:
                    if (string.IsNullOrEmpty(serverUrl))
                        throw new ArgumentException("ServerUrl is required for Http transport", nameof(serverUrl));
                    
                    await ConnectHttpAsync(serverName, serverUrl);
                    break;
                    
                default:
                    throw new ArgumentException($"Unsupported transport type: {transportType}", nameof(transportType));
            }
        }

        /// <summary>
        /// Connect to an MCP server using stdio transport
        /// </summary>
        /// <param name="serverName">Name of the server</param>
        /// <param name="command">Command to start the server</param>
        /// <param name="arguments">Arguments for the server command</param>
        public async Task ConnectStdioAsync(string serverName, string command, string[] arguments)
        {
            if (_mcpClient != null)
            {
                throw new InvalidOperationException("Already connected to an MCP server. Disconnect first.");
            }

            var clientTransport = new StdioClientTransport(new()
            {
                Name = serverName,
                Command = command,
                Arguments = arguments
            });

            _mcpClient = await McpClientFactory.CreateAsync(clientTransport, loggerFactory: _loggerFactory);
            _logger.LogInformation("Connected to MCP Server: {ServerName} (stdio)", serverName);
        }

        /// <summary>
        /// Connect to an MCP server using HTTP transport (SSE)
        /// </summary>
        /// <param name="serverName">Name of the server</param>
        /// <param name="serverUrl">URL of the HTTP server</param>
        public async Task ConnectHttpAsync(string serverName, string serverUrl)
        {
            if (_mcpClient != null)
            {
                throw new InvalidOperationException("Already connected to an MCP server. Disconnect first.");
            }

            var clientTransport = new SseClientTransport(new()
            {
                Name = serverName,
                Endpoint = new Uri(serverUrl),
                TransportMode = HttpTransportMode.AutoDetect
            }, _loggerFactory);

            _mcpClient = await McpClientFactory.CreateAsync(clientTransport, loggerFactory: _loggerFactory);
            _logger.LogInformation("Connected to MCP Server: {ServerName} (HTTP) at {ServerUrl}", serverName, serverUrl);
        }

        /// <summary>
        /// Connect to an MCP server using stdio transport (convenience method)
        /// </summary>
        /// <param name="serverName">Name of the server</param>
        /// <param name="command">Command to start the server</param>
        /// <param name="arguments">Arguments for the server command</param>
        public async Task ConnectAsync(string serverName, string command, string[] arguments)
        {
            await ConnectStdioAsync(serverName, command, arguments);
        }

        /// <summary>
        /// Connect to an MCP server using SSE transport (backward compatibility)
        /// </summary>
        /// <param name="serverName">Name of the server</param>
        /// <param name="serverUrl">URL of the SSE server</param>
        [Obsolete("Use ConnectHttpAsync instead for better clarity")]
        public async Task ConnectSseAsync(string serverName, string serverUrl)
        {
            await ConnectHttpAsync(serverName, serverUrl);
        }

        /// <summary>
        /// Connect to an MCP server using the transport mode specified in environment variables
        /// </summary>
        /// <param name="serverName">Name of the server</param>
        /// <param name="command">Command to start the server (used for stdio)</param>
        /// <param name="arguments">Arguments for the server command (used for stdio)</param>
        /// <param name="serverUrl">URL of the HTTP server (used for HTTP)</param>
        public async Task ConnectAsync(string serverName, string command, string[] arguments, string? serverUrl = null)
        {
            var transportMode = Environment.GetEnvironmentVariable("MCP_TRANSPORT")?.ToLowerInvariant() ?? "sse";
            
            if (transportMode == "sse" || transportMode == "http")
            {
                var url = serverUrl ?? Environment.GetEnvironmentVariable("MCP_SERVER_URL") ?? "http://localhost:3000";
                await ConnectHttpAsync(serverName, url);
            }
            else
            {
                await ConnectStdioAsync(serverName, command, arguments);
            }
        }

        /// <summary>
        /// Parse transport type from string
        /// </summary>
        /// <param name="transportMode">Transport mode string (stdio, http, sse)</param>
        /// <returns>Corresponding McpTransportType</returns>
        public static McpTransportType ParseTransportType(string transportMode)
        {
            return transportMode?.ToLowerInvariant() switch
            {
                "stdio" => McpTransportType.Stdio,
                "http" => McpTransportType.Http,
                "sse" => McpTransportType.Http, // SSE is HTTP-based
                _ => throw new ArgumentException($"Unsupported transport mode: {transportMode}", nameof(transportMode))
            };
        }

        /// <summary>
        /// Get the list of available tools from the connected MCP server
        /// </summary>
        public async Task<IList<McpClientTool>> ListToolsAsync()
        {
            EnsureConnected();
            var tools = await _mcpClient!.ListToolsAsync();
            return tools;
        }

        /// <summary>
        /// Call a tool on the connected MCP server
        /// </summary>
        /// <param name="toolName">Name of the tool to call</param>
        /// <param name="arguments">Arguments for the tool</param>
        public async Task<CallToolResult> CallToolAsync(string toolName, Dictionary<string, object?> arguments)
        {
            EnsureConnected();
            return await _mcpClient!.CallToolAsync(toolName, arguments);
        }

        /// <summary>
        /// Check if connected to an MCP server
        /// </summary>
        public bool IsConnected => _mcpClient != null;

        private void EnsureConnected()
        {
            if (_mcpClient == null)
            {
                throw new InvalidOperationException("Not connected to an MCP server. Call ConnectAsync first.");
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_mcpClient != null)
            {
                await _mcpClient.DisposeAsync();
                _mcpClient = null;
            }
        }
    }
}