# Forge CLI Reference

## Quick Start

```bash
# Start the MCP server first
dotnet run --project blueprints/mcp-server/McpServer.csproj &

# Run a task
dotnet run --project blueprints/coding-agent/src/Forge.App -- "Fix the bug in Parser.cs"

# Interactive mode (prompts for task)
dotnet run --project blueprints/coding-agent/src/Forge.App
```

## Modes

### Run (default)

Execute an agent task. The task can be provided as positional arguments or interactively.

```bash
# Positional task (everything before the first -- flag)
dotnet run --project Forge.App -- Fix the flaky test in AuthService

# Interactive prompt
dotnet run --project Forge.App
> Enter task: _
```

### Dry Run (`--dry-run`)

Print configuration and discovered tools without calling the LLM.

```bash
dotnet run --project Forge.App -- --dry-run
```

### Resume (`--resume <session.jsonl>`)

Resume an interrupted session using the handoff note from a previous session log.

```bash
dotnet run --project Forge.App -- --resume sessions/20260320-143022-Fix-the-bug.jsonl
```

The original task name is preserved. The agent receives a continuation prompt with the handoff summary, discovery context, assumptions, and remaining work.

### Analyze (`--analyze <path>`)

Offline analysis of session logs. Computes 6 efficiency metrics per session.

```bash
# Single session — detailed report
dotnet run --project Forge.App -- --analyze sessions/20260320-143022-Fix-the-bug.jsonl

# All sessions in a directory — individual reports + aggregate summary
dotnet run --project Forge.App -- --analyze sessions/

# Glob pattern
dotnet run --project Forge.App -- --analyze "sessions/2026032*.jsonl"
```

**Metrics reported:** steps/task, tokens/step growth, read coalescence rate, verification compliance, consolidation capture rate, episode count, pivot count.

## Configuration

Configuration is loaded in this order (later sources override earlier):

1. `appsettings.json` (in Forge.App directory)
2. Environment variables with `FORGE_` prefix
3. .NET User Secrets (id: `forge-coding-agent`)
4. Command-line `--Key=Value` arguments

### Options

| Key | Class Default | appsettings.json | Description |
|-----|---------------|------------------|-------------|
| `Model` | *(required)* | `gpt-5.4` | OpenAI model name |
| `MaxSteps` | `30` | `30` | Maximum agent loop iterations |
| `MaxTotalTokens` | `500000` | `500000` | Token budget (prompt + completion) |
| `Temperature` | `0` | `0` | LLM sampling temperature |
| `ReasoningEffort` | `null` | `Medium` | `Low`, `Medium`, or `High`. May be escalated to `High` automatically on consecutive failures (progressive deepening). |
| `ObservationMaxLines` | `200` | `200` | Max lines kept from tool output |
| `ObservationMaxChars` | `10000` | `10000` | Max characters kept from tool output |
| `Workspace` | current directory | *(not set)* | Workspace root. Guardrails restrict file operations to this path. |
| `SessionsDir` | `{Workspace}/sessions` | *(not set)* | Where session JSONL logs and structured logs are written |
| `LessonsPath` | `null` (disabled) | *(not set)* | Path to LESSONS.md for cross-session learning. Program.cs defaults to `{SessionsDir}/lessons.md` |
| `McpServerUrl` | — | `http://localhost:5000/mcp` | MCP tool server endpoint |
| `OpenAIKey` | — | *(empty)* | OpenAI API key. **Required.** Use user-secrets or env var. |

### Environment Variables

All options above can be set with `FORGE_` prefix:

```bash
export FORGE_MaxSteps=15
export FORGE_MaxTotalTokens=300000
export FORGE_ReasoningEffort=High
export FORGE_Model=gpt-5.2
export FORGE_Workspace=/path/to/project
```

Additional environment variables used by tools (not configuration options):

| Variable | Default | Used By |
|----------|---------|---------|
| `FORGE_MEMORY_ROOT` | `~/.forge/memories` | MemoryTool, ManageTodosTool, AgentLoop (todo plan loading) |
| `FORGE_SUBAGENT_DEPTH` | `0` | RunSubagentTool (recursion depth tracking, max 3) |

### User Secrets

```bash
# Set the API key (recommended over config files)
cd blueprints/coding-agent/src/Forge.App
dotnet user-secrets set "OpenAIKey" "sk-..."

# Override any option
dotnet user-secrets set "Model" "gpt-5.2"
dotnet user-secrets set "ReasoningEffort" "High"
```

## Output

### Session Logs

Each run creates a JSONL file in `{SessionsDir}/`:

```
sessions/20260320-143022-fff-Fix-the-bug-in-Parser-cs.jsonl
```

Events: `session_start`, `step` (per agent loop iteration), `session_end`, `session_handoff`.

### Structured Logs

Serilog writes to both console and a daily CLEF file:

```
sessions/forge-20260320.clef
```

Log level is `Debug`. Retained for 30 days.

### Lessons

If `LessonsPath` is set, failed or costly (>300K tokens) sessions append a lesson:

```
- [2026-03-20] fail: "Fix the flaky test..." — Stopped: maximum steps (30) reached (type: StaleContext). Steps: 30, Tokens: 489,231
```

### Exit Codes

| Code | Meaning |
|------|---------|
| `0` | Task completed successfully (or `--dry-run` / `--analyze`) |
| `1` | No task provided, invalid arguments, or no session files found |
| `2` | Unhandled exception |
