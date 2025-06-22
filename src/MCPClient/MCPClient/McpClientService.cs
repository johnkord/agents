using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace MCPClient
{
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
        /// Connect to an MCP server using stdio transport
        /// </summary>
        /// <param name="serverName">Name of the server</param>
        /// <param name="command">Command to start the server</param>
        /// <param name="arguments">Arguments for the server command</param>
        public async Task ConnectAsync(string serverName, string command, string[] arguments)
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
        /// Connect to an MCP server using SSE transport
        /// </summary>
        /// <param name="serverName">Name of the server</param>
        /// <param name="serverUrl">URL of the SSE server</param>
        public async Task ConnectSseAsync(string serverName, string serverUrl)
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
            _logger.LogInformation("Connected to MCP Server: {ServerName} (SSE) at {ServerUrl}", serverName, serverUrl);
        }

        /// <summary>
        /// Connect to an MCP server using the transport mode specified in environment variables
        /// </summary>
        /// <param name="serverName">Name of the server</param>
        /// <param name="command">Command to start the server (used for stdio)</param>
        /// <param name="arguments">Arguments for the server command (used for stdio)</param>
        /// <param name="serverUrl">URL of the SSE server (used for SSE)</param>
        public async Task ConnectAsync(string serverName, string command, string[] arguments, string? serverUrl = null)
        {
            var transportMode = Environment.GetEnvironmentVariable("MCP_TRANSPORT")?.ToLowerInvariant() ?? "stdio";
            
            if (transportMode == "sse")
            {
                var url = serverUrl ?? Environment.GetEnvironmentVariable("MCP_SERVER_URL") ?? "http://localhost:3000";
                await ConnectSseAsync(serverName, url);
            }
            else
            {
                await ConnectAsync(serverName, command, arguments);
            }
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