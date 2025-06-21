## Information Fidelity in Tool-Using LLM Agents: A Martingale Analysis of the Model Context Protocol

Flint Xiaofeng Fan 1,3 , Cheston Tan 1 , Roger Wattenhofer 3 , Yew-Soon Ong 1,2

1 CFAR, IHPC, Agency for Science, Technology and Research, Singapore

2

College of Computing and Data Science, Nanyang Technological University, Singapore 3 Department of Information Technology and Electrical Engineering, ETH Zurich, Switzerland fxf@u.nus.edu, cheston-tan@a-star.edu.sg, wattenhofer@ethz.ch, asysong@ntu.edu.sg

## Abstract

As AI agents powered by large language models (LLMs) increasingly use external tools for high-stakes decisions, a critical reliability question arises: how do errors propagate across sequential tool calls? We introduce the first theoretical framework for analyzing error accumulation in Model Context Protocol (MCP) agents, proving that cumulative distortion exhibits linear growth and high-probability deviations bounded by O ( √ T ) . This concentration property ensures predictable system behavior and rules out exponential failure modes. We develop a hybrid distortion metric combining discrete fact matching with continuous semantic similarity, then establish martingale concentration bounds on error propagation through sequential tool interactions. Experiments across Qwen2-7B, Llama-3-8B, and Mistral-7B validate our theoretical predictions, showing empirical distortion tracks the linear trend with deviations consistently within O ( √ T ) envelopes. Key findings include: semantic weighting reduces distortion by 80%, and periodic re-grounding approximately every 9 steps suffices for error control. We translate these concentration guarantees into actionable deployment principles for trustworthy agent systems. The codebase is available at https://github.com/flint-xf-fan/MCP.

## 1 Introduction

Large language models (LLMs) have achieved remarkable capabilities across natural language processing tasks, yet they remain fundamentally constrained by the static snapshots of knowledge encoded in their training corpora (Villalobos et al. 2022; Naveed et al. 2023; Li et al. 2024). Once deployed, an LLM cannot ingest new facts or correct errors without costly retraining, leading to stale or even blatantly incorrect outputs in rapidly changing domains such as realtime news, financial markets, or clinical guidelines (Bommasani et al. 2021; Liang et al. 2022; Fan et al. 2025a). As Silver and Sutton (Silver and Sutton 2025) aptly observe, 'we stand on the threshold of a new era in artificial intelligence' where systems must transcend fixed datasets and learn through dynamic interaction.

To address this fundamental limitation, the Model Context Protocol (MCP) has emerged as one popular tool for

Full working version of an extended abstract accepted at the 25th International Conference on Autonomous Agents and Multiagent Systems (AAMAS 2026).

Figure 1: MCP standardizes LLM-tool integration through a unified JSON-RPC interface (bottom), replacing custom per-tool connections (top) with centralized schema validation, context management, and uncertainty propagation.

<!-- image -->

connecting LLMs to external tools and data sources (Anthropic 2024). MCP transforms what would traditionally be an M × N integration problem (with M different AI applications needing custom connections to N different tools) into a more manageable M + N approach through a unified JSON-RPC framework, enabling seamless composition of models and services, as illustrated in Figure 1. Major platforms including Anthropic and Microsoft have implemented MCP-compatible interfaces (Anthropic 2024; Microsoft Azure AI Team 2025), facilitating the development of systems that can retrieve up-to-date information, perform precise calculations, and interact with real-world services. Through MCP, LLMs can continuously integrate fresh external evidence to stay epistemically grounded and operationally up-to-date long after their initial training, instantiating Silver and Sutton's concept of experiential learning (Silver and Sutton 2025).

However, this enhanced agility puts information fidelity at risk: each call to an external tool introduces opportunities for factual error, semantic drift, and compounding inaccuracies (Qin et al. 2025). In safety-critical applications, ranging from clinical decision support to financial analysis, even a minor mistake in an early query can cascade into catastrophic downstream consequences due to the sequential nature of decision-making processes (Fan et al. 2021, 2025b). For instance, in 2012, Knight Capital lost $440 million due to a software error in their trading algorithm, highlighting how small mistakes can lead to significant finan- cial consequences (Dolfing 2019). Similarly, studies have shown that AI systems can provide inaccurate medical drug dosages, potentially leading to dangerous treatment recommendations (Ramasubramanian et al. 2024).

As tool usage becomes increasingly central to deployed AI systems, ensuring information fidelity across sequential interactions emerges as a critical challenge. Despite MCP's widespread adoption in production systems, we lack a formal framework for understanding how errors accumulate across adaptive queries, or what guarantees can be provided regarding the integrity of multi-step reasoning chains. While empirical work has demonstrated that function-calling improves factuality (Schick et al. 2023; Yao et al. 2023), rigorous theoretical bounds on error propagation remain elusive.

In this paper, we bridge this gap by providing, to our knowledge, the first work to derive general high-probability deviation bounds for cumulative hybrid semantic distortion in MCP-style, tool-augmented LLM pipelines under explicit dependence assumptions. A martingale represents a mathematical 'fair game' where future expectations equal the current state-intuitively preventing errors from spiraling uncontrollably across sequential interactions. By adapting concentration techniques from adaptive data analysis (Howard et al. 2021; Dwork et al. 2015) and applying them to sequential tool interactions, we establish formal connections between everyday engineering practice and statistical learning theory. Our analysis models MCP exchanges as a boundeddifference martingale with finite dependency horizons, enabling us to derive high-probability bounds on cumulative semantic distortion. Our main contributions are:

- Information Fidelity Framework (Section 3). We develop a comprehensive theoretical framework for analyzing MCP interactions, including a novel semantic distortion metric that combines weighted discrete fact matching with continuous embedding-based similarity, capturing both strict factual correctness and nuanced semantic drift.
- Concentration Bounds (Section 4). Within our framework, we derive high-probability concentration bounds showing cumulative semantic distortion deviations are bounded by O ( √ T ln(1 /η )) , with experiments confirming consistent linear growth at constant per-step rates across sequential MCP interactions.
- Empirical Validation (Section 5). Through experiments with Qwen2-7B-Instruct, Llama-3-8B-Instruct, and Mistral-7B-Instruct-v0.3, we demonstrate that empirical distortion exhibits the predicted linear growth with O ( √ T ) concentration bounds across varying dependency strengths.
- Practical Insights (Section 6). We derive actionable design principles from theoretical results, including optimal re-grounding intervals and distortion monitoring strategies, enabling safe deployment of MCP agents in safetycritical domains where unchecked error accumulation risks catastrophic failures.

## 2 Preliminaries

Tool-Augmented Language Models A rich line of work teaches LLMs to invoke external tools, e.g. Toolformer (Schick et al. 2023) and ToolLLM (Qin et al. 2023). ReAct (Yao et al. 2023) interleaves reasoning and tool calls. While these methods boost performance, none provide formal guarantees on how errors accumulate across multiple invocations.

Retrieval-Augmented Generation RAG systems (Lewis et al. 2020) empower LLMs with external corpus lookups. Recent studies analyzing retrieval errors include RARR (Gao et al. 2022) and Faithful reasoning (Creswell and Shanahan 2022). Despite advances in index refinement (Ram et al. 2023) and end-to-end training (Borgeaud et al. 2022; Guu et al. 2020), the theoretical reliability of repeated retrieval-generation loops remains open.

Model Context Protocol General RPC/API standards, such as JSON-RPC (JSON-RPC Working Group 2013) and OpenAPI (OpenAPI Initiative 2021), and the Language Server Protocol (Microsoft 2022) were not designed for LLM workflows. MCP (Anthropic 2024) fills this gap, prescribing schema validation, context continuity, and uncertainty handling. Its emergence is evidenced by platforms like Anthropic (Anthropic 2024) and Microsoft Azure (Microsoft Azure AI Team 2025). Figure 1 illustrates the typical flow: an LLM issues a JSON-RPC request validated by an MCP proxy, which routes it to appropriate tools and returns structured responses. A primer on MCP is provided in Appendix A.

Martingale-Based Analyses Concentration bounds such as the Azuma-Hoeffding (Azuma 1967; Hoeffding 1963) and Freedman's refinement (Freedman 1975) underpin online learning and adaptive control (Dwork et al. 2015). A martingale is a stochastic process { X t } T t =0 where E [ X t +1 | X 0 , X 1 , . . . , X t ] = X t , formalizing the concept of a 'fair game.' Modern self-normalized variants (Howard et al. 2021) tighten these results, but to our knowledge no work has used them to quantify distortion in LLM-tool pipelines. The Azuma-Hoeffding inequality states that for a martingale with bounded differences | X t +1 -X t | ≤ c t :

<!-- formula-not-decoded -->

To our knowledge, no prior work unifies these strands into a principled, end-to-end theory of error accumulation in MCP-based LLM systems. In the next section, we develop our Information Fidelity Framework, which formalizes MCP interactions as an adaptive stochastic process and establishes the theoretical foundation for analyzing semantic distortion.

## 3 Information Fidelity Framework

To address the gap in prior work, we propose an Information Fidelity Framework that models MCP interactions as an adaptive stochastic process, quantifying error accumulation through a novel distortion metric and martingale analysis. Our framework formalizes how information propagates and potentially degrades through sequential MCP interactions, providing theoretical foundations for bounding semantic distortion across tool calls. We begin by establishing basic definitions and our semantic distortion metric, followed by key modeling assumptions, and finally derive the properties that enable our concentration results.

Definition 1 (Information Filtration) . The sequence of pairs of query-response { ( Q t , R t ) } is modeled via the natural filtration F t = σ ( Q 1 , R 1 , . . . , Q t , R t ) , representing all information available after t interactions.

Definition 2 (Influence Function) . The parameter β ∈ (0 , 1) controls exponential decay of past influences through the influence function ϕ ( i, j ) = β j -i for 1 ≤ i &lt; j ≤ T .

Definition 3 (Ideal Fact Set) . For each prompt-response pair ( Q t , R t ) , let I t be the set of ground-truth facts (e.g. triples or attribute-value pairs) that R t is expected to include.

## 3.1 Quantifying Semantic Distortion

Measuring information fidelity requires balancing strict factual correctness against broader semantic similarity. We introduce a hybrid metric that captures both dimensions:

<!-- formula-not-decoded -->

where:

<!-- formula-not-decoded -->

<!-- formula-not-decoded -->

Here, extract ( R t ) is a function that extracts the set of facts from response R t , w ( f ) assigns importance weights to individual facts, and embed( · ) maps text to a semantic vector space. We define embed( I t ) = 1 |I t | ∑ f ∈I t embed( f ) , or for weighted facts, the weighted average of their embeddings.

The first component, d w set , measures factual completeness through weighted fact matching, penalizing both missing and incorrect facts. The second component, d emb , captures semantic similarity in embedding space, addressing nuanced meaning beyond exact fact matching. The parameter λ ∈ [0 , 1] allows tuning the trade-off between factual precision and semantic similarity. The choice of λ depends on the application; for example, λ = 0 . 5 balances factual and semantic priorities equally, while lower values prioritize factual accuracy and higher values emphasize semantic coherence.

To illustrate how this metric operates in practice, consider a simple example:

- Query: 'What is the capital of France?'
- Response A: 'Paris is the capital of France.'
- Response B: 'The capital of France is Paris.'

Both responses contain the same fact (Paris is France's capital), so d w set = 0 for both. However, d emb may differ slightly due to wording differences. The hybrid metric d sem captures this nuance while still recognizing factual equivalence. This example demonstrates how our metric balances factual correctness with semantic nuance, providing a more comprehensive measure of information fidelity than either component alone.

Definition 4 (Cumulative Distortion) . Let T be the total number of tool calls in an MCP interaction. The cumulative distortion is defined as:

<!-- formula-not-decoded -->

where ∆ t is the step-wise semantic distortion at step t as defined in Equation (1) .

## 3.2 Modeling Assumptions

Our Information Fidelity Framework requires standard technical assumptions on normalization and continuity (see Appendix B.1), ensuring our distortion metric is well-defined and bounded in [0,1]. We now state the substantive modeling assumptions.

Definition 5 (Effective Branching Factor) . The effective branching factor B ≥ 1 denotes the maximum number of distinct future queries that can be directly influenced by a single response within the interaction graph. Sequential chains satisfy B = 1 , while tree-structured interactions have B equal to the maximum number of children per node.

Assumption 1 (Bounded Branching) . Let B be the maximum effective branching factor. Then βB &lt; 1 .

Here, the branching factor B represents the maximum number of future queries potentially influenced by a single response. This condition is sufficient to establish our theoretical guarantees, ensuring influence decays across steps. Notably, this is only a sufficient but not necessary condition for establishing the concentration bound.

Assumption 2 (Response Stability) . There exists α ≤ 1 such that small input perturbations induce only continuous changes in the LLM's response distribution, governed by an influence function ϕ ( i, j ) .

Modern LLMs exhibit a degree of robustness to minor input variations, and this assumption captures that property. Without response stability, a single character change could dramatically alter all subsequent reasoning, making error bounds impossible.

Assumption 3 (Temporal Decay Structure) . For any t &lt; T and any two execution paths that coincide through time t but differ at step t +1 , there exist constants α ∈ [0 , 1] , β ∈ [0 , 1) , and branching factor B ≥ 1 such that distortions at future steps can be coupled to satisfy

<!-- formula-not-decoded -->

almost surely under appropriate coupling. Additionally, | ∆ t +1 -∆ ′ t +1 | ≤ 1 by Lemma 3. If periodic re-grounding occurs every m steps, then ∆ t + k = ∆ ′ t + k for all k &gt; m .

Table 1: Embedding distances for responses with identical facts but different wording

| Response Pair                                                            |   Fact Match |   Embedding Distance |
|--------------------------------------------------------------------------|--------------|----------------------|
| 'Apple's revenue was $94.8B' vs. 'Apple reported $94.8B in revenue'      |            1 |                 0.17 |
| 'The GDP grew by 2.5% in Q2' vs. 'Second quarter GDP expansion was 2.5%' |            1 |                 0.21 |
| 'The meeting is on Monday at 3PM' vs. 'Monday at 3PM is when we'll meet' |            1 |                 0.35 |

Figure 2: Dependency graph for MCP interactions. Solid arrows indicate direct influence ϕ ( i, i +1) = β , and the dashed arrow shows long-range decay ϕ ( i, j ) = β j -i . This exponential decay structure enables us to prove that distortion deviations remain sublinear, even with adaptive queries.

<!-- image -->

Remark 1 (Modeling framework justification) . This temporal decay model captures three key properties of LLMtool systems: (i) exponential decay reflecting finite effective memory (Press, Smith, and Lewis 2022), (ii) bounded propagation preventing error amplification, and (iii) reset through periodic re-grounding-standard practice in production deployments. The framework connects to established coupling techniques from Markov chain mixing (Levin and Peres 2017) and adaptive data analysis (Dwork et al. 2015), providing sufficient conditions for concentration bounds. Section 5 validates the decay rate β through autocorrelation diagnostics.

## 3.3 Modeling as an Adaptive Stochastic Process

The inherently sequential nature of tool-augmented reasoning requires careful modeling of dependencies between successive interactions. We formalize this as a sequence of query-response pairs { ( Q t , R t ) } T t =1 , where each new query can depend on all previous interactions:

<!-- formula-not-decoded -->

This adaptive formulation is essential for capturing realistic LLM behavior, where each tool call builds on previous responses.

Figure 2 illustrates this dependency structure. A key insight is that influence follows an exponential decay pattern described by Definition 2. This pattern aligns with how information naturally attenuates in sequential reasoning: recent statements have stronger impact than distant ones, and verification steps effectively reset error chains by grounding the model in authoritative data.

## 3.4 Properties of Semantic Distortion

Our semantic distortion metric exhibits crucial properties that enable concentration analysis. First, it distinguishes lexical variation from semantic equivalence:

Claim 1 (Semantic Sensitivity) . If two responses contain identical facts but differ lexically, then

̸

<!-- formula-not-decoded -->

ensuring d sem captures semantic nuance.

Proof. If R t and R ′ t have identical fact coverage, then

<!-- formula-not-decoded -->

Therefore, d w set ( R t , I t ) = d w set ( R ′ t , I t ) .

̸

̸

However, different wording produces different embeddings: embed ( R t ) = embed ( R ′ t ) , causing d emb ( R t , I t ) = d emb ( R ′ t , I t ) . For λ &gt; 0 , this ensures d sem ( R t , I t ) = d sem ( R ′ t , I t ) . Table 1 shows empirical evidence: embedding distances vary 0.15-0.35 in cosine space even when factual content is identical.

̸

Second, the metric varies continuously with response perturbations, ensuring small edits induce proportionally small distortion changes (formalized in Lemma 2, Appendix B.2).

## 3.5 Martingale Construction for Concentration

To bound the cumulative distortion D ( T ) = ∑ T t =1 ∆ t , we construct a martingale sequence whose increments we can control with classical concentration inequalities.

Definition 6 (Distortion Martingale) . Define

<!-- formula-not-decoded -->

This forms a martingale because each Z t uses all available information up to time t to predict the final distortion D ( T ) . The martingale property ensures that future distortions are conditionally unbiased relative to our current estimate-mathematically, E [ Z t +1 |F t ] = Z t . This property is crucial as it allows us to apply concentration inequalities to bound how far the actual distortion can deviate from its expectation.

Intuitively, Z t represents our best prediction of the final cumulative distortion given all information available after t interactions; hence observing the next response cannot systematically bias that prediction. As t increases, Z t incorporates more information and converges to the actual distortion D ( T ) when all interactions are observed.

In the next section, we show that the increments Z t +1 -Z t satisfy a bounded-difference property, which we combine with Azuma's inequality to derive high-probability concentration bounds on how much the actual distortion D ( T ) can deviate from its linearly growing expected value, ensuring predictable system behavior despite adaptive querying.

## 4 Concentration for Adaptive Querying

Having established our information fidelity framework, we now address the central theoretical question: how does distortion accumulate across sequential tool calls? While Definition 4 specifies that D ( T ) = ∑ T t =1 ∆ t , each ∆ t can depend on all previous interactions through the adaptive process formalized in Section 3.3. This adaptive querying could, in principle, allow errors to compound arbitrarily. Our main result shows otherwise: under the temporal decay structure of Assumption 3, cumulative distortion remains tightly concentrated around its expectation (which grows at most linearly, with empirically constant per-step rates), with deviations bounded by O ( √ T ) despite full adaptivity.

Our main insight is that despite adaptive querying, where each new prompt can depend arbitrarily on all previous interactions, the cumulative distortion D ( T ) = ∑ T t =1 ∆ t remains tightly concentrated around its expectation. This concentration emerges from the exponential decay of influence across steps, creating a form of 'effective independence' that enables powerful probabilistic guarantees.

## 4.1 The Bounded-Difference Property

The following lemma establishes how much a single new response can affect our expectation of the total distortion under the temporal decay structure. The constant C ∗ = α 1 -βB quantifies the maximum possible cumulative influence that a single response can have on all future queries, with α capturing response stability (Assumption 2) and B accounting for the branching factor (Assumption 1). This uniform bound allows us to derive clean concentration results regardless of a query's position in the sequence.

Lemma1 (Bounded Doob increments) . Let Z t = E [ D ( T ) | F t ] be the distortion Doob martingale (Definition 6). Under Assumptions 1 and 3, we have, almost surely,

<!-- formula-not-decoded -->

with

<!-- formula-not-decoded -->

If periodic re-grounding enforces a finite horizon m , then

<!-- formula-not-decoded -->

Proof Sketch. The martingale increment decomposes as Z t +1 -Z t = (∆ t +1 -E [∆ t +1 |F t ])+ ∑ T j = t +2 ( E [∆ j |F t +1 ] -E [∆ j |F t ]) . The first term is bounded by 1 via Lemma 3. For the tail sum, the pathwise coupling from Assumption 3 ensures | ∆ j -∆ ′ j | ≤ α ( βB ) j -t -2 almost surely for coupled executions, implying each conditional expectation difference is bounded by the same factor. Summing the geometric series ∑ T j = t +2 α ( βB ) j -t -2 ≤ α/ (1 -βB ) = C ∗ completes the bound. Full details in Appendix B.3.

This bounded-difference property is the linchpin enabling concentration analysis: it ensures that even though each new response can influence all future queries through the adaptive chain, that influence diminishes rapidly enough to maintain exponential concentration around the expected cumulative distortion.

## 4.2 High-Probability Concentration Results

Theorem 1 (High-probability distortion bound) . Let C ∗ = α 1 -βB and γ ∗ = 2 C ∗ +( C ∗ ) 2 . For any η ∈ (0 , 1) ,

<!-- formula-not-decoded -->

Proof Sketch. By Lemma 1, the distortion martingale has bounded increments | Z t +1 -Z t | ≤ c ⋆ = 1 + C ∗ . Summing c 2 ⋆ = (1 + C ∗ ) 2 = 1 + 2 C ∗ + ( C ∗ ) 2 over T steps yields T (1 + γ ∗ ) . Azuma's inequality then gives the stated high-probability bound. The complete proof appears in Appendix B.4.

The correction factor γ ∗ quantifies how dependencies in our adaptive process inflate the variance term compared to the independent case. With C ∗ = α 1 -βB representing the maximum cumulative influence of any response on all future queries, γ ∗ = 2 C ∗ +( C ∗ ) 2 captures both the linear interaction between bounded differences and the quadratic effect of compounded influences. Together, they show that even with adaptive queries, cumulative distortion grows at most linearly in expectation (bounded by T since ∆ t ∈ [0 , 1] ), with empirically observed constant per-step rates and sublinearly bounded deviations. This concentration property rules out both exponential error blowup and chaotic variance growth. The practical implications of this result are captured in the following corollaries:

Corollary 1 (Sub-linear deviation) . With probability at least 1 -η , D ( T ) = E D ( T ) + O (√ T ln(1 /η ) ) .

This result directly addresses fears about chaotic error compounding in LLM reasoning chains. Rather than exhibiting exponential divergence, cumulative distortion remains tightly concentrated around a predictable linear trajectory. The O ( √ T ) deviation bound guarantees that uncertainty grows sublinearly: deviations from expected distortion scale as √ T , not T . For T = 10 steps, the high-probability deviation is bounded by √ 10 ≈ 3 . 2 times the single-step deviation constant, ensuring predictable system behavior.

Corollary 2 (Effective information horizon) . The querylevel influence function ϕ ( i, j ) = β j -i (Definition 2) decays exponentially, with influence dropping below threshold ε after H ε = ⌈ log( ε ) / log( β ) ⌉ steps. For β close to 1 , this simplifies to approximately ⌈-ln( ε ) / (1 -β ) ⌉ . The cumulative propagation of a single error across all future queries is bounded by C ∗ = α/ (1 -βB ) , which quantifies the maximum total influence (amplitude). Together, H (range) and C ∗ (magnitude) determine how far and how strongly errors can propagate.

This corollary introduces the concept of an 'effective information horizon': the maximum distance over which errors meaningfully propagate through the query chain. The horizon H is set by the decay rate β of ϕ ( i, j ) (range), while the amplitude of propagated errors is scaled by C ∗ , which incorporates both response stability ( α ) and branching ( B ) (magnitude). For typical values (e.g., β = 0 . 7 ), this horizon is approximately 9 steps, supporting periodic re-grounding in long reasoning chains. Complete proofs of above results, along with refinements using tighter concentration inequalities, appear in Appendix B.4, B.5 and B.6, respectively.

## 5 Experiments

Having established theoretical guarantees for information fidelity, we validate two central predictions: (1) Does cumulative distortion exhibit linear expected growth with sublinear concentration as claimed by Corollary 1? (2) How do framework parameters ( β , λ ) affect empirical distortion? We address these through systematic experiments across Qwen2-7B-Instruct (Qwen) (Yang et al. 2024) and Llama3-8B-Instruct (Llama) (AI@Meta 2024), with Mistral-7BInstruct-v0.3 (Mistral) (Jiang et al. 2023) in Appendix D.

MCP Implementation. All experiments use identical MCP tools: a knowledge retrieval tool operating over a deterministic cached corpus covering eight domains (history, science, technology, arts, sports, geography, literature, mathematics), and a financial data tool providing schemavalidated access to market snapshots. Both enforce strict schema validation and maintain context continuity, faithfully implementing MCP specifications. To isolate error accumulation dynamics from tool variability, we employ deterministic, cached responses. This controlled setting establishes fundamental bounds that serve as a baseline for understanding systems with stochastic tools.

Query Generation and Distortion Measurement. Each chain begins with a seed question from domain-specific templates. Subsequent queries are generated adaptively with probability β , implementing the influence function ϕ ( i, j ) = β j -i from Definition 2. Our experimental chains maintain linear structure with branching factor B = 1 , satisfying Assumption 1's condition βB &lt; 1 . For each response R t , we extract ideal fact set I t from ground truth and compute hybrid semantic distortion via Eq. (1). Cumulative distortion D ( T ) = ∑ T t =1 ∆ t is tracked across each chain, with statistics aggregated over multiple independent trials.

Theoretical Bounds and Model Validation. We construct calibrated envelopes by: (1) estimating expected perstep distortion ˆ r from mean first-step distortion, (2) extrapolating linearly to ˆ E [ D ( T )] = T · ˆ r , and (3) adding theoretical deviation √ 2(1 + ˆ γ ) T ln(1 /δ ) where ˆ γ is computed from empirical dependency strength (Appendix C.5). These bounds instantiate Theorem 1 with 95% confidence ( δ = 0 . 05 ). To validate Assumption 3, we compute empirical autocorrelations ˆ ρ ( k ) = Corr(∆ t , ∆ t + k ) and fit decay rate ˆ β = arg min β ∑ k (ˆ ρ ( k ) -β k ) 2 , yielding ˆ β ∈ [0 . 68 , 0 . 71] across architectures. Full details appear in Appendix C.

Baseline Validation. Under standard conditions ( β = 0 . 7 , λ = 0 . 5 , T = 10 , 50 chains per model), Figure 3 reveals striking consistency between theory and practice. Qwen27B achieves D (10) = 5 . 26 ± 0 . 34 versus calibrated envelope 9.15 (safety margin 1 . 74 × ); Llama-3-8B records D (10) = 4 . 92 ± 0 . 46 with envelope 8.90 (margin 1 . 81 × ). The consistent per-step rate of ≈ 0 . 5 across all chains confirms linear expected growth, while safety margins &gt; 1 . 7 × validate our O ( √ T ) concentration bounds from Corollary 1.

Ten-step baseline (T=10)

Figure 3: Baseline distortion accumulation over 10 tool calls. Cumulative distortion with β = 0 . 7 , λ = 0 . 5 (50 chains/model). Solid: empirical mean ± 1 σ ; dotted: highprobability envelopes (Theorem 1, 95% confidence). Distortion grows linearly at ≈ 0 . 5 per step with deviations tightly concentrated around the linear trend, validating O ( √ T ) concentration bounds.

<!-- image -->

Figure 4: Lambda sweep: semantic weighting effect. Distortion for λ ∈ { 0 , 0 . 25 , 0 . 5 , 0 . 75 , 1 . 0 } at T = 30 , β = 0 . 7 (8 chains/config). Solid: empirical mean ± 1 σ ; dotted: calibrated bounds.

<!-- image -->

Extended Validation. Experiments across T = 60 steps and extreme dependencies ( β ∈ { 0 . 5 , 0 . 7 , 0 . 9 , 0 . 95 , 0 . 98 } ) demonstrate predictable linear accumulation with O ( √ T ) concentration bounds maintaining consistent safety margins ( 1 . 10 × -1 . 55 × ). Even at β = 0 . 98 approaching the critical point, the system avoids exponential failure modes, showing that high dependencies inflate the variance bounds rather than the mean distortion rate (Figures 5, 6).

Summary. Our experimental evaluation demonstrates three key results: (1) Linear growth with sublinear concentration holds robustly -cumulative distortion grows linearly at constant per-step rates ( 0 . 50 -0 . 55 ) across chain lengths T ∈ [10 , 60] and dependency strengths β ∈ [0 . 5 , 0 . 98] , with empirical trajectories consistently within O ( √ T ) theoretical envelopes. (2) Framework parameters offer actionable design levers -increasing semantic weight λ from 0 to 1 reduces distortion by 80%, while stronger dependencies ( β ) increase per-step rates modestly ( ∼ 4% from 0.7 to 0.9). (3) Architectural robustness validates theoretical generality -three LLM architectures (Qwen2-7B, Llama-3-8B, Mistral-7B) exhibit nearly identical distortion patterns, confirming that concentration bounds depend on dependency structure ( β , B ) and metric properties ( λ ), not internal model mechanisms.

## 6 Discussion and Implications

Our Information Fidelity Framework shows that tool-using LLM agents can achieve provable reliability-cumulative distortion grows at most linearly (bounded by T, with em-

Long-chain scaling (T=60)

Figure 5: Extended chain scaling. Distortion for β ∈ { 0 . 5 , 0 . 7 , 0 . 9 } at T = 60 , λ = 0 . 5 (6 chains/config). Solid: mean ± 1 σ ; dotted: calibrated bounds.

<!-- image -->

Figure 6: High-dependency stress test. Distortion for β ∈ { 0 . 95 , 0 . 98 } at T = 30 , λ = 0 . 5 (6 chains/config). Solid: mean ± 1 σ ; dotted: calibrated bounds. At β = 0 . 98 approaching the critical point βB = 1 , empirical curves stay well below theoretical envelopes.

<!-- image -->

pirically constant per-step rates) with sublinear concentration bounds on deviations, ruling out exponential error blowup-through deliberate architectural choices:

Architecting for Reliability. To realize the bounds in practice, agents should be engineered so that older context matters less than recent context (e.g., via sliding or recency-biased windows), tool outputs are predictable (schema validation, caching, and bounded stochasticity), and dependencies are reset by re-grounding periodically on authoritative sources. Schedule re-grounding every H ε = ⌈ log( ε ) / log( β ) ⌉ steps for chosen threshold ε . Limit fan-out via gating or serialization to keep branching factor B small. These controls ensure βB &lt; 1 , enabling our concentration bounds.

Tuning and Monitoring in Production. Tune λ balancing factual precision ( λ ∈ [0 . 3 , 0 . 5] for safety-critical) versus semantic coherence ( λ ∈ [0 . 6 , 0 . 8] for conversational). Distortion decreases sharply with λ up to 0.75, with diminishing returns beyond (Fig. 4). For typical values ( β = 0 . 7 , ε = 0 . 05 ), re-ground every ∼ 9 steps. Fit ˆ β from autocorrelations to adapt verification cadence dynamically.

Limitations and Extensions. Our theoretical framework analyzes single-agent, sequential tool interactions with deterministic tools that enable rigorous concentration bounds. Natural extensions include analyzing multi-agent error propagation, relaxing assumptions, incorporating stochastic tool responses, and adapting to extreme dependency regimes. The metric emphasizes fact recall over precision; alternative weighting schemes could prioritize different error modes. Adaptive policies and online distortion estimation represent promising directions for future work.

## 7 Conclusion

As LLM agents mediate high-stakes decisions, formal reliability guarantees transition from curiosities to necessities. Our Information Fidelity Framework establishes that catastrophic error accumulation is avoidable: bounded contexts, stable tools, and periodic verification provably limit distortion to linear growth O ( T ) with deviations concentrated at O ( √ T ) , preventing exponential error blowup while maintaining predictable system behavior. The theory prescribes, not merely describes-providing practitioners with actionable design principles grounded in rigorous concentration bounds.

## References

AI@Meta. 2024. Llama 3 Model Card.

Anthropic. 2024. Introducing the Model Context Protocol. https://www.anthropic.com/news/model-contextprotocol. Published Nov 25, 2024.

Azuma, K. 1967. Weighted sums of certain dependent random variables. Tohoku Mathematical Journal , 19(3): 357367.

Bommasani, R.; Hudson, D. A.; Adeli, E.; Altman, R.; Arora, S.; von Arx, S.; Bernstein, M. S.; Bohg, J.; Bosselut, A.; Brunskill, E.; et al. 2021. On the opportunities and risks of foundation models. arXiv preprint arXiv:2108.07258 .

Borgeaud, S.; Mensch, A.; Hoffmann, J.; Cai, T.; Rutherford, E.; Millican, K.; Van Den Driessche, G. B.; Lespiau, J.-B.; Damoc, B.; Clark, A.; et al. 2022. Improving language models by retrieving from trillions of tokens. In Proceedings of the 39th International Conference on Machine Learning , 2206-2240.

Creswell, A.; and Shanahan, M. 2022. Faithful reasoning using large language models. arXiv preprint arXiv:2208.14271 .

Dolfing, H. 2019. Case Study 4: The $440 Million Software Error at Knight Capital. https://www.henricodolfing.com/ 2019/06/project-failure-case-study-knight-capital.html.

Dwork, C.; Feldman, V.; Hardt, M.; Pitassi, T.; Reingold, O.; and Roth, A. 2015. Preserving statistical validity in adaptive data analysis. In Proceedings of the 47th ACM Symposium on Theory of Computing , 117-126.

Fan, F. X.; Tan, C.; Ong, Y.-S.; Wattenhofer, R.; and Ooi, W.-T. 2025a. FedRLHF: A Convergence-Guaranteed Federated Framework for Privacy-Preserving and Personalized RLHF. In Proceedings of the 24th International Conference on Autonomous Agents and Multiagent Systems , AAMAS'25, 713-721. Richland, SC: International Foundation for Autonomous Agents and Multiagent Systems. ISBN 9798400714269.

Fan, F. X.; Tan, C.; Wattenhofer, R.; and Ong, Y.-S. 2025b. Position Paper: Rethinking Privacy in RL for Sequential Decision-making in the Age of LLMs. arXiv preprint arXiv:2504.11511 .

Fan, X.; Ma, Y.; Dai, Z.; Jing, W.; Tan, C.; and Low, B. K. H. 2021. Fault-tolerant federated reinforcement learning with theoretical guarantee. Advances in neural information processing systems , 34: 1007-1021.

Freedman, D. A. 1975. On tail probabilities for martingales. The Annals of Probability , 100-118.

Gao, L.; Dai, Z.; Pasupat, P.; Chen, A.; Chanthaworn, A. T.; Qu, L.; et al. 2022. Rarr: Researching and revising what language models say, using language models. arXiv preprint arXiv:2210.08726 .

Gao, T.; Yao, X.; and Chen, D. 2021. Simcse: Simple contrastive learning of sentence embeddings. arXiv preprint arXiv:2104.08821 .

Guu, K.; Lee, K.; Tung, Z.; Pasupat, P.; and Chang, M. 2020. Retrieval augmented language model pre-training. In International conference on machine learning , 3929-3938. PMLR.

Hoeffding, W. 1963. Probability inequalities for sums of bounded random variables. Journal of the American Statistical Association , 58(301): 13-30.

Howard, S. R.; Ramdas, A.; McAuliffe, J.; and Sekhon, J. 2021. Time-uniform, nonparametric, nonasymptotic confidence sequences. The Annals of Statistics , 49(2): 10551080.

Jiang, A.; Sablayrolles, A.; Mensch, A.; Bamford, C.; Chaplot, D.; de Las Casas, D.; Bressand, F.; Lengyel, G.; Lample, G.; Saulnier, L.; et al. 2023. Mistral 7B. arXiv preprint arXiv:2310.06825 .

JSON-RPC Working Group. 2013. JSON-RPC 2.0 Specification. https://www.jsonrpc.org/specification.

Levin, D. A.; and Peres, Y. 2017. Markov chains and mixing times , volume 107. American Mathematical Soc.

Lewis, P.; Perez, E.; Piktus, A.; Petroni, F.; Karpukhin, V.; Goyal, N.; K¨ uttler, H.; Lewis, M.; Yih, W.-t.; Rockt¨ aschel, T.; et al. 2020. Retrieval-augmented generation for knowledge-intensive NLP tasks. In Advances in Neural Information Processing Systems , volume 33, 9459-9474.

Li, M.; Zhao, Y.; Deng, Y.; Zhang, W.; Li, S.; Xie, W.; Ng, S.-K.; and Chua, T.-S. 2024. Knowledge Boundary of Large Language Models: A Survey. arXiv preprint arXiv:2412.12472 .

Liang, P.; Bommasani, R.; Lee, T.; Tsipras, D.; Soylu, D.; Yasunaga, M.; Zhang, Y.; Narayanan, D.; Wu, Y.; Kumar, A.; et al. 2022. Holistic evaluation of language models. arXiv preprint arXiv:2211.09110 .

Microsoft. 2022. Language Server Protocol Specification. https://microsoft.github.io/language-server-protocol/ specifications/specification-current/.

Microsoft Azure AI Team. 2025. Model Context Protocol (MCP): Integrating Azure OpenAI for Enhanced Tool Integration and Prompting. https://techcommunity.microsoft.com/blog/azure-aiservices-blog/model-context-protocol-mcp-integrating- azure-openai-for-enhanced-tool-integratio/4393788.

Naveed, H.; Khan, A. U.; Qiu, S.; Saqib, M.; Anwar, S.; Usman, M.; Akhtar, N.; Barnes, N.; and Mian, A. 2023. A comprehensive overview of large language models. arXiv preprint arXiv:2307.06435 .

OpenAPI Initiative. 2021. OpenAPI Specification v3.1.0. https://spec.openapis.org/oas/v3.1.0.

Opitz, J.; and Frank, A. 2022. SBERT studies meaning representations: Decomposing sentence embeddings into explainable semantic features. arXiv preprint arXiv:2206.07023 .

Press, O.; Smith, N.; and Lewis, M. 2022. Train Short, Test Long: Attention with Linear Biases Enables Input Length Extrapolation. In ICLR .

Qin, Y.; Li, S.; Nian, Y.; Yu, X. V.; Zhao, Y.; and Ma, X. 2025. Don't Let It Hallucinate: Premise Verification via Retrieval-Augmented Logical Reasoning. arXiv preprint arXiv:2504.06438 .

Qin, Y.; Liang, S.; Ye, Y.; Zhu, K.; Yan, L.; Lu, Y.; Lin, Y.; Cong, X.; Sun, X.; Tang, B.; et al. 2023. Toolllm: Facilitating large language models to master 16000+ real-world apis. arXiv preprint arXiv:2307.16789 .

Ram, O.; Levine, Y.; Dalmedigos, I.; M¨ uhlgay, D.; Shashua, A.; Leyton-Brown, K.; and Shoham, Y. 2023. In-context retrieval-augmented language models. arXiv preprint arXiv:2302.00083 .

Ramasubramanian, S.; Balaji, S.; Kannan, T.; Jeyaraman, N.; Sharma, S.; Migliorini, F.; Balasubramaniam, S.; and Jeyaraman, M. 2024. Comparative evaluation of artificial intelligence systems' accuracy in providing medical drug dosages: A methodological study. World journal of methodology , 14(4): 92802.

Schick, T.; Dwivedi-Yu, J.; Dess` ı, R.; Raileanu, R.; Lomeli, M.; Zettlemoyer, L.; Cancedda, N.; and Scialom, T. 2023. Toolformer: Language Models Can Teach Themselves to Use Tools. In Advances in Neural Information Processing Systems .

Silver, D.; and Sutton, R. S. 2025. Welcome to the Era of Experience. Preprint of a chapter to appear in Designing an Intelligence, MIT Press.

Villalobos, P.; Ho, A.; Sevilla, J.; Besiroglu, T.; Heim, L.; and Hobbhahn, M. 2022. Will we run out of data? Limits of LLMscaling based on human-generated data. arXiv preprint arXiv:2211.04325 .

Yang, A.; Yang, B.; Hui, B.; Zheng, B.; Yu, B.; Zhou, C.; Li, C.; Li, C.; Liu, D.; Huang, F.; et al. 2024. Qwen2 technical report, 2024. URL https://arxiv. org/abs/2407.10671 , 7: 8.

Yao, S.; Zhao, J.; Yu, D.; Du, N.; Shafran, I.; Narasimhan, K.; and Cao, Y. 2023. ReAct: Synergizing Reasoning and Acting in Language Models. In International Conference on Learning Representations .

## A Model Context Protocol Primer

The Model Context Protocol (MCP) is an open standard introduced by Anthropic in late 2024 that standardizes how AI applications, particularly large language models (LLMs), connect with external data sources and tools. MCP addresses the fundamental limitation of conventional LLMs-their isolated nature and inability to access real-time information without custom integrations for each data source. By providing a universal interface for AI-data interactions, MCP transforms what would traditionally be an M × N integration problem ( M different AI applications connecting to N different tools) into a more manageable M + N approach (Anthropic 2024).

Figure 7: Comparison of ad-hoc versus MCP-based tool integration. (Top) An LLM makes separate, custom API calls to Tool A and Tool B, each requiring bespoke handling and context management. (Bottom) The LLM communicates via a standard JSON-RPC interface to an MCP server, which enforces schema validation, maintains context continuity, propagates uncertainty estimates, and routes calls to multiple tools, returning structured responses back to the model.

<!-- image -->

MCP extends the JSON-RPC 2.0 protocol, a stateless, lightweight remote procedure call framework that uses JSON for data serialization. While JSON-RPC defines general request-response structures, MCP introduces LLM-specific features: schema validation to ensure data consistency, context continuity to maintain state across interactions, and uncertainty handling to quantify response reliability. These enhancements make MCP suitable for dynamic AI workflows.

In an MCP interaction, the LLM sends a JSON-RPC request to an MCP server, which validates the request, routes it to the appropriate tool (e.g., a database or API), and returns a structured response. The request includes standard JSON-RPC fields: jsonrpc (version), method (tool function), params (parameters), and id (response matching). MCP adds optional fields like context for session tracking and uncertainty in responses to indicate confidence.

For example, an MCP request to retrieve a stock price might be:

```
{ ''jsonrpc'': ''2.0'', ''method'': ''get_stock_price'', ''params'': {''symbol'': ''AAPL''}, ''id'': 1, ''context'': {''session_id'': ''abc123''} } The response could be: { ''jsonrpc'': ''2.0'', ''result'': {''price'': 150.25, ''timestamp'': ''2025-05-15T02:02:00+08:00''}, ''uncertainty'': 0.01, ''id'': 1 }
```

Here, uncertainty reflects the confidence in the price data.

MCP's standardized interface reduces integration complexity, ensures consistent context management, and supports reliable tool-augmented LLM systems, making it a cornerstone for modern AI applications.

## B Full Proofs of Theoretical Results

This section provides complete proofs for the theoretical results presented in the main paper.

## B.1 Technical Assumptions and Auxiliary Lemmas

We begin by stating technical assumptions and supporting lemmas omitted from the main text due to space constraints. These conditions are sufficient for our proofs to flow through and represent standard properties satisfied by modern embedding models and fact-extraction systems. While these are sufficient conditions that ensure mathematical rigor, they are not claimed to be necessary-our empirical validation (Section 5) demonstrates that the predicted behavior holds under practical implementations that approximately satisfy these requirements.

Assumption 4 (Weight Normalization) . For each ideal fact set I t , ∑ f ∈I t w ( f ) = 1 .

This standard normalization ensures our distortion metric remains bounded between 0 and 1, making quantitative comparisons meaningful. It can always be satisfied by appropriately scaling the weight function without affecting the analysis (Gao, Yao, and Chen 2021; Opitz and Frank 2022).

Assumption 5 (Embedding Normalization) . All embeddings satisfy ∥ embed( x ) ∥ = 1 .

Most modern embedding models, including SimCSE (Gao, Yao, and Chen 2021) and SBERT (Opitz and Frank 2022), normalize output vectors by default, making this a natural assumption that simplifies semantic similarity calculations.

Assumption 6 (Embedding Regularity) . For any unit vector a and any texts x, y , the embedding function satisfies

<!-- formula-not-decoded -->

This property follows directly from the Cauchy-Schwarz inequality for unit-normalized embeddings and requires no empirical verification. It ensures that semantic similarity varies continuously in embedding space, which is essential for establishing continuity of our distortion metric (Lemma 2).

Assumption 7 (Extraction Stability) . The fact-extraction process is robust to edits below a threshold τ &gt; 0 .

This assumption reflects the discreteness of fact extraction: small edits shouldn't change which facts are recognized.

Lemma 2 (Continuity of Distortion) . Let τ &gt; 0 be a threshold such that fact extraction is unchanged for edits below τ (Assumption 7). Then for any two responses R,R ′ ,

<!-- formula-not-decoded -->

<!-- formula-not-decoded -->

In particular, if the extracted facts are identical (e.g., when the edit distance is &lt; τ ), the change is bounded by λ 2 ∥ embed( R ) -embed( R ′ ) ∥ .

Remark 2. If in a particular deployment one can verify an encoder-specific inequality ∥ embed( x ) -embed( y ) ∥ ≤ L edit · d edit ( x, y ) for some constant L edit , then Lemma 2 implies the bound λL edit 2 · d edit ( R,R ′ ) for the semantic term. We do not assume this globally, as neural embeddings are not generally Lipschitz continuous with respect to edit distance.

Lemma 3 (Bounded Distortion) . For all R t and I t ,

<!-- formula-not-decoded -->

Proof. For the set-based component, due to the weight normalization condition (Assumption 4) and the fact that ˆ I t ∩ I t ⊆ I t , we have 0 ≤ d w set ( R t , I t ) ≤ 1 .

For the embedding component, since -1 ≤ cos( u, v ) ≤ 1 for any unit vectors u, v (Assumption 5), we have

<!-- formula-not-decoded -->

Since d sem is a convex combination of these two bounded components with weight λ ∈ [0 , 1] , we have 0 ≤ d sem ( R t , I t ) ≤ 1 .

## B.2 Proof of Lemma 2 (Continuity of Distortion Metric)

Proof. Let R and R ′ be two responses. We analyze the bound for each component of the distortion metric separately and then combine them.

Set-based component: The discrete fact extractor has a threshold property from Assumption 7: extracted fact sets remain identical for small edits but may change beyond edit distance τ . Therefore:

<!-- formula-not-decoded -->

Embedding component: Both embed( R ) and embed( R ′ ) are unit vectors by Assumption 5. Let a = embed( I t ) , also a unit vector. The embedding distance is:

<!-- formula-not-decoded -->

By Assumption 6 (Embedding Regularity):

<!-- formula-not-decoded -->

Combining components: The hybrid distortion is d sem = (1 -λ ) d w set + λd emb . Therefore:

<!-- formula-not-decoded -->

When extracted facts are identical (e.g., edit distance &lt; τ ), the first term vanishes and only the embedding term remains.

## B.3 Proof of Lemma 1 (Bounded Doob Increments)

Proof. Fix t &lt; T . Consider two coupled execution paths that agree up to time t and may differ at step t + 1 , with future randomness coupled as specified in Assumption 3. Let ∆ t +1 , ∆ t +2 , . . . , ∆ T denote distortions in the first execution and ∆ ′ t +1 , ∆ ′ t +2 , . . . , ∆ ′ T in the second.

The martingale increment decomposes as:

<!-- formula-not-decoded -->

Bounding the first term: By Lemma 3, 0 ≤ ∆ t +1 ≤ 1 almost surely. Therefore:

<!-- formula-not-decoded -->

Bounding the tail contribution: For j ≥ t +2 , the coupling from Assumption 3 ensures that:

<!-- formula-not-decoded -->

almost surely. This pathwise bound implies:

<!-- formula-not-decoded -->

Summing over j = t +2 , . . . , T :

<!-- formula-not-decoded -->

Since βB &lt; 1 by Assumption 1, we have 1 -( βB ) T -t -1 ≤ 1 , yielding:

<!-- formula-not-decoded -->

Combining both terms: Therefore, almost surely:

<!-- formula-not-decoded -->

Finite-horizon case: If periodic re-grounding occurs every m steps, then ∆ t + k = ∆ ′ t + k for all k &gt; m by Assumption 3. The geometric series truncates at k = m , giving:

<!-- formula-not-decoded -->

<!-- formula-not-decoded -->

This completes the proof.

## B.4 Proof of Theorem 1 (MCP Concentration Bound)

Proof. By Definition 6, { Z t } T t =0 forms a martingale with respect to filtration F t . By Lemma 1, the martingale differences are bounded: | Z t +1 -Z t | ≤ c ⋆ almost surely, where c ⋆ = 1 + C ∗ with C ∗ = α 1 -βB .

Applying Azuma's inequality:

<!-- formula-not-decoded -->

Therefore:

<!-- formula-not-decoded -->

Since Z T = D ( T ) and Z 0 = E [ D ( T )] , we have:

<!-- formula-not-decoded -->

For our uniform bounded difference constant:

<!-- formula-not-decoded -->

<!-- formula-not-decoded -->

Setting δ = √ 2 T (1 + γ ∗ ) ln 1 η , we obtain:

<!-- formula-not-decoded -->

This completes the proof of Theorem 1.

## B.5 Proof of Corollary 1 (Sub-linear deviation)

Proof. Set δ = √ 2 T (1 + γ ∗ ) ln 1 η in Theorem 1. With probability at least 1 -η ,

<!-- formula-not-decoded -->

In practice, exploiting geometric decay structure (Appendix C.5) with B = 1 yields ˆ γ ≪ γ ∗ , making the leading constant tractable for typical chain lengths.

## B.6 Proof of Corollary 2 (Effective Information Horizon)

Proof. From our influence function ϕ ( i, j ) = β j -i , we want to find the number of steps h after which the influence drops below a threshold ϵ :

β h &lt; ϵ

Taking logarithms: h log β &lt; log ϵ

<!-- formula-not-decoded -->

<!-- formula-not-decoded -->

Therefore, errors propagate effectively over approximately H ε = ⌈ log ε log β ⌉ steps. For the parameters used in our experiments ( β = 0 . 7 ), this implies an effective horizon of ⌈ 8 . 33 ⌉ = 9 steps for 5% influence, rather than the stricter e-folding time.

For practical implementation, this means that after every H ε steps, the system can be effectively 'reset' by re-grounding with authoritative information.

## C Full Experimental Details

## C.1 MCPTool Implementation and Deterministic Data Sources

Design philosophy. To isolate distortion dynamics from tool variability, we implement deterministic, cached tools that return identical outputs for identical queries across all models and runs. This ensures observed differences stem from model behavior (reasoning, context management, paraphrasing) rather than stochastic tool responses or network latency.

Knowledge Retrieval Tool. Implements information lookup over a curated corpus of 5,000 factual entries spanning Science, History, Technology, Arts, Sports, Geography, Literature, and Mathematics domains:

```
# Tool interface (tools/knowledge.py) class KnowledgeRetriever: def __init__(self, corpus_path=''data/knowledge_corpus.jsonl''): self.corpus = self._load_corpus(corpus_path) # Pre-compute embeddings self.cache = {} # Query cache for deterministic retrieval def retrieve(self, query: str, top_k: int = 3) -> List[str]: # Deterministic semantic search using cached embeddings if query in self.cache: return self.cache[query] query_emb = self.encoder.encode(query) scores = cosine_similarity(query_emb, self.corpus_embeddings) top_indices = np.argsort(scores)[-top_k:][::-1] results = [self.corpus[i][''text''] for i in top_indices] self.cache[query] = results # Cache for reproducibility return results The corpus file data/knowledge corpus.jsonl contains structured entries: {''id'': ''sci_001'', ''domain'': ''Science'', ''text'': ''The speed of light in vacuum is approximately 299,792,458 m/s.''} {''id'': ''hist_042'', ''domain'': ''History'', ''text'': ''The French Revolution began in 1789 with the storming of the Bastille.''} ...
```

Embeddings are pre-computed using sentence-transformers/all-MiniLM-L6-v2 (same model used for semantic distortion d emb) and cached to disk ( data/knowledge embeddings.npy ), eliminating runtime overhead. Retrieval latency: 5-15 ms per query (constant across all experiments).

Financial Data Tool. Provides structured market data access via cached JSON snapshots simulating real-time financial APIs:

```
# Tool interface (tools/financial.py) class FinancialDataTool: def __init__(self, data_path=''data/market_snapshots.json''): self.data = json.load(open(data_path)) # Load once def get_price(self, symbol: str, date: str = ''2024-01-15'') -> Dict: # Deterministic price lookup from cached snapshots return self.data[''prices''][symbol][date] def get_trend(self, symbol: str, days: int = 30) -> Dict: # Compute trend from cached historical prices prices = [self.data[''prices''][symbol][d][''close''] for d in self._get_date_range(days)] return {''symbol'': symbol, ''trend'': ''up'' if prices[-1] > prices[0] else ''down'' ''change_pct'': 100 * (prices[-1] -prices[0]) / prices[0]}
```

The data file data/market snapshots.json contains daily OHLCV (Open, High, Low, Close, Volume) data for 10 symbols (AAPL, MSFT, GOOGL, AMZN, TSLA, META, NVDA, JPM, V, JNJ) covering January 2024. Schema validation ensures all queries receive well-formed responses. No external API calls occur during experiments-all data is pre-cached.

Tool logging and transparency. Every tool invocation logs to results { model } / { track } /tool calls.jsonl :

```
{''step'': 3, ''tool'': ''knowledge_retrieval'', ''query'': ''What is the speed of light?'', ''results'': [''The speed of light in vacuum is approximately 299,792,458 m/s.'', ...], ''latency_ms'': 8.3}
```

This transparency enables post-hoc auditing of tool usage patterns, retrieval relevance, and latency distributions. Aggregate statistics appear in tool usage summary.csv for each experimental track.

## C.2 Distortion Metric Implementation

The hybrid semantic distortion metric (Equation (1)) combines weighted factual precision ( d w set ) and semantic similarity ( d emb). The implementation resides in core/distortion metric.py and operates in three phases: fact extraction, similarity computation, and hybrid aggregation.

Phase 1: Factual extraction via noun-phrase chunking. We extract atomic factual claims from both reference (ideal, lowtemperature) and observed (actual model) responses using NLTK's averaged perceptron POS tagger combined with rule-based chunking:

```
# core/distortion_metric.py, lines 89-125 def extract_facts(self, text: str) -> Set[str]: ''''''Extract noun phrases as atomic facts from text.'''''' # Tokenize and POS-tag tokens = nltk.word_tokenize(text.lower()) tagged = nltk.pos_tag(tokens) # Define noun-phrase grammar: DET? ADJ* NOUN+ grammar = r'''''' NP: {<DT|PP\$>?<JJ>*<NN.*>+} # Noun phrases '''''' cp = nltk.RegexpParser(grammar) tree = cp.parse(tagged) # Extract noun phrases as facts facts = set() for subtree in tree.subtrees(): if subtree.label() == 'NP': fact = ' '.join(word for word, tag in subtree.leaves()) if len(fact.split()) >= 2: # Filter single-word facts facts.add(fact) return facts
```

This approach extracts semantically meaningful units (e.g., 'speed of light', '299,792,458 meters per second', 'French Revolution') without relying on external knowledge bases or named-entity recognizers. For numerical facts, we normalize representations (e.g., '299792458' ≡ '299,792,458') via regex preprocessing.

Phase 2: Weighted Jaccard similarity ( d w set ). Given extracted fact sets F ref (reference) and F obs (observed), we compute weighted Jaccard distance:

```
# core/distortion_metric.py, lines 164-189 def calculate_weighted_jaccard_distance(self, facts_ref: Set[str], facts_obs: Set[str]) -> float: ''''''Compute weighted Jaccard distance with TF-based weights.'''''' if not facts_ref and not facts_obs: return 0.0 # Both empty = no distortion if not facts_ref or not facts_obs: return 1.0 # One empty = maximal distortion # Assign weights via term frequency (more frequent = higher importance) weights_ref = {fact: self._compute_tf_weight(fact, facts_ref) for fact in facts_ref} weights_obs = {fact: self._compute_tf_weight(fact, facts_obs)
```

```
for fact in facts_obs}
```

```
# Compute weighted intersection and union intersection_weight = sum(min(weights_ref.get(f, 0), weights_obs.get(f, 0)) for f in facts_ref & facts_obs) union_weight = sum(max(weights_ref.get(f, 0), weights_obs.get(f, 0)) for f in facts_ref | facts_obs) # Weighted Jaccard distance: 1 - (weighted_intersection / weighted_union) if union_weight == 0: return 0.0 return 1.0 -(intersection_weight / union_weight)
```

Phase 3: Semantic embedding distance ( d emb). Semantic similarity leverages pre-trained sentence embeddings:

```
# core/distortion_metric.py, lines 227-245 def calculate_semantic_distance(self, text_ref: str, text_obs: str) -> float: ''''''Compute 1 -cosine_similarity using sentence-transformers embeddings.'''''' if not text_ref.strip() or not text_obs.strip(): return 1.0 # Empty text = maximal semantic distance # Encode texts to 384-dim vectors (all-MiniLM-L6-v2) emb_ref = self.encoder.encode(text_ref, convert_to_tensor=True) emb_obs = self.encoder.encode(text_obs, convert_to_tensor=True) # Cosine similarity in [-1, 1], normalized to distance in [0, 1] cos_sim = torch.nn.functional.cosine_similarity(emb_ref.unsqueeze(0), emb_obs.unsqueeze(0)).item() return (1.0 -cos_sim) / 2.0 # Map to [0, 1]
```

Embeddings cache to GPU memory (total: 250 MB for all experimental texts) to accelerate repeated calculations during post-processing.

## Phase 4: Hybrid aggregation (Equation (1) ). The final distortion combines both components:

```
# core/distortion_metric.py, lines 273-285 def calculate_semantic_distortion(self, response_ref: str, response_obs: str, lambda_weight: float = 0.5) -> float: ''''''Compute hybrid semantic distortion per Equation 1.'''''' # Extract facts for weighted Jaccard facts_ref = self.extract_facts(response_ref) facts_obs = self.extract_facts(response_obs) d_set_w = self.calculate_weighted_jaccard_distance(facts_ref, facts_obs) # Compute semantic embedding distance d_emb = self.calculate_semantic_distance(response_ref, response_obs) # Hybrid distortion: d_sem = (1-lambda) * d_setˆw + lambda * d_emb d_sem = (1.0 -lambda_weight) * d_set_w + lambda_weight * d_emb return d_sem
```

This modular design enables independent ablation of factual vs. semantic components (exploited in the lambda sweep experiments in Section 5).

Validation and unit tests. Unit tests in tests/test distortion metric.py verify correctness against hand-crafted examples:

- Identical texts yield d sem = 0 for all λ .
- Completely unrelated texts yield d sem ≈ 1 for all λ .
- Paraphrases (same meaning, different wording) produce d w set &gt; 0 but d emb ≈ 0 , confirming metric sensitivity.
- Numerical precision: distortion values stable to ± 10 -6 across repeated calculations.

## C.3 Concentration Bounds Implementation

Theorem 1 provides high-probability envelopes for cumulative distortion. The implementation in core/concentration bounds.py computes calibrated bounds using observed dependency structures.

Gamma calculation for geometric decay. The effective dependency parameter ˆ γ captures the variance inflation due to temporal correlations:

```
# core/concentration_bounds.py, lines 160-185 def calculate_gamma(self, beta: float, T: int, alpha: float = 1.0, delta_max: float = 1.0) -> float: ''''''Compute gamma-hat for geometric dependency decay (Theorem 1).'''''' if beta >= 1.0: # Handle edge case: beta >= 1 requires finite-horizon truncation # Use conservative worst-case bound return 2.0 * T # Conservative estimate if beta <= 0: return 0.0 # Independent case # Geometric decay formula (Appendix C.5 calibration methodology): # gamma-hat = (alpha² beta² delta_max² / (1 - beta²)) * (1 - betaˆ(2(T-1))) / T numerator = (alpha ** 2) * (beta ** 2) * (delta_max ** 2) denominator = (1 -beta ** 2) geometric_sum = (1 -beta ** (2 * (T -1))) / T gamma_hat = (numerator / denominator) * geometric_sum return gamma_hat
```

High-probability confidence bound. Given observed mean distortion ˆ E [ D ( T )] and confidence level δ (typically 0.05 for 95% confidence), the envelope is:

```
# core/concentration_bounds.py, lines 188-210 def confidence_bound(self, T: int, mean_distortion: float, beta: float, delta: float = 0.05, lambda_weight: float = 0.5) -> float: ''''''Compute high-probability envelope from Theorem 1.'''''' gamma_hat = self.calculate_gamma(beta, T) # Deviation term: sqrt(2 * (1 + gamma-hat) * T * ln(1/delta)) deviation = np.sqrt(2.0 * (1 + gamma_hat) * T * np.log(1.0 / delta)) # Calibrated envelope: E[D(T)] + deviation envelope = mean_distortion + deviation return envelope
```

This approach mirrors the 'calibrated envelopes' described in §5 and detailed in Appendix C.5. Empirical validation across 3,000+ chains confirms envelope violation rates &lt; 5% (consistent with δ = 0 . 05 ).

First-step rate estimation. For calibration, we estimate E [ D ( T )] via first-step extrapolation:

```
# experiments/distortion_experiment.py, lines 590-608 def estimate_expected_distortion(self, traces: List[Dict], T: int) -> float: ''''''Estimate E[D(T)] from first-step distortion rates.'''''' # Extract first-step distortion from all chains first_step_distortions = [trace[''distortions''][0] for trace in traces if len(trace[''distortions'']) > 0] if not first_step_distortions: return 0.0 # Mean first-step rate mean_first_step = np.mean(first_step_distortions)
```

- constant per-step rate, validated empirically)

```
# Linear extrapolation: E[D(T)] = T * mean_first_step # (assumes approximately return T * mean_first_step
```

Unit tests verify that for chains with constant per-step distortion, this estimation achieves &lt; 2% relative error.

## C.4 Preliminary: Dependency Strength Ablation

To isolate the effect of temporal dependence, we first conducted a controlled ablation varying β ∈ { 0 . 5 , 0 . 7 , 0 . 9 } while holding all other parameters constant: λ = 0 . 5 , 50 chains, T = 10 , with simulated base error rate 0.1 per query.

Results confirm that both mean and variance of per-step distortion scale inversely with 1 -β . Even at β = 0 . 9 (strong coupling), distortion at T = 10 remains approximately one order of magnitude below naive linear accumulation. Guided by this analysis, we fix β = 0 . 7 (yielding an effective horizon of approximately 9 steps) as our standard configuration for downstream experiments.

## C.5 Calibrated Envelopes: Methodology and Justification

Throughout Section 5, we overlay calibrated envelopes on empirical distortion trajectories to visualize alignment with Theorem 1. This subsection provides the technical details, theoretical justification, and connection to standard concentration practices.

Construction methodology. Our calibration procedure consists of three steps:

1. Estimate expected per-step distortion: For each configuration (model, β , λ , noise level), we compute the mean first-step distortion ˆ r across all chains: ˆ r = 1 N ∑ N i =1 ∆ ( i ) 1 where N is the number of independent chains and ∆ ( i ) 1 is the first-step distortion of chain i .
2. Extrapolate expected cumulative distortion: Under the assumption that per-step distortion rates remain approximately constant (validated empirically across all experiments), we estimate ˆ E [ D ( T )] = T · ˆ r via linear extrapolation.
3. Add theoretical deviation term: We compute the high-probability envelope as:

<!-- formula-not-decoded -->

where ˆ γ is computed specifically for geometric decay (see below) and δ = 0 . 05 (95% confidence).

Computing ˆ γ for geometric decay. Theorem 1 provides a worst-case bound with γ ∗ = 2 C ∗ + ( C ∗ ) 2 where C ∗ m = α ∑ m -1 k =0 ( βB ) k = α 1 -( βB ) m 1 -βB . For deployment, we instantiate ˆ γ using the structure of geometric decay:

<!-- formula-not-decoded -->

where α ∈ [0 , 1] (response stability coefficient from Assumption 2), δ max ∈ [0 , 1] is the maximum per-step distortion bound (from Lemma 3), and the term (1 -β 2( T -1) ) / (1 -β 2 ) captures the sum of squared geometric weights. For α = 1 , δ max = 1 , β = 0 . 7 : ˆ γ (10) ≈ 0 . 096 , ˆ γ (30) ≈ 0 . 032 , ˆ γ (60) ≈ 0 . 016 . This provides much tighter bounds than worst-case γ ∗ = 2 C ∗ +( C ∗ ) 2 ≈ 17 . 8 when using C ∗ = α/ (1 -βB ) with B = 1 .

This calibration strategy follows established practice in the empirical Bernstein literature, where structure-specific variance estimates replace worst-case bounds when the dependence structure is known. The key requirement-that the variance estimate is not selected adaptively based on the outcome-is satisfied since ˆ γ depends only on the pre-specified decay rate β and chain length T .

Relationship to worst-case bounds. The distinction between γ ∗ (formal theorem) and ˆ γ (calibrated experiments) parallels the difference between Hoeffding's inequality (uses worst-case variance σ 2 ≤ 1 / 4 for bounded [0 , 1] variables). Both are theoretically sound; the latter is tighter for deployment.

In our context:

- γ ∗ provides worst-case guarantees for any dependency structure satisfying Definition 2.
- ˆ γ provides tighter guarantees for geometric decay specifically , which we verify empirically via assumption diagnostics ( experiments/assumption checks.py ).

Implementation note. Our Python implementation ( core/concentration bounds.py ) uses the structure-specific ˆ γ ( T ) formula for tighter practical bounds. While Theorem 1 proves concentration using uniform bounded differences c ⋆ = 1 + C ∗ , the implementation exploits time-dependent refinements where martingale increments near chain end have smaller bounded differences (as they influence fewer future steps). This yields tighter envelopes while maintaining valid probabilistic guarantees, with empirically verified violation rates &lt; 5% across all experiments. For reference implementations requiring maximum simplicity, practitioners may use the uniform γ ∗ = 2 C ∗ +( C ∗ ) 2 formulation from the theorem directly.

## Ten-step baseline (T=10)

Figure 8: Mistral-7B baseline distortion accumulation over T = 10 queries with β = 0 . 7 , λ = 0 . 5 (50 chains). Solid curves: empirical mean ± 1 s.d. (shaded regions); dotted curves: calibrated bounds from Theorem 1. Safety margin: 1.66 × , comparable to Qwen (1.74 × ) and Llama (1.81 × ).

<!-- image -->

Validation across experiments. Across all 3,000+ experimental chains (Qwen, Llama, Mistral combined), envelope violation rates consistently respect the δ = 0 . 05 threshold: baseline ( &lt; 4% violations), lambda sweep ( &lt; 6% ), long-chain ( &lt; 5% ), highbeta ( &lt; 7% ). These empirically measured rates align with the theoretical 95% confidence level, with slight increases in stresstest regimes expected due to boundary effects. The consistent validation across diverse experimental conditions-spanning three model architectures, multiple parameter regimes, and both in-distribution and stress-test scenarios-demonstrates the robustness of our calibration methodology.

Practical implications. For practitioners deploying MCP systems:

- Use worst-case γ ∗ for safety-critical applications where conservative bounds are required.
- Use calibrated ˆ γ for operational monitoring where tighter bounds improve alert precision without sacrificing guarantees.
- Recompute ˆ γ when changing dependency patterns (e.g., switching from exponential to power-law decay).

This methodology bridges rigorous theory with deployment realism, enabling both provable safety and actionable operational bounds-a key contribution for practical agentic AI systems.

## D Experimental Results of Mistral

To complement the Qwen2-7B and Llama-3-8B results presented in Section 5, we evaluate Mistral-7B-Instruct-v0.3 across the same experimental tracks. This third architecture confirms the generalizability of our theoretical framework across different model families.

## D.1 Baseline Validation

Under standard conditions ( T = 10 , β = 0 . 7 , λ = 0 . 5 ), Mistral-7B achieves empirical distortion D (10) = 5 . 33 ± 0 . 44 versus the calibrated envelope of 8.83, yielding a safety margin of 1 . 66 × . This performance closely matches Qwen2-7B ( D (10) = 5 . 26 , margin 1 . 74 × ) and Llama-3-8B ( D (10) = 4 . 92 , margin 1 . 81 × ), confirming that all three architectures exhibit comparable distortion dynamics under identical workloads. The first-step error rate of 0 . 44 falls between the Qwen and Llama values, suggesting Mistral's tool-usage reliability is on par with the other models.

## 入 sweep (T=30)

Figure 9: Mistral-7B semantic weighting sensitivity at T = 30 with β = 0 . 7 , sweeping λ ∈ { 0 , 0 . 25 , 0 . 5 , 0 . 75 , 1 . 0 } (8 chains per configuration). Solid curves: empirical mean ± 1 s.d.; dotted curves: calibrated bounds from Theorem 1. Increasing λ reduces distortion by ∼ 81%, matching Qwen and Llama's patterns.

<!-- image -->

## D.2 Lambda Sweep and Long-Chain Scaling

For the semantic weighting sensitivity analysis ( T = 30 , β = 0 . 7 , λ ∈ { 0 , 0 . 25 , 0 . 5 , 0 . 75 , 1 . 0 } ), Mistral exhibits similar trends to Qwen and Llama: increasing λ from 0 to 1 reduces distortion substantially. At λ = 0 (pure factual mismatch), Mistral records D (30) = 27 . 06 versus envelope 31.73 (margin 1 . 17 × ). At λ = 1 (pure semantic), distortion drops to D (30) = 5 . 07 with envelope 10.19 (margin 2 . 01 × ), representing an 81% reduction-consistent with the ∼ 80% reductions observed in Qwen and Llama.

In long-chain experiments ( T = 60 , β = 0 . 7 ), Mistral achieves D (60) = 31 . 99 ± 0 . 68 versus envelope 36.50 (margin 1 . 14 × ), tracking smoothly with the linear expectation and O ( √ T ) concentration bounds. At β = 0 . 5 , distortion is D (60) = 30 . 79 (margin 1 . 18 × ), and at β = 0 . 9 , it reaches D (60) = 33 . 33 (margin 1 . 10 × ). These values align closely with Qwen and Llama across the same dependency range, validating Corollary 1 consistently across architectures.

## D.3 Stress Testing

High-dependency stress tests ( β ∈ { 0 . 95 , 0 . 98 } , T = 30 ) reveal graceful degradation: at β = 0 . 95 , Mistral records D (30) = 16 . 52 versus envelope 24.60 (margin 1 . 49 × ), and at β = 0 . 98 , D (30) = 16 . 79 with envelope 25.99 (margin 1 . 55 × ). These margins slightly exceed those of Qwen ( 1 . 30 × , 1 . 41 × ) and Llama ( 1 . 36 × , 1 . 41 × ), suggesting Mistral may maintain tighter control under extreme dependencies, though differences are modest.

For adversarial noise experiments ( T = 30 , noise ∈ { 0 . 0 , 0 . 1 , 0 . 2 } ), Mistral displays comparable robustness to corrupted tool outputs. At noise level 0.0, D (30) = 15 . 89 (margin 1 . 27 × ); at 0.1, D (30) = 15 . 93 (margin 1 . 38 × ); and at 0.2, D (30) = 15 . 96 (margin 1 . 28 × ). The relatively stable distortion across noise levels indicates that Mistral's semantic component effectively absorbs perturbations, consistent with the patterns observed in Qwen and Llama.

## D.4 Cross-Model Summary

Table 2 summarizes key metrics across all three architectures. Mistral-7B demonstrates:

- Comparable baseline performance: D (10) = 5 . 33 (Mistral) vs. 5 . 26 (Qwen) vs. 4 . 92 (Llama), all with safety margins exceeding 1 . 6 × .

## Long-chain scaling (T=60)

Figure 10: Mistral-7B long-chain scaling to T = 60 across β ∈ { 0 . 5 , 0 . 7 , 0 . 9 } with λ = 0 . 5 (6 chains per configuration). Solid curves: empirical mean ± 1 s.d.; dotted curves: calibrated bounds from Theorem 1. Smooth sublinear growth persists with safety margins 1.10 × -1.18 × across all dependency levels.

<!-- image -->

- Consistent lambda sensitivity: ∼ 81% distortion reduction from λ = 0 to λ = 1 , matching Qwen ( 80 . 7% ) and Llama ( 80 . 9% ).
- Robust long-chain scaling: D (60) = 31 . 99 (Mistral) vs. 31 . 85 (Qwen) vs. 30 . 37 (Llama) at β = 0 . 7 , with margins 1 . 14 × -1 . 21 × across all models.
- Slightly tighter stress margins: Mistral maintains 1 . 49 × -1 . 55 × margins at β ∈ { 0 . 95 , 0 . 98 } , compared to 1 . 30 × -1 . 41 × for Qwen/Llama.

These results validate that our martingale framework captures fundamental MCP dynamics rather than model-specific artifacts. Minor variations (e.g., Mistral's slightly tighter highβ margins) likely reflect differences in pre-training data or instruction-tuning, but the overall distortion patterns remain architecturally robust.

These consistent patterns suggest that distortion accumulation in MCP systems is governed by fundamental informationtheoretic constraints on sequential tool usage, rather than by model-specific architectural choices such as attention mechanisms, hidden dimensions, or pre-training corpora. Future work could extend this analysis to encoder-decoder architectures (e.g., T5, BART) and mixture-of-experts models (e.g., Mixtral-8x7B) to further validate these findings across even broader architectural diversity.

Figure 11: Mistral-7B high-dependency stress tests at T = 30 with β ∈ { 0 . 95 , 0 . 98 } and λ = 0 . 5 (6 chains per configuration). Solid curves: empirical mean ± 1 s.d.; dotted curves: calibrated bounds from Theorem 1. Safety margins 1.49 × -1.55 × exceed Qwen/Llama, demonstrating graceful degradation under extreme dependencies.

<!-- image -->

Table 2: Cross-model comparison of key experimental metrics. Qwen2-7B, Llama-3-8B, and Mistral-7B exhibit highly consistent distortion dynamics across all tracks, with standard deviations averaging &lt; 10% of mean values. Minor variations reflect architectural differences rather than framework limitations.

| Metric                        | Qwen2-7B   | Llama-3-8B   | Mistral-7B   | Avg. Std.   |
|-------------------------------|------------|--------------|--------------|-------------|
| D (10) baseline               | 5.26       | 4.92         | 5.33         | ± 0 . 17    |
| Baseline margin               | 1.74 ×     | 1.81 ×       | 1.66 ×       | ± 0 . 06    |
| Lambda reduction              | 80.7%      | 80.9%        | 81.2%        | ± 0 . 2%    |
| D (60) at β = 0 . 7           | 31.85      | 30.37        | 31.99        | ± 0 . 70    |
| High- β margin ( β = 0 . 98 ) | 1.41 ×     | 1.41 ×       | 1.55 ×       | ± 0 . 07    |