# AgentAlpha - Enhanced AI Assistant

AgentAlpha is an intelligent AI assistant that uses the Model Context Protocol (MCP) to access a variety of tools for practical task completion.

## Overview

AgentAlpha combines OpenAI's reasoning capabilities with a rich set of tools to perform real-world tasks. The agent:

1. Takes a task/goal as input
2. Connects to an MCP Server to discover available tools  
3. Uses OpenAI to reason about the task and plan tool usage
4. Executes tool calls through the MCP Client
5. Continues in a loop until the task is considered complete

## Available Tools

### Mathematical Operations
- **add, subtract, multiply, divide** - Basic arithmetic operations

### File Operations  
- **read_file** - Read file contents
- **write_file** - Write text to files
- **list_directory** - List files and directories
- **file_exists** - Check file existence
- **delete_file** - Delete files safely
- **create_directory** - Create new directories
- **get_file_info** - Get file metadata (size, dates, etc.)

### Text Processing
- **search_text** - Search for patterns in text
- **replace_text** - Find and replace text patterns
- **extract_lines** - Extract specific lines by numbers
- **word_count** - Count words, characters, and lines
- **format_text** - Apply text formatting (case, trimming)
- **split_text** - Split text by delimiters

### System Information
- **get_current_time** - Get current date and time
- **get_system_info** - System and environment details
- **get_environment_variable** - Read environment variables
- **list_environment_variables** - List all environment variables
- **get_current_directory** - Get working directory
- **generate_uuid** - Generate unique identifiers

## Usage

### Prerequisites
- .NET 8.0 SDK
- OpenAI API key set as environment variable `OPENAI_API_KEY`

### Running the Agent

**Test MCP connection (no API key required):**
```bash
cd src/Agent/AgentAlpha
dotnet run "test"
```

**With command line argument:**
```bash
cd src/Agent/AgentAlpha
export OPENAI_API_KEY=your_api_key_here
dotnet run "Calculate 25 + 17 and then multiply the result by 3"
```

**Interactive mode:**
```bash
cd src/Agent/AgentAlpha
export OPENAI_API_KEY=your_api_key_here
dotnet run
# Enter task when prompted
```

### Enhanced Input Parameters

The agent now supports rich command-line parameters for advanced task execution:

**Basic usage (unchanged):**
```bash
dotnet run "Calculate 25 + 17"
```

**With specific OpenAI model:**
```bash
dotnet run --model "gpt-3.5-turbo" "Calculate 25 + 17"
dotnet run -m "gpt-4o" "Write a creative story"
```

**With temperature control (0.0-1.0):**
```bash
dotnet run --temperature 0.2 "Analyze this data precisely"
dotnet run -t 0.8 "Write a creative poem"
```

**With iteration limits:**
```bash
dotnet run --max-iterations 5 "Quick calculation"
dotnet run --iterations 15 "Complex analysis task"
```

**With task priority:**
```bash
dotnet run --priority High "Urgent calculation needed"
dotnet run --priority Low "Background analysis"
```

**With execution timeout (in minutes):**
```bash
dotnet run --timeout 5 "Time-critical task"
```

**With verbose logging:**
```bash
dotnet run --verbose "Debug this complex issue"
dotnet run -v "Show detailed execution steps"
```

**With custom system prompt:**
```bash
dotnet run --system-prompt "You are a math tutor" "Help me understand calculus"
```

**Combined parameters:**
```bash
dotnet run --model "gpt-4o" --temperature 0.7 --priority High --verbose "Creative writing task with detailed logging"
```

### Parameter Reference

| Parameter | Short | Description | Example |
|-----------|-------|-------------|---------|
| `--model` | `-m` | OpenAI model to use | `gpt-4o`, `gpt-3.5-turbo` |
| `--temperature` | `-t` | Response creativity (0.0-1.0) | `0.2` (precise), `0.8` (creative) |
| `--max-iterations` | `--iterations` | Max conversation loops | `5`, `15` |
| `--priority` | | Task priority level | `Low`, `Normal`, `High` |
| `--timeout` | | Execution timeout in minutes | `5`, `10`, `30` |
| `--verbose` | `-v` | Enable detailed logging | (flag, no value) |
| `--system-prompt` | | Custom system prompt | `"You are a code reviewer"` |

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

AgentAlpha can handle a wide variety of tasks using its enhanced tool set:

### Mathematical Tasks
- "Calculate 15 + 27"
- "What is 100 minus 43, then divide that by 3?"
- "Multiply 8 by 9, then add 15 to the result"

### File Management Tasks
- "Read the file config.txt and tell me what's in it"
- "Create a new file called notes.txt with a list of project tasks"
- "List all files in the current directory"
- "Check if the file data.csv exists"

### Text Processing Tasks  
- "Count the words in this document and find all occurrences of 'important'"
- "Replace all instances of 'old_name' with 'new_name' in this text"
- "Extract lines 10-20 from this log file content"
- "Convert this text to uppercase and remove extra spaces"

### System Information Tasks
- "What time is it and what operating system are we running on?"
- "Show me all environment variables containing 'PATH'"
- "Generate a new UUID for this project"
- "What's the current working directory?"

### Complex Multi-step Tasks
- "Read the README.md file, count the words, and create a summary file"
- "Search through all .txt files for error messages and compile a report"
- "Create a project structure with folders and placeholder files"

## Limitations

- Maximum 10 iterations to prevent infinite loops
- Requires OpenAI API key for reasoning and task planning
- File operations are limited to local file system access
- No web browsing or external API access (currently)

## Enhanced Features

### Dynamic Tool Discovery
AgentAlpha automatically discovers and adapts to available tools, with proper parameter mapping for each tool type. No hard-coded assumptions about tool parameters.

### Intelligent Task Planning  
The agent breaks down complex tasks into manageable steps and provides clear feedback on progress and results.

### Persistent Session Support
AgentAlpha now supports persistent sessions that maintain conversation history and context across multiple task executions. Sessions are stored in a local SQLite database and can be referenced for continued work.

### Robust Error Handling
Each tool includes comprehensive error handling with clear, actionable error messages to guide users.

### Flexible Parameter Support
Supports various parameter types (strings, numbers, booleans) and optional parameters with sensible defaults.

## Integration

This agent demonstrates how to integrate:
- **OpenAI API** for reasoning and task planning
- **MCP Client** for tool discovery and execution  
- **MCP Server** for providing available tools
- **Dynamic Tool Discovery** for flexible parameter handling
- **Multi-domain Tools** spanning math, files, text, and system operations
- **Conversational AI** patterns with enhanced tool use

## Getting Started

1. **Install Prerequisites**: Ensure .NET 8.0 SDK is installed
2. **Set API Key**: Export your OpenAI API key as `OPENAI_API_KEY`
3. **Test Connection**: Run `dotnet run "test"` to verify MCP server connectivity
4. **Try Examples**: Start with simple tasks like "What time is it?" or "List the current directory"
5. **Advanced Usage**: Combine multiple tools for complex workflows

## Using Persistent Sessions

AgentAlpha supports persistent sessions that maintain conversation history and context across multiple task executions:

### Creating a New Session
```bash
# Create a named session
dotnet run --session-name "My Project Session" "Help me plan a software project"

# The session ID will be displayed and can be used later
```

### Resuming an Existing Session
```bash
# Continue from where you left off
dotnet run --session "session-id-here" "Continue with the next step"
```

### Session Features
- **Conversation History**: All previous interactions are preserved
- **Context Continuity**: The agent remembers what has been discussed
- **SQLite Storage**: Sessions are stored locally in `agent_sessions.db`
- **Automatic Saving**: Session state is saved after each task completion

### Command Line Options
- `--session-name "Name"`: Create a new named session
- `--session "session-id"`: Resume an existing session by ID
- `--session-id "session-id"`: Alternative syntax for resuming sessions

### Session Management
Sessions are automatically saved and can be:
- **Resumed**: Pick up where you left off with full context
- **Listed**: View all available sessions (feature for future CLI enhancement)
- **Archived**: Mark completed sessions as archived

For more detailed enhancement plans and architectural details, see `docs/agent-alpha-enhancement-plan.md`.