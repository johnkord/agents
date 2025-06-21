# Proposal: Output Delivery & State Persistence

> Design proposal for the Research Agent blueprint covering two questions:
> 1. How should a research agent surface its results?
> 2. Should research sessions be resumable, extensible, or accumulative?

---

## Executive Summary

After surveying 15+ papers from the collection (Deep Research Survey, Step-DeepResearch, SFR-DeepResearch, FINDER/DEFT, Pensieve/StateLM, ResearStudio, MetaGPT, Monadic Context Engineering, CaveAgent) and cross-referencing with the knowledge base and current implementation, the recommendation is:

**Output**: Yes, improve — but surgically. Separate the report from its evidence trail, add structured quality metadata, and introduce progress streaming. Do *not* chase multi-format output (slides, posters) — it's premature.

**Persistence**: Yes, but with a specific architecture: **stateless-reducer with external state files**. No in-process pause/resume. No database. Research state (plan + findings + sources + reflections) is serialized to a well-defined file that can be loaded as context for a follow-up session. The agent itself remains a pure function.

---

## Part 1: Output Delivery

### Current State

The agent has two output channels:

| Channel | Format | Content | Consumer |
|---|---|---|---|
| Console (stdout) | Markdown text + plain-text metadata | Report between `══` banners, finding/source counts, session ID, verification pass rate | Human |
| Session JSON (`sessions/{id}.json`) | Structured JSON | Full trajectory: query, agent interactions, findings, sources, context log, aggregate metrics | Analysis pipelines (write-only) |

The report is delivered *after* the full pipeline completes. There is no streaming, no structured report format, and no quality metadata embedded in the report itself.

### What the Literature Says

**The field unanimously treats output quality as a first-class concern, not an afterthought.**

Step-DeepResearch trains models specifically on report quality (§3.4) and discovered a negative correlation between analytical depth and comprehensiveness — deeper reports miss items, broader reports are shallow. Their solution is a Synthesis-driven Drafting module with a Pairwise LLM Judger that filters low-quality drafts.

FINDER/DEFT established the first failure taxonomy for deep research reports: 14 failure modes, with **39% of failures in content generation** (not retrieval). The dominant failure: "strategic content fabrication" — agents produce professional-sounding but unsupported claims. This makes verification metadata in the output essential, not optional.

SFR-DeepResearch grades reports on factuality, instruction compliance, writing quality, and citation quality — a multi-criteria rubric, not a single pass/fail.

ResearStudio is the only paper that addresses streaming: an event-driven protocol that streams each action, file change, and tool call to a web interface in real time, making "all plans, intermediate artifacts, and actions visible."

The Deep Research Survey (Table 2) maps existing systems by output capability — nearly all support text + citations, very few support slides/posters/video. Table 5 shows benchmarks exist for these formats but adoption is minimal.

### Analysis: What Actually Matters

There are three output improvements that are clearly worth doing, one that's borderline, and two that aren't worth doing now.

**Worth doing:**

1. **Structured report envelope** — The report should not be a raw string. It should be a structured object with sections, citations, confidence scores, and verification results. The current `SessionExport` already captures most of this data — but it's in a separate file that a human never sees, and it's not integrated with the report itself. The fix is simple: wrap the Markdown report in a thin JSON envelope that includes verification pass rate, finding count, source count, and a list of failed verification claims. This costs almost nothing to implement and immediately surfaces the quality metadata FINDER says is critical.

2. **Progress events during execution** — A research session takes 4-5 minutes. During this time the user sees nothing. ResearStudio's streaming approach is the right idea, but we don't need a bidirectional web protocol. A simple event stream to stderr (or a callback interface) that reports phase transitions, finding discoveries, and iteration progress is sufficient. The key insight from ResearStudio: stream *actions* (what the agent is doing), not *findings* (which may be revised). Findings are delivered in the final report.

3. **Clean separation of report and evidence** — Right now the console output mixes the synthesized report with raw metadata (finding counts, session ID). And the session JSON contains everything in a flat structure. The improvement: the primary output is the report (a coherent, self-contained document). The secondary output is the evidence package (findings, sources, trajectories, metrics). These are already structurally separate in the code — `result.Report` vs `result.Findings` — but the console presentation blurs the line. This is mostly a presentation change.

**Borderline:**

4. **Writing quality metrics** — Step-DeepResearch evaluates on four dimensions (Information Completeness, Content Depth, Requirement Fitness, Readability). Adding an auto-evaluation step that scores the report on these dimensions before delivery would catch the 39% generation-quality failures FINDER identifies. However, this is essentially a second verification pass, and we already have the FINDER checklist verifier. The question is whether the verifier's claim-level checking is sufficient or whether document-level quality scoring adds enough value. My lean: defer this until we have empirical data from running the current verifier and seeing what it misses.

**Not worth doing now:**

5. **Multiple output formats** (slides, posters, structured HTML) — The literature shows benchmarks exist but adoption is minimal. Every new format requires its own evaluation framework. Markdown is universally consumable and convertiable. This is Phase 4+ at best.

6. **Synthesis-driven Drafting with Pairwise Judging** — Step-DeepResearch's approach of generating multiple draft reports and selecting the best via LLM judging is effective but doubles/triples the synthesis cost. The depth-vs-breadth tension they identified is real, but the right near-term mitigation is better prompting in the Synthesizer agent, not multi-draft generation.

### Proposed Design: Structured Output Envelope

```
ResearchOutput
├── Report (Markdown string — the primary artifact)
├── QualityMetadata
│   ├── FindingCount
│   ├── SourceCount
│   ├── VerificationPassRate
│   ├── FailedClaims[]
│   └── IterationCount
├── Evidence
│   ├── Findings[] (with confidence, source links, sub-question mapping)
│   ├── Sources[] (with URL, title, relevance)
│   └── Reflections[] (analyst gap observations)
└── Trajectory (optional, for debugging/analysis)
    ├── AgentInteractions[]
    └── ContextLog[]
```

The console output becomes:
- Progress events to stderr during execution (phase changes, finding count, iteration number)
- The report to stdout
- Quality summary footer to stderr
- Full structured output to the session JSON file (which now follows this envelope schema)

This means `dotnet run -- "query" > report.md` gives you just the report, while the terminal shows progress and quality metadata.

### Implementation Impact

- **ResearchResult** (in `ResearchOrchestrator.cs`): Already has all the data. Add a `QualityMetadata` record.
- **Program.cs**: Refactor console output to write report to stdout, everything else to stderr. Add progress callback.
- **SessionExport.cs**: Align with the envelope schema. Minor reshuffling, no data loss.
- **ResearchOrchestrator.cs**: Accept an optional `IProgress<ResearchProgressEvent>` callback. Report phase transitions and finding discoveries.

Estimated effort: **Small** — primarily restructuring existing output, not generating new data.

---

## Part 2: State Persistence

### Current State

`ResearchMemory` is created `new` in the `ResearchOrchestrator` constructor. It holds findings, sources, working notes, reflections, sub-question progress, and verification items in `ConcurrentDictionary` collections. All of this is lost when the process exits.

The session JSON export is write-only. It captures a snapshot of the final state but is never read back.

There is no mechanism to resume, extend, or build upon a prior research session.

### What the Literature Says

**The striking finding: no paper directly solves cross-session deep research.** Within-session memory is well-studied; cross-session persistence is virtually unexplored in the academic literature.

The paper catalogue identifies "Memory is the critical unsolved problem" as a core consensus.

Here's what the papers *do* say:

**Pensieve/StateLM** — the most sophisticated within-session memory model. Notes persist across context pruning cycles (the "sawtooth" pattern), but only within a single reasoning episode. The key insight we already use: persistent notes are the durable knowledge artifact; raw content is ephemeral.

**Step-DeepResearch** — uses the file system as external persistent memory. Three mechanisms: (1) patch-based file editing (70% token savings), (2) implicit context management (raw data to disk, summaries to context), (3) stateful todo management that decouples research progress from model context. The todo tool explicitly separates *what has been researched* from *what the model currently remembers*.

**ResearStudio** — the closest to cross-session: pause/resume of a running process, with workspace export. But this requires a long-lived backend connection and persistent process state. It's infrastructure-heavy for modest benefit.

**The 12-Factor Agents framework** (via the memory systems research doc) makes the critical architectural argument: *"Memory is an input to the reducer, not internal hidden state."* Agents should be stateless reducers. State lives outside the agent and is loaded at invocation time.

**Monadic Context Engineering** provides the formal backing: `AgentMonad = StateT S (EitherT E IO)` — state is explicitly threaded through composition, which means it can be extracted and restored at any boundary.

**CaveAgent** provides the empirical case for statefulness: +10.5% task success, 28.4% token reduction when the agent maintains state across steps. But this is within-session state, not cross-session.

### The Core Tension

The paper catalogue identifies this as Core Tension #4:

> *Stateless-reducer (12-Factor, reproducible) vs. stateful-runtime (CaveAgent +10.5% task success, 28.4% token reduction). Stateless is simpler and more testable. Stateful is more efficient and capable.*

The memory systems research doc sharpens the tradeoffs:

| For Stateless (no persistence) | For Stateful (persistence) |
|---|---|
| Reproducibility — same query, same process | Efficiency — don't re-research known topics |
| Simplicity — no state migration, no corruption | User value — real research is iterative |
| Privacy — no leaking prior sessions | The survey on memory as "cornerstone" of DR |
| No memory poisoning attack surface | CaveAgent: +10.5% task success |
| No credit assignment problem across sessions | Natural for multi-day research projects |

And identifies a resolution path: *"The resolution: memory is an input to the reducer, not internal hidden state."*

### The Case Against Persistence

Let me steelman the "don't do it" position, because the risks are real:

1. **Memory poisoning** — If findings from Session A are fed into Session B, adversarial or erroneous content propagates. The memory systems doc explicitly warns: "adversarial content can survive across sessions." Every session boundary would need finding re-verification, which is expensive.

2. **Credit assignment** — The Deep Research Survey (§6.2.3) identifies a "fundamental obstacle": when Session B's report is wrong, was the cause in Session A's findings, Session B's reasoning, or the interaction? Debugging cross-session chains is qualitatively harder than debugging single sessions.

3. **Complexity cliff** — The current architecture is clean: start → research → output → done. Adding persistence introduces state migration (what happens when the schema changes?), corruption recovery, partial state loading, and the question of *how much* prior context to load (too much → context pollution; too little → why bother?).

4. **Diminishing returns** — A research session takes 4-5 minutes and costs ~$0.50-2.00 in API calls. Re-running from scratch on a refined query is fast and cheap. The incremental value of persistence is small unless sessions become much longer or much more expensive.

5. **The field hasn't solved it** — Fifteen papers, none with a working cross-session system. This isn't because nobody thought of it; it's because the design space is genuinely hard. Building a novel solution where the research community hasn't converged is risky.

### The Case For Persistence

1. **Iterative refinement is the real workflow** — Nobody does research in one shot. You ask a question, read the results, refine the question, go deeper on one aspect. Without persistence, each refinement starts from zero. This is the single strongest argument.

2. **The architecture already supports it** — `ResearchMemory` already separates persistent findings from ephemeral working state (the Pensieve pattern). `SessionExport` already serializes everything to JSON. The gap is not "what to persist" but "how to reload it." The delta is small.

3. **Step-DeepResearch's model is proven** — Their file-system-as-external-memory approach works in practice. Writing findings to disk as they're generated (rather than only at export) would make research crash-resilient and naturally enable cross-session state.

4. **The 12-Factor resolution is clean** — Keep the agent stateless. Persist state to files. Load prior state as context at invocation time. This avoids the complexity of process pause/resume while capturing most of the value. It's how every database-backed web application works.

5. **It enables "knowledge folders"** — A user could maintain a `research/` directory where each session adds findings, and the agent automatically loads relevant prior findings when starting a new session on a related topic. This is Claude Code's approach: "the memory IS the artifact" — plain files in the project.

### My Position

**Do it, but do it the stateless-reducer way.** Specifically:

- **No process pause/resume** (ResearStudio-style) — too much infrastructure for too little benefit in a CLI tool.
- **No database** — adds a runtime dependency and operational burden disproportionate to the value.
- **No implicit cross-session memory** — no hidden state that silently influences results. All prior context is explicitly loaded and visible.

Instead: **persist research state to a well-defined file, and accept a prior state file as input to a new session.** The agent remains a pure function: `f(query, priorState?) → (report, newState)`.

### Proposed Design: Research State Files

```
ResearchState (JSON file, ~50-200KB per session)
├── metadata
│   ├── sessionId
│   ├── query
│   ├── createdAt
│   ├── model
│   └── parentSessionId?     ← links to predecessor
├── plan
│   ├── subQuestions[]
│   └── completedQuestions[]
├── findings[]                ← the durable knowledge
│   ├── id, content, confidence, sourceId, subQuestionId
│   └── verificationStatus?
├── sources[]
│   ├── id, url, title, relevance
│   └── retrievedAt
├── reflections[]             ← analyst gap observations
│   ├── content, timestamp
│   └── resolvedInIteration?
└── quality
    ├── verificationPassRate
    ├── failedClaims[]
    └── iterationCount
```

**Invocation patterns:**

```bash
# Fresh research (current behavior)
dotnet run -- "What are approaches to AI safety?"

# Continue from prior session (new)
dotnet run -- "Go deeper on constitutional AI" --prior sessions/abc123.json

# Continue with automatic prior state detection (future)
dotnet run -- "Go deeper on constitutional AI" --prior-dir research/ai-safety/
```

**What `--prior` does:**

1. Loads the prior state file
2. Injects prior findings and plan into the Planner's context as "prior research"
3. The Planner sees what has already been researched and produces a *delta* plan — what's new, what needs deepening, what to skip
4. Research proceeds normally with the delta plan
5. The output state file includes both prior and new findings (superset)

**What `--prior` does NOT do:**

- It does not resume a running process
- It does not modify the prior state file (immutable)
- It does not implicitly load state — the user explicitly chooses what prior context to include
- It does not merge conflicting findings — the new session can supersede prior findings

**The key insight from Step-DeepResearch's todo tool**: decouple research progress from model context. The state file captures *what has been learned*. The model context captures *what the agent is currently thinking about*. These are different things with different lifecycles.

### Implementation Impact

**Phase A — Make session export importable (small):**
- Define a `ResearchStateFile` schema (subset of `SessionExport` — findings, sources, plan, reflections)
- Add `--prior <path>` argument parsing in `Program.cs`
- Modify `ResearchOrchestrator` to accept optional prior state
- Modify Planner prompt to include prior findings summary
- Write the new state file alongside the session JSON

**Phase B — Streaming findings to disk (small):**
- Write findings to a temp state file as they're generated (not just at end)
- If the process crashes, the partial state file survives
- On clean completion, rename temp to final

**Phase C — Knowledge folders (medium, deferred):**
- `--prior-dir <path>` loads all state files in a directory
- Relevance filtering: only inject findings related to the new query
- This enables the "knowledge folder" pattern without a database

### What NOT to Build

- **A session database** — Files are simpler, portable, inspectable, git-friendly. A database adds operational overhead without commensurate benefit at this scale.
- **Automatic prior state detection** — Magic is the enemy of reproducibility. The user should explicitly opt into prior context.
- **Finding deduplication/merging** — When prior findings conflict with new findings, the new session simply has both. The Synthesizer already handles contradictory information (it's trained to weigh evidence). Over-engineering dedup at the persistence layer is premature.
- **Schema migration tooling** — At this stage, if the schema changes, old state files just won't load. Document the version in the file and fail fast. Migration tooling is for when there are hundreds of state files, not now.

---

## Part 3: Alternatives Considered

### Alternative A: No Persistence, Rich Output Only

Improve output delivery (structured envelope, progress streaming, stdout/stderr separation) but skip persistence entirely. Re-running research is cheap and avoids all state management complexity.

**Verdict**: Viable for a v1 — but leaves the iterative refinement workflow broken. Users will naturally want to say "go deeper on X" without losing prior work. This alternative means they can't.

### Alternative B: Database-Backed Memory (SQLite/LiteDB)

Use an embedded database for cross-session findings. Query prior sessions by topic similarity. Full dedup and merging.

**Verdict**: Over-engineered for a CLI tool. Adds a runtime dependency, makes the agent non-portable, and introduces operational complexity (backups, corruption, schema migration). The 12-Factor approach (files as state) achieves 80% of the value at 20% of the complexity.

### Alternative C: Full Pause/Resume (ResearStudio-style)

Serialize the complete agent execution state (including LLM conversation history, tool call state, iteration position) and resume from exactly where it stopped.

**Verdict**: Requires deep integration with MAF's execution model, which currently doesn't support serializable execution state. The engineering cost is high and the benefit over "start fresh with prior findings" is marginal — the LLM can reconstruct its reasoning from the findings more efficiently than replaying an interrupted conversation.

### Alternative D: External Memory Service (Vector DB)

Run a vector database (Qdrant, ChromaDB) as a sidecar. Embed all findings. On new sessions, retrieve semantically similar prior findings.

**Verdict**: Architecturally interesting (and aligned with the survey's §6.2.2 vision of "cognitive-inspired structured memory"), but premature. The agent processes 20-50 findings per session, not thousands. A JSON file with keyword matching is sufficient. When the finding count reaches hundreds, revisit this.

### Alternative E: Git-Tracked Research Artifacts

Write findings as individual Markdown files in a research directory. Use git for versioning and history. Load prior research by reading the directory.

**Verdict**: Creative and appealing — makes research artifacts first-class, versionable, diffable, and human-editable. But adds git as a dependency and the per-finding-file granularity creates filesystem noise. A single state file per session is cleaner. However, this could be a future evolution of the knowledge folder concept.

---

## Part 4: Recommendation and Phasing

### Phase 1: Structured Output (do first)

1. **Stdout/stderr separation** — Report to stdout, everything else to stderr
2. **Progress events** — Phase transitions and finding count to stderr during execution
3. **Quality metadata footer** — Verification pass rate, failed claims in a structured summary
4. Align `SessionExport` schema with the output envelope

**Why first**: Zero risk, immediate user value, no architectural decisions.

### Phase 2: State File Export/Import (do second)

1. **Define `ResearchStateFile` schema** — Subset of session data: plan, findings, sources, reflections, quality
2. **`--prior <path>` flag** — Load prior state and inject into Planner context
3. **Prior-aware planning** — Planner produces delta plans when given prior research
4. **Write state file on completion** — Alongside existing session JSON

**Why second**: The output envelope from Phase 1 defines the data model. Persistence is just "make it round-trip."

### Phase 3: Incremental Durability (do third, optional)

1. **Stream findings to disk** during research (crash recovery)
2. **`--prior-dir` support** for knowledge folders (load all state files in a directory)
3. **Relevance filtering** when loading from a directory

**Why optional**: Phase 2 already enables iterative research. Phase 3 adds resilience and ergonomics but isn't essential.

---

## Paper Sources

| Paper | Key Contribution to This Proposal |
|---|---|
| Deep Research Survey 2025 | Output capability taxonomy (Table 2), memory operations framework (§3.3), memory evolution roadmap (§6.2) |
| Step-DeepResearch 2025 | File system as persistent memory, stateful todo, depth-vs-breadth tension, synthesis-driven drafting |
| SFR-DeepResearch 2025 | Multi-criteria report grading, context management via clean_memory |
| FINDER/DEFT 2025 | 14 failure modes, 39% generation failures, structured evaluation as standard |
| Pensieve/StateLM 2026 | Self-managed context, persistent notes vs. ephemeral content, sawtooth pattern |
| ResearStudio 2025 | Streaming protocol, pause/resume, workspace export |
| MetaGPT 2023 | Structured intermediate outputs improve downstream quality |
| Monadic Context Engineering 2025 | Formal state-threading model, serializable agent state |
| CaveAgent 2025 | Empirical case for statefulness: +10.5% success, -28.4% tokens |
| 12-Factor Agents / Memory Systems | "Memory is an input to the reducer, not internal hidden state" |
| CoALA Taxonomy | Working memory vs. long-term memory architectural distinction |
