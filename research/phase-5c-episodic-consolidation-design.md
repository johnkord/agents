# Phase 5C: Episodic Consolidation — Design Document

## Problem Statement

Forge's session handoff system extracts structured facts from raw session data: files modified, failed approaches, test output, discovery context, assumptions, consolidation summary. But it operates on **flat lists of tool calls** — it has no understanding of the session's *narrative arc*. It doesn't know that steps 0-3 were exploration, steps 4-7 were a failed approach, steps 8-12 were a successful pivot, and step 13 was verification. It just sees 14 steps with some tool calls.

This matters for three reasons:

1. **Lessons are low-resolution.** Current lessons look like: `fail: "Fix auth bug" — max steps reached (type: StaleContext). Steps: 15, Tokens: 180K`. They don't capture the *story*: "explored codebase → tried approach A → failed because of X → pivoted to approach B → ran out of budget before finishing B." A session that tried 3 approaches and failed on the 3rd is fundamentally different from one that got stuck on approach 1 for 15 steps.

2. **Handoffs lose the transition narrative.** The continuation prompt tells the next session what files were modified and what approaches failed, but not *why* each pivot happened. AriadneMem (from our earlier research) showed that transition history ("tried A → failed because X → switched to B") prevents the next session from walking into the same dead end for the same reason.

3. **Compression destroys episode boundaries.** The sawtooth compression pattern treats all turns as equal. Early exploration turns get compressed to 80-char summaries the same way pivotal "aha! the bug is actually in X, not Y" turns do. Episode-aware compression could preserve pivot points.

## What We Already Have

Before designing what to build, let's be honest about what the current system already provides:

| Artifact | What it captures | Quality |
|----------|-----------------|---------|
| **ConsolidationSummary** | Agent's own self-assessment at 80% budget | HIGH — the agent knows what it intended, discovered, and concluded |
| **Auto Summary** | Tool usage + 15 capped discoveries (grep matches, files read, test results) | MEDIUM — facts without narrative |
| **FailedApproaches** | Edit failures (oldString not found) | LOW — only catches one failure type, misses conceptual failures |
| **Assumptions** | Early-step interpretation choices | HIGH — captures intent decisions |
| **TodoPlanState** | Structured plan with completion marks | HIGH — but only if manage_todos was used |
| **Lessons** | 1-line failure/cost summaries with failure type + assumptions | LOW — no episode structure |

**The gap:** We have the raw facts (WHAT happened) and the agent's self-assessment (WHAT IT THINKS happened). What's missing is the **structured transition narrative** (WHY things changed direction) and **episode-level insights** (WHICH phases of work represent reusable knowledge vs. dead ends).

## Research Basis (13 Papers + Codebase Audit)

### ★ Steve-Evolving (arXiv:2603.13131, Mar 2026) — The Most Relevant Paper

**Non-parametric self-evolving framework** with a three-phase loop that maps directly to coding agent sessions:

1. **Experience Anchoring:** Each subgoal attempt → structured experience tuple with fixed schema: (pre-state, action, diagnosis-result, post-state). Organized in a *three-tier experience space* with multi-dimensional indices.
2. **Experience Distillation:** Successes → reusable skills with preconditions and verification criteria. Failures → executable guardrails with root causes that forbid risky operations.
3. **Knowledge-Driven Closed-Loop Control:** Retrieved skills and guardrails injected into planner. Diagnosis-triggered replanning updates constraints online.

**Key insights for Forge:**
- **Dual-track distillation** is brilliant: successes and failures produce fundamentally different artifacts. Forge currently dumps both into the same 1-line LESSONS.md format. Successes should produce "for tasks like X, approach Y works well" patterns. Failures should produce "for tasks like X, approach Z fails because of W — avoid it" guardrails.
- **Three-tier experience space** — Forge could organize sessions by (task-type, codebase, failure-pattern) for targeted retrieval, not just chronological order.
- **Compositional diagnosis signals** — Steve-Evolving goes beyond binary success/fail: state-difference summaries, enumerated failure causes, stagnation/loop detection. We already have `FailureType` enum and some of this (loop = `DuplicateAttempt`), but could add more.

### ★ Nemori (arXiv:2508.03341, Aug 2025) — Self-Organizing Memory

**Core innovation: Two-Step Alignment Principle** inspired by Event Segmentation Theory. Provides a principled, top-down method for autonomously organizing raw conversational stream into **semantically coherent episodes**. Second principle (Predict-Calibrate) enables proactive learning from prediction gaps.

**Key insight for Forge:** Event Segmentation Theory from cognitive science says humans naturally segment experience at "event boundaries" — moments when the context shifts significantly. In a coding session, these boundaries are: (1) initial plan formation, (2) approach pivot (RETHINK/ALTERNATIVE), (3) verification success/failure, (4) new subgoal start. These are ALREADY detectable from our tool call patterns. We don't need an LLM to segment — we can segment heuristically using the signals we already capture.

### ★ SAMULE (arXiv:2509.20562, EMNLP 2025) — Multi-Level Reflection

**Three complementary reflection levels:**
1. **Single-Trajectory (micro):** Detailed error correction from one attempt
2. **Intra-Task (meso):** Error taxonomies across multiple trials of the SAME task
3. **Inter-Task (macro):** Transferable insights from similar-typed errors across DIFFERENT tasks

**Key insight for Forge:** Our current lessons are ALL at the macro level (1-line summaries across sessions). SAMULE shows the meso level is crucial — when a task gets resumed, the continuation session needs the micro and meso narratives from the previous attempt, not just the macro lesson. The handoff should be meso-level (detailed transition history of THIS task), while LESSONS.md stays macro-level (cross-task patterns).

### K²-Agent (arXiv:2603.00676, Feb 2026) — Know-What vs Know-How

**Separates declarative (what) and procedural (how) knowledge.** The high-level reasoner runs a Summarize-Reflect-Locate-Revise (SRLR) loop to distill task-level declarative knowledge. The low-level executor learns procedural skills.

**Key insight for Forge:** The handoff should separate:
- **Declarative:** "The auth bug is in TokenService.cs. The refresh method uses a hardcoded 1h expiry."
- **Procedural:** "To fix this type of issue, read the test first (it has the expected value), then modify the implementation."

Currently everything goes into one flat `Summary` string. Splitting into what-was-discovered (declarative) vs what-approach-worked (procedural) would help resumed sessions plan better.

### Mem-α (arXiv:2509.25911, Sep 2025) — Learning Memory Construction

**Uses RL to learn WHAT to store, HOW to structure it, and WHEN to update.** Archives into core, episodic, and semantic components.

**Key insight for Forge:** The insight isn't the RL (we don't have training data). It's the *architecture*: core (always-loaded facts), episodic (session-specific narrative), semantic (cross-session patterns). Our current system has semantic (LESSONS.md) and a rough episodic (handoff), but no formal distinction. Making this explicit would clarify what goes where.

### Previously Analyzed Papers (from Phases 4-5B)

**AriadneMem (Phase 4):** Transition history preserves "tried A → failed because X → switched to B." This IS the episode structure we need.

**BREW (Phase 4):** Experiential learning from categorized sessions. Noisy lessons hurt. Quality filtering is essential.

**Reflexion:** Verbal self-reflection. Our consolidation summary IS this — the agent reflecting on its own session. Already implemented.

**AgentDebug (Phase 5A):** Failure taxonomy with modular error categories. Our FailureType enum is a simplified version. Episodes should be tagged with the failure type that triggered each transition.

**TraceMem (Phase 4):** Episodic → semantic distillation. Raw experiences (episodes) get distilled into reusable facts (lessons). This is exactly the flow: episodes are per-session, lessons are cross-session distillations.

### ★ Repository Memory (arXiv:2510.01003, ICLR 2026) — A Paradigm-Shifting Find

**This paper fundamentally challenges the premise of 5C.** From Microsoft Research, accepted at ICLR 2026. It augments code localization agents with **non-parametric repository memory** from commit history — recent historical commits, linked issues, and functionality summaries of actively evolving code.

**Why it matters for 5C:** We've been thinking about episodic consolidation as enriching SESSION handoffs — extracting episode narratives to help the NEXT session. But Repository Memory asks a different question: **what if the memory isn't about sessions at all, but about the REPOSITORY?**

Human developers don't remember "session 1 failed at step 7." They remember "the auth module is tricky because it has three entry points" and "last time someone changed TokenService, they broke the refresh flow." This is repository knowledge, not session knowledge. And it accumulates across ALL sessions working on the same codebase, not just sequential resume chains.

**Key insights:**
- **Commit history as memory** — the codebase's own evolution IS a form of episodic memory. Each commit is a micro-episode: pre-state → change → post-state.
- **Actively evolving code detection** — identifies which parts of the codebase change frequently, which tells the agent where bugs are likely and where extra care is needed.
- **Non-parametric** — no model training needed. It's a retrieval system over structured data.

**Provocative implication for our design:** Instead of (or in addition to) segmenting Forge's SESSION into episodes, what if we extract repository-level insights from Forge's sessions and write them to REPO.md? For example: after a session that modified `TokenService.cs` with 3 failed attempts before succeeding, add a note to REPO.md: "TokenService.cs — token refresh logic is subtle; read tests before modifying." This is cross-session repository knowledge at the level that actually helps — not "session 12 had 4 episodes" but "this file is dangerous."

### SeaView (arXiv:2504.08696, Apr 2025) — Trajectory Visualization for SWE Agents

**Tool for analyzing and visualizing SWE agent trajectories.** Key observation: SWE agent trajectories exceed 128K tokens, making analysis "difficult" — researchers spend 10-30 minutes manually tracking what happened.

**Key insight:** SeaView identifies the same problem 5C addresses — trajectory comprehension is hard — but solves it for HUMANS (via visualization), not for the AGENT (via structured handoffs). The interesting pattern: they decompose trajectories into step categories, track file access patterns across steps, and identify "environment-related problems."

**For our EpisodeSegmenter:** SeaView's step categorization is essentially the same thing as episode segmentation. Their categories map to ours. This validates the structured approach — if a human needs categories to understand a trajectory, so does a resuming agent.

### A2P — Abduct-Act-Predict (from Phase 5A, arXiv:2509.10401)

**Counterfactual reasoning for failure attribution.** Already in our research corpus but has a direct bearing on 5C: the "Abduction" step (infer hidden root causes) is exactly what episode pivot detection needs. When the agent shifts from approach A to approach B, the transition reason IS the abductive inference: "approach A failed because of [root cause], so I'm trying [alternative]."

## Codebase Analysis: The Episode Infrastructure We Already Have

A deep pass through the agent code reveals something striking: **Forge already has 10 episode-detection signals scattered across 4 files.** The signals are just not unified.

| Signal | File | What It Detects | Current Use |
|--------|------|-----------------|-------------|
| `ContainsAssumptionReasoning()` | AgentLoop | Planning-phase intent interpretation | Sticky breadcrumb + handoff |
| `ContainsHypothesisReasoning()` | AgentLoop | Diagnostic reasoning (debugging) | Debug logging only |
| Consolidation capture | AgentLoop | Agent self-assessment at 80% budget | Handoff priority |
| `consecutiveFailures` counter | AgentLoop | Failure streak detection | Progressive deepening + nudges |
| `ClassifyFailure()` | AgentLoop | Failure type taxonomy | Targeted nudge text |
| `BuildFailureNudge()` RETHINK/ALTERNATIVE | AgentLoop | Explicit pivot markers in injected text | Agent response = pivot reasoning |
| `GetAdaptiveKeepRecentTurns()` | LlmClient | Edit vs explore phase detection | Compression window sizing |
| `VerificationTracker` pending/satisfied | VerificationTracker | Edit→verify mini-episode | Reminder injection |
| `CheckRedundancy()` | VerificationState | Repeated action detection (stall indicator) | Inline hints |
| `CheckFileReRead()` | VerificationState | File re-read detection | Inline hints |

**The critical realization:** We don't need to BUILD episode detection — we need to UNIFY signals we already have. The `EpisodeSegmenter` isn't inventing new detection logic. It's reading the existing signals from the `StepRecord` data and stitching them into a coherent narrative.

**What this means for the implementation:**

1. **Episode boundaries are ALREADY being detected during execution** — by the failure counter, phase detector, and verification tracker. The novel part is capturing them post-hoc from the step records.

2. **Pivot reasoning is already being ELICITED** — BuildFailureNudge injects "RETHINK/ALTERNATIVE/GIVE UP" language. The agent's response to this nudge contains the pivot reasoning. We just need to capture it like we capture assumptions.

3. **The `consecutiveFailures` counter IS an episode state machine.** Failure streak start → escalation → nudge → response → success (reset) maps perfectly to: failure-episode → pivot-episode → recovery-episode.

4. **Phase detection in compression (`GetAdaptiveKeepRecentTurns`) and episode type are the SAME THING.** When the compression switches from 6-turn explore window to 12-turn edit window, that's an episode boundary. The code already knows this — it just doesn't record it.

### What This Changes About the Design

The original design envisioned an `EpisodeSegmenter` doing sophisticated heuristic analysis of tool call sequences. The codebase analysis reveals that most of that analysis is **already happening in real-time** across the existing detection mechanisms. The segmenter can be much simpler — it reads the step records for markers that were ALREADY identified during execution:

- Steps with `IsError = true` on consecutive steps → failure episode
- Steps where FailureType changes → implicit pivot
- Steps with assumption/hypothesis indicators in `Thought` → planning/diagnosis episode
- Steps with primarily read-only tools → exploration episode
- Steps with write tools → implementation episode
- Steps followed by run_tests → verification episode

The "hard" part — detecting WHY the agent pivoted — is solved by pivot capture during execution (the agent's response to a failure nudge). The segmenter just stitches the raw step data into the episode chain.

## Cross-Paper Synthesis: Three Meta-Insights

### Insight 1: The Two Memories That Matter

Across all 13 papers, two fundamentally different types of memory keep emerging:

| Memory Type | Scope | Duration | Papers | Example |
|-------------|-------|----------|--------|---------|
| **Session memory** (episodic) | One task, one attempt | Handoff → next session | Nemori, SAMULE, AriadneMem | "Steps 4-7 tried modifying TokenService, failed because oldString changed" |
| **Repository memory** (semantic) | One codebase, all sessions | Persistent, accumulating | Repository Memory, REPO.md, BREW | "TokenService.cs changes frequently; read refresh tests before editing" |

Our current system has both but doesn't distinguish them:
- LESSONS.md is a mix of session facts and repository-level observations
- Handoff notes are session-scoped but lack the episode structure to be useful
- REPO.md captures repository structure but not behavioral knowledge from session history

**The 5C design should explicitly target both layers:**
1. **Enrich handoffs** with episode chains (session memory → better resumes)
2. **Feed session insights into REPO.md or a new repository knowledge file** (repository memory → better planning for ALL future sessions, not just the next resume)

### Insight 2: Failures Are More Valuable Than Successes

Steve-Evolving's dual-track distillation reveals an asymmetry: successful approaches are usually obvious in retrospect ("of course you fix the implementation, not the test"). But failure approaches are non-obvious — "I tried modifying the config file but it turns out the value is computed at runtime from environment variables, not read from config." This negative knowledge is the highest-value artifact from episodic consolidation.

Our current `FailedApproaches` extraction only catches one type of failure: `replace_string_in_file` with "oldString not found." It misses:
- Tests that kept failing after edits (wrong root cause diagnosis)
- Builds that failed because of an unrelated dependency
- Approaches abandoned after exploration (read the code, realized it won't work, never attempted edit)

The episode model should capture ALL failures, not just edit failures. The meso-level lesson from SAMULE should record WHY each approach failed at a semantic level.

### Insight 3: The Agent's Consolidation Summary Is the Best Episode Narrative

A recurring finding across Reflexion, L2MAC, CaveAgent, and now Nemori: **the agent's own self-assessment is consistently higher quality than automated extraction.** Our consolidation summary (captured at 80% budget) is already this. And in 5B, we added assumption capture.

The creative implication: instead of building an `EpisodeSegmenter` that tries to reconstruct the narrative from tool calls post-hoc, **what if we capture episode markers DURING execution?**

The agent already tells us when it pivots — it says "ALTERNATIVE" or "Let me try a different approach" in its response text. The consolidation summary already captures the full narrative at 80% budget. What if we also capture **pivot summaries** — the thought text at the moment the agent switches approaches?

This is cheaper, more accurate, and leverages signals we already detect:
- `ContainsAssumptionReasoning()` → captures intent at steps 0-2
- `ContainsHypothesisReasoning()` → captures diagnostic thinking
- NEW: detect pivot moments ("different approach", "let me try", "ALTERNATIVE") → capture the pivot reasoning

Together, these three captures form a natural episode chain without any post-hoc segmentation:
```
[Step 1: Assumptions] "I'm assuming backward compat for existing tokens"
[Step 6: Pivot] "The config-based approach won't work because values are runtime-computed. Let me try modifying TokenService directly."
[Step 12: Consolidation] "Modified TokenService.cs successfully. Tests pass. Backward compat preserved."
```

This IS the episode narrative, assembled from capture points the agent naturally produces.

## Critical Analysis: Should We Add an LLM Post-Processing Step?

The original implementation plan says: "Use LLM (Low reasoning effort) to segment finished sessions into episodes."

**Arguments FOR:** An LLM can understand WHY the agent pivoted, not just THAT it pivoted. It can assess which discoveries were important vs. noise. It can write a natural-language narrative that captures nuance.

**Arguments AGAINST — and why they win for now:**

1. **Cost.** Every finished session triggers an LLM call. At ~$0.10-0.50 per session, this doubles the per-session cost for a post-processing step that the USER never sees (only the next session sees it).

2. **Latency.** The LLM call happens after `Finish()`. The user is waiting for the result. Adding a summarization call delays the final output.

3. **We already HAVE the agent's own assessment.** The consolidation summary is captured FROM the agent DURING execution — it's free. Using an LLM post-hoc to re-summarize what the agent already summarized is redundant.

4. **Episode boundaries are structurally detectable.** We don't need an LLM to know that "step 5 had 3 consecutive failures and then the agent switched approach." That's in the tool call records. The SIGNALS are already there:
   - Consecutive failures → failure episode
   - RETHINK/ALTERNATIVE in response text → pivot
   - New file targets after failures → approach change
   - Test passes after edits → verification episode

5. **Steve-Evolving uses STRUCTURED tuples, not LLM summaries.** The most relevant paper doesn't use an LLM for distillation — it uses structured experience tuples with fixed schemas. This is more reliable and cheaper.

**The verdict:** Use **heuristic episode segmentation** (pattern matching on tool calls and response text), not LLM-based segmentation. Extract episodes into structured tuples (Steve-Evolving style), not narrative summaries. Save the LLM budget for the actual task.

## Revised Design: Pivot Capture + Post-Hoc Segmentation + Repository Knowledge

The cross-paper synthesis revealed that the original design (pure post-hoc `EpisodeSegmenter`) is workable but misses the highest-value signal: **the agent's own pivot reasoning at the moment of transition.** And Repository Memory (ICLR 2026) opens an entirely new dimension: session insights should feed back into the repository, not just into the next session.

### Layer 1: Pivot Capture During Execution (NEW — replaces pure post-hoc)

**The insight:** We already capture assumptions (step 0-2) and consolidation (80% budget). Pivots are the missing middle. When the agent says "ALTERNATIVE" or "let me try a different approach," that thought text contains WHY the previous approach failed — information that gets compressed away in the sawtooth pattern.

**Implementation:** In AgentLoop, detect pivot moments in `response.Text` the same way we detect assumptions and hypotheses:
```csharp
// Alongside existing captures:
// - assumptionsText: captured at steps 0-2 (planning-phase intent)
// - consolidationSummary: captured at 80% budget (agent's self-assessment)
// NEW:
// - pivotReasons: captured when agent explicitly changes approach (transition history)
```

**Detection patterns:** "different approach", "alternative", "let me try", "instead, I'll", "that didn't work", "pivoting to", "rather than", "won't work because"

**What makes this better than pure post-hoc segmentation:** The pivot REASON is in the agent's thought text at the moment of transition. Post-hoc, we can see THAT a transition happened (file targets changed, failures preceded a new approach) but not WHY. The why is only in the thought text, and the thought text gets compressed away.

### Layer 2: Post-Hoc Episode Segmentation (Simplified from original)

The `EpisodeSegmenter` still has value for producing the structured episode chain in the handoff. But now it's simpler — it doesn't need to guess at pivot reasons because those are captured live.

**Simplified episode model:**
```csharp
public sealed record EpisodeSummary
{
    public required string Type { get; init; }       // "exploration", "implementation", "verification"
    public required int StartStep { get; init; }
    public required int EndStep { get; init; }
    public required string Outcome { get; init; }    // "success", "failure", "abandoned"
    public IReadOnlyList<string> FilesInvolved { get; init; } = [];
}
```

Note: `Intent` and `PivotReason` removed from the record — they're now in the live-captured `pivotReasons` list, not inferred post-hoc.

### Layer 3: Repository Knowledge Feedback (NEW — from Repository Memory)

**The creative addition:** When a session that modified a file had ≥2 failed attempts before succeeding (or failed entirely), the episodic data represents a "this file is tricky" signal. Currently this dies in the session handoff. Repository Memory suggests feeding it back to the codebase level.

**Minimal approach:** After session completion, if episodes show failure patterns concentrated on specific files, append a note to REPO.md:
```
## Session-Derived Notes
- TokenService.cs: Multiple edit attempts failed (stale context). Re-read carefully before modifying.
- AuthController.cs: Has 3 entry points for auth flow — check tests to verify which one the task targets.
```

This is the Steve-Evolving "guardrail" concept operationalized for a coding agent: failures distilled into warnings attached to the relevant code locations, loaded for ALL future sessions via the existing REPO.md mechanism.

**Why this is high-leverage:** REPO.md is already loaded into every session's system prompt. Session-derived notes get injected into EVERY future agent session on this repository — not just the next resume. One tricky debugging session permanently improves all future sessions working on the same codebase.

### What the Handoff Gets (All Three Layers)

The continuation prompt for resumed sessions now includes:

```
Progress so far:
[consolidation summary — agent's own self-assessment]

Approach transitions:
1. [Step 5] "Config-based approach won't work — values are runtime-computed"
2. [Step 9] "TokenService.Refresh has an off-by-one in the expiry calc"

Episode chain: exploration(0-3) → impl-attempt(4-7,FAIL) → pivot → impl-attempt(8-12,partial)

Assumptions from previous session:
  - Backward compat for existing tokens
```

### What Lessons Get (Enriched with Episode Chain)

```
- [2026-03-21] fail: "Fix auth bug" — max steps. Steps: 15, Tokens: 180K
  Trajectory: explore(0-3) → impl(4-7,FAIL:StaleContext) → pivot → impl(8-12,partial)
```

The trajectory line is the AriadneMem transition history. Compact, no insight line needed — the trajectory itself tells the story.

### What Compression Gets (Sticky Pivot Breadcrumbs — Cheap Win)

Following the same pattern as sticky assumption breadcrumbs (already implemented in 5B), add **sticky pivot breadcrumbs** in `CompressTurns()`. When a turn containing pivot language gets compressed, preserve the pivot reason:
```
[Pivot: "Config-based approach won't work — values are runtime-computed"]
```

This is ~5 lines in the existing sticky breadcrumb logic. No new infrastructure. The agent at step 18 remembers not just what it assumed (step 1) but also why it changed direction (step 6).

## Implementation Plan (Revised)

### What to Build

1. **Pivot detection + capture in AgentLoop** — `ContainsPivotReasoning()` and `ExtractPivotReason()` methods, same pattern as assumption detection. Capture into a `List<(int Step, string Reason)>` during execution. ~30 lines.

2. **`EpisodeSegmenter` class** — Takes `List<StepRecord>` and returns `List<EpisodeSummary>`. Simplified: only type/start/end/outcome/files, no intent/reason (those are live-captured). ~60 lines.

3. **`EpisodeSummary` record** — Simplified data struct. ~10 lines.

4. **`SessionHandoff.Episodes` + `SessionHandoff.PivotReasons`** — New fields. Update `Generate()` and `BuildContinuationPrompt()`. ~25 lines.

5. **Sticky pivot breadcrumbs in compression** — Same pattern as sticky assumptions. ~8 lines in `CompressTurns()`.

6. **Enrich `GenerateLesson()` with episode chain** — Append trajectory line to lessons when episodes available. ~15 lines.

7. **Repository knowledge feedback** — After session, if failures concentrated on specific files, append warning to REPO.md `## Session-Derived Notes` section. ~30 lines in a new method called from `Finish()`.

8. **Tests** — Episode segmenter + pivot detection + repo feedback. ~60 lines.

**Total: ~240 lines across 4-5 files. No new classes except `EpisodeSegmenter`.**

### What NOT to Build

| Feature | Why not |
|---------|---------|
| LLM-based segmentation | Cost, latency, redundant with live capture |
| Episode-aware compression (full) | Pivot breadcrumbs give 80% of value. Full episode-aware compression requires bridging LlmClient timing. |
| Three-tier experience space (Steve-Evolving) | Over-engineered for ~30 sessions. Chronological lessons sufficient. |
| RL-based memory construction (Mem-α) | Requires training data |
| Full commit-history integration (Repository Memory) | Requires git log parsing infrastructure. Session-derived REPO.md notes are the cheap proxy. |
| Automated trajectory comparison (SeaView) | Visualization tool for researchers, not agent infrastructure. Defer to Phase 6. |

### Success Criteria

- Pivot reasons captured for 80%+ of sessions with approach changes
- Episode chain appears in handoff continuation prompt for resumed sessions
- Lessons include trajectory line when episodes are available
- REPO.md accumulates session-derived notes for frequently-modified tricky files
- Sticky pivot breadcrumbs survive compression alongside sticky assumptions
- Zero additional LLM calls
- No regression for simple sessions (1-3 steps)

## Research Papers Referenced

| Paper | arXiv | Date | Key Insight |
|-------|-------|------|-------------|
| **Steve-Evolving** | **2603.13131** | **Mar 2026** | **Dual-track distillation: successes → skills, failures → guardrails. Structured tuples. Three-phase loop.** |
| **Nemori** | **2508.03341** | **Aug 2025** | **Event Segmentation Theory → principled episode boundaries from context shifts.** |
| **SAMULE** | **2509.20562** | **EMNLP 2025** | **Three reflection levels: micro/meso/macro. Meso (intra-task trajectory) is our gap.** |
| **K²-Agent** | **2603.00676** | **Feb 2026** | **Declarative vs procedural knowledge separation. SRLR refinement loop.** |
| **Mem-α** | **2509.25911** | **Sep 2025** | **Core/episodic/semantic memory architecture.** |
| **★ Repository Memory** | **2510.01003** | **ICLR 2026** | **Non-parametric repo memory from commit history. Accumulate knowledge about the CODEBASE, not just about sessions. Agents should build long-term repo understanding.** |
| **SeaView** | **2504.08696** | **Apr 2025** | **Trajectory visualization — validates structured step categorization for comprehension. Same decomposition as our episode types.** |
| **A2P** | **2509.10401** | **Sep 2025** | **Abductive reasoning for failure attribution — pivot reasons ARE abductive inferences about root causes.** |
| AriadneMem | (Phase 4) | 2025 | Transition history prevents retry of dead ends |
| BREW | (Phase 4) | 2025 | Categorized experiential learning. Noisy lessons hurt. |
| Reflexion | (Phase 4) | 2023 | Verbal self-reflection (our consolidation summary) |
| AgentDebug | (Phase 5A) | 2025 | Modular failure taxonomy with targeted recovery |
| TraceMem | (Phase 4) | 2025 | Episodic → semantic distillation |
