# AgentAlpha Architecture for AI Tools

This document provides a quick reference for AI coding assistants working with the AgentAlpha codebase.

## Core Architecture Pattern

AgentAlpha follows a **dependency injection** pattern with clear separation of concerns:

```
Program.cs (Entry Point)
    ↓
Host.CreateDefaultBuilder() (DI Container)
    ↓  
ServiceCollectionExtensions.AddAgentAlphaServices() (Service Registration)
    ↓
ITaskExecutor.ExecuteAsync() (Main Orchestrator)
    ↓
Individual Services (ConnectionManager, ToolManager, etc.)
```

## Key Service Interfaces

### ITaskExecutor
- **Purpose**: Main orchestrator for task execution
- **Location**: `src/Agent/AgentAlpha/Interfaces/ITaskExecutor.cs`
- **Implementation**: `src/Agent/AgentAlpha/Services/TaskExecutor.cs`
- **Dependencies**: All other services (injected via constructor)

### IConnectionManager  
- **Purpose**: Manages MCP server connections
- **Location**: `src/Agent/AgentAlpha/Interfaces/IConnectionManager.cs`
- **Implementation**: `src/Agent/AgentAlpha/Services/ConnectionManager.cs`
- **Key Methods**: `ConnectAsync()`, `ListToolsAsync()`, `CallToolAsync()`

### IToolManager
- **Purpose**: Tool discovery, filtering, and execution
- **Location**: `src/Agent/AgentAlpha/Interfaces/IToolManager.cs`
- **Implementation**: `src/Agent/AgentAlpha/Services/ToolManager.cs`
- **Key Methods**: `DiscoverToolsAsync()`, `ApplyFilters()`, `ExecuteToolAsync()`

### IConversationManager
- **Purpose**: OpenAI conversation and message flow management
- **Location**: `src/Agent/AgentAlpha/Interfaces/IConversationManager.cs`
- **Implementation**: `src/Agent/AgentAlpha/Services/ConversationManager.cs`
- **Key Methods**: `InitializeConversation()`, `ProcessIterationAsync()`

### IPlanner          <!-- NEW -->
- **Purpose**: Abstraction over all planning strategies  
- **Location**: `src/Agent/AgentAlpha/Interfaces/IPlanner.cs`  
- **Implementations**:  
  - `PlanningService` (single-shot)  
  - `ChainedPlanner` (prompt-chaining, enabled when `USE_CHAINED_PLANNER=true`)  

### ChainedPlanner    <!-- NEW -->
- **Purpose**: Three-stage Analyse → Outline → Detail prompt chain  
- **Location**: `src/Agent/AgentAlpha/Services/ChainedPlanner.cs`  
- **Feature Flag**: `USE_CHAINED_PLANNER=true`

#### New Environment Variables
| Variable | Default | Description |
|----------|---------|-------------|
| `USE_CHAINED_PLANNER` | `false` | Switch between `PlanningService` and `ChainedPlanner` |
| `CHAINED_PLANNER_MODEL` | `gpt-4.1-nano` | Model for Analyse / Outline stages |
| `CHAINED_PLANNER_DETAIL_MODEL` | `gpt-4.1` | Model for Detail stage |
| `CHAINED_PLANNER_MAX_TOKENS` | `2048` | Token limit per stage |

## Configuration System

### AgentConfiguration
- **Location**: `src/Agent/AgentAlpha/Configuration/AgentConfiguration.cs`
- **Factory Method**: `AgentConfiguration.FromEnvironment()`
- **Validation**: Built-in validation with descriptive error messages
- **Environment Variables**: All config loaded from env vars with fallback defaults

### Common Configuration Patterns
```csharp
// Get validated configuration
var config = AgentConfiguration.FromEnvironment();

// Configuration is injected into services via DI
services.AddSingleton(config);
```

## Testing Patterns

### Test Structure
- **Unit Tests**: `tests/AgentAlpha.Tests/`
- **Test Naming**: `{ServiceName}Tests.cs`
- **Test Methods**: `{MethodName}_{Scenario}_{ExpectedResult}()`

### Mocking Pattern
All services use interfaces, making them easily mockable:
```csharp
var mockConnectionManager = new Mock<IConnectionManager>();
var taskExecutor = new TaskExecutor(mockConnectionManager.Object, ...);
```

## Common Modification Patterns

### Adding a New Service
1. Create interface in `Interfaces/I{ServiceName}.cs`
2. Create implementation in `Services/{ServiceName}.cs`
3. Register in `Extensions/ServiceCollectionExtensions.cs`
4. Add tests in `tests/AgentAlpha.Tests/{ServiceName}Tests.cs`

### Adding Configuration Options
1. Add properties to `AgentConfiguration.cs`
2. Add parsing logic in `FromEnvironment()` method
3. Add validation in `ValidateConfiguration()` method
4. Add tests for new validation rules

### Adding Command-Line Options
1. Update `Services/CommandLineParser.cs`
2. Add new case to the switch statement
3. Update `Models/TaskExecutionRequest.cs` if needed

## Key Design Principles

1. **Single Responsibility**: Each service has one clear purpose
2. **Dependency Injection**: All dependencies injected via constructor
3. **Interface Segregation**: Small, focused interfaces
4. **Validation Early**: Configuration validated at startup
5. **Descriptive Errors**: All error messages include context and suggestions
6. **Testability**: All services mockable via interfaces

## Error Handling Patterns

### Configuration Errors
- Thrown as `InvalidOperationException` with descriptive messages
- Include invalid value and list of valid options
- Suggest environment variable to set

### Runtime Errors  
- Logged with appropriate level (Error, Warning, Information)
- Include context about what operation was being performed
- Graceful degradation where possible

## Task Routing (P1)
A lightweight `ITaskRouter` decides whether a request is handled by the new
`IFastPathExecutor` (single-shot) or the classic ReAct `ITaskExecutor`.

This architecture makes the codebase highly maintainable for both human developers and AI coding assistants.