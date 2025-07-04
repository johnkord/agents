# P5 – Worker Sub-Conversations

## Purpose
Allow main ConversationManager to spawn isolated sub-conversations for task segments and merge results.

## Additions
- `SpawnWorkerAsync(string subTask)` on `IConversationManager`.
- `WorkerConversation` (lightweight CM without markdown tracking).
- Aggregation strategy: parent receives summary + tool results.

## Lifecycle
```mermaid
graph LR
Parent -->|Spawn| Worker1 & Worker2
Worker1 -->|Summary| Parent
Worker2 -->|Summary| Parent
Parent -->|Synthesise| Continue main loop
```

## Session Handling
Workers write to same session table with `ParentId`.

## Testing
- Simulate parallel sub-tasks, verify summaries concatenated.

