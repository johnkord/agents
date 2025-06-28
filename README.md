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
- .NET 9.0 SDK or later
- Terminal/Command Prompt  
- VS Code (optional, for debugging)

### Environment Setup

1. **Copy environment template**:
   ```bash
   cp .env.example .env
   # Edit .env with your configuration (e.g., OPENAI_API_KEY)
   ```

### Build and Run

1. **Clone and build**:
   ```bash
   git clone <repository-url>
   cd agents
   dotnet build
   ```

2. **Start the MCP Server** (in one terminal):
   ```bash
   cd src/MCPServer
   dotnet run
   ```

3. **Start the AI AgentAlpha** (in another terminal):
   ```bash
   cd src/Agent/AgentAlpha
   # Test MCP connection (no API key required)
   dotnet run "test"
   
   # Or with OpenAI API key for full agent functionality
   export OPENAI_API_KEY=your_api_key_here
   dotnet run "Calculate 15 + 27 and then multiply by 2"
   
   # Tool selection configuration (optional)
   export MAX_TOOLS_PER_REQUEST=5        # Limit tools per request (default: 10)
   export USE_LLM_TOOL_SELECTION=true    # Use LLM for tool selection (default: true)
   export TOOL_SELECTION_MODEL=gpt-3.5-turbo  # Model for tool selection
   ```

### Alternative: SSE Transport

For HTTP-based communication instead of stdio:

1. **Start MCP Server (SSE mode)**:
   ```bash
   cd src/MCPServer
   MCP_TRANSPORT=sse dotnet run
   ```

2. **Connect with AgentAlpha (SSE mode)**:
   ```bash
   cd src/Agent/AgentAlpha
   MCP_TRANSPORT=sse MCP_SERVER_URL=http://localhost:3000 dotnet run test
   ```

### VS Code Debugging

Open the repository in VS Code and use the pre-configured debug launch configurations:
- Launch MCP Server (stdio or SSE mode)
- Launch AgentAlpha (test or interactive mode)
- All configurations support breakpoint debugging

See [docs/vscode-debugging.md](docs/vscode-debugging.md) for detailed instructions.

## Project Structure

```
agents/
├── src/
│   ├── MCPServer/     # MCP Server console application
│   ├── MCPClient/     # MCP Client console application
│   ├── AgentAlpha/AgentAlpha/   # Simple AI AgentAlpha with MCP integration
│   └── Shared/Shared/           # Shared utilities and models
├── docs/                        # Comprehensive documentation
│   ├── README.md               # Documentation overview
│   ├── getting-started.md      # Setup and usage guide
│   ├── vscode-debugging.md     # VS Code debugging and transport guide
│   ├── mcp-server.md          # Server implementation details
│   ├── mcp-client.md          # Client usage and features
│   └── architecture.md        # System architecture
├── .vscode/                    # VS Code configuration
│   ├── launch.json            # Debug configurations
│   └── tasks.json             # Build tasks
├── .env.example               # Environment configuration template
├── agents.sln                 # Visual Studio solution
└── README.md                  # This file
```

## Features

### Current Implementation
- ✅ MCP Server with mathematical tools (add, subtract, multiply, divide)
- ✅ Interactive MCP Client with command-line interface
- ✅ AgentAlpha: AI agent with MCP Client integration and OpenAI API
- ✅ **Intelligent Tool Selection**: Reduces OpenAI context costs by selecting only relevant tools
- ✅ **VS Code debugging support** with launch.json and tasks.json
- ✅ **Dual transport support**: stdio and SSE (Server-Sent Events)
- ✅ **Environment configuration** with .env file support
- ✅ Microsoft Extensions integration (logging, DI, configuration)
- ✅ Official MCP C# SDK integration
- ✅ Comprehensive documentation and testing

### Planned Features
- 🔄 Full MCP protocol implementation with tools and resources
- 🔄 Dynamic tool registration and discovery
- 🔄 Resource providers for various data sources
- 🔄 Multi-agent coordination and communication
- 🔄 Plugin architecture for extensible functionality

## Technology Stack

- **.NET 9.0**: Modern, cross-platform runtime
- **Model Context Protocol C# SDK**: Official MCP implementation
- **Microsoft Extensions**: Logging, dependency injection, configuration
- **Console Applications**: Simple, cross-platform deployment

## Documentation

Detailed documentation is available in the `docs/` directory:

- 📖 [Getting Started Guide](docs/getting-started.md) - Setup and first steps
- 🖥️ [MCP Server Documentation](docs/mcp-server.md) - Server implementation
- 💻 [MCP Client Documentation](docs/mcp-client.md) - Client usage
- 🎯 [Tool Selection System](docs/tool-selection-system.md) - Intelligent tool selection for cost optimization
- 🏗️ [Architecture Overview](docs/architecture.md) - System design

## Use Cases

This repository enables building AI agents for:

- **Development Tools**: Code analysis, generation, and debugging assistants
- **Data Processing**: ETL operations and data transformation agents
- **System Administration**: Server monitoring and management automation
- **Content Creation**: Writing, editing, and content management assistants
- **Research Assistance**: Information gathering and analysis agents

## Kubernetes Deployment

This repository includes comprehensive Kubernetes support for deploying on Azure Kubernetes Service (AKS):

### Quick Deploy to AKS

```bash
# Build and push images
./scripts/build-and-push.sh your-registry.azurecr.io

# Deploy with Helm
./scripts/helm-deploy.sh your-registry.azurecr.io agents.yourdomain.com your-openai-api-key

# Or deploy with Kustomize
./scripts/kustomize-deploy.sh your-registry.azurecr.io agents.yourdomain.com
```

### Complete Deployment Guide

For a comprehensive step-by-step deployment guide with commands to run on your Linux machine, see:
- **[Complete AKS Deployment Guide](docs/aks-complete-deployment-guide.md)** - Complete instructions from zero to running agents

### What's Included

- **Dockerfiles** for all applications (MCPServer, ApprovalService, AgentAlpha)
- **Kubernetes manifests** with proper resource management and security
- **Helm chart** for easy deployment and configuration management
- **Kustomize overlays** for environment-specific configurations
- **Ingress configuration** for Azure Application Gateway
- **Automated scripts** for build and deployment
- **Persistent SQLite databases** across pod restarts

See [k8s/README.md](k8s/README.md) for detailed deployment instructions.

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