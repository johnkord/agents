## Theory of Code Space: Do Code Agents Understand Software Architecture?

Grigory Sapunov ∗ Intento gs@inten.to

## Abstract

AI code agents excel at isolated tasks-writing functions, fixing localized bugsyet struggle with complex, multi-file software engineering that requires understanding how dozens of modules relate to each other. We hypothesize that these failures stem from an inability to construct, maintain, and update coherent architectural beliefs during codebase exploration. We introduce Theory of Code Space (TOCS), a benchmark that evaluates this capability by placing agents in procedurally generated codebases under partial observability, requiring them to build structured belief states over module dependencies, cross-cutting invariants, and design intent. Our framework makes three key design choices: (1) a procedural codebase generator producing medium-complexity Python projects with four typed edge categories reflecting different discovery methods-from syntactic imports to config-driven dynamic wiring-with planted architectural constraints and verified ground truth; (2) a partial observability harness where agents explore under a budget, ensuring that exploration strategy matters; and (3) periodic belief probing via structured JSON, producing a time-series of architectural understanding rather than a single final snapshot. We decompose the Active-Passive Gap from spatial reasoning benchmarks into selection and decision components, and introduce Architectural Constraint Discovery as a code-specific evaluation dimension absent from spatial domains. Preliminary experiments with four rule-based baselines and six frontier LLM agents from three providers validate the benchmark's discriminative power: methods span a wide performance range (Dependency F1 from 0.147 to 0.676), LLM agents discover semantic edge types invisible to all baselines and achieve non-trivial architectural invariant discovery (relaxed F1 up to 0.78), yet weaker models score below simple heuristics-revealing that belief externalization and belief state maintenance are themselves non-trivial capabilities: some models catastrophically forget previously-discovered components between probes, with the surprising result that a smaller model (Gemini 2.5 Flash) maintains perfectly stable beliefs while its larger sibling (Gemini 2.5 Pro) suffers catastrophic belief collapse. We release the complete toolkit as open-source software and invite the community to evaluate additional models and contribute codebase patterns. 2

## 1 Introduction

Large language models achieve remarkable scores on code generation benchmarks [Chen et al., 2021, Austin et al., 2021], creating an expectation that they possess deep understanding of soft-

∗ Work in progress. LLM results are preliminary (single run per model-codebase pair); subsequent versions will include repeated runs with variance estimation, additional models, and codebase patterns. Collaborations welcome.

2 Code: https://github.com/che-shr-cat/tocs

ware architecture. Yet practitioners report a persistent gap: models that solve HumanEval problems with ease produce incoherent results when modifying real codebases with dozens of interdependent modules [Jimenez et al., 2024]. This gap has resisted satisfying explanation.

Recent work on spatial reasoning offers a compelling diagnostic framework. Zhang et al. [2026] introduced the Theory of Space (TOS) benchmark, demonstrating that multimodal models fail to maintain coherent internal 'cognitive maps' when actively exploring partially observable environments. They identified two core phenomena: an Active-Passive Gap (models degrade when they must gather information themselves rather than receiving it upfront) and Belief Inertia (models cannot update their spatial beliefs when the environment changes). We hypothesize that the same latent state maintenance failures explain the struggles of code agents. The term 'cognitive map' traces to Tolman [1948]; in program comprehension, Pennington [1987] and von Mayrhauser and Vans [1995] showed that developers build layered mental models of code-our benchmark operationalizes and measures this process for AI agents.

We propose Theory of Code Space (TOCS), a benchmark that transplants this diagnostic framework to software engineering. Rather than grid worlds, our 'environment' consists of procedurally generated codebases with controlled architectural structure. Rather than allocentric spatial maps, our 'cognitive maps' are structured representations of module dependencies, typed edges, cross-cutting invariants, and design intent. The agent explores under partial observability-opening files one at a time under a budget-and must externalize its architectural belief as structured JSON at regular intervals.

Beyond the spatial analogy, we introduce Architectural Constraint Discovery -a dimension absent from spatial benchmarks. In code, architectural decisions encode checkable constraints : a forbidden dependency enforces a service boundary; a validation chain ensures data integrity. We plant explicit, verifiable constraints in our generated codebases and measure whether agents can discover them.

Our contributions are:

1. The TOCS benchmark framework: the first evaluation for active architectural belief construction, revision, and exploitation in code (§3).
2. A procedural codebase generator producing medium-complexity projects with four typed edge categories, planted invariants, and verified ground truth (§4).
3. Pilot experiments with four baselines and four frontier LLM agents from three providers, showing that the two strongest agents surpass all baselines (F1 = 0.676 and 0.650 vs. 0.577) while revealing a wide capability spread: LLM agents discover all four edge types and, under improved prompts, achieve non-trivial invariant discovery (relaxed F1 up to 0.78), yet weaker models score below simple heuristics (§6).
4. An open benchmark with evaluation harness, scoring pipeline, and community call for expanded model evaluation (§7).

## 2 Related Work

Code Generation Benchmarks. SWE-bench [Jimenez et al., 2024] evaluates bug-fixing in real GitHub repositories but measures patch correctness, not evolving architectural understanding. ContextBench [Li et al., 2026] advances this by logging agent trajectories and scoring context retrieval precision/recall against human-verified gold contexts-measuring what the agent looked at but not what the agent believes about architecture . SWE-ContextBench [Zhu et al., 2026] tests cross-task experience reuse. RepoBench [Liu et al., 2024] and LoCoBench-Agent [Qiu et al., 2025] evaluate repo-level completion and long-context interaction respectively, with LoCoBench observing a 'comprehension-efficiency trade-off' that we formalize as the Active-Passive Gap. RefactorBench [Masai et al., 2025] targets multi-file refactoring but evaluates output correctness without probing belief state. None of these benchmarks require agents to externalize a revisable architectural belief state, impose exploration budgets, or score against typed dependency graphs with planted constraints. We note that our own belief externalization finding (§6) reveals a complementary limitation: even when agents do externalize beliefs, what they report may not reflect what they internally represent.

Code Understanding Tools. CodePlan [Bairi et al., 2024] augments LLMs with static-analysis dependency graphs for change propagation-an engineering solution that validates our premise: agents need architectural maps but currently cannot build them alone. Aider's RepoMap [Gauthier, 2024] uses tree-sitter parsing and PageRank to construct repository context. Code World Models [FAIR CodeGen team et al., 2025] train LLMs on execution traces to improve code reasoning. TOCS provides the diagnostic benchmark to test whether such approaches actually improve architectural belief quality.

Spatial Reasoning. Theory of Space [Zhang et al., 2026] demonstrated Active-Passive Gap and Belief Inertia in grid-world environments with multimodal models. Related spatial benchmarks include SpatialVLM [Chen et al., 2024], Habitat [Savva et al., 2019], and OpenEQA [Majumdar et al., 2024], which evaluate spatial reasoning and embodied question answering but do not require constructing revisable belief states. We transplant this framework to code, adding architectural constraint discovery (absent from spatial domains where object placement 'why' is rarely meaningful). TOM-SWE [Zhou et al., 2025] models the user's mental state in SWE agents-complementary to our focus on the agent's belief about the codebase .

## 3 Theory of Code Space

## 3.1 Problem Formulation

We formalize codebase understanding as belief construction over a latent architectural state. Let S denote the ground-truth architecture, comprising a typed dependency graph G = ( V, E ) with edge types τ ∈ { IMPORTS , CALLS\_API , DATA\_FLOWS\_TO , REGISTRY\_WIRES } , a set of cross-module invariants I , and exported contracts C . At time t , the agent has history h t = ( o 0: t , a 0: t ) and we evaluate three operations on its belief M t ( S ) . Since M t is a latent internal state, we measure it through probing : periodically asking the agent to externalize its belief as structured JSON (§3.3). We evaluate:

Construct Build M t from partial observations gathered through active exploration.

- Revise Update M t →M t +∆ t when the environment mutates ( S → S ′ ) and the agent encounters evidence of the change.
- Exploit Use M t to perform a downstream engineering task correctly (v1.0; v0.1 uses counterfactual probes as a proxy).

## 3.2 Environment and Action Space

The agent interacts with the codebase through five actions with fi xed tool semantics :

- LIST ( d ): Filenames in directory d (no contents, no recursion).
- OPEN ( f ): Full contents of file f . Costs 1 action.
- SEARCH ( q ): Matching filepaths + line numbers. No content snippets. Costs 1 action.
- INSPECT ( f , s ): Type signature + docstring of symbol s . No body. Costs 1 action.
- DONE (): Terminate. Costs 0 actions.

The agent operates under a budget B (default B =20 ). Critically, SEARCH returns only locations, never content-ensuring that architectural understanding requires deliberate OPEN decisions. INSPECT provides a lightweight discovery channel: docstrings often describe module purpose and relationships (e.g., 'wraps stage X'), allowing agents to gather architectural hints at the cost of one action without reading full file contents.

## 3.3 Cognitive Map Probing

Every K =3 actions, the harness interrupts the agent with a structured prompt requesting it to externalize its current architectural belief as JSON. Critically, probing is free : it does not consume an action from the budget B , and the agent retains the same remaining budget before and after a probe. This ensures that measurement does not interfere with exploration strategy-agents are evaluated on their understanding, not on a trade-off between exploring and reporting.

The externalized cognitive map ˆ M t contains: (1) component beliefs with status (observed/inferred/unknown), purpose, exported symbols with typed signatures, and typed dependency edges with per-element confidence; (2) invariant beliefs with structured canonical form ( type, src, dst, via, pattern ) and evidence pointers; (3) uncertainty tracking -explicit list of unexplored regions. The resulting time-series of maps (one every K steps) captures how understanding develops, not just the final state.

## 3.4 Evaluation Modes

TOCS decomposes the Active-Passive Gap through four conditions:

- Active : Agent chooses actions under budget B .
- Passive-Full : Agent receives the entire codebase; probed once.
- Passive-Oracle : Agent receives B fi les selected by oracle (maximum ground-truth connectivity); one file per step, probed every K .
- Passive-Replay : Agent receives the exact observation trace from a prior active run, without making decisions. Each observation counts as one step.

This decomposes: APGtotal (passive-full -active) into APGselection (passive-oracle -active, the cost of choosing which fi les) and APGdecision (passive-replay -active, the cost of deciding what to do with observations).

Scope note: v0.1 evaluates the Construct operation in Active mode only. The passive conditions and APG decomposition are implemented in the harness and reserved for future work with frontier model evaluations.

## 3.5 Architectural Constraint Discovery

Beyond structural mapping, we probe whether agents discover planted architectural constraints:

- Forbidden dependency : 'Module A must not import module C directly.'
- Interface-only access : 'Module X must access Y only through interface Z .'
- Validation chain : 'Data must pass through validation before reaching module W .'

Each constraint has a discoverability requirement : test evidence, structural patterns, or documentation in the codebase. Scored via counterfactual multiple-choice probes ('Which change would violate an architectural constraint?').

## 4 Procedural Codebase Generation

## 4.1 Architecture Grammar

Our v0.1 generator produces Pipeline architecture codebases with controlled anti-triviality measures. A PipelineTemplate grammar defines:

- Domain pools : Three domains (data ETL, log processing, text processing), each with 8 semantically coherent processing stages. Medium complexity selects 6-8 stages.
- Module roles : Infrastructure (models, base class, config, exceptions, registry), stages (processing steps implementing a common ABC), adapters (wrapping stages with additional behavior), middleware (cross-cutting decorators), utilities, and legacy/distractor modules.
- Anti-triviality : (1) Registry wiring -stages connected via config, not direct imports; (2) Adapter indirection -ABC interface layer; (3) Distractor modules -files not in the main pipeline; (4) Neutral naming -mod\_a.py , not extract.py ; (5) Hidden invariants -constraints discoverable only by reading function bodies or tests.

## 4.2 Four Edge Types

Each generated codebase contains edges of four types, reflecting different methods of discovery :

- IMPORTS ( ∼ 67%): Python import statements. Discoverable by AST parsing.
- CALLS\_API ( ∼ 17%): Runtime function calls between modules. Requires reading function bodies; docstrings may provide hints (e.g., 'delegates to module X').

- REGISTRY\_WIRES ( ∼ 9%): Config-driven connections where a registry module dynamically loads stages via importlib based on names listed in a JSON config file. No static import statement exists. Requires reading both the config and the registry's loading logic.
- DATA\_FLOWS\_TO ( ∼ 7%): Data dependency where one module's output feeds another's input. Requires understanding orchestration logic; docstrings on the orchestrator may describe the data flow.

These proportions are not tunable parameters but emerge from the architectural template : a Pipeline with N stages naturally generates ∼ 3-4 × more IMPORTS edges because every module requires static imports, while semantic edge types arise only at specific architectural boundaries (adapter → stage, orchestrator → stage, consecutive stages). This mirrors real-world Pipeline codebases, where import edges dominate. Different architectural patterns (event-driven, microservices) would yield different distributions-a key motivation for extending the generator. Crucially, roughly one-third of edges are invisible to import-following (specific to our Pipeline template; other architectural patterns would yield different proportions), creating a meaningful gap between syntactic analysis and semantic understanding.

## 4.3 Invariant Planting

Each codebase contains 15-16 planted constraints across five types: BOUNDARY (forbidden dependencies), DATAFLOW (required processing chains), INTERFACE (access-only-through-ABC), INVARIANT (naming/structural conventions), and PURPOSE (design rationales). Every constraint has a structured canonical form with five fields for machine-comparable scoring: type (constraint category), src and dst (the modules involved), via (the intermediary or mechanism, e.g., an ABC interface), and pattern (the structural rule, e.g., 'no direct import'). For example, a boundary constraint might be (boundary, mod\_a, mod\_c, base.StageBase, 'must access only through ABC') . Each constraint has at least one evidence source (test file, structural pattern, or documentation).

## 4.4 Generation Statistics

Table 1: Generated codebase characteristics (medium complexity, 3 seeds).

| Metric         | Seed 42   | Seed 123   | Seed 999   |
|----------------|-----------|------------|------------|
| Modules        | 27        | 30         | 27         |
| Total edges    | 70        | 84         | 70         |
| IMPORTS        | 47 (67%)  | 56 (67%)   | 47 (67%)   |
| CALLS_API      | 12 (17%)  | 15 (18%)   | 12 (17%)   |
| DATA_FLOWS_TO  | 5 (7%)    | 6 (7%)     | 5 (7%)     |
| REGISTRY_WIRES | 6 (9%)    | 7 (8%)     | 6 (9%)     |
| Invariants     | 15        | 16         | 15         |
| Sub-packages   | 5         | 5          | 5          |

## 5 Metrics

Dependency F1. Compare predicted edges from the cognitive map against ground truth. An edge matches if (source, target, type) all agree via exact string matching. Targets must be specific file paths: a directory-level edge (e.g., registry.py → stages/ ) does not match filelevel ground truth (e.g., registry.py → stages/mod\_a.py ). Similarly, symbol-qualified targets (e.g., base.py::StageBase instead of base.py ) do not match even when the file path portion is correct. This strict matching is deliberate-our ground truth is component-level (one file = one node)-but means that models reporting correct relationships at the wrong granularity or format receive no credit. We report precision, recall, and F1 separately.

Invariant F1. Match agent-discovered constraints against planted constraints via structured form comparison-exact field matching on (type, src, dst, via) , not text similarity. This eliminates ambiguity in natural-language descriptions.

Confidence Calibration. Expected Calibration Error (ECE) between agent-stated per-edge confidence and actual correctness, binned into 5 confidence intervals.

Two Efficiency Curves. Action-efficiency : AUC(F1 vs. total actions)-the headline metric. Observation-efficiency : AUC(F1 vs. OPEN count)-diagnostic, isolating information gain per file opened. Belief is piecewise-constant between probes; integration is trapezoidal.

Active-Passive Gap. For each metric m : APG m = m passive -m active, decomposed into selection and decision components as described in §3.

Belief Revision Score (BRS). After a mutation at time t m : BRS = | correctly updated | / | affected elements | . Decomposed into Inertia-proper (correctly-believed elements not updated after evidence) and Impact-discovery (missing elements newly found). A sham condition (evidence without actual change) measures Gullibility. (BRS evaluation is implemented but reserved for future work.)

## 6 Experiments

Weevaluate four rule-based exploration strategies and five frontier LLM agents from three providers on three generated codebases (seeds 42, 123, 999) under identical conditions: budget B =20 , probe interval K =3 . These results are preliminary; we report them to validate the benchmark's discriminative power and invite community replication.

## 6.1 Methods

## Rule-based baselines.

- Oracle : Outputs the ground-truth graph directly. Upper bound (F1 = 1.0).
- Config-Aware : Lists all directories, opens config/registry files first, parses module references, then follows imports BFS.
- Random : Lists root, then opens files uniformly at random until budget exhausted. Builds map from observed imports.
- BFS-Import : Opens files breadth-first following import chains.

All baselines build cognitive maps from AST-parsed imports and config references.

## LLMagents.

- GPT-5.3-Codex : OpenAI's code-specialized reasoning model ( gpt-5.3-codex ). A reasoning model (temperature fixed at 1 per API constraint).
- Claude Sonnet 4.6 : Anthropic's frontier coding model ( claude-sonnet-4-6 ).
- Gemini 2.5 Flash : Google's reasoning-optimized flash model ( gemini-2.5-flash ). A 'thinking' model with extended internal reasoning.
- Gemini 2.5 Pro : Google's reasoning-optimized pro model ( gemini-2.5-pro ). Larger sibling of 2.5 Flash.
- Gemini 3 Flash : Google's frontier flash model ( gemini-3-flash-preview ).
- Gemini 3.1 Pro : Google's most capable model ( gemini-3.1-pro-preview ). The flagship of the Gemini 3 family.

All models receive identical prompts: a system prompt describing the partial observability environment and available actions, per-turn action prompts showing remaining budget and opened files, and structured JSON probe prompts with schema, per-type field semantics, and worked examples for each invariant type (Appendix A). Relative to an earlier prompt version, the current probe includes explicit edge type decision rules and pairwise invariant decomposition instructions. Temperature is set to 0 except for GPT-5.3-Codex (reasoning model constraint). All probe responses were parsed successfully via the deterministic repair parser (zero format failures across all runs).

Table 2: Performance over 3 codebases (mean ± half-range). Baselines use AST parsing; LLM agents use prompted JSON externalization with improved probe prompts (per-type field semantics, pairwise invariant decomposition). Best non-Oracle in bold . Inv F1 uses relaxed matching (§3.3).

| Method            | Type     | Dep F1       |   Precision |   Recall | AUC          | Inv F1       | Files   |
|-------------------|----------|--------------|-------------|----------|--------------|--------------|---------|
| Oracle            | baseline | 1.000        |       1     |    1     | -            | -            | -       |
| Config-Aware      | baseline | 0.577 ± .014 |       0.736 |    0.475 | 0.212 ± .007 | 0.000        | 13      |
| Random            | baseline | 0.538 ± .028 |       1     |    0.368 | 0.142 ± .008 | 0.000        | 13      |
| BFS-Import        | baseline | 0.293 ± .060 |       1     |    0.173 | 0.079 ± .003 | 0.000        | 13      |
| GPT-5.3-Codex     | LLM      | 0.676 ± .059 |       0.782 |    0.597 | 0.306 ± .018 | 0.739 ± .067 | 15      |
| Claude Sonnet 4.6 | LLM      | 0.664 ± .026 |       0.983 |    0.502 | 0.350 ± .005 | 0.778 ± .025 | 13      |
| Gemini 2.5 Flash  | LLM      | 0.470 ± .100 |       0.642 |    0.373 | 0.259 ± .037 | 0.517 ± .154 | 14      |
| Gemini 3.1 Pro    | LLM      | 0.321 ± .128 |       0.764 |    0.212 | 0.140 ± .049 | 0.423 ± .209 | 11      |
| Gemini 2.5 Pro    | LLM      | 0.242 ± .034 |       1     |    0.138 | 0.205 ± .027 | 0.376 ± .034 | 12      |
| Gemini 3 Flash    | LLM      | 0.147 ± .012 |       0.897 |    0.08  | 0.133 ± .016 | 0.125 ± .000 | 12      |

Table 3: Edge type recall by method (aggregated over 3 codebases). n = total ground-truth edges of each type. Values show true recall (matched edges / ground truth). Bold = best non-Oracle recall per type. † P= per-type precision where significant over-generation occurs (false positives exceed true positives).

| Method            |   IMPORTS ( n =150 ) |   CALLS_API ( n =39 ) |   DATA_FLOWS ( n =16 ) | REG._WIRES ( n =19 )   |
|-------------------|----------------------|-----------------------|------------------------|------------------------|
| Config-Aware      |                 0.58 |                  0    |                   0    | 1.00 † P=.33           |
| Random            |                 0.55 |                  0    |                   0    | 0.00                   |
| BFS-Import        |                 0.25 |                  0    |                   0    | 0.00                   |
| GPT-5.3-Codex     |                 0.69 |                  0.15 |                   0.31 | 1.00 † P=.33           |
| Claude Sonnet 4.6 |                 0.56 |                  0.15 |                   0    | 1.00                   |
| Gemini 2.5 Flash  |                 0.47 |                  0.18 |                   0    | 0.37 † P=.58           |
| Gemini 3.1 Pro    |                 0.27 |                  0    |                   0.5  | 0.00                   |
| Gemini 2.5 Pro    |                 0.12 |                  0.08 |                   0    | 0.53                   |
| Gemini 3 Flash    |                 0.11 |                  0    |                   0    | 0.05                   |

## 6.2 Results

## 6.3 Analysis

The following observations are based on a single run per model-codebase pair across six LLM agents from three providers. We present them to illustrate the benchmark's discriminative power; the specific rankings and effect sizes should be treated as preliminary until confirmed with repeated runs and additional models.

Two LLM agents surpass all baselines. GPT-5.3-Codex achieves the highest Dependency F1 = 0.676, and Claude Sonnet 4.6 reaches F1 = 0.664, both surpassing the best rule-based strategy (Config-Aware, 0.577) by 9-10 points. GPT opens 15 files per codebase (vs. 13 for baselines) and attains the highest recall (0.597), while Claude opens only 13 files but with near-perfect precision (0.983). This precision-recall tradeoff reflects different exploration strategies: GPT favors broad coverage, Claude favors targeted, confident exploration. Both discover all ground-truth REG-ISTRY\_WIRES edges (Table 3)-matching Config-Aware's recall but with GPT matching ConfigAware's 33% precision on this type (over-generating false positives) while Claude achieves perfect REGISTRY\_WIRES precision (zero false positives).

LLM agents discover all four edge types. Table 3 reveals the most distinctive finding: LLM agents collectively discover all four edge types , while baselines find at most two (IMPORTS and REGISTRY\_WIRES for Config-Aware). GPT-5.3-Codex achieves the highest IMPORTS recall (69%, exceeding even Config-Aware's 58%). For DATA\_FLOWS\_TO edges-which require multi-

<!-- image -->

(a) F1 vs. exploration steps. Dashed = LLM agents, solid = baselines. GPT-5.3-Codex reaches highest final F1; Claude Sonnet 4.6 leads on efficiency (AUC).

<!-- image -->

(b) Edge type discovery heatmap. Cells show raw count / ground truth. LLM agents ( ⋆ ) discover all four edge types; baselines find at most two.

<!-- image -->

- (c) Precision, recall, and F1 per method. GPT-5.3-Codex and Claude Sonnet 4.6 surpass all baselines; weaker LLMs show high precision but low recall.

Figure 1: Exploration analysis across 4 baselines and 6 LLM agents on medium-complexity codebases ( n =3 , budget B =20 ).

hop reasoning, tracing one module's return value through the runner into the next module's inputGemini 3.1 Pro achieves the highest recall (50%), with perfect discovery on one codebase (6/6 interstage data flow edges), followed by GPT-5.3-Codex (31%). Claude found zero DATA\_FLOWS\_TO edges despite reading the runner code-it inferred REGISTRY\_WIRES from the config alone without reading enough stages to trace data flow. This exploration-comprehension tradeoff shows that even correct understanding of the orchestrator is insufficient-agents must also open the linked modules to infer inter-stage data dependencies. Gemini 2.5 Flash discovers CALLS\_API (18% recall) and REGISTRY\_WIRES (37% recall) edges invisible to BFS-Import and Random baselines.

Improved prompts unlock invariant discovery. With improved probe prompts providing pertype field semantics and pairwise decomposition instructions, LLM agents achieve substantial invariant F1 under relaxed matching (§3.3): Claude Sonnet 4.6 leads with 0.778, GPT-5.3-Codex achieves 0.739, and Gemini 2.5 Flash reaches 0.517. This is a dramatic improvement over the zero scores obtained with the earlier prompt version, confirming that invariant discovery was primarily a prompt specification problem rather than a model capability gap (§7). Gemini 2.5 Pro achieves 0.376, while Gemini 3 Flash-which struggles with edge discovery-scores only 0.125. Baselines score zero on invariants as they cannot reason about cross-cutting constraints.

The belief externalization bottleneck. The three Gemini models illustrate the belief externalization problem with distinct failure modes. Gemini 2.5 Flash opens 14 files (comparable to baselines) and demonstrates clear code comprehension-action logs show import-chain-following behavior. Yet its IMPORTS recall (47%) lags behind Config-Aware (58%) and GPT (69%) due to (1) granularity mismatch : reporting directory-level edges instead of file-level edges; and (2) inference underreach : recognizing patterns but failing to report all visible imports. Gemini 2.5 Pro and 3 Flash

reveal a more fundamental failure: catastrophic belief state instability. Despite opening comparable numbers of files, both models fail to accumulate knowledge across probes. Gemini 3 Flash exhibits recency bias-each probe reports only 3-5 recently-examined components, discarding earlier discoveries entirely. Gemini 2.5 Pro shows a different pattern: it builds a reasonable map (peak F1 = 0.33 at step 9) then destroys it, losing 12 correct edges in a single probe step. In contrast, Gemini 2.5 Flash loses zero correct edges across all probes-pure monotonic accumulation. This suggests that belief state maintenance -not just externalization format-is itself a non-trivial capability that varies dramatically across model versions. The gap between what an agent has seen and what it retains in its belief state is a first-order confounder in any belief-probing benchmark.

Precision-recall profiles differ qualitatively. Baselines using AST parsing (BFS-Import, Random) achieve perfect precision-every reported edge is correct. Among LLMs, Claude Sonnet 4.6 and Gemini 2.5 Pro both achieve near-perfect precision (0.983 and 1.000 respectively), but with dramatically different recall: Claude attains 0.502 while Gemini 2.5 Pro manages only 0.138. This decoupling reveals that high precision alone does not indicate strong architectural understandingit may simply reflect extreme conservatism. GPT-5.3-Codex trades some precision (0.782) for the highest recall (0.597). Gemini 2.5 Flash has the lowest LLM precision (0.642), generating false CALLS\_API and DATA\_FLOWS\_TO edges alongside its correct discoveries.

Efficiency strongly favors LLM agents. Under the Action AUC metric-which rewards early discovery-Claude Sonnet 4.6 dominates (0.350), followed by GPT-5.3-Codex (0.306), both well above Config-Aware (0.212). Gemini 2.5 Flash (0.259) outperforms all baselines except ConfigAware on AUC, while Gemini 2.5 Pro (0.205) falls just below Config-Aware despite its perfect precision. This reflects the tradeoff between comprehension quality and externalization completeness: strong AUC requires both understanding and reliably reporting that understanding at each probe point.

Random's competitiveness and budget effects. Random exploration (F1 = 0.538) performs nearly as well as Config-Aware (0.577). This near-parity reflects the current scale: budget B =20 on 27-30 modules allows opening roughly half the files, making raw coverage a strong proxy for import discovery. A budget sweep (Appendix C) confirms that at B =10 , Config-Aware's advantage grows to 3 × over Random (0.175 vs. 0.056). The meaningful discrimination at B =20 lies in understanding what you've seen : LLM agents discover qualitatively different edge types despite comparable file coverage.

## 7 Discussion

## 7.1 Implications for Code Agent Design

Current code agents (SWE-agent, Aider, Claude Code, Cursor) use tool-augmented retrieval to navigate repositories, but none explicitly construct or maintain a structured architectural belief. Our results show that the strongest LLM agents can surpass rule-based strategies when they combine semantic comprehension with reliable belief externalization (GPT-5.3-Codex and Claude Sonnet 4.6 both exceed all baselines), but that weaker models fail at the externalization step despite demonstrating code understanding in their exploration behavior.

This suggests four improvement paths: (1) hybrid approaches combining AST-level import extraction with LLM semantic analysis, ensuring structural completeness while adding semantic depth; (2) belief externalization training -explicitly optimizing models to faithfully serialize architectural knowledge into structured formats; (3) exploration strategy optimization -broader file coverage (GPT's 15 files vs. Claude's 13) directly correlates with higher recall and unique edge type discovery; and (4) explicit state management -providing agents with a persistent, accumulative data structure rather than relying on implicit belief state maintenance, which our results show can catastrophically fail even in capable models.

## 7.2 The Belief Externalization Problem

The spread across models is striking: GPT-5.3-Codex recalled 69% of IMPORTS edges, while Gemini 3 Flash and Gemini 2.5 Pro recalled only 11-12% despite opening a similar number of files.

This 6 × gap in IMPORTS recall-for edges the models demonstrably saw -suggests that belief externalization capability varies dramatically across model families, not just between LLMs and baselines.

Both GPT-5.3-Codex and Claude Sonnet 4.6 demonstrate that faithful externalization is achievable with current models: Claude with near-perfect precision (0.983), GPT with the highest recall (0.597). The challenge appears model-specific rather than fundamental: it is not that structured JSON is inherently difficult to produce, but that some models have not been optimized for this capability.

Belief state instability: a new failure mode. The Gemini model family reveals a striking failure mode beyond simple externalization weakness: belief state instability that manifests in three distinct patterns across four models. Gemini 2.5 Pro shows catastrophic collapse : building a reasonable map by step 9 (peak F1 = 0.33), then losing 12 correct edges in a single probe step (F1 → 0.09), before partially recovering. Gemini 3 Flash shows severe recency bias : each probe reports only 3-5 recently-examined components with no accumulation. Gemini 3.1 Pro shows variable instability : on its best codebase (F1 = 0.458) it accumulates monotonically like strong models, but on its worst (F1 = 0.203) it degrades to Gemini-3-Flash-level recency bias-the map collapses to zero edges at step 18 before partially recovering. This high variance (half-range ± 0.128) makes Gemini 3.1 Pro the least consistent model in our evaluation.

Critically, Gemini 2.5 Flash-the smallest model in the family-exhibits zero correct-edge loss across all probes, with purely monotonic accumulation. This counter-intuitive result (the smallest model maintaining the most stable beliefs) suggests that belief state maintenance is not simply a function of model scale but may depend on training objectives or architectural choices. The instability appears to stem from treating each probe as 'summarize the architecture from scratch' rather than 'incrementally update your running belief state'-a fundamental misinterpretation of the task that prompt design alone may not resolve.

Importantly, externalization quality is likely prompt-dependent : the same model may produce higher-quality maps under different prompt designs (e.g., few-shot examples, step-by-step decomposition, explicit schema walkthroughs, or chain-of-thought elicitation before the final JSON). Our use of identical prompts across all models controls for this variable experimentally but measures performance under a specific prompt regime rather than each model's ceiling. Future work should include prompt ablation studies-varying schema detail, number of examples, and elicitation strategy-to disentangle model capability from prompt sensitivity.

Case study: prompt ambiguity and edge type confusion. Error analysis of Gemini 2.5 Flash's 40 false positive edges across 3 codebases reveals that only 2 (5%) are true hallucinations -fabricated relationships with no basis in the code. The remaining 95% reflect real relationships reported in the wrong format or with the wrong edge type. Crucially, many of these 'errors' stem from ambiguity in the probe prompt itself .

The dominant failure mode (45% of FPs) is edge type confusion : when the model sees from config import PipelineConfig followed by PipelineConfig() , it reports both an IMPORTS edge and a CALLS\_API edge. Our probe defines CALLS\_API as 'the file calls a public function exported by the target,' but does not state whether edge types are mutually exclusive, whether class instantiation counts as 'calling a function,' or whether an edge should be IMPORTS or CALLS\_API when both an import statement and a function call exist. Gemini's interpretation-reporting bothis arguably reasonable given the underspecified definitions. Claude Sonnet 4.6 happens to match the ground-truth convention, but this may reflect better prompt compliance rather than deeper understanding:

| Edge                         | Gemini 2.5 Flash   | Claude Sonnet 4.6   |
|------------------------------|--------------------|---------------------|
| cli → config                 | IMPORTS            | IMPORTS             |
| cli → runner                 | IMPORTS            | IMPORTS             |
| cli → config::PipelineConfig | CALLS_API (FP)     | -                   |
| cli → runner::run_pipeline   | CALLS_API (FP)     | -                   |
| cli → runner                 | -                  | CALLS_API (TP)      |

Similarly, 22% of FPs come from treating non-component files (tests, JSON configs) as edge endpoints-but the prompt does not explicitly define what counts as a 'component.' This taxonomy reveals that prompt specification is itself a first-order variable in belief-probing benchmarks: what looks like an externalization failure may be a legitimate alternative interpretation of underspecified instructions. Future versions should provide explicit edge type decision rules (e.g., 'if both IMPORTS and CALLS\_API apply, report only CALLS\_API') and an explicit component whitelist or exclusion criteria.

## 7.3 Documentation as a Discovery Channel

Real codebases contain rich documentation beyond code: README files, architecture decision records (ADRs), API specifications (OpenAPI/Swagger), and external resources (wikis, design documents). These serve as a major architectural discovery channel that our v0.1 codebases-which contain only code-level docstrings-do not yet model.

Extending the generator to produce standalone documentation files opens several directions. First, some architectural edges or invariants could be discoverable only through documentation, testing whether agents read docs or dive straight into code. Second, documentation staleness -a pervasive real-world problem where docs describe a prior version of the architecture-creates a natural testbed for the REVISE evaluation mode: agents would need to reconcile contradictions between documentation and code, deciding which to trust. Third, external documentation could be modeled as an additional action (e.g., FETCH\_DOC ) with its own budget cost, testing whether agents know when to look outside the repository.

## 7.4 Benchmark Design Lessons

Building and running TOCS v0.1 exposed several pitfalls in belief-probing benchmark design that we document here for the benefit of future work.

L1: Define edge type semantics with decision rules, not just descriptions. Our probe prompt defines CALLS\_API as 'the file calls a public function exported by the target' alongside IMPORTS as 'a Python import statement exists.' When code both imports and calls a symbol, models must decide which edge type to report-but the prompt does not state whether types are mutually exclusive, nor whether class instantiation counts as 'calling a function.' This ambiguity caused 45% of Gemini 2.5 Flash's false positives (§7). Fix: provide explicit decision trees (e.g., 'if a file imports and calls a symbol, report CALLS\_API only; report IMPORTS only for unused imports').

L2: Define component boundaries explicitly. Our probe prompt asks models to list 'components' but never defines whether test files, configuration files, or \_\_init\_\_.py fi les qualify. Models that included test\_smoke.py or pipeline\_config.json as edge endpoints were penalized despite identifying real relationships. Fix: provide an explicit inclusion rule (e.g., 'components are Python source files excluding tests and configs') or enumerate valid components in the probe.

L3: Specify structured field semantics per invariant type. Our initial invariant schema used generic fields ( src , dst , via ) without per-type semantics, and all models scored Invariant F1 = 0.0. After adding per-type field definitions and worked examples in the improved prompt, invariant F1 jumped to 0.52-0.78 under relaxed matching-confirming this was a prompt specification problem, not a model capability gap.

L4: Validate scoring against alternative correct answers. Strict exact-match scoring conflates format compliance with architectural understanding. A model reporting registry.py → stages/ with 7 REGISTRY\_WIRES edges 'knows' the same fact as the 7 file-level ground-truth edges but receives zero credit. Fix: implement tiered scoring (full credit for exact match, partial credit for semantically equivalent answers) or normalize predictions before comparison.

L5: Prompt specification is a first-order experimental variable. Across all failure modes, the recurring theme is that what looks like a model capability gap may be a prompt specification gap . We recommend that belief-probing benchmarks treat their probe prompts as artifacts requiring the same rigor as evaluation metrics-versioned, ablated, and reported alongside results.

L6: In scratchpad mode, probe prompts affect exploration, not just scoring. We ran all four LLM agents on the same three codebases under two probe prompt versions: an initial version with minimal schema guidance, and an improved version adding per-type field semantics, file-path granularity instructions, and pairwise invariant decomposition examples. In our probe\_as\_scratchpad track, the agent's JSON map response is retained in its conversation context, serving as a selfassessment visible to the agent on subsequent turns. Table 4 shows consistent F1 improvements across all models, but with dramatically different magnitudes.

Table 4: Effect of improved probe prompt on Dependency F1 (same 3 codebases, same models, only probe prompt changed). ∆ = new -old.

| Model             |   Old Prompt |   New Prompt |   ∆ F1 |
|-------------------|--------------|--------------|--------|
| Gemini 2.5 Flash  |        0.328 |        0.469 |  0.141 |
| GPT-5.3-Codex     |        0.564 |        0.676 |  0.113 |
| Claude Sonnet 4.6 |        0.639 |        0.65  |  0.012 |

The mechanism is not just better serialization. Action log analysis reveals that in probe\_as\_scratchpad mode, the improved prompt changes exploration behavior itself. On one codebase (seed 999), GPT-5.3-Codex follows an identical action sequence under both prompts for the first 15 steps; at step 16-immediately after a probe point-the old run opens a utility file and stops, while the new run opens 4 additional stage modules. The more structured probe revealed gaps in the agent's own map, causing it to redirect remaining budget toward coverage. This yielded 26 additional correct edges (F1 from 0.392 to 0.636 on that codebase alone). Claude Sonnet 4.6, which already externalized comprehensively under the old prompt, showed minimal improvement (+0.012)-suggesting its belief externalization was already near-ceiling. This has two implications for benchmark design: (1) in scratchpad mode, the probe is not a passive measurement instrument but an active intervention that shapes the agent's exploration policy; and (2) models with weaker baseline externalization benefit disproportionately from prompt improvements, meaning model rankings are partially prompt-dependent.

## 7.5 Limitations

TOCS v0.1 is deliberately narrow in scope. Generated codebases, while structurally realistic, may not capture all patterns of real-world architecture. In particular, our use of neutral filenames ( mod\_a.py rather than authentication\_service.py ) removes naming priors that real codebases provide, making the task harder in one dimension while keeping it easier in others (clean structure, no organic complexity, limited dead code). This design choice isolates architectural reasoning from lexical guessing, but means our difficulty estimates may not transfer directly to real repositories. Additionally, our generated codebases contain only code-level documentation (docstrings) and lack standalone documentation files (README, ARCHITECTURE.md, design documents) that real repositories provide as a major architectural discovery channel. We evaluate a single architectural pattern (Pipeline) in a single language (Python). Our LLM results are preliminary: six models from three providers, each evaluated on 3 codebases with a single run per model-codebase pair (no variance estimation across runs). All LLM results use a single prompt design; different formulations could yield substantially different rankings. Our improved probe prompt addresses several ambiguities from the initial version (per-type field semantics, pairwise decomposition, edge type decision rules) and substantially improved invariant scores, but may still contain underspecifications. The cognitive map externalization is a proxy for internal representation-a model may 'know' more than it serializes, as the wide variation across models suggests. We plan to expand to additional models (open-weight), architectural patterns, repeated runs with variance estimation, and the REVISE evaluation mode.

## 7.6 Community Call

We release the complete TOCS toolkit-codebase generator, partial observability harness, evaluation pipeline, and scoring metrics-as open-source software. We invite the community to:

1. Evaluate additional models : Run open-weight models (Llama, Qwen, DeepSeek) and replicate our frontier model results with repeated runs.

2. Contribute codebase patterns : Extend beyond Pipeline to event-driven, microservices, plugin systems, hexagonal architecture (see CONTRIBUTING.md).
3. Add languages : The harness is language-agnostic; the generator needs new templates for TypeScript, Go, etc.
4. Test scaffold-augmented agents : Evaluate whether static analysis tools, RepoMap-style context, or explicit state tracking improve map quality.

## 8 Conclusion

Weintroduced TOCS, a benchmark for evaluating whether code agents can construct coherent architectural beliefs through active codebase exploration under partial observability. Our procedural generator produces medium-complexity codebases with four typed edge categories and planted invariants. Preliminary experiments with four baselines and six frontier LLM agents from three providers reveal that: (1) the two strongest LLM agents (GPT-5.3-Codex, F1 = 0.676; Claude Sonnet 4.6, F1 = 0.664) surpass all baselines by combining semantic comprehension with reliable belief externalization; (2) LLM agents collectively discover all four edge types-including DATA\_FLOWS\_TO edges (Gemini 3.1 Pro, 50% recall; GPT-5.3-Codex, 31%) and REGISTRY\_WIRES edges (GPT and Claude, 100% recall)-capabilities entirely absent from rule-based methods; (3) improved probe prompts unlock invariant discovery (relaxed F1 up to 0.78), confirming that invariant scoring failures were primarily a prompt specification problem; yet (4) weaker LLM agents score below simple heuristics due to catastrophic belief state instability-some models lose previously-discovered edges between probes, with a 6 × spread in IMPORTS recall across models (11-69%), confirming that both belief externalization and belief state maintenance remain key capability gaps. The 32-point F1 gap between Oracle and the best LLM agent defines a challenging and discriminative evaluation space. We release TOCS as an open benchmark and invite the community to replicate and extend these preliminary results.

## References

- Jacob Austin, Augustus Odena, Maxwell Nye, et al. Program synthesis with large language models. arXiv preprint arXiv:2108.07732 , 2021.
- Ramakrishna Bairi, Atharv Sonwane, Aditya Kadam, Arun Iyer Daware, Suresh Upasani, Sriram K. Rajamani, Mukul Raghothaman, and Chandra Shet. CodePlan: Repository-level coding using LLMs and planning. In Proceedings of the ACM International Conference on the Foundations of Software Engineering (FSE) , 2024. arXiv:2309.12499.
- Boyuan Chen, Zhuo Xu, Sean Kirmani, et al. SpatialVLM: Endowing vision-language models with spatial reasoning capabilities. arXiv preprint arXiv:2401.12168 , 2024.
- Mark Chen, Jerry Tworek, Heewoo Jun, Qiming Yuan, et al. Evaluating large language models trained on code. arXiv preprint arXiv:2107.03374 , 2021.
- FAIR CodeGen team, Jade Copet, Quentin Carbonneaux, Gal Cohen, Jonas Gehring, Jacob Kahn, et al. CWM: An open-weights LLM for research on code generation with world models. arXiv preprint arXiv:2510.02387 , 2025.
- Paul Gauthier. Aider: AI pair programming in your terminal. https://aider.chat , 2024. Includes RepoMap: repository map generation using tree-sitter and PageRank.
- Carlos E Jimenez, John Yang, Alexander Wettig, Shunyu Yao, Kexin Pei, Ofir Press, and Karthik Narasimhan. SWE-bench: Can language models resolve real-world GitHub issues? In International Conference on Learning Representations (ICLR) , 2024. arXiv:2310.06770.
- Han Li, Letian Zhu, Bohan Zhang, Rili Feng, Jiaming Wang, Yue Pan, Earl T. Barr, Federica Sarro, Zhaoyang Chu, and He Ye. ContextBench: A benchmark for context retrieval in coding agents. arXiv preprint arXiv:2602.05892 , 2026.
- Tianyang Liu, Canwen Xu, and Julian McAuley. RepoBench: Benchmarking repository-level code auto-completion systems. In International Conference on Learning Representations (ICLR) , 2024.

- Arjun Majumdar, Anurag Ajay, Xiaohan Zhang, Pranav Putta, Sriram Yenamandra, Mikael Henaff, Sneha Silwal, Paul Mcvay, Oleksandr Maksymets, Sergio Arnaud, Karmesh Yadav, et al. OpenEQA: Embodied question answering in the era of foundation models. In Proceedings of the IEEE/CVF Conference on Computer Vision and Pattern Recognition (CVPR) , pages 1648816498, 2024.
- Kaan Masai, Arjun Guha, Jordan Henkel, and Prem Devanbu. RefactorBench: Evaluating stateful reasoning in language agents through code. In International Conference on Learning Representations (ICLR) , 2025. arXiv:2503.07832.
- Nancy Pennington. Stimulus structures and mental representations in expert comprehension of computer programs. Cognitive Psychology , 19(3):295-341, 1987.
- Jielin Qiu, Zuxin Liu, Zhiwei Liu, Rithesh Murthy, Jianguo Zhang, Haolin Chen, Shiyu Wang, Ming Zhu, Liangwei Yang, Juntao Tan, et al. LoCoBench-Agent: An interactive benchmark for LLM agents in long-context software engineering. arXiv preprint arXiv:2511.13998 , 2025.
- Manolis Savva, Abhishek Kadian, Oleksandr Maksymets, et al. Habitat: A platform for embodied AI research. arXiv preprint arXiv:1904.01201 , 2019.
- Edward C Tolman. Cognitive maps in rats and men. Psychological Review , 55(4):189-208, 1948.
- Anneliese von Mayrhauser and A. Marie Vans. Program comprehension during software maintenance and evolution. IEEE Computer , 28(8):44-55, 1995.
- Pingyue Zhang, Zihan Huang, Yue Wang, Jieyu Zhang, Letian Xue, Zihan Wang, Qineng Wang, Keshigeyan Chandrasegaran, Ruohan Zhang, Yejin Choi, Ranjay Krishna, Jiajun Wu, Li Fei-Fei, and Manling Li. Theory of space: Can foundation models construct spatial beliefs through active exploration? arXiv preprint arXiv:2602.07055 , 2026.
- Xuhui Zhou, Valerie Chen, Zora Zhiruo Wang, Graham Neubig, Maarten Sap, and Xingyao Wang. TOM-SWE: User mental modeling for software engineering agents. arXiv preprint arXiv:2510.21903 , 2025.
- Jared Zhu, Minhao Hu, and Junde Wu. SWE context bench: A benchmark for context learning in coding. arXiv preprint arXiv:2602.08316 , 2026.

## A Evaluation Prompts

System prompt (excerpt). The agent is told: 'You are an expert software engineer exploring a codebase you have never seen before. You are operating under PARTIAL OBSERVABILITY [...] Available actions: LIST, OPEN, SEARCH, INSPECT, DONE. [...] Your goal is to build a complete and accurate ARCHITECTURAL UNDERSTANDING.' The full prompt describes action semantics and exploration strategy tips.

Cognitive map probe. After every K actions, the agent receives: 'Externalize your current architectural belief as JSON matching the following schema [...]' The schema specifies components with status/purpose/exports/edges/confidence, invariants with structured canonical form, and an uncertainty summary. A worked example demonstrates the expected format (see supplementary materials).

## B Generated Codebase Example

Amedium-complexity codebase (seed 42, text processing domain) generates the following structure:

```
text_processor/ +--__init__.py +--middleware/ +--models.py | +-- mod_i.py (logging) +--base.py | +--mod_j.py (retry) +--config.py +--utils/
```

```
+--exceptions.py | +--helpers.py +--registry.py | +--formatters.py +--stages/ +--legacy/ | +-- mod_a.py | +-- old_pipeline.py | +-- mod_b.py | +-- compat.py | +-- mod_c.py +--runner.py | +-- mod_d.py +--cli.py | +-- mod_e.py +--pipeline_config.json | +-- mod_f.py +--test_smoke.py +--adapters/ | +-- mod_g.py | +-- mod_h.py
```

Stages implement a StageBase ABC. The registry loads stage ordering from pipeline\_config.json . Stages cannot import each other directly (enforced by test). Legacy modules are distractors not in the pipeline flow.

## C Budget Sweep

To characterize how exploration budget affects discrimination between strategies, we ran all four baselines at B ∈ { 10 , 15 , 20 , 25 } across 3 codebases. Table 5 reports mean Dependency F1.

Table 5: Dependency F1 vs. exploration budget (mean over 3 codebases).

| Method       |   B =10 |   B =15 |   B =20 |   B =25 |
|--------------|---------|---------|---------|---------|
| Oracle       |   1     |   1     |   1     |   1     |
| Config-Aware |   0.175 |   0.492 |   0.577 |   0.626 |
| Random       |   0.056 |   0.317 |   0.538 |   0.632 |
| BFS-Import   |   0.078 |   0.157 |   0.293 |   0.603 |

At tight budgets ( B =10 ), Config-Aware's strategy of prioritizing registry files yields a 3 × advantage over Random (0.175 vs. 0.056). As budget increases, raw coverage dominates: at B =25 (allowing agents to open nearly all files), Random matches Config-Aware (0.632 vs. 0.626). BFS-Import shows the steepest growth from B =20 to B =25 (0.293 → 0.603), reflecting its systematic but slow import-chain traversal. The convergence at high budgets confirms that the current mediumcomplexity codebases require tighter budgets or larger codebases to strongly discriminate exploration strategy for structural edge types; the meaningful discrimination at B =20 lies in semantic edge discovery by LLM agents.