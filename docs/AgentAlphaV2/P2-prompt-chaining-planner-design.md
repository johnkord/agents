# P2 – Prompt-Chaining Planner

## Overview
Replace single PlanningService call with Analyse → Outline → Detail chain for higher quality plans.

## New Services
- `ChainedPlanner` implements `IPlanner` (new interface).  
- Uses three sequential `OpenAIResponsesService` calls with gates.

## Step Prompts
1. **Analyse** – extract goals, constraints, tool gaps.  
2. **Outline** – high-level step list.  
3. **Detail** – expand each step incl. tool mappings.

## Interaction
`TaskExecutor` asks `IPlanner.CreatePlanAsync()`.  
ChainedPlanner can fall back to existing PlanningService on error.

## Sequence Diagram
```mermaid
sequenceDiagram
    participant User
    participant TaskExecutor as Task Executor
    participant ChainedPlanner as Chained Planner
    participant OpenAI as OpenAI API
    participant ToolManager as Tool Manager
    participant ConnectionManager as Connection Manager
    
    User->>TaskExecutor: ExecuteAsync(task)
    TaskExecutor->>ChainedPlanner: CreatePlanAsync(task)
    ChainedPlanner->>OpenAI: Analyse step
    OpenAI-->>ChainedPlanner: Goals and constraints
    ChainedPlanner->>OpenAI: Outline step
    OpenAI-->>ChainedPlanner: High-level outline
    ChainedPlanner->>OpenAI: Detail step
    OpenAI-->>ChainedPlanner: Detailed plan with tool mappings
    ChainedPlanner-->>TaskExecutor: Executable plan
    TaskExecutor->>ConnectionManager: ConnectAsync()
    ConnectionManager->>MCP: Establish Connection
    MCP-->>ConnectionManager: Connected
    
    TaskExecutor->>ToolManager: DiscoverToolsAsync()
    ToolManager->>ConnectionManager: ListToolsAsync()
    ConnectionManager->>MCP: tools/list
    MCP-->>ConnectionManager: Tool Definitions
    ConnectionManager-->>ToolManager: Tools List
    ToolManager-->>TaskExecutor: Available Tools
    
    TaskExecutor->>ToolManager: SelectToolsForTaskAsync()
    ToolManager-->>TaskExecutor: Selected Tools
    
    TaskExecutor->>ChainedPlanner: ExecutePlanAsync()
    ChainedPlanner->>OpenAI: Execute step 1
    OpenAI-->>ChainedPlanner: Result 1
    ChainedPlanner->>OpenAI: Execute step 2
    OpenAI-->>ChainedPlanner: Result 2
    ChainedPlanner->>OpenAI: Execute step 3
    OpenAI-->>ChainedPlanner: Result 3
    ChainedPlanner-->>TaskExecutor: Final result
    TaskExecutor-->>User: Task completed
```

## Testing
- Unit tests per stage (mock OpenAI).  
- Integration test comparing plan quality score vs baseline.

## Migration
Register `IPlanner` with feature flag `USE_CHAINED_PLANNER=true`.