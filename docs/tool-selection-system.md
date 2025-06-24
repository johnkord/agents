# Tool Selection System Design

## Overview

The Tool Selection System is designed to reduce OpenAI API costs by intelligently selecting only the most relevant tools for each task and conversation iteration, rather than sending all available tools with every request.

## Problem Statement

Previously, the agent would send all discovered and filtered tools to OpenAI with every request. This approach:
- Increased token usage significantly due to tool definitions in the context
- Raised API costs unnecessarily
- Could hit context limits with many available tools
- Sent irrelevant tools that would never be used for the given task

## Solution Architecture

### Core Components

#### 1. IToolSelector Interface
Provides intelligent tool selection capabilities:
- `SelectToolsForTaskAsync`: Select relevant tools for the initial task
- `SelectAdditionalToolsAsync`: Dynamically expand tools during conversation
- `GetEssentialToolsAsync`: Get tools that should always be available

#### 2. ToolSelector Service
Implementation that uses multiple strategies for tool selection:
- **LLM-based Selection**: Uses a lightweight OpenAI model (gpt-3.5-turbo) to analyze task requirements and select relevant tools
- **Heuristic Fallback**: Keyword-based selection when LLM is unavailable or disabled
- **Essential Tools**: Always includes critical tools like "complete_task"

#### 3. ToolSelectionConfig
Configuration class that controls tool selection behavior:
```csharp
public class ToolSelectionConfig
{
    public int MaxToolsPerRequest { get; set; } = 10;           // Max tools per request
    public bool UseLLMSelection { get; set; } = true;           // Use LLM vs heuristics
    public string SelectionModel { get; set; } = "gpt-3.5-turbo"; // Model for selection
    public double SelectionTemperature { get; set; } = 0.1;     // Temperature for consistency
    public bool AllowDynamicExpansion { get; set; } = true;     // Enable tool expansion
    public int MaxAdditionalToolsPerIteration { get; set; } = 3; // Max tools to add per iteration
    public HashSet<string> EssentialTools { get; set; } = new() { "complete_task" }; // Always include
}
```

#### 4. Enhanced ConversationManager
Extended with `ProcessIterationWithExpansionAsync` method that:
- Attempts conversation with current tool set
- Detects when additional tools might be needed
- Dynamically expands tool set if assistant requests unavailable tools
- Retries with expanded tool set

#### 5. Modified TaskExecutor
Updated to:
- Use intelligent tool selection instead of sending all tools
- Support dynamic tool expansion during conversation
- Track and manage currently available tools per iteration

## Tool Selection Strategies

### 1. LLM-Based Selection (Primary)
Uses OpenAI's gpt-3.5-turbo to analyze task descriptions and select relevant tools:

**Prompt Template:**
```
You are a tool selection assistant. Given a task and a list of available tools, 
select the most relevant tools that would be needed to complete the task.

Task: {user_task}

Available tools:
- tool1: description1
- tool2: description2
...

Instructions:
1. Analyze the task to understand what operations might be needed
2. Select up to {max_tools} tools that are most relevant
3. Prefer tools that are directly related to the task requirements
4. Consider both immediate needs and potential follow-up operations
5. Return your response as a JSON array of tool names only

Selected tools:
```

### 2. Heuristic Fallback
Keyword-based matching when LLM selection is disabled or fails:

```csharp
var keywordMappings = new Dictionary<string[], string[]>
{
    [new[] { "math", "calculate", "add", "subtract" }] = 
        new[] { "add", "subtract", "multiply", "divide" },
    [new[] { "file", "read", "write", "directory" }] = 
        new[] { "read_file", "write_file", "list_directory" },
    [new[] { "text", "search", "replace", "word" }] = 
        new[] { "search_in_file", "replace_in_file", "word_count" },
    // ... more mappings
};
```

### 3. Essential Tools
Always includes critical tools regardless of task:
- `complete_task`: Allows agent to signal task completion (cannot be blacklisted)
- Any tools marked as essential in configuration

**Note**: The `complete_task` tool is protected and will always be included even if it appears in tool filter blacklists, as it's essential for proper task completion signaling.

## Dynamic Tool Expansion

The system supports adding tools during conversation when needed:

1. **Detection**: ConversationManager detects phrases indicating need for additional tools:
   - "I don't have"
   - "I need"
   - "would need"
   - "missing"
   - "not available"

2. **Selection**: ToolSelector analyzes recent conversation context to suggest additional tools

3. **Expansion**: Relevant tools are added to the conversation for subsequent iterations

4. **Persistence**: Newly added tools remain available for the rest of the conversation

## Configuration

### Environment Variables
- `MAX_TOOLS_PER_REQUEST`: Maximum number of tools to send per request (default: 10)
- `USE_LLM_TOOL_SELECTION`: Enable/disable LLM-based selection (default: true)
- `TOOL_SELECTION_MODEL`: Model to use for tool selection (default: gpt-3.5-turbo)

### Code Configuration
```csharp
var config = new ToolSelectionConfig
{
    MaxToolsPerRequest = 8,
    UseLLMSelection = true,
    SelectionModel = "gpt-3.5-turbo",
    SelectionTemperature = 0.1,
    AllowDynamicExpansion = true,
    MaxAdditionalToolsPerIteration = 2,
    EssentialTools = { "complete_task", "custom_essential_tool" }
};
```

## Benefits

### Cost Reduction
- **Token Savings**: Reduces tool definition tokens sent to OpenAI by ~60-80% in typical scenarios
- **API Cost Reduction**: Proportional reduction in OpenAI API costs
- **Context Efficiency**: Leaves more context space for actual conversation content

### Intelligent Selection
- **Task-Aware**: Selects tools based on actual task requirements
- **Context-Sensitive**: Can expand tools based on conversation flow
- **Fallback Safety**: Multiple selection strategies ensure robustness

### Performance
- **Fast Selection**: Uses lightweight model (gpt-3.5-turbo) for quick tool selection
- **Minimal Overhead**: Selection happens once per task with optional expansion
- **Caching Potential**: Selected tools are reused across conversation iterations

## Example Usage

### Basic Usage
```csharp
var toolSelector = new ToolSelector(openAiService, toolManager, logger, config);

// Select tools for a math task
var selectedTools = await toolSelector.SelectToolsForTaskAsync(
    "Calculate the sum of 5 and 10", 
    allAvailableTools, 
    maxTools: 5);

// Result: [complete_task, add, subtract] (3 tools instead of 20+ available)
```

### With Dynamic Expansion
```csharp
// During conversation, if assistant says "I need to read a file but don't have file tools"
var additionalTools = await toolSelector.SelectAdditionalToolsAsync(
    conversationContext,
    remainingTools,
    currentlySelectedTools,
    maxAdditional: 2);

// Result: [read_file, file_info] added to available tools
```

## Monitoring and Debugging

The system provides comprehensive logging:
- Tool selection decisions and reasoning
- Token savings estimates
- Fallback usage when LLM selection fails
- Dynamic expansion events

Example log output:
```
🔧 Discovered 25 tools total, 20 after filtering
🎯 Selected 4 relevant tools: complete_task, add, subtract, multiply
💡 Available for expansion: 16 additional tools
🔧 Expanded tools for next iteration: +2 tools
```

## Future Enhancements

1. **Tool Usage Analytics**: Track which tools are actually used vs. selected
2. **Learning-Based Selection**: Improve selection based on historical usage patterns
3. **Tool Clustering**: Group related tools for more efficient selection
4. **Performance Caching**: Cache tool selections for similar tasks
5. **Advanced Context Analysis**: Use embeddings for more sophisticated relevance scoring