# Agents Repository Documentation

This repository contains multiple AI agents with their MCP (Model Context Protocol) clients and MCP servers, exposing tools and resources for various use cases.

## Overview

The Agents repository is designed to be a comprehensive collection of:

- **MCP Servers**: Expose various tools and resources that AI agents can use
- **MCP Clients**: Connect to and interact with MCP servers
- **AI Agents**: Intelligent agents that leverage MCP infrastructure for various tasks

## Architecture

The repository follows a modular architecture with:

```
/src/
  /MCPServer/     - MCP Server implementation
  /MCPClient/     - MCP Client implementation  
  /Shared/        - Shared models and utilities between projects
/docs/            - Documentation
agents.sln        - Visual Studio solution file
```

## Quick Start

1. **Prerequisites**: .NET 8.0 SDK or later
2. **Build**: `dotnet build` from the root directory
3. **Run MCP Server**: Navigate to `src/MCPServer` and run `dotnet run`
4. **Run MCP Client**: In another terminal, navigate to `src/MCPClient` and run `dotnet run`
5. **Test**: Run `dotnet test` to execute all unit tests

## Example Usage

Once both server and client are running:

```
mcp> add 5 3
The sum of 5 and 3 is 8

mcp> divide 20 4  
The quotient of 20 and 4 is 5

mcp> divide 10 0
Error: Cannot divide by zero
```

## Documentation Index

### Core Documentation
- [Getting Started](getting-started.md) - Setup and initial configuration
- [Architecture](architecture.md) - System architecture and design decisions
- [AgentAlpha Design Document](agent-alpha-design-doc.md) - **Comprehensive guide on how an AI agent solves tasks using LLMs and MCP Client+Server with tools**

### Component Documentation
- [MCP Server](mcp-server.md) - MCP Server implementation with comprehensive tools
- [MCP Client](mcp-client.md) - MCP Client usage and interactive features  

### Specialized Documentation
- [AgentAlpha Refactoring Architecture](agent-alpha-refactoring-architecture.md) - Modular architecture details
- [AgentAlpha Enhancement Plan](agent-alpha-enhancement-plan.md) - Feature roadmap and implementation strategy
- [Unified Tool Management Design](unified-tool-management-design.md) - Tool system architecture
- [Tests](../tests/README.md) - Comprehensive testing including AI validation

## Technology Stack

- **.NET 8.0**: Primary framework
- **Model Context Protocol C# SDK**: Official MCP implementation for .NET (see `reference/csharp-sdk` for the full SDK source as a submodule)
- **Microsoft Extensions**: Logging, DI, Configuration, and Hosting
- **Console Applications**: Simple, cross-platform deployment

## Reference SDKs

This repository includes a `reference` folder containing the official Model Context Protocol C# SDK as a git submodule:

- `reference/csharp-sdk`: [modelcontextprotocol/csharp-sdk](https://github.com/modelcontextprotocol/csharp-sdk)

This folder is intended as a reference for future AI development and for ensuring compatibility with the MCP specification. To update the submodule, use:

```bash
git submodule update --remote reference/csharp-sdk
```

## Contributing

This repository serves as a foundation for building AI agents with MCP capabilities. Each agent implementation should follow the established patterns and leverage the shared infrastructure.