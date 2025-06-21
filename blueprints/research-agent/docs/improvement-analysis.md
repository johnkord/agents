# ResearchAgent Improvement Analysis

> *Based on 106 papers in the knowledge base, with particular focus on the 2025–2026 deep research and self-reflection wave.*

## Current Architecture Review

The ResearchAgent is a **fixed sequential pipeline**:

```
Planner → Researcher → Analyst → Synthesizer
```

Each agent runs **exactly once**. The Researcher has tools (search, fetch, note-taking) and uses the Pensieve read-note-prune pattern for memory. Output quality depends entirely on each agent getting it right on the first pass.

**What works well:**
- Clean separation of concerns (plan / search / analyze / write)
- Pensieve memory pattern — findings survive context pruning
- Session export with structured metrics
- OTel tracing for debugging
- Plugin architecture is extensible

**What the research says is missing:**

## Gap 1: No Verification — The Single Biggest Gap

The agent produces a report and stops. Nobody checks it.

The **Asymmetry Thesis** (DeepVerifier, 2025) says: "Verification is fundamentally easier than generation." A separate, lighter-weight verification step provides outsized returns because checking is cheaper than producing.

Right now a hallucinated claim in the Synthesizer's output sails through unchecked. The DEFT taxonomy catalogues 14 ways deep research agents fail — and our agent has no defense against any of them.

### Improvement: Add a Verifier Agent (Phase 5)

```
Planner → Researcher → Analyst → Synthesizer → Verifier
                                                   │
                                       ┌───────────┴───────────┐
                                       │  For each claim:      │
                                       │  1. Is it supported   │
                                       │     by a finding?     │
                                       │  2. Does the finding  │
                                       │     match the source? │
                                       │  3. Any contradictions│
                                       │     with other claims?│
                                       └───────────────────────┘
```

**Implementation sketch** — a new `VerificationPlugin`:

```csharp
[Description("Generate a verification checklist for the research report. "
    + "Each item should be a specific, verifiable claim from the report.")]
public string GenerateChecklist(string report) { ... }

[Description("Verify a specific claim against the accumulated research findings. "
    + "Returns SUPPORTED, UNSUPPORTED, or CONTRADICTED with evidence.")]
public string VerifyClaim(string claim) { ... }

[Description("Produce a verification summary with pass rate and list of failed items.")]
public string SummarizeVerification(string checklistResults) { ... }
```

The Verifier agent gets the report + all findings from memory and scores it. If the pass rate is below a threshold, the report should be flagged or sent back for revision.

**Effort**: Low-medium. One new plugin, one new agent, extend the pipeline.
**Impact**: High. This is the single highest-ROI improvement from the research.

---

## Gap 2: Single-Pass Pipeline — No Iteration

The Researcher searches once and moves on. If the first round of searches misses key information, or the Analyst identifies gaps, there's no way to go back.

The top-performing deep research agents (SFR-DeepResearch, Step-DeepResearch, RE-Searcher) all use **iterative search-analyze cycles**. SFR-DeepResearch showed a single 32B model with iterative RL-trained search can match systems using much larger models — because iteration compensates for imperfect initial queries.

### Improvement: Research Loop with Gap Analysis

Replace the fixed pipeline with a loop between Researcher and Analyst:

```
Planner → ┌─────────────────────────────┐ → Synthesizer → Verifier
           │  Researcher → Analyst       │
           │       ↑            │        │
           │       └── gaps? ───┘        │
           │  (max 3 iterations)         │
           └─────────────────────────────┘
```

After the Analyst identifies knowledge gaps, the orchestrator checks: are there gaps rated "critical"? If yes, send the gap list back to the Researcher for targeted follow-up. Cap at 2-3 iterations to prevent spiraling.

**Implementation**: This requires replacing `BuildSequential` with a manual orchestration loop. The MAF supports this — you'd invoke agents individually via `InProcessExecution.RunAsync` and route based on the Analyst's gap assessment.

**Effort**: Medium. The main work is switching from declarative pipeline to imperative orchestration.
**Impact**: High. The research consistently shows iteration is the single biggest differentiator between mediocre and excellent research agents.

---

## Gap 3: No Goal-Oriented Search Reflection (RE-Searcher)

The Researcher agent searches and records findings but never explicitly asks: *"Did this search actually answer the sub-question I was investigating?"*

RE-Searcher (2025) showed that a single-word change in a search query can cause cosine similarity of results to drop below 0.6. Their solution: force the agent to state a **goal** before each search and **reflect** on whether results met that goal.

### Improvement: Goal-Reflect Loop in Researcher Instructions

Update the Researcher's system prompt to enforce goal-reflect discipline:

```
For each sub-question:
1. State your search GOAL explicitly: "I need to find [specific information]"
2. Execute the search
3. REFLECT: Did these results address my goal? (Yes/No)
4. If No: reformulate the query and try again (max 2 retries per sub-question)
5. If Yes: record findings and move to next sub-question
```

This doesn't require new tools — it's a prompt engineering change that structures the Researcher's reasoning. But it could be strengthened by adding a `ReflectOnSearch` tool that makes reflection a tracked action.

**Effort**: Low (prompt change) to low-medium (add reflection tool).
**Impact**: Medium-high. Directly addresses query fragility, the most common failure mode in search-based agents.

---

## Gap 4: No Adaptive Compute Allocation

The agent spends equal effort on every sub-question regardless of difficulty. A straightforward factual sub-question gets the same treatment as a complex analytical one.

CoRefine (2026) showed that a tiny 211K-parameter confidence controller can allocate compute adaptively — halting quickly on easy problems and spending more rounds on hard ones, achieving 190x token reduction vs fixed-compute approaches.

### Improvement: Progress-Aware Research Budget

Add a `ResearchBudget` concept to `ResearchState`:

```csharp
public sealed class SubQuestionProgress
{
    public string SubQuestionId { get; init; }
    public int SearchAttempts { get; set; }
    public int FindingsRecorded { get; set; }
    public double AverageConfidence { get; set; }
    public bool MarkedComplete { get; set; }
}
```

The Researcher's `CheckResearchProgress` tool already exists — enhance it to show per-sub-question status. Then update the Researcher's instructions to:
- Move on from sub-questions with 3+ high-confidence findings
- Spend extra search rounds on sub-questions with 0-1 findings or low confidence
- Flag sub-questions that remain unanswerable after 3 search attempts

**Effort**: Low-medium. Extends existing progress tracking.
**Impact**: Medium. Improves efficiency and coverage simultaneously.

---

## Gap 5: No Reflective Memory

The Pensieve memory stores findings and working notes. But it doesn't store **what went wrong** — failed search strategies, dead-end sources, query reformulations that worked.

Reflection-Driven Control (AAAI 2026) showed that a **Reflective Memory Repository** that stores past reflections improves agent performance across tasks. Failed approaches are as valuable as successful ones — they prevent the agent from repeating mistakes.

### Improvement: Add Reflection Log to ResearchMemory

```csharp
public sealed class ReflectionEntry
{
    public string SubQuestionId { get; init; }
    public string OriginalQuery { get; init; }
    public string Reflection { get; init; }  // What went wrong, what to try instead
    public string Outcome { get; init; }     // Did the revised approach work?
    public DateTimeOffset Timestamp { get; init; }
}
```

Add a `RecordReflection` tool and a `GetReflections` tool. Include reflections in the context summary that gets passed between agents — so the Analyst knows what the Researcher already tried and failed at.

**Effort**: Low. Small model additions + one new tool.
**Impact**: Medium. Particularly valuable when combined with Gap 2 (iteration), since the second research pass can learn from the first pass's failures.

---

## Gap 6: Fixed Agent Roles vs. Dynamic Orchestration

The current `BuildSequential` pipeline means every query goes through the exact same 4-step process regardless of complexity. A simple factual question ("What year was Python created?") shouldn't need a Planner, Researcher, Analyst, AND Synthesizer.

The deep research papers show two viable approaches:
1. **Single autonomous agent** (SFR-DeepResearch): One agent dynamically decides what to do next
2. **Meta-orchestrator** (MAS²): A meta-agent routes to specialists as needed

### Improvement: Complexity-Aware Routing

Add a lightweight pre-classification step:

```
Query → Classifier → Simple?  → Direct LLM answer (no pipeline)
                   → Moderate? → Researcher → Synthesizer (skip Planner/Analyst)
                   → Complex?  → Full pipeline with iteration
```

**Effort**: Medium. Requires implementing the classifier and branching logic.
**Impact**: Medium. Improves latency and cost for simple queries. Not critical for the core research use case.

---

## Gap 7: Report Quality Evaluation — No Metrics

The session export tracks `findingCount`, `sourceCount`, and `agentInteractionCount`. But it doesn't track **report quality**.

FINDER (2025) showed that checklist-based evaluation (419 items across 100 tasks) is far more reliable than holistic scoring. Our agent could generate a checklist and score itself.

### Improvement: Auto-Evaluation in Session Export

After the Verifier runs (Gap 1), include its results in the session export:

```csharp
public sealed class SessionMetrics
{
    // Existing
    public int FindingCount { get; set; }
    public int SourceCount { get; set; }
    
    // New — verification metrics
    public int ChecklistItemCount { get; set; }
    public int ChecklistItemsPassed { get; set; }
    public double ChecklistPassRate { get; set; }
    public List<string> FailedChecklistItems { get; set; }
    public int IterationCount { get; set; }
    public Dictionary<string, int> FailureModeBreakdown { get; set; } // DEFT categories
}
```

This enables systematic quality tracking across sessions — you can measure whether changes to prompts, models, or architecture actually improve output quality.

**Effort**: Low (once Gap 1 is implemented).
**Impact**: High for iteration velocity. You can't improve what you don't measure.

---

## Gap 8: No Source Diversity or Quality Signals

The current `SourceRecord` has a `ReliabilityScore` field, but it's never meaningfully populated (simulated sources get arbitrary scores). The Analyst prompt says "Evaluate source reliability" but has no tools to actually do this.

DeepVerifier's DRA Failure Taxonomy and FINDER's DEFT both show that source quality problems (fabricated information, unreliable sources, missing attribution) account for a large portion of research agent failures.

### Improvement: Source Quality Heuristics

Add a `SourceEvaluationPlugin` with:

```csharp
[Description("Evaluate source quality based on URL pattern, source type, and content signals.")]
public string EvaluateSource(string sourceId)
{
    // Heuristics:
    // - .edu, .gov, known journals → high reliability
    // - Forum posts, social media → low reliability  
    // - Check for: date freshness, author attribution, citation density
    // - Flag potential conflicts of interest
}

[Description("Check source diversity — are we over-relying on a single source or source type?")]
public string CheckSourceDiversity()
{
    // Alert if >50% of findings come from a single source
    // Flag if no academic sources for a scientific question
    // Warn if all sources are from the same domain
}
```

**Effort**: Low-medium.
**Impact**: Medium. Directly addresses a common failure mode (DEFT categories 1-4).

---

## Prioritized Implementation Roadmap

Based on impact-to-effort ratio, informed by the research:

### Phase 1: Verification (Highest ROI)
1. **Verifier Agent** — checklist-based report verification (Gap 1)
2. **Auto-evaluation metrics** in session export (Gap 7)

### Phase 2: Iteration
3. **Research loop** — Researcher ↔ Analyst gap-analysis cycle (Gap 2)
4. **Reflection log** in memory (Gap 5)

### Phase 3: Search Quality
5. **Goal-reflect discipline** in Researcher prompt (Gap 3)
6. **Source quality heuristics** (Gap 8)

### Phase 4: Efficiency
7. **Adaptive compute** via progress-aware budgets (Gap 4)
8. **Complexity-aware routing** (Gap 6)

---

## Connection to Overall Architecture Patterns

The improvements map cleanly to patterns documented in the knowledge base:

| Improvement | Architecture Pattern | Key Paper |
|---|---|---|
| Verifier Agent | Generator-Verifier | DeepVerifier, CoRefine |
| Research Loop | Plan-Reflect-Verify | Reflection-Driven Control |
| Goal-Reflect Search | ERA 2 Structured Reflection | RE-Searcher |
| Adaptive Compute | Confidence-Guided Refinement | CoRefine |
| Reflective Memory | Reflective Memory Repository | Reflection-Driven Control |
| Dynamic Routing | Deep Research Agent Architecture | SFR-DeepResearch |
| Auto-Evaluation | Checklist Verification | FINDER/DEFT |
| Source Quality | DRA Failure Taxonomy | DeepVerifier |

The current agent sits at **Era 0** of self-correction — no verification, no reflection, no iteration. Phase 1-2 improvements would bring it to **Era 2** (Structured Reflection), which is the sweet spot for scaffolded agents that aren't RL-trained.

## A Note on What Doesn't Apply

Not all research insights are relevant to our scaffolded agent:

- **RL training** (SPOC, SFR-DeepResearch, DeepPlanner) — Requires training infrastructure we don't have. These papers inform *what behaviors to scaffold*, even if we can't train them natively.
- **World model simulation** (Dyna-Think) — More relevant for interactive/UI agents than research agents.
- **Entropy-based advantage shaping** (DeepPlanner) — Purely a training technique.
- **Meta-MAS tri-agent** (MAS²) — Overkill for a single-query research agent.

The research that's most directly actionable falls into two categories:
1. **Architectural patterns** (Generator-Verifier, Plan-Reflect-Verify) — these are scaffoldable
2. **Failure taxonomies and evaluation** (DEFT, checklist scoring) — these inform what to check for
