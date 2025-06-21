# Forge: A Research-Informed Coding Agent

> Design document — March 2026
> Grounded in 106 papers and research notes from this repository

## 1. Vision

Build a coding agent that operates at the level of a senior developer pair-programmer: it understands repository architecture before touching code, navigates dependency chains rather than grepping blindly, manages its own context budget like a scarce resource, and knows when to think deeply vs. act fast.

Unlike Claude Code or GitHub Copilot agent mode — which use a single flat ReAct loop with growing context — Forge is designed around three research-backed ideas that differentiate it:

1. **Repository-first understanding** — build a structural map of the codebase *before* writing code (Theory of Code Space, RIG)
2. **Active context management** — the agent explicitly manages its own context as a scarce resource, pruning and compressing rather than growing until overflow (Pensieve, SWE-Pruner, Active Context Compression)
3. **Adaptive cognitive depth** — match reasoning effort to task complexity, not uniformly deep (CogRouter: 7B model beats GPT-4o with smart depth allocation)

## 2. Design Principles

Drawn from cross-cutting conclusions across the research:

| # | Principle | Source |
|---|-----------|--------|
| 1 | **Context engineering > prompt engineering.** Most agent failures are context failures. A mediocre model with excellent context beats a frontier model with poor context. | Research notes §2, Paper catalogue |
| 2 | **The agent is a stateless reducer with an immutable event log.** Given the same context, it produces the same output. All state lives in serializable context. Every action is recorded as an immutable event — enabling pause, resume, replay, audit, and debugging. | Generic agent blueprint, 12-Factor Agents, ESAA |
| 3 | **Tool descriptions are the most important design surface.** Invest in ACI design as heavily as HCI — tool names, descriptions, error messages, and output formats are the agent's UX (SWE-Agent: +10.7pp from ACI alone). | SWE-Agent ACI, Research notes §4 |
| 4 | **Verification is cheaper than generation.** Invest disproportionately in verification — decompose checks into targeted sub-questions rather than holistic judgments (DeepVerifier: +12-48% F1). Smart retry with halt/rethink/alternative decisions beats brute-force sampling (CoRefine: 190× fewer tokens). | Research notes §10, CoRefine, DeepVerifier |
| 5 | **Ask early, not often.** Proactive clarification before planning prevents downstream waste (Ask-Before-Plan). But optimize *when* to ask: only when expected information gain justifies the interruption (BAO: multi-objective optimization). | Ask-Before-Plan, BAO |
| 6 | **Prefer a single well-designed agent over premature multi-agent.** Only split when one agent can't hold all needed context. AgentArk showed multi-agent quality can be distilled into single-agent weights. | Research notes §6 |
| 7 | **Design for failure first.** Max step limits, checkpointing, rollback, error recovery — from day one. 95% per-step accuracy over 20 steps = 36% end-to-end. | Research notes §7 |
| 8 | **Treat security as architecture, not afterthought.** Tool metadata itself is an attack surface (MCP tool poisoning). Stronger models are *more* vulnerable to tool-stream injection (VIGIL). Verify before committing irreversible actions. Enforce constraints at runtime, not by hoping the model follows instructions (ContextCov: 81% of repos have violations). | VIGIL, Securing MCP, ContextCov |

## 3. Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           Forge Agent Runtime                           │
│                                                                         │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │                    PHASE 0: Repository Intelligence               │  │
│  │  ┌──────────────┐  ┌──────────────┐  ┌────────────────────────┐  │  │
│  │  │ Build/Dep    │  │ Code Graph   │  │ REPO.md                │  │  │
│  │  │ Analyzer     │  │ (AST + LLM)  │  │ (Structural Snapshot)  │  │  │
│  │  └──────────────┘  └──────────────┘  └────────────────────────┘  │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                 │                                       │
│                                 ▼                                       │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │                    Context Assembler                               │  │
│  │  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐            │  │
│  │  │ System   │ │ Repo     │ │ Working  │ │ History  │            │  │
│  │  │ Prompt   │ │ Intel    │ │ Memory   │ │ (compressed)│         │  │
│  │  └──────────┘ └──────────┘ └──────────┘ └──────────┘            │  │
│  │                  │                                                │  │
│  │     ┌────────────┴─────────────┐                                  │  │
│  │     │ Context Budget Manager   │  ← token counting, pruning      │  │
│  │     │ (sawtooth, not monotonic)│    triggers, focus hints          │  │
│  │     └──────────────────────────┘                                  │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                 │                                       │
│                                 ▼                                       │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │                    Cognitive Router                                │  │
│  │  Classifies each step → depth level:                              │  │
│  │   L1 Instinctive (ls, cd, quick read)                            │  │
│  │   L2 Situational (file analysis, error triage)                   │  │
│  │   L3 Experience (pattern matching, design decisions)             │  │
│  │   L4 Strategic (architecture, multi-file planning)               │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                 │                                       │
│                                 ▼                                       │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │                    Agent Loop (Clarify-Plan-Act-Verify)            │  │
│  │                                                                   │  │
│  │   ┌──────────┐    ┌──────────┐    ┌──────────┐                   │  │
│  │   │ Clarify  │───▶│Plan(AND/ │───▶│  Act     │───▶┌──────────┐  │  │
│  │   │ (ask if  │    │ OR tree) │    │  (tools) │    │  Verify  │  │  │
│  │   │  needed) │    └──────────┘    └──────────┘    │(targeted)│  │  │
│  │   └──────────┘         ▲                          └────┬─────┘  │  │
│  │                        │    ┌─────────────────────┐    │        │  │
│  │                        └────│ HALT/RETHINK/ALTER. │◀───┘        │  │
│  │                             └─────────────────────┘             │  │
│  │   Guardrails: max steps, path restrictions, edit validation,     │  │
│  │               syntax checking, checkpoint/rollback, re-plan cap  │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                 │                                       │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │                    Tool Executor (MCP Server)                      │  │
│  │  ┌──────────────┐  ┌──────────────┐  ┌────────────────────────┐  │  │
│  │  │ File Ops     │  │ Search/Nav   │  │ Terminal/Execute       │  │  │
│  │  │ (read, write,│  │ (grep, AST,  │  │ (run, await, kill,    │  │  │
│  │  │  edit, mkdir) │  │  semantic)   │  │  sandbox)             │  │  │
│  │  └──────────────┘  └──────────────┘  └────────────────────────┘  │  │
│  │  ┌──────────────┐  ┌──────────────┐  ┌────────────────────────┐  │  │
│  │  │ Context Mgmt │  │ Web/External │  │ Human Interface        │  │  │
│  │  │ (compress,   │  │ (fetch, git  │  │ (askQuestions, memory, │  │  │
│  │  │  checkpoint) │  │  hub)        │  │  todos)                │  │  │
│  │  └──────────────┘  └──────────────┘  └────────────────────────┘  │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                         │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │                    Observation Pipeline                            │  │
│  │  Raw tool output → SWE-Pruner (goal-driven line pruning)         │  │
│  │                  → Error compaction (stack trace → actionable)    │  │
│  │                  → Token counting → Context budget check          │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                         │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │                    Security Layer                                  │  │
│  │  Verify-before-commit (irreversible actions)                      │  │
│  │  Constraint enforcement (project rules → runtime checks)          │  │
│  │  AST safety policy (block dangerous code/commands)                │  │
│  │  Tool metadata pinning (hash-verify MCP schemas)                  │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                         │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │                    Event Log (Immutable)                           │  │
│  │  Append-only events • Deterministic replay • Trajectory analysis  │  │
│  │  Cost tracking • Blame tracking • Evaluation hooks                │  │
│  └───────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────┘
```

## 4. Key Subsystems

### 4.1 Repository Intelligence (Phase 0)

**Insight:** RIG showed +12.2% accuracy and −53.9% completion time from a ~5K-token structural snapshot. Theory of Code Space found no current agent builds architectural maps — yet the strongest models outperform all baselines when they do. This is the highest-leverage intervention available.

**What it does:** Before the agent loop starts, pre-compute and cache:

1. **Build graph** — parse build files (csproj, package.json, Cargo.toml, etc.) to extract components, dependencies, build targets, and test mappings. Deterministic, AST-level extraction catches ~67% of edges reliably.
2. **Semantic dependency layer** — use a single LLM pass over the build graph + key entry points to identify the ~33% of dependencies that require semantic understanding (API calls, registry wiring, data flows).
3. **REPO.md snapshot** — serialize the combined graph into a compact structural document (~3-8K tokens depending on repo size). Include: top-level architecture, component boundaries, dependency edges, test coverage mapping, external packages.

**When it runs:**
- On first task in a repository
- Invalidated on significant structural changes (new projects, dependency changes)
- Cached to disk as a project artifact (human-readable, human-editable — following Claude Code's CLAUDE.md pattern)

**Key design choice:** This is *not* a code index or AST dump. It's a navigational map at the *architecture* level — build targets, dependency edges, test mappings. RIG deliberately omits ASTs and control-flow graphs because build-system-derived structure is more useful for "what depends on what" than source-level graphs.

### 4.2 Active Context Management

**Insight:** Context is the scarcest resource. Read operations consume ~76% of tokens (SWE-Pruner). Monotonically growing context causes attention dilution and reasoning degradation. Active compression achieves 22.7% savings with *no accuracy loss* (Active Context Compression). Pensieve/StateLM showed that agents with explicit context-pruning tools maintain 1/4 the active context of baselines while *improving* accuracy by 5-12%.

**The sawtooth pattern, not the growth curve:**

```
Tokens                   Current agents:        Forge:
  ▲                      ╱                       /\  /\  /\
  │                    ╱                        /  \/  \/  \
  │                  ╱                         /            \
  │                ╱                          /              →
  │              ╱                           /
  │            ╱                            
  │          ╱  ← grows until overflow     ← prune after each exploration phase
  └──────────────────────────▶ Steps
```

**Implementation — three mechanisms:**

1. **Observation pruning (per-step).** Every tool output passes through a lightweight pruner before entering context. Goal-driven, line-level scoring using the current task description as the focus hint. Irrelevant lines are dropped; the agent sees only task-relevant content. Backward-compatible: tools gain an optional `contextFocusHint` parameter. (SWE-Pruner: 23-38% token reduction, +1-2pp resolve rate)

2. **History compression (periodic).** Every 10-15 tool calls, trigger a compression cycle:
   - Summarize exploration findings into structured notes in Working Memory
   - Collapse intermediate tool call/result pairs to one-line summaries
   - Preserve: current plan, key findings, recent errors, full content of files being edited
   - Delete: raw search results, stale file contents, superseded observations
   
3. **Context budget tracking (continuous).** An explicit `checkBudget` mechanism gives the agent remaining-capacity feedback. When utilization exceeds 70%, the agent is prompted to run a compression cycle. At 85%, compression is mandatory.

### 4.3 Adaptive Reasoning (Scaffolding-Achievable)

**Insight:** CogRouter's 4-level cognitive depth is the right model, but its headline results (7B beats GPT-4o) required RL training with step-level credit assignment — not achievable through prompting alone. SFT alone can't learn adaptive reasoning; it just mimics the teacher's distribution (CogRouter finding). What *is* achievable through scaffolding:

**What we can do without custom training:**

1. **Structured output formatting by task phase.** During exploration, suppress chain-of-thought and emit tool calls directly. During planning, require explicit step-by-step reasoning. During debugging, require hypothesis enumeration. This is implemented via phase-specific system prompt segments, not model fine-tuning.

2. **Halt / Rethink / Alternative decisions.** Inspired by CoRefine's 3-way control signal: after each verification failure, the agent must classify its next action as one of:
   - **HALT** — the current approach succeeded; stop iterating
   - **RETHINK** — the approach is viable but the execution has a bug; fix and retry
   - **ALTERNATIVE** — the approach is fundamentally wrong; try a completely different strategy
   
   This prevents the common failure of repeatedly polishing a doomed solution. CoRefine showed this distinction alone yields 92.6% precision on halt decisions.

3. **Interleaved generation and verification.** Rather than generating a full solution then evaluating, alternate between proposing steps and verifying them inline (SPOC). Errors get caught mid-generation, avoiding wasted compute on doomed paths. Concretely: after each edit, immediately run the linter/type checker/relevant test before proceeding to the next edit.

**What requires custom training (future work):**
- True step-level depth allocation (CogRouter's confidence-aware advantage reweighting)
- Spontaneous self-correction without external verification signals (SPOC's self-play training)
- Learning *when* to reflect vs. act from outcome data rather than heuristics

**Honest assessment:** Without custom training, adaptive depth degrades to heuristic rules (e.g., "if >3 files, plan more"). This is still better than nothing — the structured output formatting and halt/rethink/alternative protocol are concrete improvements — but shouldn't be oversold as the CogRouter results.

### 4.4 Agent Loop: Clarify → Plan → Act → Verify

**Insight:** Plan-and-Execute with adaptive re-planning outperforms flat ReAct (Research notes §3, §8). But three under-addressed problems weaken most plan-execute designs:

1. **Plans start from ambiguous requirements.** Ask-Before-Plan showed proactive clarification before planning prevents downstream waste — agents that ask the right questions upfront produce plans that succeed more often than agents that guess and iterate.
2. **Flat plans can't represent fallback strategies.** StructuredAgent's AND/OR decomposition lets the agent isolate errors to specific branches and backtrack without restarting — AND nodes for required subtasks, OR nodes for alternative approaches.
3. **"Re-plan" is too coarse.** CoRefine's halt/rethink/alternative taxonomy gives the agent three distinct recovery paths, not just "try again."

**Phase structure per task:**

```
0. CLARIFY (proactive, before planning)
   - Parse the task/issue
   - Consult REPO.md for structural context
   - Identify ambiguities, missing information, unstated assumptions
   - If critical unknowns exist: ask the user (max 2 rounds)
   - Decision: ask only when expected info gain > interruption cost (BAO)

1. PLAN (AND/OR tree, not flat list)
   - Decompose into AND nodes (required subtasks) and OR nodes (alternatives)
   - For each leaf: expected outcome, verification method
   - Create a git checkpoint
   - Example:
     ├── AND: Fix auth bypass
     │   ├── AND: Locate vulnerability
     │   │   ├── OR: Search by header name
     │   │   └── OR: Trace middleware chain
     │   ├── AND: Implement fix
     │   └── AND: Verify fix
     │       ├── AND: Existing tests pass
     │       └── AND: New regression test

2. EXECUTE (per plan node, depth-first)
   - Localize: DFS along dependency chains, skeletons first
   - Act: atomic tool operations, linter-gated edits
   - Verify immediately after each edit:
     a. Syntax check (reject invalid edits before applying)
     b. Type check where available
     c. Run relevant tests
   - On failure, classify:
     HALT      → this subtree is done; move to next
     RETHINK   → approach is viable, execution has a bug; fix in place
     ALTERNATIVE → switch to OR sibling (fallback strategy)

3. VALIDATE
   - Run full test suite
   - Diff review: does the change match the intent?
   - If failing: re-plan from step 1 with new observations
   - Cap: max 3 re-plan cycles before escalating to user
```

**Key differences from v1 (flat plan-act-verify):**
- Proactive clarification eliminates the "guess wrong, waste 20 steps" failure mode
- AND/OR tree preserves partial progress when a branch fails (instead of re-planning from scratch)
- Halt/rethink/alternative gives structured error recovery instead of generic "retry"
- Hard cap on re-plan cycles prevents infinite loops (escalation > looping)

### 4.5 Working Memory

**Insight:** Working memory is the most underappreciated memory type (Research notes §5). Don't rely on conversation history as a substitute. Models that rely on implicit belief maintenance suffer catastrophic forgetting between steps (Theory of Code Space: 6× recall spread). Unify execution state and business state.

**Structured working memory, serialized into context at each step:**

```yaml
# Working Memory — maintained by the agent as a living document
task:
  description: "Fix authentication bypass in /api/users endpoint"
  status: executing_step_3
  
plan:
  - step: 1
    action: "Locate auth middleware chain"
    status: completed
    finding: "Auth handled in src/middleware/auth.ts, applied via router in src/routes/api.ts"
  - step: 2  
    action: "Identify bypass condition"
    status: completed
    finding: "Missing auth check when request has X-Internal header (line 47)"
  - step: 3
    action: "Add validation for X-Internal header"
    status: in_progress

key_facts:
  - "Auth middleware: src/middleware/auth.ts"
  - "Route registration: src/routes/api.ts:23-45"
  - "Tests: tests/auth.test.ts (14 passing, 0 failing)"
  
hypotheses: []

errors:
  - step: 2
    error: "Initial grep for 'bypass' found nothing — issue was header-based skip logic"
    resolution: "Searched for 'X-Internal' instead, found in auth.ts:47"

checkpoint: "fix/auth-bypass @ commit abc123"
```

**Why explicit working memory matters:**
- Prevents catastrophic forgetting between steps
- Makes the agent's understanding inspectable and debuggable
- Survives context compression (working memory is never pruned)
- Acts as self-assessment — externalizing understanding redirects exploration toward gaps (Theory of Code Space: +26 correct edges after a single probe)

### 4.6 Observation Pipeline

**Insight:** Read operations consume 76% of agent token budget (SWE-Pruner). Raw stack traces waste tokens — compact errors into ~100 tokens with type, cause, fix (Research notes §2). SWE-Agent's key contribution was concise, informative feedback at every step and capping search results at ≤50.

**Every tool output passes through this pipeline before entering context:**

```
Raw tool output
    │
    ▼
┌─────────────────────────┐
│ 1. Size gate             │  If >500 lines, apply pruner
│ 2. Goal-driven pruner    │  Score lines against task focus hint
│ 3. Error compactor       │  Stack trace → {type, cause, suggestion}
│ 4. Result capper         │  Search results capped at 30 items
│ 5. Token counter         │  Track observation cost
│ 6. Budget check          │  Trigger compression if >70% utilization
└─────────────────────────┘
    │
    ▼
Processed observation (enters context)
```

### 4.7 Tool Design (ACI Principles)

**Insight:** SWE-Agent's Agent-Computer Interface design accounted for +10.7pp improvement. Anthropic spent more time optimizing tool descriptions than the overall prompt for SWE-bench. Tool descriptions should explain when to use vs. alternatives, expected output, common pitfalls, and failure behavior.

**Tool design rules:**

1. **Absolute paths only.** Require absolute paths everywhere — eliminates an entire class of path-resolution errors (Anthropic finding).
2. **Atomic actions with built-in feedback.** Every edit tool returns the updated content around the changed lines. The agent always sees the effect of its action.
3. **Linter-gated edits.** Edit tools run a syntax check before applying. Invalid edits are rejected with a specific error, preventing the agent from corrupting state.
4. **Rich error messages.** `"File not found: {path}. Similar files: {matches}. Use listDirectory to explore."` not `"File not found"`.
5. **Compound operations where natural.** `searchAndReplace` instead of requiring read → find location → replace → verify as separate calls. Reduce the number of turns for common sequences.
6. **Optional focus hints.** File-read tools accept an optional `contextFocusHint` for the observation pruner. No hint → no pruning (backward compatible).
7. **Structural metadata before full content.** Navigation tools return skeletons (function signatures, class outlines) first. Full source loading is a separate, explicit action — per SWE-Adept's "defer full code loading to final stage" pattern.

## 5. Security Model

**Why this section exists:** The current design had zero security considerations. This is not optional polish — it's architectural.

- MCP tool *metadata* is a semantic attack surface: a poisoned tool description can steer model reasoning without any code execution (Securing MCP).
- Stronger, more aligned models are *more* vulnerable to tool-stream injection because they treat injected instructions as authoritative (VIGIL).  
- 81% of repositories have agent instruction violations when constraints aren't runtime-enforced (ContextCov).

### 5.1 Threat Model

| Threat | Vector | Mitigation |
|--------|--------|------------|
| Prompt injection via tool output | Malicious content in fetched web pages, file contents, or error messages | Speculative execution: reason about tool output in a sandboxed context; verify-before-commit for irreversible actions (VIGIL: −22% ASR, +2× utility) |
| Tool metadata poisoning | Compromised MCP server provides tool descriptions that steer reasoning | Pin and audit tool descriptions; hash-verify tool schemas at startup; fail-closed on unknown tools |
| Instruction drift | Agent ignores project conventions stated in AGENTS.md | Runtime constraint enforcement via AST-based checks on generated code (ContextCov approach), not just soft system prompt text |
| Arbitrary code execution | Agent generates dangerous shell commands or code | AST-based static analysis policy (CaveAgent: ImportRule, FunctionRule, AttributeRule) before execution; allowlist of safe commands |
| Data exfiltration | Tool calls that send workspace content to untrusted URLs | Domain allowlist for outbound requests; flag any URL not previously seen in project config |

### 5.2 Verify-Before-Commit Protocol

Inspired by VIGIL's "verify before commit" paradigm: the agent separates *speculative reasoning* from *irreversible action*.

- **Reversible actions** (read file, search, list directory): execute immediately
- **Irreversible actions** (write file, run shell command, git push): generate a structured intent, then verify the intent against:
  1. Current plan — does this action serve the active goal?
  2. Constraint set — does it violate any project rules?
  3. Safety policy — is this command in the allowlist?
- Only after verification passes does the action execute

This adds one validation step per irreversible action. The overhead is minimal (~50 tokens per check) but it eliminates the class of attacks where injected instructions cause harmful side effects.

### 5.3 Constraint Enforcement

Project rules from `.github/copilot-instructions.md`, `AGENTS.md`, `.editorconfig`, etc. are not treated as passive context. They are parsed into a constraint set at startup and enforced at runtime:

- **Path-aware rule scoping** — "use pytest" under "Backend > Testing" applies only to `src/backend/tests/`, not frontend (ContextCov's hierarchical extraction)
- **Fail-closed on ambiguity** — if uncertain whether a constraint applies, block the action and require explicit override (false positives are correctable; false negatives silently accumulate as tech debt)
- **Enforcement via generated-code analysis** — before applying edits, run lightweight Tree-sitter checks against the constraint set (e.g., "no wildcard imports", "use strict mode")

## 6. Event Log (Auditability)

**Insight:** ESAA showed that no mainstream agent framework (AutoGen, MetaGPT, LangGraph, CrewAI) provides immutable audit trails, deterministic replay, or blast-radius containment. Event sourcing solves all three.

**Design:** The agent emits structured *intentions* (not raw LLM output). A deterministic orchestrator executes them and records an append-only event log.

```
Event schema:
{
  "seq": 42,
  "timestamp": "2026-03-18T09:15:23Z",
  "phase": "execute",
  "type": "tool_call",
  "tool": "editFile",
  "input": { "filePath": "/src/auth.ts", "oldString": "...", "newString": "..." },
  "output_summary": "Edit applied successfully, 3 lines changed",
  "tokens_used": 1247,
  "context_utilization": 0.63,
  "verification": { "linter": "pass", "type_check": "pass" },
  "working_memory_hash": "a3f8c2..."
}
```

**What this enables:**
- **Deterministic replay** — reproduce any session by replaying events against the tools
- **Trajectory analysis** — compute efficiency metrics, identify wasted steps, measure cost
- **Blame tracking** — trace any file change back to the specific plan step and reasoning that caused it
- **Evaluation hooks** — plug in Agent-as-a-Judge or SWE-bench harnesses without modifying the agent

## 7. Novel Capabilities (Beyond Current Agents)

### 7.1 Hypothesis-Driven Debugging

**Source:** SWE-Adept's checkpointed hypothesis testing (+4.7% resolve rate), Research notes §8 debugging heuristic.

Current agents debug by trial-and-error. Forge uses a structured protocol:

```
OBSERVE  → collect error output, stack trace, recent changes
HYPOTHESIZE → generate 2-3 ranked hypotheses for the root cause
RANK     → score by: likelihood × testability × reversibility
TEST     → test top hypothesis on an isolated checkpoint branch
DIAGNOSE → did the test confirm or refute? update hypothesis ranking
FIX      → apply the confirmed fix to main
VERIFY   → run tests, confirm no regressions
```

Each hypothesis gets its own git branch. If a fix attempt fails, rollback is instant (not manual undo). This transforms chaotic trial-and-error into systematic problem-solving.

### 7.2 Exploration Budget

**Source:** Theory of Code Space (broader file coverage directly improves recall), Active Context Compression (exploration-heavy tasks benefit most from compression).

For unfamiliar codebases, Forge explicitly budgets exploration before implementation:

- **Exploration phase:** read the REPO.md, navigate key dependency chains, build understanding in Working Memory. Compression runs frequently.
- **Transition signal:** Working Memory contains sufficient facts to execute the plan (agent self-assesses).
- **Implementation phase:** compression is less aggressive (prior context stays relevant during iterative refinement).

This prevents the failure mode where agents dive into editing immediately, then waste steps because they misidentified entry points and dependency chains.

### 7.3 Skeleton-First Navigation

**Source:** SWE-Adept (+5.4% on function-level localization with DFS skeleton navigation).

When exploring unfamiliar code, Forge loads structural metadata first:

```
1. listDirectory → see file structure
2. readSkeleton(file) → function signatures, class outlines, exports (~20% tokens of full file)
3. Only after identifying the right location: readFile(file, startLine, endLine) → full source
```

This dramatically reduces context consumption during navigation. The skeleton tool is a lightweight AST-based extractor that returns signatures, docstrings, and structural hierarchy without method bodies.

### 7.4 Evolving Reflective Memory

**Source:** Reflection-Driven Control (+2.9-11.2% security rate from continuous reflection), BAO (retrospective reasoning between turns).

Current agents start every session from scratch. Forge accumulates project-specific repair patterns:

- **After each successful fix:** record the error pattern, root cause, and fix strategy as a compact memory entry
- **Before each debugging step:** retrieve relevant past repairs via semantic similarity to the current error
- **Memory reaches saturation quickly** — Reflection-Driven Control found 100% retrieval success by round 4, meaning a small corpus of 20-50 project-specific patterns covers most recurring issues
- **Stored as human-readable YAML** alongside the REPO.md — the developer can review, edit, and version-control the agent's learned patterns

This is *not* a general knowledge base. It's a narrow, high-signal collection of "in *this* repo, when you see X, the fix is Y." The scope limitation is deliberate — broad memory adds latency without proportional value (Anatomy of Agentic Memory: match memory complexity to actual need).

### 7.5 Decomposed Verification

**Source:** DeepVerifier (+12-48% F1 over holistic LLM-as-judge by decomposing checks into sub-questions).

Instead of asking "is this implementation correct?" (holistic, unreliable), Forge decomposes verification into targeted sub-checks:

```
After implementing a fix:
  ✓ Does the changed file still parse? (linter)
  ✓ Do types still check? (type checker)
  ✓ Does the specific test case that failed now pass? (targeted test)
  ✓ Do adjacent test cases still pass? (regression)
  ✓ Does the diff touch only the files identified in the plan? (scope check)
  ✓ Are there any new linter warnings in changed files? (quality)
```

Each check is concrete, binary, and grounded in external tools — not LLM self-assessment. The holistic "does this look right?" question is asked *only after* all targeted checks pass, as a final sanity check with a single reflection round (Reflection-Driven Control: diminishing returns after round 1).

## 8. What This Deliberately Omits

| Omission | Rationale |
|----------|-----------|
| **Multi-agent orchestration** | Single well-designed agent outperforms poorly coordinated multi-agent (Research notes §6). Subagent delegation is supported for context isolation, but the core loop is single-agent. If multi-agent is needed later, AgentArk's distillation approach (multi-agent quality at single-agent cost) is the path. |
| **Custom model training** | This is a scaffolding-level agent. daVinci-Dev's training recipe, CogRouter's RL-trained depth allocation, and SPOC's self-play verification are noted as the most impactful future investments, but are not launch requirements. |
| **Declarative workflow DSL** | PayPal's declarative agent workflows cut dev time by 60%, but Forge targets developers who want code-level control. A DSL layer can be added later if non-engineers need to configure agent behavior. |
| **Formal verification of agent behavior** | Formalizing Agent Designs showed many "novel" agents are actually equivalent under formal analysis. This insight informed the design (don't over-differentiate patterns) but runtime formal verification is premature for a v1. |
| **Browser automation** | Focus on code-first. Web fetch for documentation is included; full browser interaction is out of scope. |

## 9. Honest Limitations

Things this design *cannot* do well without further research or custom training:

1. **True adaptive reasoning depth.** Without RL training, cognitive depth is heuristic-based. The scaffolding version (§4.3) improves over flat ReAct but doesn't achieve CogRouter's 82% results.

2. **Self-correction without external signals.** Prompted self-reflection is unreliable (Research notes §10). The design compensates by using external verification (linters, tests, type checkers) rather than asking the model to judge its own output — but for tasks without clear external verification criteria, self-assessment remains brittle.

3. **Long-horizon tasks (>50 steps).** Compound error rates are brutal: even 95% per-step accuracy over 50 steps = 7.7% end-to-end. The design mitigates this (active compression, checkpointing, re-planning) but cannot eliminate the fundamental issue. Tasks exceeding ~30 steps should be decomposed by the user or a planning agent.

4. **Codebases without build systems.** Repository Intelligence depends on build file parsing. Repos that are a collection of scripts without dependency metadata get a degraded experience — structural understanding falls back to heuristic file-tree analysis.

5. **Novel frameworks and languages.** The agent's knowledge is bounded by the model's training data. For very new or niche frameworks, the model will hallucinate APIs. The reflective memory (§7.4) helps for *recurring* patterns but doesn't solve first-encounter hallucination.

## 10. Implementation Plan

See [implementation-plan.md](implementation-plan.md) for the full phased plan with checkpoints, decision points, and refactoring windows.

**Summary of phases:**

| Phase | Goal | Key Deliverable |
|-------|------|-----------------|
| 0 | Project scaffolding | Build compiles, projects in solution |
| 1 | Dumbest working agent | ReAct loop + MCP + event logging |
| 2 | Memory & planning | Working Memory + Plan-Act-Verify |
| 3 | Context intelligence | REPO.md + observation pipeline + budget tracking |
| 4 | Security & verification | Verify-before-commit + decomposed checks + halt/rethink/alternative |
| 5 | Proactive intelligence | Clarification + hypothesis debugging + reflective memory |
| 6 | Evaluation infrastructure | SWE-bench harness + trajectory analysis + regression suite |

Each phase ends with a checkpoint (run agent on real tasks), a review (what worked, what didn't), and a decision point (what to keep, cut, or defer).

## 11. Research References

Key papers that informed specific design decisions:

| Decision | Paper | Key Finding |
|----------|-------|-------------|
| Repo Intelligence | RIG (2026) | +12.2% accuracy, −53.9% time from ~5K-token structural snapshot |
| Observation pruning | SWE-Pruner (2026) | 76% of tokens are read ops; goal-driven pruning: −23-38% tokens, +1-2pp accuracy |
| Sawtooth context | Pensieve/StateLM (2026) | 1/4 active context, +5-12% accuracy with explicit prune tools |
| Active compression | Focus (2026) | −22.7% tokens, no accuracy loss; compress every 10-15 calls |
| ACI design | SWE-Agent (2024) | +10.7pp from agent-specific tool design; linter-gated edits key guardrail |
| Skeleton navigation | SWE-Adept (2026) | DFS skeletons beat BFS expansion by 5.4% on localization |
| Codebase maps | Theory of Code Space (2026) | No current agent builds arch maps; strongest models outperform when they do |
| Hypothesis debugging | SWE-Adept (2026) | Checkpointed hypothesis testing: +4.7% resolve rate |
| Stateful runtime | CaveAgent (2026) | Code-as-action: −28.4% tokens, −38.6% steps; 30B matches Sonnet 4.5 |
| Memory taxonomy | Anatomy of Agentic Memory (2026) | Match memory complexity to need; benchmark external memory vs full-context first |
| Halt/Rethink/Alternative | CoRefine (2026) | 92.6% halt precision, 190× fewer tokens than majority voting |
| Decomposed verification | DeepVerifier (2026) | Sub-question checks: +12-48% F1 over holistic LLM-as-judge |
| Proactive clarification | Ask-Before-Plan (2024) | Asking the right questions upfront prevents downstream plan failures |
| Human interaction cost | BAO (2026) | Multi-objective: ask only when info gain > interruption cost |
| AND/OR planning | StructuredAgent (2026) | AND/OR trees isolate errors to branches; preserve partial progress on backtrack |
| Self-correction honesty | SPOC (2025) | Prompted self-reflection unreliable; RL-trained self-play needed for true self-correction |
| Reflective memory | Reflection-Driven Control (2025) | +2.9-11.2% from continuous reflection; saturation at ~4 rounds / ~50 patterns |
| Tool-stream injection | VIGIL (2026) | Stronger models *more* vulnerable; verify-before-commit: −22% ASR, +2× utility |
| Tool poisoning | Securing MCP (2025) | Tool metadata is a semantic attack surface — no execution required to steer reasoning |
| Constraint enforcement | ContextCov (2026) | 81% of repos have violations without runtime enforcement; fail-closed on ambiguity |
| Event sourcing | ESAA (2026) | Append-only event logs enable replay, audit, and blast-radius containment |
| Context format | Structured Context Eng. (2026) | YAML is 28-60% more token-efficient than JSON/MD for agent-consumed context |
| Adaptive depth (future) | CogRouter (2026) | 7B beats GPT-4o (82.3% vs 42%) with RL-trained depth — requires custom training |
| Training recipe (future) | daVinci-Dev (2026) | Agent-native trajectories + mid-training outperform post-training alone |
| Formal analysis | Formalizing Agent Designs (2026) | Many "novel" agents equivalent under formal analysis; don't over-differentiate |
