# Simple AI Agent

This is a simple AI Agent that demonstrates using the MCP Client as part of its loop.

## Overview

The agent:
1. Takes a task/goal as input
2. Connects to an MCP Server to discover available tools
3. Uses OpenAI to reason about the task and determine what tools to call
4. Executes tool calls through the MCP Client
5. Continues in a loop until the task is considered complete

## Usage

### Prerequisites
- .NET 8.0 SDK
- OpenAI API key set as environment variable `OPENAI_API_KEY`

### Running the Agent

**With command line argument:**
```bash
cd src/Agent/Agent
export OPENAI_API_KEY=your_api_key_here
dotnet run "Calculate 25 + 17 and then multiply the result by 3"
```

**Interactive mode:**
```bash
cd src/Agent/Agent
export OPENAI_API_KEY=your_api_key_here
dotnet run
# Enter task when prompted
```

## Architecture

The agent implements a simple ReAct (Reasoning and Acting) loop:

```
1. User provides task
2. Agent connects to MCP Server
3. Agent discovers available tools
4. Loop:
   a. Send task/context to OpenAI with available tools
   b. If OpenAI returns tool calls, execute them via MCP Client  
   c. Add results to conversation context
   d. Continue until OpenAI indicates task is complete
5. Display final result
```

## Example Tasks

The agent can handle mathematical tasks using the available MCP math tools:

- "Calculate 15 + 27"
- "What is 100 minus 43, then divide that by 3?"
- "Multiply 8 by 9, then add 15 to the result"
- "Calculate the sum of 5.5 and 3.3, then multiply by 2"

## Limitations

- Maximum 10 iterations to prevent infinite loops
- Currently only supports math tools from the MCP Server
- Requires OpenAI API key for reasoning
- Simple text-based interaction only

## Integration

This agent demonstrates how to integrate:
- **OpenAI API** for reasoning and task planning
- **MCP Client** for tool discovery and execution  
- **MCP Server** for providing available tools
- **Conversational AI** patterns with tool use