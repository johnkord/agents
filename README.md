# Agents

A comprehensive repository for AI agents with Model Context Protocol (MCP) integration, featuring MCP servers and clients that expose tools and resources for various AI use cases.

## Overview

This repository provides a foundation for building AI agents that leverage the Model Context Protocol (MCP) for tool and resource access. It includes:

- **MCP Server**: Exposes tools and resources through the standard MCP protocol
- **MCP Client**: Connects to MCP servers and provides an interactive interface
- **Shared Infrastructure**: Common utilities and models for agent development
- **Comprehensive Documentation**: Detailed guides and architecture documentation

## Quick Start

### Prerequisites
- .NET 8.0 SDK or later
- Terminal/Command Prompt

### Build and Run

1. **Clone and build**:
   ```bash
   git clone <repository-url>
   cd agents
   dotnet build
   ```

2. **Start the MCP Server** (in one terminal):
   ```bash
   cd src/MCPServer/MCPServer
   dotnet run
   ```

3. **Start the MCP Client** (in another terminal):
   ```bash
   cd src/MCPClient/MCPClient
   dotnet run
   ```

## Project Structure

```
agents/
├── src/
│   ├── MCPServer/MCPServer/     # MCP Server console application
│   ├── MCPClient/MCPClient/     # MCP Client console application
│   └── Shared/Shared/           # Shared utilities and models
├── docs/                        # Comprehensive documentation
│   ├── README.md               # Documentation overview
│   ├── getting-started.md      # Setup and usage guide
│   ├── mcp-server.md          # Server implementation details
│   ├── mcp-client.md          # Client usage and features
│   └── architecture.md        # System architecture
├── agents.sln                  # Visual Studio solution
└── README.md                   # This file
```

## Features

### Current Implementation
- ✅ Basic MCP server with foundation for tools and resources
- ✅ Interactive MCP client with command-line interface
- ✅ Microsoft Extensions integration (logging, DI, configuration)
- ✅ Official MCP C# SDK integration
- ✅ Comprehensive documentation and guides

### Planned Features
- 🔄 Full MCP protocol implementation with tools and resources
- 🔄 Dynamic tool registration and discovery
- 🔄 Resource providers for various data sources
- 🔄 Multi-agent coordination and communication
- 🔄 Plugin architecture for extensible functionality

## Technology Stack

- **.NET 8.0**: Modern, cross-platform runtime
- **Model Context Protocol C# SDK**: Official MCP implementation
- **Microsoft Extensions**: Logging, dependency injection, configuration
- **Console Applications**: Simple, cross-platform deployment

## Documentation

Detailed documentation is available in the `docs/` directory:

- 📖 [Getting Started Guide](docs/getting-started.md) - Setup and first steps
- 🖥️ [MCP Server Documentation](docs/mcp-server.md) - Server implementation
- 💻 [MCP Client Documentation](docs/mcp-client.md) - Client usage
- 🏗️ [Architecture Overview](docs/architecture.md) - System design

## Use Cases

This repository enables building AI agents for:

- **Development Tools**: Code analysis, generation, and debugging assistants
- **Data Processing**: ETL operations and data transformation agents
- **System Administration**: Server monitoring and management automation
- **Content Creation**: Writing, editing, and content management assistants
- **Research Assistance**: Information gathering and analysis agents

## Contributing

This repository serves as a foundation for AI agent development. When adding new agents:

1. Follow the established MCP patterns
2. Leverage the shared infrastructure
3. Add comprehensive documentation
4. Include appropriate tests

## Model Context Protocol

This project uses the [Model Context Protocol](https://modelcontextprotocol.io/) (MCP), which provides:

- **Standardized Communication**: Consistent protocol for AI-tool interaction
- **Tool Discovery**: Dynamic discovery of available tools and capabilities
- **Resource Access**: Secure access to external resources and data
- **Extensibility**: Easy addition of new tools and resource providers

## License

[Add your license information here]

## Getting Help

- 📚 Check the [documentation](docs/) for detailed guides
- 🐛 Create an issue for bugs or feature requests
- 💬 Start a discussion for questions and ideas