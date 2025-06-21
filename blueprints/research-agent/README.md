# Research Agent Blueprint

> A general-purpose research agent built on **.NET 10** and the **Microsoft Agent Framework** (v1.0.0-rc3). Decomposes a research question into sub-queries, iteratively searches and analyzes with reflective memory, synthesizes a structured report, and verifies claims against evidence.

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│              Iterative Orchestration (V2)                            │
│              Microsoft Agent Framework                               │
│                                                                      │
│  ┌──────────┐   ┌────────────┐   ┌──────────┐                      │
│  │ Planner  │──▶│ Researcher │──▶│ Analyst  │                      │
│  │          │   │            │   │          │                      │
│  │ Decompose│   │ Search     │   │ Evaluate │──── gaps? ──┐       │
│  │ query    │   │ Reflect ↻  │   │ Gaps     │             │       │
│  │ into     │   │ Note ✎    │   │ Patterns │    ┌────────▼──┐    │
│  │ sub-Qs   │   │ Prune 🗑  │   │          │    │ Iterate?  │    │
│  │ (SQ1..N) │   └──────▲─────┘   └──────────┘    │ yes → loop│    │
│  └──────────┘          │                          │ no → next │    │
│                        │          ┌───────────────┴───────────┘    │
│                        └──────────┘                                 │
│                        (max N iterations)                           │
│                                                                      │
│                  ┌───────────┐   ┌──────────┐                       │
│                  │Synthesizer│──▶│ Verifier │                       │
│                  │           │   │          │                       │
│                  │ Write     │   │ Checklist│                       │
│                  │ report    │   │ verify   │                       │
│                  │ citations │   │ claims   │                       │
│                  └───────────┘   └──────────┘                       │
│                                       │                              │
│              ┌──────────────────┐     │                              │
│              │ Research Memory  │◀────┘                              │
│              │ (Persistent)     │                                    │
│              │                  │                                    │
│              │ • Findings       │ ◀── Pensieve Pattern              │
│              │ • Working Notes  │     read → note → prune           │
│              │ • Reflections    │ ◀── RE-Searcher goal-reflect      │
│              │ • Progress       │ ◀── Sub-question tracking         │
│              │ • Verification   │ ◀── FINDER checklist              │
│              │ • Sources        │                                    │
│              └──────────────────┘                                    │
└─────────────────────────────────────────────────────────────────────┘
```

### Pipeline Stages

| Stage | Agent | Plugins (Tools) | Paper Inspiration |
|---|---|---|---|
| **1. Planning** | Planner | *(none — pure reasoning)* | HiMAC hierarchical decomposition |
| **2. Research** | Researcher | WebSearch, ContentExtraction, NoteTaking | Pensieve read-note-prune, RE-Searcher goal-reflect |
| **3. Analysis** | Analyst | NoteTaking | Agent-as-a-Judge, CoRefine gap analysis |
| **↻ Iterate** | *(orchestrator)* | *(control flow)* | SFR-DeepResearch adaptive iteration |
| **4. Synthesis** | Synthesizer | NoteTaking, ReportFormatting | CASTER Scientific Discovery workflow |
| **5. Verification** | Verifier | Verification | FINDER checklist, DeepVerifier rubric |

## Research Papers Informing This Design

| Paper | Key Contribution Used |
|---|---|
| **Pensieve / StateLM** (2602.12108) | Self-context engineering: read → note → prune cycle. Memory tools: `deleteContext`, `readChunk`, `updateNote`. 52% on BrowseComp-Plus vs 5% baseline. |
| **CASTER** (2601.19793) | Multi-agent workflow routing. Scientific Discovery template: Researcher → Theorist → Experimenter. |
| **RE-Searcher** (2503.07470) | Goal-oriented search with explicit reflection. Reason→Search→Reflect loop improves search quality. |
| **Reflection-Driven Control** (2506.08890) | Reflective Memory Repository — store failed approaches to avoid repeating them. |
| **FINDER** (2507.13696) | Checklist-based verification of research reports. DEFT failure taxonomy. |
| **SFR-DeepResearch** | Adaptive iteration — keep researching until coverage thresholds are met. |
| **Agentic RAG Survey** (2501.09136) | Agentic patterns (reflection, planning, tool use) applied to retrieval pipelines. |
| **HiMAC** | Hierarchical task decomposition for complex multi-step plans. |
| **Monadic Context Engineering** (2512.22431) | Case study of composable research agent: plan → execute → synthesize → format. |
| **Agent-as-a-Judge** | Using LLM agents to evaluate research quality and source reliability. |
| **GAIA2** (2602.11964) | Benchmarks showing exploration and systematic information gathering drive agent success. |

## Technology Stack

| Component | Technology |
|---|---|
| **Runtime** | .NET 10 (LTS) with C# 14 |
| **Agent Framework** | Microsoft Agent Framework 1.0.0-rc3 (`Microsoft.Agents.AI.*`) |
| **Orchestration** | Manual phase-by-phase via `InProcessExecution.RunAsync` with iterative loop |
| **Agent Type** | `ChatClientAgent` via `AsAIAgent()` with `[Description]` tools |
| **Tool Registration** | `AIFunctionFactory.Create()` from `Microsoft.Extensions.AI` |
| **Workflow Execution** | `InProcessExecution.RunAsync` → `Run.NewEvents` |
| **AI Providers** | OpenAI, Azure OpenAI (configurable via `IChatClient` abstraction) |
| **Configuration** | `Microsoft.Extensions.Configuration` (user secrets, env vars, JSON) |

## Project Structure

```
blueprints/research-agent/
├── ResearchAgent.sln
├── .gitignore
├── README.md                              ← You are here
│
├── docs/
│   └── invocation.md                      ← Full configuration & invocation reference
│
├── sessions/                              ← Session exports (git-ignored)
│   ├── {sessionId}.state.json             ← Importable research state (pass to --prior)
│   └── {sessionId}.json                   ← Full trajectory for analysis
│
├── ResearchAgent.App/                     ← Console entry point + orchestrator
│   ├── Program.cs                         ← CLI entry, config, logging, session export
│   ├── ResearchOrchestrator.cs            ← Wires up 5 agents with iterative loop + verifier
│   ├── Diagnostics.cs                     ← OpenTelemetry ActivitySource for tracing
│   └── appsettings.json                   ← Default configuration
│
├── ResearchAgent.Core/                    ← Domain models + memory
│   ├── Models/
│   │   ├── ResearchState.cs               ← State, findings, sources, sub-questions
│   │   └── SessionExport.cs               ← JSON-serializable session export DTOs
│   └── Memory/
│       └── ResearchMemory.cs              ← Pensieve memory (persistent notes, context builder)
│
└── ResearchAgent.Plugins/                 ← Agent plugins (tools via [Description])
    ├── Search/
    │   └── WebSearchPlugin.cs             ← Web + academic search
    ├── Content/
    │   ├── ContentExtractionPlugin.cs     ← URL/PDF content fetching
    │   └── NoteTakingPlugin.cs            ← Pensieve note CRUD + progress + reflections
    ├── Verification/
    │   └── VerificationPlugin.cs          ← FINDER checklist verification
    └── Synthesis/
        └── ReportFormattingPlugin.cs      ← Markdown report formatting
```

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- An OpenAI API key (or Azure OpenAI endpoint + key)

### Configuration

> **Full reference**: See [docs/invocation.md](docs/invocation.md) for all configuration parameters, environment variable mapping, invocation patterns, and troubleshooting.

Set your API key using one of these methods:

```bash
# Option 1: User secrets (recommended for development)
cd ResearchAgent.App
dotnet user-secrets set "AI:ApiKey" "sk-your-key-here"
dotnet user-secrets set "AI:Model" "gpt-4o"

# Option 2: Environment variables
export RESEARCH_AGENT_AI__APIKEY="sk-your-key-here"
export RESEARCH_AGENT_AI__MODEL="gpt-4o"

# Option 3: Edit appsettings.json directly (don't commit secrets!)
```

For Azure OpenAI:
```bash
dotnet user-secrets set "AI:Provider" "azure"
dotnet user-secrets set "AI:Model" "your-deployment-name"
dotnet user-secrets set "AI:ApiKey" "your-azure-key"
dotnet user-secrets set "AI:Endpoint" "https://your-resource.openai.azure.com/"
```

### Running

```bash
cd blueprints/research-agent

# Build
dotnet build

# Run with a research question (report → stdout, progress → stderr)
dotnet run --project ResearchAgent.App -- "What are the current approaches to building AI research agents?"

# Pipe just the report to a file
dotnet run --project ResearchAgent.App -- "AI research agents" > report.md

# Continue from a prior session
dotnet run --project ResearchAgent.App -- "Go deeper on tool use" --prior sessions/a1b2c3.state.json

# Run with verbose output
dotnet run --project ResearchAgent.App -- "Your question here" --Output:ShowHistory=true --Output:ShowContextLog=true

# Run with debug logging
dotnet run --project ResearchAgent.App -- "Your question here" --Logging:MinLevel=Debug
```

### Output Model

The agent uses **stdout/stderr separation** — the report goes to stdout, everything else to stderr:

| Channel | Content |
|---|---|
| stdout | Research report (Markdown) — pipeable to file |
| stderr | Progress events, metadata, session paths, logs |
| `.state.json` | Importable research state for `--prior` |
| `.json` | Full trajectory for analysis |

### Session Continuation (`--prior`)

Pass a `.state.json` file from a previous session to build on existing research:

```bash
# Session 1
dotnet run --project ResearchAgent.App -- "AI safety approaches"
# → sessions/a1b2c3.state.json

# Session 2: continue with prior findings
dotnet run --project ResearchAgent.App -- "Deep dive on RLHF" --prior sessions/a1b2c3.state.json
# → sessions/d4e5f6.state.json (links to parent a1b2c3)
```

The Planner sees prior findings and produces a **delta plan** — researching only what's new.

### Session Export

Every research session is automatically saved to `sessions/` (configurable via `Output:SessionDir`). Two files per session:

#### Session file structure

```
sessions/
├── a1b2c3d4e5f6.state.json   ← Importable research state (findings, sources, plan, quality)
├── a1b2c3d4e5f6.json         ← Full trajectory (agent interactions, context log, metrics)
├── f7e8d9c0b1a2.state.json
├── f7e8d9c0b1a2.json
└── ...
```

**State file** (`.state.json`) — the file you pass to `--prior`:

| Field | Description |
|---|---|
| `metadata` | Session ID, query, timestamps, model, parent session ID |
| `plan` | Raw planner output, sub-question IDs, completed questions |
| `findings[]` | Distilled findings with confidence scores, source IDs |
| `sources[]` | Sources with reliability scores, URLs, types |
| `reflections[]` | Analyst gap observations and methodological notes |
| `quality` | Aggregate metrics: finding/source count, verification pass rate, failed claims |

**Session log** (`.json`) — the full trajectory for analysis:

| Field | Description |
|---|---|
| `sessionId` | Unique session identifier |
| `query` | The original research question |
| `provider` / `model` | AI provider and model used (e.g. `openai`, `gpt-4o`) |
| `startedAt` / `completedAt` | ISO 8601 timestamps |
| `durationMs` | Total wall-clock time for the session |
| `report` | The final synthesized research report |
| `agentInteractions[]` | Timestamped list of each agent's response (agent name, role, text, timestamp) |
| `findings[]` | All research findings with confidence scores, source IDs, tags |
| `sources[]` | All discovered sources with reliability scores, URLs, types |
| `contextLog[]` | Timestamped memory operations (Pensieve read/note/prune events) |
| `metrics` | Aggregate counts: findings, sources, interactions, report length, avg confidence, iterations, verification pass rate |

#### Configuration

```json
{
  "Output": {
    "SessionDir": "sessions",
    "ShowHistory": false,
    "ShowContextLog": false
  },
  "Logging": {
    "MinLevel": "Information"
  }
}
```

| Config Key | Default | Description |
|---|---|---|
| `Output:SessionDir` | `sessions` | Directory for session JSON files. Set to empty string to disable export. |
| `Output:ShowHistory` | `false` | Print agent interaction history to console |
| `Output:ShowContextLog` | `false` | Print memory operations to console |
| `Logging:MinLevel` | `Information` | Console log level (`Debug`, `Information`, `Warning`, `Error`) |

#### Using session data for analysis

```bash
# List sessions by duration (longest first)
jq -r '[.sessionId, .durationMs, .metrics.findingCount, .query[:60]] | @tsv' sessions/*.json | sort -t$'\t' -k2 -rn

# Find sessions with low-confidence findings
jq 'select(.metrics.averageFindingConfidence < 0.5) | {sessionId, query, avgConf: .metrics.averageFindingConfidence}' sessions/*.json

# Extract all agent interactions for a session
jq '.agentInteractions[] | {agent, timestamp, chars: .charCount}' sessions/a1b2c3d4e5f6.json
```

## How It Works

### 1. Planning (HiMAC Pattern)

The Planner agent decomposes the research query into 3-7 specific, answerable sub-questions ordered by dependency. This follows HiMAC's hierarchical task decomposition pattern — break the abstract question into concrete, independently searchable queries.

### 2. Research (Pensieve + RE-Searcher Goal-Reflect Cycle)

The Researcher agent has access to search, content extraction, note-taking, and reflection tools. For each sub-question it executes a cycle combining StateLM's Pensieve paradigm with RE-Searcher's goal-reflect pattern:

1. **Goal** → State explicitly what information is needed for this sub-question
2. **Search** → Find relevant sources using web or academic search
3. **Reflect** → Did results address the goal? If not, record a reflection and reformulate
4. **Read** → Fetch and extract content from promising URLs
5. **Note** → Distill key findings into persistent `ResearchFinding` objects
6. **Prune** → Only distilled notes carry forward; raw content is ephemeral

Reflections are stored in a Reflective Memory Repository (from AAAI 2026 research). Before starting each sub-question, the Researcher checks past reflections to avoid repeating failed approaches.

### 3. Analysis (Agent-as-a-Judge + Gap Detection)

The Analyst agent reviews all accumulated findings and notes, evaluating:
- Source reliability and finding confidence
- Patterns, consensus, and contradictions across sources
- **Knowledge gaps** — uses `RecordKnowledgeGap` to flag under-researched sub-questions
- Recommended narrative structure for the report

If critical gaps are identified, the orchestrator loops back to the Researcher for targeted follow-up (inspired by SFR-DeepResearch adaptive iteration).

### 4. Synthesis (CASTER Scientific Discovery)

The Synthesizer agent produces the final report following CASTER's Scientific Discovery workflow pattern — structured output with executive summary, findings by theme, analysis, limitations, and sourced references.

### 5. Verification (FINDER Checklist Pattern)

The Verifier agent (new in V2) implements the Asymmetry Thesis from FINDER: verification is cheaper than generation. It:

1. Generates a checklist of every factual claim in the report
2. Checks each claim against the accumulated findings and sources
3. Records verdicts: SUPPORTED, UNSUPPORTED, CONTRADICTED, or UNVERIFIABLE
4. Categorizes failures using the DEFT taxonomy (Factual, Reasoning, Completeness, Coherence, Attribution)
5. Produces a verification summary with pass rate

## Extending the Agent

### Add Real Search API

Replace the simulated search in `WebSearchPlugin.cs`:

```csharp
// Example: Bing Search API — just use [Description], no [KernelFunction] needed
[Description("Search the web for information on a topic")]
public async Task<string> SearchWebAsync(
    [Description("The search query")] string query,
    [Description("Maximum results to return")] int maxResults = 10)
{
    using var client = new HttpClient();
    client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _apiKey);
    var response = await client.GetAsync(
        $"https://api.bing.microsoft.com/v7.0/search?q={Uri.EscapeDataString(query)}&count={maxResults}");
    // Parse and return results...
}
```

### Switch to Handoff Orchestration

For dynamic routing (e.g., the Researcher decides it needs more planning):

```csharp
// Use HandoffsWorkflowBuilder for dynamic agent-to-agent routing
var workflow = AgentWorkflowBuilder
    .CreateHandoffBuilderWith(plannerAgent)
    .WithHandoff(plannerAgent, researcherAgent, "Start researching the plan")
    .WithHandoff(researcherAgent, analystAgent, "When enough findings are gathered")
    .WithHandoff(researcherAgent, plannerAgent, "When the plan needs revision")
    .WithHandoff(analystAgent, synthesizerAgent, "When analysis is complete")
    .WithHandoff(analystAgent, researcherAgent, "When more research is needed")
    .Build();

// Execute — same pattern as sequential
var run = await InProcessExecution.RunAsync(workflow, input, sessionId, ct);
var events = run.NewEvents.ToList();
```

### Add Vector Memory (RAG)

For large-scale research with many findings, add semantic search via `Microsoft.Extensions.VectorData`:

```csharp
// MAF integrates with Microsoft.Extensions.VectorData for RAG
// Add a TextSearchProvider as an AI context provider on the agent
var searchProvider = new TextSearchProvider(vectorStore, new TextSearchProviderOptions
{
    CollectionName = "findings",
    EmbeddingGenerator = embeddingGenerator,
});

// Attach to agent via ChatClientBuilder
var agent = new ChatClientBuilder(chatClient)
    .UseAIContextProviders(searchProvider)
    .BuildAIAgent(instructions: "...", name: "Researcher");
```

### Add Group Chat

For collaborative discussion between agents:

```csharp
// GroupChatWorkflowBuilder enables multi-turn agent collaboration
var workflow = AgentWorkflowBuilder
    .CreateGroupChatBuilderWith(managerFactory)
    .AddParticipants([analystAgent, researcherAgent, synthesizerAgent])
    .Build();
```

## Design Decisions

| Decision | Rationale |
|---|---|
| **Sequential over Handoff** | V2 uses manual phase orchestration for iterative loop control. Planner runs once; Researcher↔Analyst iterate; Synthesizer and Verifier run once. Upgradable to `HandoffsWorkflowBuilder`. |
| **Shared ResearchMemory** | All agents access the same Pensieve memory — findings persist across the pipeline. |
| **Tools per agent, not global** | Each agent gets only the tools it needs via `AIFunctionFactory.Create()`. |
| **`[Description]` over `[KernelFunction]`** | MAF uses standard `System.ComponentModel.DescriptionAttribute` — no framework-specific attributes. |
| **Simulated tools** | Blueprint focuses on architecture; swap in real APIs without changing agent logic. |
| **Iterative loop** | Researcher↔Analyst loop with configurable max iterations (default: 2). Exits early when no critical gaps remain. |
| **Verification phase** | FINDER-inspired checklist verification catches unsupported claims before output. |
| **`AsAIAgent()` extension** | Converts OpenAI `ChatClient` directly to `ChatClientAgent` — built-in tool invocation. |
| **.NET 10 + C# 14** | LTS release, field-backed properties, extension blocks, improved performance. |

## Key Concepts from the Paper Collection

### Pensieve Paradigm (StateLM)
> "What happens when we finally place the wand in the model's hand?"

Traditional RAG stuffs retrieved content into context. StateLM instead trains the model to **manage its own context** — reading chunks, distilling notes, and pruning raw text. Our `ResearchMemory` implements this: findings are persistent notes; raw content is ephemeral.

### Agentic RAG
> "Agentic RAG transcends [static RAG] by embedding autonomous AI agents into the RAG pipeline."

Our Researcher agent doesn't just retrieve-and-stuff. It dynamically decides what to search for, evaluates results, follows leads, and iteratively refines its understanding.

### RE-Searcher Goal-Reflect Pattern
> "Reason → Search → Reflect: an explicitly stated goal before each search, with structured reflection after."

The Researcher agent states its goal before every search, then reflects on whether results met that goal. Failed approaches are stored in a Reflective Memory Repository to prevent repetition.

### FINDER Verification
> "Verification is asymmetrically cheaper than generation."

The Verifier agent checks each claim against evidence — catching unsupported or contradicted claims that the Synthesizer may have hallucinated or overstated. Uses the DEFT failure taxonomy for structured error categorization.

### Agent-as-a-Judge
The Analyst agent serves as an internal quality evaluator — assessing source reliability, finding confidence, and identifying gaps before synthesis.

---

*Back to [Index](../../README.md)*
