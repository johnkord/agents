# Forge Design Review: Cutting-Edge Research & Recommendations

> March 2026 · Based on 106 papers in the knowledge base + recent online research
>
> This document synthesizes the latest research to evaluate Forge's current design,
> identify where the field has moved since initial design, and propose concrete
> architectural changes with alternatives.

---

## Table of Contents

1. [The Six Big Questions](#1-the-six-big-questions)
2. [Where Forge Is Right](#2-where-forge-is-right)
3. [Where Forge Should Change](#3-where-forge-should-change)
4. [Deep Dive: The Verification Architecture](#4-deep-dive-the-verification-architecture)
5. [Deep Dive: Context Management Strategy](#5-deep-dive-context-management-strategy)
6. [Deep Dive: Reasoning Budget Allocation](#6-deep-dive-reasoning-budget-allocation)
7. [The Subagent Question](#7-the-subagent-question)
8. [Deep Dive: Session Resumption & Long-Running Tasks](#8-deep-dive-session-resumption--long-running-tasks)
9. [Ranked Recommendations](#9-ranked-recommendations)
10. [What Not to Do](#10-what-not-to-do)

---

## 1. The Six Big Questions

The last 6 months of research (Oct 2025 – Mar 2026) have crystallized around six questions that every coding agent must answer. These aren't academic — they directly determine Forge's architecture.

### Q1: Should the model manage its own context, or should external middleware do it?

**The Pensieve camp**: Train the LLM to use memory tools (deleteContext, readChunk, updateNote) and let it be its own context engineer. StateLM-14B achieves 83.9% on needle-in-a-haystack at 2M tokens vs 1.7% for vanilla Qwen3-14B.

**The SWE-Pruner camp**: A 0.6B-parameter neural skimmer prunes 23-54% of tokens while *improving* success rates by 1.2-1.4 points. Keep the pruning external — the LLM should focus on reasoning, not housekeeping.

**The tension**: This is the control theory question — does the pilot fly the plane, or does autopilot? Internal management is more adaptive (the model knows what it needs) but requires model-specific training we can't do. External management is model-agnostic but can't adapt to the model's evolving understanding.

**Verdict for Forge**: **External middleware is correct for now**. We use a frontier model (GPT-5.4) we can't fine-tune, so Pensieve-style internalized context management is out of reach. Forge's existing sawtooth compression in OpenAIResponsesLlmClient is architecturally sound. But we should make it smarter — see §5.

### Q2: Where should verification live? Inside generation, external loop, or confidence-based?

Three competing approaches emerged in early 2026:

| Approach | Paper | How it works | Cost |
|----------|-------|-------------|------|
| **Internalized** | SPOC (2025) | Train model to interleave solution + self-check in one pass | Zero marginal cost, requires RL training |
| **External rubric** | DeepVerifier (2026) | Decompose verification into sub-questions per failure taxonomy | 8-11% accuracy gain, ~2x token cost |
| **Confidence signal** | CoRefine (2026) | 211K-param controller reads confidence traces → HALT/RETHINK/ALT | 92.6% precision, near-zero overhead |

**The synthesis**: These aren't mutually exclusive. The most effective system would use confidence-based routing (cheap: should we verify at all?) → rubric decomposition (focused: verify the specific risk). SPOC-style internalization requires RL training we can't do.

**Verdict for Forge**: Forge's current Plan→Act→Verify prompt is the right *shape* but it's purely verbal — the model is just asked to verify, with no structural support. We should add **structured verification checkpoints** that decompose verification into specific sub-questions for each tool call type. See §4.

### Q3: Is reasoning-trace thinking actually valuable?

**Brittle ReAct (2024)**: ReAct's performance comes from exemplar similarity, not the thinking traces themselves. Placebo reasoning outperforms real reasoning.

**Dyna-Think (2025)**: Thinking IS valuable — but only when it's *action-relevant simulation* ("what will happen if I run this edit?"), not unconstrained chain-of-thought.

**The reconciliation**: Generic "let me think about this" is worthless. Specific "let me predict what this edit will do to the test output" is valuable. The distinction is *grounded* vs *ungrounded* thinking.

**Verdict for Forge**: Forge's system prompt should steer thinking toward **action prediction** rather than generic planning. Instead of "What files are involved? What is the root cause?", prompt for "What will this specific edit change in the test output? What could go wrong?" This is a cheap, high-leverage prompt change.

### Q4: How fast is AI capability growth, and what should we build for?

**"Measuring AI Ability to Complete Long Software Tasks" (2025, NeurIPS)**: Frontier AI 50% time horizon is ~50 minutes and doubles every 7 months. The increase is primarily driven by **greater reliability and ability to adapt to mistakes**, not raw capability.

**SWE-RL (2025, NeurIPS)**: RL on software evolution data gives 41% on SWE-bench Verified from a 70B model. RL-based reasoning generalizes to 5 out-of-domain tasks.

**Implication**: Reliability engineering matters more than capability engineering. The model is already smart enough for most tasks — it's the *recovery from mistakes* that limits performance. This validates Forge's consecutive-failure nudging and Plan→Act→Verify cycle, but suggests we should invest more in error recovery patterns.

### Q5: Single agent or multi-agent?

**AgentArk (2026)**: Multi-agent debate dynamics can be distilled into a single model's weights. Multi-agent systems may be a training technique, not a deployment architecture.

**Forge's RunSubagentTool**: We already have process-level subagent isolation, inspired by AGENTSYS's context isolation (97% attack reduction). The security value of isolation is separate from the reasoning value of multi-agent debate.

**Verdict**: Forge is correctly single-agent for reasoning, multi-process for isolation. The RunSubagent tool should be framed as a *context isolation boundary*, not a "smarter reasoning engine." Don't add debate/critic agents — the research says this can be internalized into the model's reasoning.

### Q6: Does architecture need to be model-dependent?

**Structured Context Engineering (2026, 9,649 experiments)**: File-native context (agents reading via tools) improves frontier models by +2.7% but *hurts* open-source models by -7.7%. The 21-point model gap dwarfs all architecture choices.

**Forge's position**: We use GPT-5.4 (frontier) and have already committed to file-native context retrieval. This is correct for our model tier. But it means Forge's design is **not portable** to weaker models without significant changes.

**Implication**: If we ever want to support local models (Llama, Qwen), we'll need a model-capability-aware configuration layer that adjusts context strategy. This is a future concern, not an immediate one.

---

## 2. Where Forge Is Right

These design decisions are validated by recent research:

| Design Choice | Research Validation |
|---|---|
| **Sawtooth context compression** | Pensieve validates the pattern; SWE-Pruner confirms 76% of tokens are reads |
| **Plan→Act→Verify prompt** | DeepVerifier, SPOC, CoRefine all confirm verification is critical |
| **Progressive tool disclosure** | Agent Skills Architecture confirms "beyond critical library size, selection degrades sharply" |
| **Single-agent core** | AgentArk shows multi-agent can be distilled into single |
| **Process-level subagent isolation** | AGENTSYS shows 97% attack reduction from context isolation |
| **MCP for tool interface** | Protocol surveys confirm MCP+A2A as convergence point |
| **Event log / JSONL sessions** | 12-Factor Agents: "store the event log, let the model pick up from any message" |
| **Observation pipeline (truncation)** | SWE-Pruner: read operations are 76.1% of token budget |
| **File-native context for frontier model** | Structured CE: +2.7% for frontier models |

---

## 3. Where Forge Should Change

### 3.1 Verification is too weak (verbal only)

**Current**: The system prompt says "verify your changes" but provides no structural support. The model decides if it verified enough.

**Research says**: DeepVerifier shows 8-11% accuracy gain from structured rubric-based verification. CoRefine shows 92.6% halt precision from a tiny external controller.

**Recommendation**: Add a **verification checkpoint** after each edit-type tool call that asks specific sub-questions:

```
After replace_string_in_file:
  1. Did the edit apply to the correct location? (read_file to check)
  2. Does the file still parse/compile? (get_errors)
  3. Do relevant tests still pass? (run_tests)
```

This is the "decompose verification into sub-questions" pattern from DeepVerifier, adapted for coding.

### 3.2 Thinking is ungrounded

**Current**: System prompt asks "What files are involved? What is the root cause?" — generic planning.

**Research says**: Brittle ReAct shows generic reasoning traces are decorative. Dyna-Think shows *action-relevant simulation* is valuable.

**Recommendation**: Change the PLAN phase from generic questions to **predicted-outcome prompts**:

```
Before each edit:
  "Predict: what will this change affect? What test case will this fix/break?"

After each edit:
  "Was the prediction correct? If not, what did you miss?"
```

This forces grounded thinking — the model must reason about *specific consequences*, not abstract plans.

### 3.3 No failure taxonomy

**Current**: All errors are treated the same. A blocked tool call, a test failure, and a wrong file edit all produce the same consecutive-failure counter.

**Research says**: DeepVerifier's failure taxonomy (5 major, 13 sub-categories) and DEFT's 14 failure modes show that categorizing failures enables targeted recovery.

**Recommendation**: Build a **coding-specific failure taxonomy** and use it to guide recovery:

| Failure Type | Signal | Recovery Strategy |
|---|---|---|
| **Wrong file** | Edit applied but test unchanged | Re-read test, trace imports |
| **Stale context** | Edit conflicts with compressed history | Re-read the target file fresh |
| **Test misread** | Fix doesn't match the actual error | Re-run test, read full output |
| **Syntax error** | get_errors returns compile error | Undo edit, try again |
| **Logic error** | Tests pass but wrong behavior | Read existing test assertions |
| **Scope creep** | >3 files modified without plan | Step back, re-plan |

### 3.4 Compression loses too much

**Current**: Sawtooth compression summarizes older turns into a developer message with 200-char result previews.

**Research says**: SWE-Pruner demonstrates goal-driven pruning — filter by intent, not just age. Static summaries lose task-relevant information that a goal-aware pruner would keep.

**Recommendation**: Make compression **goal-aware**: before compressing, the system should evaluate each older turn's relevance to the *current subtask* (not just its age). A turn that read a file the agent is about to edit should survive compression longer than a turn that explored an unrelated directory.

**Implementation**: Add a "relevance heuristic" to `CompressTurns()`: if a tool call's file path matches any file in the last 3 turns' tool calls, keep full detail. Otherwise, compress to summary.

### 3.5 No learning across sessions

**Current**: Each session starts fresh. The self-improvement plan describes Reflexion-style `LESSONS.md` but it's not implemented.

**Research says**: SWE-RL (NeurIPS 2025) demonstrates that RL on software evolution data generalizes to out-of-domain tasks. Even without RL, Reflexion (2023) showed 91% on HumanEval without weight updates — purely through verbal self-reflection.

**Recommendation**: Implement the `LESSONS.md` mechanism from the self-improvement plan. This is the single highest-leverage feature not yet built. After each session:

1. If the session failed or used >300K tokens: generate a 2-sentence lesson
2. Append to `/memories/repo/lessons.md` via MemoryTool
3. Inject top-N lessons into context at session start (system prompt or first user message)

This creates a feedback loop that improves Forge *through use*, not just through code changes.

---

## 4. Deep Dive: The Verification Architecture

### The Problem

Forge's current verification is a prompt instruction: "After each change, verify it worked." This is the weakest possible implementation of a concept that multiple papers show is the highest-leverage architectural component.

### Three Options

#### Option A: Prompt-Level Verification Rubrics (Low effort)

Add verification checklists to the system prompt, keyed by tool type:

```
After replace_string_in_file → read_file the changed region + run_tests
After create_file → list_directory to confirm + get_errors if code
After run_bash_command → check exit code, if non-zero read stderr
```

**Pros**: Zero code change, immediate value, forces the model to take specific actions.  
**Cons**: Model compliance is probabilistic; no enforcement mechanism.

#### Option B: Verification Injection in AgentLoop (Medium effort)

After each tool execution step that modifies files, automatically inject a verification user message:

```csharp
if (toolExecution.ToolCallRecords.Any(r => r.ToolName is "replace_string_in_file" or "create_file"))
{
    llmClient.AddUserMessage("VERIFY: Read back the modified file(s) and run tests before proceeding.");
}
```

**Pros**: Guaranteed verification prompt, no model-dependent compliance.  
**Cons**: Adds a full LLM round-trip per modification; ~30% token cost increase.

#### Option C: Structured Verification Phase with Failure Detection (High effort)

Separate the verify phase into its own mini-loop that runs up to 2 verification checks before releasing control back to the planning phase. Track a typed `VerificationResult` enum (Passed / FailedSyntax / FailedTest / FailedLogic / Inconclusive) that feeds into the failure taxonomy.

**Pros**: Most robust; enables typed recovery strategies per failure kind.  
**Cons**: Significant code complexity; may over-constrain the agent on simple tasks.

### Recommendation

**Start with Option A**, then graduate to Option B for file-modifying tools only. Option C is overengineered for current maturity — the consecutive-failure nudge is sufficient for recovery orchestration, it just needs typed failures feeding into it.

---

## 5. Deep Dive: Context Management Strategy

### The Current Sawtooth

Forge's `OpenAIResponsesLlmClient` implements a clean sawtooth: keep last 8 turns in full, compress older turns into summaries with 200-char result previews. This is fundamentally the right pattern.

### What the Research Says Should Improve

**SWE-Pruner** (2026): 76% of tokens come from read operations. Goal-driven pruning that filters based on the agent's current intent prunes 23-54% while improving accuracy.

**Pensieve** (2026): The model should have explicit `deleteContext` capability — the ability to say "I no longer need turn X."

**Active Context Compression** (2026): 22.7% token savings with 0% accuracy loss using compression that preserves *information density* not just recency.

### Three Options

#### Option A: Relevance-Weighted Compression (Medium effort)

Modify `CompressTurns()` to score each being-compressed turn by file-path overlap with recent turns. Turns involving files still "active" get full result previews (500 chars). Others get minimal summaries (tool name + args only, no results).

```
If turn.ToolCalls intersect currentActiveFiles → keep 500-char preview
Else → compress to "→ grep_search(query='auth', rootPath='...')" only
```

**Pros**: Simple heuristic, no model calls, preserves task-relevant context.  
**Cons**: Heuristic may be wrong; file path isn't the only relevance signal.

#### Option B: LLM-Assisted Compression (High effort, high cost)

Before each sawtooth compression, ask a lightweight model (or the same model with low reasoning-effort) to select which older turns to keep, summarize, or discard.

**Pros**: Semantically accurate pruning.  
**Cons**: Adds latency and cost (extra LLM call per compression cycle).

#### Option C: KeepRecentTurns as Adaptive (Low effort)

Instead of fixed KeepRecentTurns=8, adapt based on task phase:
- During exploration (many read_file/grep): KeepRecentTurns=6 (compress faster, most reads are one-shot)
- During editing (replace_string_in_file, run_tests): KeepRecentTurns=12 (keep edit context longer)

Detect phase by looking at the tool call distribution in the last 3 turns.

**Pros**: Trivial to implement, addresses the key insight that edit phases need more context than exploration phases.  
**Cons**: Coarse-grained; phase detection is imperfect.

### Recommendation

**Implement Option A + C together**. Relevance-weighted compression is the biggest win (reclaiming space wasted on one-shot exploratory reads) and adaptive KeepRecentTurns aligns budget with need. Skip LLM-assisted compression — the cost doesn't justify the gain at our scale.

---

## 6. Deep Dive: Reasoning Budget Allocation

### The Insight

Forge already supports `ReasoningEffort: Medium` as a global setting. But research suggests reasoning effort should be **per-step**, not per-session.

**Dyna-Think** (2025): Not all thinking tokens are valuable. Action-relevant simulation outperforms ungrounded reasoning.

**Kimi k1.5** (2025): Long-CoT techniques can improve short-CoT models. The optimal strategy is to think deeply on hard steps and cheaply on easy steps.

**Art of Scaling Test-Time Compute** (2025): Adaptive compute allocation outperforms uniform allocation by 4-8x efficiency.

### Options

#### Option A: Cognitive Router (from design.md, not implemented)

Classify each step into depth levels (Instinctive → Strategic) and adjust reasoning effort per call. The design doc proposed this but it was never built.

**Pros**: Research-validated concept; directly supported by the Responses API's per-request reasoning_effort.  
**Cons**: Classification accuracy matters a lot; a bad router wastes more than it saves.

#### Option B: Progressive Deepening

Start every task with Medium reasoning. If the agent fails 2+ times on the same subtask, automatically escalate to High for the retry.

**Pros**: Dead simple, no classification needed, spends budget where it's needed.  
**Cons**: High effort only kicks in on failures, never on genuinely hard first attempts.

#### Option C: Step-Type Heuristic

Map tool patterns to reasoning levels:
- Exploration (list_directory, grep_search, read_file) → Low
- Editing (replace_string_in_file) → Medium
- Debugging (consecutive failures, test analysis) → High
- Planning (no tool calls, just thinking) → Medium

**Pros**: No model calls for routing, covers >80% of cases correctly.  
**Cons**: Misses edge cases; exploration sometimes requires deep reasoning.

### Recommendation

**Implement Option B (progressive deepening)**, then evaluate if adding Option C's heuristics helps. Progressive deepening is essentially free — it's just `if (consecutiveFailures >= 2) options.ReasoningEffort = High` in the agent loop. The returns from Option A (full cognitive router) aren't justified until we have empirical data on where reasoning effort actually matters in Forge's workload.

---

## 7. The Subagent Question

### Current State

Forge has `RunSubagentTool` (process-level isolation) and `SearchSubagentTool` (local grep-based search). The question: should we invest more here?

### What Research Says

**AGENTSYS (2026)**: Context isolation is the single most impactful security measure (2.19% ASR with isolation alone vs 30.66% baseline). This validates RunSubagent.

**Choose Your Agent (2026)**: Delegation creates positive externalities. But the delegation must be structured — the delegator must provide clear intent, not just "figure it out."

**AgentArk (2026)**: Multi-agent debate at inference time is wasteful. Single-model distillation captures the same reasoning quality.

### Recommendation

**Don't invest more in subagent infrastructure**. The current RunSubagent + SearchSubagent are sufficient. The research says:

1. Single-agent reasoning (with good context management) matches multi-agent systems
2. Subagent value is primarily *context isolation* (security), not *reasoning quality*
3. The investment of adding debate/critic agents is not justified by the marginal gains

Instead, invest that effort in the verification architecture (§4) which provides the same "second opinion" functionality but within the single-agent loop.

---

## 8. Deep Dive: Session Resumption & Long-Running Tasks

### The Problem

Forge currently treats every session as a fresh start. The agent records a complete JSONL event log — every step, tool call, token count, and result — but cannot reload that state to continue an interrupted session. With AI time horizons doubling every 7 months (NeurIPS 2025), tasks are growing beyond what fits in a single session. A 30-step limit, a token budget exhaustion, a network timeout, or a user interrupt all mean starting over.

This is not just an engineering convenience issue — it's an architectural gap that limits the class of tasks Forge can handle.

### What Nine Papers Say (Synthesized)

This is one of the richest areas in the 2025-2026 literature. Nine papers address different facets of session persistence, and their approaches reveal deep tensions about what "state" even means for an agent.

#### The Fidelity Spectrum

The papers arrange along a spectrum from *high-fidelity state preservation* to *abstract insight consolidation*:

| Paper | State Representation | Fidelity | Resume Mechanism |
|-------|---------------------|----------|-----------------|
| **CaveAgent** | Native Python objects (DataFrames, models) | Maximum — zero serialization loss | Runtime reload |
| **Aeon** | WAL + mmap blobs with CRC32 checksums | Near-exact — byte-level durability | Log replay with checksum validation |
| **AriadneMem** | Temporal edges in memory graph | Structural — preserves state *transitions* | Graph traversal with conflict resolution |
| **AgentOS** | Semantic slices with hash-indexed pages | Selective — relevance-filtered snapshots | Semantic page table lookup |
| **Auton** | Consolidated insights (3-tier: episodic/semantic/procedural) | Abstract — distilled lessons only | Reflector-driven reconstruction |

**The core tension**: CaveAgent argues that objects lose fidelity when serialized to text ("textualization bottleneck"). Auton argues the opposite — raw state is *too costly* to preserve; what matters is the *insight extracted from it*. Both are right for different domains.

**For Forge**: The filesystem provides CaveAgent-like fidelity for free. Edited files, git diffs, test outputs — these survive sessions without explicit serialization. What Forge *lacks* is Auton-style insight consolidation: the ability to distill "I learned X from exploring Y" into a reusable form.

#### Memory Consolidation: The Session Boundary Mechanism

Three papers independently converge on the same insight: *the session boundary is not a pause — it's a consolidation opportunity*.

**Auton's Reflector** partitions raw event streams into coherent episodes, extracts salient insights, and discards low-utility content. The formal objective: maximize mutual information between compressed memory and future tasks, subject to a storage constraint. The Reflector doesn't just save state — it *upgrades* it from raw experience to reusable knowledge.

**AriadneMem's Conflict-Aware Coarsening** handles the tricky case where state *evolved* during a session. When encountering seemingly contradictory facts ("meeting at 2pm" vs "meeting rescheduled to 3pm"), it creates directed temporal edges to preserve the transition history. This matters for coding agents: if the agent tried approach A, failed, and switched to approach B, the handoff note must encode the *transition*, not just the final state. Otherwise the resuming session might retry approach A.

**Aeon's Generational GC** demonstrates that consolidation can happen continuously with near-zero overhead (<1% latency, <10µs freeze). Double-buffered shadow compaction keeps the main processing path clear while a background thread handles the expensive I/O.

**For Forge**: Session handoff should not just capture "where things are" but "how we got here and what we learned along the way." This is the difference between a snapshot (`filesModified: ["LoginService.cs"]`) and a narrative (`tried == comparison fix → 3/5 tests pass → mock setup is the remaining problem → approach A for TokenExpiry is wrong because...`).

#### State Transition Encoding vs Flat Snapshots

**AriadneMem** introduces a powerful concept: *state transitions encoded as directed edges*. Instead of overwriting "the current plan" when it changes, preserve both the old and new versions with a temporal link: `plan_v1 → plan_v2 (reason: test failure revealed wrong assumption)`.

This is critical for coding agents because:
- The agent might resume and re-encounter the same dead-end approach A
- Without transition history, it has no reason to prefer approach B
- With transition edges, the handoff note says "I tried A, it failed because X, so I switched to B"

**AgentOS** takes a different approach: forget selectively via semantic paging. When a reasoning thread shifts focus, irrelevant slices are "swapped out" to L2 storage, keeping only core logical anchors in L1. On resume, the Semantic Page Table (SPT) identifies which slices to reload based on the next task's semantic signature.

**Tension**: AriadneMem says "preserve everything with temporal links." AgentOS says "forget actively and reload selectively." Both work because they optimize for different scenarios: AriadneMem for *understanding how you got here* vs AgentOS for *efficiency of the next step*.

**For Forge**: We need both. The handoff note should preserve transition history (AriadneMem pattern) for the narrative, but the *context injection* on resume should be selective (AgentOS pattern) — only load what's relevant to the next step.

#### The Proactive Anticipation Pattern

**BAO (2026)** introduces a concept most papers miss: *proactive* session management. Instead of reactively handling session boundaries when they occur (timeout, token limit, max steps), the agent should *anticipate* them and prepare gracefully.

BAO's "Dynamic Scheduling" adapts strategy based on remaining interaction budget. Applied to session resumption: when the agent detects it's approaching a limit (e.g., 80% of MaxTotalTokens), it should shift from "keep working" to "prepare for continuation":

1. Consolidate current understanding into a structured handoff note
2. Commit modified files
3. Write explicit next-steps

**AMemGym** adds a critical evaluation insight: memory systems must be tested *on-policy* — with the agent that will actually use them. Off-policy evaluation (testing handoff notes on a different model or in isolation) produces misleading results because "write and read failures consistently increase over longer interactions." The implication: our handoff format must be validated by actually resuming sessions, not just by unit-testing the serialization.

#### Multi-Session Learning: Three Levels

**Auton's Self-Evolution Framework** proposes three levels of cross-session improvement, each with different time horizons:

| Level | Mechanism | What Improves | Cost |
|-------|-----------|---------------|------|
| **Level 1: In-Context** | Inject retrieved lessons (LESSONS.md) | Behavior via prompt | Zero (no training) |
| **Level 2: STaR** | Fine-tune on successful trajectories | Reasoning patterns | Medium (SFT) |
| **Level 3: RL** | GRPO/PPO for novel strategies | Strategy discovery | High (RL training) |

**For Forge**: Level 1 is already implemented (LESSONS.md). Level 2 is out of reach without fine-tuning access. But there's a creative hybrid: *use the event log as training data for prompt optimization*. Analyze past successful sessions to extract the prompting patterns that worked, and feed those back into the system prompt. This is Level 1.5 — not fine-tuning, but empirical prompt refinement.

### The Updated Design: Five Approaches, Reconsidered

The original analysis proposed three approaches (Faithful Replay, Summarized Handoff, Hybrid). With the deeper research, the design space is actually richer:

#### Approach A: Faithful Replay
Re-inject the full conversation history from the event log.

**Updated assessment**: Even worse than originally concluded. AMemGym shows "write and read failures consistently increase over longer interactions" — replaying a 15-step history amplifies errors. AriadneMem's temporal edges are not captured in flat replay. And Aeon shows that crash recovery works via *log compaction*, not raw replay.

**Verdict**: ❌ Reject.

#### Approach B: Summarized Handoff
Generate a compact narrative summary and inject it as context.

**Updated assessment**: Still valid, but insufficient alone. Auton's Reflector shows that effective consolidation requires *structured extraction* (episodic segmentation → insight extraction → compression), not just free-form summarization. A flat summary loses the transition history that AriadneMem shows is critical for avoiding repeated mistakes.

**Verdict**: ⚠️ Necessary foundation but not sufficient.

#### Approach C: Hybrid (Summary + Artifacts)
Structured summary plus key file-level artifacts.

**Updated assessment**: Good, and this is what we should build first. But it should be augmented with two patterns from the research:

1. **Transition history** (AriadneMem): Include failed approaches and *why* they failed, not just the current state
2. **Proactive preparation** (BAO): The agent should write the handoff note *before* hitting the limit, not after

**Verdict**: ✅ Recommended for Phase 1.

#### Approach D: Episodic Consolidation (New)
Inspired by Auton's Reflector: partition the session into episodes, extract insights per episode, and store them as retrievable episodic memories rather than a single flat summary.

```
Episode 1: Exploration (steps 0-4)
  Insight: Authentication logic is in src/Auth/, tests in tests/Auth/
  Key files: LoginService.cs, AuthController.cs, AuthControllerTests.cs

Episode 2: Bug Localization (steps 5-8)
  Insight: Bug is == comparison in LoginService.cs:47
  Failed approach: tried string.Equals first but the issue is timing-safe comparison
  Resolution: Use CryptographicOperations.FixedTimeEquals

Episode 3: Fix + Partial Verification (steps 9-12)
  Insight: Fix applied, 3/5 tests pass. 2 failures are mock-related, not logic.
  Remaining: AuthControllerTests.TokenExpiry needs mock for FixedTimeEquals
```

**Pros**: Preserves transition history. Segmented format is more retrievable than a monolithic summary. Maps naturally to how developers think about work sessions. Auton shows this format maximizes mutual information with future tasks.

**Cons**: Generating well-segmented episodes requires either LLM assistance (token cost) or heuristic rules (fragile). More complex than a flat summary.

**Verdict**: ✅ Recommended for Phase 2 — build after flat handoff notes prove the concept.

#### Approach E: Semantic Paging (New, Deferred)
Inspired by AgentOS: maintain a persistent "semantic page table" across sessions, where each page is a context slice (file contents, test results, plan fragments) indexed by semantic hash. On resume, the agent requests specific pages by semantic query rather than loading everything.

This is the most ambitious approach and the most aligned with where the field is heading (AgentOS, Pensieve/StateLM). It requires building persistent infrastructure (a local vector store or hash-indexed file store) that outlives individual sessions.

**Verdict**: 🔮 Deferred to Phase 3+. Right concept, wrong time.

### Updated Design: Session Handoff Notes v2

The handoff note format, updated with transition history and episodic structure:

```json
{
  "event": "session_handoff",
  "ts": "2026-03-19T14:30:00Z",
  "data": {
    "task": "Fix the authentication bug in LoginService.cs",
    "status": "incomplete",
    "stepsCompleted": 12,
    "stepsTotal": 30,
    "tokensUsed": 250000,
    "episodes": [
      {
        "label": "Exploration",
        "steps": "0-4",
        "insight": "Auth logic in src/Auth/, tests in tests/Auth/. Key files: LoginService.cs, AuthControllerTests.cs"
      },
      {
        "label": "Bug Localization",
        "steps": "5-8",
        "insight": "Bug is == comparison at LoginService.cs:47. REJECTED: string.Equals (wrong — issue is timing, not comparison semantics)"
      },
      {
        "label": "Fix + Partial Verification",
        "steps": "9-12",
        "insight": "Applied FixedTimeEquals fix. 3/5 tests pass. 2 mock failures remain — not logic issues"
      }
    ],
    "failedApproaches": [
      "Tried string.Equals at step 6 — wrong because the issue is timing-safe comparison, not string equality"
    ],
    "filesModified": ["src/Auth/LoginService.cs"],
    "lastTestOutput": "Passed: 3, Failed: 2 — AuthControllerTests.TokenExpiry, AuthControllerTests.RefreshFlow",
    "nextSteps": ["Fix mock setup in AuthControllerTests.cs for FixedTimeEquals", "Re-run full test suite"],
    "gitCommit": "a1b2c3d"
  }
}
```

### Proactive Session Boundary Detection

Inspired by BAO's Dynamic Scheduling — the agent should prepare for session end before it's forced:

```
When (tokensUsed > 0.8 * MaxTotalTokens) OR (stepNum > 0.8 * MaxSteps):
  If task is incomplete:
    1. Inject a developer message: "Approaching session limit. Consolidate your progress."
    2. The agent's next response should include:
       - Summary of what's done and what remains
       - Explicit next steps for continuation
    3. Generate the handoff note from the agent's consolidation response
```

This is meaningfully different from just writing a handoff note when the hard limit hits. By prompting the agent to consolidate *proactively*, we get a higher-quality summary (the agent can reflect while still having context) vs. a mechanical extraction from the event log after the fact.

### Why Not Full Replay (Reinforced)

The deeper research reinforces the rejection of full replay even more strongly:

1. **The sawtooth pattern exists because monotonic growth doesn't work.** Replaying 15 steps of history recreates the exact problem the sawtooth was designed to prevent.
2. **Tool results go stale.** A `read_file` result from 20 minutes ago may not reflect the current file. Replaying stale results is actively harmful.
3. **AMemGym shows failures accumulate.** Write/read failures increase over interaction length. Longer replayed histories = more failure surface.
4. **AriadneMem shows transitions matter.** Flat replay doesn't encode *why* approach A was abandoned — the model might retry it.
5. **Auton's Reflector shows consolidation is better.** Distilling episodes into insights gives the model more usable context per token than raw history.

### Implementation Phases (Updated)

**Phase 1 — Handoff Notes (Low effort)**
- On session end (success, failure, limit): generate structured handoff note in the JSONL event log
- On proactive limit detection: inject consolidation prompt before hard limit
- Add `--resume <session-file>` flag that loads the handoff note as context
- Handoff note generated heuristically from event log (no LLM call)

**Phase 2 — Episodic Consolidation (Medium effort)**
- Use the LLM (with Low reasoning effort) to segment the session into episodes and extract insights
- Include transition history: failed approaches with reasons
- Store as a separate `.handoff.json` alongside the session JSONL
- LESSONS.md integration: auto-extract cross-session lessons from episodes

**Phase 3 — Multi-Session Chaining (High effort)**
- Task decomposition: the agent breaks large tasks into subtasks before starting
- Each subtask runs as a separate session with handoff notes linking them
- A "planner" session generates the decomposition; "worker" sessions execute
- This is the "agent that plans its own sessions" pattern — Auton's hierarchical Level 3

**Phase 4 — Semantic Persistence (Deferred)**
- Persistent semantic page table across sessions (AgentOS pattern)
- Local hash-indexed store for context slices
- Resume by semantic query, not session ID
- Requires significant infrastructure investment; defer until Phases 1-3 are validated

---

## 9. Ranked Recommendations

Ordered by expected impact per implementation effort:

| # | Recommendation | Effort | Impact | Section |
|---|---|---|---|---|
| 1 | **Implement LESSONS.md session memory** | Low | High | §3.5 |
| 2 | **Add verification checklists to system prompt** | Low | High | §4, Option A |
| 3 | **Progressive deepening (reasoning escalation on failure)** | Low | Medium | §6, Option B |
| 4 | **Grounded thinking prompts (predict outcomes)** | Low | Medium | §3.2 |
| 5 | **Relevance-weighted compression** | Medium | High | §5, Option A |
| 6 | **Adaptive KeepRecentTurns** | Low | Medium | §5, Option C |
| 7 | **Failure taxonomy with typed recovery** | Medium | Medium | §3.3 |
| 8 | **Verification injection for file edits** | Medium | Medium | §4, Option B |
| 9 | **Session handoff notes (resumption Phase 1)** | Medium | High | §8, Phase 1 |
| 10 | **REPO.md auto-generation** (from design.md) | High | High | Already planned |
| 11 | **Cognitive router** | High | Medium | §6, Option A |

### The "Do These First" Package

Items 1-4 are all low-effort, high-impact changes that can be implemented in a single session:

- **LESSONS.md**: Add 10 lines to AgentLoop.Finish() to generate and store a lesson on failure
- **Verification checklists**: Add 15 lines to SystemPrompt.Build() with per-tool-type verification steps
- **Progressive deepening**: Add 3 lines to AgentLoop where consecutiveFailures increments
- **Grounded thinking**: Rewrite 5 lines in the system prompt's PLAN section

Total estimated code change: ~40 lines. Expected impact: meaningful improvement in error recovery and task success rate based on the research findings.

---

## 10. What Not to Do

Research also reveals several tempting ideas that Forge should explicitly reject:

### ❌ Don't build a separate verifier model
CoRefine and DeepVerifier use external verifiers that are effective but require training data we don't have. Prompt-level verification rubrics give 60-70% of the value at 0% of the cost.

### ❌ Don't add multi-agent debate/critique
AgentArk shows the reasoning quality of multi-agent debate can be captured in a single model. Adding critic agents would increase complexity, latency, and cost without proportional benefit.

### ❌ Don't optimize context format (YAML vs JSON vs Markdown)
Structured Context Engineering's 9,649 experiments show format has "no significant aggregate effect" on frontier models. The 21-point model gap dominates. Don't waste time on this.

### ❌ Don't implement world-model simulation
Dyna-Think requires model-specific training to be effective. Without it, predicting next states is just ungrounded speculation that wastes tokens. Use grounded thinking prompts instead.

### ❌ Don't chase SWE-bench scores
SWE-bench measures single-issue resolution. Forge's value proposition is being a reliable pair programmer across tasks. Optimize for reliability, error recovery, and session-over-session improvement via LESSONS.md.

### ❌ Don't make the architecture model-dependent yet
Structured CE shows architecture should adapt to model capability, but Forge currently targets one frontier model (GPT-5.4). Building a capability-adaptive layer is premature optimization. Revisit when supporting multiple models.

---

## Appendix: Papers Referenced

| Short Name | Full Title | Year | Key Finding |
|---|---|---|---|
| Dyna-Think | Dyna-Think: World Model Simulation | 2025 | Action-relevant thinking valuable; generic CoT isn't |
| SPOC | Spontaneous Self-Correction | 2025 | Single model can interleave generation + verification |
| CoRefine | Confidence-Guided Self-Refinement | 2026 | 211K-param controller achieves 92.6% halt precision |
| daVinci-Dev | Agent-Native Mid-Training | 2026 | Mid-training on agent trajectories > post-training |
| SWE-Pruner | Context Pruning for Code Agents | 2026 | Goal-driven pruning improves accuracy while cutting tokens |
| Structured CE | Structured Context Engineering | 2026 | Architecture decisions are model-tier dependent |
| Brittle ReAct | Brittle ReAct Prompting | 2024 | ReAct works via exemplar similarity, not reasoning |
| AgentArk | Distilling Multi-Agent to Single | 2026 | Multi-agent can be collapsed into single model |
| Pensieve/StateLM | Stateful Context Management | 2026 | Model can learn to be its own context engineer |
| DeepVerifier | Rubric-Guided Verification | 2026 | Structured verification gives 8-11% accuracy gain |
| SWE-RL | RL on Software Evolution | 2025 | RL on SE data generalizes; 41% SWE-bench Verified |
| Long Task Measure | Measuring AI on Long Tasks | 2025 | 50% time horizon ~50min, doubling every 7 months |
| ReasonFlux | Hierarchical Reasoning Templates | 2025 | Thought templates > flat CoT for structured reasoning |
| Kimi k1.5 | Scaling RL with LLMs | 2025 | Long-CoT improves short-CoT; simple RL > complex methods |
| Cognition Eng. | Test Time Scaling Drives Cognition | 2025 | Shift from prompt engineering to cognition engineering |
| CaveAgent | Stateful Runtime for Agents | 2026 | Persistent runtime state: +10.5% success, -28.4% tokens |
| Aeon | Memory Management with WAL | 2026 | Write-ahead log + episodic trace graph for crash recovery |
| AgentOS | OS-Inspired Agent Architecture | 2026 | Contextual Checkpoints + Semantic Paging for state snapshots |
| Alignment in Time | Long-Horizon Agent Alignment | 2026 | Recovery from intermediate failures is a primary quality dimension |
| SeaView | SWE Agent Visual Interface | 2025 | Trajectory visualization for debugging agent sessions |
| AriadneMem | Lifelong Memory Threading | 2026 | Conflict-aware temporal edges preserve state transition history across sessions |
| Auton | Declarative Agent Framework | 2026 | 3-level self-evolution: in-context lessons → SFT on trajectories → RL for novel strategies |
| AMemGym | Long-Horizon Memory Benchmark | 2026 | On-policy evaluation diverges from off-policy; write/read failures increase with interaction length |
| BAO | Proactive Agentic Optimization | 2026 | Proactive scheduling: anticipate session boundaries, don't just react to them |
| Anatomy of Memory | Empirical Analysis of Agent Memory | 2026 | Episodic consolidation and hierarchical persistence outperform flat storage |
