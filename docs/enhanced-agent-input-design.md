# Enhanced Agent Run Input Parameters Design Document

## Overview

This document describes the enhanced input parameter system for AgentAlpha, expanding beyond simple string tasks to support comprehensive task execution requests with configurable parameters.

## Problem Statement

Previously, the agent could only accept a single string parameter representing the task to execute. This limited flexibility and required users to rely entirely on environment variables for configuration. The new system provides a rich parameter model that allows per-task customization while maintaining backwards compatibility.

## Solution Design

### Core Components

#### 1. TaskExecutionRequest Model

A comprehensive request object that encapsulates all parameters needed for task execution:

```csharp
public class TaskExecutionRequest
{
    public string Task { get; set; }                    // The task description
    public string? Model { get; set; }                  // OpenAI model (e.g., "gpt-4o", "gpt-4.1-nano")
    public double? Temperature { get; set; }            // OpenAI temperature (0.0-1.0)
    public int? MaxIterations { get; set; }             // Max conversation iterations
    public string? SystemPrompt { get; set; }           // Custom system prompt
    public TaskPriority Priority { get; set; }          // Task priority (High, Normal, Low)
    public TimeSpan? Timeout { get; set; }              // Task execution timeout
    public ToolFilterConfig? ToolFilter { get; set; }   // Tool filtering preferences
    public bool VerboseLogging { get; set; }            // Enable verbose logging
}
```

#### 2. Enhanced ITaskExecutor Interface

The interface now supports both backwards-compatible string tasks and the new rich request model:

```csharp
public interface ITaskExecutor
{
    Task ExecuteAsync(string task);                     // Backwards compatibility
    Task ExecuteAsync(TaskExecutionRequest request);    // Enhanced functionality
}
```

#### 3. Command Line Argument Parsing

New command-line options allow users to specify parameters directly:

```bash
# Basic usage (unchanged)
dotnet run "Calculate 25 + 17"

# With model specification
dotnet run --model "gpt-4.1-nano" "Calculate 25 + 17"

# With temperature control
dotnet run --model "gpt-4o" --temperature 0.7 "Write a creative story"

# With iteration limit
dotnet run --max-iterations 5 "Analyze this complex problem"

# With priority and timeout
dotnet run --priority High --timeout 10 "Urgent calculation needed"

# With verbose logging
dotnet run --verbose "Debug this issue"

# With custom system prompt
dotnet run --system-prompt "You are a math tutor" "Help me learn algebra"
```

## Parameter Details

### OpenAI Model Selection
- **Purpose**: Allow per-task model selection instead of relying only on environment variables
- **Examples**: "gpt-4o", "gpt-4.1-nano", "gpt-4-turbo"
- **Fallback**: Uses AgentConfiguration.Model if not specified

### Temperature Control
- **Purpose**: Control response creativity and randomness
- **Range**: 0.0 (deterministic) to 1.0 (highly creative)
- **Use Cases**: 
  - 0.0-0.3 for factual/analytical tasks
  - 0.7-1.0 for creative writing tasks

### Max Iterations Override
- **Purpose**: Adjust conversation loop length per task complexity
- **Default**: Uses AgentConfiguration.MaxIterations
- **Use Cases**: Simple tasks (lower), complex analysis (higher)

### Custom System Prompts
- **Purpose**: Task-specific agent behavior customization
- **Examples**: 
  - "You are a code reviewer" for development tasks
  - "You are a friendly tutor" for educational tasks

### Task Priority Levels
- **Purpose**: Future optimization and resource allocation
- **Levels**: Low, Normal, High
- **Current Impact**: Logging and display purposes
- **Future**: Could influence execution scheduling, resource allocation

### Execution Timeout
- **Purpose**: Prevent runaway tasks and resource management
- **Format**: Minutes as integer (converted to TimeSpan)
- **Use Cases**: Time-critical environments, testing scenarios

### Verbose Logging
- **Purpose**: Enhanced debugging and monitoring
- **Impact**: Detailed parameter logging and execution traces

## Implementation Strategy

### Phase 1: Core Model (Completed)
- ✅ TaskExecutionRequest model creation
- ✅ ITaskExecutor interface enhancement
- ✅ TaskExecutor implementation updates
- ✅ Command-line argument parsing

### Phase 2: Enhanced Integration (Future)
- ConversationManager integration for temperature/model passing
- OpenAI service updates for dynamic model selection
- Enhanced tool filtering per request

### Phase 3: Advanced Features (Future)
- Request templates and presets
- Configuration file support
- Request validation and sanitization
- Performance metrics per parameter set

## Backwards Compatibility

The system maintains full backwards compatibility:

1. **String Task Interface**: `ExecuteAsync(string task)` still works
2. **Command Line**: Simple task strings work unchanged
3. **Environment Variables**: All existing configuration still applies
4. **Default Behavior**: Unspecified parameters use existing defaults

## Usage Examples

### Simple Usage (No Change)
```bash
dotnet run "What time is it?"
```

### Creative Writing Task
```bash
dotnet run --model "gpt-4o" --temperature 0.8 --system-prompt "You are a creative writer" "Write a short story about AI"
```

### Quick Analysis Task  
```bash
dotnet run --model "gpt-4.1-nano" --temperature 0.2 --max-iterations 3 "Analyze this data quickly"
```

### High-Priority Urgent Task
```bash
dotnet run --priority High --timeout 5 --verbose "Emergency calculation needed"
```

## Benefits

1. **Flexibility**: Per-task parameter customization
2. **User Experience**: Rich command-line interface
3. **Backwards Compatibility**: No breaking changes
4. **Extensibility**: Easy to add new parameters
5. **Performance**: Task-specific optimization potential
6. **Debugging**: Enhanced logging and monitoring capabilities

## Future Enhancements

1. **Configuration Profiles**: Named parameter sets
2. **Model Auto-Selection**: Intelligent model choice based on task type
3. **Dynamic Resource Management**: Priority-based execution
4. **Request Validation**: Parameter validation and suggestions
5. **Performance Analytics**: Parameter impact analysis
6. **API Integration**: REST API for remote task submission

## Technical Notes

- All parameters are optional with sensible defaults
- Parameter validation ensures safe ranges (temperature clamping, positive iterations)
- The design follows the existing architectural patterns
- Implementation uses minimal changes to existing code
- Full compatibility with existing tests and infrastructure