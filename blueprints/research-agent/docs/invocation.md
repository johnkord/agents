# Research Agent — Invocation & Configuration Reference

Complete reference for running the Research Agent blueprint, including all configuration parameters, invocation patterns, and environment setup.

## Quick Start

```bash
cd blueprints/research-agent

# Build
dotnet build

# Run with a research question
dotnet run --project ResearchAgent.App -- "What are the current approaches to AI safety?"

# Pipe report to a file (progress + metadata go to stderr)
dotnet run --project ResearchAgent.App -- "What are the current approaches to AI safety?" > report.md

# Continue from a prior session
dotnet run --project ResearchAgent.App -- "Go deeper on constitutional AI" --prior sessions/a1b2c3.state.json
```

The agent runs a 5-stage pipeline (Planner → [Researcher ↔ Analyst]×N → Synthesizer → Verifier) and typically completes in **4–5 minutes** depending on model and query complexity.

## Prerequisites

| Requirement | Details |
|---|---|
| **.NET 10 SDK** | [Download](https://dotnet.microsoft.com/download/dotnet/10.0) — verify with `dotnet --version` |
| **OpenAI API key** | Or Azure OpenAI endpoint + key |
| **Environment** | Linux, macOS, Windows. Tested on WSL Ubuntu. |

If .NET is installed to a non-standard location (e.g. `$HOME/.dotnet`):
```bash
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"
```

## Configuration Parameters

All parameters can be set via JSON config, environment variables, user secrets, or command-line arguments.

### AI Settings

| Key | Default | Required | Description |
|---|---|---|---|
| `AI:Provider` | `openai` | No | AI provider. Supported: `openai`, `azure` |
| `AI:Model` | `gpt-4o` | No | Model name (OpenAI) or deployment name (Azure) |
| `AI:ApiKey` | *(empty)* | **Yes** | API key for the provider |
| `AI:Endpoint` | *(empty)* | Azure only | Azure OpenAI endpoint URL (e.g. `https://my-resource.openai.azure.com/`) |

### Logging

| Key | Default | Description |
|---|---|---|
| `Logging:MinLevel` | `Information` | Console log verbosity: `Debug`, `Information`, `Warning`, `Error` |

### Telemetry

| Key | Default | Description |
|---|---|---|
| `Telemetry:Enabled` | `false` | Enable OpenTelemetry distributed tracing with console exporter. Look for `Activity` lines in output. |

### Output

| Key | Default | Description |
|---|---|---|
| `Output:ShowHistory` | `false` | Print full agent interaction history to console after the report |
| `Output:ShowContextLog` | `false` | Print all Pensieve memory operations to console |
| `Output:SessionDir` | `sessions` | Directory for session JSON exports. Set to empty string `""` to disable export. |

### Environment

| Variable | Default | Description |
|---|---|---|
| `DOTNET_ENVIRONMENT` | `Development` | Selects which `appsettings.{env}.json` file to load |

## Configuration Sources (Priority Order)

Configuration is loaded in this order — later sources override earlier ones:

| Priority | Source | Example |
|---|---|---|
| 1 (lowest) | `appsettings.json` | Checked into repo with safe defaults |
| 2 | `appsettings.{env}.json` | `appsettings.Development.json` — git-ignored, for local secrets |
| 3 | Environment variables | Prefixed with `RESEARCH_AGENT_`, double underscore for nesting |
| 4 | User secrets | `dotnet user-secrets set "AI:ApiKey" "sk-..."` (secrets ID: `research-agent-blueprint`) |
| 5 (highest) | Command-line arguments | `-- --AI:Model=gpt-4.1` after the query |

## Setting Configuration

### Method 1: User Secrets (recommended for development)

```bash
cd ResearchAgent.App

dotnet user-secrets set "AI:ApiKey" "sk-your-key-here"
dotnet user-secrets set "AI:Model" "gpt-4o"

# For Azure OpenAI
dotnet user-secrets set "AI:Provider" "azure"
dotnet user-secrets set "AI:Endpoint" "https://your-resource.openai.azure.com/"
```

### Method 2: Environment Variables

Use the `RESEARCH_AGENT_` prefix. Nest keys using double underscores (`__`):

```bash
export RESEARCH_AGENT_AI__APIKEY="sk-your-key-here"
export RESEARCH_AGENT_AI__MODEL="gpt-4o"
export RESEARCH_AGENT_AI__PROVIDER="openai"

# Output settings
export RESEARCH_AGENT_OUTPUT__SHOWHISTORY="true"
export RESEARCH_AGENT_OUTPUT__SESSIONDIR="my-sessions"

# Telemetry
export RESEARCH_AGENT_TELEMETRY__ENABLED="true"

# Logging
export RESEARCH_AGENT_LOGGING__MINLEVEL="Debug"
```

### Method 3: appsettings.Development.json

Create `ResearchAgent.App/appsettings.Development.json` (git-ignored):

```json
{
  "AI": {
    "Provider": "openai",
    "Model": "gpt-4o",
    "ApiKey": "sk-your-key-here"
  }
}
```

### Method 4: Command-Line Arguments

Pass config after `--` (the first `--` separates dotnet args from app args):

```bash
dotnet run --project ResearchAgent.App -- "your query" --AI:Model=gpt-4.1 --Logging:MinLevel=Debug
```

## Invocation Patterns

### Basic invocation

```bash
dotnet run --project ResearchAgent.App -- "What are the current approaches to building AI research agents?"
```

### Multi-word query (no quotes needed if no special chars)

```bash
dotnet run --project ResearchAgent.App -- What are the current approaches to AI safety
```

All non-`--` arguments are joined into the query string. The `--prior` flag and config arguments (e.g. `--AI:Model=gpt-4.1`) are excluded.

### Continue from prior session

Pass a state file from a previous session to build on existing research. The Planner produces a **delta plan** — focusing on what's new or needs deepening.

```bash
# First session
dotnet run --project ResearchAgent.App -- "What are approaches to AI safety?"
# → sessions/a1b2c3.state.json

# Follow-up session using prior findings
dotnet run --project ResearchAgent.App -- "Go deeper on constitutional AI" --prior sessions/a1b2c3.state.json
# → sessions/d4e5f6.state.json (includes prior + new findings, links to parent)
```

The `--prior` flag:
- Loads prior findings, sources, and reflections into memory
- Instructs the Planner to produce a delta plan (skip already-answered questions)
- Creates a parent→child link between sessions
- Does **not** modify the prior state file (immutable)

### Interactive mode (no query argument)

```bash
dotnet run --project ResearchAgent.App
# Prompts: "Enter your research question: "
```

### Pipe report to file

The report goes to stdout; progress, metadata, and logs go to stderr. This means you can cleanly capture just the report:

```bash
# Report to file, progress visible in terminal
dotnet run --project ResearchAgent.App -- "your query" > report.md

# Report to file, suppress progress too
dotnet run --project ResearchAgent.App -- "your query" > report.md 2>/dev/null

# Capture everything
dotnet run --project ResearchAgent.App -- "your query" > report.md 2> progress.log
```

### Verbose debugging

```bash
dotnet run --project ResearchAgent.App -- "your query" \
  --Logging:MinLevel=Debug \
  --Output:ShowHistory=true \
  --Output:ShowContextLog=true \
  --Telemetry:Enabled=true
```

### Different model

```bash
dotnet run --project ResearchAgent.App -- "your query" --AI:Model=gpt-4.1-mini
```

### Azure OpenAI

```bash
dotnet run --project ResearchAgent.App -- "your query" \
  --AI:Provider=azure \
  --AI:Model=my-deployment \
  --AI:ApiKey=your-azure-key \
  --AI:Endpoint=https://my-resource.openai.azure.com/
```

### Disable session export

```bash
dotnet run --project ResearchAgent.App -- "your query" --Output:SessionDir=""
```

### Run from pre-built binary

```bash
dotnet build -c Release
./ResearchAgent.App/bin/Release/net10.0/ResearchAgent.App "your query"
```

## Output Model

Output follows a **stdout/stderr separation** pattern so the report can be piped cleanly:

| Channel | Content | Purpose |
|---|---|---|
| **stdout** | Research report (Markdown) | The primary artifact — pipe to file, process, display |
| **stderr** | Progress events, metadata, logs | Real-time visibility into the research pipeline |
| **State file** | `{sessionId}.state.json` | Importable research state for follow-up sessions (`--prior`) |
| **Session log** | `{sessionId}.json` | Full trajectory for analysis pipelines |

### stdout — The Report

Stdout contains only the synthesized Markdown report. Nothing else. This means:

```bash
# Just the report
dotnet run -- "query" > report.md
```

### stderr — Progress & Metadata

During execution, progress events stream to stderr:

```
► Planning...
↻ Research iteration 1/2
► Researching (iteration 1)...
  ● 8 findings accumulated
► Analyzing (iteration 1)...
  △ 2 knowledge gap(s) — iterating
↻ Research iteration 2/2
► Researching (iteration 2)...
  ● 14 findings accumulated
► Analyzing (iteration 2)...
► Synthesizing report (14 findings, 9 sources)...
► Verifying claims...
──────────────────────────────────────────────────────
Session: a1b2c3d4e5f6
Findings: 14
Sources: 9
Agent interactions: 7
Research iterations: 2
Verification: 11/12 claims passed (92%)
Duration: 247.3s
──────────────────────────────────────────────────────
State file: sessions/a1b2c3d4e5f6.state.json
Session log: sessions/a1b2c3d4e5f6.json
```

When continuing from a prior session, the progress output includes:

```
  Loaded prior state: session abc123 — 10 findings, 7 sources
► Planning (with prior context)...
```

### State File (`.state.json`)

The importable research state — pass this to `--prior` for follow-up sessions. Contains:

| Field | Description |
|---|---|
| `metadata` | Session ID, query, timestamps, model, parent session link |
| `plan` | Raw planner output, sub-question IDs, completed questions |
| `findings[]` | Distilled research findings with confidence scores |
| `sources[]` | Sources consulted with reliability scores |
| `reflections[]` | Analyst gap observations and methodological notes |
| `quality` | Aggregate metrics: finding count, verification pass rate, failed claims |

Analyze state files with `jq`:

```bash
# View prior session's query and finding count
jq '{query: .metadata.query, findings: .quality.findingCount}' sessions/*.state.json

# List all findings from a session
jq '.findings[] | {content, confidence, subQuestionId}' sessions/a1b2c3.state.json

# Check verification quality
jq '.quality | {passRate: .verificationPassRate, failed: .failedClaims}' sessions/a1b2c3.state.json
```

### Session Log (`.json`)

Full trajectory for analysis pipelines — agent interactions with timestamps, findings, sources, context log, and aggregate metrics. See the main [README.md](../README.md) for schema details and `jq` commands.

### Log Levels

| Level | What you see |
|---|---|
| `Error` | Only failures and missing config |
| `Warning` | Unexpected event types, empty agent responses |
| `Information` | Agent invocations (`▶`/`■`), event breakdown, session summary, timing |
| `Debug` | ChatClient creation, tool discovery, workflow build, response previews, token estimates, SuperStep events |

## Error Handling

| Error | Cause | Fix |
|---|---|---|
| `API key not configured` | `AI:ApiKey` is empty | Set via user secrets, env var, or config file |
| `Unknown AI provider: X` | `AI:Provider` not `openai` or `azure` | Use a supported provider |
| `AI:Endpoint required for Azure` | Provider is `azure` but no endpoint | Set `AI:Endpoint` |
| `Research session failed` | Runtime error (API timeout, rate limit, etc.) | Check full stack trace, retry, or reduce query complexity |

## Default Configuration File

The full `appsettings.json` with all defaults:

```json
{
  "AI": {
    "Provider": "openai",
    "Model": "gpt-4o",
    "ApiKey": "",
    "Endpoint": ""
  },
  "Logging": {
    "MinLevel": "Information"
  },
  "Telemetry": {
    "Enabled": false
  },
  "Research": {
    "MaxIterations": 2,
    "VerificationEnabled": true
  },
  "Output": {
    "ShowHistory": false,
    "ShowContextLog": false,
    "SessionDir": "sessions"
  }
}
```

---

*Back to [README](../README.md)*
