# Forge: Implementation Plan

> Companion to [design.md](design.md) and [research-review-2026-03.md](research-review-2026-03.md) — Updated March 2026
>
> This plan tracks what's been built, what validated, what was cut, and what's next.
> Phases 0-6D are complete, informed by experiment observations,
> 352 passing tests, and 50+ research papers surveyed across Phases 4-6.

## Philosophy

This plan is structured around **learning loops, not waterfall phases**. Each phase ends with a checkpoint where we run the agent against real tasks and decide what to keep, what to refactor, and what to cut. The ordering is driven by a single insight: **you can't evaluate what you can't run**.

## Technology Decisions (Actual)

| Decision | Choice | Rationale | Notes |
|----------|--------|-----------|-------|
| Language | C# / .NET 10 | Matches existing repo. Static typing. Fast compile cycles. | ✅ Working well |
| Tool backend | MCP server (HTTP, port 5000) | 40 tools implemented (10 core + 30 discoverable via find_tools). | ✅ Stable |
| LLM client | OpenAI Responses API (`ResponsesClient`) | Switched from `IChatClient` to support reasoning_effort + tools. Custom `ILlmClient` abstraction. | Changed from plan |
| Context management | Client-managed sawtooth | No `PreviousResponseId`. Build InputItems from scratch each call. Relevance-weighted compression. Adaptive window (6/8/12 turns). | Not in original plan |
| Event log | Append-only JSONL per session | Working. Millisecond timestamps to avoid collisions. | ✅ |
| Project structure | `Forge.Core` + `Forge.App` + `Forge.Tests` | ~18 core files, ~26 test files, 352 tests passing. | ✅ |

---

## Phase 0: Project Scaffolding ✅ Complete

Set up project structure, dependencies, build.

**Delivered:** Forge.Core, Forge.App, Forge.Tests. Added to agents.sln. User secrets for API keys. Serilog logging (console + CLEF).

---

## Phase 1: Working Agent ✅ Complete

**Goal:** A single agent loop that can solve coding tasks via MCP tools.

### What Was Built
- **AgentLoop** — Orchestration with streaming, tool execution delegation, consecutive failure nudging
- **OpenAIResponsesLlmClient** — Responses API with client-managed context, sawtooth compression
- **ToolExecutor** — Guardrails, duplicate detection, observation processing, error tracking
- **ToolRegistry** — Two-tier progressive disclosure (8 core + find_tools meta-tool for 18+ more)
- **ObservationPipeline** — Size gating, stack trace compaction, line counting
- **Guardrails** — Path restriction (workspace-only), command denylist with regex, resource limits
- **EventLog** — JSONL session recording, crash-safe FileStream handling
- **SystemPrompt** — Plan→Act→Verify with grounded thinking and verification checklists
- **40 MCP tools** — 10 core (read_file, create_file, replace_string_in_file, grep_search, file_search, list_directory, run_bash_command, run_tests, get_project_setup_info, manage_todos) + 30 discoverable via find_tools
- **DryRunPreview** — `--dry-run` flag prints system prompt + tool list without LLM call

### Checkpoint 1 Result
Agent completed 5/5 smoke tests. Self-improvement tasks (Level 1-3) completed successfully: agent modified its own code, all tests passed after.

### What Was Cut From Original Plan
- `Microsoft.Extensions.AI IChatClient` — replaced with custom `ILlmClient` + Responses API
- `Microsoft.Extensions.Hosting` — unnecessary for a CLI tool
- YAML working memory format — not needed; sawtooth compression handles context management

---

## Phase 2: Research-Informed Improvements ✅ Complete

**Goal:** Apply cutting-edge research findings to improve reliability and efficiency.

### What Was Built (from [research-review-2026-03.md](research-review-2026-03.md))
- **Grounded thinking prompts** — System prompt requires outcome prediction before edits (Dyna-Think, Brittle ReAct)
- **Per-tool verification checklists** — Structured verification steps after replace/create/bash/tests (DeepVerifier)
- **Progressive deepening** — Reasoning effort escalates Medium→High after 2 consecutive failures (Kimi k1.5)
- **Failure taxonomy** — 6 typed failures (StaleContext, SyntaxError, TestFailure, DuplicateAttempt, Blocked, ToolMissing) with targeted recovery nudges (DeepVerifier, DEFT)
- **LESSONS.md cross-session learning** — Auto-generates lessons on failure/costly sessions; injects into system prompt on next session (Reflexion)
- **Relevance-weighted compression** — Active-file turns get 400-char previews; inactive turns get minimal summaries (SWE-Pruner)
- **Adaptive KeepRecentTurns** — Edit phases keep 12 turns, exploration phases keep 6 (SWE-Pruner)
- **SSRF protection** — Both FetchWebPage and OpenBrowserPage block private/internal IPs
- **Atomic file writes** — ReplaceStringInFileTool writes to .tmp then renames
- **JsonDocument path extraction** — Guardrails no longer use fragile string parsing

### What Was Validated
- Progressive deepening mechanism (via `SetReasoningEffort` on ILlmClient)
- Failure taxonomy classifies all 6 types correctly (7 tests)
- Lessons generate for failed and costly sessions, skip cheap successes (3 tests)
- System prompt contains verification checklists and grounded thinking (7 tests)
- 4 full review passes found and fixed 30+ issues across critical/medium/low severity

### What Was Cut
- **Cognitive Router** — Too complex for current maturity; progressive deepening gives 80% of the value at 5% of the cost
- **Separate verifier model** — DeepVerifier's rubric approach is good but prompt-level checklists give 60-70% of the value at 0% cost
- **YAML WorkingMemory struct** — The sawtooth compression + LESSONS.md provides sufficient state management without a separate data structure

---

## Phase 3: Session Continuity & Repository Intelligence ✅ Complete

**Goal:** The agent can resume interrupted sessions effectively and understand repos before acting.

**Key decisions:** Hybrid discovery-extraction + agent consolidation for handoffs (AriadneMem, TraceMem).
Sticky file summaries + redundancy detection for compression (MemoryOS, FuseSearch).
Auto-generated REPO.md for structural awareness (RIG, Agent Skills Architecture).
Graceful Ctrl+C with proactive consolidation at 80% budget (BAO pattern).

**Validated by:** Phase 5 experiments (Scenarios 3, 3b) confirmed handoff quality enables
effective resume. Consolidation summary dominates auto-extraction for resume quality.

### 3A-3E: All Complete ✅

- [x] 3A: Filename sanitization (spaces, quotes, unicode → hyphens)
- [x] 3B: Rich handoff notes (discovery-aware extraction + consolidation summary + smart continuation)
- [x] 3C: Sticky file summaries in compression + redundancy-aware re-read detection
- [x] 3D: REPO.md auto-generation (5 ecosystems, .sln parsing, structural map)
- [x] 3E: Graceful interruption (Ctrl+C handler, proactive consolidation at 80% budget)

---

## Phase 4: Verification & Trust ✅ Complete

**Goal:** The agent's fixes are verified by tools, not self-assessment.

- [x] VerificationTracker: data-driven rules with selective reminder injection (CoRefine-inspired)
- [x] Git checkpoint via `git stash create` before task execution
- [x] VerificationReport: TotalEdits, VerifiedEdits, ComplianceRate

**Validated by:** Phase 5 experiments showed 100% verification compliance across all sessions. VerificationTracker never fired a reminder — the agent verifies proactively. Safety net confirmed working but never needed.

---

## Phase 5: Proactive Intelligence

**Goal:** The agent debugs systematically, handles underspecified tasks, and preserves session narrative for handoffs.

### 5A. Hypothesis-Driven Debugging ✅

When the task is a bug fix:
- [x] Conditional debugging protocol in system prompt (keyword detection)
- [x] For-and-against prompting: each hypothesis states what confirms AND refutes it
- [x] Escalating diagnostic intensity (L1: prompt, L2: monologue, L3: runtime inspection)
- [x] Debugging-aware failure nudges (monologue at 2+ failures, runtime at 3+)
- [x] Hypothesis reasoning detection (observability logging)
- [x] 30 new unit tests (276 total, up from 246)
- [x] Design doc: research/phase-5a-hypothesis-debugging-design.md (14 papers surveyed)

### 5B. Proactive Clarification ✅ (Explore-Then-Assume)

Research: 13 papers surveyed (Ambig-SWE ICLR 2026, DS-IA, InteractComp, AwN, TENET, CLAMBER, ReHAC, AT-CXR, + 5 more)
Design doc: research/phase-5b-proactive-clarification-design.md

- [x] Always-on assumption guidance in PLAN section (not conditional — bypasses unreliable detection)
- [x] "Check tests for intent" guidance (TENET: tests as executable specifications)
- [x] Assumption detection: `ContainsAssumptionReasoning()` + `ExtractAssumptionText()`
- [x] Early-step assumption capture in AgentLoop (steps 0-2 only)
- [x] Sticky assumption breadcrumbs in sawtooth compression (survive context window)
- [x] SessionHandoff.Assumptions field with extraction + continuation prompt
- [x] Assumption-aware lesson generation (cross-session learning feedback loop)
- [x] 25 new unit tests (301 total, up from 276)

### 5C. Episodic Consolidation for Session Handoffs ✅

Research: 13 papers surveyed (Steve-Evolving Mar 2026, Nemori Aug 2025, SAMULE EMNLP 2025, Repository Memory ICLR 2026, K²-Agent, Mem-α, SeaView, A2P, + 5 from earlier phases)
Design doc: research/phase-5c-episodic-consolidation-design.md
Codebase audit: 10 existing episode-like signals across 4 files unified

**Layer 1 — Live pivot capture in AgentLoop (during execution):**
- [x] `ContainsPivotReasoning()` + `ExtractPivotReason()` — detect approach changes in response text
- [x] Capture pivot reasons into `List<(Step, Reason)>` alongside assumptions and consolidation
- [x] Sticky pivot breadcrumbs in `CompressTurns()` — `[Pivot: "reason"]` survives sawtooth compression

**Layer 2 — Post-hoc episode segmentation (in Finish/Handoff):**
- [x] `EpisodeSegmenter` class — heuristic segmentation from tool-type classification. ~170 lines, no LLM.
- [x] `EpisodeSummary` record — Type/StartStep/EndStep/Outcome/FilesInvolved
- [x] `SessionHandoff.Episodes` + `SessionHandoff.PivotReasons` — new fields with extraction fallbacks
- [x] `BuildContinuationPrompt()` includes episode trajectory chain + approach transition reasons
- [x] `BuildTrajectoryLine()` for compact lesson format: `explore(0-3) → impl(4-7,FAIL) → verify(8-9)`
- [x] `GenerateLesson()` appends trajectory line to failed session lessons
- [x] 34 new unit tests (335 total, up from 301)

**What was CUT from the original plan:**
- ~~LLM segmentation~~ → Heuristic from tool patterns (cheaper, no latency)
- ~~.handoff.json~~ → Episodes in existing SessionHandoff record
- ~~Per-episode insight extraction~~ → Live pivot capture IS the insight
- ~~Layer 3 repo feedback~~ → Deferred to Future Work (auto-generated REPO.md conflict)

### 5D. Self-Improvement Tasks → Merged into Phase 6A

Original plan called for Level 4 (debugging) and Level 5 (implement from spec) self-improvement tasks.
Experiment observations showed these overlap with the regression suite design.
The curated regression task suite (Phase 6A) subsumes 5D with more specific,
research-informed task definitions targeting the features we actually need to validate.

---

## Phase 6: Evaluation & Refinement

**Goal:** Measure what we built, fix what the experiments revealed, build repeatable evaluation.

Observations doc: research/phase-5-experiment-observations.md (2 experiments, 5 insights, 5 research connections)

### 6A. Fix Lesson Quality + Regression Suite ✅

**Problem identified in experiments:** `GenerateLesson` listed all errored tools even for budget-exhaustion failures.

- [x] Fix `GenerateLesson()`: budget-exhaustion failures omit `failed tools`, note budget constraint instead
- [x] Create `regression-tasks.json` with 5 curated tasks targeting tail-case features
- [x] 4 new unit tests (342 total). Validated with live scenario: lesson correctly omits tool blame
- [x] Research basis: BREW (noisy lessons degrade), TestPrune (small+diverse suites), A2P (counterfactual attribution)

### 6B. Mid-Session Consolidation + Resume Optimization ✅

**Problem identified in experiments:** Consolidation fires only at 80%. Resumed sessions spend ~20% budget re-reading files.

- [x] Add 50% progress checkpoint: quietly captures substantive thought for sessions >8 steps
- [x] Optimize resume re-verification: `BuildContinuationPrompt` suggests `grep_search` for known files instead of full `read_file`
- [x] Research basis: BAO (multi-checkpoint), Steve-Evolving (subgoal anchoring), SWE-Pruner (reads = 76% cost)

### 6C. Operational Efficiency ✅ (from experiment observations)

Design doc: research/operational-observations-design.md (5 observations, Efficiency Frontier model)

- [x] Read coalescence guidance in ACT section: "Read files in generous ranges. Each read consumes a step."
- [x] Enriched consolidation prompt: now asks for assumptions validation + highest-risk remaining step
- [x] Research basis: SWE-Pruner (reads = 76% of cost), Reflexion (agent self-assessment > extraction)

**Deferred:**
- Hierarchical compression (~40 lines but touches sawtooth — high risk, only benefits sessions >12 steps)
- Session splitting as deliberate strategy (needs auto-resume infrastructure)

### 6D. Trajectory Analysis + Progress Metrics ✅ Complete

Design doc: research/phase-6d-trajectory-analysis-design.md (10 papers: PRInTS, PRM Survey, Agent-RewardBench, SWE-Effi, iStar, + 5 from corpus)

**Architecture:** `--analyze <path>` flag on Forge.App. Reads session JSONL, computes metrics, outputs text summary. ~270 lines in SessionAnalyzer.cs.

- [x] Session parser: extract steps + handoff from JSONL using existing `HandoffGenerator.LoadFromSessionFile()`
- [x] 6 core metrics: steps/task, tokens/step growth (SWE-Effi "token snowball"), read coalescence (FuseSearch), verification compliance, consolidation capture rate, resume overhead
- [x] Feature detector: cut — expectedFeatures matching requires structured task registry not yet built
- [x] CLI: single session = detailed report, directory/glob = aggregated summary
- [x] Tests: 6 unit tests with synthetic data (verification compliance, format report, format aggregate, read coalescence)
- [x] Run against 57 existing session files. Baseline: 67% resolve, 7.3 avg steps, 70% read coalescence, 103% token growth

**Baseline metrics (57 sessions):** 67% resolve rate, 7.3 avg steps/task, 81K avg tokens, 70% read coalescence, 103% avg token/step growth, 67% consolidation capture.

---

## What Was Cut (and Why)

| Item | Original Plan | Why Cut |
|------|--------------|---------|
| `IChatClient` | Model-agnostic abstraction | Doesn't support reasoning_effort + tools together. Custom ILlmClient is better. |
| YAML WorkingMemory | Explicit state struct serialized per step | Sawtooth compression + LESSONS.md provides sufficient state management. YAML format adds parsing complexity for marginal benefit. |
| Cognitive Router | Classify steps into depth levels | Progressive deepening (escalate on failure) gives 80% of the value. Full router needs empirical data we don't have yet. |
| Separate verifier model | External 211K-param controller (CoRefine) | Prompt-level verification checklists give 60-70% of value. Training a verifier requires data we don't have. |
| Multi-model routing | Cheaper model for routine steps | Need Phase 6 data to know which steps are cheap. Premature optimization. |
| AND/OR Planning | Tree-structured plans for branching tasks | Flat plans with RETHINK/ALTERNATIVE haven't been proven insufficient. Build only if needed. |
| ReadSkeleton tool | AST-based code skeleton extraction | Interesting but unvalidated. SemanticSearch + grep_search + read_file cover the use case. |
| Automatic context compression | LLM-assisted compression at budget thresholds | Relevance-weighted heuristic compression is sufficient and free. LLM compression adds cost/latency. |
| 14 remaining tool stubs | VS Code / Jupyter tools | Require IDE integration. Not implementable in standalone MCP server. |
| LLM-based episode segmentation (5C) | Use LLM to segment sessions into episodes | Heuristic segmentation from tool patterns is cheaper, faster, and equally effective. Consolidation summary already provides the agent's own narrative. |
| .handoff.json file format (5C) | Store episodes separately | Episodes fit in existing SessionHandoff record. No new file format needed. |
| IsUnderspecifiedTask heuristic (5B) | Conditional assumption prompt section | Detection unreliable per CLAMBER/Ambig-SWE. Always-on guidance bypasses detection entirely. |

## What Was Added (Not in Original Plan)

| Item | Why Added | Research Basis |
|------|-----------|---------------|
| Progressive deepening | Escalate reasoning on consecutive failures | Kimi k1.5, Art of Scaling Test-Time Compute |
| Failure taxonomy | Typed failures with targeted recovery nudges | DeepVerifier, DEFT |
| Grounded thinking prompts | Predict outcomes before edits | Dyna-Think, Brittle ReAct |
| Per-tool verification checklists | Structured verification per tool type | DeepVerifier |
| LESSONS.md cross-session learning | Verbal self-reflection across sessions | Reflexion |
| Relevance-weighted compression | Active-file turns keep fuller context | SWE-Pruner |
| Adaptive KeepRecentTurns | Window adapts to task phase (edit vs explore) | SWE-Pruner |
| Session handoff notes | Resume interrupted sessions | CaveAgent, Aeon, AriadneMem, Auton, BAO |
| Hypothesis-driven debugging (5A) | Conditional debugging protocol with for-and-against prompting, escalating diagnostic depth | FVDebug, InspectCoder, NExT, SemCoder, A2P (14 papers) |
| Explore-then-assume (5B) | Always-on assumption guidance + sticky assumption breadcrumbs in compression + assumption-aware lessons | Ambig-SWE ICLR 2026, DS-IA, InteractComp, AwN, CLAMBER (13 papers) |
| Repository knowledge feedback (5C) | Session-derived warnings in REPO.md for tricky files | Repository Memory ICLR 2026, Steve-Evolving guardrails |
| Pivot capture during execution (5C) | Live capture of WHY agent changed approach, survives compression | Nemori, SAMULE meso-level, AriadneMem transition history |
| SSRF protection | Block private IPs in web-fetching tools | securing-mcp-tool-poisoning |
| Atomic file writes | Prevent partial-write corruption | Standard engineering practice |
| find_tools meta-tool | Progressive tool disclosure | Agent Skills Architecture, HyFunc |
| RunSubagent/SearchSubagent tools | Context-isolated delegation | AGENTSYS |
| Lesson causal attribution fix (6A) | Budget-exhaustion failures no longer blame incidental tool errors | BREW (noisy lessons degrade), A2P (counterfactual attribution) |
| Mid-session checkpoint (6B) | 50% quiet checkpoint as consolidation fallback | Steve-Evolving (subgoal anchoring), Mem-α (episodic tiers) |
| Resume re-verification optimization (6B) | grep_search instead of full re-reads for known files | SWE-Pruner (reads = 76% cost), Repository Memory |
| Regression task suite (6A) | 5 curated tasks targeting tail-case features | TestPrune (small+diverse suites) |

---

## Future Work (Post-Phase 6, Research-Informed)

Ideas that emerged from experiments and research but are explicitly deferred until Phase 6 data validates them.

| Idea | Research Basis | Prerequisite |
|------|---------------|-------------|
| Deliberate session splitting as cost strategy | HPCA 2026 (diminishing returns); observed 41% token savings | Auto-resume infrastructure + task complexity estimation |
| Hierarchical compression (Level 2) | Pensieve/StateLM (1/4 active context); observed 52% per-step cost growth at step 15 | Risk: touches sawtooth compression. Only benefits sessions >12 steps |
| Interactive user channel (`ask_user` tool) | Ambig-SWE ICLR 2026 (74% recovery from interaction) | 5B design doc has full `IUserChannel` architecture ready |
| Repository-level knowledge persistence | Repository Memory ICLR 2026, Steve-Evolving guardrails | Separate file from auto-generated REPO.md |
| Episode-aware compression | Nemori (event boundaries), SeaView (step categorization) | Bridge LlmClient and Segmenter timings |
| Hallucination tracking in EventLog | AgentHallu (11.6% detection), Tool Receipts | Per-step `hallucinated:true` field |
| Dynamic step budget (start small, extend) | EET (early termination), ReHAC (optimal intervention timing) | Budget estimation model from regression data |
| Codebase-level memory tier | Repository Memory ICLR 2026, Mem-α architecture | Distinct from lessons (cross-task) and handoffs (per-task) |

---

## Decision Log

### Checkpoint 1 (Phase 1)
```
date: 2026-03-17
tasks_run: 5 smoke tests + 3 self-improvement tasks
success_rate: 1.0 (8/8)
features_validated:
  - agent loop: working, streaming, tool execution
  - MCP integration: 26 tools implemented and tested
  - system prompt: Plan→Act→Verify with grounded thinking
  - progressive tool disclosure: find_tools meta-tool works
decisions:
  - switch from IChatClient to Responses API (reasoning_effort support)
  - cut YAML WorkingMemory (sawtooth compression is sufficient)
  - add LESSONS.md (Reflexion pattern, highest-leverage missing feature)
```

### Review Passes (Phase 2)
```
date: 2026-03-18 through 2026-03-19
review_passes: 4
issues_found: 30+
issues_fixed: 30+
tests_at_start: 93
tests_at_end: 154
key_fixes:
  - SSRF in FetchWebPage (critical security)
  - Deadlock in TestFailure (stderr never consumed)
  - Guardrails pipe-to-shell regex bypass
  - ToolExecutor not counting blocked/dup/not-found as failures
  - Path traversal via relative paths in guardrails
  - Non-atomic file writes
  - EventLog timestamp collision
features_added:
  - progressive deepening
  - failure taxonomy (6 types)
  - grounded thinking + verification checklists
  - LESSONS.md
  - relevance-weighted compression
  - adaptive KeepRecentTurns
```

### Phase 2b: Research-Driven Improvements (2026-03-19)
```
date: 2026-03-19
research_papers_surveyed: 12 (Oct 2025 – Mar 2026)
session_investigations: 3
tests_at_start: 154
tests_at_end: 193
key_improvements:
  - Dynamic verification checklist (SystemPrompt generates from tool registry)
  - Verification scaling guidance (trivial→low→medium→high risk tiers)
  - VerificationState redundancy tracker (detect build-after-test waste)
  - Improved tool-not-found error (suggests find_tools before fallback)
  - find_tools description strengthened (anti-hallucination guidance)
  - ClassifyFailure: hallucinated tools now classified as ToolMissing
confirmed_results:
  - get_errors hallucination: eliminated (A/B across 2 sessions)
  - Redundant build-after-test: eliminated (wall time -33%)
  - find_tools recovery path: working (agent follows error suggestion)
  - Tool hallucination → find_tools → fallback: 3-step recovery confirmed
research_basis:
  - "The Reasoning Trap" (arXiv:2510.22977): reasoning ↑ → tool hallucination ↑
  - FuseSearch (arXiv:2601.19568): 34.9% redundant invocation rate
  - EET (arXiv:2601.05777): experience-driven early termination → 32% cost savings
  - EGSS (arXiv:2602.05242): entropy-guided scaling → 28% token savings
  - ScaleMCP (arXiv:2505.06416): single source of truth for tool availability
  - "Internal Representations as Indicators" (arXiv:2601.05214): 86.4% hallucination detection from activations
  - AgentHallu (arXiv:2601.06818): tool-use hallucinations hardest to detect (11.6%)
  - TOOLQP (arXiv:2601.07782): iterative query planning > single-shot retrieval
  - "Tool Receipts" (arXiv:2603.10060): epistemic classification of tool claims
future_consideration:
  - Proactive tool capability summary at session start (TOOLQP-inspired)
  - Hallucination tracking in EventLog (hallucinated:true field)
  - Lesson-driven verification scaling across sessions
  - Dynamic step budget (start small, extend on demand)
```

### Phase 5: Proactive Intelligence (2026-03-21)
```
date: 2026-03-21
phases_completed: 5A (hypothesis debugging), 5B (explore-then-assume), 5C (episodic consolidation)
research_papers_surveyed: 40+ across Phases 4-5C
tests_at_start: 246
tests_at_end: 346 (includes tests added by agent during experiments)
experiments_run: 2 live scenarios against Forge's own codebase
key_implementations:
  - Conditional debugging protocol with for-and-against prompting (5A, 14 papers)
  - Always-on assumption guidance in PLAN section (5B, 13 papers)
  - Sticky breadcrumbs in compression (assumptions + pivots survive sawtooth)
  - EpisodeSegmenter: heuristic episode chain from tool-type classification
  - Pivot detection + capture during execution
  - Episode trajectory in lessons and handoff continuation prompts
  - SessionHandoff: Assumptions, PivotReasons, Episodes fields
key_experiment_findings:
  - Consolidation summary is the highest-value handoff artifact (not episodes)
  - Agent treats protocols as strategy menus, not rigid checklists
  - 100% verification compliance across both experiments
  - Re-read cost in resumed sessions: ~20% overhead (3 of 15 steps)
  - Per-step token cost grows 25% between 10-step and 20-step sessions
  - Session splitting saves ~19% tokens via context reset
  - Episode segmentation is supplementary, not primary — consolidation does the heavy lifting
  - Lesson causal attribution is wrong: blames incidental tool errors for budget exhaustion
decisions:
  - Cut Layer 3 (REPO.md feedback): auto-generated file ownership conflict
  - Cut LLM-based episode segmentation: heuristic is cheaper and consolidation summary exists
  - Cut IsUnderspecifiedTask heuristic: detection unreliable per CLAMBER/Ambig-SWE
  - Merged 5D (self-improvement tasks) into Phase 6A (regression suite)
  - Rewrote Phase 6 from generic evaluation to experiment-informed 4-priority plan
```
