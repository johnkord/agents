# MCP Math Tools Tests

This directory contains comprehensive tests for the MCP Math Tools implementation.

## Test Projects

### MCPMathTools.Tests
Unit tests for the mathematical operations implemented in the MCP server.

**Features:**
- Tests all four basic mathematical operations (add, subtract, multiply, divide)
- Includes edge case testing (division by zero, negative numbers, decimals)
- Uses xUnit testing framework
- 24 comprehensive test cases covering various scenarios

**Running the tests:**
```bash
dotnet test tests/MCPMathTools.Tests
```

### MCPMathTools.AI.Tests
AI validation tests using OpenAI API to test the MCP tools through natural language.

**Features:**
- Connects to the MCP server and retrieves available tools
- Uses OpenAI API with function calling to validate tool interactions
- Tests natural language scenarios like "Calculate 15 + 27"
- Demonstrates real-world AI integration with MCP tools

**Prerequisites:**
- OpenAI API key set in environment variable `OPENAI_API_KEY`

**Running the AI tests:**
```bash
export OPENAI_API_KEY=your_api_key_here
dotnet run --project tests/MCPMathTools.AI.Tests
```

## Continuous Integration

The GitHub Actions workflow (`.github/workflows/ci-cd.yml`) automatically:
1. Builds all projects
2. Runs unit tests
3. Runs AI validation tests (when `OPENAI_AGENTS_1` secret is available)
4. Publishes test results

## Test Coverage

The tests cover:
- ✅ Basic arithmetic operations
- ✅ Edge cases (division by zero)
- ✅ Negative numbers
- ✅ Decimal numbers
- ✅ MCP tool registration and discovery
- ✅ AI integration through OpenAI function calling
- ✅ Error handling and validation

## Architecture

```
tests/
├── MCPMathTools.Tests/           # Unit tests
│   ├── MathToolsUnitTests.cs     # Direct testing of math operations
│   └── MCPMathTools.Tests.csproj
└── MCPMathTools.AI.Tests/        # AI integration tests
    ├── Program.cs                # OpenAI API integration
    └── MCPMathTools.AI.Tests.csproj
```