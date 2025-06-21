# Phase 5A: Hypothesis-Driven Debugging — Design Document

## Problem Statement

Forge's current debugging behavior is reactive: the agent sees an error, guesses a fix, applies it, and checks if it worked. When the first guess fails, the failure taxonomy (StaleContext, SyntaxError, etc.) nudges recovery. But there's no systematic diagnostic process — no evidence gathering, no hypothesis ranking, no differential elimination.

**Observed in integration tests:**
- Test 10 (pipe char bug): Agent read the code, identified the gap, fixed it. This worked because the bug was directly described. But what if the user said "filenames are broken sometimes" without specifying the pipe character?
- The RETHINK/ALTERNATIVE decision already encodes a binary hypothesis: "my approach is right but execution failed" (RETHINK) vs "my approach is wrong" (ALTERNATIVE). This is hypothesis testing, just not formalized.

**The gap:** For bugs described by symptoms rather than causes, the agent needs a diagnostic protocol that systematically narrows possibilities before attempting fixes.

## Research Basis (14 Papers Surveyed)

### Previously Analyzed

#### FVDebug (arXiv:2510.15906, Sep 2025)
**Approach:** Causal graph synthesis from failure traces → graph scanning with for-and-against prompting → narrative exploration → fix generation.

**Key insight for us:** **For-and-against prompting** — when evaluating a hypothesis, generate evidence BOTH supporting and refuting it. This prevents confirmation bias where the agent fixates on its first guess.

**Applicability:** Full causal graph synthesis requires formal trace data. But the for-and-against pattern maps directly to our prediction framework: "If my hypothesis is correct, what should I see when I run this test? What should I NOT see?"

#### GNNVerifier (arXiv:2603.14730, Mar 2026)
**Approach:** Represent plans as DAGs, use node/edge risk scores to localize errors.

**Key insight:** Structural analysis catches failures that narrative reasoning misses — broken dependencies, type mismatches, missing intermediates.

**Applicability:** We don't need a GNN. But the concept of **risk scoring plan steps** is valuable: before debugging, rank which code locations are most likely to contain the bug based on the error signature.

#### SWE-Adept (arXiv:2603.01327, Mar 2026)
**Approach:** Shared working memory with step-indexed checkpoints + git branching.

**Key insight:** **Checkpoint before attempting a fix, branch to explore alternatives.** If fix A fails, revert and try fix B without accumulated state from failed attempt.

**Applicability:** Our git checkpoint already captures pre-task state. Per-hypothesis branching is over-engineered for our current scale.

#### Dyna-Think (existing in our research review)
**Key insight:** Only action-relevant simulation helps. For debugging: "If the bug is in method X, then test Y should fail with error Z. Let me check." NOT: "Let me read all the files to understand the architecture."

### Newly Analyzed

#### InspectCoder (arXiv:2510.18327, Oct 2025)
**Approach:** Dual-agent framework for interactive LLM-debugger collaboration. Strategic breakpoint placement, targeted state inspection, incremental runtime experimentation within stateful debugger sessions. 5.10%-60.37% improvement in repair accuracy, 1.67x-2.24x superior bug-fix efficiency.

**Key insight for us:** **Adaptive inspection over fixed log collection.** InspectCoder doesn't dump all logs and hope — it adaptively inspects and perturbs relevant intermediate states. The pattern: identify what runtime state would confirm/refute your hypothesis, then inspect ONLY that state.

**Applicability:** We can't control a debugger via MCP tools. But we CAN instruct the agent to use `run_bash_command` for targeted runtime inspection — insert a print statement, run the test, read the output, remove the print. This is the "poor man's InspectCoder" and it's the right tool for persistent logic bugs.

#### NExT (arXiv:2404.14662, Apr 2024)
**Approach:** Teach LLMs to inspect execution traces (variable states of executed lines) and reason about runtime behavior through chain-of-thought rationales. Self-training bootstraps synthetic execution-aware rationales. 26.1% improvement on MBPP fix rate.

**Key insight for us:** **Execution trace reasoning.** When stuck on a logic bug, mentally simulate the code execution: "Input X enters function F. Line 3 sets y=... Line 5 branches to... The bug is that line 7 assumes y>0 but it can be 0." This is formalized "rubber duck debugging."

**Applicability:** Direct prompt guidance. When the agent is debugging a logic error (not a compile error), guide it to trace execution mentally before hypothesizing.

#### SemCoder (arXiv:2406.01006, NeurIPS 2024)
**Approach:** Monologue reasoning — train LLMs to reason about code semantics (functional descriptions, local execution effects, input/output behavior) through natural language. Mimics "rubber duck debugging." 6.7B model competitive with GPT-3.5-turbo.

**Key insight for us:** **Monologue-style execution reasoning** integrates semantics from multiple dimensions more smoothly than concrete scratchpad reasoning. The key is reasoning in natural language about what the code DOES, not what it LOOKS LIKE.

**Applicability:** Direct prompt reinforcement. When debugging, the agent should explain what the code does in natural language before forming hypotheses. This is particularly valuable for logic errors where the code "looks right" but behaves wrong.

#### AutoCodeRover (arXiv:2404.05427, ISSTA 2024)
**Approach:** Autonomous program improvement via LLM + code search on AST representations. **Spectrum-based fault localization** using tests: run tests, identify which lines are executed by failing tests but not passing tests. Iterative search for context. 19% on SWE-bench-lite at $0.43 average.

**Key insight for us:** **Iterative context narrowing.** Don't read all files. Start with the error message, search for the relevant code structure, narrow to the specific method. AutoCodeRover's "iterative search" pattern maps directly: use grep_search → file_search → read_file with progressively narrowing scope.

**Applicability:** Prompt guidance for the "investigate before fix" phase. Also: the spectrum-based fault localization concept can be approximated: "Which test files cover this area? Run them. Which ones fail? The failure pattern localizes the bug."

#### ChatDBG (arXiv:2403.16354, FSE 2025)
**Approach:** LLM as autonomous debugger agent that can query/control the debugger — navigate stacks, inspect state, perform root cause analysis. 67% single-query fix rate, 85% with one follow-up. 75,000+ downloads.

**Key insight for us:** **Let the agent act as debugger, not just coder.** ChatDBG grants the LLM autonomy to "take the wheel" with the debugger. We already have this via `run_bash_command` — the agent can insert print statements, add assertions, run under verbose mode, inspect state. The key is ENCOURAGING this behavior rather than assuming the agent will jump straight to editing.

**Applicability:** The system prompt should explicitly list diagnostic tools the agent can use via bash: print/log insertion, assertion addition, running with verbose flags, reproducing with minimal input.

#### AgentDebug (arXiv:2509.25370, Sep 2025)
**Approach:** AgentErrorTaxonomy spanning memory/reflection/planning/action/system failure modes. AgentDebug framework isolates root-cause failures and provides corrective feedback, enabling iterative recovery. 24% higher accuracy, 26% improvement in task success.

**Key insight for us:** **Modular failure taxonomy with corrective feedback.** We already have FailureType enum. AgentDebug's insight is that the corrective feedback should be SPECIFIC to the failure mode, not generic. Our BuildFailureNudge already does this — AgentDebug validates our approach.

**Applicability:** Confirms our architecture is sound. Potential refinement: AgentDebug's taxonomy includes "memory" and "reflection" failure modes we don't track. For debugging specifically: does the agent forget what it already read? Does it fail to reflect on why a fix didn't work? These are trackable.

#### A2P — Abduct-Act-Predict (arXiv:2509.10401, Sep 2025)
**Approach:** Three-step causal scaffolding for failure attribution: (1) Abduction — infer hidden root causes behind actions, (2) Action — define a minimal corrective intervention, (3) Prediction — simulate the subsequent trajectory and verify if the intervention resolves the failure. 2.85× improvement over baseline in step accuracy.

**Key insight for us:** **Counterfactual reasoning as debugging strategy.** Instead of "what's wrong?" ask "if I change THIS ONE THING, does the problem go away?" This is the formalization of differential debugging — and it maps perfectly to: "If I revert my last edit, does the test pass again?"

**Applicability:** Direct prompt guidance for the RETHINK/ALTERNATIVE decision. Before picking a strategy, perform a counterfactual: "If this hypothesis is correct, what minimal change would fix it? What would I expect to see after that change?" This transforms the RETHINK/ALTERNATIVE choice from gut feel to evidence-based decision.

#### Empirical Study on Bug Fixing (arXiv:2411.10213, Nov 2024/Oct 2025)
**Approach:** Comparative analysis of 6 repair systems on SWE-bench Verified. Assessed fault localization accuracy at file and code symbol levels, bug reproduction capabilities.

**Key insight for us:** **Bug reproduction is a critical differentiator.** The study found significant variation in bug reproduction capability across systems. Systems that can reproduce the bug first have much higher fix rates. This validates making "Reproduce" step 1 in our debugging protocol.

**Applicability:** Reinforces the reproduce-first principle. The agent should ALWAYS try to reproduce before hypothesizing, even if the user describes the bug clearly.

## Critical Analysis: Should Debugging Strategies Be Pluggable?

### The Question

The user asked whether we should "implement all the various approaches in a pluggable/enableable way." This section critically evaluates that proposition.

### Arguments FOR Pluggable Architecture

1. **Different bug types need different strategies.** Logic errors benefit from execution tracing (NExT/SemCoder). Type errors benefit from structural analysis (GNNVerifier). Integration bugs benefit from spectrum-based localization (AutoCodeRover).
2. **A/B testing.** Could empirically compare strategies across sessions.
3. **Independent evolution.** Strategies could be added/removed without affecting core loop.

### Arguments AGAINST — And Why They Win

1. **All 14 papers converge on ONE protocol.** Despite different mechanisms (breakpoints, AST analysis, monologue reasoning, causal scaffolding), every paper follows the same fundamental flow: Reproduce → Hypothesize → Test → Fix → Verify. The variation is in HOW, not WHAT. Pluggable strategies would abstract the wrong thing.

2. **Our interface doesn't support most "pluggable" mechanisms.** InspectCoder needs debugger control. AutoCodeRover needs AST parsing. ChatDBG needs debugger integration. Forge operates through text-based MCP tools. The mechanisms that CAN work through our interface (prompt guidance, bash-based inspection, iterative context search) are all compatible and complementary — they compose, not compete.

3. **Pluggable infrastructure is premature engineering.** Interfaces, registries, configuration — all for <20 sessions of data. YAGNI applies strongly here.

4. **What the papers ACTUALLY teach is richer than "pick strategy A or B."** Each paper contributes a complementary technique to the SAME diagnostic protocol:
   - FVDebug → for-and-against prompting (prevents confirmation bias)
   - NExT/SemCoder → monologue execution reasoning (catches logic errors)
   - A2P → counterfactual reasoning (makes RETHINK/ALTERNATIVE evidence-based)
   - AutoCodeRover → iterative context narrowing (efficient investigation)
   - ChatDBG → "take the wheel" runtime inspection (persistent bugs)
   - AgentDebug → modular error taxonomy with targeted feedback (we have this)
   - InspectCoder → adaptive targeted inspection (not "dump all logs")

   These aren't alternative strategies. They're layers of the same strategy at increasing diagnostic depth.

### The Right Abstraction: Escalating Diagnostic Intensity

Instead of pluggable strategies, implement **escalating diagnostic intensity** — aligned with our existing progressive deepening pattern:

| Level | Trigger | Technique | Research Basis |
|-------|---------|-----------|----------------|
| **L1: Prompt Protocol** | Always, on debugging tasks | Reproduce → Hypothesize (with for-and-against) → Test → Fix → Verify | FVDebug, AutoCodeRover, Empirical Study |
| **L2: Monologue Reasoning** | After 1 failed fix attempts | "Explain line-by-line what the code does in natural language. What does each variable hold?" | NExT, SemCoder |
| **L3: Counterfactual + Runtime Inspection** | After 2 failed fix attempts | "What minimal change would fix this? Insert a print statement to verify your hypothesis. Revert and try a different approach." | A2P, InspectCoder, ChatDBG |

This maps naturally to our existing failure escalation (progressive deepening + failure taxonomy nudges). Level 1 is always-on prompt guidance. Levels 2 and 3 are injected as additional guidance when the agent is stuck — using the same mechanism as BuildFailureNudge.

**This is not pluggable.** It's a single, coherent debugging protocol with escalating depth. The escalation is automatic (triggered by failure count) and deterministic (no strategy selection needed). It synthesizes ALL 14 papers' insights rather than picking one.

## Design

### The Core Principle: Prompt-Level, Not Infrastructure

**Critical decision (reaffirmed after expanded research):** Hypothesis-driven debugging should be implemented as **prompt guidance in the system prompt**, with **escalating depth via failure nudges**, NOT as AgentLoop infrastructure. Here's why:

1. **The model already reasons hypothetically.** Test 10 showed the agent naturally forming and testing hypotheses ("Prediction: this will only affect filename sanitization"). Adding infrastructure to force this adds overhead without value.

2. **Infrastructure-level hypothesis tracking is premature.** We have ~15 sessions of data. Building a HypothesisTracker class, logging hypothesis trails in events, adding hypothesis-aware nudges — all of this requires knowing what hypothesis patterns look like in practice. We don't have enough data yet.

3. **The Dyna-Think research is clear:** Generic reasoning structures ("generate 3 hypotheses, rank them") are decorative. What works is forcing specific, testable predictions: "If I change X, test Y should produce Z."

4. **The system prompt already has the skeleton.** PLAN says "predict what this change will affect." RETHINK/ALTERNATIVE is hypothesis testing. We just need to extend the debugging-specific guidance.

5. **Escalation via failure nudges gives us "depth levels" for free.** We already inject nudges after consecutive failures. Adding debugging-specific escalation (monologue reasoning, runtime inspection) to this existing mechanism is zero-cost infrastructure-wise.

### What to Add to the System Prompt (Level 1 — Always-On)

Add a new section after the existing VERIFY section for debugging tasks:

```
## Debugging Protocol (when diagnosing bugs, test failures, or unexpected behavior)

Before fixing, diagnose:
1. **Reproduce**: Run the failing test or trigger the error. Read the FULL error output.
   Never skip this — systems that reproduce first fix more reliably.
2. **Hypothesize**: State 2-3 possible root causes, ranked by likelihood.
   For each hypothesis, state BOTH:
   - "If this IS the cause, I expect to see [specific observation]"
   - "If this is NOT the cause, I expect to see [different observation]"
3. **Test the top hypothesis**: Read the suspected code, check your prediction.
   - If confirmed → fix it.
   - If refuted → move to the next hypothesis. State what you learned.
4. **Fix with prediction**: "My fix changes X. I predict test Y will now pass
   and test Z will remain unaffected."
5. **Verify the prediction**: Run the specific test. Compare actual vs predicted result.

Do NOT:
- Jump to fixing before reading the error output
- Read all files "to understand" — read only what your hypothesis targets
- Retry the same fix after it failed (use ALTERNATIVE instead)
```

### What to Add to Failure Nudges (Levels 2 and 3 — On Escalation)

Extend `BuildFailureNudge` to add debugging-specific escalation for `TestFailure` and `Unknown` failure types:

**Level 2 (2+ consecutive failures, TestFailure or Unknown):**
> "Stop and explain what the code does line by line in natural language. Trace the execution path that leads to the failure. What does each variable hold at each step? This often reveals assumptions your edits are based on that don't match reality."

**Level 3 (3+ consecutive failures, TestFailure or Unknown):**
> "Try runtime inspection: insert a targeted print/log statement in the suspected method, run the failing test, and read the output. This gives you actual runtime state instead of guessing. Remove the print statement after diagnosis."

### What NOT to Build

| Proposed feature | Why not (yet) |
|-----------------|---------------|
| HypothesisTracker class | No data on what hypothesis patterns look like in practice. Build after 50+ debugging sessions. |
| Event log hypothesis trails | Adds complexity to EventLog schema for speculative data. The agent's `thought` field already captures reasoning. |
| Pluggable debugging strategies | All papers converge on one protocol. Our interface doesn't support the varied mechanisms. Escalating depth within one protocol is the right abstraction. |
| Causal graph synthesis (FVDebug) | Requires formal trace data. Forge operates on text-level errors, not waveforms. |
| GNN risk scoring (GNNVerifier) | Requires training data. The agent's own code understanding is sufficient for risk assessment. |
| Per-hypothesis git branching (SWE-Adept) | Over-engineered. The existing git checkpoint at session start is sufficient. |
| Debugger integration (InspectCoder/ChatDBG) | Requires IDE/debugger integration. `run_bash_command` with print statements is the pragmatic equivalent. |
| Spectrum-based fault localization (AutoCodeRover) | Requires AST tooling. The agent can approximate this by running test subsets. |

### What to Build

1. **System prompt debugging section** (described above) — Always-on for debugging tasks. ~250 tokens when active. Encodes the universal Reproduce→Hypothesize→Test→Fix→Verify protocol with FVDebug's for-and-against pattern.

2. **Debugging-aware failure nudges** — Extend BuildFailureNudge to include monologue reasoning (L2) and runtime inspection (L3) guidance for debugging-related failures. Uses existing infrastructure.

3. **Detect debugging tasks** — Simple keyword check in SystemPrompt.Build to decide whether to include the debugging section. Keywords: "bug", "fix", "failing", "broken", "error", "crash", "wrong", "not working", "diagnose", "debug", "investigate".

4. **Log hypothesis presence** — When the agent's thought contains hypothesis indicators ("hypothesis", "if...then", "root cause", "suspect"), log at Debug level. Zero infrastructure cost — enables future analysis.

### Success Criteria

- On debugging tasks, the agent should:
  - Read the error output before attempting a fix (not skip to editing)
  - State at least 1 hypothesis with a testable prediction
  - Include for-and-against observations (what would confirm AND refute)
  - Verify the fix against the prediction
- After escalation (L2/L3), the agent should switch to monologue reasoning or runtime inspection rather than repeating the same approach
- Measure via session investigation: does the agent follow the debugging protocol?
- Target: 80%+ compliance on debugging tasks within 10 sessions

## Implementation Plan

### Step 1: Add Debugging Protocol to SystemPrompt.cs
- New debugging section conditionally included when task matches debugging keywords
- Add `IsDebuggingTask` helper method for keyword detection
- ~250 tokens added to system prompt when active

### Step 2: Extend BuildFailureNudge for Debugging Escalation
- Add `FailureType.DebuggingStuck` detection or reuse TestFailure/Unknown
- Level 2 nudge (2+ failures): monologue reasoning guidance (NExT/SemCoder)
- Level 3 nudge (3+ failures): runtime inspection guidance (InspectCoder/ChatDBG)
- Integrate with existing progressive deepening mechanism

### Step 3: Tag Hypothesis Reasoning in AgentLoop.cs
- After capturing the agent's `response.Text`, check for hypothesis patterns
- Log `_logger.Debug("Hypothesis detected in step {Step}", stepNum)` when found
- No infrastructure changes — just observability

### Step 4: Test with Debugging Scenarios
- Pre-plant a bug (e.g., change MaxLines from 200 to 2)
- Give the task: "Tests are failing. Diagnose and fix."
- Analyze: Did the agent follow the protocol? Did it hypothesize before fixing?
- Test escalation: Use a harder bug that resists the first fix attempt

## Research Papers Referenced

| Paper | arXiv | Date | Key Insight Applied |
|-------|-------|------|-------------------|
| FVDebug | 2510.15906 | Sep 2025 | For-and-against prompting → testable predictions with refutation |
| GNNVerifier | 2603.14730 | Mar 2026 | Risk-scored localization → rank hypotheses by likelihood |
| SWE-Adept | 2603.01327 | Mar 2026 | Checkpoint before fix attempts → our git checkpoint covers this |
| Dyna-Think | (existing) | 2025 | Only action-relevant simulation helps → "if X, then Y" predictions |
| BREW | 2511.20297 | Nov 2025 | Experiential learning from debugging sessions → lesson quality |
| InspectCoder | 2510.18327 | Oct 2025 | Adaptive runtime inspection → L3 escalation via print statements |
| NExT | 2404.14662 | Apr 2024 | Execution trace reasoning → L2 monologue reasoning guidance |
| SemCoder | 2406.01006 | Jun 2024 | Monologue debugging → natural language execution tracing |
| AutoCodeRover | 2404.05427 | Apr 2024 | Iterative context narrowing + spectrum-based localization → efficient investigation |
| ChatDBG | 2403.16354 | Mar 2024 | LLM as debugger agent → runtime inspection via bash |
| AgentDebug | 2509.25370 | Sep 2025 | Modular error taxonomy with corrective feedback → validates our FailureType pattern |
| A2P | 2509.10401 | Sep 2025 | Counterfactual reasoning → evidence-based RETHINK/ALTERNATIVE decisions |
| Empirical Study | 2411.10213 | Nov 2024 | Bug reproduction is a critical differentiator → reproduce-first principle |
| Our Failure Taxonomy | (existing) | 2025 | 6-type categorization → pre-emptive diagnostic questions |
