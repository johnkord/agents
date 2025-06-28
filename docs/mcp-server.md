# MCP Server

The MCP Server is a console application that implements the Model Context Protocol server-side functionality. It exposes tools and resources that MCP clients can discover and use.

## Overview

The current implementation provides a complete MCP server that:
- Starts and runs as a console application using the hosting framework
- Uses Microsoft Extensions for logging and dependency injection
- References the official ModelContextProtocol NuGet package (see also the `reference/csharp-sdk` submodule for full SDK source)
- Implements comprehensive tools for shell operations, file management, GitHub/Azure DevOps integration, and more using the MCP SDK
- Provides proper stdio transport for client communication
- Handles tool registration and execution through the MCP framework

## Current Implementation

### Basic Structure

The server is implemented in `src/MCPServer/Program.cs` with comprehensive tool implementations:

```csharp
// Program.cs - Server setup and hosting
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using MCPServer.Tools;

namespace MCPServer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            
            builder.Logging.AddConsole(consoleLogOptions =>
            {
                consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
            });

            builder.Services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithTools<ShellTools>()
                .WithTools<TaskCompletionTool>()
                .WithTools<GitHubTools>()
                .WithTools<AzureDevOpsTools>()
                .WithTools<OpenAIVectorStoreTools>()
                .WithTools<CodeReviewTools>();

            var host = builder.Build();
            
            Console.WriteLine("MCP Server Starting...");
            Console.WriteLine("Available tools: GitHub PR review, Azure DevOps PR review, OpenAI Vector Store, Code Review analysis, and more...");
            
            await host.RunAsync();
        }
    }
}
```

### Dependencies

The server project includes these NuGet packages:
- `Microsoft.Extensions.Hosting` (v9.0.6) - Application hosting framework
- `ModelContextProtocol` (v0.3.0-preview.1) - Official MCP SDK
- `Microsoft.Extensions.Logging` (v9.0.6) - Logging framework
- `Microsoft.Extensions.Logging.Console` (v9.0.6) - Console logging provider

## Current Tools and Resources

The server now implements a comprehensive set of tools across multiple domains:

### File Operations Tools
1. **read_file** - Read the contents of a file
2. **write_file** - Write text content to a file
3. **list_directory** - List files and directories in a given path
4. **file_exists** - Check if a file exists at the given path
5. **delete_file** - Delete a file at the given path
6. **create_directory** - Create a new directory at the given path
7. **get_file_info** - Get information about a file (size, creation date, etc.)

### Text Processing Tools
1. **search_text** - Search for a pattern in text and return matching lines
2. **replace_text** - Replace all occurrences of a pattern with replacement text
3. **extract_lines** - Extract specific lines from text by line numbers
4. **word_count** - Count words, characters, and lines in text
5. **format_text** - Apply formatting to text (uppercase, lowercase, title case)
6. **split_text** - Split text by a delimiter and return the parts

### System Information Tools
1. **get_current_time** - Get the current date and time
2. **get_system_info** - Get basic system information
3. **get_environment_variable** - Get the value of an environment variable
4. **list_environment_variables** - List all environment variables (or filter by pattern)
5. **get_current_directory** - Get the current working directory
6. **generate_uuid** - Generate a new UUID/GUID

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
cd src/MCPServer
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

The server references the official Model Context Protocol C# SDK from the `reference/csharp-sdk` submodule (mirroring the `modelcontextprotocol/csharp-sdk` project). This ensures compatibility with the standard MCP specification and provides a local reference for future AI development.

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