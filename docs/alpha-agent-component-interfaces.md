# TaskExecutor Component Interface Design

## Overview

This document provides detailed interface definitions and implementation guidelines for the components that will be extracted from the TaskExecutor during refactoring. Each interface follows SOLID principles and provides clear contracts for component interactions.

## Component Interfaces

### 1. ISessionCoordinator

```csharp
using AgentAlpha.Models;
using Common.Models.Session;

namespace AgentAlpha.Interfaces;

/// <summary>
/// Manages session lifecycle, creation, resumption, and activity logging setup
/// </summary>
public interface ISessionCoordinator
{
    /// <summary>
    /// Sets up session context based on the task execution request
    /// Handles session creation, resumption, or default session assignment
    /// </summary>
    /// <param name="request">Task execution request containing session information</param>
    /// <returns>Session context with session details and resumption status</returns>
    Task<SessionContext> SetupSessionAsync(TaskExecutionRequest request);

    /// <summary>
    /// Configures activity logging for the session context
    /// </summary>
    /// <param name="context">Session context to configure logging for</param>
    /// <param name="services">Services that need activity logging setup</param>
    Task ConfigureActivityLoggingAsync(SessionContext context, ActivityLoggingServices services);

    /// <summary>
    /// Saves session state and activity logs
    /// </summary>
    /// <param name="context">Session context to save</param>
    Task SaveSessionAsync(SessionContext context);
}

/// <summary>
/// Context information for a session including state and configuration
/// </summary>
public class SessionContext
{
    public AgentSession? Session { get; set; }
    public bool IsResuming { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Services that require activity logging configuration
/// </summary>
public class ActivityLoggingServices
{
    public IToolSelector? ToolSelector { get; set; }
    public IPlanningService? PlanningService { get; set; }
    // Add other services as needed
}
```

### 2. IExecutionStrategyManager

```csharp
using AgentAlpha.Models;
using AgentAlpha.Configuration;

namespace AgentAlpha.Interfaces;

/// <summary>
/// Determines and coordinates different execution strategies based on request parameters
/// </summary>
public interface IExecutionStrategyManager
{
    /// <summary>
    /// Determines the optimal execution strategy for the given request
    /// </summary>
    /// <param name="request">Task execution request</param>
    /// <param name="sessionContext">Session context information</param>
    /// <returns>Recommended execution strategy</returns>
    ExecutionStrategy DetermineStrategy(TaskExecutionRequest request, SessionContext sessionContext);

    /// <summary>
    /// Executes the task using the appropriate strategy
    /// </summary>
    /// <param name="request">Task execution request</param>
    /// <param name="sessionContext">Session context</param>
    /// <param name="config">Agent configuration</param>
    /// <returns>Execution result with completion status and details</returns>
    Task<ExecutionResult> ExecuteTaskAsync(
        TaskExecutionRequest request, 
        SessionContext sessionContext, 
        AgentConfiguration config);
}

/// <summary>
/// Available execution strategies
/// </summary>
public enum ExecutionStrategy
{
    /// <summary>
    /// Standard conversation-based execution without structured planning
    /// </summary>
    ConversationBased,
    
    /// <summary>
    /// Markdown-based execution with structured task planning
    /// </summary>
    MarkdownBased,
    
    /// <summary>
    /// Advanced planning-based execution with state management
    /// </summary>
    PlanningBased
}

/// <summary>
/// Result of task execution
/// </summary>
public class ExecutionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public ExecutionStrategy StrategyUsed { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public Dictionary<string, object> ResultData { get; set; } = new();
}
```

### 3. IToolOrchestrator

```csharp
using AgentAlpha.Models;
using AgentAlpha.Configuration;
using OpenAIIntegration.Model;

namespace AgentAlpha.Interfaces;

/// <summary>
/// Orchestrates tool discovery, selection, expansion, and execution
/// </summary>
public interface IToolOrchestrator
{
    /// <summary>
    /// Discovers all available tools and selects the most relevant ones for the task
    /// </summary>
    /// <param name="request">Task execution request</param>
    /// <param name="isResumingSession">Whether this is a resumed session</param>
    /// <returns>Array of selected tool definitions</returns>
    Task<ToolDefinition[]> DiscoverAndSelectToolsAsync(
        TaskExecutionRequest request, 
        bool isResumingSession = false);

    /// <summary>
    /// Expands the current tool set with additional tools as needed
    /// </summary>
    /// <param name="currentTools">Currently available tools</param>
    /// <param name="requestedTools">Tools being requested for expansion</param>
    /// <param name="maxAdditional">Maximum number of additional tools to add</param>
    /// <returns>Expanded set of tool definitions</returns>
    Task<ToolDefinition[]> ExpandToolsAsync(
        ToolDefinition[] currentTools, 
        string[] requestedTools,
        int maxAdditional = 5);

    /// <summary>
    /// Executes multiple tool calls and aggregates results
    /// </summary>
    /// <param name="toolCalls">Tool calls to execute</param>
    /// <param name="config">Agent configuration for filtering</param>
    /// <returns>Array of tool execution results</returns>
    Task<ToolExecutionResult[]> ExecuteToolsAsync(
        ToolCall[] toolCalls, 
        AgentConfiguration config);

    /// <summary>
    /// Gets additional tools that might be relevant based on current context
    /// </summary>
    /// <param name="availableTools">All available tools</param>
    /// <param name="currentTools">Currently selected tools</param>
    /// <param name="maxAdditional">Maximum additional tools to suggest</param>
    /// <returns>Additional tool definitions</returns>
    Task<ToolDefinition[]> GetAdditionalToolsAsync(
        IList<IUnifiedTool> availableTools,
        ToolDefinition[] currentTools,
        int maxAdditional);
}

/// <summary>
/// Result of tool execution
/// </summary>
public class ToolExecutionResult
{
    public string ToolName { get; set; } = string.Empty;
    public Dictionary<string, object?> Arguments { get; set; } = new();
    public string Result { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public bool IsTaskCompletion { get; set; }
}
```

### 4. IConversationExecutor

```csharp
using AgentAlpha.Configuration;
using OpenAIIntegration.Model;

namespace AgentAlpha.Interfaces;

/// <summary>
/// Handles the main conversation loop with AI including tool expansion and execution
/// </summary>
public interface IConversationExecutor
{
    /// <summary>
    /// Executes the main conversation loop with dynamic tool expansion
    /// </summary>
    /// <param name="availableTools">Initially available tools</param>
    /// <param name="config">Agent configuration</param>
    /// <param name="sessionContext">Session context information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Conversation execution result</returns>
    Task<ConversationResult> ExecuteConversationLoopAsync(
        ToolDefinition[] availableTools,
        AgentConfiguration config,
        SessionContext sessionContext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a single conversation iteration
    /// </summary>
    /// <param name="availableTools">Tools available for this iteration</param>
    /// <param name="config">Agent configuration</param>
    /// <param name="iteration">Current iteration number</param>
    /// <returns>Iteration result</returns>
    Task<ConversationIterationResult> ProcessIterationAsync(
        ToolDefinition[] availableTools,
        AgentConfiguration config,
        int iteration);
}

/// <summary>
/// Result of conversation execution
/// </summary>
public class ConversationResult
{
    public bool TaskCompleted { get; set; }
    public string? CompletionReason { get; set; }
    public List<string> ExecutionSummary { get; set; } = new();
    public int IterationsCompleted { get; set; }
    public TimeSpan TotalExecutionTime { get; set; }
    public List<ToolExecutionResult> ToolResults { get; set; } = new();
}

/// <summary>
/// Result of a single conversation iteration
/// </summary>
public class ConversationIterationResult
{
    public bool HasToolCalls { get; set; }
    public List<ToolCall> ToolCalls { get; set; } = new();
    public string AssistantResponse { get; set; } = string.Empty;
    public bool TaskCompleted { get; set; }
    public List<string> ToolsExpanded { get; set; } = new();
}
```

### 5. Enhanced ITaskExecutor

```csharp
using AgentAlpha.Models;

namespace AgentAlpha.Interfaces;

/// <summary>
/// Main task executor interface - simplified to focus on coordination
/// </summary>
public interface ITaskExecutor
{
    /// <summary>
    /// Execute a simple task string using default configuration
    /// </summary>
    /// <param name="task">The task description to execute</param>
    /// <returns>Task representing the async operation</returns>
    Task ExecuteAsync(string task);
    
    /// <summary>
    /// Execute a task with full request parameters
    /// </summary>
    /// <param name="request">Complete task execution request</param>
    /// <returns>Task representing the async operation</returns>
    Task ExecuteAsync(TaskExecutionRequest request);

    /// <summary>
    /// Execute a task and return detailed results
    /// </summary>
    /// <param name="request">Task execution request</param>
    /// <returns>Detailed execution result</returns>
    Task<ExecutionResult> ExecuteWithResultAsync(TaskExecutionRequest request);
}
```

## Implementation Guidelines

### Dependency Injection Configuration

```csharp
// In Program.cs or DI container configuration
services.AddScoped<ISessionCoordinator, SessionCoordinator>();
services.AddScoped<IExecutionStrategyManager, ExecutionStrategyManager>();
services.AddScoped<IToolOrchestrator, ToolOrchestrator>();
services.AddScoped<IConversationExecutor, ConversationExecutor>();
services.AddScoped<ITaskExecutor, TaskExecutor>();
```

### Error Handling Strategy

Each component should:
1. **Catch specific exceptions** related to its domain
2. **Log errors** with appropriate context
3. **Wrap exceptions** in domain-specific exception types
4. **Provide fallback behavior** where appropriate
5. **Propagate critical errors** to the calling component

### Logging Strategy

Each component should:
1. **Use structured logging** with consistent fields
2. **Log at appropriate levels** (Debug, Info, Warning, Error)
3. **Include correlation IDs** for tracing across components
4. **Avoid sensitive data** in log messages
5. **Use performance logging** for key operations

### Testing Strategy

Each component should have:
1. **Unit tests** for all public methods
2. **Mock dependencies** for isolated testing
3. **Integration tests** for component interactions
4. **Performance tests** for critical paths
5. **Error scenario tests** for exception handling

## Component Interaction Flow

```
Request → TaskExecutor → SessionCoordinator → SessionContext
                      → ExecutionStrategyManager → Strategy Selection
                      → ToolOrchestrator → Tool Discovery & Selection
                      → ConversationExecutor → Conversation Loop
                      → ToolOrchestrator → Tool Execution
                      → SessionCoordinator → Session Persistence
```

## Configuration Requirements

Each component may need configuration sections:

```json
{
  "AgentConfiguration": {
    "SessionCoordinator": {
      "DefaultSessionTimeout": "01:00:00",
      "MaxConcurrentSessions": 10
    },
    "ExecutionStrategyManager": {
      "DefaultStrategy": "ConversationBased",
      "StrategySelectionTimeout": "00:00:30"
    },
    "ToolOrchestrator": {
      "MaxToolsPerRequest": 10,
      "ToolExpansionLimit": 5,
      "ToolExecutionTimeout": "00:05:00"
    },
    "ConversationExecutor": {
      "MaxIterations": 10,
      "IterationTimeout": "00:02:00"
    }
  }
}
```

This interface design provides a solid foundation for the TaskExecutor refactoring while maintaining flexibility and extensibility.