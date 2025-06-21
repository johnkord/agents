# Improving Forge's Task Tracking: Research, Tensions, and Design

## The Problem

`manage_todos` is now a core tool (10 core tools). The agent instinctively calls it at the start of complex tasks to externalize its plan. The question: **how should we guide its use, and what system-level support should complement it?**

**Observed behavior from sessions:**
- Test 1 (IsEmpty): 7 steps, no todos used, 100% verification compliance. Simple task — todos would have been overhead.
- Test 2 (rename VerificationStats): 8 steps, `manage_todos` called at step 0. Used 1 step on bookkeeping. Task completed but hit step limit partially because of the overhead.
- Resume sessions: The handoff note captures discovery context and consolidation, but NOT the agent's plan state. A resumed session doesn't know what steps were planned vs. completed.

**Core tension:** Every step on bookkeeping is a step not on the task. But without externalized state, the plan is lost when context compresses — and resumed sessions start blind.

## Research Landscape

### 1. SWE-Adept: Shared Working Memory + Git Checkpoints (arXiv:2603.01327, Mar 2026)

**Key idea:** A two-agent framework (localization + resolution) where both share a **working memory** that stores code-state checkpoints indexed by execution steps. The resolution agent has specialized tools for **progress tracking** and **Git-based version control** — branching to explore alternatives, reverting failed edits. Achieves +4.7% resolve rate on SWE-Bench.

**Critical insight for us:** SWE-Adept's "working memory" is NOT a conversation — it's a structured state store external to the LLM context. Checkpoints are indexed by step number. This is exactly what `manage_todos` could become: not just a task list, but a **step-indexed checkpoint register** that records what was attempted, what worked, and what state the code is in after each action.

**Tension:** SWE-Adept uses two separate agents with a shared store. Forge uses one agent. The "shared working memory" concept still applies — the todo list IS the shared memory between the agent and the system.

### 2. SciBORG: "State and Memory is All You Need" (arXiv:2507.00081, Jun 2025)

**Key idea:** Agents augmented with **finite-state automata (FSA) memory** — persistent state tracking with explicit state transitions. Eliminates need for manual prompt engineering. Agents maintain context across extended workflows and recover from failures through state awareness.

**Critical insight for us:** SciBORG's FSA memory is the architectural version of our todo list. The state machine (`not-started → in-progress → completed`) is already implemented in ManageTodosTool. But SciBORG goes further: the FSA **governs** what actions are permissible from each state. An `in-progress` todo with a recorded failure could prevent the agent from retrying the same approach (connecting to AriadneMem's transition history).

**Tension:** Full FSA memory is heavy infrastructure. Forge's simpler approach — a typed todo list with validated transitions — captures 80% of the value at 10% of the complexity. The question is whether to add failure annotation to todos (richer state) or keep them simple.

### 3. COMPASS: Hierarchical Context Organization (arXiv:2510.08790, Oct 2025)

**Key idea:** Separates tactical execution, strategic oversight, and context organization into three components: Main Agent (reasoning + tools), **Meta-Thinker** (monitors progress, issues strategic interventions), and **Context Manager** (maintains concise progress briefs). Improves accuracy by up to **20% relative** on GAIA, BrowseComp, HLE.

**Critical insight for us:** COMPASS's "Context Manager" maintains **progress briefs** — concise summaries of what's been accomplished tailored to the current reasoning stage. This is the automated version of what `manage_todos` does manually. The 20% improvement comes from the progress brief being always up-to-date and injected at the right time, not from the agent managing it.

**Tension:** COMPASS uses 3 separate components (multi-agent). Forge is single-agent. But the Context Manager role could be played by the AgentLoop itself — auto-generating a "progress brief" from tool call history rather than requiring the agent to maintain it.

### 4. Conversational Planning (arXiv:2502.19500, Feb 2025)

**Key idea:** LLM acts as **meta-controller** deciding the next macro-action, with tool-augmented option policies executing each macro-action. Applied to personal plans that span days/weeks/months across multiple sessions.

**Critical insight for us:** The meta-controller pattern is what our consolidation summary + discovery context already implements for cross-session planning. The novel part here is **macro-actions** — not individual tool calls but higher-level operations like "read and understand the implementation" or "edit and verify." Our todo items ARE macro-actions: "Add IsEmpty() method" is a macro that decomposes into read → edit → verify.

### 5. Structured Cognitive Loop (SCL) — arXiv:2511.17673, Nov 2025

**Key idea:** 5-phase R-CCAM architecture with symbolic plan governance. Zero policy violations, eliminates redundant tool calls, complete decision traceability. "Soft Symbolic Control" applies constraints to probabilistic inference.

**Relevance:** Strongest argument for the todo list as a governance tool — not just tracking but *constraining* the agent's actions. An agent that's "in-progress" on step 2 shouldn't be starting step 4.

### 6. Agent Workflow Optimization (AWO) — arXiv:2601.22037, Jan 2026

**Key idea:** Meta-tools bundle recurring tool-call sequences. Reduces LLM calls by 11.9% and increases task success rate by 4.2%.

**Relevance:** The overhead of plan management should be minimized. If the agent always does "plan → file_search → read_file → edit → verify", the plan step could be automated or eliminated.

### 7. CaveAgent (2026) — From research review

**Key finding:** Persistent state manipulation outperforms stateless by +10.5%.

### 8. MemoryOS (arXiv:2506.06326) — 3-tier memory

**Key insight:** Short-term (conversation), mid-term (session facts), long-term (cross-session patterns). Todo = mid-term memory.

### 9. Dyna-Think / Brittle ReAct — From research review

**Key finding:** Generic reasoning traces are decorative. Only action-relevant simulation helps. Plans should contain predicted outcomes.

## The Core Tension

**Bookkeeping cost vs. memory benefit.**

| Approach | Benefit | Cost |
|----------|---------|------|
| **Always use todos** | Plan survives compression; visible progress; handoff context | 1+ step overhead per session; token cost; over-planning simple tasks |
| **Never use todos** | Zero overhead; all steps on task | Plan lost on compression; harder to resume; no progress visibility |
| **Agent decides** | Optimal for each task | Wastes a step deciding; inconsistent behavior |
| **System-level auto-tracking** | Zero agent overhead; always available | No agent buy-in; plan may not match agent's intent |

## Recommended Design: Prompt Guidance + Todo-Aware Handoff

### Design Constraints

- **Don't auto-inject manage_todos calls.** That adds tokens without the agent understanding what it's tracking.
- **Don't make it mandatory.** Simple tasks (info queries, single-file edits) don't need plan tracking. Test 1 proved this — 7 steps, no todos, perfect execution.
- **Don't use it for sub-step verification.** That's what `VerificationTracker` does. Todos are for task-level progress, not tool-level compliance.
- **Don't auto-generate progress briefs from tool calls (yet).** This requires NLP pattern matching and the sticky file summaries in compression already provide 70% of the value. Revisit when we have 50+ session traces to analyze patterns.

### Implementation Plan

#### Phase 1: Prompt Guidance + Todo-Aware Handoff (Effort: Low-Medium)

Two changes, shipped together:

**1a. System prompt guidance.** Add to the PLAN section:
```
For complex tasks with 3+ steps, use manage_todos to externalize your plan.
This preserves your progress across context compression and session interruptions.
For simple tasks (single edit, info query), skip it — plan in your reasoning.
```

This gives the agent clear decision criteria without mandating usage.

**1b. Todo-aware handoff.** In `HandoffGenerator`, read the persisted todo file (`~/.forge/memories/session/todos.json`) and include it in the handoff note:
```
Plan state:
  1. ✅ Read implementation file (step 0)
  2. ✅ Add IsEmpty() method (step 2)
  3. ⬜ Add unit test (not started)
  4. ⬜ Run test suite (not started)
```

This gives resumed sessions a structured plan with completion status — dramatically better than "Completed 5 steps. Tools used: file_search, read_file."

### Future Research Directions

These are informed by the research landscape but NOT implementation targets. They represent potential Phase 2+ work if Phase 1 proves valuable.

| Direction | Research basis | What it would mean for Forge | Why wait |
|-----------|---------------|------------------------------|----------|
| **Auto progress brief** | COMPASS (+20% on GAIA) | AgentLoop auto-generates "Progress: 3/5 done" from tool history, injects into compressed context | Sticky summaries already do 70% of this. Need more session data to justify complexity. |
| **Step-indexed working memory** | SWE-Adept (+4.7% SWE-Bench) | Todos annotated with `failedAt: step 3, reason: "oldString not found"`. Plan records failure history. | Current LESSONS.md + handoff discovery context cover this across sessions. Within-session value unclear. |
| **FSA plan governance** | SCL (zero violations), SciBORG (FSA memory) | Todo state machine constrains actions — can't start step 4 while step 2 is in-progress. | Over-constrained for coding where discovery changes the plan. Agent flexibility > rigid governance. |
| **Macro-action decomposition** | Conversational Planning (meta-controller) | Todos as macro-actions that auto-decompose into tool sequences. | Requires tool-sequence pattern mining (AWO). Need 50+ traces first. |

## Research Papers Referenced

| Paper | arXiv | Date | Key Insight |
|-------|-------|------|-------------|
| SWE-Adept | 2603.01327 | Mar 2026 | Shared working memory with step-indexed checkpoints + git branching for alternatives. +4.7% on SWE-Bench. |
| SciBORG | 2507.00081 | Jun 2025 | FSA memory for persistent state tracking. State + memory = robust agents. |
| COMPASS | 2510.08790 | Oct 2025 | Hierarchical: Main Agent + Meta-Thinker + Context Manager. Auto-progress briefs. +20% on GAIA. |
| Conversational Planning | 2502.19500 | Feb 2025 | LLM as meta-controller selecting macro-actions. Multi-session planning across days/weeks. |
| Structured Cognitive Loop (SCL) | 2511.17673 | Nov 2025 | 5-phase R-CCAM with symbolic plan governance. Zero policy violations but rigid. |
| Agent Workflow Optimization (AWO) | 2601.22037 | Jan 2026 | Meta-tools bundle recurring patterns. Reduce LLM calls 11.9%, +4.2% success. |
| CaveAgent | (in review) | 2026 | Persistent state manipulation +10.5%. Filesystem is the checkpoint. |
| MemoryOS | 2506.06326 | May 2025 | 3-tier memory (short/mid/long). Todo = mid-term memory. |
| Dyna-Think | (in review) | 2025 | Only action-relevant reasoning helps. Generic plans are decorative. |
