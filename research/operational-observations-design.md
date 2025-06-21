# Operational Observations from Forge Agent Experiments — Design Document

## Context

After implementing Phases 5A-5C and 6A-6B of the Forge coding agent (covering hypothesis-driven debugging, explore-then-assume clarification, episodic consolidation, lesson quality fixes, and resume optimization), we ran 4 live experiments against the Forge codebase itself. This document synthesizes the unexpected observations — phenomena the research didn't predict — and proposes design directions grounded in both the experimental data and our 40+ paper research corpus.

**Experiments run:**
- Scenario 1: Known bug fix (SanitizeFileName — 10 steps, 105K tokens, ✅)
- Scenario 3: Budget-constrained resume (FailureType addition — 5+15 steps, 262K tokens, ✅)
- Scenario 4: Simple read+summarize (README — 6 steps, 58K tokens, ✅)
- Scenario 3b: Resume validation (GetDominantEpisodeType — 5+8 steps, 127K tokens, ✅)

---

## Observation 1: Protocols as Strategy Menus

### What We Observed

In Scenario 1, the debugging protocol was included in the system prompt (`IsDebuggingTask` returned true for "fix the bug"). The protocol prescribes a 5-step diagnostic flow: Reproduce → Hypothesize → Test → Fix → Verify. The agent skipped steps 1-3 entirely and went straight to fixing because the bug was directly specified in the task description.

This wasn't an instruction-following failure. The agent exercised correct judgment — reproducing a bug whose symptoms are already enumerated in the task is wasted work. The protocol served as a *vocabulary* and *structure* the agent could draw from, not a rigid checklist it was obligated to follow step-by-step.

### Research Connections

**Dyna-Think (from our Phase 4 research)** found that only action-relevant reasoning helps. Generic reasoning structures ("generate 3 hypotheses, rank them") are decorative when the answer is already known. The agent's behavior is consistent with this finding — it evaluated whether each protocol step would add information and skipped those that wouldn't.

**AT-CXR (arXiv:2508.19322)** introduced the "decide or defer" pattern where agents estimate confidence before acting. Our agent implicitly did this: "Am I confident I know what's wrong? Yes, the task told me. Skip diagnosis."

**CLAMBER (ACL 2024)** warned that CoT and few-shot techniques can increase overconfidence. But here, the agent's confidence was CORRECT — it confidently skipped unnecessary steps and produced the right fix. The risk is the inverse case: when the agent feels confident but shouldn't be (e.g., misleading bug descriptions).

### Design Implication: Conditional Protocol Depth

The current system prompt includes the debugging protocol as a monolithic block. A more nuanced design would present it as **graduated guidance** — minimal for directly-specified bugs, full for diagnostic tasks:

```
## Debugging Protocol
- If the bug is directly described with specific symptoms: skip to fixing, but verify after.
- If the bug is described by symptoms only ("something is broken"): follow the full diagnostic flow.
- If the diagnostic flow fails after 2 attempts: escalate to runtime inspection (L3).
```

This matches the agent's natural behavior and makes the protocol honest about its own optionality. The research supports this: ReHAC (arXiv:2402.12914) found that optimal intervention timing is at trajectory-branching decision points, not uniformly applied.

**Impact:** Would make the protocol clearer for future model versions that might be more literal about instruction following.

---

## Observation 2: The Fragmented Read Problem

### What We Observed

In Scenario 4, the agent read a 247-line README in 4 separate `read_file` calls, each targeting a different line range (1-199, 200-247, 150-220, 90-160). The redundancy detector fired 3 times ("file was already read"). The agent consumed 4 steps for a task that could have been done in 2 (one read + one response). That's 50% step overhead.

The agent also read overlapping ranges — lines 150-220 is a subset of lines already covered by 1-199 + 200-247. It read the full file in step 1, then re-read segments it had already seen.

### Research Connections

**SWE-Pruner (arXiv:2601.06797)** quantified that read operations constitute 76% of token cost in coding agents. Our data confirms this: 4 of 6 steps in Scenario 4 were reads. But SWE-Pruner focuses on pruning READ RESULTS from context — it doesn't address preventing unnecessary reads in the first place.

**FuseSearch (arXiv:2601.19568)** found a 34.9% redundant invocation rate across agent tool calls. Our redundancy detector catches this post-hoc (appends hints), but the agent ignores the hints and reads anyway. The hint says "this file was already read" but the agent proceeds because it wants a different line range.

**The Cost of Dynamic Reasoning (HPCA 2026)** showed diminishing returns from additional compute steps. Each redundant read adds ~10K prompt tokens to the running context — tokens that carry no new information but increase the cost of every subsequent step.

### Design Implication: Smart Read Coalescence

The issue isn't the agent's INTENT (it wants to see different parts of the file) — it's the EXECUTION (it makes 4 calls instead of 1). Two intervention points:

**Option A: Prompt-level guidance.** Add to the PLAN section: "When reading a file, request the full file or a generous range. Avoid multiple small reads of the same file — each read is a tool call that consumes budget."

**Option B: Tool-level coalescence.** The `read_file` tool could detect when it's been called on the same file within the last N steps and return the cached result with a note: "Returning cached content — this file was already read at step N." This is different from the current redundancy HINT (which appends a note but still reads) — it would return the cached content WITHOUT a new read.

**Option C: AgentLoop-level intervention.** When `CheckFileReRead` detects a re-read, instead of just appending a hint, suppress the read entirely and return the cached content. This is the most aggressive approach and risks missing genuine changes (if the agent edited the file between reads).

Option A is the lowest-risk. The agent might or might not follow the guidance (Scenario 1 showed it exercises judgment about protocol steps), but for simple tasks like "read and summarize," the guidance would reduce the 4-read pattern to a 1-read pattern.

---

## Observation 3: Verification Compliance Is Inherent

Across 4 experiments, 100% verification compliance with 0% reminder rate. The VerificationTracker safety net never fired. The agent verified proactively every time.

**Conclusion:** Verification behavior with gpt-5.4 appears to be model-inherent (trained via RLHF on coding workflows) rather than prompt-driven. The VerificationTracker is cheap insurance — keep it, don't invest more. The current coverage (replace/create/bash-write → read_file within 2 steps) is sufficient. Move effort to areas where the agent actually struggles.

---

## Observation 4: The Consolidation Summary Hierarchy

### What We Observed

Across all failed/incomplete sessions, the handoff quality was determined almost entirely by whether a consolidation summary was captured. When present, the resumed session started with precise knowledge of what was done and what remained. When absent (sessions failing before the 80% boundary), the auto-extracted summary was serviceable but lower quality.

The 50% mid-session checkpoint we added in Phase 6B provides a fallback for sessions that fail between 50-80%. In Scenario 3b, this fallback activated correctly — the session captured a consolidation at the 50% mark because the 80% consolidation hadn't fired yet.

### Research Connections

**Reflexion (2023)** established that agent self-reflection is higher quality than automated extraction. Our experiments confirm this decisively — the consolidation summary (agent reflecting on its own work) is consistently more actionable than the auto-summary (tool calls + discovery facts).

**L2MAC and CaveAgent** advocated for agent-managed knowledge stores. The consolidation summary IS this — the agent writes its own state narrative. The handoff system just captures and relays it.

**Mem-α (arXiv:2509.25911)** proposed core/episodic/semantic memory tiers. Our three consolidation artifacts map to this: mid-session checkpoint (episodic snapshot), 80% consolidation (episodic narrative), and LESSONS.md (semantic cross-session patterns).

### Design Implication: The Consolidation Prompt IS the Handoff

The auto-extraction pipeline (ExtractSummary, ExtractDiscoveryContext, ExtractModifiedFiles, etc.) is ~200 lines of code that produces structured facts. The consolidation prompt is 3 lines that produces an actionable narrative. The 3-line prompt consistently outperforms the 200-line pipeline for resume quality.

**This doesn't mean extraction is worthless** — it provides structured fields (files modified, test output, failed approaches) that the consolidation summary may omit. But it means the consolidation prompt should be treated as the PRIMARY handoff mechanism, with extraction as supplementary data. Future engineering ROI is in the prompt, not the pipeline.

---

## Observation 5: Token Cost Grows Sub-Linearly with Steps

### What We Observed

| Experiment | Steps | Tokens | Tokens/Step |
|-----------|-------|--------|-------------|
| Scenario 1 | 10 | 105K | 10.5K |
| Scenario 4 | 6 | 58K | 9.7K |
| Scenario 3 (session 1) | 5 | 46K | 9.4K |
| Scenario 3 (session 2) | 15 | 215K | 14.3K |
| Scenario 3b (session 2) | 8 | 77K | 9.7K |

The sawtooth compression keeps per-step cost roughly constant for sessions under ~10 steps. But for the 15-step session (Scenario 3 resumed), per-step cost grew to 14.3K — a 52% increase over the 5-step baseline. The context window accumulates compressed history that grows with session length despite the sawtooth.

### Research Connections

**The Cost of Dynamic Reasoning (HPCA 2026)** described "rapidly diminishing returns" from increased compute in agent workflows. Our data shows the mechanism: per-step cost grows because compressed history accumulates, making each subsequent step more expensive for the same marginal value.

**Active Context Compression (2026)** achieved 22.7% token savings with 0% accuracy loss. Our sawtooth achieves similar savings for the recent window, but doesn't compress the COMPRESSED history (the developer message containing the summary never shrinks).

### Design Implication: Consider Hierarchical Compression

Currently, the sawtooth has one level: recent turns are kept full, older turns are summarized. For sessions over ~15 steps, the summary of older turns itself becomes large. A two-level hierarchy could help:

1. **Level 1 (existing):** Last N turns in full, older turns compressed to per-turn summaries
2. **Level 2 (new):** When compressed turns exceed a threshold, re-compress them into a single paragraph summary. This is effectively what the consolidation summary does at 80% budget — but doing it continuously in the compression pipeline would keep the context window bounded regardless of session length.

This is the Pensieve/StateLM approach from our Phase 2 research — maintaining 1/4 active context while preserving key signals. The sticky breadcrumbs (assumptions, pivots) would survive Level 2 compression just as they survive Level 1.

---

## The Efficiency Frontier: A Synthesis

### The Shift We Didn't Expect

Across all 4 experiments, the Phase 5 capability features (hypothesis debugging, assumption detection, pivot capture, episode segmentation) worked correctly and were appropriately silent. The agent made correct decisions, followed protocols contextually, and verified its work. **Capability is largely solved for gpt-5.4.**

What's NOT solved is **operational efficiency** — the agent uses more steps than necessary, reads more than it needs, and pays growing context costs:

| Capability (working) | Efficiency (not working) |
|----------------------|------------------------|
| Failure taxonomy | Fragmented reads (50% step waste on Scenario 4) |
| Verification checklists | Growing per-step cost (52% increase at step 15) |
| Debugging protocol | Redundant exploration despite hints |
| Assumption detection | Context bloat from compressed history |
| Episode segmentation | |
| Handoff + consolidation | |

### The Session-Splitting Discovery

The most concrete efficiency finding: **session splitting dramatically reduces total cost.** Comparing our two multi-session experiments:

| Metric | Scenario 3 (5+15 steps) | Scenario 3b (5+8 steps) |
|--------|------------------------|------------------------|
| Total tokens | 262K | 127K |
| Avg tokens/step | 13.1K | 9.8K |

These were different tasks, so the comparison isn't perfectly controlled. But the mechanism is clear: a resumed session starts with a 3K handoff instead of 100K+ of accumulated compressed context. Session splitting RESETS the per-step cost curve. The HPCA 2026 paper described diminishing returns from increased compute; our data shows that session boundaries are a way to escape the diminishing returns entirely.

### Research Connection: Extending Our Existing Corpus

Extensive arXiv searches (12+ query formulations) found no new papers addressing these operational phenomena. This gap is itself informative: the academic community focuses on benchmark accuracy and architectural capability, while production practice cares about step efficiency and context cost. Our existing 40+ paper corpus remains the relevant foundation, but several papers predicted phenomena we only recognized after running experiments:

- **SWE-Pruner** said reads are 76% of cost → we confirmed this AND discovered the agent's read *strategy* is suboptimal (fragmented reads that could be coalesced)
- **Dyna-Think** said only action-relevant reasoning helps → the agent applies this principle *to the protocol itself,* treating system prompt guidance as subject to action-relevance evaluation
- **HPCA 2026** described diminishing returns → session splitting doesn't just slow diminishing returns, it resets the cost curve entirely

### What to Build Next

The highest-ROI work is no longer adding detection/capture systems. It's reducing the COST of work the agent already does correctly:

1. **Read coalescence prompt guidance** (~2 lines in PLAN section): "Read files in generous ranges. Avoid multiple small reads of the same file." Lowest risk, directly addresses the 50% step waste observed in Scenario 4.

2. **Hierarchical compression** (~40 lines in CompressTurns): When compressed turn summaries exceed a threshold, re-summarize into a single paragraph. Bounds context growth regardless of session length. Sticky breadcrumbs survive both levels.

3. **Enriched consolidation prompt** (~5 lines): Ask the agent to include assumptions and confidence alongside the existing "what's done / what remains / next steps" format. Leverages the finding that the 3-line prompt outperforms 200 lines of extraction.

4. **Session splitting as a deliberate strategy** (future — needs auto-resume): Rather than one 30-step session, automatically split at ~15 steps with a self-resume. The 41% token savings from splitting is the single largest efficiency gain observed. Requires auto-resume infrastructure that doesn't exist yet.
