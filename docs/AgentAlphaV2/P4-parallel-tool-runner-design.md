# P4 – Parallel Tool Runner *(Deferred)*

> **STATUS: Deferred** – Work on this feature is postponed until a later planning cycle (earliest revisit: Q4 2024).  
> Implementation MUST NOT start before the design is re-approved.

## Goal
Execute independent tool calls concurrently to cut latency.

## Component
`ParallelToolRunner` wrapping `SimpleToolManager.ExecuteToolAsync`.

### Method
```csharp
public Task<IList<ToolCallResult>> RunAsync(IEnumerable<ToolCall> calls)
```
- Groups calls by `IsSideEffectFree`.  
- Runs side-effect-free calls in `Task.WhenAll` with bounded degree (cfg `MAX_CONCURRENCY`).  
- Serialises unsafe calls.

## Config & DI
```
MAX_PARALLEL_TOOL_CONCURRENCY=4
services.AddSingleton<IParallelToolRunner, ParallelToolRunner>();
```

## Thread-Safety Notes
- SimpleToolManager is stateless; connection is thread-safe per MCP library docs.

## Metrics
Log per-tool duration & overall wall-clock savings.

