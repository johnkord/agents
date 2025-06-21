---
name: research-agent-investigation
description: >
  Diagnoses issues with the Research Agent blueprint — a 4-agent sequential pipeline
  built on .NET 10 + Microsoft Agent Framework (MAF). Use when the research agent
  produces unexpected results, fails, hangs, generates empty reports, or when you need
  to understand what happened during a research session. Contains MAF API pitfalls and
  non-obvious runtime behaviors that cannot be inferred from reading the source code.
---

# Research Agent Investigation

Hard-won knowledge for troubleshooting the Research Agent blueprint at `blueprints/research-agent/`. Everything below was discovered through debugging and is **not documented in MAF** or inferable from the codebase alone.

## Environment

The .NET 10 SDK is installed at `$HOME/.dotnet` (not on PATH by default):
```bash
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"
```

## MAF API Traps (v1.0.0-rc3)

These are the critical pitfalls. Each one caused silent failures or hangs during development.

### 1. RunStreamingAsync Hangs on Sequential Workflows

`InProcessExecution.RunStreamingAsync()` does **NOT work** for sequential workflows. It silently hangs after the first agent completes — no error, no timeout, just blocks forever. You must use `InProcessExecution.RunAsync()` instead. The code already uses `RunAsync`; do NOT "improve" it to streaming.

### 2. NewEvents is a One-Shot IEnumerable

`Run.NewEvents` can only be enumerated **once**. A second pass yields nothing — no exception, just empty. Always `.ToList()` immediately:
```csharp
var run = await InProcessExecution.RunAsync(workflow, input, sessionId, ct);
var events = run.NewEvents.ToList();  // Materialize ONCE — do not skip this
```

### 3. AgentResponseUpdateEvent Subclass Trap

`AgentResponseUpdateEvent` is a **subclass** of `WorkflowOutputEvent`. This is not obvious from the API surface. In a C# switch:

```csharp
// BUG — silently drops ALL agent text (AgentResponseUpdateEvent matches first)
case AgentResponseUpdateEvent responseEvt:
    break;
case WorkflowOutputEvent outputEvt:
    // Never reached for agent responses!
```

The fix: handle only `WorkflowOutputEvent` and cast `.Data` to `AgentResponseUpdate`. The current code does this correctly — do not add a separate `AgentResponseUpdateEvent` case.

### 4. ExecutorId Format

`WorkflowOutputEvent.ExecutorId` uses the undocumented format `"AgentName_hexguid"` (e.g., `Planner_a1b2c3`). The special value `"OutputMessages"` is a framework-internal forwarder, not an actual agent — filter it out.

## Runtime Expectations

- A normal research session takes **4–5 minutes** with gpt-5.4 (4 agents, simulated tools). If it hasn't produced output after 10 minutes, something is wrong.
- A healthy run produces ~800–1200 `WorkflowOutputEvent`s across all agents. Fewer than 50 suggests an early failure.
- All tools are **simulated** — they return plausible fake data. Empty findings/sources means the agent chose not to call tools, not that the tools broke.

## Config Gotcha

Config files are loaded from `AppContext.BaseDirectory` (the build output directory), **not** the current working directory. If `appsettings.json` isn't being picked up after a code change, check that the `.csproj` has `<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>` on the config files.
