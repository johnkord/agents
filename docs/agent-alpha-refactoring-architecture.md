# AgentAlpha Refactoring Architecture

## Overview

This document outlines the new modular architecture for AgentAlpha, designed to improve maintainability, testability, and extensibility by separating concerns into distinct components.

## Current State Problems
- Previously the legacy class `SimpleAgentAlpha` (≈673 lines) handled
  MCP connections, OpenAI interaction, tool discovery and execution,
  and message flow in a single file.  
- That file has been deleted; the new modular services now provide the
  same functionality in a cleaner, testable manner.

## New Architecture

### Core Components

#### 1. IConnectionManager
**Responsibility**: Manage MCP server connections and lifecycle

```csharp
public interface IConnectionManager : IAsyncDisposable
{
    Task ConnectAsync(McpTransportType transport, string serverName, string? serverUrl = null, string? command = null, string[]? args = null);
    bool IsConnected { get; }
    Task<IReadOnlyList<ToolDefinition>> ListToolsAsync();
    Task<ToolCallResult> CallToolAsync(string toolName, IReadOnlyDictionary<string, object?> arguments);
}
```

#### 2. IToolManager
**Responsibility**: Handle tool discovery, validation, and schema management

```csharp
public interface IToolManager
{
    Task<IReadOnlyList<ToolDefinition>> DiscoverToolsAsync(IConnectionManager connection);
    IReadOnlyList<ToolDefinition> ApplyFilters(IReadOnlyList<ToolDefinition> tools, ToolFilterConfig filter);
    ToolDefinition CreateOpenAiToolDefinition(ToolDefinition mcpTool);
    Task<string> ExecuteToolAsync(IConnectionManager connection, string toolName, Dictionary<string, object?> arguments);
}
```

#### 3. IConversationManager
**Responsibility**: Manage OpenAI conversations and message flow

```csharp
public interface IConversationManager
{
    void InitializeConversation(string systemPrompt, string userTask);
    Task<ConversationResponse> ProcessIterationAsync(ToolDefinition[] availableTools);
    void AddAssistantMessage(string content);
    void AddToolResults(IEnumerable<string> toolSummaries);
    bool IsTaskComplete(string assistantResponse);
}
```

#### 4. ITaskExecutor
**Responsibility**: Orchestrate the overall task execution flow

```csharp
public interface ITaskExecutor
{
    Task ExecuteAsync(string task);
}
```

#### 5. Enhanced Configuration
**Responsibility**: Centralized configuration management

```csharp
public class AgentConfiguration
{
    public string OpenAiApiKey { get; set; } = "";
    public string Model { get; set; } = "gpt-4o";
    public int MaxIterations { get; set; } = 10;
    public McpTransportType Transport { get; set; } = McpTransportType.Stdio;
    public string? ServerUrl { get; set; }
    public ToolFilterConfig ToolFilter { get; set; } = new();
}
```

### Data Models

#### ConversationResponse
```csharp
public record ConversationResponse(
    string AssistantText,
    IReadOnlyList<ToolCall> ToolCalls,
    bool HasToolCalls
);

public record ToolCall(
    string Name,
    Dictionary<string, object?> Arguments
);
```

## Implementation Strategy

### Phase 1: Extract Interfaces and Base Implementations
1. Create interface definitions
2. Extract ConnectionManager from existing MCP connection code
3. Extract ToolManager from tool discovery and schema creation logic
4. Create basic implementations that maintain current behavior

### Phase 2: Extract Conversation Management
1. Extract ConversationManager from OpenAI interaction code
2. Simplify message flow handling
3. Remove complex nested switch statements

### Phase 3: Create TaskExecutor and Refactor Main Class
1. Create TaskExecutor to orchestrate the overall flow
2. Refactor SimpleAgentAlpha to use injected dependencies
3. Update Program.cs to wire up new components

### Phase 4: Enhance Configuration
1. Centralize all configuration in AgentConfiguration
2. Improve environment variable handling
3. Add validation and default values

## Benefits of New Architecture

### Maintainability
- **Single Responsibility**: Each component has one clear purpose
- **Smaller Classes**: Easier to understand and modify individual components
- **Clear Interfaces**: Well-defined contracts between components

### Testability
- **Dependency Injection**: Easy to mock components for unit testing
- **Isolated Logic**: Test individual components in isolation
- **Clear Boundaries**: Easy to test specific behaviors

### Extensibility
- **Plugin Architecture**: Easy to add new tool types or connection methods
- **Configurable Behavior**: Modify behavior through configuration
- **Swappable Components**: Replace implementations without changing dependent code

### Error Handling
- **Localized Errors**: Errors are contained within specific components
- **Clear Error Paths**: Easy to trace where errors originate
- **Recovery Strategies**: Component-specific error recovery

## Migration Strategy

1. **Backward Compatibility**: Maintain existing public interface during transition
2. **Incremental Refactoring**: Refactor one component at a time
3. **Test-Driven**: Ensure all existing tests pass after each change
4. **Documentation**: Update documentation as components are refactored

## File Organization

```
src/Agent/AgentAlpha/
├── Program.cs                    # Entry point and DI setup
├── Configuration/
│   ├── AgentConfiguration.cs     # Main configuration class
│   └── ToolFilterConfig.cs       # Tool filtering configuration  
├── Interfaces/
│   ├── IConnectionManager.cs     # MCP connection interface
│   ├── IToolManager.cs           # Tool management interface
│   ├── IConversationManager.cs   # Conversation interface
│   └── ITaskExecutor.cs          # Task execution interface
├── Services/
│   ├── ConnectionManager.cs      # MCP connection implementation
│   ├── ToolManager.cs            # Tool management implementation
│   ├── ConversationManager.cs    # OpenAI conversation implementation
│   └── TaskExecutor.cs           # Task execution orchestration
├── Models/
│   ├── ConversationResponse.cs   # Response data models
│   └── ToolCall.cs               # Tool call data models
└── Legacy/
    └── SimpleAgentAlpha.cs       # Temporary during migration
```

This architecture provides a solid foundation for future enhancements while maintaining the existing functionality and improving code organization.