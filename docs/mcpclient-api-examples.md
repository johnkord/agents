# McpClientService API Examples

This file demonstrates the enhanced McpClientService API that supports both HTTP and Stdio transports.

## Transport Type Enum

```csharp
public enum McpTransportType
{
    Stdio,  // Standard input/output transport for local process communication
    Http    // HTTP transport using Server-Sent Events (SSE)
}
```

## New Unified API

### Connect with explicit transport type

```csharp
using Microsoft.Extensions.Logging;
using MCPClient;

var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var mcpService = new McpClientService(loggerFactory);

// Connect using Stdio transport
await mcpService.ConnectAsync(
    McpTransportType.Stdio,
    "My MCP Server",
    command: "dotnet",
    arguments: new[] { "run", "--project", "MyServer.csproj" }
);

// Connect using HTTP transport
await mcpService.ConnectAsync(
    McpTransportType.Http,
    "My HTTP MCP Server",
    serverUrl: "http://localhost:3000"
);
```

## Specific Transport Methods

### Stdio Transport
```csharp
await mcpService.ConnectStdioAsync(
    "Math Server",
    "dotnet",
    new[] { "run", "--project", "MCPServer.csproj" }
);
```

### HTTP Transport
```csharp
await mcpService.ConnectHttpAsync(
    "Remote Math Server",
    "http://localhost:3000"
);
```

## Backward Compatibility

### Existing methods still work
```csharp
// This still works (uses Stdio)
await mcpService.ConnectAsync(
    "Server",
    "dotnet",
    new[] { "run", "--project", "server.csproj" }
);

// Environment-based connection (checks MCP_TRANSPORT env var)
await mcpService.ConnectAsync(
    "Server",
    "dotnet",
    new[] { "run", "--project", "server.csproj" },
    serverUrl: "http://localhost:3000"
);
```

### Deprecated methods (but still functional)
```csharp
// ConnectSseAsync is now deprecated in favor of ConnectHttpAsync
await mcpService.ConnectSseAsync("Server", "http://localhost:3000");
```

## Transport Type Parsing

```csharp
// Parse transport type from string
var transportType = McpClientService.ParseTransportType("http");
// Returns McpTransportType.Http

var stdioType = McpClientService.ParseTransportType("stdio");
// Returns McpTransportType.Stdio

// SSE is treated as HTTP
var sseType = McpClientService.ParseTransportType("sse");
// Returns McpTransportType.Http
```

## Environment Variable Support

Set `MCP_TRANSPORT` environment variable to automatically choose transport:

```bash
# Use Stdio transport (default)
MCP_TRANSPORT=stdio dotnet run

# Use HTTP transport
MCP_TRANSPORT=http MCP_SERVER_URL=http://localhost:3000 dotnet run

# SSE is alias for HTTP
MCP_TRANSPORT=sse MCP_SERVER_URL=http://localhost:3000 dotnet run
```

## Complete Example

```csharp
using Microsoft.Extensions.Logging;
using MCPClient;

class Program
{
    static async Task Main(string[] args)
    {
        var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        var mcpService = new McpClientService(loggerFactory);
        await using var _ = mcpService; // Ensure disposal

        try
        {
            // Connect based on environment or explicit choice
            var transportMode = Environment.GetEnvironmentVariable("MCP_TRANSPORT") ?? "stdio";
            var transportType = McpClientService.ParseTransportType(transportMode);

            if (transportType == McpTransportType.Http)
            {
                var serverUrl = Environment.GetEnvironmentVariable("MCP_SERVER_URL") ?? "http://localhost:3000";
                await mcpService.ConnectAsync(McpTransportType.Http, "My Server", serverUrl: serverUrl);
            }
            else
            {
                await mcpService.ConnectAsync(
                    McpTransportType.Stdio,
                    "My Server",
                    command: "dotnet",
                    arguments: new[] { "run", "--project", "server.csproj" }
                );
            }

            // Use the connected service
            var tools = await mcpService.ListToolsAsync();
            Console.WriteLine($"Available tools: {string.Join(", ", tools.Select(t => t.Name))}");

            // Call a tool
            var result = await mcpService.CallToolAsync("add", new Dictionary<string, object?>
            {
                ["a"] = 5.0,
                ["b"] = 3.0
            });

            if (!result.IsError)
            {
                Console.WriteLine($"Result: {result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
```