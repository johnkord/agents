# Getting Started

This guide will help you set up and run the MCP server and client applications.

## Prerequisites

- .NET 8.0 SDK or later
- Any code editor (VS Code, Visual Studio, or any text editor)
- Terminal/Command Prompt

## Installation

1. **Clone the repository**:
   ```bash
   git clone <repository-url>
   cd agents
   ```

2. **Restore dependencies**:
   ```bash
   dotnet restore
   ```

3. **Build the solution**:
   ```bash
   dotnet build
   ```

## Running the Applications

### MCP Server

The MCP server exposes tools and resources that clients can use.

1. **Navigate to server directory**:
   ```bash
   cd src/MCPServer
   ```

2. **Run the server**:
   ```bash
   dotnet run
   ```

3. **Expected output**:
   ```
   Simple MCP Server Starting...
   Press Ctrl+C to stop the server...
   info: MCPServer.Program[0] MCP Server is running...
   info: MCPServer.Program[0] Available tools: echo, time, random
   ```

### MCP Client

The MCP client connects to MCP servers and uses their tools.

1. **Open a new terminal** (keep the server running in the other terminal)

2. **Navigate to client directory**:
   ```bash
   cd src/MCPClient
   ```

3. **Run the client**:
   ```bash
   dotnet run
   ```

4. **Expected output**:
   ```
   Simple MCP Client Starting...
   info: MCPClient.Program[0] MCP Client is running...
   info: MCPClient.Program[0] This client will connect to MCP servers and use their tools.
   Available commands:
   - help: Show this help message
   - quit: Exit the client

   mcp>
   ```

5. **Try the commands**:
   - Type `help` to see available commands
   - Type `quit` to exit the client

## Project Structure

```
agents/
├── src/
│   ├── MCPServer/
│   │   └── MCPServer/          # MCP Server console application
│   │       ├── Program.cs      # Main server implementation
│   │       └── MCPServer.csproj
│   ├── MCPClient/
│   │   └── MCPClient/          # MCP Client console application
│   │       ├── Program.cs      # Main client implementation
│   │       └── MCPClient.csproj
│   └── Shared/
│       └── Shared/             # Shared utilities (future use)
│           └── Shared.csproj
├── docs/                       # Documentation
├── agents.sln                  # Solution file
└── README.md
```

## Next Steps

- [Learn about the MCP Server](mcp-server.md)
- [Explore MCP Client features](mcp-client.md)
- [Understand the architecture](architecture.md)

## Troubleshooting

### Common Issues

1. **Build errors**: Ensure you have .NET 8.0 SDK installed
2. **Missing packages**: Run `dotnet restore` from the root directory
3. **Port conflicts**: The server will use default ports; check for conflicts

### Getting Help

- Check the documentation in this `docs/` directory
- Review the source code in the `src/` directory
- Ensure all prerequisites are properly installed