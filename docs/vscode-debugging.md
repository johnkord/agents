# VS Code Debugging and Transport Configuration

This guide explains how to use the VS Code debugging configurations and transport options added to the project.

## VS Code Debugging

### Setup
1. Open the repository in VS Code
2. Copy `.env.example` to `.env` and configure your environment variables
3. Use the debug configurations in the Run and Debug panel

### Available Debug Configurations

| Configuration | Description | Use Case |
|---------------|-------------|----------|
| **Launch MCP Server** | Starts MCP Server in stdio mode | Default debugging, compatible with existing clients |
| **Launch MCP Server (SSE Mode)** | Starts MCP Server as web server | HTTP/SSE transport testing |
| **Launch AgentAlpha** | Starts AgentAlpha in test mode | Quick connection testing |
| **Launch AgentAlpha (Interactive)** | Starts AgentAlpha in interactive mode | Full agent testing with OpenAI |
| **Launch AgentAlpha with SSE** | Starts AgentAlpha using SSE transport | Testing SSE client connectivity |

### Build Tasks

Use Ctrl+Shift+P → "Tasks: Run Task" to access:
- **build-all**: Build entire solution
- **build-mcp-server**: Build MCP Server only
- **build-agent-alpha**: Build AgentAlpha only
- **build-mcp-client**: Build MCP Client only
- **clean**: Clean all build artifacts
- **restore**: Restore NuGet packages
- **test**: Run all tests

## Transport Configuration

### Environment Variables

Configure transport behavior using environment variables in your `.env` file:

```bash
# Transport Configuration
MCP_TRANSPORT=stdio          # Options: stdio, sse
MCP_SERVER_URL=http://localhost:3000  # For SSE transport

# Server Configuration  
ASPNETCORE_URLS=http://localhost:3000  # SSE server listening address
ASPNETCORE_ENVIRONMENT=Development

# OpenAI Configuration
OPENAI_API_KEY=your_openai_api_key_here
```

### Stdio Transport (Default)

```bash
# Start MCP Server (stdio mode)
cd src/MCPServer/MCPServer
dotnet run

# Connect with AgentAlpha
cd src/Agent/AgentAlpha  
dotnet run test
```

### SSE Transport

```bash
# Start MCP Server (SSE mode)
cd src/MCPServer/MCPServer
dotnet run -- --transport sse
# OR using environment variable:
# MCP_TRANSPORT=sse dotnet run

# Connect with AgentAlpha (SSE mode)
cd src/Agent/AgentAlpha
MCP_TRANSPORT=sse MCP_SERVER_URL=http://localhost:3000 dotnet run test
```

### Command Line Options

#### MCP Server
```bash
# Stdio mode (default)
dotnet run

# SSE mode
dotnet run -- --transport sse

# With custom environment
ASPNETCORE_URLS=http://localhost:5000 dotnet run -- --transport sse
```

#### AgentAlpha
```bash
# Test mode (quick MCP connection test)
dotnet run test

# Interactive mode (requires OPENAI_API_KEY)
dotnet run "Calculate the sum of 15 and 27"
```

## Development Workflow

### 1. Quick Testing
```bash
# Terminal 1: Start MCP Server
cd src/MCPServer/MCPServer
dotnet run

# Terminal 2: Test with AgentAlpha
cd src/Agent/AgentAlpha
dotnet run test
```

### 2. SSE Transport Testing
```bash
# Terminal 1: Start SSE Server
cd src/MCPServer/MCPServer
MCP_TRANSPORT=sse ASPNETCORE_URLS=http://localhost:3000 dotnet run

# Terminal 2: Test SSE Client
cd src/Agent/AgentAlpha
MCP_TRANSPORT=sse MCP_SERVER_URL=http://localhost:3000 dotnet run test
```

### 3. VS Code Debugging
1. Set breakpoints in your code
2. Select appropriate debug configuration
3. Press F5 to start debugging
4. Use the integrated terminal for additional testing

## Troubleshooting

### Common Issues

1. **Build Errors**: Run `dotnet restore` from the root directory
2. **Missing OpenAI Key**: Use `dotnet run test` to test MCP connectivity without OpenAI
3. **Port Conflicts**: Change `ASPNETCORE_URLS` in `.env` file
4. **SSE Connection Issues**: Ensure MCP Server is running in SSE mode before testing client

### Debugging Tips

- Use the **build-all** task before debugging to ensure everything compiles
- Check the Debug Console for detailed error messages
- Use **Launch AgentAlpha** configuration for quick MCP testing without OpenAI dependency
- Monitor both client and server logs when debugging SSE transport

### Logging Levels

Adjust logging verbosity in `.env`:
```bash
LOGGING__LOGLEVEL__DEFAULT=Debug        # More verbose
LOGGING__LOGLEVEL__DEFAULT=Information  # Normal
LOGGING__LOGLEVEL__DEFAULT=Warning      # Less verbose
```

## Transport Comparison

| Feature | Stdio | SSE |
|---------|--------|-----|
| **Setup** | Simple | Requires web server |
| **Process Model** | Server launched as subprocess | Server runs independently |
| **Network** | Local pipes | HTTP connections |
| **Debugging** | Easier (single process) | More complex (multi-process) |
| **Production** | Limited scalability | Web-scale ready |
| **Use Case** | Development, testing | Production, remote access |

Choose stdio for development and simple testing, SSE for production deployments and remote access scenarios.