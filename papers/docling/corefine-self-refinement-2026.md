## CONFIDENCE-GUIDED SELF-REFINEMENT

Chen Jin 1 , Ryutaro Tanno 2 , Tom Diethe 1 , Philip Teare 1

1 Centre for AI, AstraZeneca, Cambridge, UK 2 Google DeepMind, UK

## ABSTRACT

Large Language Models (LLMs) often rely on test-time scaling via parallel decoding (e.g., 512 samples) to boost reasoning accuracy, but this incurs substantial compute. We introduce CoRefine , a confidence-guided self-refinement method that achieves competitive accuracy at a fraction of the tokens via a lightweight ∼ 211k -parameter Conv1D controller atop a frozen LLM. The controller consumes full-trace confidence to decide whether to halt, re-examine, or try a different approach-enabling targeted self-correction with an average of ∼ 2 . 7 refinement steps per problem ( ≈ 190 × token reduction) relative to 512-sample baselines. Across diverse reasoning benchmarks and three open-source models, the controller achieves 92.6% precision when it confidently halts, indicating that confidence dynamics reliably signal correctness without ground-truth verification. We extend this to CoRefine-Tree , a hybrid sequential-parallel variant that adaptively balances exploration and exploitation, with easy serving integration and verifier compatibility. By treating confidence as a control signal rather than a correctness guarantee, CoRefine provides a modular primitive for scalable reasoning and agentic settings with imperfect verifiers.

Figure 1: Top: Token efficiency versus accuracy across four reasoning benchmarks: AIME24, AIME25, BRUMO25 and HMMT25. CoRefine achieves competitive or superior accuracy to 512sample or 20-sample majority voting with ∼ 190 × fewer tokens. Wall-clock time versus accuracy showing that token savings translate to actual latency reduction, with CoRefine saving up to 63% over parallel baselines. Bottom: Confidence-Guided Self-Refine overview. The controller consumes full-trace confidence features of the LLM decoded reasoning chain and decides: HALT (accept current answer), RETHINK (verify reasoning), or ALTERNATIVE (explore new approach).

<!-- image -->

1 Email: chen.jin@astrazeneca.com

## 1 INTRODUCTION

Test-time scaling improves LLM reasoning accuracy but incurs substantial compute (Snell et al., 2024; Welleck et al., 2024). Parallel methods like self-consistency (Wang et al., 2023) sample hundreds of reasoning paths and aggregate via majority voting, trading compute for accuracy at proportionally scaling costs. To illustrate: achieving a 14-point accuracy improvement on AIME 2025 (from 68% to 82%) with DeepSeek-8B requires 512 parallel traces per problem, consuming over 100 million additional tokens (Fu et al., 2025).

An alternative paradigm is sequential refinement , where the model iteratively improves its answer based on feedback (Madaan et al., 2023). However, refinement suffers from compounding errorsearly mistakes amplify downstream (Sang, 2025)-and existing approaches face two fundamental challenges: (1) when to stop -models often lack principled criteria for halting refinement, leading to over-iteration or premature termination (Seo et al., 2024); and (2) how to refine -generic 'rethink' prompts may not provide sufficient guidance for targeted improvement. Indeed, recent studies show that refined outputs are not always superior to original versions (Seo et al., 2024), and that LLMs fail to correct errors over half the time without external guidance (Feng et al., 2025).

We propose CoRefine: Confidence-Guided Self-Refinement , which addresses both challenges by using confidence as a control signal . The key insight is that token-level prediction confidence, aggregated across a reasoning trace, provides a rich signal for deciding whether to accept the current answer (HALT), re-examine it (RETHINK), or try a different approach (ALTERNATIVE). This framing treats refinement as an exploration-exploitation tradeoff (Tang et al., 2024), and crucially, uses confidence for adaptive compute allocation rather than as a direct correctness estimate-even imperfectly calibrated confidence can guide useful refinement decisions (Section 2).

CoRefine consists of three components: (1) full-trace confidence extraction from the model's logprobs, (2) a lightweight neural controller ( ∼ 211K parameters) that maps confidence features to refinement decisions, and (3) targeted synthesis prompts that compact previous reasoning into high-signal context for self-correction. The controller is trained via supervised learning on historical trajectories to predict oracle-optimal actions from confidence patterns. We further extend this to CoRefine Tree , a hybrid sequential-parallel variant that combines the token efficiency of sequential refinement with the robustness of parallel sampling.

Our approach offers several advantages:

- Efficiency: CoRefine achieves parity or better accuracy than 512-sample majority voting with an average of ∼ 2.7 iterations, representing a ≈ 190 × reduction in token usage, translating to 63% wall-clock saving (Figure 1).
- Modularity: The controller is a separate, frozen-LLM-compatible module that requires no backbone fine-tuning and integrates seamlessly with existing serving stacks.
- Adaptivity: The controller learns problem-specific stopping criteria-halting early on confident, consistent answers while allowing more exploration on difficult problems.
- Reliability: When the controller confidently decides to halt (majority of traces vote HALT), it achieves 92.6% precision -validating that confidence patterns provide a reliable signal for knowing when the model has found the correct answer.
- Adaptability to regulated domains: By extending to a 4-class controller with REFUSE, CoRefine learns to distinguish genuine uncertainty from post-trained conservative behavior-predicting when encouragement will recover correct answers versus when honest abstention is appropriate (Section 4.9).
- Foundation for agentic systems: By framing confidence as a control signal, CoRefine provides a principled primitive for future multi-agent systems where individual agents may have imperfect verifiers. Recent work on agent debugging shows that systematic learning from failures can improve task success rates by up to 26% (Liang et al., 2025); CoRefine's ALTERNATIVE action provides a mechanism for such recovery.

We evaluate CoRefine across diverse mathematical reasoning benchmarks (AIME 2024/2025, HMMT 2025, BRUMO25) and multiple open-source models (DeepSeek-8B, Qwen3-32B). Results demonstrate that CoRefine consistently matches or outperforms parallel approaches while using orders of magnitude fewer computational resources.

22

Confidence (Logits)

20

18

16

14

12

10

AveragedConfidenceEvolution:CorrectvsIncorrect(DeepSeek-R1-8B)

EARLY（0-30%)

LATE (70-100%)

Incorrect:16.66

Correct:16.61

Correct:17.00

:+0.05

:-1.49

Incorrect:15.51

Dataset:12,060 traces (DeepSeek-R1-8Bv3)

20

40

60

ProgressthroughReasoning(%)

Figure 2: Averaged confidence evolution for correct vs. incorrect reasoning traces. Left: DeepSeekR1-8B (12,060 traces). Right: Qwen3-32B (8,354 traces). Both models show correct traces maintaining higher late-phase confidence, but with distinct dynamics: DeepSeek exhibits increasing confidence for correct traces with a sharp terminal spike, while Qwen3 shows globally descending confidence for both classes.

<!-- image -->

## 2 CONFIDENCE AS A CONTROL SIGNAL

Before describing our method, we establish the empirical foundation: why confidence from tokenlevel logprobs provides useful signal for refinement decisions, and how we can leverage it without assuming it directly estimates correctness.

## 2.1 TOKEN-LEVEL CONFIDENCE EXTRACTION

Given a language model's predicted token distribution P i at position i , we compute token confidence C i as the negative average log-probability of the topk tokens:

<!-- formula-not-decoded -->

where k denotes the number of top tokens considered (we use k = 20 ). High confidence corresponds to peaked distributions and greater model certainty, while low confidence indicates uncertainty in token prediction.

For a complete reasoning trace of N tokens, we aggregate these into a confidence trace c = ( C 1 , C 2 , . . . , C N ) . This full-trace representation captures the model's confidence dynamics throughout the reasoning process.

## 2.2 CONFIDENCE DISTRIBUTIONS: CORRECT VS. INCORRECT

Figure 2 shows confidence distributions for correct and incorrect reasoning traces. Analysis of 12,060 traces (DeepSeek-R1-8B: 8,155 correct, 3,905 incorrect) and 8,354 traces (Qwen3-32B: 6,003 correct, 2,351 incorrect) reveals:

1. Late-phase divergence: Both models show correct traces achieving higher confidence in late phases (70-100%). DeepSeek shows a ∆ =-1.49 gap (17.00 vs 15.51 logits) while Qwen3 shows ∆ =-1.19 (17.01 vs 15.81 logits), with correct traces consistently higher.
2. Early overconfidence paradox: Counterintuitively, incorrect traces start with higher confidence in early phases (0-30%): DeepSeek shows ∆ =+0.05 (16.66 vs 16.61) and Qwen3 shows ∆ =+0.44 (18.31 vs 17.86). This suggests early confidence is misleading.
3. Model-specific dynamics: DeepSeek exhibits increasing confidence for correct traces with a sharp 'hook' spike at 95-100%, while incorrect traces remain flat. Qwen3 shows globally descending confidence for both classes, but with steeper decline for incorrect traces. These distinct patterns motivate learning-based controllers over hand-crafted rules.

Key insight: Control vs. Estimation. These observations suggest that confidence is not a reliable correctness estimator. However, it can still be a useful control signal . Consider an analogy: a

Incorrect(n=3,905)

Correct (n=8,155)

80

100

0

(C) Ours+: CoRefine Tree

<!-- image -->

Figure 3: (a) DeepConf - Parallel: Sample K traces, filter by confidence, aggregate via weighted voting. (b) CoRefine - Sequential: Iteratively refine using controller decisions based on full-trace confidence. (c) CoRefine Tree - Hybrid: Combine parallel sampling with sequential refinement for best of both paradigms.

robot's wheel encoders may not perfectly measure position, but the robot can still use encoder deltas to decide when to turn. Similarly, confidence may not tell us if an answer is correct, but confidence patterns -drops, stability, trends-can inform when to continue refining.

## 2.3 FROM CONFIDENCE TO CONTROL ACTIONS

We define three control actions based on the controller's assessment of the confidence trace:

- HALT : Accept the current answer. Triggered when confidence is stable and answer is consistent across iterations.
- RETHINK : Re-examine the reasoning. Triggered when confidence suggests potential errors but the overall approach seems sound.
- ALTERNATIVE : Try a completely different approach. Triggered when low confidence with inconsistent answers suggests the current path is unproductive.

The controller learns to map confidence features to these actions through supervised learning on trajectories where we know the eventual outcome (correct/incorrect). Importantly, the controller does not predict correctness directly-it predicts which action is most likely to lead to a correct final answer.

## 3 CONFIDENCE-GUIDED SELF-REFINE (COREFINE)

We now present CoRefine, our confidence-guided self-refinement framework. The system consists of three components: confidence feature extraction, a neural controller, and synthesis prompts for refinement.

## 3.1 SYSTEM OVERVIEW

Figure 3 contrasts CoRefine with parallel (DeepConf (Fu et al., 2025)) and hybrid approaches. CoRefine operates through an iterative refinement loop. At each iteration t , the system generates a response y t from the language model while simultaneously extracting the token-level confidence trace c t from the model's logprobs. The final answer a t is then parsed from the response using standard extraction patterns (e.g., \ boxed{} notation for mathematical problems). These signals,

together with the refinement history h t , are transformed into a feature vector ϕ ( c t , h t ) that captures both the current confidence dynamics and the trajectory of previous attempts.

The neural controller π θ consumes this feature vector and outputs a probability distribution over three discrete actions. If the controller selects HALT, the system terminates and returns the current answer a t . Otherwise, the system constructs a synthesis prompt incorporating the action type (RETHINK or ALTERNATIVE) and compacted summaries of previous reasoning attempts, then continues to the next iteration. This loop proceeds until either the controller issues a HALT decision or a maximum iteration budget is exhausted.

## 3.2 CONFIDENCE FEATURE EXTRACTION

Given a confidence trace c = ( C 1 , . . . , C N ) with N tokens, we extract features designed to capture patterns useful for control decisions:

Temporal Downsampling. Long traces (often 5,000+ tokens for mathematical reasoning) are downsampled to a fixed length L using average pooling:

<!-- formula-not-decoded -->

where B j is the j -th bin of tokens. This aggressive downsampling (from N ≈ 5 , 000 -20 , 000 tokens to L = 16 bins) is deliberate: as Figure 2 shows, raw confidence traces exhibit substantial highfrequency noise, with token-level fluctuations obscuring the underlying correctness signal. Early experiments with minimal or no downsampling ( L = 64 , L = 256 , or full-length traces) yielded worse controller accuracy, suggesting the noise overwhelms pattern detection. Average pooling acts as a low-pass filter, smoothing local variations while preserving the macro-level dynamics (early overconfidence, late-phase divergence) that discriminate correct from incorrect traces. The resulting feature vector ϕ t = ¯ c t ∈ R 16 serves as input to the controller. We also explored augmenting this representation with regional statistics and cross-iteration dynamics, but these provided marginal gains ( &lt; 1% accuracy); see Appendix E.

Why Not Text Features? A natural alternative would be to use the actual reasoning text (CoT) as controller input. We deliberately avoid this for two reasons. First, computational cost : processing long reasoning traces (5,000-20,000 tokens) would require a text encoder and likely fine-tuning, negating our goal of a lightweight, modular controller. Second, noisy signal : our early experiments with text-based uncertainty detection proved unreliable-hedging language ('maybe', 'perhaps', 'I think') appeared frequently in correct traces as well as incorrect ones, providing little discriminative power. In contrast, token-level confidence traces offer a compact, semantics-free signal that captures model uncertainty without parsing ambiguous linguistic cues.

## 3.3 NEURAL CONTROLLER ARCHITECTURE

The controller π θ : R 16 → ∆ 3 maps the downsampled confidence trace to a probability distribution over three actions. We employ a one-dimensional convolutional architecture that naturally captures temporal patterns:

<!-- formula-not-decoded -->

The convolutional layers employ kernel sizes [5 , 5 , 3] with stride 2, enabling hierarchical extraction of local confidence fluctuations into progressively abstract representations. We chose Conv1D over fully-connected (MLP) architectures based on empirical comparison: MLPs achieved 78-80% validation accuracy versus Conv1D's 83-84%, likely because Conv1D provides translation invariancethe same confidence pattern (e.g., a mid-trace dip followed by recovery) is detected regardless of its absolute position. This property is crucial since diagnostic patterns can occur at varying points in a reasoning trace. The architecture maintains extreme parameter efficiency at approximately 211K parameters.

Training. We train the controller via supervised learning on historical refinement trajectories. Given a dataset of N train training and N val validation traces from math problems (see Section 4 for model-specific details), each labeled with oracle actions ( x i , { ( y ( i ) t , c ( i ) t , o ( i ) t ) } T i t =1 ) where o ( i ) t ∈ { 0 , 1 , 2 } denotes HALT, RETHINK, or ALTERNATIVE, we minimize cross-entropy loss augmented with a step penalty:

<!-- formula-not-decoded -->

The step cost λ · t (with λ = 0 . 1 ) encourages early halting when appropriate. Training uses Adam optimizer with learning rate 10 -3 , batch size 32, for 30 epochs. The controller converges to 83-84% validation accuracy, demonstrating that raw confidence patterns alone provide sufficient signal for refinement decisions.

Oracle Label Generation. Training data combines traces from two sources: (1) parallel sampling , where multiple independent traces are generated per problem without iteration ( t = 0 ), and (2) sequential refinement , where traces are generated through iterative runs ( t ≥ 0 ). Correct traces receive HALT labels regardless of source. For incorrect traces from sequential runs, we use iteration history: if a subsequent iteration eventually succeeds within the same approach, the label is RETHINK; if success requires a fundamentally different method, the label is ALTERNATIVE. For incorrect traces from parallel runs (no iteration history), we apply confidence-based heuristics: declining trend suggests RETHINK (foundational errors), stable early confidence with late drop suggests RETHINK (calculation errors), and high volatility suggests ALTERNATIVE (unstable approach requiring a fresh start).

Theoretical Justification. A potential concern is circularity: for parallel traces without iteration history, labels are derived from confidence-based heuristics, yet the controller is trained to predict actions from confidence. We provide a Bayesian decision-theoretic analysis in Appendix A showing this does not introduce degeneracy. The key insight is that labels encode correctness (ground-truth verification), not confidence-the heuristics merely approximate the counterfactual 'which action would have succeeded?' for traces where we lack iteration history. Formally, the controller learns P ( a ∗ | c ) where the optimal action a ∗ is defined by correctness outcomes, and confidence c serves as a sufficient statistic for predicting these outcomes. Three factors break potential circularity: (1) correct traces (67-72% of data) receive HALT labels based purely on ground-truth verification; (2) sequential traces provide causal ground truth from actual iteration outcomes; and (3) the heuristics encode domain knowledge about error types (not just pattern matching), serving as an informative prior that the controller refines through supervised learning.

## 3.4 SYNTHESIS PROMPTS FOR REFINEMENT

When the controller selects a refinement action, we construct a synthesis prompt that provides the language model with structured context for its next attempt. The prompt integrates four components: the original problem statement, compacted summaries of previous reasoning attempts, the specific action type (RETHINK or ALTERNATIVE), and aggregate confidence statistics from prior iterations.

Message Compaction. Long-form reasoning traces (often exceeding 10,000 tokens) must be compressed to fit within context limits while preserving actionable information. Our heuristic compaction extracts the final answer (parsed from \ boxed{} notation) and key intermediate steps. This compacted representation typically reduces trace length by 90-95% while retaining the information most relevant for subsequent refinement.

Action-Specific Instructions. The synthesis prompt includes action-specific guidance that shapes the model's refinement strategy. For RETHINK actions, we provide a truncated window of the previous reasoning (approximately 800 tokens from the end) and instruct the model to review its approach, identify weak points, and check for calculation errors or logical gaps-encouraging careful reconsideration rather than wholesale abandonment. For ALTERNATIVE actions, we explicitly direct the model to try a completely different method or problem formulation, signaling that the

```
Algorithm 1: CoRefine: Confidence-Guided Self-Refinement Inputs: Problem x , LLM M , Controller π θ , max iterations T Output: Final answer a history ← [] y 1 , c 1 ←M ( x ) # Generate initial response with logprobs a 1 ← extract_answer ( y 1 ) for t = 1 to T do ϕ t ← extract_features ( c t , history ) action ← arg max π θ ( ϕ t ) # Controller decision if action = HALT then return a t end if summary t ← compact ( y t , a t , confidence_stats ( c t )) history . append ( summary t ) prompt ← synthesis_prompt ( x, history , action ) y t +1 , c t +1 ←M ( prompt ) a t +1 ← extract_answer ( y t +1 ) end for return a T # Return last answer if max iterations reached
```

previous approach may have fundamental issues that cannot be resolved through incremental correction.

Algorithm 1 summarizes the complete CoRefine procedure, showing how confidence extraction, controller decisions, and synthesis prompts integrate into the iterative refinement loop.

## 3.5 COREFINE TREE AND VARIANTS

Our primary results use raw downsampled confidence ( L = 16 ), a 3-layer Conv1D controller, and heuristic message compaction. We also developed CoRefine Tree, a hybrid extension that combines parallel sampling with sequential refinement.

CoRefine Tree (Hybrid). For problems requiring both exploration and refinement, CoRefine Tree, as illustrated in Figure 3, combines parallel sampling with sequential refinement in a tree structure. The method operates in three phases: (1) Warmup : Sample K initial traces in parallel (e.g., K = 4 ), extracting confidence and answers from each. (2) Branching Refinement : Each trace marked for refinement (RETHINK/ALTERNATIVE) spawns multiple children (branch factor B , default B = 2 ), creating a tree where promising directions are explored in parallel. The controller evaluates each new trace, recursively branching until maximum depth or early stopping. (3) Early Stopping : Refinement halts when the cumulative halt rate exceeds 50% (majority of controller decisions say HALT), or when halt rate equals 50% with consistent answers among halted traces. This adaptive stopping prevents unnecessary token expenditure on easy problems while allowing deeper exploration for difficult ones. Final answers are aggregated over halted traces using standard voting methods; CoRefine Tree is compatible with both majority voting and confidence-weighted voting, following the same aggregation strategies as DeepConf. This hybrid approach provides robustness against poor initial samples while maintaining token efficiency through early stopping-typically using 4-12 total traces versus 256-512 for pure parallel methods.

Other Variants. During development, we explored several extensions that provided marginal or no accuracy gains over the base configuration: (1) feature enrichment with regional statistics and cross-iteration dynamics ( &lt; 1% improvement despite doubling model parameters); (2) iteration normalization via z-score adjustment to address iteration-dependent confidence bias (restored refinement behavior but did not improve accuracy); and (3) enhanced message compaction using GPT-

4o-mini for richer information extraction and rule-based hybrid controllers. We retained the simpler base configuration for our primary results. Detailed descriptions and ablation results are provided in Appendix E.

## 4 EXPERIMENTS

We evaluate CoRefine across multiple reasoning benchmarks and backbone models, comparing against both high-budget parallel sampling methods and low-budget ensembles. Our experiments address three main questions: (1) Can sequential refinement with confidence-guided halting match the accuracy of massively parallel sampling? (2) What efficiency gains does adaptive compute allocation provide? (3) Does the controller reliably identify when to stop refining?

## 4.1 EXPERIMENTAL SETUP

Models. We evaluate CoRefine on three open-source reasoning LLMs: DeepSeek-8B (DeepSeekR1-0528-Qwen3-8B), a Qwen3-8B model distilled from DeepSeek-R1 (Guo et al., 2025); Qwen332B (Yang et al., 2025), recognized for strong mathematical reasoning capabilities; and PaCoRe8B (Hu et al., 2026), an 8B reasoning model with post-training confidence calibration that provides better-calibrated logprobs. These models represent different scales, training paradigms, and confidence calibration properties, allowing us to assess CoRefine's generality across backbone architectures.

Benchmarks. We evaluate on four challenging mathematical reasoning datasets widely adopted in recent evaluations of top reasoning LLMs and featured in the MathArena leaderboard (Balunovic et al., 2025): AIME 2024/2025 (American Invitational Mathematics Examination problems (Art of Problem Solving, 2024; 2025)), BRUMO25 (Bulgarian Mathematical Olympiad 2025 (BRUMO, 2025)), and HMMT25 (Harvard-MIT Mathematics Tournament February 2025 (HMMT, 2025)). These benchmarks span a range of difficulty levels, from competition-level problems solvable by strong high school students to olympiad-level challenges requiring sophisticated mathematical insight.

Baselines. We compare against three baseline approaches representing the spectrum of test-time scaling strategies. Pass@1 measures single-trace accuracy without ensembling or refinement, establishing the base model capability. Majority@K implements self-consistency (Wang et al., 2023) with K traces and majority voting, representing the standard parallel sampling approach; we distinguish Maj-P@K (parallel sampling, all traces generated independently) from Maj-S@K (sequential sampling, traces generated one at a time with the same prompt). DeepConf@K applies confidencefiltered majority voting using self-certainty metrics (Fu et al., 2025; Kang et al., 2025), representing state-of-the-art parallel methods that incorporate confidence information.

Controller Training. We train separate controllers for each backbone model using model-specific trajectory datasets. For DeepSeek-8B, we collect 12,060 traces (8,155 correct, 3,905 incorrect) split 70/15/15% for train/val/test. For Qwen3-32B, we use 8,354 traces (5,847 train, 1,252 val, 1,255 test) with 71.9% correct. Traces are collected from a held-out 30% random subset of problems from AIME 2024/2025, BRUMO 2025, and HMMT 2025, with up to 20 refinement iterations per problem. The remaining 70% of problems are reserved for evaluation. Training uses Adam optimizer with learning rate 10 -3 , batch size 32, for 30 epochs. All controllers achieve 83-84% validation accuracy on held-out traces.

Inference Settings. All experiments use temperature 0.7, top-p 0.95, and maximum 64,000 tokens per generation. CoRefine uses a maximum of 20 iterations with early stopping when the controller predicts HALT. All stochastic methods (CoRefine, CoRefine Tree, Majority, DeepConf) are evaluated over 5 independent runs with different random seeds; we report mean accuracy and standard deviation.

## 4.2 MAIN RESULTS

Table 1 presents our main accuracy comparison across all benchmark-model combinations.

Table 1: Benchmarking results. Mean accuracy (%) over 5 runs with standard deviation shown as subscripts. Maj and DC denote Majority and DeepConf; Maj-P denotes parallel sampling, Maj-S denotes sequential sampling. CoRefine averages ∼ 2.7 iterations per problem.

| Model       | Dataset                      | Pass @1                                 | Maj-P @512                              | DC @512                                 | Maj-P @20                               | Maj-S @20                               | DC @20                                  | CoRefine                                | Tree                                    |
|-------------|------------------------------|-----------------------------------------|-----------------------------------------|-----------------------------------------|-----------------------------------------|-----------------------------------------|-----------------------------------------|-----------------------------------------|-----------------------------------------|
| DeepSeek-8B | AIME24 AIME25 BRUMO25 HMMT25 | 82.0 ±1.6 77.4 ±1.3 80.0 ±2.1 58.0 ±1.6 | 85.3 ±1.7 82.0 ±1.6 92.0 ±1.6 69.3 ±1.3 | 92.0 ±1.6 87.4 ±1.3 92.6 ±1.3 82.6 ±1.3 | 90.6 ±1.3 86.7 ±2.1 91.3 ±1.6 72.6 ±2.5 | 90.0 ±2.1 86.0 ±1.4 90.7 ±1.3 72.0 ±2.6 | 91.3 ±1.6 87.4 ±1.3 92.0 ±1.6 82.0 ±1.6 | 90.0 ±2.9 86.7 ±2.1 86.7 ±2.1 81.3 ±3.4 | 90.6 ±2.5 87.3 ±3.3 92.6 ±1.3 82.7 ±2.5 |
| Qwen3-32B   | AIME24 AIME25 BRUMO25 HMMT25 | 80.7 ±4.4 70.7 ±3.3 78.7 ±2.6 52.0 ±2.7 | 85.4 ±2.7 80.7 ±1.3 92.6 ±1.3 63.3 ±2.1 | 89.3 ±1.3 81.3 ±1.6 92.0 ±1.6 62.6 ±1.3 | 86.0 ±1.4 82.6 ±1.3 90.7 ±1.3 61.3 ±1.6 | 85.4 ±2.7 82.0 ±1.6 90.7 ±2.5 60.7 ±1.3 | 90.0 ±2.1 83.3 ±2.1 90.7 ±1.3 62.0 ±1.6 | 89.3 ±2.5 82.7 ±3.3 86.7 ±2.1 64.7 ±1.7 | 90.7 ±2.5 83.3 ±4.2 90.6 ±3.9 66.0 ±1.4 |
| PaCoRe-8B   | AIME24 AIME25 BRUMO25 HMMT25 | 76.7 ±2.1 73.3 ±3.0 80.7 ±1.3 60.0 ±2.1 | 86.0 ±1.4 90.7 ±1.3 90.7 ±1.3 86.7 ±2.1 | 90.7 ±1.3 86.7 ±2.1 92.6 ±1.3 86.7 ±3.7 | 90.0 ±2.1 83.3 ±4.2 90.7 ±1.3 80.7 ±1.3 | 85.4 ±2.7 83.3 ±2.1 90.7 ±2.5 80.7 ±2.5 | 87.4 ±1.3 87.4 ±1.3 90.7 ±1.3 81.3 ±3.4 | 86.7 ±2.1 86.7 ±2.1 86.7 ±3.7 80.7 ±2.5 | 90.7 ±1.3 87.3 ±3.3 92.6 ±1.3 83.3 ±2.1 |

Accuracy Comparison. CoRefine matches or exceeds parallel baselines on 6 of 8 benchmarkmodel combinations. The most notable improvements occur on the challenging HMMT25 benchmark, where CoRefine Tree with DeepSeek-8B achieves 82.7% accuracy compared to Majority@512's 69.3%-a gain of 13.4 percentage points. Similarly, on AIME25 with Qwen3-32B, CoRefine Tree reaches 83.3% versus Majority@512's 80.7%. We note that with 30 problems per benchmark, a single-problem difference corresponds to 3.3%; however, the standard deviations over 5 runs (Table 1) confirm that gains ≥ 6.6% are statistically meaningful, while smaller differences should be interpreted as ties. These gains are particularly significant given that CoRefine uses orders of magnitude fewer computational resources (see Section 4.3).

Efficiency Gains. CoRefine averages approximately 2.7 iterations per problem, representing a ∼ 190 × reduction compared to Majority@512 (2.7 vs. 512 traces) and a ∼ 7 × reduction compared to Majority@20 (2.7 vs. 20 traces). This dramatic efficiency improvement stems from the controller's ability to halt early on confident, consistent answers while allocating additional compute only to problems that require it.

Adaptive Behavior. The controller demonstrates genuine adaptive compute allocation: it halts early on easy problems (1-2 iterations) while permitting extended exploration on difficult ones (5+ iterations) based on detailed analysis in Figure 5. This behaviour emerges naturally from training on oracle labels without explicit difficulty estimation, suggesting that confidence patterns encode problem difficulty implicitly.

## 4.3 TOKEN EFFICIENCY ANALYSIS

Beyond iteration counts, we analyze total token consumption to provide a more complete picture of computational savings. CoRefine achieves 62-286 × token reduction compared to Majority@512 across all settings, with positive accuracy improvements on 6 of 8 configurations (see Table 2). The largest gains occur on HMMT25, where CoRefine improves accuracy by 13-17 percentage points while using only 1/30-1/62 of the tokens. Here we focus on a fairer comparison at matched compute budgets.

Table 2 presents detailed token efficiency analysis. CoRefine achieves 62-286 × token reduction compared to Majority@512 across all settings, with positive accuracy improvements on 6 of 8 configurations. The largest gains occur on HMMT25, where CoRefine improves accuracy by 13-17 percentage points while using only 1/30-1/62 of the tokens. The only accuracy trade-off ( -3.3% to -6.6%) occurs on BRUMO25, which has the highest baseline accuracy (93.3%), suggesting that near-ceiling performance leaves limited room for refinement-based improvement. Figure 10 in Appendix D visualizes this accuracy-efficiency trade-off, confirming that CoRefine and CoRefine Tree consistently occupy the Pareto-optimal region (high accuracy, low tokens) across all benchmarks.

Table 2: Token efficiency comparison vs. high-budget baselines. Tokens ( × 10 7 ) and accuracy (%) at @512 compute budget.

| Model       | Dataset               | Majority@512   | Majority@512   | DeepConf@512                     | DeepConf@512                        | CoRefine                             | CoRefine                              | CoRefine Tree                        | CoRefine Tree                       |
|-------------|-----------------------|----------------|----------------|----------------------------------|-------------------------------------|--------------------------------------|---------------------------------------|--------------------------------------|-------------------------------------|
|             |                       | Token          | Acc            | Token (fold)                     | Acc ( ∆ %)                          | Token (fold)                         | Acc ( ∆ %)                            | Token (fold)                         | Acc ( ∆ %)                          |
| DeepSeek-8B | AIME24 AIME25 BRUMO25 | 35.5 40.1 35.6 | 85.3 82.0 92.0 | 14.5 ↓ 1/2.4 23.7 ↓ 1/1.7 21.7 ↓ | 92.0 ↑ +6.7 87.4 ↑ +5.4 92.6 ↑ +0.6 | 0.38 ↓ 1/93 0.39 ↓ 1/103 0.40 ↓ 1/89 | 90.0 ↑ +4.7 86.7 ↑ +4.7 86.7 ↓ -5.3   | 0.49 ↓ 1/72 0.59 ↓ 1/68 0.44 ↓ 1/81  | 90.6 ↑ +5.3 87.3 ↑ +5.3 92.6 ↑ +0.6 |
|             |                       |                | 80.7           | 1.61 1/15                        | 89.3 ↑ +3.9 81.3 ↑ +0.6             | 0.07 ↓ 1/286 0.19 ↓ 1/128            | 89.3 ↑ +3.9 82.7 ↑ +2.0 86.7 ↓ -5.9 ↑ | 0.14 ↓ 1/143 0.30 ↓ 1/81 0.28 ↓ 1/78 | 90.7 ↑ +5.3 83.3 ↑ +2.6             |
|             |                       |                |                | 1/1.6                            |                                     |                                      |                                       |                                      |                                     |
|             | HMMT25                | 44.9           | 69.3           | 34.3 ↓ 1/1.3                     | 82.6 ↑ +13.3                        | 0.73 ↓ 1/62                          | ↑ +12.0                               | 0.76 ↓ 1/59                          | 82.7 ↑ +13.4                        |
| Qwen3-32B   | AIME24                |                |                |                                  |                                     |                                      | 81.3                                  |                                      |                                     |
|             | AIME25                | 20.0           | 85.4           | 8.8 ↓ 1/2.3 ↓                    |                                     |                                      |                                       |                                      |                                     |
|             | BRUMO25               | 24.3 21.7      | 92.6           | 1.37 ↓ 1/16                      | 92.0 ↓ -0.6                         | 0.18 ↓ 1/121                         |                                       |                                      | 90.6 ↓ -2.0                         |
|             |                       |                |                |                                  |                                     | 0.40 ↓ 1/69                          | 64.7 +1.4                             | 0.60 ↓ 1/46                          | 66.0 ↑ +2.7                         |
|             | HMMT25                | 27.6           | 63.3           | 2.24 ↓ 1/12                      | 62.6 ↓ -0.7                         |                                      |                                       |                                      |                                     |

## 4.4 COMPARISON WITH LOW-BUDGET ENSEMBLES

A natural question is whether CoRefine's efficiency gains persist when compared to more computationally matched baselines. Table 3 compares CoRefine against Majority@20 and DeepConf@20, which use similar token budgets.

Table 3: Token efficiency comparison at similar compute budgets. Tokens ( × 10 7 ) and accuracy (%). CoRefine vs. Majority@20 and DeepConf@20.

| Model       | Dataset                      | Majority@20         | Majority@20         | DeepConf@20                                         | DeepConf@20                                     | CoRefine                                            | CoRefine                                        | CoRefine Tree                                       | CoRefine Tree                                   |
|-------------|------------------------------|---------------------|---------------------|-----------------------------------------------------|-------------------------------------------------|-----------------------------------------------------|-------------------------------------------------|-----------------------------------------------------|-------------------------------------------------|
|             |                              | Token               | Acc                 | Token ( ∆ %)                                        | Acc ( ∆ %)                                      | Token ( ∆ %)                                        | Acc ( ∆ %)                                      | Token ( ∆ %)                                        | Acc ( ∆ %)                                      |
| DeepSeek-8B | AIME24 AIME25 BRUMO25 HMMT25 | 1.85 1.72 1.78 1.74 | 90.6 86.7 91.3 72.6 | 1.19 ↓ -35.7 1.40 ↓ -18.6 1.24 ↓ -30.3 1.55 ↓ -10.9 | 91.3 ↑ +0.7 87.4 ↑ +0.7 92.0 ↑ +0.7 82.0 ↑ +9.4 | 0.38 ↓ -79.5 0.39 ↓ -77.3 0.40 ↓ -77.5 0.73 ↓ -58.0 | 90.0 ↓ -0.6 86.7 +0.0 86.7 ↓ -4.6 81.3 ↑ +8.7   | 0.49 ↓ -73.5 0.59 ↓ -65.7 0.44 ↓ -75.3 0.76 ↓ -56.3 | 90.6 +0.0 87.3 ↑ +0.6 92.6 ↑ +1.3 82.7 ↑ +10.1  |
| Qwen3-32B   | AIME24 AIME25 BRUMO25 HMMT25 | 1.48 1.46 1.27 1.06 | 86.0 82.6 90.7 61.3 | 0.71 ↓ -52.0 0.91 ↓ -37.7 0.77 ↓ -39.4 1.01 ↓ -4.7  | 90.0 ↑ +4.0 83.3 ↑ +0.7 90.7 +0.0 62.0 ↑ +0.7   | 0.07 ↓ -95.3 0.19 ↓ -87.0 0.18 ↓ -85.8 0.40 ↓ -62.3 | 89.3 ↑ +3.3 82.7 ↑ +0.1 86.7 ↓ -4.0 64.7 ↑ +3.4 | 0.14 ↓ -90.5 0.30 ↓ -79.5 0.28 ↓ -78.0 0.60 ↓ -43.4 | 90.7 ↑ +4.7 83.3 ↑ +0.7 90.6 ↓ -0.1 66.0 ↑ +4.7 |
| PaCoRe-8B   | AIME24 AIME25 BRUMO25 HMMT25 | 2.77 3.47 2.62 2.94 | 90.0 83.3 90.7 80.7 | 2.51 ↓ -9.4 2.87 ↓ -17.3 2.40 ↓ -8.4 2.78 ↓ -5.4    | 87.4 ↓ -2.6 87.4 ↑ +4.1 90.7 +0.0 81.3 ↑ +0.6   | 1.74 ↓ -37.2 1.83 ↓ -47.3 1.52 ↓ -42.0 2.00 ↓ -32.0 | 86.7 ↓ -3.3 86.7 ↑ +3.4 86.7 ↓ -4.0 80.7 +0.0   | 0.43 ↓ -84.5 0.58 ↓ -83.3 0.45 ↓ -82.8 0.51 ↓ -82.7 | 90.7 ↑ +0.7 87.3 ↑ +4.0 92.6 ↑ +1.9 83.3 ↑ +2.6 |

Even at comparable compute budgets, CoRefine maintains advantages over low-budget ensembles. On most benchmarks, CoRefine uses 2-12 × fewer tokens than Majority@20 while achieving equal or better accuracy. The benefits are most pronounced on difficult benchmarks: on HMMT25 with DeepSeek-8B, CoRefine achieves +11.7% accuracy improvement over Majority@20 using nearly identical token budgets, while DeepConf@20 achieves only +3.4% despite using 2 × more tokens. This suggests that sequential refinement with confidence-guided halting provides fundamentally different benefits than simply filtering parallel samples by confidence.

## 4.5 LATENCY ANALYSIS

A natural concern is whether token savings translate to actual wall-clock speedup, since sequential refinement incurs per-iteration latency that parallel sampling avoids through batching. Figure 1 shows that CoRefine's token reduction yields 63% wall-clock speedup over Majority@20, because: (1) modern LLM inference is memory-bandwidth bound, so fewer tokens directly reduces time; (2) CoRefine's average 2.7 iterations incur minimal sequential overhead compared to 512-sample parallelism, which requires batching infrastructure and aggregation; and (3) the controller's lightweight inference ( ∼ 211K parameters) adds negligible latency ( &lt; 1ms per decision). Detailed wall-clock benchmarks across hardware configurations are provided in Table 4, which reports the same lowbudget comparison as Table 3, but using wall-clock time (hours) instead of token count. This highlights that the efficiency gains translate to latency savings at matched compute budgets.

Table 4: Latency comparison at similar compute budgets. Time (hours) and accuracy (%). CoRefine vs. Majority@20 and DeepConf@20.

| Model       | Dataset   | Majority@20   | Majority@20   | DeepConf@20                             | DeepConf@20             | CoRefine                                 | CoRefine                          | CoRefine Tree                          | CoRefine Tree                     |
|-------------|-----------|---------------|---------------|-----------------------------------------|-------------------------|------------------------------------------|-----------------------------------|----------------------------------------|-----------------------------------|
|             |           | Time (hrs)    | Acc           | Time ( ∆ %)                             | Acc ( ∆ %)              | Time ( ∆ %)                              | Acc ( ∆ %)                        | Time ( ∆ %)                            | Acc ( ∆ %)                        |
| DeepSeek-8B | AIME24    | 11.85 14.72   | 90.6 86.7     | 7.19 ↓ -39.3 11.40 ↓ -22.6 9.24 ↓ -27.7 | 91.3 ↑ +0.7 87.4 ↑ +0.7 | 10.38 ↓ -12.4 8.39 ↓ -43.0 10.40 ↓ -18.6 | 90.0 ↓ -0.6 86.7 +0.0 86.7 ↓ -4.6 | 4.65 ↓ -60.8 5.73 ↓ -61.1 4.68 ↓ -63.4 | 90.6 +0.0 87.3 ↑ +0.6 92.6 ↑ +1.3 |
|             | AIME25    |               |               |                                         |                         |                                          |                                   |                                        |                                   |
|             | BRUMO25   | 12.78         | 91.3          |                                         | 92.0 ↑ +0.7             |                                          |                                   |                                        |                                   |
|             | HMMT25    | 13.74         | 72.6          | 11.55 ↓ -15.9                           | 82.0 ↑ +9.4             | 8.73 ↓ -36.5                             | 81.3 ↑ +8.7                       | 7.33 ↓ -46.7                           | 82.7 ↑ +10.1                      |
| Qwen3-32B   | AIME24    | 7.30          | 86.0          | 12.02 ↑ +64.7                           | 90.0 ↑ +4.0             | 10.07 ↑ +37.9                            | 89.3 ↑ +3.3                       | 6.14 ↓ -15.9                           | 90.7 ↑ +4.7                       |
| Qwen3-32B   | AIME25    | 21.46         | 82.6          | 17.19 ↓ -19.9                           | 83.3 ↑ +0.7             | 12.19 ↓ -43.2                            | 82.7 ↑ +0.1                       | 8.30 ↓ -61.3                           | 83.3 ↑ +0.7                       |
| Qwen3-32B   | BRUMO25   | 11.27         | 90.7          | 13.81 ↑ +22.5                           | 90.7 +0.0               | 9.18 ↓ -18.5                             | 86.7 ↓ -4.0                       | 7.28 ↓ -35.4                           | 90.6 ↓ -0.1                       |
| Qwen3-32B   | HMMT25    | 15.06         | 61.3          | 19.49 ↑ +29.4                           | 62.0 ↑ +0.7             | 20.40 ↑ +35.5                            | 64.7 ↑ +3.4                       | 9.60 ↓ -36.3                           | 66.0 ↑ +4.7                       |

## 4.6 CONTROLLER BEHAVIOR ANALYSIS

To validate that the controller makes reliable halting decisions, we analyze its behavior using CoRefine Tree (warmup=4, branch factor=2, max depth=3) on DeepSeek-8B across 120 problems. Table 5 presents early stopping and controller precision statistics.

Table 5: CoRefine Tree early stopping and controller precision analysis. Early Stop Rate : Fraction of problems where the controller triggered early termination before exhausting the maximum tree depth. Early Stop Acc : Accuracy on problems that were early-stopped, measuring whether the controller correctly identifies solvable problems. Halt Precision ( ≥ 50%) : For problems where the majority of tree nodes voted HALT (halt rate ≥ 50%), the fraction that were answered correctly-this measures the controller's reliability when it confidently decides to stop exploring.

| Benchmark   | Early Stop Rate   |   Early Stop Acc (%) | High-Halt Problems   |   Halt Precision (%) |
|-------------|-------------------|----------------------|----------------------|----------------------|
| AIME24      | 29/30 (96.7%)     |                 89.7 | 25/30                |                100   |
| AIME25      | 28/30 (93.3%)     |                 89.3 | 24/30                |                 95.8 |
| BRUMO25     | 28/30 (93.3%)     |                 85.7 | 25/30                |                 92   |
| HMMT25      | 26/30 (86.7%)     |                 69.2 | 20/30                |                 80   |
| Overall     | 111/120 (92.5%)   |                 83.8 | 94/120               |                 92.6 |

Early Stopping Effectiveness. The controller achieves a 92.5% early stopping rate (111/120 problems), meaning that for the vast majority of problems, the system confidently terminates before exhausting the full tree exploration budget. Critically, early-stopped problems achieve 83.8% accuracy, demonstrating that the controller accurately identifies when further exploration is unnecessary.

Halt Precision. The most striking result is the 92.6% halt precision on high-confidence problems. We define a problem as 'high-halt' when ≥ 50% of tree nodes vote HALT (i.e., the controller's majority decision is to stop). On these 94 problems where the controller is confident enough to halt by majority consensus, 87 are answered correctly. This validates our central thesis: confidence patterns provide a reliable control signal for knowing when the model has found the right answer-even without ground-truth verification.

Exemplary Case Study. Figure 4 illustrates CoRefine Tree's decision-making on a challenging HMMT2025 problem. The tree explores 15 nodes (3 warmup + 4 depth-1 + 8 depth-2) and demonstrates perfect controller discrimination : the controller HALTs only on the single node producing the correct answer (2304, with confidence p = 0 . 74 ), while correctly issuing RETHINK or ALTERNATIVE on all 14 nodes with incorrect answers (40, 20, etc.). Critically, the controller achieves zero false HALTs -it never prematurely stops on an incorrect solution. This behavior exemplifies the 'safety-first' property we observe across all benchmarks: the controller's conservatism manifests as occasional over-refinement of correct answers (harmless), never as premature acceptance of wrong answers (catastrophic). Additional case studies in Appendix F show this pattern holds even on problems where the controller is more conservative.

Controller Action Distribution. Across all 636 controller decisions in the tree: HALT accounts for 45.3%, ALTERNATIVE for 34.6%, and RETHINK for 20.1%. The high ALTERNATIVE rate

Figure 4: CoRefine Tree visualization on HMMT 2025 Q13 (Sophie's coordinate grid paths). Each node shows the model's answer and confidence; edge colors indicate controller decisions (green=HALT, red=RETHINK, orange=ALTERNATIVE). The controller achieves 100%precision : it HALTs only on the correct answer (2304) while correctly refining all 14 incorrect answers. This 'zero false HALT' property-never stopping on wrong answers-is the controller's most critical safety guarantee.

<!-- image -->

reflects the branching paradigm where the controller frequently explores diverse reasoning paths in parallel rather than iteratively refining a single trace. The relatively lower RETHINK rate suggests that when the controller detects issues, it more often recommends exploring fundamentally different approaches rather than incremental corrections. Figure 5 visualizes this distribution across benchmarks.

Tree Efficiency Metrics. Figure 6 shows the average nodes explored, tree depth, and early stopping rate per benchmark. The controller explores an average of 5-7 nodes per problem (vs. 60 maximum possible with warmup=4, branch=2, depth=3), achieving 87-97% early stopping rates. Tree depth averages 0.4-0.7, indicating most problems are solved within the warmup phase or one level of branching.

## 4.7 ABLATION STUDIES

We conducted extensive ablations on feature representation, controller architecture, and halting strategies; full results are provided in Appendix E. Key findings include: (1) Feature ablations: Raw downsampled confidence ( L = 16 ) achieves 83.2% controller validation accuracy; adding regional statistics and cross-iteration dynamics provides marginal gains ( &lt; 1%) while increasing parameters by 30%, confirming that raw confidence captures sufficient signal. (2) Controller architec-

Figure 5: Controller action distribution across benchmarks. The controller balances HALT (green), RETHINK (red), and ALTERNATIVE (orange) decisions based on confidence patterns. HALT rates are highest on BRUMO25 and AIME24 (66% and 64%), reflecting these benchmarks' higher tractability. HMMT25 shows the lowest HALT rate (48%) and highest RETHINK rate (37%), indicating the controller appropriately allocates more exploration effort to harder problems.

<!-- image -->

## CoRefineTreeEfficiencyMetrics-DeepSeek-8B

Figure 6: CoRefine Tree efficiency metrics across benchmarks. Left: Average nodes explored per problem (5-7 out of 60 maximum). Middle: Average tree depth (0.4-0.7 out of 3 maximum). Right: Early stopping rate (87-97%). Together, these metrics demonstrate that the controller effectively prunes the search space, using only ∼ 10% of the maximum possible nodes while maintaining high accuracy.

<!-- image -->

ture: Conv1D outperforms MLP by 3-5% due to translation-invariant pattern detection-the same confidence signature (e.g., mid-trace dip followed by recovery) is detected regardless of absolute position. (3) Halting strategy: Iteration normalization via z-score adjustment reduces average iterations by 2-4 × but does not improve accuracy, suggesting the base configuration already achieves an effective accuracy-efficiency trade-off. Based on these findings, we recommend the simplest configuration: raw confidence features, Conv1D controller ( ∼ 211K parameters), and heuristic message compaction.

## 4.8 CROSS-TASK GENERALIZATION

A key question for practical deployment is whether the controller generalizes across mathematical domains-can a controller trained on one competition task (e.g., AIME) transfer to others (e.g., HMMT, BRUMO)? To investigate, we trained four task-specific controllers on 228 samples each (undersampled for balance) and evaluated each on all four benchmarks.

Figure 7 shows the results. Controllers achieve 95.4% in-task accuracy (diagonal) versus 94.6% out-of-task accuracy (off-diagonal), yielding a generalization gap of only 0.8% . This remarkable transferability suggests that confidence patterns learned by the controller-such as late-phase divergence between correct and incorrect traces, mid-trace dips indicating reasoning uncertainty, and confidence plateaus signaling stagnation-are task-agnostic properties of the underlying language model rather than task-specific artifacts. Practically, this means a single controller trained on any mathematical reasoning task can be deployed across diverse benchmarks without task-specific retraining, supporting the modularity claims of our approach. Full experimental details including data generation, training configuration, and per-cell accuracy values are provided in Appendix C.3.

ActionAccuracy(%)

Figure 7: Cross-task generalization matrix. Action prediction accuracy (%) when controllers trained on one task (rows) are evaluated on all tasks (columns). The near-uniform high accuracy across the matrix demonstrates that confidence patterns are task-agnostic: a controller trained on any single benchmark generalizes effectively to others with minimal degradation.

<!-- image -->

## 4.9 EXTENSION: ADAPTING TO REGULATED DOMAINS WITH REFUSAL

We evaluate CoRefine's adaptability to regulated domains using BixBench (Sasse et al., 2025), a bioinformatics benchmark of 205 multiple-choice questions. 1 This setting presents a dual out-ofdistribution challenge: (1) knowledge domain shift from mathematics to biology, and (2) behavioral shift from mandatory answering to selective refusal. The latter is particularly relevant for regulated applications where models trained for safety must learn when to abstain from uncertain predictions.

Motivation. Pre-trained models fine-tuned for regulated domains often exhibit conservative behavior, refusing to answer when uncertain. However, existing refinement frameworks lack explicit mechanisms to decide when refusal is appropriate versus when additional reasoning could resolve uncertainty. This gap is critical for cost-effective adaptation of large models to specialized domains-rather than expensive full fine-tuning, can a lightweight controller learn when to push for an answer versus when to accept refusal?

4-Class Controller Extension. We extend CoRefine to a 4-action framework: HALT (accept correct answer), RETHINK (re-examine with same approach), ALTERNATIVE (try different strategy), and REFUSE (accept model abstention). Training data consists of 6,560 confidence traces (205 questions × 32 samples) collected from Qwen3-32B on MCQ tasks with 'Insufficient information' as the 5th choice. Oracle labels are derived from correctness: traces yielding correct answers receive HALT labels, while incorrect refusals are labeled RETHINK/ALTERNATIVE based on confidence patterns, and genuine uncertainty receives REFUSE labels.

Two-Phase Prompting Strategy. To preserve training distribution fidelity, we employ NEUTRAL prompts at Iteration 0 (matching training data collection) followed by AGGRESSIVE prompts at refinement iterations that exclude the refusal option and explicitly demand commitment. This ap-

1 Our evaluation differs from the original BixBench paper, which uses an agent-based pipeline where LLMs first generate analysis notebooks from datasets, then answer MCQs conditioned on their generated analysis. We instead perform direct MCQ evaluation without agent-generated context, testing models' inherent bioinformatics knowledge. This methodological difference explains the lower baseline accuracies we observe compared to Sasse et al. (2025); see Appendix G.2 for details.

proach prevents infinite refusal loops while allowing the controller to decide when initial uncertainty warrants additional reasoning.

Experimental Setup. We evaluate CoRefine on BixBench using DeepSeek-8B and Qwen3-32B across two task configurations: (1) standard 4-choice MCQ where models must select one answer, and (2) MCQ with refusal, adding 'Insufficient information' as a 5th option. Baselines include Majority@32 (self-consistency with 32 samples), DeepConf@32 (confidence-filtered voting), and DC@32+Threshold (DeepConf with naive confidence thresholding that excludes low-confidence traces below a model-specific threshold; see Appendix G.9). The 4-class controller was trained on 6,560 traces (4,590 train / 982 val / 988 test) with 76.8% validation accuracy.

Results. Figure 8 presents our findings. Baseline methods reveal severe over-refusal: accuracy drops from 38.5% (standard MCQ) to 3.4% when the refusal option is available for Qwen3-32B, indicating models default to abstention rather than reasoning through uncertainty. Notably, naive confidence thresholding (DC+Thresh) fails to improve over vanilla DeepConf-and actually degrades performance-because models exhibit higher confidence when refusing than when answering correctly (analysis in Appendix G.9). In contrast, CoRefine's learned controller improves accuracy from 3.4% to 16.3% (Qwen3-32B) and 23.4% (DeepSeek-8B), demonstrating that distinguishing recoverable from genuine uncertainty requires pattern recognition beyond simple thresholding. Full implementation details appear in Appendix G.

## BixBench MCQ Results: Impact of Refusal Option and CoRefine Recovery

<!-- image -->

Figure 8: BixBench MCQ results. Accuracy (%) for standard MCQ and MCQ with refusal option across DeepSeek-8B (blue) and Qwen3-32B (orange). Annotations highlight key observations: 1 ⃝ DeepConf improves over Majority voting; 2 ⃝ Adding the refusal option causes dramatic accuracy collapse; 3 ⃝ Naive confidence thresholding fails; 4 ⃝ CoRefine significantly recovers accuracy; 5 ⃝ CoRefine Tree achieves the best results. Horizontal lines indicate random baselines (25% for standard MCQ, 20% for MCQ with refusal).

Exemplary Case Study: Distinguishing Genuine vs. Post-Trained Uncertainty. Figure 9 illustrates the 4-class controller's key capability: distinguishing between genuine uncertainty (warranting REFUSE) and over-trained conservative behavior that can be overcome with encouragement. On this BCG vaccine odds ratio question, the warmup phase produces 4 traces-3 selecting 'Unsure' (choice A) and 1 selecting E-all receiving RETHINK actions. Despite initial refusals, the controller recognizes confidence patterns indicating recoverable uncertainty rather than irreducible knowledge gaps. After aggressive refinement prompting (removing the refusal option), 4 of 8 depth1 nodes achieve HALT on substantive answers, with majority voting correctly selecting D. This demonstrates the controller's ability to predict when encouragement will succeed : the same model that defaulted to 3.4% accuracy under passive prompting can recover correct answers when the controller identifies that refusal stems from post-trained conservatism rather than genuine knowledge limitations.

Q3:Using.anordinallogisticregressionmodel(orderedlogit),whatistheoddsratioassociatedwith healthcareworkershavingreceivedaBCGvaccineforhigherCoviD-19severity?

<!-- image -->

Options:E=GT|A=Unsure|5 total choices

Figure 9: CoRefine Tree on BixBench Q3 (BCG vaccine odds ratio, ground truth: D after excluding Unsure=A). The 4-class controller distinguishes over-trained refusal from genuine uncertainty. Warmup: 4 traces (3 select 'Unsure', 1 selects E) all receive RETHINK (red). After aggressive refinement: 4/8 nodes HALT (green) on substantive answers (B, C, D, D), with D winning majority vote. Node colors: green=HALT, red=RETHINK, orange=ALTERNATIVE. Additional BixBench case studies appear in Appendix G.10.

## 5 DISCUSSION

Our results show that confidence-guided sequential refinement can match or exceed parallel sampling on our benchmarks while using substantially fewer computational resources. We attribute this to: (1) targeted correction -refinement can build on prior attempts to fix specific mistakes rather than restarting from scratch; (2) confidence-guided compute allocation -the controller halts early on easy problems and allocates more refinement to hard ones, unlike fixed-budget parallel sampling; and (3) contextual synthesis -synthesis prompts provide explicit feedback from previous attempts, enabling more informed refinement.

Several limitations suggest directions for future work. We treat confidence as a control signal rather than a calibrated correctness estimate; better calibration may improve robustness. Training relies on oracle-labeled trajectories; reducing labeling cost (e.g., via weaker supervision) would improve scalability. Our evaluation focuses on mathematical reasoning; extending to other domains (e.g., code and scientific reasoning) is important, especially in settings where strict first-token latency or heavy batching changes the latency/throughput trade-off (Section 4.5). Finally, the controller can still make sub-optimal decisions (e.g., unnecessary refinement or missed HALTs); we analyze these behaviors in Appendix F and Appendix G.10.

## 6 RELATED WORK

Recent work has explored test-time scaling through parallel sampling (Wang et al., 2023; Brown et al., 2024), tree search (Wu et al., 2024), and extended chain-of-thought (Wei et al., 2022; Guo et al., 2025); CoRefine offers an orthogonal approach via sequential refinement with learned halting. Self-refinement methods prompt models to critique and improve their outputs (Madaan et al., 2023),

but unlike prior work that uses fixed iteration counts or heuristic stopping criteria, CoRefine learns when and how to refine based on confidence signals. Prior work has used confidence for selective prediction (Ren et al., 2023), output ranking (Jain et al., 2024), and trace filtering (Fu et al., 2025; Kang et al., 2025); CoRefine uniquely uses confidence as a control signal for refinement decisions rather than for filtering or voting. Finally, early halting and adaptive depth have been explored in various architectures (Graves, 2016), and CoRefine extends this principle to LLM inference through a learned controller. We provide an extended discussion of related work in Appendix I.

## 7 CONCLUSION

We present CoRefine, a confidence-guided self-refinement framework that achieves state-of-the-art efficiency in test-time scaling for LLM reasoning. By treating confidence as a control signal rather than a correctness estimate, CoRefine learns to make adaptive refinement decisions that match or exceed parallel sampling approaches with orders of magnitude fewer resources.

Our key contributions include:

1. A principled framework for confidence-guided refinement with three distinct actions (HALT, RETHINK, ALTERNATIVE)
2. A lightweight ( ∼ 211K parameter) Conv1D controller that learns temporal patterns in confidence traces
3. Extensive empirical validation showing ∼ 190 × efficiency gains with competitive accuracy
4. High-precision halting: When the controller confidently decides to stop (majority vote HALT), it achieves 92.6% precision across 94 high-confidence problems, demonstrating that confidence patterns reliably indicate correct answers without ground-truth verification
5. A foundation for future agentic systems that require adaptive compute allocation

We believe CoRefine represents a promising direction for practical LLM deployment, where computational efficiency is as important as accuracy. By providing a modular, trainable control layer for test-time compute, CoRefine enables flexible trade-offs between accuracy and efficiency that can be tailored to specific deployment constraints.

## REFERENCES

- Pranjal Aggarwal, Aman Madaan, Yiming Yang, and Mausam. Let's sample step by step: Adaptiveconsistency for efficient reasoning and coding with llms. arXiv preprint arXiv:2305.11860 , 2023.
- Mohammad Aghajani Asl et al. FAIR-RAG: Faithful adaptive iterative refinement for retrievalaugmented generation. arXiv preprint , 2025.
- Anthropic. Effective context engineering for AI agents. https://www.anthropic.com/ engineering/effective-context-engineering-for-ai-agents , 2025.
- Art of Problem Solving. 2024 aime i problems. https://artofproblemsolving.com/wiki/ index.php/2024\_AIME\_I , 2024.
- Art of Problem Solving. 2025 aime i problems. https://artofproblemsolving.com/wiki/ index.php/2025\_AIME\_I , 2025.
- Mislav Balunovic et al. Matharena: A comprehensive benchmark for math reasoning with llms. https://matharena.ai , 2025.
- Bradley Brown, Jordan Juravsky, Ryan Ehrlich, Ronald Clark, Quoc V Le, Christopher Ré, and Azalia Mirhoseini. Large language monkeys: Scaling inference compute with repeated sampling. arXiv preprint arXiv:2407.21787 , 2024.
- BRUMO. Bulgarian mathematical olympiad 2025. https://brumo.org , 2025.
- Jason Cai et al. CERET: Cost-effective extrinsic refinement for text generation. In NAACL , 2024.
- Jiuhai Chen, Lianmin Yu, Mingyi Chen, and Eric P Xing. Do not trust your llm's reasoning: When llms contradict themselves in complex reasoning. arXiv preprint arXiv:2311.09547 , 2024.
- Jonathan Chuang et al. Learning to generate better than your llm. arXiv preprint , 2025.
- Ekaterina Fadeeva, Roman Vashurin, Akim Tsvigun, Roman Fishchenko, Sergey Petrakov, Artem Vazhentsev, Maxim Panov, Alexander Panchenko, and Artem Shelmanov. Fact-checking the output of large language models via token-level uncertainty quantification. arXiv preprint arXiv:2403.04696 , 2024.
- Yiyang Feng et al. Unraveling misinformation propagation in LLM reasoning. arXiv preprint , 2025.
- Daniel Y Fu, Tri Dao, Khaled K Saab, Armin W Thomas, Atri Rudra, and Christopher Ré. Efficiently scaling transformer inference. arXiv preprint arXiv:2211.05102 , 2024.
- Yichao Fu, Xuewei Wang, Yuandong Tian, and Jiawei Zhao. Deep think with confidence. arXiv preprint arXiv:2508.15260 , 2025.
- Alex Graves. Adaptive computation time for recurrent neural networks. arXiv preprint arXiv:1603.08983 , 2016.
- Daya Guo, Dejian Yang, He Zhang, Junxiao Song, Runxin Zhang, Ruoyu Xu, Qihao Zhu, Shirong Ma, Peiyi Wang, Xiao Bi, et al. Deepseek-r1: Incentivizing reasoning capability in llms via reinforcement learning. arXiv preprint arXiv:2501.12948 , 2025.
- HMMT. Harvard-mit mathematics tournament february 2025. https://hmmt.org , 2025.
- Bairu Hou et al. Thinkprune: Pruning long chain-of-thought reasoning with adaptive pruning. arXiv preprint , 2025.
- Jingcheng Hu, Yinmin Zhang, Shijie Shang, Xiaobo Yang, Yue Peng, Zhewei Huang, Hebin Zhou, Xin Wu, Jie Cheng, Fanqi Wan, et al. Pacore: Learning to scale test-time compute with parallel coordinated reasoning. arXiv preprint arXiv:2601.05593 , 2026.
- Robert Irvine, Douglas Boubert, Yong Liang Mber, Yury Starosielec, Daniel Sherman, Daniele Patel, Ari Malik, Tyler Dunn, Ehud Aharoni, Hao Le, et al. Rewarding chatbots for real-world engagement with millions of users. arXiv preprint arXiv:2303.06135 , 2023.

- Aaron Jaech et al. Openai o1 system card. arXiv preprint arXiv:2412.16720 , 2024.
- Siddhartha Jain, Xiaofei Ma, Anoop Deoras, and Bing Xiang. Lightweight reranking for language model generations. In Proceedings of the 62nd Annual Meeting of the Association for Computational Linguistics , 2024.
- Hyeonseok Jang et al. Confidence-guided refinement reasoning for zero-shot question answering. arXiv preprint arXiv:2509.20750 , 2025.
- Moonkyung Kang et al. Scalable best-of-n selection for large language models via self-certainty. arXiv preprint arXiv:2502.18581 , 2025.
- Yiwei Li, Peiwen Lin, Yujiu Li, et al. Escape sky-high cost: Early-stopping self-consistency for multi-step reasoning. arXiv preprint arXiv:2401.10480 , 2024.
- Churong Liang et al. Where LLM agents fail and how they can learn from failures. alphaXiv , 2025.
- Tsung-Yi Lin, Priya Goyal, Ross Girshick, Kaiming He, and Piotr Dollár. Focal loss for dense object detection. In Proceedings of the IEEE International Conference on Computer Vision , pp. 2980-2988, 2017.
- Qingyu Luo, Junxiang Zhang, Zhuofei Fu, Zhongyu Wang, Xiao Chen, et al. O1 replication journey: A strategic progress report. arXiv preprint arXiv:2501.02644 , 2025.
- Aman Madaan, Niket Tandon, Prakhar Gupta, Skyler Hallinan, Luyu Gao, Sarah Wiegreffe, Uri Alon, Nouha Dziri, Shrimai Prabhumoye, Yiming Yang, et al. Self-refine: Iterative refinement with self-feedback. Advances in Neural Information Processing Systems , 36, 2023.
- Tianlong Ni et al. Reasoning with confidence: Efficient verification of LLM reasoning steps via uncertainty heads. arXiv preprint arXiv:2511.06209 , 2025.
- Shuaijie Qiao et al. ConCISE: Confidence-guided compression in step-by-step efficient reasoning. arXiv preprint arXiv:2505.04881 , 2025.
- Yuxi Ren, Zhaopeng Zhang, Qianyin Liu, Jianguo Chen, and Helen Meng. Self-evaluation guided beam search for reasoning. Advances in Neural Information Processing Systems , 36, 2023.
- Yinghao Sang. AutoCrit: A meta-reasoning framework for self-critique and iterative error correction in LLM chains-of-thought. Preprints.org , 2025.
- Alexander Sasse et al. BixBench: A comprehensive benchmark for LLM-based agents in computational biology. arXiv preprint arXiv:2503.00096 , 2025.
- Minju Seo et al. Rethinking code refinement: Learning to judge code efficiency. In EMNLP , 2024.
- Kumar Shridhar et al. The ART of LLM refinement: Ask, refine, and trust. arXiv preprint arXiv:2311.07961 , 2023.
- Charlie Snell, Jaehoon Lee, Kelvin Xu, and Aviral Kumar. Scaling llm test-time compute optimally can be more effective than scaling model parameters. arXiv preprint arXiv:2408.03314 , 2024.
- ,
- Hao Tang et al. Code repair with LLMs gives an exploration-exploitation tradeoff. In NeurIPS 2024.
- Alon Taubenfeld et al. Confidence improves self-consistency in LLMs. arXiv preprint arXiv:2502.06233 , 2025.
- Kimi Team. Kimi k1.5: Scaling reinforcement learning with llms. arXiv preprint arXiv:2501.12599 , 2025.
- Adithya Thatipalli. Context engineering is the #1 skill in 2025. Medium , 2025.
- Xuezhi Wang, Jason Wei, Dale Schuurmans, Quoc V Le, Ed H Chi, Sharan Narang, Aakanksha Chowdhery, and Denny Zhou. Self-consistency improves chain of thought reasoning in language models. arXiv preprint arXiv:2203.11171 , 2023.

- Jason Wei, Xuezhi Wang, Dale Schuurmans, Maarten Bosma, Fei Xia, Ed Chi, Quoc V Le, and Denny Zhou. Chain-of-thought prompting elicits reasoning in large language models. Advances in Neural Information Processing Systems , 35:24824-24837, 2022.
- Sean Welleck, Amanda Bertsch, Matthew Finlayson, Hailey Sclar, Alex Xie, Graham Neubig, Ilia Kulikov, and Zaid Harchaoui. From decoding to meta-generation: Inference-time algorithms for large language models. arXiv preprint arXiv:2406.16838 , 2024.
- Yangzhen Wu, Zhiqing Sun, Shanda Yuan, Yiming Yin, Jian Shao, Yueting Zhuang, Hang Li, Tong Xiao, and Jingbo Zhu. Inference scaling laws: An empirical analysis of compute-optimal inference for problem-solving with language models. arXiv preprint arXiv:2408.00724 , 2024.
- xAI. Grok-4 technical report. Technical Report , 2025.
- Fuxiao Xue et al. Adaptive-consistency: Dynamic self-consistency for improved reasoning quality. arXiv preprint arXiv:2311.01727 , 2023.
- Aiyuan Yang, An Yang, Baosong Liu, et al. Qwen3 technical report. arXiv preprint arXiv:2505.09388 , 2025.
- Chiyuan Zhang, Samy Bengio, Moritz Hardt, Benjamin Recht, and Oriol Vinyals. Understanding deep learning (still) requires rethinking generalization. In Communications of the ACM , volume 64, pp. 107-115, 2021.
- Zhenkai Zhao et al. Learning to abstain: Selective prediction with learned thresholds for large language models. arXiv preprint , 2025.
- Jiaming Zhu et al. Path-consistency: Prefix enhancement for efficient inference in LLM. arXiv preprint arXiv:2409.01281 , 2024.

## A BAYESIAN JUSTIFICATION FOR ORACLE LABEL GENERATION

This section provides a formal analysis addressing the concern that using confidence-based heuristics for oracle label generation introduces circularity when training a confidence-based controller.

## A.1 PROBLEM FORMULATION

We frame refinement control as a Bayesian decision problem. Let:

- c ∈ R L : the downsampled confidence trace (observation)

- y ∈ { 0 , 1 } : correctness of the current answer (latent state)

- θ ∈ { correct , repairable , fundamental } : underlying error type

- a ∈ { HALT , RETHINK , ALTERNATIVE } : control action

The optimal Bayes decision rule minimizes expected loss:

<!-- formula-not-decoded -->

where L ( θ, a ) encodes the cost of taking action a when the true state is θ (e.g., token cost plus probability of eventual failure).

## A.2 WHY CIRCULARITY DOES NOT ARISE

The concern is that if oracle labels a ∗ are derived from confidence c via heuristics, and the controller learns π θ ( a | c ) , then the controller merely learns to reproduce the heuristic rather than the true optimal policy. We show this concern is unfounded for three reasons.

Reason 1: Labels Encode Correctness, Not Confidence. The fundamental labeling rule is:

<!-- formula-not-decoded -->

where correctness y is determined by external ground-truth verification , not by confidence. For correct traces (67-72% of our training data), labels are independent of confidence patterns. The controller must learn the non-trivial mapping P ( y = 1 | c ) to predict HALT correctly-this is exactly what Figure 2 shows is learnable but not trivially deducible from c .

Reason 2: Sequential Traces Provide Causal Ground Truth. For traces from sequential refinement runs, we observe the actual outcome of refinement actions:

<!-- formula-not-decoded -->

These labels encode counterfactual causal outcomes -'what would have happened if we had taken this action?'-not confidence patterns. The controller learns to predict these outcomes from confidence, but the outcomes themselves are defined independently.

Reason 3: Heuristics as Informative Prior. For parallel traces without iteration history, confidence-based heuristics approximate the counterfactual:

<!-- formula-not-decoded -->

where h encodes domain knowledge: declining confidence suggests foundational errors (RETHINK), high volatility suggests unstable reasoning (ALTERNATIVE). Critically, this is a prior belief that the controller can refine through exposure to sequential traces with ground-truth labels.

Formally, let D seq denote sequential traces (ground-truth labels) and D par denote parallel traces (heuristic labels). The controller learns:

<!-- formula-not-decoded -->

As |D seq |→∞ , the ground-truth term dominates and the controller converges to the Bayes-optimal policy regardless of heuristic quality. In practice, sequential traces comprise ∼ 40% of training data, providing substantial ground-truth signal.

## A.3 SUFFICIENT STATISTICS INTERPRETATION

A complementary perspective views confidence as a sufficient statistic for refinement decisions. By the factorization theorem, c is sufficient for θ if:

<!-- formula-not-decoded -->

where T ( trace ) = c extracts confidence. While we do not claim strict sufficiency, Figure 2 demonstrates that c captures substantial information about θ : late-phase confidence diverges by 1.2-1.5 logits between correct and incorrect traces, providing discriminative signal that the controller can exploit.

The 83-84% controller validation accuracy-substantially above the 33% random baseline and the ∼ 70% accuracy achievable by always predicting the majority class (HALT)-confirms that confidence patterns contain learnable structure beyond what simple heuristics encode.

## A.4 CONNECTION TO TRAINING OBJECTIVE

A natural question is how the Bayes decision rule (Eq. above) relates to the cross-entropy training loss used in practice (Section 3). The connection is as follows:

From Bayes Rule to Oracle Labels. The Bayes-optimal action a ∗ ( c ) minimizes expected loss L ( θ, a ) . In our setting, we cannot compute this directly at training time, but we can observe the optimal action retrospectively: for correct traces, a ∗ = HALT; for incorrect traces with iteration history, a ∗ is determined by which action led to eventual success. Oracle labels o ( i ) t encode these observed optimal actions.

From Oracle Labels to Cross-Entropy. Given oracle labels, the standard approach to learn the Bayes-optimal policy is to train a classifier via cross-entropy:

<!-- formula-not-decoded -->

Cross-entropy is a proper scoring rule : minimizing it recovers the true conditional distribution P ( a ∗ | c ) . Taking arg max of this distribution at test time yields the Bayes-optimal decision.

Step Penalty as Bayes Loss. The step penalty λ · t in our training objective can be interpreted within the Bayes framework as encoding the compute cost in L ( θ, a ) : actions that require more iterations incur higher loss. This encourages the controller to HALT early when confident, consistent with minimizing expected compute cost.

Thus, the cross-entropy training objective (main text) and the Bayes decision framework (this appendix) are consistent : the former is the standard method for learning a policy that approximates the latter.

## A.5 ROBUSTNESS TO LABEL NOISE

Finally, neural networks are known to be robust to label noise (Zhang et al., 2021). Even if heuristic labels for parallel traces are imperfect proxies for optimal actions, the controller can learn the underlying structure provided:

1. Label noise is not systematically biased (heuristics have &gt; 33% accuracy)
2. Sufficient clean labels exist (sequential traces provide ground truth)
3. The true decision boundary is learnable from c

All three conditions hold in our setting, ensuring that heuristic label noise does not prevent learning the optimal policy.

## B CONTROLLER ARCHITECTURE DETAILS

## B.1 CONV1D ARCHITECTURE

The CoRefine controller uses a Conv1D architecture optimized for temporal pattern recognition in confidence traces:

- Input: Downsampled confidence trace ¯ c ∈ R 16
- Conv1D Block 1: 64 channels, kernel size 5, stride 2
- Conv1D Block 2: 128 channels, kernel size 5, stride 2
- Conv1D Block 3: 256 channels, kernel size 3, stride 2
- MLP Head: 256 → 128 → 3 (action logits)
- Success Head: 256 → 128 → 1 (success probability)

Each Conv1D block includes batch normalization, ReLU activation, and dropout (0.3). Total parameters: ∼ 211,000.

## B.2 TRAINING DETAILS

- Optimizer: Adam with learning rate 10 - 4

- Batch size: 64

- Epochs: 30

- Step cost: λ = 0 . 1

- Training data: 5,000 traces from AIME/BRUMO/HMMT

- Validation split: 20%

Class Imbalance Handling. Training data exhibits significant class imbalance, particularly for Qwen3-32B where HALT labels comprise ∼ 84-90% of oracle actions, with RETHINK and ALTERNATIVE each at ∼ 5-8%. This imbalance causes naive training to predict HALT almost exclusively. We address this through three complementary strategies:

1. Focal Loss: We use focal loss (Lin et al., 2017) with focusing parameter γ = 2 . 0 : L focal = -α t (1 -p t ) γ log( p t ) . This down-weights well-classified examples (easy HALT cases) while focusing on hard minority examples.
2. Weighted Cross-Entropy with Smoothing: Inverse frequency class weights provide stronger minority emphasis: w c = N/ ( K · n c ) where N is total samples, K is number of classes, and n c is class count. Raw inverse frequency creates aggressive weight ratios ( ∼ 18 × for minorities); we apply a smoothing parameter s ∈ [0 , 1] via w smooth c = w s c , where s = 0 . 5 yields dampened ratios ( ∼ 4.3 × ) that improve RETHINK/ALTERNATIVE recall without excessive over-correction.
3. HALT Undersampling: We optionally undersample the HALT class to a target ratio (e.g., 67% of training data), reducing the majority class dominance while preserving all minority examples.

For DeepSeek-8B, standard cross-entropy suffices as the training data is more balanced ( ∼ 74% HALT). For Qwen3-32B, we use focal loss ( γ = 2 . 0 ) combined with HALT undersampling (target ratio 0.67) and dampened class weights (smoothing=0.5), which improves minority class F1 from ∼ 0.05 to ∼ 0.45 while maintaining overall accuracy.

## B.3 ORACLE LABEL GENERATION

Oracle labels are generated retrospectively:

1. If iteration t produces correct answer and no later iteration improves → HALT
2. If later iteration with same approach (RETHINK) eventually succeeds → RETHINK
3. If later iteration with different approach (ALTERNATIVE) eventually succeeds → ALTERNATIVE
4. If no iteration succeeds → use heuristic based on confidence patterns

## C EXPERIMENTAL DETAILS

## C.1 GENERATION HYPERPARAMETERS

Table 6: Generation hyperparameters for all experiments.

| Parameter   | DeepSeek-8B   | Qwen3-32B   |
|-------------|---------------|-------------|
| Temperature | 0.7           | 0.7         |
| Top-p       | 0.95          | 0.95        |
| Top-k       | 50            | 20          |
| Max tokens  | 64,000        | 32,000      |
| Logprobs    | 20            | 20          |

## C.2 HARDWARE AND RUNTIME

- GPUs: 2× NVIDIA A100 (tensor parallel)
- Inference framework: vLLM with prefix caching
- Average time per problem: 2-5 minutes (depending on iterations)
- Controller inference: &lt;1ms per decision

## C.3 CROSS-TASK GENERALIZATION EXPERIMENT

This section provides detailed experimental settings for the cross-task generalization study (Section 4.8).

Data Generation. We collected confidence traces from DeepSeek-8B on four mathematical reasoning benchmarks: AIME 2024, AIME 2025, BRUMO 2025, and HMMT February 2025. For each task, we combined traces from both refinement paradigms (RETHINK and ALTERNATIVE) and sampled across all iterations. Each trace was converted to training samples containing: (1) raw confidence values (token-level logprobs), (2) correctness labels, (3) oracle actions (HALT for correct, RETHINK/ALTERNATIVE for incorrect based on trace type).

Balancing. To ensure fair comparison across tasks with different sample counts, we undersampled each task to 228 samples-the minimum count across all tasks. This prevents larger tasks from dominating the training signal and enables direct comparison of task-specific controller quality.

Training Configuration. Each task-specific controller was trained independently using:

- Architecture: Conv1D controller with sequence length L = 16

, no manual features

- Data split: 70% train / 15% validation / 15% test (stratified by correctness)

- Optimizer: Adam with learning rate 10 - 3

- Training: 30 epochs, batch size 32

- Validation accuracy: 87-89% across all four controllers

Evaluation. Each of the 4 trained controllers was evaluated on test sets from all 4 benchmarks, yielding a 4 × 4 cross-task accuracy matrix (16 evaluations total). Accuracy is measured as the fraction of correct action predictions (HALT vs. RETHINK/ALTERNATIVE) compared to oracle labels.

Results Summary. Table 7 shows the complete cross-task accuracy matrix.

Table 7: Cross-task generalization accuracy (%). Rows indicate training task, columns indicate evaluation task. Diagonal entries (in-task) are highlighted.

| Train \ Eval   |   AIME24 |   AIME25 |   BRUMO25 |   HMMT25 |   Avg |
|----------------|----------|----------|-----------|----------|-------|
| AIME 2024      |     94.1 |     94.1 |      90.2 |     90.2 |  92.2 |
| AIME 2025      |     97.1 |     97.1 |      97.1 |     94.1 |  96.4 |
| BRUMO 2025     |     97.1 |     94.1 |      97.1 |     94.1 |  95.6 |
| HMMTFeb 2025   |     94.1 |     97.1 |      97.1 |     94.1 |  95.6 |
| Column Avg     |     95.6 |     95.6 |      95.4 |     93.1 |  94.9 |

## Key Findings.

- In-task accuracy (diagonal): 94.1-97.1%, mean 95.6%
- Out-of-task accuracy (off-diagonal): 90.2-97.1%, mean 94.6%
- Generalization gap : 0.8% (in-task minus out-of-task average)
- Worst transfer : AIME24 → BRUMO25 and AIME24 → HMMT25 (90.2%)
- Best transfer : AIME25 → BRUMO25 (97.1%, matching in-task)

The near-uniform accuracy across the matrix confirms that confidence patterns are task-agnostic: controllers trained on any single benchmark generalize effectively to others without task-specific adaptation.

## D ADDITIONAL RESULTS

## D.1 TOKEN EFFICIENCY VISUALIZATION

## D.2 PER-PROBLEM ITERATION DISTRIBUTION

Figure 11 shows the distribution of iterations used by CoRefine across all four benchmarks for different maximum iteration configurations. The controller demonstrates adaptive compute allocation: problems are binned by the number of iterations used (1, 2, 3, 4, or ≥ 5).

Key observations from the iteration distribution:

- Early stopping dominance: 40-60% of problems are solved in just 1-2 iterations, indicating the controller effectively identifies confident, correct answers early.
- Adaptive budget usage: Only 10-20% of problems require the full iteration budget ( ≥ 5 iterations), demonstrating efficient compute allocation.
- Task-dependent patterns: Easier benchmarks (AIME24, BRUMO25) show higher earlystopping rates, while harder benchmarks (HMMT25) require more iterations on average.

## D.3 ITERATION BUDGET SCALING

Figure 12 shows how CoRefine performance scales with maximum iteration budget compared to Majority@K (sequential sampling with majority voting). While Majority@K uses all K samples regardless of problem difficulty, CoRefine dynamically allocates compute based on confidence signals.

Key findings from the iteration budget ablation:

Figure 10: Accuracy vs. token usage across all methods and benchmarks. Left: Comparison at @512 budget. Right: Comparison at @20 budget. CoRefine (green) and CoRefine Tree (blue) consistently achieve high accuracy (80-95%) while using orders of magnitude fewer tokens than Majority (red) and DeepConf (yellow). BRUMO25 (triangles) achieves the highest accuracy ( ∼ 95%), while HMMT25 (diamonds) is most challenging ( ∼ 60-75%). At @20 budget, CoRefine methods use 0.2-1.5 × 10 7 tokens versus 1.5-1.75 × 10 7 for baselines-a consistent efficiency advantage across all benchmarks.

<!-- image -->

- Efficiency scaling: At all budget levels, CoRefine uses only 1.4-4.8 average iterations versus the full K budget for Majority@K, representing 2-4 × compute savings.
- Accuracy parity: CoRefine matches or exceeds Majority@K accuracy across configurations while using far fewer iterations.
- Diminishing returns for baselines: Majority@K scales linearly with budget, while CoRefine's iteration usage grows sub-linearly (1.4 → 1.9 → 2.5 → 4.8), demonstrating adaptive compute allocation.

## D.4 CONTROLLER CONFUSION MATRIX

Figure 13 shows the confusion matrices for controller action predictions versus oracle labels on validation sets for both primary controllers: DeepSeek-R1-8B and Qwen3-32B. The matrices reveal distinct prediction patterns shaped by the underlying class distributions in each model's training data.

DeepSeek-R1-8B (1,808 validation samples, 84.0% accuracy):

- HALT : precision 84.6%, recall 96.5% (F1: 0.902, support: 1,223)
- RETHINK : precision 81.3%, recall 72.5% (F1: 0.766, support: 371)
- ALTERNATIVE : precision 63.9%, recall 24.8% (F1: 0.357, support: 214)

Qwen3-32B (1,638 validation samples, 84.6% accuracy):

- HALT : precision 94.3%, recall 84.9% (F1: 0.893, support: 1,219)
- RETHINK : precision 67.6%, recall 83.8% (F1: 0.749, support: 389)
- ALTERNATIVE : precision 43.1%, recall 83.3% (F1: 0.568, support: 30)

Interpretation. The two controllers achieve similar overall accuracy ( ∼ 84%) but exhibit complementary error patterns. DeepSeek-R1-8B excels at HALT decisions with 96.5% recall, but struggles with ALTERNATIVE (24.8% recall), tending to under-predict exploration. Qwen3-32B shows more balanced performance across all three classes with notably high RETHINK recall (83.8%) and ALTERNATIVE recall (83.3%), though at the cost of lower precision for these minority classes. Both

<!-- image -->

MaxIterationsConfig

MaxIterationsConfig

Figure 11: CoRefine iteration distribution across benchmarks. Stacked bar charts show the percentage of problems solved at each iteration count (binned: 1, 2, 3, 4, ≥ 5) for different maximum iteration configurations (it10, it20). The high proportion of problems solved in 1-2 iterations demonstrates effective early stopping, while the tail of problems requiring ≥ 5 iterations shows appropriate resource allocation for difficult cases.

<!-- image -->

Figure 12: Performance vs. maximum iteration budget. Each data point represents the average across all four benchmark datasets. CoRefine (green) achieves competitive accuracy while using only 1.4-4.8 average iterations compared to the full K budget used by Majority@K (red). At K = 3 , 5, 10, and 20, CoRefine uses 1.4, 1.9, 2.5, and 4.8 iterations respectively. The efficiency gap grows with larger budgets: at K = 20 , CoRefine uses ∼ 4 × fewer iterations on average.

controllers maintain high HALT precision ( &gt; 84%), ensuring they rarely stop on incorrect answersthe critical safety property. The complementary strengths suggest potential for ensemble approaches in future work.

<!-- image -->

PredictedAction

PredictedAction

Figure 13: Controller confusion matrices for DeepSeek-R1-8B (left) and Qwen3-32B (right). Rows represent oracle (ground-truth) actions; columns represent predicted actions. Cell values show counts with row-normalized percentages. Both controllers achieve ∼ 84% accuracy with similar class distributions but exhibit different error patterns, reflecting the distinct confidence characteristics of their underlying LLMs.

## E ARCHITECTURAL VARIANTS AND ABLATION RESULTS

This section provides detailed descriptions of architectural variants explored during development, along with experimental results. Our primary configuration uses raw downsampled confidence features with a Conv1D controller; subsequent variants explored extensions that did not yield significant accuracy improvements.

## E.1 FEATURE ENRICHMENT

We investigated augmenting the raw confidence trace with additional feature types:

Regional Statistics. We computed phase-specific confidence aggregates: head confidence (first 10% of tokens), middle confidence (central 80%), tail confidence (final 10%), and global minimum confidence. These 12 additional features aimed to capture reasoning-phase-specific patterns.

Cross-Iteration Dynamics. For iterations t &gt; 1 , we extracted: confidence delta ∆ t = ¯ c ( t ) mean -¯ c ( t -1) mean , answer consistency (binary), confidence trend (increasing/decreasing/stable), and iteration count. These 4 features aimed to model refinement trajectory.

Results. Table 8 summarizes feature ablation results.

Table 8: Feature ablation results. Controller validation accuracy across configurations.

| Configuration             |   Input Dim | Val Acc   | Params   |
|---------------------------|-------------|-----------|----------|
| Raw only ( L = 16 )       |          16 | 83.2%     | 211K     |
| Raw + Regional            |          28 | 83.8%     | 248K     |
| Raw + Regional + Dynamics |          32 | 84.1%     | 272K     |

Finding: Feature enrichment provides marginal validation accuracy gains ( &lt; 1%) while increasing model complexity from 211K to 272K parameters. The simpler raw-only configuration achieves comparable test accuracy with 30% fewer parameters. Analysis of 106 post-bug-fix experiments showed regional features achieved highest average accuracy at 79.97%, but raw confidence alone reached the best single result at 90.00% on AIME 2024, demonstrating that raw confidence captures sufficient signal when properly normalized.

## E.2 ITERATION NORMALIZATION

This variant addressed a distribution shift between training and inference, discovered through debugging unexpected controller behavior.

Bug Discovery. During development, we discovered a critical bug: training data inadvertently leaked oracle labels through manual features ( step\_idx , prev\_success , prev\_delta\_score ), achieving 88% controller accuracy by exploiting these signals rather than learning confidence patterns. After removing manual features and retraining with raw confidence only, controllers achieved 83-84% validation accuracy but exhibited unexpected behavior: 100% HALT at iteration 1 despite balanced training labels (74% HALT, 13% RETHINK, 13% ALTERNATIVE).

Root Cause. Analysis revealed severe iteration-dependent confidence bias: iteration 0 shows mean=15.65 logits, iteration 1 drops to 12.94, and iteration 2+ stabilizes at 8-9 logits. During training, data was collected from forced multi-iteration runs, yielding confidence statistics at iterations 1-10. During inference, the controller determines stopping, so most problems halt at iteration 1. Controllers trained on mixed iterations (average ∼ 10) interpreted high iteration-0 confidence as 'above training average' and always halted.

Solution. We applied z-score normalization relative to iteration-specific baselines:

<!-- formula-not-decoded -->

where µ t and σ t are computed from training data at iteration t . Specifically, we used µ 0 =15.65, µ 1 =12.94, µ 2+ =8.5 as iteration-specific baselines.

Results. Table 9 compares base and normalized configurations.

Table 9: Iteration normalization results. Normalization reduces iterations but does not improve accuracy.

| Config                          | AIME24   | AIME25   | BRUMO25   | HMMT25   |
|---------------------------------|----------|----------|-----------|----------|
| Base Accuracy                   | 83.3%    | 80.0%    | 70.0%     | 60.0%    |
| Normalized (all_conv)           | 83.3%    | 76.7%    | 70.0%     | 46.7%    |
| Normalized (aim_bru)            | 83.3%    | 73.3%    | 83.3%     | 63.3%    |
| Base Avg Iters                  | 2.37     | 2.37     | 2.57      | 5.03     |
| Normalized Avg Iters (all_conv) | 1.17     | 1.17     | 1.20      | 1.13     |
| Normalized Avg Iters (aim_bru)  | 1.57     | 1.47     | 1.37      | 1.27     |

Finding: Iteration normalization successfully restored refinement behavior (7-30% of problems now iterate beyond iteration 1) and reduces average iterations by 2-4 × , but does not improve accuracy over the base configuration. The controller becomes more conservative, halting earlier on average (86-93% HALT rate vs. continuous refinement in the base). Performance is similar on easier tasks (AIME24, BRUMO25) but degrades on harder tasks (HMMT25: 46.7% vs 60.0%). This trades refinement diversity for efficiency: 1.1-1.6 average iterations versus 2.4-5.0, representing 2-4 × fewer LLM calls. Notably, task-specific training achieves +13.3% on BRUMO25, suggesting specialization potential.

## E.3 ENHANCED MESSAGE COMPACTION

We tested two approaches to improve message compaction beyond heuristic extraction:

Prompt-Based Compaction. Instead of heuristic extraction, we used GPT-4o-mini to extract richer information from reasoning traces: key observations, explicitly stated uncertainties, identified errors, and promising directions-signals that simple heuristics cannot reliably detect. This reduced trace length by 90-95% while preserving actionable information, producing higher-quality summaries but increasing latency and cost.

Rule-Based Hybrid Controller. We designed an 8-rule decision system combining confidence thresholds, answer consistency, and iteration count:

1. High confidence ( &gt; 0 . 85 ) + consistent answer → HALT
2. Low confidence ( &lt; 0 . 55 ) + iteration 1 → ALTERNATIVE
3. Moderate confidence + answer change → RETHINK

Results. Table 10 summarizes the accuracy across all three phases.

Table 10: Enhanced compaction experiments. Each phase builds on the previous, showing incremental improvements from better context utilization.

| Phase   | Component              | AIME24   | AIME25   | BRUMO25   | HMMT25   |
|---------|------------------------|----------|----------|-----------|----------|
| Phase 1 | Rule-based + Heuristic | 83.3%    | 80.0%    | 80.0%     | 63.3%    |
| Phase 2 | + Prompt Compaction    | 83.7%    | 83.3%    | 83.3%     | 66.7%    |
| Phase 3 | + Neural Controller    | 86.3%    | 83.3%    | 83.3%     | 66.7%    |

Finding: Enhanced compaction provides consistent but modest improvements. Phase 1 uses rulebased hybrid controller (8 decision rules) with heuristic message compaction, extracting answer, confidence statistics, identified errors, and solution methods from traces. Phase 2 adds GPT-4o-mini based compaction for richer context extraction (key observations, explicitly stated uncertainties, promising directions), yielding +0.4-3.3% gains across benchmarks. Phase 3 incorporates a neural controller trained on 1,500 problems with sentence-BERT embeddings, providing an additional +3.0% on AIME24. Overall, the full pipeline achieves +3.0% on AIME24 and +3.4% on HMMT25 over the base rule-based approach. However, these gains come at the cost of increased latency (GPT4o-mini API calls) and complexity; the simpler heuristic compaction remains the recommended default for most use cases.

## E.4 SUMMARY AND RECOMMENDATIONS

Based on our extensive ablation studies, we recommend the base configuration as the default:

- Features: Raw downsampled confidence only ( L = 16 )
- Controller: 3-layer Conv1D ( ∼ 211K parameters)
- Compaction: Heuristic extraction

This configuration achieves the best accuracy-efficiency trade-off with minimal complexity. Future work should explore orthogonal improvements such as better base models, diverse sampling strategies, or verification-augmented refinement.

## F COREFINE TREE CASE STUDIES

This section provides additional CoRefine Tree visualizations demonstrating controller behavior across different problem difficulties. All examples use DeepSeek-8B with warmup=3, branch factor=2, max depth=2 (15 total nodes).

## F.1 CASE STUDY: BRUMO 2025 Q23 (SAFETY-FIRST BEHAVIOR)

Figure 14 shows controller behavior on a BRUMO 2025 function iteration problem. The controller achieves 73.3% accuracy (11/15 nodes with correct HALT/REFINE decisions) with the critical property of zero false HALTs -it never stops on incorrect answers.

Key Observation. The controller's 'over-cautiousness' (REFINE on 4 correct answers) is a desirable failure mode. When uncertain, the controller errs toward additional exploration rather than premature commitment. This asymmetry-cautious on correct, never wrong on incorrect-emerges naturally from training on confidence patterns without explicit safety objectives.

<!-- image -->

Question23(AlME2025)

Define thefunction $f$ on positive integers $$ f(n)=begin{cases}frac{n}{2}&amp;text{if }n smallest positive integer $k$ such that $f^ {k}(n)=1$. How many positive integers satisfy $S(n)=11$ ?

GroundTruth:89

Figure 14: CoRefine Tree on BRUMO 2025 Q23 (function iteration S ( n ) problem, ground truth: 89). The controller HALTs on 1 correct answer while correctly refining 10 incorrect answers. It exhibits safety-first behavior: 4 correct answers receive REFINE (over-cautious but harmless), while zero incorrect answers receive HALT. This conservative approach prioritizes avoiding catastrophic errors over maximizing efficiency.

## F.2 CASE STUDY: HMMT 2025 Q14 (CHALLENGING COMBINATORICS)

Figure 15 shows controller behavior on a difficult grid-counting problem. Despite the problem's complexity, the controller maintains zero false HALTs .

Key Observation. The 60% accuracy reflects increased conservatism on a difficult problem, not poor discrimination. The controller recognizes that this problem produces diverse incorrect answers with varying confidence patterns, and responds by raising its threshold for HALT decisions. This adaptive conservatism is precisely the desired behavior: allocate more exploration budget to harder problems.

## F.3 SUMMARY: THE ZERO-FALSE-HALT PROPERTY

Across all three case studies, the controller exhibits a consistent pattern:

The zero-false-HALT property is the controller's most important safety guarantee. Variation in accuracy stems from conservativeness (REFINE on correct answers), which costs efficiency but not correctness. This asymmetric error profile-harmless over-exploration vs. catastrophic premature stopping-emerges naturally from confidence-based training and represents a key advantage over fixed iteration counts or heuristic stopping rules.

<!-- image -->

Figure 15: CoRefine Tree on HMMT 2025 Q14 (11 × 11 grid door-counting problem, ground truth: 200). The controller achieves 60% accuracy with zero false HALTs . It correctly HALTs on 1 node with answer 200, correctly REFINEs 8 incorrect answers, but conservatively REFINEs 6 correct answers. On this challenging combinatorics problem, the controller appropriately increases caution rather than making critical errors.

Table 11: Controller behavior across case studies. All examples achieve zero false HALTs.

| Problem   | Accuracy   |   Correct HALTs |   False HALTs |   Over-cautious | Characteristic                      |
|-----------|------------|-----------------|---------------|-----------------|-------------------------------------|
| HMMTQ13   | 100%       |               1 |             0 |               0 | Perfect discrimination Safety-first |
| BRUMO Q23 | 73.3%      |               1 |             0 |               4 |                                     |
| HMMTQ14   | 60%        |               1 |             0 |               6 | Conservative on hard                |

## G BIXBENCH: ADAPTING TO REGULATED DOMAINS WITH REFUSAL

This appendix provides technical details for the BixBench extension described in Section 4.9.

## G.1 MOTIVATION AND PROBLEM FORMULATION

Deploying pre-trained LLMs in regulated domains (healthcare, finance, bioinformatics) presents a unique challenge: models must balance answering questions correctly with abstaining when uncertain. Safety fine-tuning often makes models overly conservative, defaulting to refusal even when additional reasoning could resolve uncertainty. This creates a cost-effectiveness bottleneck: full fine-tuning for domain adaptation is expensive, yet naive prompting yields excessive refusal rates.

BixBench (Sasse et al., 2025) provides a testbed for this scenario: 205 bioinformatics MCQs spanning genomics, proteomics, and systems biology. We extend the task by adding 'Insufficient information to answer the question' as a 5th choice, creating a dual-OOD challenge:

1. Knowledge OOD: Mathematical reasoning (training domain) → biological knowledge (test domain)
2. Behavior OOD: Mandatory answering → selective refusal

## G.2 EVALUATION METHODOLOGY COMPARISON

Our evaluation protocol differs substantially from the original BixBench paper (Sasse et al., 2025), which explains the different baseline accuracies observed.

Original BixBench Evaluation: Agent-Based Pipeline. The BixBench benchmark is designed to evaluate LLM agents that perform bioinformatics analysis. In the original evaluation pipeline:

1. The agent is given a dataset (CSV/TSV files) and a high-level analysis request
2. The agent autonomously generates Python analysis notebooks to explore the data
3. MCQquestions are then answered conditioned on the agent-generated analysis outputs

This agent-based approach provides rich context for answering questions, as the model has already 'seen' relevant computations and visualizations from its own analysis. Sasse et al. (2025) report accuracies of ∼ 23-48% for various LLMs (Claude 3.5, GPT-4o) under this paradigm.

Our Evaluation: Direct MCQ Without Agent Context. We evaluate models on direct MCQ answering without the agent analysis pipeline:

- Models receive only the question text and answer choices
- No datasets, no generated notebooks, no intermediate analysis outputs
- Tests models' inherent bioinformatics knowledge and reasoning capability

This setup is more challenging and yields lower baseline accuracies ( ∼ 3-38% in our experiments), but serves our evaluation goal: testing whether the CoRefine controller can navigate uncertainty in a novel domain where the underlying LLM lacks task-specific context.

Random Baseline Considerations. For MCQ evaluation, the theoretical random baseline is 1 /k where k is the number of choices (25% for 4-choice, 20% for 5-choice with refusal). With 205 questions, empirical sampling introduces variance: a 95% confidence interval for the random baseline is approximately 14.6%-25.4% for 5-choice MCQ. We use the theoretical 1 /k following standard practice, which represents expected performance under uniform random guessing.

Implications for Interpreting Results. The lower baseline accuracies in our direct MCQ setup amplify the challenge of the refusal-vs-reasoning distinction. When a model achieves only 38.5% on standard MCQ, adding a refusal option that collapses accuracy to 3.4% reveals severe overconservative behavior. CoRefine's recovery to 16-23% demonstrates meaningful improvement in this challenging regime, even if absolute accuracies remain below agent-based evaluation levels.

## G.3 4-CLASS CONTROLLER ARCHITECTURE

We extend the CoRefine controller from 3 classes (HALT, RETHINK, ALTERNATIVE) to 4 by adding REFUSE :

- HALT: Accept current answer (model is correct with high confidence)
- RETHINK: Re-examine reasoning with same approach (recoverable error)
- ALTERNATIVE: Try fundamentally different strategy (systematic error)
- REFUSE: Accept model abstention (irreducible uncertainty)

Model Architecture. Identical to the mathematical reasoning controller (Appendix B) except for the output layer: Conv1D encoder (16-dim input → 256-dim embedding) + MLP head (256 → 128 → 4 action logits + success probability). Total parameters: ∼ 42,000.

## G.4 TRAINING DATA COLLECTION

We collected 6,560 confidence traces (205 questions × 32 traces per question) using Qwen3-32B:

- Prompt Format: Neutral MCQ prompt with all 5 choices (A/B/C/D/Unsure)
- Confidence Extraction: Token-level logprobs → mean(-20 × logprobs) per token → range [3, 36]
- Position Bias Prevention: Choices randomized for each sample

Oracle Label Generation. Unlike mathematical reasoning where correctness is binary, refusal introduces ambiguity: is 'Insufficient information' an error (question is answerable) or appropriate caution? We adopt a ground-truth based labeling strategy:

## Algorithm 2: Oracle Label Generation for BixBench

```
Input: extracted_answer, ground_truth, unsure_letter, confidences Output: label ∈ {HALT, RETHINK, ALTERNATIVE, REFUSE} if extracted_answer = ground_truth then label ← HALT else if extracted_answer = unsure_letter then mean_conf ← mean(confidences) if mean_conf ≤ 10.5 // Over-confident refusal then label ← ALTERNATIVE else if mean_conf > 11.5 // Genuinely uncertain then label ← REFUSE else label ← RETHINK end else // Wrong non-refuse answer label ← heuristic(confidence_pattern) end return label
```

Thresholds (10.5, 11.5) were derived from confidence distribution analysis on Qwen3-32B, where refusal answers exhibit bimodal confidence: over-confident refusals (mean ≈ 9-10) versus cautious refusals (mean ≈ 12-13). These thresholds are model-specific; DeepSeek-8B requires different calibration.

## G.5 TRAINING CONFIGURATION

Training follows the methodology in Appendix B with modifications for class imbalance:

- Dataset Split: 4,590 train / 982 val / 988 test
- Class Distribution: HALT: 4.8%, RETHINK: 46.7%, ALTERNATIVE: 30.2%, REFUSE: 18.3%
- Loss Function: Focal loss ( γ = 2 . 0 ) with smoothed class weights (smoothing=0.5)
- Training: 30 epochs, batch size 32, lr= 10 -4 , step cost λ = 0 . 1

Validation accuracy: 76.8% (4-class). Per-class F1: HALT: 0.52, RETHINK: 0.79, ALTERNATIVE: 0.71, REFUSE: 0.61.

## G.6 TWO-PHASE PROMPTING STRATEGY

A critical design decision addresses prompt distribution mismatch. The controller was trained on confidence traces from a neutral prompt where all 5 choices (including 'Insufficient information') were presented. Using a different prompt at test time would produce different confidence distributions, causing incorrect controller decisions.

## Solution: Phased Prompting.

- Iteration 0 (NEUTRAL): Present all 5 choices, matching training distribution exactly.
- Iterations 1+ (AGGRESSIVE): Remove 'Insufficient information' option and explicitly instruct the model to commit to a concrete answer.

This approach preserves confidence patterns for controller evaluation while preventing infinite refusal loops. The aggressive refinement prompt states: 'Your previous answer was 'Insufficient information' - but that option has been REMOVED. You MUST now select from the remaining choices...'

## G.7 INFERENCE PIPELINES

We implement two inference variants:

CoRefine (Sequential). Single-trace iterative refinement:

1. Generate initial answer with NEUTRAL prompt (5 choices)
2. Extract confidence trace, apply controller
3. If HALT or REFUSE → stop
4. If RETHINK or ALTERNATIVE → generate refinement with AGGRESSIVE prompt (4 choices)
5. Repeat until HALT/REFUSE or max iterations (5)

CoRefine-Tree (Parallel Branching). Hybrid warmup + branching refinement:

1. Warmup: Generate K = 4 traces in parallel (NEUTRAL prompt)
2. Controller Evaluation: Apply controller to all traces
3. Early Stopping Check: If ≥ 50% of traces receive HALT/REFUSE → stop, aggregate via voting
4. Branching Refinement: For RETHINK/ALTERNATIVE traces, spawn B = 2 children each (AGGRESSIVE prompt)
5. Recursion: Repeat until max depth (3) or early stop
6. Voting: Aggregate answers via majority, confidence-weighted, and high-confidence majority methods

## G.8 REFUSAL BEHAVIOR ANALYSIS

Full BixBench results are presented in Section 4.9 (Figure 8). Here we provide additional analysis of refusal behavior that motivates the 4-class controller design.

Refusal Statistics. Whenthe refusal option is available, 89.5% of Qwen3-32B traces select 'Insufficient information' rather than attempting to answer. Critically, models exhibit higher confidence when refusing than when answering correctly: mean confidence for refusals is 12.17 compared to 10.3 for correct answers and 13.8 for wrong answers (lower values indicate higher confidence in Qwen3-32B's logprob scale).

Model-Specific Patterns. This 'over-confident refusal' behavior is model-specific. DeepSeek8B shows opposite patterns: cautious refusal with high uncertainty. These divergent patterns explain why simple confidence thresholding fails (see Section G.9) and motivate the learned controller approach that can adapt to model-specific confidence characteristics.

## G.9 NAIVE CONFIDENCE THRESHOLD BASELINE

A natural baseline for handling over-refusal is confidence thresholding: exclude traces whose confidence falls outside a specified range before voting. The intuition is that low-confidence refusals might be 'uncertain refusals' that should be discarded in favor of more confident non-refusal answers.

Experimental Setup. We evaluated DeepConf@32 with confidence thresholding on BixBench (MCQ with refusal):

- DeepSeek-8B: Exclude traces with mean confidence &gt; 11.5 ( exclude\_max=11.5 ). This targets 'cautious refusals' where higher values indicate lower confidence in DeepSeek's logprob scale.
- Qwen3-32B: Exclude traces with mean confidence &lt; 12.0 ( exclude\_min=12.0 ). This targets 'over-confident refusals' where lower values indicate higher confidence in Qwen3's logprob scale.

Results. Naive thresholding fails to improve accuracy:

- DeepSeek-8B: 3.41% (DC@32) → 2.54% (DC+Thresh) degraded by 0.87pp
- Qwen3-32B: 3.41% (DC@32) → 4.39% (DC+Thresh) - improved by 0.98pp but still &lt; 5%

Analysis: Why Thresholding Fails. Confidence distribution analysis (from refusal\_confidence\_analysis.ipynb ) reveals the fundamental limitation:

1. Models are more confident when refusing than when correct. For Qwen3-32B: mean confidence for refusals is 12.17 (lower = more confident) compared to 10.3 for correct answers. This means confidence thresholding that targets refusals will also fi lter out correct answers.
2. Refusal confidence is not discriminative. The confidence distributions for 'correct refusal' (question genuinely unanswerable) vs 'incorrect refusal' (question is answerable but model refuses) overlap substantially. Simple thresholds cannot distinguish these cases.
3. Model-specific calibration is brittle. DeepSeek-8B and Qwen3-32B exhibit opposite confidence patterns: DeepSeek shows 'cautious refusal' (high uncertainty when refusing) while Qwen3 shows 'over-confident refusal' (high confidence when refusing). Hand-tuned thresholds for one model do not transfer.

Implication for CoRefine. This analysis motivates the learned controller approach: rather than hand-crafting confidence thresholds, CoRefine trains a neural network to recognize patterns in full confidence traces that distinguish recoverable vs genuine uncertainty. The 76.8% validation accuracy (vs &lt; 5% naive threshold) demonstrates that these patterns exist and are learnable, even if they cannot be captured by simple threshold rules.

## G.10 BIXBENCH CASE STUDIES

We present two additional case studies demonstrating the 4-class controller's behavior on BixBench questions.

## G.10.1 CASE STUDY: Q19 - SUCCESSFUL REFINEMENT WITH UNSURE EXCLUSION

Figure 16 shows the controller's behavior on a Mann-Whitney U statistic question. The warmup trace selects answer B with confidence 11.49, receiving RETHINK. After refinement, 2 of 3 nodes

HALT on B (67%), with one receiving ALTERNATIVE. The early stopping condition is satisfied with consistent answer B.

<!-- image -->

Figure 16: CoRefine Tree on BixBench Q19 (Mann-Whitney U statistic, ground truth: B after Unsure exclusion). Demonstrates the full refinement pipeline: warmup → RETHINK → refinement → HALT. The controller correctly identifies that the initial moderate-confidence answer warrants verification, then halts after refinement confirms consistent answer B. Node colors: green=HALT, red=RETHINK, orange=ALTERNATIVE.

Key Observation. This example demonstrates the 'Unsure Exclusion' mechanism where ground truth changes from C → B after removing the 'unsure' option. The controller successfully navigates this by recognizing that initial uncertainty (RETHINK) can be resolved through additional reasoning.

## G.10.2 CASE STUDY: Q5 - HONEST REFUSAL ON GENUINE UNCERTAINTY

Figure 17 shows controller behavior on a chi-square test p-value question where the model exhibits genuine uncertainty. All 4 warmup traces select option B ('Insufficient information'), with the controller assigning 2 RETHINK and 2 REFUSE actions (50% early stop threshold met).

Key Observation. This case demonstrates honest refusal -a critical capability for regulated domains. The controller distinguishes between: (1) over-trained conservative refusal that can be overcome with encouragement (Q3: all RETHINK → successful refinement), and (2) genuine knowledge limitations warranting abstention (Q5: 50% REFUSE → early stop). This distinction explains the dramatic accuracy difference: baseline methods drop from 38.5% to 3.4% because they cannot differentiate these cases, while CoRefine's 4-class controller learns when encouragement will succeed versus when honest refusal is appropriate.

## G.10.3 SUMMARY: 4-CLASS CONTROLLER BEHAVIOR

The three BixBench case studies (Q3 in Section 4.9, Q19 and Q5 above) demonstrate the 4-class controller's key capability:

Table 12: BixBench case studies: 4-class controller distinguishes refusal types.

| Question                                           | Warmup Behavior                         | Controller Action                        | Result                          | Interpretation                                                     |
|----------------------------------------------------|-----------------------------------------|------------------------------------------|---------------------------------|--------------------------------------------------------------------|
| Q3 (Odds ratio) Q19 (Mann-Whitney) Q5 (Chi-square) | 3/4 Unsure 1/1 B (uncertain) 4/4 Unsure | 4/4 RETHINK RETHINK 2 RETHINK + 2 REFUSE | Correct (D) Correct (B) Abstain | Recoverable conservatism Verification succeeds Genuine uncertainty |

The controller's ability to predict when refinement will succeed is the key to recovering accuracy from the 3.4% baseline. By learning confidence patterns that distinguish post-trained humility from genuine knowledge gaps, CoRefine enables selective encouragement rather than blanket aggressive prompting.

Q5:Using a chi-square test,what is the\_p-valuefor the associationbetweenBCGvaccination and CoviD-19severity?

<!-- image -->

Options:A=GT|B=Unsure|5totalchoices

Figure 17: CoRefine Tree on BixBench Q5 (Chi-square p-value, ground truth: A). The controller recognizes genuine uncertainty: all 4 warmup traces select 'Unsure' with high confidence in that choice. Unlike Q3 where refusal stemmed from post-trained conservatism (3.4% accuracy recoverable to correct answers), here the 50% REFUSE rate indicates the controller predicts additional reasoning will not resolve the knowledge gap. Node colors: green=HALT, red=RETHINK, orange=ALTERNATIVE, purple=REFUSE.

## G.11 KEY TAKEAWAYS

1. Dual-OOD Challenge: Regulated domains require adaptation to both knowledge distribution (biology vs. mathematics) and behavioral distribution (refusal vs. mandatory answering).
2. Over-Refusal Problem: Safety-tuned models default to abstention even when additional reasoning could resolve uncertainty (38.5% → 3.4% accuracy drop).
3. Lightweight Adaptation: A 42K-parameter controller learns when to accept refusal vs. push for answers, avoiding expensive full fine-tuning.
4. Prompt Distribution Fidelity: Two-phase prompting (neutral → aggressive) preserves training distribution while enabling refinement.
5. Model-Specific Patterns: Refusal confidence patterns vary by model architecture (Qwen3: over-confident, DeepSeek: cautious), requiring calibrated thresholds.

## H SYNTHESIS PROMPT TEMPLATES

## H.1 RETHINK PROMPT

## RETHINK Synthesis Prompt

You are solving a mathematical problem. Your previous attempt may have errors.

Problem:

{problem}

Previous Attempts:

{compacted\_history}

Previous Answer: {previous\_answer} Confidence: {confidence\_stats}

Task: Re-examine your reasoning step by step. Verify each calculation and logical inference. Consider whether your approach is sound. If you find errors, correct them. If your reasoning is correct, confirm your answer.

Please provide your solution with final answer in \ boxed{}.

## H.2 ALTERNATIVE PROMPT

## ALTERNATIVE Synthesis Prompt

You are solving a mathematical problem. Your previous approaches have not succeeded.

Problem:

{problem}

Previous Attempts: {compacted\_history}

Task: Your previous approaches may have fundamental issues. Try a COMPLETELY DIFFERENT method or problem formulation. Consider: - Alternative problem representations - Different mathematical techniques - Unconventional solution paths

Please provide your solution with final answer in \ boxed{}.

## I RELATED WORK (EXTENDED)

## I.1 TEST-TIME SCALING

Current LLMs increasingly succeed by allocating very large amounts of reasoning at inference, a paradigm we call test-time scaling (Snell et al., 2024; Welleck et al., 2024). Along one axis, Chainof-Thought (Wei et al., 2022) depth is scaled by lengthening a single reasoning trajectory through more thinking steps; representative models include o1 (Jaech et al., 2024), DeepSeek R1 (Guo et al., 2025), Kimi K1.5 (Team, 2025), Qwen3 (Yang et al., 2025), and Grok-4 (xAI, 2025). Along a complementary axis, parallel generation is scaled by increasing the number of trajectories and aggregating them: Self-Consistency (Wang et al., 2023) and Best-of-N (Brown et al., 2024; Irvine et al., 2023) sample multiple candidates and select via voting or a score. CoRefine introduces a third axis: sequential refinement with learned halting.

## I.2 EFFICIENT REASONING

Test-time scaling for reasoning seeks better accuracy-compute trade-offs through adaptive sampling and richer aggregation. On the parallel axis, Early-Stopping Self-Consistency (ESC), AdaptiveConsistency, Dynamic Voting, and Dynasor achieve more efficient self-consistency by reducing the required sample count while preserving accuracy (Li et al., 2024; Aggarwal et al., 2023; Xue et al., 2023; Fu et al., 2024). On the CoT-depth axis, efficient CoT fine-tuning methods elicit shorter, more efficient chains (Chen et al., 2024; Luo et al., 2025; Hou et al., 2025). CoRefine contributes a sequential refinement approach that complements both axes.

## I.3 CONFIDENCE ESTIMATION

Confidence estimation techniques offer a complementary direction by directly quantifying the reliability of model outputs. DeepConf (Fu et al., 2025) demonstrates that confidence-filtered majority voting substantially outperforms naive self-consistency, establishing that token-level confidence provides discriminative signal for solution quality. Related work proposes metrics such as

token-level entropy and uncertainty scores (Fadeeva et al., 2024), self-certainty based on KL divergence from a uniform distribution (Kang et al., 2025), and specialized confidence tokens learned during fine-tuning (Chuang et al., 2025; Zhao et al., 2025). The ART framework (Shridhar et al., 2023) introduced trust scoring for iterative refinement, while path-consistency methods (Zhu et al., 2024) leverage high-confidence partial reasoning prefixes to guide subsequent sampling. Auxiliary lightweight predictors such as UHeads (Ni et al., 2025) provide task-agnostic step-level uncertainty estimates. CoRefine uniquely uses full-trace confidence as a control signal for refinement decisions rather than for trace filtering or ranking.

## I.4 SELF-REFINEMENT AND ERROR CORRECTION

Self-refinement methods enable LLMs to iteratively improve their outputs through feedback loops (Madaan et al., 2023). However, recent work reveals fundamental limitations: Seo et al. (2024) demonstrate that refined code is not always superior to original versions, motivating learned stopping criteria rather than fixed iteration counts. The AutoCrit framework (Sang, 2025) introduces meta-reasoning with dedicated critique agents and execution monitors, achieving 12-18% accuracy improvements by catching mistakes 'in the moment.' This approach requires expensive additional model calls; CoRefine achieves similar benefits through lightweight confidence-based control.

Recent confidence-guided refinement methods provide complementary perspectives: ConCISE (Qiao et al., 2025) monitors step-wise confidence for deficits, triggering early stopping or confidence phrase insertion to achieve ∼ 50% token-length savings. CISC (Taubenfeld et al., 2025) uses softmax-normalized confidence for weighted voting, reducing sampling costs by 40% while preserving accuracy. C2R (Jang et al., 2025) curates diverse sub-question chains and selects those with sufficiently high confidence margins for zero-shot QA. Cost-effective refinement remains an active area: CERET (Cai et al., 2024) provides extrinsic refinement using semantic stability and entailment without iterative LLM inference, while Tang et al. (2024) frame code repair as an explorationexploitation tradeoff via Thompson Sampling. CoRefine's RETHINK vs. ALTERNATIVE decision space embodies this tradeoff with learned confidence-based routing.

## I.5 ERROR PROPAGATION IN REASONING

A critical challenge in multi-step reasoning is error propagation-the tendency for early mistakes to amplify downstream (Sang, 2025). Feng et al. (2025) study misinformation propagation in LLM reasoning chains, finding that models fail to correct errors over half the time and that early factual corrections are the most effective mitigation. This motivates CoRefine's design: confidence-guided intervention enables early detection and correction before errors compound. The FAIR-RAG framework (Aghajani Asl et al., 2025) addresses related issues through structured evidence assessment gating, while AgentErrorBench (Liang et al., 2025) demonstrates that systematic learning from failures can improve agent success rates by 26%. CoRefine's controller learns similar patterns from historical trajectories, enabling proactive intervention based on confidence signals.

## I.6 CONTEXT ENGINEERING FOR REFINEMENT

Effective refinement requires managing context across iterations. Anthropic (2025) identify context engineering as essential for AI agents, noting that LLMs have finite attention budgets and suffer from 'context rot' as token counts increase. Key strategies include compaction, summarization, and treating the file system as unlimited context (Thatipalli, 2025). CoRefine's synthesis prompts implement these principles: previous reasoning attempts are compacted into high-signal summaries that extract key insights, identified errors, and promising directions, avoiding the 'lost in the middle' problem while preserving actionable information for subsequent iterations.

## J FAILURE ANALYSIS

## J.1 COMMON FAILURE MODES

False HALT. The controller occasionally halts prematurely when an incorrect answer has high confidence (overconfident wrong answer). This occurs in approximately 5% of cases.

Excessive iteration. On some problems, the controller fails to converge to HALT, reaching the maximum iteration limit. This typically occurs when confidence oscillates between moderate values.

Answer extraction errors. Complex mathematical expressions with nested braces can cause answer extraction failures, leading to false 'inconsistent answer' signals.

## J.2 MITIGATION STRATEGIES

- CoRefine Tree (Hybrid mode): Use branching refinement with K = 4 warmup traces and branch factor B = 2 , with early stopping when halt rate &gt; 50%. This provides robustness while maintaining token efficiency.
- Answer consistency override: If same answer appears 3+ times, force HALT regardless of confidence
- Maximum iteration cap: Enforce reasonable upper bound (20 iterations) to prevent runaway computation

## K REPRODUCIBILITY

## K.1 DATASET INFORMATION

All evaluation datasets are publicly available:

- AIME 2024/2025: https://artofproblemsolving.com/wiki/index.php/AIME
- HMMT2025: https://hmmt.org
- BRUMO 2025: https://brumo.org

## K.2 COMPUTE REQUIREMENTS

- Controller training: &lt;1 GPU-hour on A100
- Full benchmark evaluation: ∼ 24 GPU-hours
- Minimum requirements: Single GPU with 24GB VRAM (with model quantization)