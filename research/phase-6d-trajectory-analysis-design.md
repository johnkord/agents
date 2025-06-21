# Phase 6D: Trajectory Analysis & Progress Metrics — Design Document

## Problem Statement

Forge has 346 tests, 6 live experiments, and a 5-task regression suite — but no automated way to measure whether changes improve the agent. We evaluate by eye: "did the lesson look right?" "did the handoff work?" This doesn't scale.

The deeper problem: **binary pass/fail doesn't capture what matters.** A session that completes in 5 steps and one that completes in 15 steps both "pass" — but the 5-step session is 3x more efficient. A session that pivots twice before succeeding is qualitatively different from one that succeeds on the first try. We need metrics that capture efficiency, not just correctness.

## What We Have to Work With

Every Forge session already produces rich data in the JSONL event log:

```jsonl
{"event":"session_start","data":{"task":"...","model":"gpt-5.4",...}}
{"event":"step","data":{"stepNumber":0,"thought":"...","toolCalls":[...],"promptTokens":6592,...}}
{"event":"step","data":{"stepNumber":1,...}}
...
{"event":"session_end","data":{"success":true,"totalSteps":10,...}}
{"event":"session_handoff","data":{"status":"complete","episodes":[...],"pivotReasons":[...],...}}
```

Each step record contains: thought text, tool calls (name, args, result summary, duration, error status), prompt/completion tokens, step duration. The handoff contains: episodes, pivots, assumptions, consolidation summary, files modified, failed approaches.

This is everything needed for trajectory analysis — we just don't have the code to parse and analyze it.

## Research Basis (10 Papers)

### ★ PRInTS (arXiv:2511.19314, Nov 2025) — Step-Level Quality Dimensions

**Process reward model for long-horizon information-seeking agents.** Defines multiple quality dimensions for evaluating individual steps:
- **Tool call informativeness**: Did this tool call produce new, useful information?
- **Interpretation of tool outputs**: Did the agent correctly interpret what the tool returned?
- **Reasoning quality**: Is the agent's reasoning connected to the evidence?

**Plus trajectory summarization**: Compresses the growing context while preserving essential information for step evaluation — directly analogous to our sawtooth compression.

**Key insight for 6D:** We shouldn't score steps on a single axis. A step can be high-quality reasoning but low-efficiency execution (e.g., correct hypothesis but unnecessary re-read). PRInTS's multi-dimensional scoring maps to our observations: we care about correctness AND efficiency AND verification compliance simultaneously.

### ★ Process Reward Models Survey (arXiv:2510.08049, Oct 2025)

**Comprehensive survey of PRMs** across math, code, text, multimodal, robotics, and agents. Key framing: **Outcome Reward Models (ORMs) judge only final answers. Process Reward Models (PRMs) evaluate at the step or trajectory level.**

**Forge connection:** Our current evaluation is purely ORM-style (did the task succeed? did tests pass?). We need PRM-style evaluation (was each step productive? was exploration efficient? were redundant operations avoided?). The survey shows this shift is happening across the field.

**What the survey reveals about agent-specific PRMs:** The hardest part is defining "correct step" for agents. In math, a correct step follows logically. In coding agents, a "correct" step might be reading a file that turns out to be irrelevant — was it correct to read it? Only in hindsight do we know.

### ★ Agent-RewardBench (arXiv:2506.21252, ACL 2025) — Step-Level Reward Evaluation

**Benchmark for evaluating reward models on agent tasks.** 3 key features:
1. **Multiple dimensions**: perception, planning, safety
2. **Step-level granularity**: assesses capabilities at individual steps, not just task completion
3. **Difficulty calibration**: samples from 10 diverse models to ensure appropriate challenge

**Key insight:** Even state-of-the-art models show limited performance on step-level reward evaluation. This means our heuristic approach (computing metrics from tool call patterns rather than using a reward model) is appropriate — the reward models themselves struggle with this.

### ★ SWE-Effi (arXiv:2509.09853, Sep 2025) — The Efficiency Evaluation Paper

**Re-evaluates SWE-bench agents on holistic effectiveness, not just accuracy.** Defines effectiveness as the balance between outcome accuracy and resources consumed (tokens and time). Introduces multi-dimensional metrics for re-ranking agents.

**Three critical findings directly relevant to our trajectory analysis:**
1. **The "token snowball" effect:** Agents consume increasing tokens per step as sessions progress. This is exactly what we observed (52% growth at step 15) and now have a name for from the literature.
2. **"Expensive failures":** Agents consume excessive resources while stuck on unsolvable tasks. This maps to our budget-exhaustion sessions — the agent doesn't know when to give up, consuming full budget on tasks it can't solve.
3. **Token budget vs time budget tradeoff:** Fast responses are essential for scalability, but token-efficient agents may be slow and vice versa. We should track BOTH dimensions.

**Key insight:** SWE-Effi's multi-dimensional effectiveness score is exactly what our metrics should measure. They propose: `effectiveness = accuracy × cost_efficiency`. We can adapt this as: `session_effectiveness = task_completion × (1 / normalized_cost)` where cost is tokens or steps relative to the task's baseline difficulty.

### ★ iStar (arXiv:2509.19199, Sep 2025) — Implicit Step Rewards for Agents

**Introduces implicit step rewards for agentic RL** — a credit-assignment strategy that assesses step quality without explicit human labels. Uses trajectory-based DPO objective to learn a step-wise reward function, then combines step-level and episode-level advantages.

**Key result:** Achieves task success with FEWER steps — demonstrating that step-level credit assignment directly improves efficiency, not just accuracy.

**Key insight for 6D:** iStar confirms that step-level evaluation is the right level of granularity. But it also shows something we should internalize: **the goal isn't just to MEASURE step quality, it's to identify which step-level signals predict task success.** If redundant reads don't correlate with failure, they're a cost problem not a quality problem. If low verification compliance correlates with failure, that's a quality problem. Our metrics should distinguish these.

### From Our Existing Corpus

**SeaView (arXiv:2504.08696):** Trajectory visualization for SWE agents. Researchers spend 10-30 minutes manually analyzing trajectories. Our EpisodeSegmenter + JSONL logs give us the structured data SeaView advocates for.

**FuseSearch (arXiv:2601.19568):** 34.9% redundant invocation rate. Provides the "read coalescence rate" metric — unique files read / total read_file calls.

**HPCA 2026 Cost of Dynamic Reasoning:** Per-step cost growth as the key sustainability metric. Provides the "tokens/step curve" dimension.

**SWE-Pruner (arXiv:2601.06797):** Read operations are 76% of cost. Provides the "exploration ratio" metric — read steps / total steps.

## Critical Analysis: What Should We Actually Measure?

### The Temptation of Overcounting

The research suggests measuring everything — step quality, tool informativeness, reasoning accuracy, context utilization, verification compliance, exploration efficiency, cost curves, episode transitions, pivot quality, assumption validation...

This is a trap. **Metrics are useful only if they change decisions.** If a metric goes up or down and you wouldn't change anything, don't measure it.

### The Metrics That Would Actually Change Decisions

From our 6 experiments, here are the metrics that would have told us something actionable:

| Metric | What it reveals | What we'd change |
|--------|----------------|-----------------|
| **Steps/task** | How many steps the agent uses to complete (or fail at) a task | If increasing, investigate what's causing bloat |
| **Tokens/step curve** | How per-step cost grows with session length | If steepening, invest in hierarchical compression |
| **Read coalescence rate** | unique files / total read_file calls | If low, strengthen the read coalescence guidance |
| **Verification compliance** | verified edits / total edits | If dropping, investigate prompt regression |
| **Consolidation capture rate** | sessions with consolidation summary / sessions with boundary warning | If low, fix the consolidation capture bug |
| **Resume overhead** | re-read steps in resumed session / total resumed steps | If high, improve the re-verification prompt |

These 6 metrics cover the findings from ALL our experiments. More metrics would be noise.

### What NOT to Measure

| Tempting Metric | Why Skip It |
|----------------|-------------|
| Episode type distribution | Cosmetic — doesn't change any decision |
| Pivot count per session | Pivots are neither good nor bad; the count means nothing |
| Assumption frequency | We showed assumptions are correctly silent for well-specified tasks |
| Hypothesis detection rate | Observability logging, not a quality metric |
| Tool call duration | Tool latency is infrastructure, not agent behavior |
| Lesson quality score | We'd need a judge model, which is its own project |

## Design: Lightweight Offline Analyzer

### Architecture Decision: Script, Not Service

The trajectory analyzer should be a **CLI script** that reads session JSONL files and outputs metrics, not an online service or an in-process monitor. Reasons:

1. **Separation of concerns.** Analysis is post-hoc, not real-time. The AgentLoop shouldn't be burdened with metric computation during execution.
2. **Reproducibility.** Running the analyzer on the same JSONL files produces the same metrics. No state to manage.
3. **Composability.** A CLI script can be piped, filtered, and scripted. A service needs infrastructure.

### The Analyzer Output

For a single session:
```
Session: 20260322-074827-447-Something-is-wrong...
  Status: incomplete (token limit)
  Steps: 14 | Tokens: 207K | Duration: 106s
  Tokens/step: 9.4K → 14.9K (58% growth)
  Read coalescence: 7 unique files / 9 reads (78%)
  Verification: 2/2 edits verified (100%)
  Consolidation: ✗ not captured (boundary warning fired but agent responded with tool call)
  Episodes: explore(0-5) → impl(6) → explore(7) → impl(8) → explore(9) → verify(10-12) → explore(13)
  Pivots: 1 ("switching to deterministic allowlist slug generator")
  Features detected: hypothesis(3), pivot(1), assumption(0)
```

For the regression suite (aggregated):
```
Regression Suite Summary (5 tasks):
  Resolve rate: 4/5 (80%)
  Avg steps/task: 9.4
  Avg tokens/task: 98K
  Read coalescence: 82% (target: >75%)
  Verification compliance: 100% (target: >80%)
  Consolidation capture: 3/4 incomplete sessions (75%)
  Token/step growth: avg 32% at step 15 (baseline 9.5K → 12.5K)
  Features exercised: debugging-protocol(1/1), assumptions(0/2), pivots(1/1), episodes(5/5)
```

### Implementation: What to Build

**A C# console tool or dotnet script** that:

1. Reads one or more session JSONL files
2. Parses step events into `StepRecord` objects (reusing the existing type)
3. Parses the handoff event for episodes/pivots/assumptions
4. Computes the 6 core metrics
5. Outputs text or JSON summary

**NOT a test.** This isn't a unit test — it's an analysis tool. It runs against real session data, not mock data. Unit tests verify the metric computation logic; the tool runs against production sessions.

### The 6 Metrics: Computation Details

**1. Steps/task** — `session_end.totalSteps`. Trivially extracted.

**2. Tokens/step curve** — For each step, compute `promptTokens`. Plot the growth: `step[N].promptTokens / step[0].promptTokens`. Report the ratio at step 10 and step 15 if the session is long enough.

**3. Read coalescence rate** — Count unique `filePath` values across all `read_file` tool calls. Divide by total `read_file` call count. 1.0 = perfect (every read is a unique file). 0.25 = terrible (same file read 4 times).

**4. Verification compliance** — Already computed by VerificationTracker and logged in the session. Parse the log for "Verification compliance: X/Y edits verified." Or recompute from step data: count `replace_string_in_file`/`create_file` calls, then check if `read_file` follows within 2 steps.

**5. Consolidation capture rate** — Check if the handoff has a non-null `consolidationSummary` for sessions where `boundaryWarningIssued` would have been true (status = "incomplete").

**6. Resume overhead** — For resumed sessions (continuation context present), count how many of the first N steps are `read_file` on files mentioned in the handoff's `filesModified` or consolidation summary. These are re-verification reads.

### Progress Rate: Feature Scoring

Each regression task in `regression-tasks.json` has an `expectedFeatures` list. For each completed session:

1. Parse the session log for evidence of each feature:
   - `debugging-protocol`: step thought contains "hypothesis" or "reproduce"
   - `verification`: VerificationTracker reports > 0 verified edits
   - `run_tests`: at least one `run_tests` tool call with success
   - `assumptions`: handoff has non-null `Assumptions` field
   - `manage_todos`: at least one `manage_todos` tool call
   - `consolidation`: handoff has non-null `ConsolidationSummary`
   - `episode-chain`: handoff has > 1 episode

2. Score: features detected / features expected

This gives a progress rate per task that captures partial success better than binary pass/fail.

## What NOT to Build

| Feature | Why not |
|---------|---------|
| Online metrics dashboard | Over-engineered for ~30 sessions. CLI output is sufficient. |
| Process Reward Model | PRInTS/Agent-RewardBench show even SOTA models struggle with step-level rewards. Heuristic metrics are more reliable for our scale. |
| LLM-as-judge for step quality | Adds cost and latency. Our heuristic metrics (read coalescence, verification compliance) are cheaper and more objective. |
| Time-series visualization | Not enough data points. Text summary is sufficient until we have 50+ sessions. |
| Comparative A/B analysis | Needs identical tasks run with different configs. Build after the regression runner is automated. |

## Implementation Plan

1. **Session parser** (~50 lines): Read JSONL, extract step records + handoff event into typed objects. Reuse existing `StepRecord` and `SessionHandoff` types.

2. **Metric computation** (~80 lines): 6 functions, one per metric. Each takes parsed session data and returns a value.

3. **Feature detector** (~40 lines): For each `expectedFeature`, a pattern matcher on the session data.

4. **CLI entry point** (~30 lines): Accept file paths or glob, run analysis, output text summary.

5. **Tests** (~40 lines): Unit tests for metric computation with synthetic step data.

**Total: ~240 lines. One new file or small project. No changes to Forge.Core.**

## Cross-Paper Synthesis: The Emerging Efficiency Evaluation Stack

Across these 10 papers, a clear architecture for agent evaluation emerges — and it maps almost perfectly to what we need:

| Layer | What it evaluates | Papers | Our implementation |
|-------|------------------|--------|-------------------|
| **Outcome** | Did the task succeed? | SWE-bench, SWE-Effi | Binary pass/fail from test suite |
| **Effectiveness** | Success per unit cost? | SWE-Effi, HPCA 2026 | `task_completion × cost_efficiency` |
| **Process** | Was each step productive? | PRInTS, PRM Survey, iStar | Read coalescence, verification compliance |
| **Feature** | Did the agent exercise expected capabilities? | Agent-RewardBench | Feature detection from `expectedFeatures` |

**The insight we can act on:** SWE-Effi's "expensive failures" pattern is our biggest unmeasured phenomenon. Our budget-exhausted sessions (Scenario 3, vague bug scenario) consumed full token budgets. Were they "expensive failures" (stuck on unsolvable tasks) or "expensive progress" (making real headway, just ran out of budget)?

**The way to distinguish them:** iStar's step-level reward signal. If the agent was making progress (each step moved closer to the solution), it's expensive progress. If the agent was spinning (repeating the same exploration without new information), it's an expensive failure. Our read coalescence metric captures part of this — low coalescence means spinning.

**A new metric this suggests: information gain per step.** If each step's tool call produces information the agent didn't have before (new file content, new test results, new search matches), the step has positive information gain. If the tool call produces content that was already in the compressed context (redundant read), information gain is zero. Steps with zero information gain are waste. This is the FuseSearch redundancy rate but measured per-step rather than per-session.

## Research Papers Referenced

| Paper | arXiv | Date | Key Insight |
|-------|-------|------|-------------|
| **PRInTS** | **2511.19314** | **Nov 2025** | **Multi-dimensional step quality scoring: tool informativeness, interpretation accuracy, reasoning quality.** |
| **PRM Survey** | **2510.08049** | **Oct 2025** | **ORMs → PRMs: the field is shifting to step-level evaluation. Agent PRMs are hardest.** |
| **Agent-RewardBench** | **2506.21252** | **ACL 2025** | **Step-level reward evaluation. Even SOTA models struggle — validates heuristic approach.** |
| **★ SWE-Effi** | **2509.09853** | **Sep 2025** | **Effectiveness = accuracy × cost efficiency. Token snowball effect. Expensive failures pattern. Token vs time budget tradeoff.** |
| **★ iStar** | **2509.19199** | **Sep 2025** | **Implicit step rewards via trajectory DPO. Step-level credit assignment directly improves efficiency (fewer steps to success).** |
| SeaView | 2504.08696 | Apr 2025 | Trajectory visualization — structured data enables comprehension |
| FuseSearch | 2601.19568 | Jan 2026 | 34.9% redundant invocation rate → read coalescence metric |
| HPCA 2026 | 2506.04301 | Jun 2025 | Per-step cost growth → tokens/step curve metric |
| SWE-Pruner | 2601.06797 | Jan 2026 | Reads = 76% cost → exploration ratio metric |
| syftr | 2505.20266 | May 2025 | Pareto-optimal agent configurations — balancing cost and accuracy |
