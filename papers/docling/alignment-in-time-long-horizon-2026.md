## Alignment in Time: Peak-Aware Orchestration for Long-Horizon Agentic Systems

Hanjing Shi hasa23@lehigh.edu Lehigh University Bethlehem, Pennsylvania, USA

Dominic DiFranzo djd219@lehigh.edu Lehigh University Bethlehem, Pennsylvania, USA

average step correctness, but also on how systems recover from intermediate failures and how trajectories conclude.

Psychological research demonstrates that retrospective evaluations of experiences are temporally structured. The peak-end rule shows that judgments are disproportionately influenced by the most intense moment and the ending of an experience [9, 12]. This temporal asymmetry suggests that interaction quality cannot be fully captured by mean performance alone. If human evaluation is temporally weighted, then alignment mechanisms that ignore trajectory structure may systematically misalign with how users assess system reliability and reuse potential.

In this paper, we introduce APEMO (Affect-aware Peak-End Modulation for Orchestration), a temporal-affective orchestration layer for AI workflows. APEMO does not modify model weights or training objectives. Instead, it reallocates reasoning effort and repair across a trajectory under fixed computational budgets. We formulate temporal orchestration as a constrained multi-objective optimization problem that balances trajectory-level quality, reuse propensity, frustration-related behavioral signals, and coordination cost.

Through systematic evaluation across long-horizon single-agent trajectories and multi-agent workflows, we investigate whether temporal control can function as a bidirectional alignment layer. Rather than aligning only token-level outputs to predefined values, our approach aligns interaction dynamics with evaluation-sensitive trajectory properties. This reframes alignment as a property of entire interaction sequences rather than isolated predictions.

Figure 1 illustrates the core intuition of our approach. Under uniform budget allocation, negative peaks persist and endings remain weak, leading to trajectory-level instability. APEMO introduces temporal control that detects and repairs negative peaks while preserving a fixed compute budget.

## 2 Related Work

## 2.1 Model Alignment and Preference Modeling

Reinforcement Learning from Human Feedback (RLHF) aligns model outputs with human preferences by learning reward functions from labeled comparisons [5, 20]. However, human preference judgments are not temporally neutral. Prior work has shown that trajectorylevel evaluations often exhibit non-Markovian structures, where judgments depend disproportionately on salient segments rather than average reward [7].

Most alignment methods treat such temporal weighting as bias to be corrected within reward modeling. In contrast, our work treats temporal asymmetry as a structural signal. Rather than modifying the reward function or retraining the model, we operationalize evaluation-sensitive weighting at runtime. This shifts alignment

## Abstract

Traditional AI alignment primarily focuses on individual model outputs; however, autonomous agents in long-horizon workflows require sustained reliability across entire interaction trajectories. We introduce APEMO (Affect-aware Peak-End Modulation for Orchestration), a runtime scheduling layer that optimizes computational allocation under fixed budgets by operationalizing temporal-affective signals. Instead of modifying model weights, APEMO detects trajectory instability through behavioral proxies and targets repairs at critical segments, such as peak moments and endings. Evaluation across multi-agent simulations and LLM-based planner-executor flows demonstrates that APEMO consistently enhances trajectorylevel quality and reuse probability over structural orchestrators. Our results reframe alignment as a temporal control problem, offering a resilient engineering pathway for the development of long-horizon agentic systems.

## 1 Introduction

Artificial intelligence systems are rapidly transitioning from singleturn instruction followers to autonomous, multi-step agents operating within long-horizon workflows. Recent work on autonomous LLM agents has demonstrated sustained multi-step planning and open-ended interaction [27]. In such environments, large language models (LLMs) plan, execute, and coordinate across specialized roles to solve open-ended tasks. Alignment in these settings is no longer solely a property of model parameters; it unfolds across time. System behavior accumulates over turns, and user expectations adapt throughout the interaction. Over extended trajectories, accumulated inconsistencies may lead to cumulative frustration or degradation of behavioral coherence.

Recent advances in AI alignment have focused on value specification and instruction tuning, particularly through reinforcement learning from human feedback (RLHF) [5, 20]. In parallel, the HCI community has established principles for transparency, trust calibration, and effective human-AI collaboration [1, 2]. These approaches primarily operate at the model or interface level. They improve output correctness, explanation quality, and interaction clarity. However, they do not explicitly address how alignment unfolds across extended interaction sequences.

At the orchestration level, multi-agent and workflow frameworks have advanced structural optimization. Structured reasoning paradigms such as ReAct [30] and automated workflow generation systems such as AFlow [31] focus on improving task accuracy, routing strategies, and computational efficiency. Yet these systems typically aggregate performance across steps. They rarely treat the temporal structure of interaction as an alignment variable. In long-horizon workflows, perceived reliability depends not only on

Figure 1: APEMO as a temporal-affective overlay for long-horizon workflows. Uniform allocation optimizes mean-step performance but allows negative peaks to persist. APEMO reallocates effort toward peak repair and ending stabilization under fixed compute budgets, improving trajectory-level robustness.

<!-- image -->

from a parameter-level optimization problem to a trajectory-level control problem.

Recent work on self-reflection and iterative reasoning, such as Tree-of-Thoughts [29], Reflexion [25], and Self-Refine [18], improves performance by expanding search depth or introducing additional reasoning passes. These approaches typically increase computational effort to enhance solution quality. In contrast, APEMO operates under a fixed global budget and reallocates compute across turns. Our objective is not to increase total reasoning depth, but to redistribute effort toward evaluation-critical segments such as negative peaks and final states.

## 2.2 Agentic Orchestration and Multi-Agent Workflows

The field has rapidly progressed from single-step inference to compound AI systems composed of planners, executors, critics, and memory modules. ReAct interleaves reasoning and acting to improve task performance [30]. AFlow automates agentic workflow generation using structured search [31]. AgentOrchestra introduces hierarchical coordination via the Tool-Environment-Agent protocol [32]. More recent systems such as CORAL [23], Kairos [4], and Orchestrator [3] address information flow, latency, and longhorizon coordination efficiency.

These systems focus primarily on structural optimization, search efficiency, routing logic, or serving scalability. Their objectives typically aggregate performance across steps, implicitly approximating mean-step optimization. While some incorporate reflection or memory mechanisms, they rarely formalize temporal asymmetry as an optimization target. In particular, coordination cost is often treated as engineering overhead rather than an explicit trade-off variable.

Our work complements these orchestration architectures by introducing a temporal-affective overlay that operates orthogonally to workflow topology. Instead of redesigning agent roles or adding new modules, APEMO reallocates existing compute to stabilize evaluation-critical segments under a fixed budget. This distinguishes compute reallocation from compute expansion and frames coordination tax as a first-class variable in alignment analysis.

## 2.3 Temporal Evaluation and Robust Interaction Trajectories

The peak-end rule demonstrates that retrospective evaluations are disproportionately influenced by the most intense moment and the ending of an experience [9, 12]. This temporal asymmetry suggests that interaction quality cannot be fully captured by mean performance alone. If human assessment is temporally weighted, then alignment mechanisms that optimize uniform step performance may systematically diverge from user-perceived reliability.

In long-horizon AI systems, extended interaction introduces additional challenges, including behavioral drift and coherence degradation over time. Open-ended agents such as Voyager illustrate how multi-step autonomy increases exposure to trajectory instability [27]. Memory architectures such as BMAM provide mechanisms for salience-aware retrieval [16], yet memory storage alone does not specify how compute should be redistributed when instability arises.

APEMO integrates temporal evaluation principles with runtime control by treating frustration-related behavioral proxies as actionable signals. Rather than enforcing uniform quality across all turns, it prioritizes negative-peak recovery and end-state stabilization under fixed computational constraints.

To validate this control principle across abstraction levels, we adopt a stacked evidence strategy spanning Agent-Based Modeling (ABM), single-agent LLM reasoning, and multi-agent workflows. ABM isolates temporal dynamics under controlled Monte Carlo sampling; single-agent LLM experiments test robustness under neural stochasticity; and multi-agent evaluations verify that temporal control remains orthogonal to role decomposition. Together, these layers demonstrate that peak-aware compute reallocation is not a simulation artifact but a transferable orchestration principle.

## 3 Methodology

## 3.1 Problem Formulation

Wemodelalong-horizon AI workflow as a trajectory of 𝑇 sequential turns. At each turn 𝑡 , the system produces an intermediate output 𝑦 𝑡 and generates observable behavioral signals. Rather than optimizing isolated step accuracy, we treat alignment as a trajectory-level property.

APEMOdefines a runtime policy 𝜋 that maximizes the following constrained objective:

<!-- formula-not-decoded -->

where:

- 𝑄 denotes peak-end weighted trajectory quality,
- 𝑅 denotes reuse-related robustness,
- 𝐹 denotes cumulative frustration burden,
- 𝐶 denotes coordination cost.

All policies operate under identical model weights, decoding parameters, workflow topology, and shared computational caps. Realized coordination cost may differ by policy and is explicitly measured. APEMO differs from baselines only in runtime temporal allocation and repair scheduling.

Trajectory-level quality 𝑄 is defined using peak-end weighting:

<!-- formula-not-decoded -->

Coordination cost accumulates as:

<!-- formula-not-decoded -->

with the global constraint:

<!-- formula-not-decoded -->

This formulation reframes alignment as trajectory dynamics optimization rather than token-level correctness.

## 3.2 Runtime Control Mechanism

Figure 2 illustrates the APEMO internal control loop.

At each turn, behavioral proxy signals are aggregated into a scalar frustration score 𝑆 𝑓 ( 𝑡 ) . A peak-detection condition evaluates whether trajectory quality has entered a negative regime. When triggered, a precision repair operation reallocates compute from low-impact turns to peak or terminal segments.

The repair process does not modify model weights or workflow topology. Instead, it redistributes inference precision within the same global compute budget. Budget enforcement ensures that total coordination cost remains bounded.

Unlike topology-based orchestrators, APEMO operates as a temporal overlay that adjusts when and where reasoning precision is applied.

## 3.3 Trade-off and Frontier Analysis

Because temporal scheduling introduces coordination overhead, we analyze the trade-off between robustness gains and coordination cost.

Configurations are projected onto a Pareto frontier defined over ( 𝑅,𝐶 ) . A configuration is considered economically viable if improvements in reuse-related robustness exceed marginal coordination overhead.

This analysis isolates whether trajectory-level stabilization justifies additional orchestration cost under fixed budgets.

## 4 Experimental Setup

We evaluate APEMO under controlled equal-budget conditions to isolate the effect of temporal-affective orchestration. Our objective is not to optimize model training but to test whether runtime temporal control improves trajectory-level robustness.

All experiments are conducted using locally deployed smallmodel LLM families, including llama3.2:1b , qwen2.5:1.5b , and gemma2:2b , executed through an Ollama-based runtime. Prompt templates, decoding parameters, and sampling temperature are held constant across all conditions. No fine-tuning, reward modification, or architectural changes are applied.

Long-horizon evaluation uses 𝑇 = 8 turns to induce trajectory drift and potential negative peaks. Short-horizon evaluation uses 𝑇 = 2 to test whether improvements depend on extended context accumulation. Multi-agent settings adopt a fixed Planner-ExecutorCritic topology across all variants, with APEMO applied solely as a temporal overlay.

## APEMO: Internal Control Loop

Figure 2: APEMO internal control loop. At each turn 𝑡 , a frustration signal 𝑆 𝑓 ( 𝑡 ) is computed from behavioral proxies. If a negative peak is detected, a precision repair module reallocates compute toward peak and ending turns. Coordination cost accumulates under a fixed budget constraint 𝐶 ≤ 𝐶 𝑚𝑎𝑥 .

<!-- image -->

Baselines include uniform budget allocation (mean-step optimization), structured reflective orchestration ( plan\_execute and plan\_execute\_reflect ), and role-based multi-agent flows without temporal reallocation (fl ow\_plain and fl ow\_temporal ). All policies are evaluated under the same runtime protocol and global budget constraints; realized token and orchestration costs are then compared directly.

To evaluate recovery robustness, we introduce a controlled perturbation benchmark that injects mid-trajectory reasoning degradation to simulate negative peaks. Recovery quality is measured as post-perturbation stabilization relative to pre-trap performance.

Performance is evaluated using four trajectory-level metrics: peak-end weighted quality 𝑄 , reuse-related robustness 𝑅 , cumulative frustration 𝐹 , and coordination cost 𝐶 .

We conduct both agent-based model (ABM) simulations and real LLM executions. ABM simulations are used for mechanism calibration and stress testing, while small-model LLM experiments provide the primary quantitative evidence reported in the main Results section. This separation avoids over-claiming from simulator-only dynamics while preserving a controlled mechanism-development layer.

## 4.1 Primary Block Configuration Overview

Table 1 summarizes the main LLM blocks reported in Results, including run count and validity gate.

## 4.2 Statistical Analysis

All statistics are computed at the run level. Each run corresponds to a fixed configuration defined by model, seed, policy, and trajectory length. Episode-level metrics are aggregated within run to avoid pseudo-replication.

For each metric, we report mean, standard deviation (std), and standard error (se). Uncertainty is estimated using nonparametric bootstrap confidence intervals with 95% coverage via resampling over runs.

Pairwise comparisons are expressed as delta metrics:

Δ = MetricAPEMO -MetricBaseline .

For each delta, we report the 95% bootstrap confidence interval.

Table 1: Primary LLM block configuration summary used in main Results.

| Block                                | Models                               |   Turns | Episodes   | Runs (n) Policies                                        |   no_fallback_rate |
|--------------------------------------|--------------------------------------|---------|------------|----------------------------------------------------------|--------------------|
| Strict Long-Horizon                  | qwen2.5:1.5b, gemma2:2b              |       8 | 2 20       | task_affect, task_peak_end, apemo                        |                  1 |
| Short-Horizon Boundary               | llama3.2:1b, qwen2.5:1.5b, gemma2:2b |       2 | 2 21       | task_affect, task_peak_end, apemo                        |                  1 |
| Trap Recovery (strict)               | gemma2:2b                            |       8 | 1 20       | task_peak_end, apemo                                     |                  1 |
| Multi-Agent Flow                     | qwen2.5:1.5b, gemma2:2b              |       8 | 1 16       | flow_plain, flow_temporal, apemo                         |                  1 |
| L3 Plan-Execute-Class                | llama3.2:1b, qwen2.5:1.5b, gemma2:2b |       2 | 2 30       | plan_execute, plan_execute_reflect, task_peak_end, apemo |                  1 |
| L3 Plan-Execute-Class (Long-Horizon) | llama3.2:1b, qwen2.5:1.5b, gemma2:2b |       8 | 1 18       | plan_execute, plan_execute_reflect, task_peak_end, apemo |                  1 |

Table 2: Strict long-horizon evaluation ( 𝑇 = 8 , 𝑛 = 20 ). Values are mean deltas with 95% bootstrap CIs.

| Baseline      | Mean Quality                | Reuse Prob.                 | Avg. Frust.                   |
|---------------|-----------------------------|-----------------------------|-------------------------------|
| task_peak_end | + 0 . 0791 [0.0525, 0.1055] | + 0 . 0609 [0.0383, 0.0826] | - 0 . 0172 [-0.0306, -0.0071] |
| task_affect   | + 0 . 1874 [0.1596, 0.2159] | + 0 . 1161 [0.0861, 0.1433] | - 0 . 0543 [-0.0805, -0.0356] |

As a conservative small-sample robustness check, we additionally report two-sided sign-test p-values based on directional wins across runs. Key p-values are reported in the main Results text and full p-value tables are provided in block-level comparison files. This nonparametric test avoids distributional assumptions.

Interpretation is gated by reliability validity. For each block, we compute a no\_fallback\_rate . Blocks with incomplete validity are treated as directional evidence rather than primary statistical support.

All comparisons are theory-driven and restricted to predefined trajectory-level objectives. Primary endpoints were fixed before large𝑛 reruns. These include mean quality, reuse probability, reuseper-cost, average frustration, and trap endpoint and rebound metrics. We emphasize effect sizes and uncertainty intervals over binary significance claims.

## 5 Results

Wereport results with a staged validation strategy progressing from controlled trajectory tests to perturbation recovery and multi-agent extensions. All statistics are computed at the run level. Reported intervals are nonparametric 95% bootstrap confidence intervals.

## 5.1 Long-Horizon Trajectory Performance

We first evaluate APEMO under strict long-horizon settings ( 𝑇 = 8) with cross-model runs and 𝑛 = 20 independent configurations. All runs completed without fallback (no\_fallback\_rate = 1.0).

Relative to the peak-end baseline, APEMO improves mean trajectory quality by + 0 . 0791 (95% CI [0.0525, 0.1055], sign-test 𝑝 = 4 . 01 × 10 -5 ) and reuse probability by + 0 . 0609 (95% CI [0.0383, 0.0826], 𝑝 = 4 . 01 × 10 -5 ). Average frustration decreases by -0 . 0172 (95% CI [-0.0306, -0.0071], 𝑝 = 0 . 115).

Relative to the affect baseline, gains are larger: quality + 0 . 1874 (95% CI [0.1596, 0.2159], 𝑝 = 1 . 91 × 10 -6 ), reuse probability + 0 . 1161 (95% CI [0.0861, 0.1433], 𝑝 = 4 . 01 × 10 -5 ), and frustration -0 . 0543 (95% CI [-0.0805, -0.0356], 𝑝 = 1 . 91 × 10 -6 ).

Table 2 summarizes the primary long-horizon results.

Figure 3 reports effect sizes and confidence intervals across longhorizon, short-horizon, trap, and multi-agent blocks.

## 5.2 ABMMechanism Consistency (Support Layer)

To verify mechanism consistency beyond specific model families, we run high-volume ABM stress tests under matched policy structure. ABM results are directionally aligned with LLM findings (better endpoint/recovery behavior with lower frustration), supporting the control mechanism itself rather than any single LLM idiosyncrasy.

Because simulator effect magnitudes can be inflated relative to real LLM stochastic regimes, we treat ABM as support-layer evidence and report full ABM effect tables in supplementary material rather than using them as the primary basis of claims.

## 5.3 Short-Horizon Regime Boundary

To test horizon sensitivity, we evaluate short-horizon runs ( 𝑇 = 2, 𝑛 = 21, no\_fallback\_rate = 1 . 0).

APEMO improves mean quality by + 0 . 0520 (95% CI [0.0303, 0.0750], 𝑝 = 0 . 0072) and reuse probability by + 0 . 0613 (95% CI [0.0339, 0.0910], 𝑝 = 0 . 0072) over task\_peak\_end. Reuse-per-cost remains positive ( + 0 . 0051, 95% CI [0.0015, 0.0085], 𝑝 = 0 . 0266), though effect magnitudes are smaller than in long-horizon settings.

This boundary condition suggests that coordination overhead amortizes more effectively when trajectories are deeper, consistent with the trajectory-level control hypothesis developed in Discussion.

## 5.4 Negative-Peak Perturbation and Recovery

We introduce controlled negative-peak perturbations ( 𝑛 = 20, no\_fallback\_rate = 1 . 0).

APEMO improves endpoint delivery quality by + 0 . 1243 (95% CI [0.0522, 0.1924], 𝑝 = 0 . 0118) relative to task\_peak\_end. Rebound metrics remain directionally positive but variance-sensitive: trap\_quality\_rebound2 + 0 . 0807 (95% CI [0.0075, 0.1540], 𝑝 = 0 . 824), while trap\_frustration\_drop2 + 0 . 0439 (95% CI [-0.0084, 0.0943], 𝑝 = 0 . 263) includes zero. The CI and sign-test discrepancy on rebound is expected under this conservative test with mixed directional wins.

These results indicate that endpoint stabilization is robust, whereas intermediate rebound amplitudes depend more heavily on stochastic cross-model dynamics. Figure 4 shows trap-response dynamics. APEMO shows weaker quality collapse at the trap turn and improved endpoint recovery behavior.

## 5.5 Multi-Agent Workflow Extension

We extend APEMO above a Planner-Executor-Critic topology ( 𝑛 = 16, no\_fallback\_rate = 1 . 0).

Relative to plain multi-agent flow, APEMO improves quality by + 0 . 1782 (95% CI [0.1386, 0.2132], 𝑝 = 3 . 05 × 10 -5 ) and reuse

## APEMO EffectSizeswith 95%BootstrapCI

Figure 3: Forest-style effect plot for key deltas (APEMO minus baseline) with 95% bootstrap CIs.

<!-- image -->

Figure 4: Trap recovery dynamics in the 𝑛 = 20 LLM trap block, derived from episode-level trap drop/rebound metrics. Left: quality change around trap; right: frustration change around trap. Shaded areas are 95% bootstrap CIs.

<!-- image -->

probability by + 0 . 1257 (95% CI [0.0869, 0.1633], 𝑝 = 5 . 19 × 10 -4 ). Relative to the stronger temporal-flow baseline, quality remains positive ( + 0 . 0609, 95% CI [0.0285, 0.0956], 𝑝 = 0 . 0768) while reuse and reuse-per-cost are near ties (confidence intervals include zero; 𝑝 = 0 . 4545 and 𝑝 = 0 . 8036).

This pattern suggests that temporal-affective scheduling complements structural role decomposition and yields its strongest improvements when no prior temporal control is present.

## 5.6 Plan-Execute-Class Baseline Alignment (L3)

WeexpandedL3toalarger cross-model block with 𝑛 = 30 valid turn2 runs. We compare plan\_execute, plan\_execute\_reflect, task\_peak\_end, and APEMO under the same runtime protocol.

Relative to plan\_execute , APEMO improves quality by + 0 . 1028 (95% CI [0.0843, 0.1218], 𝑝 = 1 . 86 × 10 -9 ), reuse probability by + 0 . 0547 (95% CI [0.0381, 0.0724], 𝑝 = 5 . 95 × 10 -5 ), and reduces frustration by -0 . 0181 (95% CI [-0.0269, -0.0105], 𝑝 = 3 . 25 × 10 -4 ). Reuse-per-cost is also positive ( + 0 . 0027, 95% CI [0.0006, 0.0048], 𝑝 = 0 . 0428).

Relative to plan\_execute\_reflect , APEMO remains positive on quality ( + 0 . 0370, 95% CI [0.0229, 0.0521], 𝑝 = 5 . 95 × 10 -5 ) and reuse probability ( + 0 . 0211, 95% CI [0.0081, 0.0347], 𝑝 = 0 . 0052), while reuse-per-cost and frustration remain near ties (confidence intervals include zero).

To address long-horizon external-baseline alignment directly, we further run an L3 turn-8 cross-model block ( n =18, no\_fallback\_rate = 1.0). Relative to plan\_execute , APEMO improves mean quality by + 0 . 1259 (95% CI [0.0987, 0.1521], 𝑝 = 7 . 63 × 10 -6 ), reuse probability by + 0 . 1223 (95% CI [0.0857, 0.1573], 𝑝 = 1 . 45 × 10 -4 ), and average frustration by -0 . 0193 (95% CI [-0.0295, -0.0100], 𝑝 = 0 . 0013). Relative to plan\_execute\_reflect , quality remains positive at + 0 . 1881 (95% CI [0.1557, 0.2247], 𝑝 = 7 . 63 × 10 -6 ), with reuse and reuse-per-cost also positive.

Against task\_peak\_end at turn-8, APEMO still improves quality ( + 0 . 0988, 95% CI [0.0631, 0.1356], 𝑝 = 1 . 45 × 10 -4 ) and frustration ( -0 . 0096, 95% CI [-0.0159, -0.0047], 𝑝 = 0 . 0075), while reuse and reuse-per-cost become near ties. This reinforces the earlier boundary: L3 supports directional orthogonality, but efficiency deltas remain baseline-sensitive.

Table 3: Coordination frontier summary (large𝑛 blocks). Percentages are relative deltas. All rows use mean-quality gain except Trap Endpoint, which uses endpoint-quality gain by design.

| Setting                             | Quality Gain   | Cost Increase   |
|-------------------------------------|----------------|-----------------|
| Long-Horizon ( 𝑇 = 8, vs peak-end)  | + 14 . 49%     | + 6 . 28%       |
| Long-Horizon ( 𝑇 = 8, vs affect)    | + 42 . 86%     | + 8 . 15%       |
| Short-Horizon ( 𝑇 = 2, vs peak-end) | + 8 . 48%      | + 3 . 39%       |
| Trap Endpoint (vs peak-end)         | + 29 . 90%     | + 5 . 33%       |
| Multi-Agent (vs flow_plain)         | + 41 . 25%     | + 7 . 14%       |

CoordinationFrontier:QualityGainvsCoordinationCost

Cost Increase (%)

Figure 5: Coordination frontier (quality gain vs cost increase). Each point is one large𝑛 block/comparison.

<!-- image -->

## 5.7 Coordination Frontier

We quantify trade-offs between robustness and orchestration cost using relative quality gain and realized total coordination cost increase under shared runtime constraints.

Figure 5 visualizes the frontier: Long-horizon and multi-agent plain-flow settings lie in a high-gain region where relative quality improvements dominate realized coordination cost increases. In short-horizon or strong-temporal baseline settings, gains remain positive but move toward diminishing-return regions.

## 5.8 Scope and Boundary Conditions

Cross-model trap evidence exhibits wider confidence intervals than strict non-trap long-horizon blocks, reflecting perturbation sensitivity and role-level interaction variance. We therefore treat crossmodel trap runs as directional support and use the strict trap block (n=20, no\_fallback\_rate=1.0) as the primary perturbation evidence.

External-orchestrator alignment in this version targets planexecute-class baselines. We now include both a larger short-horizon L3 block (turn-2, n=30) and a long-horizon L3 extension (turn-8, n=18), but we still do not claim full long-horizon parity against industrial workflow generators.

Wetherefore interpret APEMO as a trajectory-level orchestration layer that demonstrates stable benefits in long-horizon and plainflow multi-agent regimes, with explicit boundary conditions in shallow trajectories, strong temporalized baselines, and currently limited long-horizon external-orchestrator comparisons.

## 6 Discussion

## 6.1 Trajectory Alignment as a Control Problem

Most contemporary alignment research treats alignment as a property of model outputs. Reward modeling and RLHF optimize for response-level preference consistency [5, 20], while process supervision approaches refine reasoning traces at the step level [17, 26]. In parallel, HCI scholarship has emphasized trust calibration, transparency, and appropriate reliance in human-automation interaction [1, 14, 21].

Our results suggest a complementary perspective: alignment in long-horizon workflows is not solely a property of outputs or reasoning chains, but of how interactions unfold across time. Psychological evidence demonstrates that retrospective evaluation is temporally asymmetric, disproportionately influenced by salient peaks and endings [9, 12]. Rather than treating this as bias to be corrected, APEMO treats temporal weighting as a predictable structural feature of evaluation.

This motivates a broader theoretical claim: many alignment pipelines implicitly assume a stationary mapping between local behavior and utility across a trajectory. Long-horizon interaction violates this assumption. Evaluation salience shifts over time as user state, task uncertainty, and accumulated context change, so identical local errors can have different global consequences depending on when they occur. In effect, alignment faces a within-trajectory temporal distribution shift, not only a dataset-level shift. Static reward alignment is therefore necessary but incomplete for agentic workflows. Temporal control policies are needed to track and correct this non-stationarity during execution.

Under shared runtime budget caps, reallocating effort across a trajectory can shift evaluation-sensitive outcomes without modifying token-level likelihoods or retraining reward models. In this sense, alignment becomes a runtime control problem over sequences. The locus of intervention moves from parameter space to temporal allocation.

## 6.2 Orthogonality to Structural Agent Design

Recent advances in agentic systems emphasize structural organization: reasoning-acting interleaving [30], planner-executor architectures [31], hierarchical coordination [32], or multi-agent conversational frameworks [11, 28]. These systems restructure *who does what*.

Our findings indicate that temporal scheduling governs a different axis: *when and where compute is invested*. Even when planner-executor-critic roles remain unchanged, trajectory robustness increases under temporal-affective control. This suggests that structural orchestration and temporal allocation address separable dimensions of reliability.

This separation clarifies the engineering contribution of APEMO. It does not compete with topology search or role design; it overlays them. Structural frameworks define workflow composition, while APEMO governs evaluation-sensitive saliency within that composition.

## 6.3 Mechanistic Interpretation and Stability Dynamics

The empirical pattern across ABM, single-agent LLM, and multiagent settings reveals two interacting stability dynamics.

Ending stabilization amplifies perceived reliability. Because final states disproportionately shape retrospective judgment [12], targeted compute allocation near termination improves reuse probability and evaluation outcomes even when intermediate steps are imperfect.

Early instability detection dampens cascading degradation. Behavioral proxies such as repetition similarity and context drift serve as low-cost indicators of trajectory destabilization. Precision repair prevents local perturbations from propagating across turns. This resembles adaptive compute principles in which effort is scaled based on contextual uncertainty rather than held constant [24].

The strength of long-horizon gains is therefore not accidental. In shallow trajectories, coordination overhead cannot amortize. In deeper trajectories, temporal control compounds across turns, producing superlinear robustness effects relative to uniform allocation.

## 6.4 Implications for Human-AI Interaction

Trust in automation is shaped not only by accuracy but by consistency and recovery behavior over time [6, 10, 19]. Systems that degrade unpredictably across extended interactions undermine reliance even if average performance remains high.

APEMO suggests that reliability can be engineered at the temporal level. By shaping peaks and endings, systems can stabilize retrospective trust formation. This reframes alignment as a property of interaction trajectories rather than isolated answers. In longrunning agentic workflows-such as open-ended embodied agents [27] or tool-rich multi-agent systems-trajectory-level control may be critical for sustained collaboration.

## 6.5 Limitations

Several boundaries remain.

Trap-recovery micro-dynamics remain variance-sensitive in certain cross-model regimes. While endpoint stabilization is robust, rebound-style metrics fluctuate with model family and perturbation profile.

Comparisons against external orchestrators are restricted to plan-execute-style baselines rather than complete reproductions of industrial multi-agent systems [23, 31]. The results therefore demonstrate orthogonality rather than superiority over full-stack orchestration pipelines.

Human-subject validation is absent. Although trajectory-level metrics improve consistently, subjective trust, perceived competence, and reuse intent require direct empirical measurement [8, 14].

Finally, real-world deployment may introduce orchestration overhead beyond the controlled cost accounting used here. Continuous monitoring and scheduling logic can introduce latency trade-offs in highly interactive settings. While coordination cost is explicitly modeled within fixed-budget constraints, end-to-end serving impact remains an open systems question [4, 13].

## 6.6 Future Work

A natural extension is empirical validation of trajectory alignment in human studies. Measuring how peak-aware scheduling influences trust calibration, delegation decisions, and long-term collaboration stability would connect runtime control directly to humancentered automation theory [15, 22].

Another direction is adaptive scheduler learning. Rather than fixed thresholds, temporal allocation policies could be meta-learned across tasks and model families. Integrating runtime scheduling with process-supervision or reflective refinement loops [18, 25] may yield systems that both allocate compute strategically and improve reasoning quality.

Finally, extending temporal control to frontier-scale models and longer contexts will clarify scaling properties. Larger models may exhibit intrinsic stabilization mechanisms that interact with scheduling policies. Understanding these interactions will determine whether trajectory alignment remains a critical layer as model capability increases.

## 7 Conclusion

This work introduces a temporal-affective perspective on alignment in long-horizon AI workflows. While prior approaches primarily optimize parameters, reward models, or structural agent topologies, we demonstrate that runtime temporal control constitutes an independent axis of reliability.

APEMO reallocates reasoning effort across interaction trajectories without modifying model weights or expanding model capacity. Through stacked validation across agent-based simulations, singleagent LLM execution, and multi-agent orchestration flows, we show that peak-aware scheduling improves trajectory-level robustness, stabilizes endpoint delivery, and increases reuse probability under shared runtime constraints, while explicitly quantifying realized coordination-cost differences.

These results suggest that alignment is not merely an outcome of model training or interface transparency. It is also shaped by how computation is distributed across time. Treating temporal saliency as a controllable signal opens a complementary pathway for engineering resilient agentic systems.

As AI systems increasingly operate over extended, autonomous workflows, trajectory-level control may become as critical as parameter level alignment. This work positions temporal orchestration as a principled and cost-aware mechanism for sustaining reliability in long-horizon interaction.

## References

- [1] Saleema Amershi, Dan Weld, Mihaela Vorvoreanu, Adam Fourney, Besmira Nushi, Penny Collisson, Jina Suh, Shamsi Iqbal, Paul N Bennett, Kori Inkpen, et al. 2019. Guidelines for human-AI interaction. In Proceedings of the 2019 chi conference on human factors in computing systems . 1-13.
- [2] Gagan Bansal, Tongshuang Wu, Joyce Zhou, Raymond Fok, Besmira Nushi, Ece Kamar, Marco Tulio Ribeiro, and Daniel Weld. 2021. Does the whole exceed its parts? the effect of ai explanations on complementary team performance. In Proceedings of the 2021 CHI conference on human factors in computing systems . 1-16.
- [3] Lukas Beckenbauer, Johannes-Lucas Loewe, Ge Zheng, and Alexandra Brintrup. 2025. Orchestrator: Active Inference for Multi-Agent Systems in Long-Horizon Tasks. arXiv preprint arXiv:2509.05651 (2025).
- [4] Jinyuan Chen, Jiuchen Shi, Quan Chen, and Minyi Guo. 2025. Kairos: Low-latency multi-agent serving with shared llms and excessive loads in the public cloud. arXiv preprint arXiv:2508.06948 (2025).

- [5] Paul F. Christiano, Jan Leike, Tom Brown, Miljan Martic, Shane Legg, and Dario Amodei. 2017. Deep Reinforcement Learning from Human Preferences. In Advances in Neural Information Processing Systems .
- [6] Mary T. Dzindolet, Scott A. Peterson, Rebecca A. Pomranky, Linda G. Pierce, and Heather P. Beck. 2003. The Role of Trust in Automation Reliance. Journal of Experimental Psychology: Applied 9, 2 (2003), 86-99. doi:10.1037/1076-898X.9.2.86
- [7] Joseph Early, Tom Bewley, Christine Evers, and Sarvapali Ramchurn. 2022. Nonmarkovian reward modelling from trajectory labels via interpretable multiple instance learning. Advances in Neural Information Processing Systems 35 (2022), 27652-27663.
- [8] Mica R. Endsley. 1995. Toward a Theory of Situation Awareness in Dynamic Systems. Human Factors 37, 1 (1995), 32-64. doi:10.1518/001872095779049543
- [9] Barbara L. Fredrickson and Daniel Kahneman. 1993. Duration Neglect in Retrospective Evaluations of Affective Episodes. Journal of Personality and Social Psychology 65, 1 (1993), 45-55.
- [10] Peter A Hancock, Theresa T Kessler, Alexandra D Kaplan, Kimberly Stowers, J Christopher Brill, Deborah R Billings, Kristin E Schaefer, and James L Szalma. 2023. How and why humans trust: A meta-analysis and elaborated model. Frontiers in psychology 14 (2023), 1081086.
- [11] Sirui Hong, Mingchen Zhuge, Jonathan Chen, Xiawu Zheng, Yuheng Cheng, Jinlin Wang, Ceyao Zhang, Zili Wang, Steven Ka Shing Yau, Zijuan Lin, et al. 2023. MetaGPT: Meta programming for a multi-agent collaborative framework. In The twelfth international conference on learning representations .
- [12] Daniel Kahneman, Barbara L Fredrickson, Charles A Schreiber, and Donald A Redelmeier. 1993. When more pain is preferred to less: Adding a better end. Psychological science 4, 6 (1993), 401-405.
- [13] Jared Kaplan, Sam McCandlish, Tom Henighan, Tom B Brown, Benjamin Chess, Rewon Child, Scott Gray, Alec Radford, Jeffrey Wu, and Dario Amodei. 2020. Scaling laws for neural language models. arXiv preprint arXiv:2001.08361 (2020).
- [14] John D. Lee and Katrina A. See. 2004. Trust in Automation: Designing for Appropriate Reliance. Human Factors 46, 1 (2004), 50-80. doi:10.1518/hfes.46.1. 50\_30392
- [15] Franziska Legler and Angelika C Bullinger. 2025. How to achieve human-centered automation: the importance of trust for safety-critical behavior and intention to use in human-robot collaboration. Frontiers in Organizational Psychology 3 (2025), 1669782.
- [16] Yang Li, Jiaxiang Liu, Yusong Wang, Yujie Wu, and Mingkun Xu. 2026. BMAM: Brain-inspired Multi-Agent Memory Framework. arXiv preprint arXiv:2601.20465 (2026).
- [17] Hunter Lightman, Vineet Kosaraju, Yuri Burda, Harrison Edwards, Bowen Baker, Teddy Lee, Jan Leike, John Schulman, Ilya Sutskever, and Karl Cobbe. 2023. Let's verify step by step. In The twelfth international conference on learning representations .
- [18] Aman Madaan, Niket Tandon, Prakhar Gupta, Skyler Hallinan, Luyu Gao, Sarah Wiegreffe, Uri Alon, Nouha Dziri, Shrimai Prabhumoye, Yiming Yang, et al. 2023. Self-refine: Iterative refinement with self-feedback. Advances in neural information processing systems 36 (2023), 46534-46594.
- [19] Poornima Madhavan and Douglas A. Wiegmann. 2007. Similarities and Differences Between Human-Human and Human-Automation Trust: An Integrative Review. Theoretical Issues in Ergonomics Science 8, 4 (2007), 277-301. doi:10.1080/14639220500337708
- [20] Long Ouyang, Jeffrey Wu, Xu Jiang, Diogo Almeida, Carroll Wainwright, Pamela Mishkin, Chong Zhang, Sandhini Agarwal, Katarina Slama, Alex Ray, et al. 2022. Training language models to follow instructions with human feedback. Advances in neural information processing systems 35 (2022), 27730-27744.
- [21] Raja Parasuraman and Victor Riley. 1997. Humans and Automation: Use, Misuse, Disuse, and Abuse. Human Factors 39, 2 (1997), 230-253. doi:10.1518/ 001872097778543886
- [22] Raja Parasuraman, Thomas B. Sheridan, and Christopher D. Wickens. 2000. A Model for Types and Levels of Human Interaction with Automation. IEEE Transactions on Systems, Man, and Cybernetics - Part A: Systems and Humans 30, 3 (2000), 286-297. doi:10.1109/3468.844354
- [23] Xinxing Ren, Quagmire Zang, Caelum Forder, Suman Deb, Ahsen Tahir, Roman J Georgio, Peter Carroll, and Zekun Guo. 2026. Beyond Rule-Based Workflows: An Information-Flow-Orchestrated Multi-Agents Paradigm via Agent-to-Agent Communication from CORAL. arXiv preprint arXiv:2601.09883 (2026).
- [24] Tal Schuster, Adam Fisch, Jai Gupta, Mostafa Dehghani, Dara Bahri, Vinh Tran, Yi Tay, and Donald Metzler. 2022. Confident adaptive language modeling. Advances in Neural Information Processing Systems 35 (2022), 17456-17472.
- [25] Noah Shinn et al. 2023. Reflexion: Language Agents with Verbal Reinforcement Learning. In NeurIPS .
- [26] Jonathan Uesato, Nate Kushman, Ramana Kumar, Francis Song, Noah Siegel, Lisa Wang, Antonia Creswell, Geoffrey Irving, and Irina Higgins. 2022. Solving math word problems with process-and outcome-based feedback. arXiv preprint arXiv:2211.14275 (2022).
- [27] Guanzhi Wang, Yuqi Xie, Yunfan Jiang, Ajay Mandlekar, Chaowei Xiao, Yuke Zhu, Linxi Fan, and Anima Anandkumar. 2023. Voyager: An open-ended embodied agent with large language models. arXiv preprint arXiv:2305.16291 (2023).
- [28] Qingyun Wu et al. 2023. AutoGen: Enabling Next-Gen LLM Applications via Multi-Agent Conversation. In The 2023 Conference on Empirical Methods in Natural Language Processing .
- [29] Shunyu Yao, Dian Yu, Jeffrey Zhao, Izhak Shafran, Tom Griffiths, Yuan Cao, and Karthik Narasimhan. 2023. Tree of thoughts: Deliberate problem solving with large language models. Advances in neural information processing systems 36 (2023), 11809-11822.
- [30] Shunyu Yao, Jeffrey Zhao, Dian Yu, Nan Du, Izhak Shafran, Karthik R Narasimhan, and Yuan Cao. 2022. React: Synergizing reasoning and acting in language models. In The eleventh international conference on learning representations .
- [31] Jiayi Zhang, Jinyu Xiang, Zhaoyang Yu, Fengwei Teng, Xionghui Chen, Jiaqi Chen, Mingchen Zhuge, Xin Cheng, Sirui Hong, Jinlin Wang, et al. 2024. Aflow: Automating agentic workflow generation. arXiv preprint arXiv:2410.10762 (2024).
- [32] Wentao Zhang, Liang Zeng, Yuzhen Xiao, Yongcong Li, Yilei Zhao, Ce Cui, Yang Liu, and Bo An. 2025. AgentOrchestra: Orchestrating hierarchical multi-agent intelligence with the Tool-Environment-Agent (TEA) protocol. (2025).