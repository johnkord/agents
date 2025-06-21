# MCP Server

The MCP Server is a console application that implements the Model Context Protocol server-side functionality. It exposes tools and resources that MCP clients can discover and use.

## Overview

The current implementation provides a basic MCP server that:
- Starts and runs as a console application
- Uses Microsoft Extensions for logging and dependency injection
- References the official ModelContextProtocol NuGet package
- Provides a foundation for exposing tools and resources

## Current Implementation

### Basic Structure

The server is implemented in `src/MCPServer/MCPServer/Program.cs`:

```csharp
using Microsoft.Extensions.Logging;
using ModelContextProtocol;

namespace MCPServer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Simple MCP Server Starting...");
            
            // Create logging infrastructure
            using var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<Program>();
            
            // Log server status
            logger.LogInformation("MCP Server is running...");
            logger.LogInformation("Available tools: echo, time, random");
            
            // Keep server running
            Console.WriteLine("Press Ctrl+C to stop the server...");
            await Task.Delay(-1);
        }
    }
}
```

### Dependencies

The server project includes these NuGet packages:
- `ModelContextProtocol` (v0.3.0-preview.1) - Official MCP SDK
- `Microsoft.Extensions.Logging` (v9.0.6) - Logging framework
- `Microsoft.Extensions.Logging.Console` (v9.0.6) - Console logging provider

## Planned Tools and Resources

The server is designed to expose these example tools:

### Tools
1. **echo** - Echoes back the input text
2. **time** - Gets the current server time
3. **random** - Generates random numbers within a specified range

### Resources
- Configuration data
- System information
- File system access (with appropriate security)

## Future Enhancements

### Short-term Goals
1. **Full MCP Protocol Implementation**: Implement proper MCP server using the SDK
2. **Tool Registration**: Dynamic tool discovery and registration
3. **Resource Management**: Implement resource providers
4. **Configuration**: External configuration for server settings

### Long-term Vision
1. **Plugin Architecture**: Support for dynamically loaded tool providers
2. **Authentication**: Secure access to server tools and resources
3. **Monitoring**: Health checks and metrics
4. **Scalability**: Support for multiple concurrent clients

## Usage

### Starting the Server

```bash
cd src/MCPServer/MCPServer
dotnet run
```

### Expected Output

```
Simple MCP Server Starting...
Press Ctrl+C to stop the server...
info: MCPServer.Program[0] MCP Server is running...
info: MCPServer.Program[0] Available tools: echo, time, random
```

### Stopping the Server

Press `Ctrl+C` to gracefully stop the server.

## Configuration

Currently, the server uses default configuration. Future versions will support:
- Configuration files (appsettings.json)
- Environment variables
- Command-line arguments
- External configuration providers

## Development Notes

### SDK Integration

The server references the official Model Context Protocol C# SDK from the `modelcontextprotocol/csharp-sdk` project. This ensures compatibility with the standard MCP specification.

### Logging

The server uses Microsoft Extensions Logging for consistent log output. This provides:
- Structured logging support
- Multiple output providers
- Log level filtering
- Performance optimizations

### Async/Await Pattern

The server is built with async/await throughout to ensure:
- Non-blocking operations
- Scalable request handling
- Proper resource cleanup

## Extending the Server

To add new tools or resources:

1. **Define Tool Interface**: Create tool implementation classes
2. **Register with MCP**: Use the SDK to register tools with the MCP framework
3. **Handle Requests**: Implement request handlers for each tool
4. **Add Documentation**: Update this documentation with new capabilities

## Troubleshooting

### Common Issues

1. **Port binding errors**: Check for port conflicts
2. **Package reference errors**: Ensure ModelContextProtocol package is properly restored
3. **Logging not appearing**: Verify console logger configuration

### Debug Mode

Run with detailed logging:
```bash
dotnet run --configuration Debug
```