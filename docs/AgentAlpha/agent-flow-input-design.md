# AgentAlpha v2 – Flow-Driven Architecture

## 1  Goal  
Callers supply an **Agent Flow Object (AFO)**—a JSON state-machine that orchestrates modular *state-handlers* (plan, react, summarise, evaluate, etc.).  
**Why?**  

* One-shot or multi-step tasks on demand → cost-efficient.  
* Built-in loops, branching, human approvals, and worker spawning.  
* Future patterns—parallel branches, sub-flows—slot in without code edits.

---

## 2  Core Concepts

| Term | Meaning |
|------|---------|
| **State-Handler** | `IAgentStateHandler` implementation (e.g., `PlanningHandler`). |
| **AFO** | Version-controlled JSON describing states, transitions, guards, error routes. |
| **Transition** | Directed edge with optional `when` condition or `onError` fallback. |
| **TaskContext** | Short-term *+* long-term memory persisted between states. |

---

## 3  AFO Schema (v1.1 excerpt)

```jsonc
{
  "$schema": "https://agentalpha.ai/flow/v1.1",
  "start": "plan",
  "allowLoops": true,
  "states": {
    "plan": {
      "handler": "planning",
      "next": "execute"
    },
    "execute": {
      "handler": "react",
      "params": { "maxIterations": 3 },
      "next": [
        { "when": "context.completed",         "to": "done"     },
        { "when": "context.error != null",     "to": "recover"  },
        { "when": "else",                      "to": "summarise"}
      ],
      "onError": "recover",
      "approvalRequired": false
    },
    "recover": {
      "handler": "retry",
      "params": { "maxRetries": 2 },
      "next": "execute"
    },
    "summarise": { "handler": "summarise", "next": "evaluate" },
    "evaluate":  {
      "handler": "evaluate",
      "next": [
        { "when": "context.planQuality >= 0.8", "to": "execute" },
        { "when": "else",                       "to": "plan"    }
      ]
    },
    "done": { "handler": "complete" }
  }
}
```

---

## 4  Implementation Roadmap (v2 → v2.1)

| Phase | Area | Key Tasks | Target Files |
|-------|------|-----------|--------------|
| 1 | **Contracts** | • Add `IAgentStateHandler` with `Task ExecuteAsync(TaskContext ctx)`<br>• Add `IAgentFlowEngine` for running AFOs | `src/Agent/AgentAlpha/Interfaces/` |
| 2 | **Core Engine** | • Implement `AgentFlowEngine`:<br> • JSON parsing & validation (System.Text.Json)<br> • State-loop with guard evaluation<br> • Error & approval routing | `src/Agent/AgentAlpha/Services/AgentFlowEngine.cs` |
| 3 | **Default Handlers** | • PlanningHandler → wraps `IPlanner`<br>• ReactHandler → wraps `IConversationManager`<br>• RetryHandler → simple exponential back-off<br>• SummariseHandler → calls `ConversationManager.UpdateMarkdownAsync`<br>• EvaluateHandler → wraps `IPlanEvaluator`<br>• CompleteHandler → finalises session & logging | `src/Agent/AgentAlpha/Services/StateHandlers/*.cs` |
| 4 | **DI & Wiring** | • Register handlers & engine in `ServiceCollectionExtensions`<br>• Add helper factory to resolve by handler-name | `Extensions/ServiceCollectionExtensions.cs` |
| 5 | **CLI Integration** | • Extend `CommandLineParser` to accept `--flow <file|json>`<br>• If supplied, `Program.cs` executes via `IAgentFlowEngine` instead of router/executor path | `Services/CommandLineParser.cs`, `Program.cs` |
| 6 | **Configuration** | • Add `AgentConfiguration.EnableFlowEngine` (env var `ENABLE_FLOW_ENGINE`, default `false`) | `Configuration/AgentConfiguration.cs` |
| 7 | **Testing** | • Unit tests for JSON parsing, guard evaluation, happy-path & failure routing | `tests/AgentAlpha.Tests/AgentFlowEngineTests.cs` |
| 8 | **Docs & Samples** | • Provide sample AFO files under `docs/flows/`<br>• Update README & architecture diagrams | `docs/AgentAlpha/*` |

> **Roll-out:** keep feature gated (`ENABLE_FLOW_ENGINE=false`) until test coverage ≥ 90 % and latency/$$ benchmarks meet current baseline.
