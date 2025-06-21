# Phase 5B: Proactive Clarification — Design Document

## Problem Statement

Forge currently operates as a fully autonomous agent: it receives a task string, runs to completion (or resource exhaustion), and returns a result. There is no mechanism for the agent to pause, ask the user a question, and incorporate the answer before continuing.

This creates three failure modes:

1. **Ambiguity leads to wasted work.** "Fix the auth bug" could mean the login flow, the token refresh, or the session expiration. The agent guesses, spends 15 steps exploring the wrong hypothesis, and exhausts its budget.

2. **Missing context leads to bad assumptions.** "Add a caching layer" — but the user wants Redis caching, and the agent implements in-memory caching. No mechanism to surface "I'm going to use MemoryCache — is that what you want?" before committing.

3. **Irreversible decisions get made silently.** "Refactor the service layer" — should this preserve the existing API contract? Delete deprecated methods? The agent makes irrevocable choices without confirmation.

## How Existing Systems Handle This

### Claude Code

Claude Code operates as an **interactive terminal agent** with a permission-based autonomy model:

- **Permission tiers**: `allow` (auto-approve), `ask` (prompt user), `deny` (block). Configured per-tool via settings files (user, project, managed scopes).
- **No explicit "ask clarification" mechanism**: Claude Code doesn't have a dedicated "ask the user" tool. Instead, it uses the natural conversational interface — it can simply respond with text asking a question, and the user types back.
- **Interaction Frequency Dilemma**: DS-IA paper (arXiv:2603.16207) identifies this as a core tension — existing frameworks oscillate between "reckless execution" (autonomous but wrong) and "excessive user questioning" (correct but annoying). Claude Code manages this via the permission system, not an intelligent clarification mechanism.
- **Plan Mode**: A recent addition (2026) — a dedicated read-only planning phase that researches the codebase, produces a structured plan with **open questions for clarification**, and waits for user iteration before executing. This is the closest analog to proactive clarification: gather context first, surface ambiguities, get confirmation, THEN act.

**Key insight**: Claude Code's approach is conversational-native. The user is sitting at a terminal. Asking a question costs nothing architecturally — it's just another response turn. The overhead is purely in user attention/latency.

### GitHub Copilot

GitHub Copilot handles clarification through **mode separation** rather than mid-execution questioning:

- **Ask mode**: Q&A only, no code changes. The user asks, Copilot answers. Perfect for clarification BEFORE starting work.
- **Edit mode**: User provides files + prompt. Copilot proposes changes. The user reviews and accepts/rejects. Clarification happens through the user narrowing the working set and refining the prompt.
- **Agent mode**: Fully autonomous — determines files, proposes changes, suggests terminal commands. The agent iterates to complete the task. Clarification happens implicitly: the agent runs a command, sees output, adapts. But it can also stream its reasoning to the user and wait for command confirmations (explicit user gates on terminal commands).
- **Plan mode**: Like Claude Code's — generate a high-level plan with **open questions about ambiguous requirements**, iterate with user, then hand off to agent mode for execution.

**Key insight**: Copilot's system prompt (visible in this conversation) instructs the agent to "infer the most useful likely action and proceed" rather than asking. The philosophy is bias-to-action with user review gates, not proactive questioning.

### The Emerging Pattern: Plan-Then-Execute

Both Claude Code Plan mode and Copilot Plan mode converge on the same architecture:

```
Phase 1 (Read-Only): Research codebase, identify plan, surface ambiguities as "open questions"
  ← User clarifies
Phase 2 (Edit): Execute the agreed plan
```

This separates clarification from execution. The agent never needs to pause mid-edit to ask a question — all clarification happens in the planning phase.

## The Autonomy Spectrum: Where Should Forge Sit?

### Fully Autonomous Agents

**Current state**: Forge, SWE-bench contestants, Devin, OpenHands.

**Clarification approach**: None. Receive task description, execute. All ambiguity resolved through inference, codebase analysis, or trial-and-error.

**When this works**: Tasks with clear success criteria (tests pass, build succeeds, specific error is fixed). SWE-bench tasks are fully specified by issue descriptions + test patches.

**When this fails**: Underspecified tasks, design decisions, and preference-dependent choices. "Make the API more user-friendly" has no clear success criterion an autonomous agent can verify against.

**Should autonomous agents want clarification?** **No — and here's the counterintuitive argument.** The entire value proposition of a fully autonomous agent is that you fire-and-forget. Adding clarification questions destroys the operational model. If you're monitoring the agent anyway to answer questions, you might as well review its work directly. Autonomous agents should instead be given better-specified tasks, not the ability to ask vague questions.

### Semi-Autonomous Agents

**Examples**: Claude Code (terminal), Copilot Agent mode, Cursor.

**Clarification approach**: Conversational — the user is present and can respond.

**When this works**: Developer-in-the-loop workflow. The developer is working alongside the agent, can quickly answer "which auth module?" or "Redis or in-memory?".

**When this fails**: Background/batch execution, long-running tasks where the developer goes to lunch, CI/CD pipelines. Any scenario where the user isn't watching.

**What would semi-autonomous Forge look like?** A CLI mode where the agent can pause, print a question, read stdin for the answer, and continue. The architecture is simple. The design questions are:

1. **When should it ask?** (Too rarely → wrong work. Too often → annoying.)
2. **How long should it wait?** (Timeout → proceed with assumptions? Or block forever?)
3. **What happens if asked during compression?** (User's answer needs to survive context window management.)

### The Forge-Specific Reality

Forge is currently deployed as a CLI tool (`dotnet run --project Forge.App -- "task"`). Users invoke it, watch token streaming, and get a result. This is semi-autonomous in practice — the user IS present (watching the output), but there's no reverse channel for conversations.

**The minimal viable interaction**: Forge pauses, prints a question, reads a line from stdin, injects the answer as a user message, and continues.

**The concern**: This breaks the current operational model where Forge runs non-interactively (e.g., in a script, via resume, in CI). Any clarification mechanism needs a clean fallback for non-interactive contexts.

## Research Basis (13 Papers Surveyed)

### ★ Ambig-SWE (arXiv:2502.13069, ICLR 2026) — The Definitive Paper

**This is the single most important paper for Phase 5B.** From CMU (Neubig lab), it directly studies underspecificity in software engineering agents, accepted at ICLR 2026.

**What they built:** An underspecified variant of SWE-Bench Verified, where fully-specified GitHub issues are synthetically summarized to remove key details. Three evaluation settings: Full (complete info), Hidden (underspecified, no interaction), and Interaction (underspecified + can query a simulated user proxy).

**The three capabilities they decompose:**
1. **Detecting underspecificity** — Can the agent tell when information is missing?
2. **Asking targeted clarification** — Does it ask the RIGHT questions?
3. **Leveraging interaction** — Does it USE the answers effectively?

**Critical findings:**

- **74% performance recovery through interaction.** Claude Sonnet 4 recovers 89% of fully-specified performance. This conclusively proves that clarification is high-value.
- **Models default to non-interactive behavior without explicit prompting.** Even with severely underspecified inputs, agents proceed autonomously. They *never* ask unless explicitly prompted to.
- **Detection is the hardest step.** Only Claude Sonnet 4 (89%) and Sonnet 3.5 (84%) achieve notable accuracy in distinguishing underspecified from well-specified tasks. Qwen 3 Coder achieves 100% false negative rate — it *never* detects underspecificity regardless of prompt.
- **Exploration-first strategy is optimal.** Claude Sonnet models explore the codebase FIRST, then ask 3-4 targeted questions about what can't be independently discovered. This achieves comparable information gain to models asking 6+ questions. Quality >>> quantity.
- **Navigational vs. informational questions.** Asking "which file?" is less valuable than asking "what is the expected behavior?" because skilled agents can navigate codebases themselves. The highest-value questions are about *intent* and *behavior*, not *location*.
- **Prompt engineering alone is insufficient for detection.** Varying interaction prompts (Neutral → Moderate → Strong encouragement) produces wildly inconsistent results across models. This is NOT a problem solvable purely at the prompt level.

**Key insights for Forge:**

1. **Exploration-first, then ask.** Don't ask before reading the codebase. Read first, identify what the codebase can't tell you, THEN ask (or state assumptions about) the remaining unknowns.
2. **Focus assumptions on behavior/intent, not location.** "I'm assuming the expected behavior is X" is more valuable than "I'm assuming the bug is in file Y" because the agent can discover file locations.
3. **Detection is THE hard problem.** Building an `IsUnderspecifiedTask` heuristic is one thing, but teaching the agent to *notice* when it's missing information mid-reasoning is fundamentally harder and more valuable.
4. **3-4 targeted assumptions > 6+ vague ones.** Quality over quantity applies exactly.

### DS-IA — Dual-Stage Intent-Aware Framework (arXiv:2603.16207, Mar 2026)

Addresses the exact problem: agents oscillate between reckless execution and excessive questioning.

**Solution:** Two-stage architecture:
- Stage 1: **Semantic firewall** — filter invalid instructions, resolve vague commands by checking current state. Only ask the user when ambiguity is **irreducible** (can't be resolved by examining the environment).
- Stage 2: **Deterministic cascade verifier** — step-by-step rule checking before execution.

**Results:** Autonomous Success Rate increased from 42.86% to 71.43% while maintaining high precision in identifying irreducible ambiguities.

**Key insight for Forge:** **Most ambiguity is resolvable by reading the codebase.** "Fix the auth bug" → grep for auth-related test failures. If there's one failing test, no need to ask. Only surface assumptions when the codebase itself doesn't disambiguate. This aligns perfectly with Ambig-SWE's exploration-first finding.

### InteractComp (arXiv:2510.24668, Oct 2025)

**Benchmark for agents handling ambiguous queries.** Found that the best LLM achieves only 13.73% accuracy on ambiguous queries (vs 71.50% with complete context).

**Shocking finding:** *"Forced interaction produces dramatic gains, demonstrating latent capability current strategies fail to engage."* Agents CAN interact effectively — they just don't because they're trained to be autonomous.

**Finding #2:** *"Interaction capabilities stagnated over 15 months while search performance improved seven-fold."* The industry is building better autonomous reasoning but NOT better clarification behavior.

**Key insight for Forge:** Agents have a systematic overconfidence problem. Ambig-SWE confirms this — even capable models like Qwen 3 Coder NEVER ask, despite having strong underlying capability. Explicitly enabling assumption-stating captures latent capability.

### ClarQ-LLM (arXiv:2409.06097, Sep 2024)

**Benchmark for clarification questions in task-oriented dialog.** Even the best model achieves only 60.05% task success through clarification.

**Key insight:** Asking GOOD clarification questions is itself hard. Models ask too broadly ("What do you want?"), ask about already-answerable details, or ask at wrong times. Ambig-SWE's data reinforces this: Llama 3.1 asks only 2.61 questions (too few, too vague) while Qwen 3 asks 6.02 (too many, including recoverable details). Claude's 3.8-4.0 targeted questions is the sweet spot.

### DERA — Dialog-Enabled Resolving Agents (arXiv:2303.17071, Mar 2023)

**Two-agent framework**: Researcher (gathers info, identifies gaps) → Decider (integrates and acts). The dialog surfaces unknowns.

**Key insight for Forge:** The Researcher/Decider pattern maps to Plan/Execute. The planning phase acts as an internal "Researcher" that identifies unknowns before the "Decider" executes. In a non-interactive deployment, the Researcher's unknowns become documented assumptions.

### ClarifyGPT (arXiv:2310.10996, Oct 2023)

Referenced by Ambig-SWE — empowers LLM code generation with intention clarification. Generates clarifying questions to resolve ambiguity before code generation.

**Key insight:** Test-driven workflows that generate test cases from clarified intent, then validate with users, achieve significant improvements. This maps to our existing PLAN→ACT→VERIFY workflow: the plan should include testable predictions that implicitly encode assumptions.

### Conversational Planning (our existing research, COMPASS arXiv:2412.18270)

From our Phase 4 research — COMPASS showed structured planning with user alignment improves task success. GitHub Copilot and Claude Code both adopted this as Plan mode.

### Where LLM Agents Fail / AgentDebug (arXiv:2509.25370, Sep 2025)

From our Phase 5A research — AgentErrorTaxonomy includes "planning failure" modes. The relevant failure, for 5B: the agent plans based on wrong assumptions it never validated. This is an assumption-quality problem, not a planning-quality problem.

### ★ Ask-when-Needed / NoisyToolBench (arXiv:2409.00557, Aug 2024, updated Feb 2025)

**Directly addresses tool-use under imperfect instructions.** Analyzes real-world user instructions, identifies error patterns when args are missing, and proposes the "Ask-when-Needed" (AwN) framework.

**Critical finding:** Due to next-token prediction training, LLMs tend to **arbitrarily generate missed arguments** when instructions are unclear — hallucinating parameter values rather than acknowledging they're missing. This is the mechanistic explanation for Ambig-SWE's overconfidence finding.

**The AwN framework:** Prompts LLMs to ask questions whenever they encounter obstacles from unclear instructions. Significantly outperforms existing approaches on NoisyToolBench.

**Key insight for Forge:** The hallucination mechanism is the same for coding tasks. When the user says "fix the bug," the agent hallucinates a specific bug rather than acknowledging it doesn't know WHICH bug. AwN's approach — make the agent stop and ask at the moment of confusion — maps to our "state assumption at the point of uncertainty" guidance. But crucially, AwN shows this works at the tool-call level (missing args), not just at the task level (vague instructions). This suggests our assumption-stating should be triggered not just during PLAN, but also when the agent is about to make a tool call with uncertain arguments.

### TENET: Tests as Executable Specifications (arXiv:2509.24148, Sep 2025)

**Test-Driven Development for LLM agents in repository-level code.** Key framing: in the "vibe coding" era, test cases serve as **executable specifications that explicitly define and verify intended functionality beyond what natural-language descriptions can convey.**

**Results:** 69.08% Pass@1 on RepoCod, beating baselines by 9.49pp. Three innovations: (1) test harness selecting concise diverse test suite, (2) tailored agent toolset for efficient retrieval, (3) reflection-based refinement.

**Key insight for Forge:** Tests ARE the specification. When the task is underspecified, the agent can sometimes resolve ambiguity by reading existing tests — the tests encode what the user intended. This is a third type of exploration beyond code and documentation: **test archaeology.** If `AuthControllerTests.TestTokenRefresh` expects 24h expiry, that IS the specification. The agent doesn't need to ask OR assume — the test tells it. This extends the exploration-first strategy: explore code → explore tests → THEN assume about what neither code nor tests reveal.

### CLAMBER: Identifying and Clarifying Ambiguous Information Needs (arXiv:2405.12063, ACL 2024)

**12K-example benchmark with taxonomy of ambiguity types.** Found that CoT and few-shot prompting may result in **overconfidence** in LLMs, yielding only marginal improvements in identifying ambiguity. LLMs fall short in generating clarifying questions due to **lack of conflict resolution** and **inaccurate utilization of inherent knowledge.**

**Key insight for Forge:** CLAMBER identifies a failure mode we haven't accounted for: the agent may THINK it understands (because it has inherent knowledge about similar concepts) but its knowledge is stale or wrong for the specific codebase. Example: "add caching" — the agent has training knowledge about caching patterns, so it doesn't feel confused even though it doesn't know THIS codebase's caching conventions. Prompting techniques make this WORSE because they increase confidence. This reinforces Ambig-SWE's finding that detection is the hard problem — the agent must distinguish "I know about caching in general" from "I know what caching pattern THIS codebase uses."

### ReHAC: RL-based Human-Agent Collaboration (arXiv:2402.12914, Feb 2024)

**Uses reinforcement learning to learn WHEN to involve humans.** Trains a policy model to determine the most opportune stages for human intervention. Results show well-planned, limited human intervention significantly improves performance.

**Key insight for Forge:** The optimal intervention timing is NOT at the start (before any work) and NOT distributed throughout — it's at **decision points where the trajectory will diverge based on interpretation.** For coding, these are: (1) before choosing which approach to take, (2) before modifying a file that changes behavior, (3) when multiple tests could be the "right" one to fix. ReHAC validates our "before any file-modifying tool call" constraint for future interactive mode, but deepens it: the policy should be aware of how many possible trajectories branch from this point.

### AT-CXR: Uncertainty-Aware Agentic Triage (arXiv:2508.19322, Aug 2025)

**An agent that estimates per-case confidence and distributional fit, then follows a stepwise policy to either issue an automated decision or abstain with a suggested label for human intervention.** Medical imaging domain, but the architecture transfers directly.

**Key insight for Forge:** The "decide or defer" framing is precisely what we need for underspecificity. The agent should estimate its confidence in its interpretation of the task, and if below a threshold, **defer** by stating assumptions rather than silently proceeding. AT-CXR's two router designs — deterministic rule-based vs LLM-decided — map to our two-tier approach: heuristic detection (`IsUnderspecifiedTask`) for the rule-based tier, and future LLM-based detection for the adaptive tier.

### TheAgentCompany (arXiv:2412.14161, Dec 2024)

**Benchmark simulating a real software company.** Agents interact with websites, write code, run programs, and **communicate with simulated coworkers.** The best agent completes only 30% of tasks autonomously.

**Key insight for Forge:** TheAgentCompany includes tasks that inherently require communication with coworkers (clarification, delegation, coordination). The 30% completion rate on FULLY SPECIFIED company tasks means the real bottleneck isn't specification quality — it's the long-horizon multi-step nature of real work. This tempers expectations: even with perfect clarification, coding agents face fundamental capability limits. Stated assumptions help at the margins but aren't a silver bullet.

## Cross-Paper Synthesis: Five Converging Insights

After reviewing 13 papers across 3 years (2023-2026), five meta-insights emerge that go beyond what any individual paper identifies:

### Insight 1: The Overconfidence-Hallucination Pipeline

Three papers describe the same mechanism from different angles:
- **AwN**: Next-token prediction training causes LLMs to hallucinate missing arguments
- **CLAMBER**: Inherent knowledge creates false confidence in understanding ("I know about caching → I understand this caching task")
- **Ambig-SWE**: Models proceed autonomously even with severely underspecified inputs

These aren't three different problems. They're ONE pipeline: `training bias → false confidence → silent hallucination → wrong execution`. Our design must interrupt this pipeline at the "false confidence" stage, before it reaches "silent hallucination." Stated assumptions do this by forcing the confidence from implicit to explicit.

### Insight 2: The Exploration Hierarchy

Multiple papers converge on a layered exploration strategy:
1. **Code structure** (AutoCodeRover, DS-IA): Navigate files and methods
2. **Runtime behavior** (InspectCoder, ChatDBG from 5A): Execute and observe
3. **Test specifications** (TENET — NEW): Tests encode intended behavior as executable specs
4. **User intent** (Ambig-SWE, AwN): Only ask/assume about what layers 1-3 can't resolve

This hierarchy is the design principle: explore ALL cheaper layers before surfacing assumptions about the expensive layer (user intent). Our current design already has layers 1 and 4. **TENET adds layer 3 as a critical middle tier** — the agent should read existing tests to infer intent before assuming.

### Insight 3: The Intervention Timing Paradox

- **ReHAC**: RL learns that optimal intervention happens at trajectory-branching decision points
- **Ambig-SWE**: Detection is measured in the first 3 turns because models who miss it early never recover
- **AT-CXR**: Decide or defer must happen BEFORE committing to action

The paradox: the best time to ask/assume is at the beginning (before wasted work), but the agent doesn't KNOW what it doesn't know until it explores. The solution is the exploration-first pattern — but it creates a risk: by the time exploration reveals the ambiguity, the agent may already be deep enough to feel committed to its approach.

**Design implication:** The system prompt should instruct: "State assumptions AFTER initial exploration but BEFORE your first file-modifying tool call." This creates a hard temporal gate that prevents the paradox from materializing.

### Insight 4: Tests as Implicit Clarification

TENET's framing — tests as executable specifications — provides a creative alternative to explicit clarification. Instead of asking "what should the expiry be?" the agent can read `TestTokenRefresh` and discover the answer encoded in an assertion. This is "clarification through archaeology" — the codebase itself contains the user's original intent, encoded in tests written (sometimes years) before the current task.

**Design implication:** Add to the exploration-first guidance: "Before stating assumptions, check if existing tests already specify the expected behavior. Tests often encode intent more precisely than task descriptions."

### Insight 5: The Stated Assumption as Dual-Use Artifact

Our "Stated Assumptions" design serves two separable purposes that reinforcing each other:

1. **For the user** (if watching): Allows Ctrl+C and re-specification before wasted work
2. **For the LLM itself**: Forces explicit reasoning that improves output quality (SemCoder/NExT monologue effect from 5A)

The AwN and CLAMBER findings strengthen purpose #2: the act of articulating "I'm assuming X" interrupts the overconfidence-hallucination pipeline even without user feedback. The agent that says "I'm assuming 24h token expiry" is forced to consider whether that assumption is justified — a metacognitive check that implicit assumptions skip entirely.

This dual-use nature means stated assumptions provide value even in fully autonomous deployments where no user is watching. The assumption isn't just a message TO the user — it's a cognitive discipline FOR the agent.

## Critical Analysis: The Interaction Frequency Dilemma (Revised After Ambig-SWE)

### Revising the "Autonomous Agents Don't Need This" Argument

The initial design argued that fully autonomous agents shouldn't want clarification because it destroys the fire-and-forget model. **Ambig-SWE's ICLR 2026 data forces a revision of this position.**

The key data point: Claude Sonnet 4 in the Hidden (autonomous) setting solves 46.0% of underspecified tasks. With interaction, it solves 60.8% — a gain of 14.8 percentage points, or **32% relative improvement**. More strikingly, across ALL models, interaction is *statistically significant* (p < 0.05, Wilcoxon signed-rank test) for every single one.

**What this means for the autonomy argument:**

The fire-and-forget model works when tasks are well-specified. SWE-bench tasks ARE well-specified — they have issue descriptions, test patches, and clear success criteria. But real-world coding tasks are NOT SWE-bench tasks. Real users write "fix the auth bug." This is the Ambig-SWE insight: **the autonomy argument only holds if you can guarantee task specification quality, which you can't.**

**The revised position:** Autonomous agents DO need clarification — but they need it in a form that doesn't break autonomy. That form is **stated assumptions**: the agent proceeds autonomously but makes its interpretation explicit, allowing asynchronous correction without blocking. This is autonomous-compatible clarification.

### The DS-IA paper names the core tension perfectly: **the Interaction Frequency Dilemma.** Every question has a cost:

| Dimension | Cost of NOT asking/assuming | Cost of asking/assuming |
|-----------|-------------------|----------------|
| Token budget | Agent wastes steps exploring wrong path (potentially 50%+ of budget) | Assumption statement costs ~100 tokens. Exploring first costs ~1-2 steps |
| User attention | User gets wrong result, has to re-run | User reads assumptions in streaming output — minimal cost (they're already watching) |
| Operational model | Works non-interactively (scripts, CI, batch) | Assumptions are just text — don't block execution. Compatible everywhere. |
| Correctness | Agent may build the wrong thing entirely (Ambig-SWE: 32% worse) | Agent builds with stated interpretation, user can Ctrl+C early if wrong |

**The revised asymmetry (informed by Ambig-SWE's data):** Stated assumptions cost virtually nothing (100 tokens, no blocking) but provide:
1. 32% recovery potential if the user catches a wrong assumption (Ambig-SWE's interaction gain)
2. Self-documentation of decisions (for handoff, debugging, lesson extraction)
3. Improved LLM reasoning quality from explicit-over-implicit decisions
4. Zero overhead for well-specified tasks (the system prompt triggers only for underspecified tasks)

### The Exploration-First Filtering Principle

Ambig-SWE's strongest contribution to our design is separating **navigational ambiguity** (the agent can resolve by exploring) from **behavioral ambiguity** (only the user can resolve).

In their data, models that ask navigational questions ("which file has the auth code?") get little benefit — strong models can find files themselves. But models that ask behavioral questions ("should the refresh token expire in 24h or 1h?") get significant gains.

**This maps to a filtering principle for assumptions:**

```
Ambiguity in task → Explore codebase → Still ambiguous?
  ├─ NO → Proceed (codebase resolved it)
  └─ YES → What KIND of remaining ambiguity?
       ├─ Navigational (which file/method) → Keep exploring, don't state
       └─ Behavioral (what behavior/design) → STATE ASSUMPTION
```

This is the DS-IA "semantic firewall" operationalized for coding agents: the codebase is the firewall. What gets through is what needs stating.

## Design: The "Explore-Then-Assume" Pattern

### Revised Design After Ambig-SWE

The original design proposed "Stated Assumptions" — the agent states its assumptions and proceeds. Ambig-SWE (ICLR 2026) validates this direction but reveals a crucial subtlety: **exploration must precede assumption-stating.** The best-performing agents (Claude Sonnet) explore the codebase FIRST, identify what they CAN'T discover independently, and only then surface the remaining unknowns.

This transforms "Stated Assumptions" from a static prompt technique into a dynamic **"Explore-Then-Assume"** workflow:

```
1. Read codebase (grep, file_search, read_file) — resolve navigational ambiguity autonomously
2. State behavioral/intent assumptions the codebase couldn't answer
3. Proceed with stated assumptions
```

This is superior to the original design because:
- It avoids low-value assumptions ("I assume the bug is in AuthController.cs" → just search for it!)
- It focuses assumptions on genuinely irreducible ambiguity (intent, preferences, design choices)
- It aligns with Ambig-SWE's finding that exploration-first strategy achieves comparable results with 50% fewer questions
- It mirrors DS-IA's "semantic firewall" concept — the codebase IS the firewall that filters resolvable ambiguity

### Why NOT a User Interaction Channel (Yet)

Building `Console.ReadLine()` into the agent loop creates several problems:
1. **Breaks non-interactive execution**: Resume, scripts, CI pipelines, handoffs.
2. **Requires timeout/fallback logic**: What if nobody answers? Agent hangs? Proceeds with default? How long to wait?
3. **Context management complexity**: The user's answer needs to survive compression. When does the answer become stale?
4. **Testing difficulty**: Integration tests can't simulate user interaction without mocking stdin.
5. **Ambig-SWE proved prompt engineering alone is insufficient for detection**: Even "Strong Encouragement" prompts produce inconsistent results. The system prompt CAN'T reliably teach the agent WHEN to ask/assume. We need session data first to understand when Forge encounters genuinely irreducible ambiguity.
6. **But the prompt CAN teach WHAT to assume about.** Ambig-SWE showed that when models DO interact, they're effective. The capability exists. The system prompt just needs to activate it.

### The "Explore-Then-Assume" Innovation

Instead of asking questions directly, the agent **explores the codebase to resolve navigable ambiguity, then states its remaining assumptions about intent/behavior and proceeds**. This captures Ambig-SWE's key finding: separate navigational discovery (can do autonomously) from behavioral understanding (needs user input or explicit assumptions).

**How it works:**

After PLAN, the agent does its initial exploration (existing PLAN behavior). Then, before committing to edits, it adds an "Assumptions" section focused on the types of unknowns the codebase CAN'T answer:

```
## My Plan:
1. Fix the auth bug — found failing test AuthControllerTests.TestTokenRefresh (line 42)
2. The test expects a refresh token with 24h expiry but gets 1h

## Assumptions (proceeding unless told otherwise):
- The user wants 24h expiry (as the test expects), not the current 1h
- I'll fix the TokenService implementation, not change the test expectation
- I'll preserve backward compatibility for existing tokens
```

**What makes these good assumptions (per Ambig-SWE's findings):**
- They're about **behavior and intent** (should expiry be 24h?), not navigation (which file has the auth code — the agent already found it)
- They're **specific and actionable** (Claude-style: 3-4 targeted items, not Llama-style vague "any workarounds?")
- They describe **what the agent chose AND what it chose NOT to do** (fix implementation, not change test)
- They're **testable** — the user reading the output can immediately say "no, change the test instead"

### What Gets Added to the System Prompt

```
## Planning with Assumptions

After exploring the codebase, identify what the code CAN'T tell you:
1. State your plan with specific files and changes (existing PLAN guidance)
2. Check if existing tests already specify the expected behavior.
   Tests often encode intent more precisely than task descriptions.
3. **State your key assumptions** — but only about what exploration AND tests couldn't resolve:
   - What specific behavior/outcome are you targeting? (e.g., "24h token expiry")
   - What design choices are you making when alternatives exist? (e.g., "fix impl, not test")
   - What scope boundaries are you drawing? (e.g., "preserving backward compat")
4. Do NOT state assumptions about things you can discover by reading code or tests.
   - BAD: "I assume the auth code is in AuthController.cs" — just search for it.
   - BAD: "I assume the expected expiry is 24h" — check the test assertion first.
   - GOOD: "I assume the user wants backward compatibility for existing tokens."
5. Proceed with the stated assumptions. In REPORT, note if any were wrong.

When to state assumptions:
- The task description is vague or short (< 2 sentences without specific details)
- Multiple valid interpretations exist after exploring code AND tests
- A design choice has alternatives the user might prefer differently

When NOT to state assumptions:
- The task is fully specified (test pass/fail gives clear signal)
- Tests already encode the expected behavior
- There's only one reasonable interpretation after reading the codebase
- NEVER state assumptions about file locations or code structure — explore instead
```

### Ambig-SWE Implications for Future Interactive Mode (Phase 5B-II)

_Documenting for when we have enough data._

Ambig-SWE's most provocative finding is that **prompt engineering alone cannot reliably teach detection.** This means our prompt-level Explore-Then-Assume pattern will hit a ceiling — the agent won't always notice when it needs to state assumptions. This is known and acceptable for Phase 5B-I (prompt-level). But it means Phase 5B-II (interactive mode) will need more than just a prompt tweak.

The Ambig-SWE-informed design for future interactive mode:

1. **`IUserChannel` interface in AgentLoop**: 
   ```csharp
   interface IUserChannel
   {
       Task<string?> AskAsync(string question, TimeSpan timeout, CancellationToken ct);
       bool IsInteractive { get; }
   }
   ```

2. **Two implementations**:
   - `ConsoleUserChannel`: Reads from stdin with configurable timeout. Default for CLI.
   - `NullUserChannel`: Returns null immediately. For scripts/CI/testing.

3. **`ask_user` as an MCP tool** (not an AgentLoop mechanism):
   - The agent calls `ask_user("Which auth module?")` like any other tool.
   - Tool implementation delegates to `IUserChannel`.
   - If non-interactive, returns "No user available. State your assumption and proceed."

4. **Ambig-SWE-informed limits**:
   - Max 3-4 questions per session (Claude Sonnet's sweet spot from the paper).
   - Only callable before any file-modifying tool call (enforced by VerificationTracker).
   - **Questions must be about behavior/intent, not navigation** — enforced via prompt guidance.
   - The agent MUST explore before asking — enforced by requiring at least 1 exploration tool call before `ask_user`.

5. **Detection assistance (future research)**:
   - Ambig-SWE showed that external detection is hard. But we can build heuristics:
     - Short task (< 50 chars, no file references, no error messages) → likely underspecified
     - Task contains vague verbs ("improve", "fix", "refactor", "update") without specific outcomes → likely underspecified
     - Agent's plan mentions multiple mutually exclusive approaches → likely underspecified
   - These heuristics could trigger a system-injected nudge: "This task may be underspecified. Consider stating your assumptions before modifying files."

## Implementation Plan (Phase 5B-I: Explore-Then-Assume)

### Step 1: Add Assumption Guidance to SystemPrompt.cs
- New conditional section (like the Debugging Protocol)
- Triggered when task appears underspecified (heuristic: short task < 50 chars, no specific file/test/error mentioned, contains vague words like "improve", "fix", "refactor" without specific outcomes)
- Explicitly instructs: "explore FIRST, then state assumptions about behavior/intent, NOT about file locations"
- ~250 tokens when active

### Step 2: Detect Underspecified Tasks
- `IsUnderspecifiedTask(string? task)` method in SystemPrompt.cs
- Heuristics informed by Ambig-SWE + AwN + CLAMBER findings:
  - Short length (< ~100 chars AND no file paths, no test names, no error messages)
  - Contains vague verbs without specific outcomes ("fix bug" vs "fix the NPE in AuthController.TestRefresh")
  - No code references, stack traces, or specific error strings
  - **AwN-inspired:** Missing key arguments that would be needed to act (no specific module, no specific test, no specific behavior)
- Bias toward triggering (false positives are cheap — the agent just states obvious assumptions)
- Override: tasks containing specific filenames, test names, or error strings are never underspecified
- **CLAMBER caveat:** CoT/few-shot won't help with detection — the heuristic must be structural, not LLM-judged

### Step 3: Log Assumption Presence in AgentLoop.cs
- Like hypothesis detection in 5A: scan response text for "assumption" / "assuming" / "proceeding with"
- Log at Debug level for future analysis
- Count assumptions per session for handoff metadata
- Zero infrastructure cost

### Step 4: Update Handoff to Include Assumptions
- If assumptions were detected in step 0-2, include them in the session handoff note
- A resumed session should know what the predecessor assumed
- Format: "Assumptions made: [list]. None were contradicted by user."

### Step 5: Track Assumption Quality (Observability)
- After session completion, compare assumptions to final outcome:
  - Did the agent's assumptions survive contact with the codebase?
  - Were any assumptions contradicted by test failures?
- Log for future analysis (no automated correction yet)

### What NOT to Build (Yet)
| Feature | Why not | Ambig-SWE evidence |
|---------|---------|-------------------|
| Console.ReadLine() interaction | Breaks non-interactive execution. Need data first. | Prompt engineering alone is insufficient for detection heuristics. |
| `ask_user` MCP tool | Requires IUserChannel infrastructure. Design ready, build later. | But when built: limit to 3-4 questions, behavior-only, exploration-first. |
| Timeout/fallback logic | No interaction channel to timeout on. | — |
| Assumption detection training | Can't train without interaction data. | Ambig-SWE found prompt-level detection inconsistent. |
| Automated underspecificity classification | Need labeled Forge sessions to build a classifier. | Models struggle to distinguish well-specified from underspecified (Ambig-SWE Table 2). |

### Success Criteria
- Agent states 2-3 specific, behavior-focused assumptions for underspecified tasks
- Assumptions are about intent/behavior, NOT about file locations or code structure
- Agent explores codebase AND tests BEFORE stating assumptions (exploration hierarchy)
- Assumptions are visible in streaming output and captured in session handoff notes
- No behavioral change for well-specified tasks (no false positive overhead)
- Non-interactive execution unaffected
- Target: assumptions stated in 70%+ of underspecified task sessions

## Research Papers Referenced

| Paper | arXiv | Date | Key Insight |
|-------|-------|------|-------------|
| **Ambig-SWE** | **2502.13069** | **Feb 2025 / ICLR 2026** | **The definitive paper. 74% performance recovery through interaction. Exploration-first strategy. Behavior > navigation questions. Detection is the hard problem. 3-4 targeted questions optimal.** |
| DS-IA | 2603.16207 | Mar 2026 | Interaction Frequency Dilemma — most ambiguity resolvable by examining environment; only ask for irreducible ambiguity |
| InteractComp | 2510.24668 | Oct 2025 | Agents have systematic overconfidence — forced interaction dramatically improves accuracy. Interaction capability stagnated industry-wide |
| **AwN/NoisyToolBench** | **2409.00557** | **Aug 2024 / Feb 2025** | **Next-token prediction causes LLMs to hallucinate missing arguments rather than acknowledge gaps. The mechanistic root of overconfidence. Ask-when-Needed framework.** |
| **TENET** | **2509.24148** | **Sep 2025** | **Tests as executable specifications. Tests encode user intent more precisely than NL descriptions. Test archaeology before assumption-stating.** |
| **CLAMBER** | **2405.12063** | **May 2024 / ACL 2024** | **CoT and few-shot INCREASE overconfidence. Inherent knowledge creates false sense of understanding. 12K-example benchmark with ambiguity taxonomy.** |
| **ReHAC** | **2402.12914** | **Feb 2024** | **RL learns optimal human intervention timing: at trajectory-branching decision points, not uniformly. Limited intervention >> no intervention or constant intervention.** |
| **AT-CXR** | **2508.19322** | **Aug 2025** | **Uncertainty-aware "decide or defer" pattern. Agent estimates confidence, abstains when below threshold. Rule-based vs LLM-decided router designs.** |
| **TheAgentCompany** | **2412.14161** | **Dec 2024** | **30% autonomous task completion in realistic company environment. Some tasks inherently need communication. Tempers expectations for clarification as silver bullet.** |
| ClarQ-LLM | 2409.06097 | Sep 2024 | Asking good questions is itself hard — quality > quantity |
| DERA | 2303.17071 | Mar 2023 | Researcher/Decider pattern — unknowns become documented assumptions |
| ClarifyGPT | 2310.10996 | Oct 2023 | Intention clarification before code generation. Test-driven validation |
| COMPASS | 2412.18270 | Dec 2024 | Conversational planning with user alignment improves task success |
| AgentDebug | 2509.25370 | Sep 2025 | Planning failures from unvalidated assumptions are a distinct failure category |
| Claude Code Plan Mode | Docs (2026) | Mar 2026 | Read-only planning phase surfaces open questions before execution |
| Copilot Plan Mode | Docs (2026) | Mar 2026 | Same pattern — plan with open questions, iterate, then execute |
