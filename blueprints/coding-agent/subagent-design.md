# Forge Subagent Architecture

> Design document — March 2026 (revised March 22, 2026)  
> Companion to [design.md](design.md) §8 (Deliberate Omissions)  
> Grounded in 21 papers spanning multi-agent orchestration, context isolation, agent protocols, and hierarchical planning

## 0. Why This Document Exists

Forge's main design doc states: *"Prefer a single well-designed agent over premature multi-agent."* That principle is correct — and the current `RunSubagentTool` reflects it: a simple process-spawn with context isolation, not a sophisticated orchestration system.

But the principle has a second half: *"Only split when one agent can't hold all needed context."* After 6 phases of implementation and dozens of real sessions, we now have concrete evidence of **when the single-agent model breaks down**:

1. **Exploration-heavy tasks** consume 76% of the token budget on reads (SWE-Pruner). A 30-step session exploring a large codebase hits context limits before it starts implementing. The sawtooth compression helps but can't recover information once compressed.

2. **Multi-file analysis** requires holding N files in context simultaneously. At ~500 tokens/file for meaningful analysis, 20 files = 10K tokens of working state. The parent agent's context fills up with exploration artifacts that are irrelevant to the final edit.

3. **Untrusted data processing** — fetching web pages, reading user-provided files, processing tool output from external MCP servers — creates injection risk. AGENTSYS (2026) showed isolation alone drops attack success from 30.66% to 2.19%.

4. **Session cost scaling** — a 20-step session costs disproportionately more than two 10-step sessions because of monotonically growing prompt tokens. Context reset via subagent delegation can be a *cost optimization*, not just a capability.

5. **Information decay over long chains** — MCP Information Fidelity (2026) proved that cumulative distortion grows linearly with steps, bounded by O(√T). After ~9 sequential tool interactions, error influence from earlier steps falls below 5%. This means subagent chains of 3+ agents degrade reliably — but a single delegation with return is within the safe zone.

6. **Coordination overhead exceeds benefit for strong models** — GAIA2 (2026) showed that for Claude-4-Sonnet-class models, "increasing the collaborator ratio does not improve cost-normalized performance under best-of-k sampling." Multi-agent helps weak models more than strong ones. This validates Forge's conservative approach: delegate for context isolation, not reasoning amplification.

This document designs a principled subagent system that solves these specific problems without the coordination overhead that makes most multi-agent systems worse than single-agent.

## 1. Design Principles for Subagents

These extend the main design doc's principles, not replace them:

| # | Principle | Source |
|---|-----------|--------|
| S1 | **Subagents are context isolation boundaries, not reasoning enhancers.** The primary value is fresh context + security isolation. Don't expect two agents to reason better than one with the same information. | AgentArk (distillation matches multi-agent quality), AGENTSYS (isolation = security) |
| S2 | **The parent agent is the orchestrator, not a peer.** Subagents report to the parent. There is no peer-to-peer communication, no shared memory, no debate. The parent decomposes, delegates, and synthesizes. | CORAL anti-finding: dynamic A2A is powerful but impossible to debug. Hub-and-spoke is the right production topology for a coding agent. |
| S3 | **Schema-validated returns only.** Nothing crosses the isolation boundary except the subagent's final text output, capped and sanitized. The subagent's internal reasoning trace, tool calls, and intermediate context are discarded. | AGENTSYS: "External data and subtask reasoning traces never directly enter the main agent's memory." |
| S4 | **Subagents inherit the parent's tool set but not its conversation.** A subagent gets the same MCP server, the same REPO.md, but a completely fresh conversation history. It knows nothing about what the parent has already explored. | Process-level isolation, already implemented |
| S5 | **The parent must be able to do everything a subagent can, just less efficiently.** Subagents are an optimization, not a capability. If the `RunSubagent` tool disappeared, every task should still be completable (slower, costlier). | Single-agent sufficiency principle |
| S6 | **Delegation decisions are made by the parent based on task structure, not hardcoded workflows.** No predefined pipelines. The LLM decides when to delegate based on task complexity signals. | CORAL: emergent patterns > predefined workflows |
| S7 | **Subagent results are disposable summaries, not preserved state.** The parent never restores a subagent's full context. It receives a compressed summary and discards the rest. This is deliberately lossy — the information fidelity cost of the handoff is the price of isolation. | MCP Information Fidelity (2026): linear distortion growth, but bounded and predictable |
| S8 | **Prefer heterogeneous delegation over homogeneous.** When delegating, match the subagent's capability to the subtask. An Explore subagent should be cheaper/faster than the parent. A Verify subagent needs different tools, not different intelligence. | GAIA2 (2026): heterogeneous teams +9.9pp over homogeneous; CASTER (2026): 45-54% cost reduction via model routing |

## 2. Current State Assessment

### What exists today

| Component | Status | Quality |
|-----------|--------|---------|
| `RunSubagentTool` | Implemented | Good bones, but underspecified use cases |
| `SearchSubagentTool` | Implemented | Solid — multi-pass local search, not actually a "subagent" (no LLM) |
| Recursion depth tracking | Implemented | Max 3 via `FORGE_SUBAGENT_DEPTH` env var |
| Process isolation | Implemented | Separate `dotnet run` process, fresh stdout/stderr |
| Output extraction | Implemented | Parses "Task completed" marker, falls back to full stdout |
| Timeout | Implemented | 300s (5 min) hardcoded |

### What's missing (addressed by Path A implementation)

| Gap | Impact | Addressed in |
|-----|--------|-------------|
| **No compound exploration tool** | Agent calls grep_search + read_file 5-10 times for what should be 1 step. Burns context on raw file reads that become stale after compression. | Phase 7A (`explore_codebase`) |
| **SearchSubagent naming confusion** | `SearchSubagentTool` isn't a subagent — it's a local search utility. The name implies it spawns a sub-agent. | Phase 7B (rename → `search_codebase`) |
| **No specialized subagent modes** | Every subagent is a full Forge instance with 40 tools. An explore subagent doesn't need write tools. | Phase 7C (mode parameter) |
| **No context handoff** | Subagent starts cold — wastes steps re-discovering what the parent already found. | Phase 7C (context parameter) |
| **No delegation guidance** | System prompt doesn't teach *when* to delegate. No heuristics. | Phase 7D (system prompt + auto-activation) |
| **No delegation failure recovery** | Parent blindly absorbs empty/garbage subagent results. | Phase 7E (failure taxonomy) |

### Gaps deferred (not addressed in v1)

| Gap | Why deferred |
|-----|-------------|
| **No budget coordination** | Mode-specific limits (10-20 steps, 100-200K tokens) are sufficient. Parent deduction adds complexity without proven need. |
| **No parallel execution** | ToolExecutor is sequential. Cross-cutting change not justified until session data shows >2 independent `run_subagent` calls per session. |
| **No model routing** | Needs API infrastructure for per-call model selection. Revisit when cheaper models are available. |
| **No context branching** | The `context` parameter achieves 90% of CMV's benefit at 1% of the implementation cost. |
| **No async execution** | Temporal awareness isn't needed for coding tasks. |

## 3. Architecture

### 3.1 Delegation Topology

```
┌─────────────────────────────────────────────────────────────────────┐
│                    Parent Agent (Orchestrator)                       │
│                                                                     │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │  Delegation Router (in system prompt)                         │  │
│  │                                                               │  │
│  │  Task signals → delegation decision:                          │  │
│  │                                                               │  │
│  │  INLINE (do it yourself):                                     │  │
│  │    • < 5 files to examine                                     │  │
│  │    • Single concern (one bug, one feature)                    │  │
│  │    • Information already in context                            │  │
│  │    • < 25% budget remaining (can't afford delegation)         │  │
│  │                                                               │  │
│  │  DELEGATE (spawn subagent):                                   │  │
│  │    • Exploration of unfamiliar code area (> 5 files)          │  │
│  │    • Multi-file analysis ("find all usages of X")             │  │
│  │    • Processing untrusted input (web fetch → analyze)         │  │
│  │    • Independent subtask that doesn't need parent context     │  │
│  │    • Task requires clean context (deep analysis task)         │  │
│  └───────────────────────────────────────────────────────────────┘  │
│                                                                     │
│           ┌────────────┬────────────┬────────────┐                  │
│           ▼            ▼            ▼            ▼                  │
│     ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐           │
│     │ Explore  │ │ Analyze  │ │ Verify   │ │ Execute  │           │
│     │ Subagent │ │ Subagent │ │ Subagent │ │ Subagent │           │
│     │          │ │          │ │          │ │          │           │
│     │ Read-only│ │ Read-only│ │ Read +   │ │ Full     │           │
│     │ tools    │ │ tools    │ │ run_tests│ │ tools    │           │
│     │ 10 steps │ │ 15 steps │ │ 10 steps │ │ 20 steps │           │
│     └────┬─────┘ └────┬─────┘ └────┬─────┘ └────┬─────┘           │
│          │             │             │             │                 │
│          ▼             ▼             ▼             ▼                 │
│     ┌──────────────────────────────────────────────────────┐        │
│     │  Structured Result (capped text, schema-guided)      │        │
│     │  → ObservationPipeline (same as any tool output)     │        │
│     └──────────────────────────────────────────────────────┘        │
└─────────────────────────────────────────────────────────────────────┘
```

### 3.2 Subagent Modes

Instead of every subagent being a full Forge instance, define **modes** that constrain the subagent's capabilities and budget:

| Mode | Tools Available | Max Steps | Max Tokens | Use Case |
|------|----------------|-----------|------------|----------|
| **Explore** | `read_file`, `list_directory`, `grep_search`, `file_search`, `search_codebase`, `explore_codebase`, `get_project_setup_info` | 10 | 100K | Navigate unfamiliar code, build understanding, find relevant files, dependency tracing |
| **Verify** | Explore tools + `run_tests`, `run_bash_command` (build only), `get_errors`, `test_failure` | 10 | 100K | Run tests, check compilation, validate changes |
| **Execute** | Full tool set | 20 | 200K | Independent implementation subtask requiring file writes |

**Why modes matter:**
- **Token savings**: An Explore subagent sends 5 tool schemas instead of 40. At ~200 tokens per tool description, that's 7K tokens saved per LLM call × 10 calls = 70K tokens.
- **Safety**: An Explore subagent literally cannot write files. No guardrail needed — the capability doesn't exist.
- **Focus**: Fewer tools = less decision space = faster, more accurate tool selection (HyFunc 2026: tool descriptions are the #1 token waste; Agent Skills Architecture 2026: selection degrades beyond critical library size).

### 3.3 Context Handoff Protocol

The parent passes structured context to the subagent — not its raw conversation history, but a curated briefing:

```yaml
# Subagent briefing (injected as user message)
task: "Find all callers of AuthMiddleware.ValidateToken() and determine if any skip the X-Internal header check"

context:
  repo_type: ".NET 10 web API"
  key_files:
    - "src/middleware/auth.ts: AuthMiddleware class, ValidateToken method at line 47"
    - "src/routes/api.ts: Route registration, applies auth middleware"
  known_facts:
    - "X-Internal header bypasses auth check (vulnerability)"
    - "Tests in tests/auth.test.ts (14 passing)"
  parent_has_tried:
    - "grep for 'ValidateToken' found 12 matches but couldn't trace call chains"

constraints:
  mode: explore
  return_format: |
    Return a structured summary:
    1. List of callers (file:line → function name)
    2. Which callers skip the X-Internal check
    3. Suggested fix locations
```

**What this solves:**
- Subagent doesn't waste steps re-discovering what the parent already knows
- Parent communicates *what it has already tried* (preventing duplicate work)
- Return format is specified upfront (structured output > free-form)
- Mode constrains the subagent's capabilities

### 3.4 Budget Coordination

**Problem:** Without coordination, a parent + 3 subagents could burn 2M tokens (500K × 4).

**Solution:** The parent deducts subagent budget from its own remaining budget:

```
Parent budget:  500K tokens, 30 steps
                     │
                     ▼
Step 5: Delegate Explore subagent (budget: 100K tokens, 10 steps)
  Parent remaining: 400K tokens, 29 steps (the delegation itself costs 1 step)
                     │
Step 8: Delegate Analyze subagent (budget: 150K tokens, 15 steps)
  Parent remaining: 250K tokens, 28 steps
                     │
Step 12: Delegate another Explore? 
  Check: 250K remaining, need 100K → OK, delegate
  Parent remaining: 150K tokens, 27 steps
                     │
Step 18: Want to delegate Execute subagent (200K)?
  Check: 150K remaining, need 200K → DENIED (insufficient budget)
  Agent must do it inline or request smaller budget
```

**Implementation:** Pass `--MaxTotalTokens` and `--MaxSteps` as CLI args to the subagent process, deducted from parent's remaining budget.

### 3.5 Context Branching (CMV-Inspired)

**Insight:** Contextual Memory Virtualisation (2026) introduced a Git-like DAG model for LLM context: "a user who spends 40 minutes generating 80k tokens of architectural understanding can snapshot that state as a stable root commit. From this root, they can spawn independent, parallel branches."

Forge can apply a lightweight version of this pattern:

```
Parent context at step 8 (after exploring auth system):
  [system prompt + REPO.md + 8 steps of exploration = ~60K tokens]
       │
       ├─ Snapshot: extract key facts into structured briefing (~2K tokens)
       │
       ├─▶ Subagent A (Explore): briefing + "Find all callers of ValidateToken"
       │     Gets: 2K context (not 60K), fresh context budget
       │
       └─▶ Subagent B (Analyze): briefing + "Assess refactoring impact"
             Gets: same 2K briefing, independent context budget
```

**What this is NOT:** We are not literally forking the parent's full conversation (that would defeat the purpose of isolation and cost ~$0.53 per fork at current rates). We're extracting a **structured snapshot** — the parent's working memory distilled into a compact briefing. This is the CMV concept adapted for our cost constraints.

**The snapshot extraction is already natural:** The parent agent's Working Memory (key_facts, plan, hypotheses) is the right content for the briefing. When the parent delegates, it passes its current understanding, not its raw conversation.

**CMV's quantitative finding:** Structural trimming (removing tool result bloat while preserving conversational content) achieves 20-39% reduction on mixed sessions. Applied to subagent briefings: the parent's 60K context compresses to ~2K of distilled facts — a 97% reduction that the subagent can work from.

### 3.6 Parallel Execution

**When two delegations are independent**, execute them concurrently:

```csharp
// Parent's tool call: two parallel subagents
// The agent emits two RunSubagent tool calls in the same step

// ToolExecutor runs them concurrently:
var tasks = toolCalls
    .Where(tc => tc.FunctionName == "run_subagent")
    .Select(tc => ExecuteSubagentAsync(tc))
    .ToArray();

var results = await Task.WhenAll(tasks);
```

**Constraints:**
- Max 3 concurrent subagents (prevent resource exhaustion)
- Combined budget of all concurrent subagents must fit within parent's remaining budget
- Each concurrent subagent gets its own process (already the case)
- Timeout is per-subagent, not aggregate

**When NOT to parallelize:**
- Subagent B depends on Subagent A's output
- Parent has < 25% budget remaining (can't afford concurrent burns)
- Tasks overlap in file scope (risk of conflicting edits for Execute mode)

**GAIA2 evidence for heterogeneous parallel teams:**
- Llama-main + Claude-app: 16.2% pass@1 (weak orchestrator + strong executor)
- Claude-main + Llama-app: 18.3% (strong orchestrator + weak executor)
- All-strong (Claude + Claude): 29.3%
- All-weak (Llama + Llama): 8.5%

**Key takeaway:** "Stronger executors improve outcomes even when the main agent is light" — and vice versa. The parent + subagent pairing doesn't need to be homogeneous. A strong parent can delegate to cheaper subagent models without proportional quality loss.

### 3.7 Information Fidelity Across the Isolation Boundary

**Problem:** Every handoff between parent and subagent loses information. How much, and is it predictable?

**MCP Information Fidelity (2026)** proved mathematically:

1. **Cumulative distortion grows linearly:** $D(T) = O(T)$ with high-probability deviation bound $O(\sqrt{T})$
2. **Influence decays exponentially:** Error from step $i$ on step $j$ decays as $\beta^{j-i}$ where $\beta < 1$
3. **Re-grounding interval:** ~9 steps before earlier context drops below 5% influence
4. **Semantic weighting reduces distortion 80%:** Prioritizing semantic coherence over factual precision in summaries dramatically reduces effective loss

**Implication for Forge's delegation model:**

```
Parent → Subagent → Parent  (1 handoff boundary: safe)
  Distortion: ~0.5 per boundary, well within O(√T) bounds

Parent → Subagent A → Subagent B → Parent  (2 boundaries: marginal)
  Distortion: ~1.0, approaching practical limits

Parent → A → B → C → Parent  (3+ boundaries: avoid)
  Distortion: >1.5, significant information loss
```

**Design rule:** Forge's max recursion depth of 3 is correct but for the wrong reason (it was set for fork-bomb prevention). The information fidelity bound independently validates it — beyond depth 2, accumulated distortion degrades result quality. Depth 1 (parent → subagent → parent) is the sweet spot. Depth 2 should be reserved for cases where the intermediate agent genuinely adds value (e.g., parent → explore subagent → the explore subagent delegates a focused search).

## 4. The Five Subagent Use Patterns

Derived from session analysis and research, these are the concrete patterns where delegation beats inline execution:

### 4.1 Scout Pattern (Explore mode)

**Trigger:** Agent needs to understand an unfamiliar code area before implementing.

**Flow:**
```
Parent: "I need to fix the auth bypass. Let me understand the auth system first."
  │
  └─▶ Explore Subagent: 
       "Map the authentication flow in this .NET web API.
        Find: middleware chain, all auth-related files, how tokens
        are validated, what headers influence auth decisions.
        Return: file list with line ranges, dependency graph,
        entry points."
       │
       └─▶ Returns: structured map of 8 files, 3 entry points,
            2 header-based bypass conditions
  │
Parent: [now has focused map, starts editing with precise knowledge]
```

**Why not inline:** The exploration would consume 5-8 steps and fill context with file contents that become stale after compression. The subagent explores, summarizes, and the parent gets a clean 500-token map instead of 15K tokens of raw file reads.

**Research backing:** SWE-Adept's skeleton-first navigation (+5.4% localization) and Theory of Code Space (+26 correct edges after structured probing). The subagent is the probe.

### 4.2 Analyst Pattern (Explore mode)

**Trigger:** Agent needs to assess impact of a change across many files.

**Flow:**
```
Parent: "Before I refactor DatabaseConnection, I need to know the blast radius."
  │
  └─▶ Explore Subagent:
       "Find all direct and transitive dependents of DatabaseConnection class
        in src/data/. For each dependent, classify whether it:
        (a) instantiates DatabaseConnection directly
        (b) receives it via constructor injection  
        (c) accesses it through a service locator
        Return: categorized list with file:line, dependency type,
        and estimated refactoring difficulty (trivial/moderate/hard)."
       │
       └─▶ Returns: 14 dependents, 3 direct instantiation (hard),
            8 DI (trivial), 3 service locator (moderate)
  │
Parent: [plans refactoring strategy based on impact analysis]
```

**Why not inline:** Tracing transitive dependencies requires reading 20+ files. The parent's context can't hold all of them simultaneously without aggressive compression that loses detail.

### 4.3 Sentinel Pattern (Verify mode)

**Trigger:** Agent wants to verify changes without polluting its context with test output.

**Flow:**
```
Parent: [just finished editing 3 files]
  │
  └─▶ Verify Subagent:
       "Run the full test suite for the project at /src.
        If tests fail, identify: (1) which tests (2) the assertion 
        that failed (3) whether it's a regression from the recent
        changes to auth.ts, routes.ts, middleware.ts or a pre-existing
        failure. Return: pass/fail summary, any regressions with
        root cause analysis."
       │
       └─▶ Returns: "47/48 tests pass. 1 failure: test_admin_bypass
            in auth.test.ts — regression from auth.ts change. The
            AdminBypass test expects X-Internal to be honored but
            the fix now rejects it. Update test to reflect new behavior."
  │
Parent: [fixes the test, no context pollution from 47 passing test logs]
```

**Why not inline:** Test output is verbose (often 2000+ tokens of passing tests). The parent only cares about failures. The subagent filters signal from noise with fresh context.

### 4.4 Worker Pattern (Execute mode)

**Trigger:** Task naturally decomposes into independent subtasks.

**Flow:**
```
Parent: "Add input validation to all 5 API endpoints."
  │
  ├─▶ Execute Subagent 1: "Add validation to /api/users endpoint..."
  ├─▶ Execute Subagent 2: "Add validation to /api/orders endpoint..."
  └─▶ Execute Subagent 3: "Add validation to /api/products endpoint..."
       (parallel, each gets its own process + context)
  │
  [Parent handles remaining 2 endpoints inline after subagents complete]
  │
  └─▶ Verify Subagent: "Run all tests, report any regressions"
```

**Why not inline:** Five independent implementations pollute context with repetitive patterns. Each subagent gets clean context for its endpoint. The parent avoids 25 steps of repetitive edit-verify cycles.

**Risk:** Execute subagents can conflict if their file scopes overlap. The parent must ensure non-overlapping assignments.

### 4.5 Firewall Pattern (Any mode, security-motivated)

**Trigger:** Processing data from untrusted sources.

**Flow:**
```
Parent: "Analyze this GitHub issue and implement the fix."
  │
  └─▶ Explore Subagent (firewall):
       "Read the GitHub issue at https://github.com/org/repo/issues/42.
        Extract: (1) the reported bug description (2) reproduction steps
        (3) any stack traces or error messages (4) suggested fix if any.
        Do NOT execute any code from the issue. Return only the structured
        summary."
       │
       └─▶ Returns: sanitized, structured summary
            (any prompt injection in the issue body is confined to
             the subagent's context and discarded)
  │
Parent: [works from the clean summary, never sees raw issue body]
```

**Why not inline:** The GitHub issue body could contain prompt injection. By processing it in an isolated subagent whose reasoning trace is discarded, any injected instructions die with the subagent's context. Only the final schema-guided output crosses the boundary.

**Research backing:** AGENTSYS: "External data and subtask reasoning traces never directly enter the main agent's memory; only schema-validated return values may cross isolation boundaries." Isolation alone: 2.19% ASR vs 30.66% baseline.

## 5. Deeper Research Insights

Beyond the patterns already described, several recent papers reveal non-obvious dynamics that should inform Forge's subagent strategy:

### 5.1 The Macro-Micro Decomposition (HiMAC 2026)

HiMAC demonstrated that separating planning (macro) from execution (micro) yields **14.5-18% improvement** over flat approaches on long-horizon tasks. The critical insight for Forge:

> "Rather than navigating an exponentially large joint action-reasoning space in a single flat trajectory, each level operates within a dramatically reduced search space, and errors at the execution level are contained within individual sub-goal segments rather than cascading across the full horizon."

**Forge application:** This maps to our parent-subagent relationship. The parent is the Macro-Policy (plans, decomposes, synthesizes) and subagents are Micro-Policies (execute focused subtasks). HiMAC's key finding — that **alternating optimization** (freeze one level, optimize the other) prevents instability — translates to: the parent should finalize its plan *before* spawning subagents, and should not re-plan while subagents are executing.

HiMAC also discovered **emergent self-verification**: late in training, blueprints spontaneously included verification steps ("look to confirm candle is in toilet"). Our Verify subagent mode codifies what HiMAC's agent learned to do spontaneously.

**Failure containment lesson:** When HiMAC removed hierarchy (flat GRPO), WebShop success dropped from 84.1% to 66.1% (-18%). "Non-stationarity is particularly harmful in environments with longer horizons and noisier reward signals." This is exactly the failure mode in Forge sessions >20 steps — the accumulation of context noise drowns signal. Delegation resets this.

### 5.2 Meta-Generated Architectures (ADAS 2024)

ADAS used a meta-agent to *discover* optimal agent topologies by searching a code space. Two findings are directly relevant:

1. **Emergent patterns converge to ensemble + refinement:** Across math, reading comprehension, and code generation, the discovered architectures consistently used "multiple COTs to generate possible answers, refine them, and finally ensemble the best answers." This is *not* the debate pattern (which fails) — it's independent parallel generation followed by selection. Forge's parallel Execute subagents with a parent synthesizer matches this discovered optimum.

2. **Cross-domain transfer of discovered architectures:** Agents discovered on MGSM (math) transferred to GSM8K (+25.9%), GSM-Hard (+13.2%), and even non-math tasks like DROP (+6.1%). **Implication:** The subagent patterns that work for code exploration likely transfer to other domains (documentation analysis, test generation, dependency tracing) without task-specific tuning.

### 5.3 Self-Rectifying Runtime (MAS² 2025)

MAS²'s Generator-Implementer-Rectifier pattern adds a dimension missing from Forge's current design: **runtime reconfiguration.**

- **Generator**: Creates the multi-agent workflow template
- **Implementer**: Assigns concrete models to roles
- **Rectifier**: Monitors execution and modifies the system when cumulative resource consumption exceeds a threshold or failures occur

**Key result:** Removing the Rectifier caused the largest quality drop on MATH (-6.6%), confirming that runtime adaptation matters most for complex reasoning tasks.

**Forge application:** The parent agent already plays the Generator + Implementer roles (decides to delegate, configures the mode). The missing piece is the Rectifier function: if a subagent times out or returns garbage, the parent should not just absorb the failure — it should reclassify the task and try a different strategy (e.g., switch from Execute mode to breaking the task into smaller Explore + inline implementation). This is the HALT/RETHINK/ALTERNATIVE taxonomy applied to delegation failures.

MAS² also demonstrated **20× cost reduction** vs. Self-Consistency baselines through intelligent model assignment: "Economical models (Qwen, GPT-4o-mini) generate/test in parallel → GPT-4o performs final synthesis." This reinforces Principle S8 (heterogeneous delegation).

### 5.4 Uncertainty Across Agent Boundaries (Managing Uncertainty 2026)

This paper identifies a failure mode we haven't addressed: **semantic misalignment between agents.**

> "A general triage agent might interpret a heart rate of 140 bpm as 'critical' while a pediatric agent interprets the same value as 'normal' for a neonate."

In Forge's context: the parent might describe a "module" as a .csproj project, while the subagent interprets it as a namespace or a directory. These semantic gaps compound silently.

**Mitigation:** The context handoff should include explicit **terminology anchoring**:

```yaml
context:
  terminology:
    - "module" = ".csproj project in the solution"
    - "test" = "xUnit test in Forge.Tests/"
    - "tool" = "MCP tool registered in ToolRegistry"
```

The paper's uncertainty lifecycle also suggests an **escalation trigger**: when a subagent encounters persistent ambiguity (e.g., multiple plausible interpretations of the task), it should return early with a clarification request rather than guess. This is Ask-Before-Plan applied to the subagent level.

### 5.5 Sandbox Isolation via CodeAct (OpenHands 2024)

OpenHands' `AgentDelegateAction` provides a clean delegation model:

> "OpenHands allows interactions between multiple agents... enabling an agent to delegate a specific subtask to another agent."

**Architecture parallels:**
- CodeActAgent (generalist) delegates web browsing to BrowsingAgent (specialist)
- Each agent runs in a Docker sandbox with mounted workspace
- Event stream enables state replication without shared memory

**Key difference from Forge:** OpenHands' agents share a persistent IPython runtime (CaveAgent-style), allowing structured data to cross boundaries as native Python objects rather than serialized text. This eliminates the information fidelity loss of text-based handoffs. Forge's process-level isolation is stronger for security but weaker for data fidelity.

**Practical consideration:** For Forge's Verify subagent, sharing the parent's built artifacts (compiled binaries in `bin/`) is essential. This already works because the subagent process shares the same filesystem. But OpenHands' approach suggests a more deliberate "shared workspace, isolated context" model — the subagent sees the same files but a different conversation.

### 5.6 The Auton Cost Constraint (2026)

Auton formalized agent cost management via KKT conditions, producing a concrete insight:

> Shadow price λ dynamically adjusts reasoning verbosity as token consumption approaches budget B.

Translated to Forge: as the parent's budget depletes, the **cost of delegation should increase** (because the fixed overhead is a larger fraction of remaining budget). This maps to our existing "< 25% budget → don't delegate" heuristic, but Auton suggests a **continuous** adjustment rather than a binary threshold:

```
Delegation cost multiplier = 1 / (1 - budget_utilization)

At 50% budget used: multiplier = 2×  (delegation twice as "expensive")
At 75% budget used: multiplier = 4×  (strongly prefer inline)
At 90% budget used: multiplier = 10× (effectively blocked)
```

This creates a smooth ramp-down rather than a cliff, matching Auton's KKT-optimal behavior.

## 6. Comparison with Production Systems

### 6.1 GitHub Copilot Agent Mode

Copilot uses subagents in a specific pattern worth studying:

- **Explore subagent**: A fast, read-only agent specialized for codebase navigation. It can search files, grep, and read — but cannot write or execute. Called for broad context gathering before implementation begins.
- **Key insight**: The Explore subagent is *cheaper* (smaller model or lower reasoning effort) and *faster* (read-only tools, shorter context). It's an **asymmetric delegation** — the subagent is intentionally less capable but more focused.
- **Result aggregation**: Returns file paths and code snippets, which the parent uses to form its plan. The parent never sees the subagent's reasoning process.
- **When invoked**: Copilot's system prompt guides the model to use the Explore subagent for search-heavy tasks rather than calling `grep_search` and `read_file` directly 10+ times.

**Forge parallel**: Our Explore mode maps directly to this pattern. The key difference is that Copilot's Explore subagent uses a *different, cheaper model* — a cost optimization Forge should adopt.

### 6.2 Claude Code

Claude Code takes a different approach:

- **Single-agent architecture** with aggressive context management
- **No formal subagent system** — instead, Claude Code manages a large context window (200K tokens) with sophisticated attention mechanisms
- **Effective strategy**: Rather than delegating to subagents, Claude Code reads more aggressively upfront and relies on the model's long-context capabilities
- **Weakness**: Very long sessions accumulate context debt. A 50,000-token conversation performs worse than two 25,000-token conversations with a clean handoff.

**Forge lesson**: Claude Code's single-agent approach works because of its massive context window. Forge, targeting models with 128K-200K windows, benefits more from delegation. But Claude Code's emphasis on *reading aggressively upfront* before editing is a pattern Forge's Scout subagent naturally enables.

### 6.3 Devin / Factory-Style Agents

Production coding agents (Devin, Factory, Codegen) converge on a similar pattern:

- **Planning agent** (lightweight, strategic) decomposes the task
- **Implementation agents** (focused, parallel) execute subtasks
- **Verification agent** (independent, conservative) validates the result
- **Key pattern**: The planning agent and verification agent are often *different* from the implementation agent — different system prompts, different temperature, sometimes different models.

**Forge parallel**: Our Worker + Sentinel patterns match this architecture. The design principle of mode-specific tool restrictions achieves the same effect as running different system prompts.

## 7. Anti-Patterns to Avoid

Drawn from research failures and real-world experience:

### 7.1 The Debate Anti-Pattern

**Don't do:** Agent A generates code, Agent B critiques, Agent A revises.

**Why it fails in practice:**
- LLMs tend to agree with their own outputs when asked to self-critique (SPOC 2025: prompted self-reflection unreliable)
- Two LLM calls for the quality of one, with coordination overhead
- Verification via external tools (linter, tests, type checker) is faster, cheaper, and more reliable than LLM-as-critic (DeepVerifier: decomposed external checks > holistic LLM judgment)

**What to do instead:** Parent generates code → Verify subagent runs tests → Parent fixes based on concrete test failures. Tool-grounded verification, not LLM opinions.

### 7.2 The Pipeline Anti-Pattern

**Don't do:** Fixed pipeline: Search Agent → Analyze Agent → Plan Agent → Code Agent → Test Agent.

**Why it fails:**
- Rigid — can't skip stages or loop back dynamically
- Information loss at each handoff (telephone game) — MCP Information Fidelity (2026) proved distortion accumulates linearly per boundary, and influence decays exponentially. A 5-stage pipeline accumulates ~2.5 distortion units vs. ~0.5 for a single delegation.
- 5 agents × overhead per agent = 5× coordination cost for tasks that need 2 agents
- CORAL (2026): "Workflow-based MAS suffer from massive manual effort to predefine states and inability to handle unforeseen edge cases"
- GAIA2 (2026): For strong models, "increasing the collaborator ratio does not improve cost-normalized performance" — more agents ≠ better results

**What to do instead:** Parent decides dynamically which (if any) subagents to spawn, based on the specific task. Most tasks need 0-1 subagents, not 5.

### 7.3 The Over-Delegation Anti-Pattern

**Don't do:** Delegate every subtask ("I'll have a subagent read this file, another subagent analyze it, another subagent write the fix").

**Why it fails:**
- Each delegation costs 1 parent step + subagent startup overhead (~3K tokens for system prompt + REPO.md)
- Subagent can't build on parent's context — repeats exploration
- Three 10-step subagents burn more tokens than one 15-step inline exploration
- Use the ≤15-step / ≤5-file heuristic: if the task is small enough, do it inline

**Decision rule:** Delegate when the *context isolation benefit* exceeds the *startup + coordination cost*. For a 3-file change, inline. For a 15-file analysis, delegate.

### 7.4 The Shared Memory Anti-Pattern

**Don't do:** Give subagents access to a shared scratchpad or memory store.

**Why it fails:**
- Breaks context isolation (the primary security benefit)
- Creates race conditions with parallel subagents
- Debugging becomes impossible (who wrote what, when?)
- The only safe communication is the structured return value

**What to do instead:** Parent assembles context for each subagent explicitly. If Subagent B needs Subagent A's results, the parent chains them: run A → extract result → include in B's prompt.

### 7.5 The Homogeneous Agent Anti-Pattern

**Don't do:** Run every subagent on the same model with the same configuration as the parent.

**Why it wastes resources:**
- GAIA2 (2026): Heterogeneous teams (different models for orchestrator vs. executor) consistently outperform homogeneous teams. Claude-app + Llama-main = 18.3% vs Llama-only = 8.5% (+9.8pp).
- CASTER (2026): Intelligent routing achieves 45-54% cost reduction with maintained quality. "Outperforms FrugalGPT cascading by 20.7-48.0% cost."
- MAS² (2025): "Economical models generate/test in parallel → premium model performs final synthesis" achieves 20× cost reduction vs. Self-Consistency.
- An Explore subagent doing `grep_search` and `read_file` doesn't need the same reasoning depth as a parent deciding which files to edit.

**What to do instead:** Match model capability to subtask complexity. Explore subagents can use a lighter model or lower reasoning effort. Only Execute subagents (writing code) need full model capability. This is the CASTER pattern applied at the delegation level.

**Future work:** Add a `model` parameter to `RunSubagentTool` that allows the parent to specify a cheaper model for simple subtasks. Default to parent's model for backward compatibility.

### 7.6 The Semantic Drift Anti-Pattern

**Don't do:** Give the subagent a vague task with implicit assumptions.

**Why it cascades:**
- Managing Uncertainty (2026): Semantic misalignment between agents is a primary driver of coordination failure. "Specialized agent roles may lack shared semantic grounding, leading to misinterpretations of perception data and misaligned reasoning traces."
- HiMAC (2026): "A minor syntactic deviation in an early step often cascades into irreversible failure states" in long-horizon tasks. The delegation boundary amplifies this — the subagent has no opportunity to ask clarifying questions.

**What to do instead:** Include terminology anchoring in the briefing. Define project-specific terms explicitly. Specify the expected return format. Use the `context` parameter to ground the subagent's understanding.

## 8. Implementation Plan — Path A: Lean Delegation

> Based on the tensions analysis: most "subagent" value comes from cheap, focused read-only exploration, not from full multi-step child processes. This plan builds the high-value compound tool first, upgrades the existing `RunSubagent` tool second, and consolidates redundant tools along the way.

### Implementation Status (March 2026)

All phases (8.0–8.6) shipped and validated through 7 experiments (D, B-lite, C, E, E', A, F). Key outcomes:

| Phase | Planned | Actual Outcome |
|-------|---------|---------------|
| **8.0** Tool consolidation | Rename SearchSubagent → SearchCodebase | ✅ Done. Also extracted `CodeSearchUtils.cs` shared module. |
| **8.1** explore_codebase (core) | Compound exploration tool as Tier 1 core | ✅ Built, tested, then **demoted to Tier 2** after experiments showed the LLM never chooses it over grep_search + read_file. |
| **8.2** Rename SearchSubagent | Mechanical rename | ✅ Done. |
| **8.3** Upgrade RunSubagent | Add mode, context, budget | ✅ Done. **Critical fix:** CLI arg passing was silently broken — switched to env vars (`FORGE_MaxSteps`, `FORGE_ToolMode`, etc.). |
| **8.4** System prompt + auto-activation | TaskLooksComplex heuristic | ✅ Done. 24/24 accuracy on labeled test set. |
| **8.5** Delegation failure recovery | DelegationFailure taxonomy type | ✅ Done. |
| **8.6** Evaluation | Run experiments | ✅ 7 experiments ran. See [subagent-experiment-proposal.md](subagent-experiment-proposal.md) for full results. |

**Key architectural divergence from plan:** The plan assumed `explore_codebase` would be the high-value core tool that replaces 80% of subagent use cases. Experiments showed the LLM naturally prefers `grep_search` + `file_search` + `read_file` (the tools it's been trained on). `explore_codebase` produces good output (B-lite: 3/3 correct) but the LLM doesn't perceive step-saving as worth discovering a new tool for. Demoted to Tier 2 — available via `find_tools("explore")` with zero per-session overhead.

**Key bug found:** RunSubagentTool's `--MaxSteps`, `--MaxTotalTokens`, and `--ToolMode` CLI arguments were silently dropped by .NET's `AddCommandLine()` parser. This meant every subagent ran with default 30 steps / 500K tokens and no tool restrictions. Fixed by switching to `FORGE_` environment variables.

### 8.0 Prerequisites: Tool Consolidation

Before adding any new capability, clean up the existing tool surface. The MCP server has 40 tools with several overlapping groups:

**Search tools (5 tools doing overlapping things):**

| Tool | What it does | LLM vs Local | Overlap |
|------|-------------|--------------|---------|
| `grep_search` | Regex text search across files | Local | Core — fast, precise |
| `file_search` | Glob-based filename matching | Local | Core — complementary to grep |
| `semantic_search` | Keyword expansion + synonym matching | Local | Tier 2 — rarely better than grep |  
| `search_subagent` | Multi-pass file + content + structure search | Local | Tier 2 — superset of grep + file_search |
| `get_search_results` | VS Code search view results | Local | Tier 2 — VS Code-specific |

**Action:** Rename `search_subagent` → `search_codebase`. It is not a subagent (no LLM call). The name causes confusion about what "subagent" means in Forge's architecture. This is a mechanical rename — no behavior change.

**Web tools (2 tools doing the same thing):**

| Tool | What it does | Overlap |
|------|-------------|---------|
| `fetch_webpage` | Fetches URL, returns readable text | Primary |
| `open_browser_page` | Fetches URL with optional query-focused extraction | Superset of fetch_webpage |

**Action:** Keep both — `open_browser_page` is the enriched version (query-focused extraction), `fetch_webpage` is the lightweight version. No consolidation needed, but they should be described more distinctly so the agent doesn't confuse them.

**Terminal tools (4 tools with overlapping scope):**

| Tool | What it does | Overlap |
|------|-------------|---------|
| `run_bash_command` | Full background/foreground bash execution | Core — primary |
| `terminal_last_command` | Simpler alternative to run_bash | Convenience wrapper |
| `create_and_run_task` | Named labeled commands with guardrails | Structured alternative |
| `await_terminal` / `get_terminal_output` / `kill_terminal` | Manage background processes | Async management |

**Action:** No consolidation — these serve distinct UX patterns. But `terminal_last_command` and `create_and_run_task` could be merged in a future cleanup.

---

### 8.1 Phase 7A: The Compound Exploration Tool (Tier 0 Subagent)

**What:** Build `explore_codebase` — an in-process tool that makes 1-3 LLM calls with read-only tools to explore and summarize a code area. This is the "subagent" for 80% of delegation use cases, without any child process overhead.

**Why Tier 0, not a real subagent:**
- A 10-step Explore subagent via process spawn = ~3-5s startup + ~30s of LLM calls + 5.2K token overhead
- An in-process compound tool = ~0s startup + ~10s of LLM calls + ~500 token overhead
- Same result quality, 10× less overhead

**Architecture:**

```
┌──────────────────────────────────────────────────────────────┐
│  Parent Agent (main AgentLoop)                                │
│                                                               │
│  Step N: agent calls explore_codebase(query, files?, depth?)  │
│     │                                                         │
│     ▼                                                         │
│  ┌───────────────────────────────────────────────────────┐    │
│  │  ExploreCodebaseTool (in-process)                      │    │
│  │                                                        │    │
│  │  1. search_codebase(query) → file list + structure     │    │
│  │  2. Read top-ranked files (read_file, surgical ranges) │    │
│  │  3. ONE LLM call with mini system prompt:              │    │
│  │     "Given these files and the query, produce a         │    │
│  │      structured summary: relevant files, key entities, │    │
│  │      dependency relationships, and suggested locations" │    │
│  │  4. Return structured summary to parent                │    │
│  │                                                        │    │
│  │  Tools: search_codebase, read_file, grep_search,       │    │
│  │         list_directory (called directly, not via LLM)   │    │
│  │  LLM calls: 1 (synthesis only)                         │    │
│  │  Total cost: ~5-10K tokens                             │    │
│  └───────────────────────────────────────────────────────┘    │
│     │                                                         │
│     ▼                                                         │
│  Step N result: structured exploration summary (~500-1500     │
│  tokens) enters parent context via ObservationPipeline        │
└──────────────────────────────────────────────────────────────┘
```

**Implementation in `ExploreCodebaseTool.cs`:**

```csharp
[McpServerToolType]
public static class ExploreCodebaseTool
{
    [McpServerTool, Description(
        "Explore an unfamiliar code area and return a structured summary. " +
        "Combines file search, content analysis, and LLM synthesis in one step. " +
        "Use instead of manually searching and reading 5+ files. " +
        "Returns: relevant files with line ranges, key classes/functions, " +
        "dependency relationships, and suggested entry points.")]
    public static async Task<string> ExploreCodebase(
        [Description("What you want to understand (e.g. 'authentication flow', " +
            "'database connection setup', 'how tests are organized')")]
        string query,
        
        [Description("Short 3-5 word label for tracking")]
        string description,
        
        [Description("Optional: specific files or directories to focus on")]
        string? focusPath = null,
        
        [Description("Depth of exploration: 'quick' (3 files, ~3K tokens), " +
            "'medium' (8 files, ~8K tokens), 'thorough' (15 files, ~15K tokens). " +
            "Default: medium")]
        string? depth = null)
    {
        // 1. Use SearchCodebaseTool locally (no LLM, just file/content matching)
        var searchResult = SearchCodebaseTool.SearchCodebase(
            query, description, details: null, rootPath: focusPath);
        
        // 2. Parse top-ranked files from search results
        var filesToRead = ParseTopFiles(searchResult, depth ?? "medium");
        
        // 3. Read surgical ranges from each file (signatures + context)
        var fileContents = await ReadFileSummaries(filesToRead);
        
        // 4. One LLM call to synthesize (using parent's OpenAI client)
        var summary = await SynthesizeWithLlm(query, fileContents);
        
        return summary;
    }
}
```

**The LLM synthesis call** is the key design decision. Two options:

| Option | Mechanism | Pros | Cons |
|--------|-----------|------|------|
| **A: Direct API call** | Tool creates its own OpenAI client, makes one chat call with a mini system prompt | Fully isolated, no conversation state leakage | Needs API key access, creates second client, no prompt caching |
| **B: Deterministic synthesis** | No LLM call — use heuristics to rank files and format a structured summary | Zero LLM cost, deterministic, fast | Can't reason about relationships, just returns ranked file list |

**Recommendation: Start with Option B (deterministic), graduate to Option A if quality is insufficient.** The search_codebase tool already returns ranked files with structure extraction and bridge file discovery. Wrapping that in a more digestible format (sorted by relevance, with line ranges, nearby structure) may be good enough without an LLM call. This makes `explore_codebase` a pure upgrade over `search_codebase` — better formatting, automatic file reading, surgical extraction — without the cost of an LLM call.

If Option B proves insufficient (exploration summaries lack the relational reasoning needed), add a lightweight LLM synthesis step. Use the parent's model at reduced reasoning effort (`low` or `medium`) to keep costs down.

**Tier placement:** `explore_codebase` should be **core** (Tier 1). It replaces the pattern of calling `grep_search` + `read_file` 5-10 times sequentially. Making it core:
- Saves the agent 1 step (no `find_tools` needed)
- Replaces 5-10 tool calls with 1
- Token savings: ~200 tokens for the tool description vs ~4K-10K tokens for 5-10 separate tool results entering context

**Changes required:**
1. New file: `McpServer/Tools/ExploreCodebaseTool.cs`
2. Update `ToolRegistry.cs`: add `explore_codebase` to `CoreTools` set
3. Update `SystemPrompt.cs`: mention `explore_codebase` in the Plan phase workflow

---

### 8.2 Phase 7B: Rename SearchSubagentTool

**What:** Rename `SearchSubagentTool` → `SearchCodebaseTool` (tool name: `search_codebase`).

**Why:** It's not a subagent. The name causes the agent to confuse it with `run_subagent`. With `explore_codebase` as the new core exploration tool, `search_codebase` becomes the lower-level building block — still available via `find_tools` but typically used internally by `explore_codebase`.

**Changes required:**
1. Rename `McpServer/Tools/SearchSubagentTool.cs` → `McpServer/Tools/SearchCodebaseTool.cs`
2. Rename class `SearchSubagentTool` → `SearchCodebaseTool`
3. Rename method `SearchSubagent` → `SearchCodebase`
4. Update `Forge.Tests/SearchSubagentToolTests.cs` → `SearchCodebaseToolTests.cs`
5. Update all `SearchSubagentTool.SearchSubagent(...)` calls in tests → `SearchCodebaseTool.SearchCodebase(...)`

**Mechanical refactor — no behavior change, no risk.**

---

### 8.3 Phase 7C: Upgrade RunSubagentTool

**What:** Add `mode` and `context` parameters to the existing `RunSubagentTool`. This handles the 20% of cases where a full process-spawn subagent is genuinely needed (Worker pattern, Firewall pattern).

**Changes to `RunSubagentTool.cs`:**

```csharp
[McpServerTool, Description(
    "Delegate a task to an isolated subagent process with its own context window. " +
    "Use when you need: (1) full multi-step implementation of an independent subtask, " +
    "(2) security isolation for processing untrusted data, or " +
    "(3) a task that would consume too much of your remaining context. " +
    "The subagent cannot see your conversation history. " +
    "For simple code exploration, prefer explore_codebase instead — it's faster.")]
public static async Task<string> RunSubagent(
    [Description("Detailed task for the subagent. Include: what to do, " +
        "what files to look at, what you've learned, and what format to return.")]
    string prompt,
    
    [Description("Short 3-5 word label")]
    string description,
    
    [Description("Mode: 'explore' (read-only, 10 steps), 'verify' (read + test, 10 steps), " +
        "'execute' (full tools, 20 steps). Default: explore")]
    string mode = "explore",
    
    [Description("Context to pass: key files, known facts, failed approaches. " +
        "The subagent starts cold — include anything it needs to know.")]
    string? context = null,
    
    [Description("Max steps. Capped by mode limit.")]
    int? maxSteps = null)
```

**Mode implementation:** Pass `--ToolMode={mode}` as a CLI arg to the subagent process. In the subagent's `Program.cs`, parse this and configure `ToolRegistry` to filter tools:

```csharp
// In subagent's Program.cs, after tool loading:
var toolMode = config["ToolMode"]; // "explore", "verify", "execute"
if (toolMode is not null)
    toolRegistry.ApplyMode(toolMode);
```

```csharp
// In ToolRegistry.cs, add:
private static readonly Dictionary<string, HashSet<string>> ModeTools = new()
{
    ["explore"] = ["read_file", "list_directory", "grep_search", "file_search",
                   "search_codebase", "explore_codebase", "get_project_setup_info"],
    ["verify"]  = ["read_file", "list_directory", "grep_search", "file_search",
                   "run_tests", "run_bash_command", "get_errors", "test_failure"],
    ["execute"] = null! // null = all tools (no restriction)
};

public void ApplyMode(string mode)
{
    if (ModeTools.TryGetValue(mode, out var allowed) && allowed is not null)
    {
        _modeRestriction = allowed;
        // GetActiveTools() will intersect with _modeRestriction
    }
}
```

**Context handoff:** The `context` parameter is appended to the subagent's task prompt with a separator:

```csharp
var taskPrompt = prompt;
if (!string.IsNullOrWhiteSpace(context))
    taskPrompt += $"\n\n--- Context from parent agent ---\n{context}";
```

This is deliberately simple. The parent agent's existing Working Memory (key_facts, plan, hypotheses) naturally provides the right content for the `context` parameter. No separate briefing format needed — the LLM will write a natural-language context string.

**Budget coordination:** Pass `--MaxTotalTokens` and `--MaxSteps` to the subagent process, derived from mode defaults (not parent remaining budget, for now — that's a future optimization):

```csharp
var modeBudget = mode switch
{
    "explore" => (steps: 10, tokens: 100_000),
    "verify"  => (steps: 10, tokens: 100_000),
    "execute" => (steps: 20, tokens: 200_000),
    _         => (steps: 10, tokens: 100_000),
};
var steps = Math.Min(maxSteps ?? modeBudget.steps, modeBudget.steps);

psi.ArgumentList.Add($"--MaxSteps={steps}");
psi.ArgumentList.Add($"--MaxTotalTokens={modeBudget.tokens}");
psi.ArgumentList.Add($"--ToolMode={mode}");
```

**Changes required:**
1. Modify `McpServer/Tools/RunSubagentTool.cs`: add `mode`, `context` params; pass mode/budget flags
2. Modify `Forge.App/Program.cs`: parse `--ToolMode` arg and call `toolRegistry.ApplyMode()`
3. Modify `Forge.Core/ToolRegistry.cs`: add `ApplyMode()` method with mode-specific tool sets and `_modeRestriction` field
4. Modify `ToolRegistry.GetActiveTools()`: intersect with `_modeRestriction` when set
5. Update tests

---

### 8.4 Phase 7D: System Prompt & Auto-Activation

**What:** Update `SystemPrompt.cs` to guide the agent on when to use `explore_codebase` vs `run_subagent`, and auto-activate `run_subagent` when task complexity warrants it.

**System prompt changes in `SystemPrompt.Build()`:**

Add to the Plan phase section:

```
## Exploration

When you need to understand unfamiliar code, use explore_codebase to get a 
structured map of relevant files, classes, and dependencies in one step. 
This replaces manually searching and reading multiple files.

For tasks requiring full multi-step isolated execution — independent 
implementation subtasks, test suite validation, or processing untrusted 
external data — use find_tools("subagent") to access the delegation tool.
```

**Auto-activation logic in `AgentLoop.cs`:**

Instead of making `run_subagent` core (wastes ~200 tokens/turn for 80% of tasks that don't need it) or requiring `find_tools` discovery (wastes 1 step), auto-activate it based on task complexity signals:

```csharp
// In AgentLoop.RunAsync(), after the task is set:
if (TaskLooksComplex(task))
    _toolRegistry.Activate("run_subagent");

private static bool TaskLooksComplex(string task)
{
    // Heuristic signals that delegation might help:
    var complexitySignals = new[]
    {
        "all files", "every file", "across the codebase", "all endpoints",
        "refactor", "migrate", "each module", "parallel", "independent",
        "untrusted", "external", "github issue", "fetch", "web page"
    };
    var lowerTask = task.ToLowerInvariant();
    return complexitySignals.Count(s => lowerTask.Contains(s)) >= 2;
}
```

This is intentionally simple and conservative — two signals must match. False positives cost ~200 tokens/turn (the tool description); false negatives cost 1 step (the agent calls `find_tools`). The cost of false positives is low, so the heuristic can be generous.

**Changes required:**
1. Modify `Forge.Core/SystemPrompt.cs`: add exploration guidance section
2. Modify `Forge.Core/AgentLoop.cs`: add `TaskLooksComplex()` + auto-activation

---

### 8.5 Phase 7E: Delegation Failure Recovery

**What:** When `run_subagent` returns an empty/garbage/timeout result, classify the failure and guide recovery — instead of the parent blindly absorbing the failure.

**Implementation in `AgentLoop.cs` failure handling:**

The existing failure taxonomy has 7 types. Add an 8th:

```csharp
enum FailureType
{
    StaleContext,
    SyntaxError,
    TestFailure,
    DuplicateAttempt,
    Blocked,
    ToolMissing,
    DelegationFailure,  // NEW
    Unknown,
}
```

With a targeted nudge:

```csharp
FailureType.DelegationFailure => 
    "The subagent returned an empty or unhelpful result. " +
    "Options: (1) Try the task inline with explore_codebase for initial context, " +
    "(2) Retry with a more specific prompt and explicit return format, " +
    "(3) Break the task into smaller pieces you can handle directly.",
```

**Detection in `ToolExecutor`:** After receiving a `run_subagent` result, check if the output looks like a failure:

```csharp
if (toolCall.FunctionName is "run_subagent" or "RunSubagent")
{
    if (result.Contains("Error:") || result.Contains("timed out") ||
        result.Length < 50) // near-empty return
    {
        // Flag as delegation failure for taxonomy classification
        isDelegationFailure = true;
    }
}
```

**Changes required:**
1. Modify `Forge.Core/AgentLoop.cs`: add `DelegationFailure` to `FailureType` enum + nudge message
2. Modify `Forge.Core/ToolExecutor.cs`: detect delegation failures in result processing

---

### 8.6 Phase 7F: Evaluation & Tuning

**What:** Measure the impact of the new tools on real tasks before declaring victory.

**Evaluation plan:**

1. **A/B comparison on regression suite**: Run the existing 5 regression tasks with and without `explore_codebase`. Measure:
   - Total tokens used
   - Steps to first edit
   - Steps to completion
   - Success rate
   - Context utilization at completion

2. **Explore quality assessment**: For exploration-heavy tasks, compare:
   - `explore_codebase` output vs. manual `grep_search` + `read_file` chain
   - Does the structured summary contain the information the agent needed?
   - Does the agent make fewer false starts after using `explore_codebase`?

3. **Delegation quality**: For the 1-2 tasks that actually use `run_subagent`:
   - Does the mode restriction work? (explore subagent can't write files)
   - Does the context handoff prevent re-exploration?
   - Token cost comparison vs. inline execution

**Session analysis:** Update `SessionAnalyzer.cs` to track delegation metrics:
- Count of `explore_codebase` calls per session
- Count of `run_subagent` calls per session and their return quality
- Steps saved vs. sequential grep+read chains

**Changes required:**
1. Modify `Forge.Core/SessionAnalyzer.cs`: add delegation tracking metrics
2. Run regression suite, collect session logs
3. Compare metrics

---

### 8.7 Summary: What Changes Where

```
PHASE 7A — New compound exploration tool
  NEW:    McpServer/Tools/ExploreCodebaseTool.cs
  MODIFY: Forge.Core/ToolRegistry.cs (add to CoreTools)
  NEW:    Forge.Tests/ExploreCodebaseToolTests.cs

PHASE 7B — Rename SearchSubagent  
  RENAME: McpServer/Tools/SearchSubagentTool.cs → SearchCodebaseTool.cs
  RENAME: Forge.Tests/SearchSubagentToolTests.cs → SearchCodebaseToolTests.cs

PHASE 7C — Upgrade RunSubagent
  MODIFY: McpServer/Tools/RunSubagentTool.cs (add mode, context params)
  MODIFY: Forge.Core/ToolRegistry.cs (add ApplyMode method)
  MODIFY: Forge.App/Program.cs (parse --ToolMode)

PHASE 7D — System prompt & auto-activation
  MODIFY: Forge.Core/SystemPrompt.cs (exploration guidance)
  MODIFY: Forge.Core/AgentLoop.cs (TaskLooksComplex + auto-activate)

PHASE 7E — Delegation failure recovery
  MODIFY: Forge.Core/AgentLoop.cs (DelegationFailure type + nudge)
  MODIFY: Forge.Core/ToolExecutor.cs (detect delegation failures)

PHASE 7F — Evaluation
  MODIFY: Forge.Core/SessionAnalyzer.cs (delegation metrics)
  RUN:    Regression suite
```

**Total: 3 new files, 8 modified files, 2 renamed files.**

### 8.8 What This Deliberately Defers

See §13 (Future Directions) for the complete list with research basis and data-driven triggers to revisit each feature.

### 8.9 Execution Order & Dependencies

```
Phase 7B ─────────────────────────────────┐
(rename SearchSubagent → SearchCodebase)  │
                                          │
Phase 7A ─────────────────────────────────┤── Independent, can be parallel
(build ExploreCodebaseTool)               │
                                          │
Phase 7C ─────────────────────────────────┘
(upgrade RunSubagentTool with mode/context)
         │
         ▼
Phase 7D (system prompt + auto-activation)
         │   depends on 7A (explore_codebase exists)
         │   depends on 7C (run_subagent has mode param)  
         ▼
Phase 7E (delegation failure recovery)
         │   depends on 7C (run_subagent exists with modes)
         ▼
Phase 7F (evaluation)
         │   depends on all above being complete
         ▼
      DONE
```

**Phases 7A, 7B, and 7C are independent and can be implemented in parallel.** 7D depends on 7A+7C. 7E depends on 7C. 7F depends on everything.

**Estimated scope per phase:**
- 7A: ~150 lines of new code + ~50 lines of tests
- 7B: Mechanical rename (find & replace)
- 7C: ~80 lines modified across 3 files + ~60 lines of tests
- 7D: ~40 lines modified across 2 files
- 7E: ~30 lines modified across 2 files
- 7F: ~50 lines modified + session analysis

## 9. Cost Model

Delegation has a fixed overhead that must be justified by context savings:

```
Overhead per subagent:
  ~3,000 tokens: system prompt (role + REPO.md + workflow)
  ~500 tokens:   user message (task + context handoff)
  ~1,500 tokens: tool descriptions (mode-dependent: 5-40 tools)
  ~200 tokens:   result processing
  ─────────────
  ~5,200 tokens  fixed cost per delegation

Break-even analysis:
  A 10-step inline exploration adds ~20K tokens to parent context
  (2K tokens per step × 10 steps, monotonically growing prompt)
  
  A delegated exploration costs:
    5.2K overhead + subagent's own token usage (~30K for 10 steps)
    But only ~800 tokens return to parent context (structured result)
  
  Net parent context savings: 20K - 800 = 19.2K tokens  
  Net total spend: 30K + 5.2K = 35.2K tokens
  vs inline:         20K × growth factor (~1.5 for sawtooth) = 30K
  
  Delegation is ~17% more expensive in total tokens but saves 19.2K 
  tokens of parent context capacity for future steps.
```

**When delegation pays off:**
- Parent has many steps remaining (context savings compound)
- Task requires deep exploration (> 5 files, > 8 steps)
- Multiple independent explorations can be parallelized (wall time savings)
- Security isolation is needed (untrusted data processing)

**When inline wins:**
- Task is small (< 5 files, < 5 steps)
- Parent is near budget limit (can't justify fixed overhead)
- Parent needs the intermediate reasoning (not just the conclusion)
- Task requires iterative dialog with the parent's existing context

### 8.1 Future Cost Optimizations

Two deferred optimizations would significantly change the economics (see §12):

**Continuous budget ramp-down (Auton-inspired):** Instead of a binary "< 25% → don't delegate" threshold, apply a continuous cost multiplier: `effective_cost = overhead × 1/(1 - utilization)`. At 50% utilization → 2× cost, 75% → 4×, 90% → 10×. Matches KKT-optimal behavior from Auton's formal analysis.

**Heterogeneous model routing (CASTER):** CASTER (2026) showed 45-54% cost reduction via intelligent per-subtask model selection. If Explore subagents use a model ~3× cheaper than the parent, the break-even flips: delegation becomes 57% *cheaper* than inline, transforming it from a context-management tool into a cost-optimization tool.

## 10. Research References

| Decision | Paper | Key Finding |
|----------|-------|-------------|
| Context isolation for security | AGENTSYS (2026) | Isolation alone: 2.19% ASR vs 30.66% baseline |
| Single agent can match multi-agent | AgentArk (2026) | Process-Aware Distillation: single model internalizes multi-agent debate |
| Delegation creates positive externalities | Choose Your Agent (2026) | Even undelegated tasks improve when delegation is available |
| Dynamic > rigid orchestration | CORAL (2026) | Emergent patterns outperform predefined workflows: +8.49pp on GAIA |
| Tool reduction improves selection | HyFunc (2026) | Sending all tool descriptions = #1 token waste |
| Progressive tool disclosure | Agent Skills Architecture (2026) | Beyond critical library size, selection accuracy degrades |
| Hub-and-spoke for production | Research notes §6 | Simple, debuggable, clear authority |
| Document-centric > dialogue | MetaGPT (2023) | Structured outputs reduce hallucination cascading ~60% |
| Heterogeneous ensembles | Mixture of Agents (2024) | Different models > same model copies (65.1% vs 56.7%) |
| Cost-aware model routing | CASTER (2026) | Neural routing: 45-54% cost reduction, maintained quality |
| Prompted self-reflection unreliable | SPOC (2025) | LLM self-critique is unreliable — use external verification |
| External verification > LLM judgment | DeepVerifier (2026) | Decomposed tool-grounded checks: +12-48% F1 over holistic LLM-as-judge |
| Stateful runtime isolation | CaveAgent (2026) | Subagent with persistent state: +10.5% success, -28.4% tokens |
| Async execution & heterogeneous teams | GAIA2 (2026) | Coordination overhead exceeds benefit for strong models; heterogeneous teams +9.9pp; temporal awareness impossible in synchronous |
| Macro-micro decomposition | HiMAC (2026) | 14.5-18% improvement over flat approaches; emergent self-verification; failure containment via level isolation |
| Runtime self-reconfiguration | MAS² (2025) | Generator-Implementer-Rectifier: 20× cost reduction; Rectifier removal = -6.6% on complex tasks |
| Meta-discovered architectures | ADAS (2024) | Discovered topologies converge to parallel generation + synthesis; cross-domain transfer +25.9% |
| Formal cost optimization | Auton (2026) | KKT-optimal budget allocation; continuous cost ramp-down via shadow price λ |
| Context branching DAG | CMV (2026) | Git-like context forking eliminates re-exploration cost; 20-39% token reduction via structural trimming |
| Information distortion bounds | MCP Information Fidelity (2026) | Linear O(T) distortion, O(√T) deviation; re-ground every ~9 steps; semantic weighting reduces loss 80% |
| Sandbox delegation | OpenHands (2024) | AgentDelegateAction for specialist delegation; Docker sandbox; shared filesystem + isolated context |
| Semantic misalignment | Managing Uncertainty (2026) | Agents interpreting same terms differently is a primary coordination failure mode; terminology anchoring required |

## 11. Honest Limitations

1. **Delegation adds latency.** A 10-step subagent takes 30-120 seconds. If the task doesn't benefit from context isolation, this is pure overhead. GAIA2 showed that time-sensitive tasks suffer inverse scaling when reasoning latency increases — GPT-5 dropped from 34.4% to 0.0% under real latency constraints. The `explore_codebase` compound tool (§8.1) mitigates this for the most common case by avoiding process spawn entirely.

2. **No inter-subagent communication.** If Subagent A discovers something Subagent B needs, the parent must mediate. This is a feature (isolation) but feels limiting for tightly coupled subtasks.

3. **Cost model is approximate.** The break-even analysis depends on task complexity, model pricing, and context growth rates that vary per session.

4. **No learning about delegation quality.** Unlike the Reflexion-based LESSONS.md for task execution, there's no feedback loop that improves delegation decisions over time. Phase 7F logs delegation outcomes — a future enhancement would train a lightweight router from this data.

5. **Model quality bound.** The subagent uses the same model as the parent. CASTER (2026) showed 45-54% cost reduction from intelligent per-subtask model routing. Deferred until cheaper models are available and proven (see §8.8).

6. **Information fidelity loss is real and measurable.** Every delegation boundary adds ~0.5 distortion units (MCP Information Fidelity 2026). For depth-1 delegation this is acceptable, but chaining subagents (depth 2-3) accumulates loss. Poorly specified return formats lose more information than well-specified ones.

7. **Semantic alignment is assumed, not enforced.** The parent and subagent may interpret project-specific terms differently (Managing Uncertainty 2026). The `context` parameter in Phase 7C mitigates this but doesn't eliminate it.

## 12. Future Directions

Ordered by expected impact and feasibility. Updated March 2026 based on experiment results.

| # | Feature | Research Basis | Trigger to Revisit |
|---|---------|---------------|--------------------|
| 1 | **Heterogeneous model routing** — Explore subagents use a cheaper model | CASTER (45-54% cost reduction), MAS² (20× cheaper), GAIA2 (heterogeneous teams +9.9pp) | When GPT-5.4-mini or equivalent is available and benchmarked |
| 2 | **Promote explore_codebase back to core** — if future models use compound tools more naturally | Experiment F showed current models prefer grep+read over compound tools | If session data shows agents calling `find_tools("explore")` frequently |
| 3 | **Learned delegation routing** — Train a classifier from delegation outcomes | CASTER + ADAS (cross-domain transfer +25.9%) | After 50+ sessions with delegation metrics |
| 4 | **Self-rectifying delegation** — Auto-adjust strategy after repeated failures | MAS² Rectifier (-6.6% without it on complex tasks) | If DelegationFailure rate exceeds 20% in session data |
| 5 | **Parallel subagent execution** — Concurrent `run_subagent` calls | GAIA2 (wall time reduction for independent tasks) | If session logs show >2 independent `run_subagent` calls per session |
| 6 | **Budget coordination** — Parent deducts subagent cost from remaining budget | Auton (KKT-optimal allocation) | If agents routinely hit token limits due to subagent cost |
| 7 | **Macro-micro co-training** — Train parent and subagent jointly | HiMAC (14-18% improvement via alternating optimization) | If/when Forge moves to custom model training |

### Lessons Learned from Experiments

1. **Tool descriptions are the #1 lever for LLM tool selection.** Negative guidance ("Skip when X") is more effective than relying on the LLM to infer when NOT to use a tool (Experiment E vs E').
2. **LLMs don't optimize for step count.** They optimize for task completion using familiar tools. Compound tools that save steps are not naturally discovered or preferred (Experiment F).
3. **CLI arg parsing is a silent failure mode.** .NET's `AddCommandLine()` drops key=value args when mixed with flags and positional args. Environment variables are more reliable for inter-process config (Experiment C).
4. **Unit tests can miss integration failures.** The `ApplyMode` unit test passed perfectly while the actual CLI-to-config chain was completely broken (Experiment C).
5. **Output structure matters for truncation.** Front-loading navigational maps before detailed code excerpts ensures graceful degradation under the ObservationPipeline's character limit (Research review, §7.3).
