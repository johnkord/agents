# MCP Client

The MCP Client is a console application that connects to MCP servers and provides an interactive interface for using their tools and resources.

## Overview

The current implementation provides a basic MCP client that:
- Starts as an interactive console application
- Provides a command-line interface for user interaction
- Uses Microsoft Extensions for logging
- References the official ModelContextProtocol NuGet package
- Serves as a foundation for connecting to MCP servers

## Current Implementation

### Basic Structure

The client is implemented in `src/MCPClient/MCPClient/Program.cs`:

```csharp
using Microsoft.Extensions.Logging;
using ModelContextProtocol;

namespace MCPClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Simple MCP Client Starting...");
            
            // Create logging infrastructure
            using var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<Program>();
            
            logger.LogInformation("MCP Client is running...");
            logger.LogInformation("This client will connect to MCP servers and use their tools.");
            
            // Interactive command loop
            while (true)
            {
                Console.Write("mcp> ");
                var input = await Console.In.ReadLineAsync();
                
                // Command processing
                switch (input?.ToLower().Trim())
                {
                    case "help":
                        // Show help
                        break;
                    case "quit":
                    case "exit":
                        return;
                    default:
                        Console.WriteLine($"Unknown command: {input}");
                        break;
                }
            }
        }
    }
}
```

### Dependencies

The client project includes these NuGet packages:
- `ModelContextProtocol` (v0.3.0-preview.1) - Official MCP SDK
- `Microsoft.Extensions.Logging` (v9.0.6) - Logging framework
- `Microsoft.Extensions.Logging.Console` (v9.0.6) - Console logging provider

## Interactive Commands

The client provides an interactive shell with these commands:

### Available Commands
- `help` - Display available commands and usage
- `quit` / `exit` - Exit the client application

### Future Commands
- `connect <server>` - Connect to an MCP server
- `disconnect` - Disconnect from current server
- `list-tools` - Show available tools on connected server
- `call <tool> [args]` - Execute a tool with arguments
- `list-resources` - Show available resources on connected server
- `get-resource <name>` - Retrieve a resource from the server

## Usage

### Starting the Client

```bash
cd src/MCPClient/MCPClient
dotnet run
```

### Expected Output

```
Simple MCP Client Starting...
info: MCPClient.Program[0] MCP Client is running...
info: MCPClient.Program[0] This client will connect to MCP servers and use their tools.
Available commands:
- help: Show this help message
- quit: Exit the client

mcp>
```

### Interactive Session

```
mcp> help
Available commands:
- help: Show this help message
- quit: Exit the client

mcp> quit
Goodbye!
```

## Future Enhancements

### Short-term Goals

1. **Server Connection**: Implement connection to MCP servers
   ```csharp
   // Connect to local server
   var client = new McpClient("stdio://localhost:3000");
   await client.ConnectAsync();
   ```

2. **Tool Discovery**: List and describe available tools
   ```csharp
   var tools = await client.ListToolsAsync();
   foreach (var tool in tools)
   {
       Console.WriteLine($"Tool: {tool.Name} - {tool.Description}");
   }
   ```

3. **Tool Execution**: Call server tools with parameters
   ```csharp
   var result = await client.CallToolAsync("echo", new { text = "Hello, World!" });
   Console.WriteLine($"Result: {result}");
   ```

4. **Resource Access**: Retrieve resources from servers
   ```csharp
   var resource = await client.GetResourceAsync("config/settings");
   Console.WriteLine($"Resource: {resource.Content}");
   ```

### Long-term Vision

1. **Multiple Connections**: Connect to multiple MCP servers simultaneously
2. **Session Management**: Save and restore connection sessions
3. **Scripting Support**: Execute batch commands from files
4. **GUI Interface**: Optional graphical interface for easier interaction
5. **Plugin System**: Extensible command system

## Configuration

### Connection Settings

Future versions will support configuration for:
- Default server endpoints
- Connection timeouts
- Authentication credentials
- Preferred communication protocols

### Example Configuration (Planned)
```json
{
  "McpClient": {
    "DefaultServers": [
      {
        "Name": "LocalServer",
        "Endpoint": "stdio://localhost:3000",
        "AutoConnect": true
      }
    ],
    "ConnectionTimeout": "00:00:30",
    "RetryAttempts": 3
  }
}
```

## Development Notes

### SDK Integration

The client uses the official Model Context Protocol C# SDK, ensuring:
- Standard protocol compliance
- Consistent behavior across implementations
- Access to latest MCP features

### Async Programming

The client is built with async/await patterns for:
- Non-blocking network operations
- Responsive user interface
- Proper resource management

### Error Handling

Future implementations will include:
- Connection error recovery
- Graceful timeout handling
- User-friendly error messages
- Logging of all operations

## Extending the Client

### Adding New Commands

1. **Define Command**: Add new case to switch statement
2. **Implement Handler**: Create async method for command logic
3. **Update Help**: Add command to help text
4. **Add Tests**: Ensure command works correctly

### Example Command Implementation
```csharp
case "status":
    await ShowConnectionStatus();
    break;

private static async Task ShowConnectionStatus()
{
    if (client?.IsConnected == true)
    {
        Console.WriteLine("Connected to: " + client.ServerInfo.Name);
    }
    else
    {
        Console.WriteLine("Not connected to any server");
    }
}
```

## Troubleshooting

### Common Issues

1. **Connection failures**: Check server availability and network connectivity
2. **Command not recognized**: Verify command spelling and case sensitivity
3. **Slow response**: Check network latency and server performance
4. **Memory issues**: Monitor connection count and resource usage

### Debug Mode

Enable detailed logging:
```bash
dotnet run --configuration Debug
```

### Verbose Output

For development, enable verbose logging in code:
```csharp
builder.Logging.SetMinimumLevel(LogLevel.Debug);
```