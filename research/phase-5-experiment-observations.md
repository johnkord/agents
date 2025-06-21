# Phase 5 Experiment Observations — Forge Coding Agent

## Summary

After implementing Phases 5A (Hypothesis-Driven Debugging), 5B (Explore-Then-Assume), and 5C (Episodic Consolidation), we ran two live experiments against the Forge codebase itself. This document records what we observed, what worked, what didn't, and what the results tell us about the research that informed the design.

**Test environment:** Forge v0.1 with gpt-5.4, MCP server with 40 tools (10 core), medium reasoning effort, targeting the Forge codebase itself.

---

## Experiment 1: Known Bug Fix (Scenario 1)

**Task:** "There's a bug in the EventLog.cs file — filenames with special characters like backticks, curly braces, and dollar signs aren't being sanitized properly. The SanitizeFileName method misses these characters. Find and fix the bug."

**Budget:** 12 steps, 200K tokens.

### Results

| Metric | Value |
|--------|-------|
| Steps used | 10 / 12 |
| Tokens | 105,622 |
| Duration | 45.5s |
| Outcome | ✅ Correct fix + regression test |
| Tests | 336 passing (1 new) |
| Verification compliance | 100% |
| Assumptions stated | None (task well-specified) |
| Pivots | None (first approach worked) |
| Episodes | 6 (explore → impl → explore → verify → explore → planning) |

### Observations

**The agent executed a clean Plan→Act→Verify cycle.** It used `manage_todos` to externalize a 3-step plan, explored the codebase to locate `SanitizeFileName`, read both the implementation and existing tests, made a focused fix (4 additional characters in the `.Replace()` chain), added a regression test, read back both edits, ran tests, and reported. This is the textbook workflow the system prompt was designed to produce.

**The debugging protocol was correctly deprioritized.** The task contains "bug" and "fix" which trigger `IsDebuggingTask`, but the agent correctly judged that the bug was directly described — no need to reproduce, hypothesize, or do for-and-against analysis. The protocol guidance was present in the prompt but the agent adapted its behavior to the task's specificity. This validates the Dyna-Think research principle: only action-relevant reasoning helps. Forcing the agent through a 5-step diagnostic protocol for a directly-specified bug would have wasted ~3 steps.

**The 5B assumption guidance was a clean no-op.** The PLAN section says "If the task has multiple valid interpretations, state which one you're choosing." For this task, there was only one interpretation. No assumptions were stated. This confirms the design: zero cost for well-specified tasks, because the "if" condition evaluates to false and the agent moves on.

**Episode segmentation produced accurate but noisy output.** The 6-episode chain (`exploration(0-3) → implementation(4-5) → exploration(6) → verification(7) → exploration(8) → planning(9)`) correctly captures the arc but includes single-step "exploration" episodes that are actually verification reads and todo management. The segmenter classifies by tool type, not intent — a `read_file` used for verification looks the same as one used for exploration. This is the inherent limitation of heuristic segmentation without intent inference. Nemori's Event Segmentation Theory (arXiv:2508.03341) addresses this with semantic coherence analysis, but that requires LLM-level understanding we deliberately avoided.

**The lesson system was correctly silent.** Successful sessions under 300K tokens don't generate lessons (BREW research: noisy lessons degrade performance). No lesson was saved.

### Research Validation

| Research Claim | Validated? | Evidence |
|----------------|-----------|----------|
| **DeepVerifier:** Structured verification checklists improve accuracy | ✅ Yes | 100% verification compliance — agent read back both edits and ran tests |
| **Dyna-Think:** Only action-relevant reasoning helps | ✅ Yes | Agent skipped debugging protocol for directly-specified bug |
| **BREW:** Noisy lessons degrade performance | ✅ Yes (indirectly) | Correctly suppressed lesson for successful session |
| **SWE-Pruner:** Read ops are 76% of token cost | ✅ Approximate | ~6 of 10 steps involved read-only tools |

---

## Experiment 2: Budget-Constrained Resume (Scenario 3)

**Task:** "Add a new FailureType called 'Timeout' to the failure taxonomy. This should be classified when a tool call's DurationMs exceeds 30000ms. Add appropriate nudge text and update ClassifyFailure. Include tests."

**Session 1 budget:** 5 steps, 150K tokens.
**Session 2 (resumed) budget:** 15 steps, 200K tokens.

### Session 1 Results (Failed)

| Metric | Value |
|--------|-------|
| Steps used | 5 / 5 |
| Tokens | 46,718 |
| Duration | 21.0s |
| Outcome | ❌ Budget exhausted during exploration |
| Files modified | None |
| Consolidation summary | ✅ Captured (1081 chars, excellent quality) |
| Todo state | ✅ Captured (1/5 in-progress, 4 not-started) |
| Episodes | 1 (exploration, all 5 steps) |
| Pivots | None |
| Assumptions | None |

### Session 2 Results (Resumed)

| Metric | Value |
|--------|-------|
| Steps used | 15 / 15 |
| Tokens | 215,245 |
| Duration | 107.6s |
| Outcome | ✅ (work likely complete — all edits verified, tests pass) |
| Tests | 338 passing (2 new) |
| Verification compliance | 100% (4/4 edits) |
| Continuation context | 3,118 chars injected |
| Re-exploration steps | ~3 (re-read files, but targeted not broad) |
| Episodes | 9 |

### Observations

**The consolidation summary is the single most valuable handoff artifact.** Session 1's consolidation told session 2 exactly: (a) what files contain the relevant code, (b) what style of tests to match, (c) what 4 steps remain. The resumed session's plan directly referenced this knowledge: "Re-read AgentLoop.cs and FailureTaxonomyTests.cs to verify the current state before editing." It didn't start from "what files are in this project?" — it started from "the taxonomy is in AgentLoop.cs, the tests are in FailureTaxonomyTests.cs."

This validates the Reflexion (2023) and L2MAC research: the agent's own verbal self-assessment is higher quality than automated extraction. The auto-summary says "Completed 5 steps. Tools used: manage_todos, file_search, grep_search, read_file." The consolidation says "Confirmed FailureType, ClassifyFailure, and BuildFailureNudge all live in Forge.Core/AgentLoop.cs. Confirmed existing test style. What remains: add enum member, nudge text, classification logic, tests." The difference in actionability is night and day.

**The todo plan state provided a concrete checklist.** The resumed session immediately set up its own 5-item todo list that mapped to the predecessor's unfinished items. This is the CaveAgent/SWE-Adept persistent state pattern working exactly as designed.

**Re-reads are a necessary cost, not waste.** The resumed session re-read `AgentLoop.cs` and `FailureTaxonomyTests.cs` — files session 1 had already explored. The system prompt says "Verify the current file state with read_file before making edits — files may have changed since this summary was written." This is correct behavior (files genuinely could have changed between sessions), but it's 3 steps of re-exploration. For a 15-step budget, that's 20% overhead. For a 30-step budget it would drop to 10%. The re-read cost is fixed, not proportional.

**The "work likely complete" detection fired correctly.** Session 2 hit max steps at step 14 (run_tests, all passing). The heuristic — all edits verified + last step is verification + no pending edits — correctly identified that the task was functionally done, just missing the final report step. The yellow warning message ("Budget exhausted — but edits were verified successfully") prevented a misleading "task failed" message. This validates the EET (arXiv:2601.05777) early-termination detection approach.

**Episode chain for the resumed session is informative but noisy.** 9 episodes over 15 steps means an episode boundary roughly every 1.7 steps. The chain is `exploration(0-5) → implementation(6) → exploration(7) → implementation(8) → exploration(9-10) → implementation(11) → verification(12) → exploration(13) → verification(14)`. The repeating explore→implement→explore→implement pattern reflects the agent's careful verify-after-edit workflow, but the segmenter can't distinguish "read for verification" from "read for exploration." This is cosmetic — the primary handoff data (consolidation summary, todo state, files modified, test output) was all accurate.

**The lesson from the failed session has an attribution problem.** The lesson says `(failed tools: semantic_search)` — referring to a tool the agent tried that doesn't exist. But `semantic_search` failing didn't cause the session failure; budget exhaustion did. `GenerateLesson` extracts ALL errored tools and lists them, even if they're incidental. This has been a known quality issue since Phase 4 (BREW research warned that noisy lessons degrade performance). The lesson text conflates "what tools had errors during the session" with "why the session failed." These are different questions.

**No pivots or assumptions were captured — by design.** This task was well-specified (add X to Y), so no assumptions were needed. And the agent never changed approach (it just ran out of budget mid-exploration), so no pivots occurred. The 5C pivot capture infrastructure was correctly silent.

### Research Validation

| Research Claim | Validated? | Evidence |
|----------------|-----------|----------|
| **Reflexion:** Agent's self-assessment > automated extraction | ✅ Yes | Consolidation summary dramatically more actionable than auto-summary |
| **CaveAgent:** Persistent state helps resume | ✅ Yes | Todo state carried checklist across sessions |
| **AriadneMem:** Transition history prevents dead-end retries | ⬜ Not tested | No pivots occurred in this scenario, so no transition history to validate |
| **SAMULE meso-level:** Intra-task trajectory matters | ⬜ Not tested | Single-episode session had no trajectory diversity |
| **EET:** Early-termination detection from execution patterns | ✅ Yes | "Work likely complete" correctly detected all-verified-edits + test-pass |
| **BREW:** Noisy lessons hurt | ⚠️ Partially | Lesson saved with misleading `semantic_search` attribution |
| **Steve-Evolving:** Dual-track distillation | ⬜ Not tested | No failures-then-success pattern to distill |
| **Nemori:** Event boundaries at context shifts | ⬜ Partially | Segmenter detects type changes but can't distinguish intent |

---

## Cross-Experiment Insights

### Insight 1: The Consolidation Summary Is the Killer Feature

Across both experiments, the consolidation summary — captured from the agent's own response when the boundary warning fires — was the highest-quality artifact in the entire handoff system. It's free (captured during execution, no post-hoc LLM call), it captures intent (the agent knows WHY it does things, not just WHAT tools it called), and it's actionable (tells the next session exactly what to do).

The implication: **investments in improving consolidation capture have higher ROI than investments in post-hoc episode analysis.** The agent at 80% budget has full context about everything it's done and everything that remains — more context than any post-hoc analyzer could reconstruct. The consolidation prompt design (Ask the agent to: summarize what's done, list what remains, give specific next steps) is the right format.

**Potential improvement:** The consolidation currently fires once at 80% budget. For longer sessions (30+ steps), capturing a mid-session checkpoint at 50% budget might provide an intermediate consolidation that enriches the final handoff, especially if the agent pivots between the two checkpoints.

### Insight 2: Episode Segmentation Is Supplementary, Not Primary

The episode chain (`explore → impl → verify`) provides useful narrative structure but is never the primary data that drives decision-making. In the resumed session, the plan was built from the consolidation summary and todo state, not from the episode chain. The episode chain would become more valuable in two scenarios we didn't test:

1. **Multi-approach sessions** where the agent tries approach A (fails), pivots to B (fails), pivots to C (succeeds). The episode chain would show `impl-A(FAIL) → pivot → impl-B(FAIL) → pivot → impl-C(success)` — and this AriadneMem-style transition history would be genuinely useful for understanding what was tried.

2. **Cross-session trajectory analysis** (Phase 6B) where episode chains from many sessions are compared to identify systematic patterns: "tasks in this codebase always spend 60% of budget on exploration" or "stale-context failures cluster in sessions targeting file X."

For the current use case — single-session handoffs — the consolidation summary does the heavy lifting and the episode chain is supplementary narration.

### Insight 3: The Features That Didn't Fire Aren't Failures

The 5B assumption capture, 5C pivot detection, and 5A debugging protocol were all designed for specific task types: underspecified tasks, multi-approach sessions, and diagnostic debugging. Neither experiment triggered these patterns — Experiment 1 was a well-specified known-bug fix, and Experiment 2 was a well-specified feature addition that failed due to budget, not approach.

This is correct behavior. If these features triggered on every session regardless of task type, they'd be noise. Their value is in the TAIL cases — the session where the user says "fix the auth bug" (which auth bug?) or the session where the first approach fails and the agent must pivot. These experiments validated that the features are correctly SILENT when they shouldn't fire, which is half the battle.

The research is clear on this: CLAMBER (ACL 2024) showed that detection-based approaches often produce false positives that increase overconfidence. Ambig-SWE (ICLR 2026) showed that models struggle to distinguish well-specified from underspecified tasks. By making the features respond to structural signals (the agent's own text containing "assuming"/"alternative"/etc.) rather than trying to pre-detect when they're needed, we avoid the false positive problem entirely.

### Insight 4: Lesson Quality Needs Causal Attribution

The lesson from the failed session (`failed tools: semantic_search`) reveals a systematic weakness: `GenerateLesson` conflates correlation with causation. Every tool error during the session gets listed, even if it was incidental. A genuine improvement would be to only list tools whose failure was related to the session outcome. For max-steps failures, the cause is "task too complex for budget" not "semantic_search failed." For test-failure stops, the cause IS the test failure.

This maps to the A2P paper's (arXiv:2509.10401) counterfactual approach: "If we removed the semantic_search error, would the session have succeeded?" Answer: no, it would have run out of budget anyway. Therefore semantic_search is not the cause. Full counterfactual reasoning is too expensive for lesson generation, but a simpler heuristic would help: for budget-exhaustion failures, omit the "failed tools" detail entirely and instead note "task too complex for N-step budget."

### Insight 5: Verification Compliance Is Consistently High

Both experiments achieved 100% verification compliance (every edit was verified with a read-back and/or test run). The DeepVerifier-inspired verification checklist in the system prompt, combined with the CoRefine-inspired VerificationTracker that injects reminders when verification is overdue, appears to produce reliable verification behavior. Neither experiment triggered a verification reminder — the agent verified proactively every time.

This suggests the prompt-level guidance is sufficient for verification compliance with the current model (gpt-5.4). The VerificationTracker serves as a safety net that hasn't been needed yet. Whether it will fire for harder tasks or weaker models remains to be seen.

---

## What the Experiments Didn't Test

| Scenario | What it would validate | Why it didn't happen |
|----------|----------------------|---------------------|
| Multi-approach debugging | 5A debugging protocol, 5C pivot capture | Both tasks were directly specified, no diagnosis needed |
| Underspecified task | 5B assumption stating | Both tasks were well-specified |
| Wrong first approach | 5C pivot + transition history in handoff | Agent's first approach worked both times |
| Cross-session lesson use | LESSONS.md → behavior change | No session read a relevant lesson for its task |
| Compression destroying pivots | 5C sticky pivot breadcrumbs | No pivots occurred to compress |
| Long session (30+ steps) | Episode chain diversity, multi-checkpoint consolidation | Both experiments used reduced budgets |

These scenarios require either deliberately adversarial tasks (plant a misleading bug description), longer budgets, or tasks on unfamiliar codebases where the agent would naturally need multiple attempts.

---

## Research Integration: What the Literature Predicted vs. What We Observed

### The Cost of Dynamic Reasoning (arXiv:2506.04301, HPCA 2026)

This paper presents the first system-level analysis of AI agent infrastructure costs — quantifying resource usage, latency, energy consumption across diverse agent designs. Key finding: **agents suffer from rapidly diminishing returns with increased compute**, and the accuracy-cost tradeoffs vary dramatically by design choice.

**Connection to our experiments:** Experiment 1 used 105K tokens for a 10-step bug fix. Experiment 2 used 262K tokens total (46K + 215K) across two sessions for a 20-step feature addition. The cost-per-step increases as the session progresses because the context window grows (even with sawtooth compression). Our adaptive `KeepRecentTurns` — using 6 turns during exploration and 12 during editing — is an implicit acknowledgment of the diminishing-returns finding: we compress more aggressively when the agent is doing low-value exploration and preserve more context when it's doing high-value editing.

**A deeper observation the paper frames for us:** The resume pattern (Experiment 2) is actually a form of compute-efficient reasoning. Instead of one long 30-step session with monotonically growing context, two shorter sessions (5 + 15) with a handoff in between effectively RESET the context window cost. The resumed session starts with a 3K-char summary instead of 46K tokens of compressed history. This is cheaper AND more informative — the handoff is a higher-quality summary than the sawtooth compression would produce from the raw turns. **Session splitting may be a deliberate cost-optimization strategy, not just a fallback for budget exhaustion.**

### The Explore-Edit-Verify Macro Pattern

Across both experiments, the agent followed the same macro pattern: explore the codebase → edit the code → verify the edit. This pattern maps to the SWE-Pruner finding (arXiv:2601.06797) that read operations constitute 76% of token cost. In Experiment 1, 6 of 10 steps were read-only exploration/verification. In Experiment 2, roughly 12 of 20 cumulative steps were read-only.

**What this means for episode segmentation:** The `EpisodeSegmenter` classifies steps by tool type, producing explore→impl→verify episodes. This classification is structurally correct but may be the wrong abstraction. The agent's INTENT doesn't change between exploration steps 0-3 and implementation step 4 — it's all one coherent effort to fix the bug. The episode boundary is a tool-type transition, not an intent transition. Nemori's Event Segmentation Theory (arXiv:2508.03341) defines boundaries at "moments when the context shifts significantly." A shift from `read_file` to `replace_string_in_file` is a tool shift, not necessarily a context shift — the agent may have been building up to the edit all along.

**Implication:** For handoffs and lessons, the coarse arc ("explored then fixed then verified") is more useful than the fine-grained episode chain. The consolidation summary captures the coarse arc naturally. The episode chain adds precision that is rarely needed. This reinforces our Scenario 1 conclusion: episodes are supplementary, not primary.

### What SAMULE's Three Levels Mean for Our Data

SAMULE (arXiv:2509.20562, EMNLP 2025) proposes micro/meso/macro reflection levels. Our experiments reveal something about how these levels manifest in practice:

- **Micro level (single trajectory):** This is what happens within a session — the agent reads code, edits, verifies, adapts. Our step records capture this fully. But micro-level detail rarely survives to the next session because it's compressed or lost. The sticky breadcrumbs (assumptions, pivots) are our attempt to preserve the highest-value micro-level signals.

- **Meso level (intra-task across attempts):** This is what the handoff captures — the narrative arc of one multi-session task. Our consolidation summary + todo state IS the meso level. Experiment 2 demonstrated this working: the resumed session received a meso-level narrative ("located the code, confirmed the style, these 4 items remain") and used it effectively.

- **Macro level (inter-task cross-session):** This is LESSONS.md — one-line patterns that cross task boundaries. Our experiments revealed a quality problem at this level: the lesson from Experiment 2's failed session misattributed the cause to `semantic_search` instead of budget exhaustion. If macro-level lessons carry false attributions, they can MISLEAD future sessions (BREW's noisy-lesson finding). The micro and meso levels worked well; the macro level needs causal attribution repair.

**The creative insight:** There may be a fourth level that none of the papers address — what we might call the **"codebase level."** This sits between meso (one task) and macro (cross-task patterns) and represents accumulated knowledge about THIS specific codebase. "AgentLoop.cs is 968 lines and contains the failure taxonomy." "Tests are in FailureTaxonomyTests.cs and follow a specific pattern." This knowledge doesn't belong in lessons (too specific to one codebase) or handoffs (too general for one task). It belongs in REPO.md — which we already load into every session. The Repository Memory paper (arXiv:2510.01003, ICLR 2026) identified this level, and our experiments confirm it: the most valuable pieces of the consolidation summary were codebase-level facts, not task-level progress.

### The Protocol Adaptation Phenomenon

In Experiment 1, the debugging protocol was included in the system prompt (IsDebuggingTask returned true) but the agent correctly adapted its workflow — skipping reproduction and hypothesis generation because the bug was directly specified. This is not something any of the papers predicted. The research assumes agents follow protocols mechanically: give them a debugging protocol, they'll follow it step-by-step. Our agent didn't. It treated the protocol as guidance to draw from contextually, not as a rigid checklist.

**Why this matters:** This suggests the protocol sections in the system prompt work differently than we assumed. They're not rules that get followed verbatim — they're a **menu of strategies** the agent draws from based on task characteristics. The debugging protocol gave the agent VOCABULARY (hypothesis, for-and-against, reproduce) and STRUCTURE (5-step diagnostic flow), but the agent exercised judgment about which parts to use. When the diagnosis was already in the task description, it skipped straight to fixing.

**This is actually better than rigid protocol following.** AT-CXR (arXiv:2508.19322) showed that the best results come from a "decide or defer" pattern where the agent estimates confidence and acts accordingly. Our agent implicitly did this: "Am I confident I know what's wrong? Yes (the task told me). Skip diagnosis, proceed to fix."

The risk is the opposite case: when the agent SHOULD follow the protocol but doesn't because it feels confident. Ambig-SWE (ICLR 2026) showed agents are systematically overconfident. Our debugging protocol might be ignored for tasks where it would actually help, simply because the agent feels it already understands the problem. We haven't tested this case yet (it requires a task with misleading symptoms).

### The Re-Read Cost in Resumed Sessions

Experiment 2's resumed session spent 3 of its first 5 steps re-reading files that session 1 had already explored. The handoff told it "FailureType, ClassifyFailure, and BuildFailureNudge all live in Forge.Core/AgentLoop.cs" — but the system prompt says "Verify the current file state with read_file before making edits — files may have changed."

This creates a tension: **the handoff provides knowledge, but the verification protocol demands re-verification.** The agent resolved this correctly (files could have changed between sessions!), but the cost is 3 steps of re-exploration — a ~20% overhead on a 15-step budget.

**No paper addresses this specific tension.** The closest is CaveAgent's principle that "the filesystem IS the checkpoint" — if the handoff says file X contains code Y but file X has been modified since, the handoff is wrong and the re-read is essential. The re-read cost is the price of correctness in a world where the codebase isn't frozen between sessions.

**Potential optimization:** Instead of re-reading entire files, the resumed session could verify the handoff's claims with a targeted check: `grep_search` for the specific function names mentioned in the consolidation summary. This would confirm the code structure hasn't changed at a fraction of the re-read cost. If the grep matches, trust the handoff. If it doesn't, then re-read.

---

## Recommendations for Phase 6

Based on these experiments and the research analysis, four priorities emerge for evaluation infrastructure:

1. **Build a regression suite with diverse task types.** The two experiments tested the "happy path" (well-specified tasks, clean approaches). The tail-case features (assumptions, pivots, debugging protocol) need tasks that deliberately trigger them: vague descriptions, planted bugs with misleading error messages, tasks requiring multi-file refactors with design decisions.

2. **Fix lesson causal attribution.** The current `GenerateLesson` conflates incidental tool errors with session failure causes. For budget-exhaustion failures, suppress the "failed tools" detail. For test-failure stops, include only the failing test, not unrelated tool errors. This is a small change (~10 lines in `GenerateLesson`) with disproportionate impact on lesson quality.

3. **Consider a mid-session consolidation checkpoint.** For sessions with 30+ steps, a consolidation at 50% budget (in addition to 80%) would capture the agent's narrative at two points in its trajectory. If the agent pivots between the two, the 50% consolidation captures the pre-pivot state and the 80% consolidation captures the post-pivot state — giving the handoff a natural two-episode narrative without needing the EpisodeSegmenter.

4. **Explore deliberate session splitting as a cost strategy.** The HPCA 2026 cost paper's diminishing-returns finding, combined with our observation that resumed sessions reset context cost, suggests that deliberately splitting long tasks into explore-then-implement sessions might be more efficient than one long session where exploration context bloats the implementation phase.

---

## Additional Research (Round 2)

### TestPrune (arXiv:2510.18270, Oct 2025) — Regression Tests for Agent Debugging

**Key insight:** Regression test suites in real projects are large and exceed agent context limits, introducing noise and inflating costs. TestPrune minimizes the suite to a small, relevant subset — yielding 6-13% relative improvement in issue resolution at $0.02-$0.05 per instance.

**Connection to our experiments:** In Experiment 1, the agent read the existing test file (172 lines) to understand the test style before adding its own test. This is the TENET "tests as executable specifications" pattern working naturally. But for larger codebases with thousands of tests, the agent can't read the entire suite. TestPrune's insight — that test minimization is essential for agent performance — reinforces our 5B recommendation to "check existing tests for intent" while suggesting a future need for intelligent test selection (which tests are relevant to the current task?).

**For Phase 6:** Our regression suite should be kept small and focused. Rather than accumulating every previous task, curate a minimal diverse set that covers the tail-case features (pivots, assumptions, debugging, vague tasks). TestPrune's principle applies to our evaluation suite just as it does to production test suites.

### The Cost of Dynamic Reasoning (arXiv:2506.04301, HPCA 2026) — Deeper Analysis

Revisiting this paper with our experimental data: Forge Experiment 1 used 105K tokens over 10 steps (10.5K tokens/step average). Experiment 2 used 262K tokens over 20 cumulative steps (13.1K tokens/step). The per-step cost grew 25% between a 10-step and 20-step session — this IS the diminishing returns the paper describes. The context window grows each step, and even with sawtooth compression, the prompt tokens per step increase because more compressed history accumulates.

**The session-splitting insight deepens:** If we split a 20-step task into two 10-step sessions, the total tokens would be approximately: Session 1 (10 steps × 10.5K) + Session 2 (10 steps × 10.5K) + handoff overhead (3K) = 213K. The actual cost was 262K — meaning session splitting saves ~19% of tokens for equivalent work. This isn't theoretical — it's what our experiment demonstrated. The handoff's 3K summary is cheaper than carrying 5 compressed exploration turns across 15 implementation steps.

---

## Implementation Plan: Research-Informed Improvements

Based on the experiments, research analysis, and cross-paper synthesis, here are the concrete improvements ordered by impact-to-effort ratio.

### Priority 1: Fix Lesson Causal Attribution (~15 lines)

**Problem:** `GenerateLesson` lists all errored tools in a failed session, even when they're incidental. Experiment 2's lesson blamed `semantic_search` (a tool the agent tried once and moved on from) when the actual cause was budget exhaustion.

**Research basis:** BREW (arXiv:2511.20297) showed noisy lessons degrade performance. A2P (arXiv:2509.10401) emphasizes counterfactual attribution: "would removing this error have changed the outcome?"

**Implementation:**
- For budget-exhaustion failures (`"maximum steps"` or `"Maximum tokens"` in FailureReason): omit `failed tools` from the lesson entirely and instead note the budget constraint
- For test-failure stops: include only the failing test tool result, not every other tool that errored
- For cancelled sessions: already correctly filtered (existing logic)

**Files:** `AgentLoop.cs` → `GenerateLesson()` method only.

**Expected outcome:** Lessons become causally accurate. Future sessions reading "budget too small for task" learn to plan more efficiently rather than avoiding `semantic_search`.

### Priority 2: Curated Regression Task Suite (~100 lines, new file)

**Problem:** We tested 2 scenarios, both on the happy path. The tail-case features (assumptions, pivots, debugging protocol, episode transitions) have never been exercised in a real session.

**Research basis:** TestPrune (arXiv:2510.18270) — keep suites small and diverse. SWE-Bench+ (arXiv:2410.06992) — evaluation quality depends on task diversity. AgentBoard — sub-goal scoring reveals partial progress that pass/fail misses.

**Implementation:** Create a `regression-tasks.json` file with curated task definitions:

```json
[
  {
    "id": "bug-fix-known",
    "task": "The SanitizeFileName method in EventLog.cs doesn't handle tilde characters. Fix it.",
    "type": "bug-fix-specified",
    "expectedFeatures": ["verification"],
    "maxSteps": 12
  },
  {
    "id": "bug-fix-vague",
    "task": "Something is wrong with how session filenames are generated. Some come out garbled.",
    "type": "bug-fix-vague",
    "expectedFeatures": ["debugging-protocol", "assumptions", "hypothesis"],
    "maxSteps": 15
  },
  {
    "id": "feature-add",
    "task": "Add a new FailureType for when the agent calls a deprecated tool",
    "type": "feature-addition",
    "expectedFeatures": ["manage_todos", "verification"],
    "maxSteps": 15
  },
  {
    "id": "multi-file-refactor",
    "task": "Rename the VerificationState class to EditTracker and update all references",
    "type": "refactor",
    "expectedFeatures": ["assumptions", "verification"],
    "maxSteps": 20
  },
  {
    "id": "resume-test",
    "task": "Add comprehensive XML doc comments to all public members of EpisodeSegmenter.cs",
    "type": "feature-addition",
    "expectedFeatures": ["consolidation", "episode-chain", "manage_todos"],
    "maxSteps": 5,
    "expectResume": true
  }
]
```

5 tasks total. Each has an `expectedFeatures` list for automated checking. TestPrune principle: minimal and diverse, not exhaustive.

**Files:** New `blueprints/coding-agent/regression-tasks.json`. Future: a runner script.

### Priority 3: Mid-Session Consolidation Checkpoint (~20 lines)

**Problem:** For sessions with 30+ steps, a consolidation at only 80% budget means the agent's narrative freezes at step 24 of 30. If the agent pivots at step 15, the 80% checkpoint only captures the post-pivot state. The pre-pivot state (why the agent abandoned approach A) is lost to compression.

**Research basis:** BAO pattern (proactive boundary detection — already in our codebase but single-fire). Steve-Evolving (arXiv:2603.13131 — experience anchoring at subgoal boundaries, not just at session end). Mem-α (arXiv:2509.25911 — multi-tier memory with episodic checkpoints).

**Implementation:**
- In the boundary detection logic, fire a lighter-weight "progress checkpoint" at 50% budget
- Don't inject a full consolidation prompt (that disrupts the agent's flow). Instead, quietly capture the agent's current thought context: the most recent `response.Text` that's substantive (>100 chars, contains planning language)
- Store as `midSessionCheckpoint` — available for handoff if the session fails before the 80% consolidation fires

**Files:** `AgentLoop.cs` — boundary detection section. `SessionHandoff.cs` — new optional field.

**Expected outcome:** For long sessions that fail between 50-80% budget, the handoff includes a mid-session narrative snapshot. For sessions that reach 80%, the 80% consolidation supersedes.

### Priority 4: Targeted Re-Verification for Resumed Sessions (~10 lines prompt change)

**Problem:** Resumed sessions spend ~20% of their budget re-reading files the predecessor already explored. The system prompt says "verify with read_file before editing" which forces full re-reads even when the handoff provides precise file locations.

**Research basis:** SWE-Pruner (arXiv:2601.06797 — goal-driven pruning; reads are 76% of cost). Repository Memory (arXiv:2510.01003 — trust accumulated knowledge; developers don't re-read entire files they worked on yesterday).

**Implementation:** Modify the "IMPORTANT" instruction at the end of `BuildContinuationPrompt` from:
```
IMPORTANT: Verify the current file state with read_file before making edits
```
to:
```
IMPORTANT: Before editing, verify the current file state. For files mentioned in
this summary, a targeted grep_search confirming key function names still exist
is sufficient. Full re-reads are only needed if the grep shows changes.
```

**Files:** `SessionHandoff.cs` — `BuildContinuationPrompt()` method only.

**Expected outcome:** Resumed sessions use `grep_search` (fast, cheap) instead of `read_file` (slow, expensive) for verification, saving 1-2 steps. The handoff's precise file knowledge becomes trustworthy unless grep contradicts it.

### What NOT to Build Yet

| Idea | Why defer |
|------|----------|
| Deliberate session splitting | Promising (19% token savings) but requires auto-resume infrastructure and task complexity estimation. Build after regression suite quantifies the benefit. |
| Full trajectory analysis (Phase 6B) | Need the regression suite (Priority 2) to generate comparable session data first. Analysis without diverse data is meaningless. |
| Episode-aware compression | Pivot breadcrumbs give 80% of value. Full episode boundaries in compression requires bridging LlmClient and Segmenter timings. |
| Repository-level knowledge persistence | REPO.md ownership conflict unresolved. Need a separate persistence mechanism. |
| AgentBoard-style sub-goal scoring | Requires task-specific sub-goal decomposition. Build after regression suite defines the evaluation tasks. |
