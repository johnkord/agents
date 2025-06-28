# MCP Server Tests

This directory contains comprehensive tests for the MCP Server implementation.

## Test Projects

### AgentAlpha.Tests
Unit tests for the AgentAlpha functionality.

**Running the tests:**
```bash
dotnet test tests/AgentAlpha.Tests
```

### MCPClient.Tests
Tests for the MCP client implementation.

**Running the tests:**
```bash
dotnet test tests/MCPClient.Tests
```

### MCPServer.Tests
Tests for the MCP server functionality.

**Running the tests:**
```bash
dotnet test tests/MCPServer.Tests
```

## Continuous Integration

The GitHub Actions workflow (`.github/workflows/ci-cd.yml`) automatically:
1. Builds all projects
2. Runs unit tests
3. Publishes test results

## Test Coverage

The tests cover:
- ✅ MCP tool registration and discovery
- ✅ Server/client communication
- ✅ Agent functionality
- ✅ Error handling and validation

## Architecture

```
tests/
├── AgentAlpha.Tests/              # Agent functionality tests
├── MCPClient.Tests/               # MCP client tests
└── MCPServer.Tests/               # MCP server tests
```