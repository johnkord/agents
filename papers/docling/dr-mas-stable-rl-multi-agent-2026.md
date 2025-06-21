## Dr. MAS: Stable Reinforcement Learning for Multi-Agent LLM Systems

Lang Feng 1 , Longtao Zheng 1 , Shuo He 1 , Fuxiang Zhang 1 , BoAn 1

1 Nanyang Technological University, Singapore

Multi-agent LLM systems enable advanced reasoning and tool use via role specialization, yet reliable reinforcement learning (RL) post-training for such systems remains difficult. In this work, we theoretically pinpoint a key reason for training instability when extending group-based RL to multiagent LLM systems. We show that under GRPO-style optimization, a global normalization baseline may deviate from diverse agents' reward distributions, which ultimately leads to gradient-norm instability. Based on this finding, we propose Dr. MAS, a simple and stable RL training recipe for multi-agent LLM systems. Dr. MAS uses an agent-wise remedy: normalizing advantages per agent using each agent's own reward statistics, which calibrates gradient scales and dramatically stabilizes training, both theoretically and empirically. Beyond the algorithm, Dr. MAS provides an end-to-end RL training framework for multi-agent LLM systems, supporting scalable orchestration, flexible per-agent LLM serving and optimization configs, and shared resource scheduling of LLM actor backends. We evaluate Dr. MAS on multi-agent math reasoning and multi-turn search benchmarks using Qwen2.5 and Qwen3 series models. Dr. MAS achieves clear gains over vanilla GRPO (e.g., +5.6% avg@16 and +4.6% pass@16 on math, and +15.2% avg@16 and +13.1% pass@16 on search) while largely eliminating gradient spikes. Moreover, it remains highly effective under heterogeneous agent-model assignments while improving efficiency.

Date:

February 9, 2026

Correspondence:

Bo An at boan@ntu.edu.sg

Author emails:

{lang005, longtao001}@e.ntu.edu.sg

/github Code:

https://github.com/langfengQ/DrMAS

## 1 Introduction

Large Language Models (LLMs) (Achiam et al., 2023; Team et al., 2023; Liu et al., 2024; Yang et al., 2025) have demonstrated impressive reasoning abilities across diverse domains (e.g., question answering, code generation), but many practical applications involve complex scenarios where multiple agents need to interact and coordinate. For example, tasks like complex information retrieval (Chang et al., 2025; Zhang et al., 2025d), agentic software engineering (Hong et al., 2024; Qian et al., 2024), and open-ended device control (Wang et al., 2024; Tan et al., 2025) involve multiple participants working together over extended horizons. Organizing LLMs into a Multi-Agent System (MAS), where each agent specializes in a subtask or role, has become a trend to handle complex real-world tasks more effectively (Tran et al., 2025; Zhang et al., 2025a).

Reinforcement Learning (RL) (Sutton &amp; Barto, 2018) now plays a foundational role in LLM post-training. Despite its growing importance, the extension of RL training to multi-agent LLM systems remains largely underexplored from both algorithmic and system perspectives. On the algorithmic side, while group-based RL methods like Group Relative Policy Optimization (GRPO) (Shao et al., 2024) excel in single-agent scenarios, adapting them to multi-agent settings introduces significant challenges due to the frequent instability observed across various scenarios (Chen et al., 2025; Zhao et al., 2025; Yuan et al., 2025). Agents are often invoked at different frequencies, leading to heterogeneous data distributions that greatly complicate end-toend optimization (Hong et al., 2025). On the system side, recent large-scale RL post-training frameworks, like veRL (Sheng et al., 2024), ROLL (Wang et al., 2025b), and AReaL (Fu et al., 2025), provide flexible, high-throughput training pipelines for LLMs, but are largely designed around optimizing a single LLM actor. They generally lack the native support for efficient multi-agent orchestration and multiple LLMs' co-training,

restricting the flexible scheduling and resource sharing required for heterogeneous agent configurations.

In this work, we theoretically identify that applying vanilla GRPO to train multi-agent LLM systems introduces systematic gradient variance and destabilizes training. We provide rigorous mathematical and empirical analysis, demonstrating that using a global advantage baseline across agents can inflate the second moment of their gradients, leading to gradient-norm explosion. Building on this analysis, we propose Dr. MAS, a simple and stable RL training recipe for multi-agent LLM systems. Dr. MAS adopts a straightforward yet effective remedy: each agent normalizes rewards using its own mean and variance. Concretely, we group action experience by agent so that each policy's advantage estimates are normalized with respect to its own data distribution. This calibration balances per-agent gradients, thus resulting in a dramatic reduction in variance for the policy gradient estimator. Beyond the algorithm itself, Dr. MAS also provides an end-to-end RL training framework tailored for multi-agent LLM systems. It supports scalable multi-agent orchestration, flexible agent-model assignment with optional LLM sharing (e.g., co-training 7B and 3B models), per-agent configuration of optimization, and shared resource pooling for efficient scheduling of LLM actor backends. The result is a unified system that maintains well-conditioned gradients and high hardware efficiency while enabling stable co-training across multiple LLM agents.

We evaluate Dr. MAS on role-specialized multi-agent systems for math reasoning and multi-turn search, using Qwen2.5 (Bai et al., 2025) and Qwen3 (Yang et al., 2025) series models, under both LLM-sharing and non-sharing settings. Across tasks and settings, Dr. MAS consistently improves the performance over vanilla GRPO (e.g., +5.6% avg@16 and +4.6% pass@16 on math, and +15.2% avg@16 and +13.1% pass@16 on search). We also observe markedly improved stability, with gradient-norm spikes largely eliminated. Furthermore, Dr. MAS remains highly effective under heterogeneous agent-model assignments, enabling smaller models for lower-level agents' decisions while improving overall system efficiency.

## 2 Related Work

Reinforcement Learning for LLMs. Beyond early alignment-focused approaches such as RLHF (Ziegler et al., 2019; Stiennon et al., 2020; Ouyang et al., 2022; Rafailov et al., 2024), recent work studies Reinforcement Learning from Verifiable Rewards (RLVR), which leverages automatically checkable signals (e.g., correctness in math or code) to improve LLM capabilities (Zeng et al., 2025). Within this setting, group-based RL has emerged as a strong alternative to classical actor-critic algorithms like PPO (Schulman et al., 2017). Techniques such as GRPO (Shao et al., 2024), RLOO (Kool et al., 2019; Ahmadian et al., 2024), Dr. GRPO (Liu et al., 2025c), DAPO (Yu et al., 2025), and GSPO (Zheng et al., 2025) aggregate multiple rollouts for the same query and perform relative comparisons within the group, thereby avoiding explicit value-function learning. RLVR has also been extended to agentic, multi-turn settings where LLMs act as automatic agents (Zhou et al., 2024; Bai et al., 2024; Feng et al., 2025a; Wang et al., 2025a; Zhang et al., 2025b; Feng et al., 2026). GRPO-style training has been widely applied to tool use (Qian et al., 2025; Xue et al., 2025b), OS control (Lai et al., 2025), and gaming (Wang et al., 2025c). Further variants refine group construction or objectives, such as GiGPO (Feng et al., 2025b) and ARPO (Dong et al., 2025).

Reinforcement Learning for Multi-Agent LLMs. Recent RL post-training has expanded from single-agent scenarios to learning coordination in role-specialized, multi-turn multi-agent systems. Self-play training (e.g., SPIRAL (Liu et al., 2025a), and MARSHAL (Yuan et al., 2025) use multi-turn dynamics to generate curricula and rewards with minimal manual labeling. However, these approaches are often confined to dyadic (two-agent) self-play scenarios. To ease deployment, Chain-of-Agents (Li et al., 2025a) distills multi-agent trajectories into a single agentic policy. Meanwhile, group-relative optimization has been extended to multi-agent settings (Liu et al., 2025b; Chen et al., 2025; Li et al., 2025b; Park et al., 2025; Wan et al., 2025; Xue et al., 2025a; Mo et al., 2025; Hong et al., 2025), but these methods typically rely on heuristics and lack stability guarantees. Dr. MAS distinguishes itself by theoretically identifying gradient-norm inflation as the root cause of instability and proposing a simple yet rigorous agent-wise solution.

ReinforcementLearningInfrastructure. As RL post-training scales, infrastructure has shifted toward optimizing the end-to-end rollout-train loop , where throughput, scheduling, and variable-length generation dominate system efficiency. General-purpose stacks such as veRL (Sheng et al., 2024), OpenRLHF (Hu et al., 2024), ROLL (Wang et al., 2025b), slime (Zhu et al., 2025), and AReaL (Fu et al., 2025) increasingly provide modular

pipeline abstractions and distributed execution to improve utilization under heavy sampling. As agentic use cases grow, frameworks increasingly emphasize multi-turn rollout and tool integration (e.g., verl-agent (Feng et al., 2025b), VerlTool (Jiang et al., 2025), Agent-Lightning (Luo et al., 2025)), with MARTI (Zhang et al., 2025c) and PettingLLMs (Zhao et al., 2025) offering a practical multi-agent training interface. However, they either provide limited support for heterogeneous model assignments or lack a shared resource pool for efficient scheduling. Our Dr. MAS addresses both to improve scalability and utilization in MAS post-training.

## 3 Preliminaries

## 3.1 Multi-Agent LLMs

̸

We consider a cooperative multi-agent LLM system consisting of K distinct LLM agents π θ 1 , π θ 2 , . . . , π θ K , each parameterized by its own LLM weights θ k . The agents jointly engage in solving complex tasks sampled from a distribution x ∈ p ( X ) . Each full interaction process (trajectory) produces a single outcome reward R ∈ R to indicate success or failure. During task completion, the agents' joint interaction unfolds as a trajectory τ = { ( s 1 , a 1 , k 1 ) , ( s 2 , a 2 , k 2 ) , . . . , ( s T , a T , k T ) } , where s t denotes the conversational or contextual state (e.g., dialogue history, task prompt, or shared memory) at execution step t , a t is the text output produced, and k t ∈ 1 , . . . , K denotes which LLM agent was active at step t . The active agent can change dynamically across the trajectory, for instance, in a hierarchical multi-agent framework, a high-level planner LLM may decide which sub-agent executes at each step. Hence, the index k t explicitly denotes the identity of the agent executing at each step. The execution LLM agent k t generates its output based on its policy a t ∼ π θ k t ( · | s t ) . Depending on the system design, the agents may share parameters (i.e., θ 1 = · · · = θ K ) , differing in role-specific prompts, enabling efficient adaptation under a unified LLM, or they may maintain distinct parameters ( θ i = θ j ) to specialize in heterogeneous sub-tasks, allowing diverse reasoning capabilities across agents.

## 3.2 Group Relative Policy Optimization

Group-based RL methods like Group Relative Policy Optimization (GRPO) (Shao et al., 2024) optimize policies by comparing multiple rollouts generated from the same task instruction and normalizing their rewards within each group, thereby avoiding explicit value-function estimation. Formally, given a task instruction x , the multi-agent LLM system samples a set of N trajectories

<!-- formula-not-decoded -->

where k i t ∈ { 1 , . . . , K } denotes the active agent at step t of trajectory i . Each trajectory τ i receives a scalar terminal reward R i = R ( τ i ) ∈ R that measures the overall quality of the generated outcome. The normalized advantage for each trajectory is computed using the group's mean and standard deviation:

<!-- formula-not-decoded -->

This advantage is then propagated to all agent outputs that contributed to the trajectory. Formally, we define the set of outputs of agent k as Y k = { a i t | k i t = k } , i.e., the collection of all time steps ( i, t ) across the group at which agent k produces an action. Notably, agents are often invoked at different frequencies, which results in varying sample sizes |Y k | . The RL objective for agent k is then given by

<!-- formula-not-decoded -->

where ρ θ k ( a i t ) = π θ k ( a i t | s i t ) π θ k old ( a i t | s i t ) is the importance sampling ratio. Here, we omit the KL-divergence regularization for notational brevity.

## 4 Methodology

In a multi-agent LLM system, different agents often specialize in distinct functions (e.g., information retrieval vs. answer synthesis, high-level planning vs. low-level execution), and consequently can exhibit substantially different reward distributions. We find that using vanilla GRPO with the global baseline ( µ, σ ) for all agents can be suboptimal: some agents may consistently operate in reward distributions above the global mean, while others remain below it. This persistent bias in how advantages are normalized can introduce a deterministic shift in the effective advantages seen by each agent, which in turn can inflate gradient-estimator variance and destabilize training.

Figure 1 Algorithm comparison. (a) GRPO with global baseline ( µ, σ ) can cause unstable gradient norm. (b) Dr. MAS with per-agent normalization ( µ k , σ k ) stabilizes the training of MAS.

<!-- image -->

In this section, we introduce Dr. MAS by ( 1 ) theoretically formalizing the instability and analyzing the second moment of the per-agent gradient under GRPO optimization (Section 4.1); ( 2 ) proposing an agent-wise remedy that calibrates each agent's advantage using its own reward statistics, thereby improve the training stability (Section 4.2); and ( 3 ) describing a system framework that implements efficient end-to-end RL training recipe for multi-agent LLM systems (Section 4.3). The complete pseudo code of Dr. MAS is provided in Appendix D.

## 4.1 Risk of Gradient Norm Explosion

To focus on how advantage normalization causes the instability in MAS training, we perform a theoretical analysis of the gradient norm. We base our analysis on the unclipped GRPO gradient (clipping and other regularization only further bound the update and are orthogonal to the gradient issue we study). For each agent k , and for each step ( i, t ) such that k i t = k , we define the score function as z ( k ) i,t ≜ ∇ θ k ρ θ k ( a i t ) and corresponding (unclipped) GRPO gradient contribution as

<!-- formula-not-decoded -->

Here, ( µ, σ ) are the mean and standard deviation used by vanilla GRPO. We assume that each agent's score function has a bounded second moment:

Assumption 4.1. For each agent k , there exists a constant C k &lt; ∞ such that E [ ∥ z ( k ) i,t ∥ 2 ] ≤ C k .

Then we can express the second moment of the per-agent gradient as follows.

Lemma 4.2. Under Assumptions 4.1, for any agent k ,

<!-- formula-not-decoded -->

where µ k ≜ 1 |Y k | ∑ a i t ∈Y k R i , σ 2 k ≜ 1 |Y k | ∑ a i t ∈Y k ( R i -µ k ) 2 are the mean and variance when sampling time steps uniformly from Y k (i.e., when agent k is active). ∆ k is a score-reward covariance correction term.

See Appendix A.1 for the proof. Lemma 4.2 separates the per-agent gradient norm into a dominant scaling factor and a residual covariance correction. The multiplicative factor ( σ 2 k + ( µ k -µ ) 2 ) /σ 2 grows when agent k operates in a reward distribution whose mean is far from the global mean or agent k 's conditional reward variance is much larger than the global variance. The term ∆ k captures the residual score-reward correlation. In large-scale LLM training, rewards are typically low-dimensional signals of final task quality (e.g., pass/fail for reasoning, correctness for coding), while z ( k ) i,t depends mainly on the local token-level stochasticity of the policy. Empirically, their covariance is often much smaller than the dominant scaling factor E k [ ∥ z ( k ) i,t ∥ 2 ]( σ 2 k +( µ k -µ ) 2 ) /σ 2 . This decomposition reveals the intrinsic instability of global normalization of GRPO in heterogeneous multi-agent training: a large deviation in the dominant scaling factor can inflate the gradient and lead to unstable updates. We formalize this phenomenon below.

Figure 2 Overview of multi-agent LLM RL framework. A multi-agent orchestrator manages distributed rollouts, agents are mapped to LLM worker groups with optional LLM sharing, and a shared resource pool schedules actor backends for efficient inference and per-model optimization.

<!-- image -->

Proposition 4.3 (Gradient-Norm Inflation) . As either the normalized mean deviation | µ k -µ | /σ or the normalized variance ratio σ 2 k /σ 2 becomes large, the second moment of ˜ g global k grows at least linearly. Consequently, along any training process for which there exists a sequence of iterations indexed by m such that

<!-- formula-not-decoded -->

where ˜ g global m = (˜ g global 1 ,m , . . . , ˜ g global K,m ) stacking all LLM agents' gradients.

The proof is provided in Appendix A.2. Proposition 4.3 demonstrates that gradient-norm inflation can be triggered by any agent whose reward distribution is poorly aligned with the global baseline. In practice, the gradient norms in such cases typically do not reach mathematical infinity. However, they often grow large enough and trigger severe gradient spikes, thus destabilizing the training process of the entire multi-agent LLM system.

## 4.2 Agent-Wise Remedy

Fortunately, Proposition 4.3 suggests a straightforward and effective remedy: calibrating each agent's advantages using reward statistics computed exclusively on the steps where that agent is active. Specifically, we replace the global baseline ( µ, σ ) with ( µ k , σ k ) , which ensures that ( σ 2 k +( µ k -µ ) 2 ) /σ 2 = 1 . In practice, this corresponds to normalizing each agent's reward using its own empirical mean and variance:

<!-- formula-not-decoded -->

where µ k ≜ 1 |Y k | ∑ a i t ∈Y k R i and σ 2 k ≜ 1 |Y k | ∑ a i t ∈Y k ( R i -µ k ) 2 . Therefore, an analysis analogous to Lemma 4.2 yields

<!-- formula-not-decoded -->

where ˜ g agent k = R i -µ k σ k z ( k ) i,t . Thus, under agent-wise normalization, the second moment of each agent's gradient is bounded purely by its own score statistics. Crucially, this effect is inherently multi-agent : as the number of specialized agents increases and their roles become more heterogeneous, a single global baseline is increasingly likely to be badly aligned with some agents, leading to gradient norm explosion. As shown in Figure 1, a simple agent-wise remedy, by adapting to each agent's own statistics, achieves keeping all gradients in a comparable, well-conditioned range, while still enabling cooperative optimization of the overall multi-agent LLM system.

## 4.3 Framework for Multi-Agent LLM RL

We next present a unified system framework that realizes end-to-end RL post-training for multi-agent LLMs. As illustrated in Figure 2, the system is designed to ensure well-conditioned gradient updates across agents, while maintaining scalable orchestration, flexible agent-model assignment, per-agent optimization configs, and efficient hardware utilization for multi-agent rollouts.

Figure 3 Illustration of the orchestrations. Left : Math orchestration uses a two-agent loop, where a solver proposes candidate solutions and a verifier evaluates and either approves or requests refinement. Right : Multi-turn search orchestration uses a hierarchical three-agent pipeline, where a top-level verifier selectively invokes either a search agent to retrieve external information or an answer agent to produce the final result.

<!-- image -->

Multi-Agent Orchestration. Our system is coordinated by a multi-agent trajectory collector, which manages the distributed interaction between the multi-agent LLM system and the environment. It delegates the rollout to a user-defined multi-agent orchestra, which governs the agent roles and execution flow. The orchestra dynamically selects and invokes agent policies based on the current state or prior agent outputs, enabling flexible and conditional control over multi-agent decision-making.

Agent-Model Assignment. A core assignment logic maps logical agents ( 1 , . . . , K ) to physical LLM worker groups ( wg\_id ). In non-shared settings, each agent k is assigned a distinct worker group (e.g., 7B and 3B models). Conversely, in shared settings, all agents configured with the same model are mapped to a single, shared worker group, allowing joint training and inference while reusing model weights.

Per-Agent Configuration. Dr. MAS supports agent-specific training hyperparameters for granular control. This allows configurations like actor.optim.lr to be specified on a per-agent basis. Our system injects the k -th hyperparameter set into the configuration for agent k , which is then attached to its corresponding LLM work group. A runtime check ensures that all agents sharing the same worker group utilize identical configurations.

Shared Resource Pooling and Scheduling. This component decouples logical agent-model assignments from physical resource placement. A resource pool manager provisions hardware resources (e.g., GPUs) into named pools. All LLM actor backends (one for each wg\_id ) are mapped to the ActorRollout role. To support high-throughput and low-latency decoding in multi-agent rollouts, these actor backends use sglang (Zheng et al., 2024) as the inference engine. This allows them to be co-provisioned within the same shared resource pool using Ray placement groups, enabling scalable scheduling of multiple concurrent LLMs. Agent calls are routed by an agent\_to\_wg\_mapping ( agent\_id → wg\_id ). This mapping dynamically dispatches the agent's generation request to the correct backend worker group ( actor\_rollout\_wg[wg\_id] ). In the optimization phase, the trainer partitions the aggregated batch B into per-model micro-batches B wg according to their worker group ID. Policy updates are then performed for each worker group, ensuring that gradients from an agent's trajectories only update its designated LLM backend.

## 5 Experiment

In this section, we evaluate Dr. MAS on two multi-agent orchestrations: a two-agent loop pipeline for math reasoning and a three-agent hierarchical pipeline for multi-turn search (Figure 3), under both LLM sharing and non-sharing settings. Specifically, we aim to demonstrate: ( 1 ) the consistent performance gains of Dr. MAS over vanilla GRPO; ( 2 ) more stable training dynamics and smoother gradient norms of Dr. MAS; ( 3 ) the individual contribution of each normalization component via a detailed ablation study; and ( 4 ) the practical efficiency and compatibility of Dr. MAS when applied to heterogeneous agent-model assignments.

## 5.1 Math

MathOrchestration. We first evaluate Dr. MAS on challenging mathematical reasoning tasks using a two-agent architecture (a solver agent and a verifier agent ), as shown in Figure 3. In each episode, the solver agent proposes candidate solutions, while the verifier agent inspects the solver's reasoning and decides whether the current solution should be accepted or revised. If the verifier deems the solution unsatisfactory, the system triggers another round of solver refinement. Otherwise, the interaction terminates and the final answer is

Table 1 Math results on Qwen3-4B/8B. We report the avg@16 and pass@16 of single-agent training with GRPO and multi-agent training under LLM sharing/non-sharing, using vanilla GRPO and Dr. MAS. Subscripts for Dr. MAS denote ∆ over the vanilla GRPO under the same multi-agent setting.

|           | Single-Agent   | Single-Agent   | Multi-Agent &LLMSharing   | Multi-Agent &LLMSharing   | Multi-Agent &LLMSharing   | Multi-Agent &LLMSharing   | Multi-Agent &LLMNon-Sharing   | Multi-Agent &LLMNon-Sharing   | Multi-Agent &LLMNon-Sharing   | Multi-Agent &LLMNon-Sharing   |
|-----------|----------------|----------------|---------------------------|---------------------------|---------------------------|---------------------------|-------------------------------|-------------------------------|-------------------------------|-------------------------------|
| Benchmark | GRPO           | GRPO           | GRPO                      | GRPO                      | Dr. MAS                   | Dr. MAS                   | GRPO                          | GRPO                          | Dr. MAS                       | Dr. MAS                       |
|           | avg@16         | pass@16        | avg@16                    | pass@16                   | avg@16                    | pass@16                   | avg@16                        | pass@16                       | avg@16                        | pass@16                       |
| Qwen3-4B  | Qwen3-4B       | Qwen3-4B       | Qwen3-4B                  | Qwen3-4B                  | Qwen3-4B                  | Qwen3-4B                  | Qwen3-4B                      | Qwen3-4B                      | Qwen3-4B                      | Qwen3-4B                      |
| AIME'24   | 38.8           | 63.3           | 39.3                      | 64.0                      | 39.3 0 . 0                | 63.3 ↓ 0 . 7              | 42.7                          | 73.3                          | 46.9 ↑ 4 . 2                  | 80.0 ↑ 6 . 7                  |
| AIME'25   | 33.1           | 56.7           | 31.4                      | 53.3                      | 38.1 ↑ 6 . 7              | 63.3 ↑ 10 . 0             | 35.6                          | 63.3                          | 38.1 ↑ 2 . 5                  | 66.7 ↑ 3 . 4                  |
| AMC'23    | 83.5           | 95.0           | 85.6                      | 95.0                      | 87.3 ↑ 1 . 7              | 95.0 0 . 0                | 83.5                          | 95.0                          | 89.5 ↑ 6 . 0                  | 97.5 ↑ 2 . 5                  |
| MATH500   | 89.0           | 94.2           | 89.5                      | 96.2                      | 90.5 ↑ 1 . 0              | 96.0 ↓ 0 . 2              | 89.6                          | 95.0                          | 92.4 ↑ 2 . 8                  | 97.0 ↑ 2 . 0                  |
| Minerva   | 37.9           | 49.6           | 37.5                      | 50.0                      | 40.9 ↑ 3 . 4              | 53.3 ↑ 3 . 3              | 37.5                          | 50.7                          | 39.0 ↑ 1 . 5                  | 51.5 ↑ 0 . 8                  |
| Olympiad  | 53.3           | 66.5           | 57.6                      | 65.6                      | 58.2 ↑ 0 . 6              | 68.6 ↑ 3 . 0              | 56.3                          | 68.9                          | 60.9 ↑ 4 . 6                  | 73.6 ↑ 4 . 7                  |
| Average   | 55.9           | 70.9           | 56.8                      | 70.7                      | 59.0 ↑ 2 . 2              | 73.2 ↑ 2 . 6              | 57.5                          | 74.4                          | 61.1 ↑ 3 . 6                  | 77.7 ↑ 3 . 3                  |
| Qwen3-8B  | Qwen3-8B       | Qwen3-8B       | Qwen3-8B                  | Qwen3-8B                  | Qwen3-8B                  | Qwen3-8B                  | Qwen3-8B                      | Qwen3-8B                      | Qwen3-8B                      | Qwen3-8B                      |
| AIME'24   | 36.0           | 67.3           | 42.7                      | 66.7                      | 54.8 ↑ 12 . 1             | 80.0 ↑ 13 . 3             | 42.9                          | 70.0                          | 44.6 ↑ 1 . 7                  | 73.3 ↑ 3 . 3                  |
| AIME'25   | 32.7           | 50.0           | 31.4                      | 53.3                      | 39.4 ↑ 8 . 0              | 70.0 ↑ 16 . 7             | 31.8                          | 53.3                          | 41.5 ↑ 9 . 7                  | 56.7 ↑ 3 . 4                  |
| AMC'23    | 87.0           | 95.0           | 87.3                      | 95.0                      | 88.9 ↑ 1 . 6              | 97.5 ↑ 2 . 5              | 86.1                          | 95.0                          | 87.5 ↑ 1 . 4                  | 95.0 0 . 0                    |
| MATH500   | 89.9           | 94.8           | 89.6                      | 96.2                      | 91.3 ↑ 1 . 7              | 96.0 ↓ 0 . 2              | 90.5                          | 96.6                          | 90.7 ↑ 0 . 2                  | 96.2 ↓ 0 . 4                  |
| Minerva   | 36.0           | 46.7           | 37.5                      | 50.0                      | 39.9 ↑ 2 . 4              | 49.6 ↓ 0 . 4              | 39.2                          | 50.7                          | 40.9 ↑ 1 . 7                  | 54.0 ↑ 3 . 3                  |
| Olympiad  | 57.9           | 67.5           | 58.2                      | 71.4                      | 59.3 ↑ 1 . 1              | 72.4 ↑ 1 . 0              | 58.2                          | 67.6                          | 59.0 ↑ 0 . 8                  | 70.2 ↑ 2 . 6                  |
| Average   | 56.6           | 70.2           | 57.8                      | 72.1                      | 62.3 ↑ 4 . 5              | 77.6 ↑ 5 . 5              | 58.1                          | 72.2                          | 60.7 ↑ 2 . 6                  | 74.2 ↑ 2 . 0                  |

emitted. We use Qwen3-4B/8B (Yang et al., 2025) as the LLM policy for each agent and evaluate both the shared-LLM and non-shared settings.

Setup. For training, we adopt the training corpus from DAPO-Math (Yu et al., 2025), which consists of diverse math problems paired with verifiable solutions and reward signals. The rollout group size is set to 8. For evaluation, we report the avg@16 and pass@16 results on a suite of competitive benchmarks: AIME'24, AIME'25, AMC'23, MATH500 (Hendrycks et al., 2021), Minerva, and OlympiadBench (He et al., 2024). All other experimental details are available in Appendix B.1.

Results. As shown in Table 1, Dr. MAS improves over vanilla GRPO under both LLM sharing and LLM non-sharing. While applying GRPO directly to the multi-agent setting can reach decent average scores, the gains are not always consistent across benchmarks, and some hard splits may not improve. This suggests that a single global normalization of GRPO can make multi-agent training less reliable. In contrast, Dr. MAS uses per-agent normalization to keep each agent's update on a similar scale, which leads to more consistent improvements across datasets and settings (an overall increase of 5.6% in avg@16 and 4.6% in pass@16). For Qwen3-4B, Dr. MAS improves the performance under the sharing setting from 56.8/70.7 to 59.0/73.2 and improves the non-sharing setting from 57.5/74.4 to 61.1/77.7. The pronounced gain in the non-shared setting suggests that when agents possess independent parameters, their behavioral distributions diverge more significantly, making Dr. MAS's agent-specific calibration even more critical. Similarly, for Qwen3-8B, we observe strong gains in both configurations. The most significant improvements occur on the challenging AIME benchmarks (e.g., 42.7/66.7 → 54.8/80.0 on AIME'24), demonstrating that high-variance gradients from GRPO can easily disrupt the learning of fragile, long-horizon reasoning chains. Dr. MAS guarantees stable convergence, allowing agents to robustly learn the precise, multi-stage deductions.

## 5.2 Multi-Turn Search

Search Orchestration. We then evaluate Dr. MAS on the multi-turn search tool-calling task. To this end, we design a hierarchical workflow comprising three agents: a verifier agent , a search agent , and an answer agent , as shown in Figure 3. At the top level, the verifier agent determines whether the information currently available is sufficient to answer the query. If not, it delegates downward to the search agent, which is responsible for retrieving additional external evidence. Once the verifier agent judges that the information is adequate, it invokes the answer agent, which synthesizes all retrieved evidence into a final answer. We use

Table 2 Multi-turn search QA results on Qwen2.5-3B/7B. We report the avg@16 and pass@16 of single-agent training with GRPO and multi-agent training under LLM sharing/non-sharing, using vanilla GRPO and Dr. MAS. Subscripts for Dr. MAS denote ∆ over the vanilla GRPO under the same multi-agent setting.

|            | Single-Agent   | Single-Agent   | Multi-Agent &LLMSharing   | Multi-Agent &LLMSharing   | Multi-Agent &LLMSharing   | Multi-Agent &LLMSharing   | Multi-Agent &LLMNon-Sharing   | Multi-Agent &LLMNon-Sharing   | Multi-Agent &LLMNon-Sharing   | Multi-Agent &LLMNon-Sharing   |
|------------|----------------|----------------|---------------------------|---------------------------|---------------------------|---------------------------|-------------------------------|-------------------------------|-------------------------------|-------------------------------|
| Benchmark  | GRPO           | GRPO           | GRPO                      | GRPO                      | Dr. MAS                   | Dr. MAS                   | GRPO                          | GRPO                          | Dr. MAS                       | Dr. MAS                       |
|            | avg@16         | pass@16        | avg@16                    | pass@16                   | avg@16                    | pass@16                   | avg@16                        | pass@16                       | avg@16                        | pass@16                       |
| Qwen2.5-3B | Qwen2.5-3B     | Qwen2.5-3B     | Qwen2.5-3B                | Qwen2.5-3B                | Qwen2.5-3B                | Qwen2.5-3B                | Qwen2.5-3B                    | Qwen2.5-3B                    | Qwen2.5-3B                    | Qwen2.5-3B                    |
| NQ         | 40.6           | 54.7           | 41.0                      | 59.0                      | 43.8 ↑ 2 . 8              | 58.5 ↓ 0 . 5              | 43.8                          | 54.5                          | 44.6 ↑ 0 . 8                  | 58.1 ↑ 3 . 6                  |
| TriviaQA   | 58.1           | 68.8           | 57.9                      | 68.4                      | 61.7 ↑ 3 . 8              | 70.1 ↑ 1 . 7              | 60.6                          | 70.8                          | 61.1 ↑ 0 . 5                  | 71.7 ↑ 0 . 9                  |
| PopQA      | 44.2           | 49.6           | 43.2                      | 58.0                      | 45.0 ↑ 1 . 8              | 57.6 ↓ 0 . 4              | 45.6                          | 54.5                          | 46.5 ↑ 0 . 9                  | 57.4 ↑ 2 . 9                  |
| HotpotQA   | 31.8           | 40.9           | 32.5                      | 48.0                      | 33.3 ↑ 0 . 8              | 51.2 ↑ 3 . 2              | 32.5                          | 45.2                          | 35.3 ↑ 2 . 8                  | 51.1 ↑ 5 . 9                  |
| 2Wiki      | 29.9           | 43.7           | 33.7                      | 64.0                      | 34.1 ↑ 0 . 4              | 64.0 0 . 0                | 29.2                          | 48.9                          | 34.9 ↑ 5 . 7                  | 60.2 ↑ 11 . 3                 |
| MuSiQue    | 7.9            | 14.6           | 9.1                       | 26.5                      | 10.2 ↑ 1 . 1              | 25.8 ↓ 0 . 7              | 8.6                           | 19.2                          | 10.4 ↑ 1 . 8                  | 26.1 ↑ 6 . 9                  |
| Bamboogle  | 15.3           | 27.2           | 26.4                      | 46.4                      | 28.6 ↑ 2 . 2              | 49.6 ↑ 3 . 2              | 21.0                          | 33.6                          | 25.4 ↑ 4 . 4                  | 46.4 ↑ 12 . 8                 |
| Average    | 32.5           | 42.8           | 34.8                      | 52.9                      | 36.7 ↑ 1 . 8              | 53.8 ↑ 0 . 9              | 34.5                          | 46.7                          | 36.9 ↑ 2 . 4                  | 53.0 ↑ 6 . 3                  |
| Qwen2.5-7B | Qwen2.5-7B     | Qwen2.5-7B     | Qwen2.5-7B                | Qwen2.5-7B                | Qwen2.5-7B                | Qwen2.5-7B                | Qwen2.5-7B                    | Qwen2.5-7B                    | Qwen2.5-7B                    | Qwen2.5-7B                    |
| NQ         | 46.4           | 57.6           | 45.2                      | 60.0                      | 47.4 ↑ 2 . 2              | 60.7 ↑ 0 . 7              | 27.1                          | 39.0                          | 47.7 ↑ 20 . 6                 | 59.5 ↑ 20 . 5                 |
| TriviaQA   | 63.1           | 72.4           | 63.9                      | 70.9                      | 63.1 ↓ 0 . 8              | 71.2 ↑ 0 . 3              | 53.1                          | 64.4                          | 63.4 ↑ 10 . 3                 | 72.7 ↑ 8 . 3                  |
| PopQA      | 47.2           | 53.6           | 43.9                      | 55.0                      | 45.9 ↑ 2 . 0              | 57.3 ↑ 2 . 3              | 20.7                          | 27.9                          | 46.7 ↑ 26 . 0                 | 57.8 ↑ 29 . 9                 |
| HotpotQA   | 43.0           | 55.1           | 40.3                      | 55.0                      | 42.5 ↑ 2 . 2              | 56.0 ↑ 1 . 0              | 24.4                          | 36.2                          | 44.0 ↑ 19 . 6                 | 57.5 ↑ 21 . 3                 |
| 2Wiki      | 40.6           | 61.6           | 41.6                      | 67.8                      | 42.0 ↑ 0 . 4              | 67.1 ↓ 0 . 7              | 30.3                          | 51.2                          | 45.4 ↑ 15 . 1                 | 68.1 ↑ 16 . 9                 |
| MuSiQue    | 17.8           | 34.6           | 15.2                      | 31.7                      | 16.7 ↑ 1 . 5              | 32.1 ↑ 0 . 4              | 8.3                           | 18.1                          | 19.4 ↑ 11 . 1                 | 34.9 ↑ 16 . 8                 |
| Bamboogle  | 36.7           | 54.4           | 40.1                      | 58.4                      | 40.1 0 . 0                | 59.2 ↑ 0 . 8              | 31.9                          | 46.4                          | 39.8 ↑ 7 . 9                  | 57.6 ↑ 11 . 2                 |
| Average    | 42.1           | 55.6           | 41.5                      | 57.0                      | 42.5 ↑ 1 . 1              | 57.7 ↑ 0 . 7              | 28.0                          | 40.5                          | 43.8 ↑ 15 . 8                 | 58.3 ↑ 17 . 8                 |

Qwen2.5-3B/7B (Bai et al., 2025) as the LLM policy for each agent and evaluate both the shared-LLM and non-shared settings.

Setup. Our experimental setup follows Search-R1 (Jin et al., 2025). We employ E5 (Wang et al., 2022) as the retriever. The rollout group size is set to 5 and the max turn is set to 4. For evaluation, we consider both single-hop QA benchmarks (NQ (Kwiatkowski et al., 2019), TriviaQA (Joshi et al., 2017), PopQA (Mallen et al., 2022)) and multi-hop QA benchmarks (HotpotQA (Yang et al., 2018), 2WikiMultiHopQA (Ho et al., 2020), MuSiQue (Trivedi et al., 2022), Bamboogle (Press et al., 2022)) and report the avg@16 and pass@16 results. For training, we use the mixture of NQ and HotpotQA. All other experimental details are available in Appendix B.2.

Results. As shown in Table 2, the instability shows up more clearly in multi-turn search, as errors can snowball across tool calls and across agents. In this setting, vanilla GRPO is especially risky when LLMs are not shared, since each agent can drift and the same global scaling may no longer match their learning dynamics. A clear example of this failure occurs with Qwen2.5-7B (non-sharing), where vanilla GRPO learns to avoid calling search agents entirely due to high gradient norms, leading to a severe performance drop (28.0/40.5). In contrast, Dr. MAS effectively mitigates this risk and yields consistent improvements, with an overall increase of 15.2% in avg@16 and 13.1% in pass@16. Notably, Dr. MAS restores the performance of Qwen2.5-7B to 43.8/58.3, a result that not only far exceeds the vanilla baseline but also surpasses both the single-agent baseline and the shared-LLM setting. This trend, consistent with our observations in math tasks, highlights that LLM non-sharing can be hurt badly without proper stabilization, and reducing training noise at the agent level becomes crucial.

## 5.3 Gradient-Norm Instability

Next, we investigate the gradient-norm instability by tracking the training accuracy and per-agent gradient norms during RL post-training for the three-agent search orchestration (see Appendix E.1 for math results). As shown in Figure 4, vanilla GRPO induces frequent, high-magnitude gradient norm spikes. The search agent has the largest spikes (reaching very high values early and again around the middle of training), the answer agent also exhibits large spikes at the beginning, and the verifier shows noticeable peaks as well. These spikes

Figure 4 Comparison of training accuracy and gradient norm between GRPO and Dr. MAS. The results are recorded during multi-agent RL post-training for three-agent search orchestration under LLM non-sharing (Qwen2.5-3B).

<!-- image -->

mean that some steps produce unusually large updates, which makes training noisy and harder to control.

Dr. MAS mitigates this failure via the agent-wise remedy that normalizes advantages per agent, keeping per-agent update scales better calibrated. As illustrated in Figure 4, Dr. MAS keeps the gradient norms of all three agents much smoother and at a lower level throughout training, therefore achieving notable performance gains in Tables 1 and 2.

## 5.4 Ablation Study

In this part, we conduct an ablation study on the multi-turn search task using Qwen2.57B under the LLM non-sharing setting. We compare four advantage normalization configurations: global statistics ( µ, σ ) (i.e., GRPO), per-agent mean with global standard deviation ( µ k , σ ) , global mean with per-agent standard deviation ( µ, σ k ) , and fully per-agent normalization ( µ k , σ k ) (i.e., Dr. MAS).

As shown in Table 3, GRPO performs poorly, indicating that global normalization is a poor match for multi-agent LLM training where agents play different roles and thus exhibit different advantage distributions. Adding either per-agent mean or per-agent standard deviation already brings large improvements. The per-agent standard deviation ( µ, σ k ) brings a bigger gain, likely because agents differ more in the spread of their advantages than in the average level. Finally, combining both per-agent mean and standard deviation, Dr. MAS with fully agent-wise remedy ( µ k , σ k ) achieves the best results, showing that setting both the mean and the scale per agent gives the most reliable learning signal.

## 5.5 Heterogeneous Model Assignment

At last, we explore the practical efficiency of Dr. MAS when applied to heterogeneous agent-model assignments, where agents with different capacities are combined to optimize performance and cost. We compare a homogeneous baseline where all three agents (verifier, search, and answer) use Qwen2.5-7B, against a heterogeneous setting where the verifier uses Qwen2.5-7B while the search and answer agents use Llama-3.2-3B-Instruct (Grattafiori et al., 2024).

As shown in Figure 5, the heterogeneous system maintains performance levels nearly identical to the all-7B baseline, and the average token usage per trajectory remains compara-

Figure 5 Performance and efficiency comparison between homogeneous (all 7B models) and heterogeneous (7B for Verifier, 3B for Search/Answer) model assignment on search tasks. Token counts are the average tokens per trajectory for each agent. Cost ($) is estimated using OpenRouter market prices (7B: $0.30/M tokens, 3B: $0.06/M tokens) and reported as the total inference cost over the full test set (51.7k samples).

<!-- image -->

ble, with the heterogeneous setup even showing a slight reduction in total volume. This suggests that, in

Table3 Ablation study of different advantage normalization configurations on the search task. We report the avg@16 and pass@16 across all datasets. Subscripts denote ∆ over the vanilla GRPO.

| Metric   | Normalization Configuration   | Normalization Configuration   | Normalization Configuration   | Normalization Configuration   |
|----------|-------------------------------|-------------------------------|-------------------------------|-------------------------------|
| Metric   | ( µ,σ )                       | ( µ k ,σ )                    | ( µ,σ k )                     | ( µ k ,σ k )                  |
| avg@16   | 28.0                          | 39.1 ↑ 11 . 1                 | 42.9 ↑ 14 . 9                 | 43.8 ↑ 15 . 8                 |
| pass@16  | 40.5                          | 53.5 ↑ 13 . 0                 | 57.6 ↑ 17 . 1                 | 58.3 ↑ 17 . 8                 |

hierarchical multi-agent system, assigning a stronger model to the top-level verifier is sufficient to preserve overall decision quality. By deploying smaller, more efficient models to the low-level agents, the heterogeneous system achieved a 31.6% reduction in latency and a 41.8% reduction in total API cost. These findings demonstrate that strategic agent-model assignment facilitates a more flexible and cost-effective multi-agent deployment without sacrificing task precision.

## 6 Conclusions and Limitations

In this work, we studied RL post-training for multi-agent LLM systems and found that directly extending GRPO with a single global advantage baseline can be brittle when agents have different reward statistics, leading to gradient spikes and unstable post-training. To address this issue, we proposed Dr. MAS, which normalizes advantages for each agent using its own reward mean and variance, and we also built an end-to-end training framework that supports multi-agent orchestration, optional LLM sharing and non-sharing, per-agent optimization settings, and efficient resource pooling. Across a two-agent math loop and a three-agent multiturn search pipeline, Dr. MAS consistently improves over vanilla GRPO and yields more stable training under both sharing and non-sharing settings. Despite these improvements, Dr. MAS does not resolve all sources of instability in multi-agent LLM RL (e.g., credit assignment across agents and turns). Furthermore, although our framework supports flexible multi-agent orchestration and resource pooling, we have not evaluated settings with a much larger number of agents. In such scenarios, resource allocation and potential asynchronous execution issues may become more challenging and remain open questions for future work.

## References

- Josh Achiam, Steven Adler, Sandhini Agarwal, Lama Ahmad, Ilge Akkaya, Florencia Leoni Aleman, Diogo Almeida, Janko Altenschmidt, Sam Altman, Shyamal Anadkat, et al. GPT-4 technical report. arXiv preprint arXiv:2303.08774 , 2023.
- Arash Ahmadian, Chris Cremer, Matthias Gallé, Marzieh Fadaee, Julia Kreutzer, Olivier Pietquin, Ahmet Üstün, and Sara Hooker. Back to basics: Revisiting reinforce style optimization for learning from human feedback in LLMs. In Proceedings of the 62nd Annual Meeting of the Association for Computational Linguistics (Volume 1: Long Papers) , pp. 12248-12267, 2024.
- Hao Bai, Yifei Zhou, Jiayi Pan, Mert Cemri, Alane Suhr, Sergey Levine, and Aviral Kumar. DigiRL: Training in-the-wild device-control agents with autonomous reinforcement learning. In Advances in Neural Information Processing Systems , volume 37, pp. 12461-12495, 2024.
- Shuai Bai, Keqin Chen, Xuejing Liu, Jialin Wang, Wenbin Ge, Sibo Song, Kai Dang, Peng Wang, Shijie Wang, Jun Tang, et al. Qwen2.5-VL technical report. arXiv preprint arXiv:2502.13923 , 2025.
- Chia-Yuan Chang, Zhimeng Jiang, Vineeth Rakesh, Menghai Pan, Chin-Chia Michael Yeh, Guanchu Wang, Mingzhi Hu, Zhichao Xu, Yan Zheng, Mahashweta Das, et al. MAIN-RAG: Multi-agent filtering retrieval-augmented generation. In Proceedings of the 63rd Annual Meeting of the Association for Computational Linguistics (Volume 1: Long Papers) , pp. 2607-2622, 2025.
- Guanzhong Chen, Shaoxiong Yang, Chao Li, Wei Liu, Jian Luan, and Zenglin Xu. Heterogeneous group-based reinforcement learning for LLM-based multi-agent systems. arXiv preprint arXiv:2506.02718 , 2025.
- Guanting Dong, Hangyu Mao, Kai Ma, Licheng Bao, Yifei Chen, Zhongyuan Wang, Zhongxia Chen, Jiazhen Du, Huiyang Wang, Fuzheng Zhang, et al. Agentic reinforced policy optimization. arXiv preprint arXiv:2507.19849 , 2025.
- Lang Feng, Weihao Tan, Zhiyi Lyu, Longtao Zheng, Haiyang Xu, Ming Yan, Fei Huang, and Bo An. Towards efficient online tuning of VLM agents via counterfactual soft reinforcement learning. In International Conference on Machine Learning , volume 267, pp. 16884-16903, 2025a.
- Lang Feng, Zhenghai Xue, Tingcong Liu, and Bo An. Group-in-group policy optimization for LLM agent training. In Advances in Neural Information Processing Systems , 2025b.
- Lang Feng, Fuchao Yang, Feng Chen, Xin Cheng, Haiyang Xu, Zhenglin Wan, Ming Yan, and Bo An. AgentOCR: Reimagining agent history via optical self-compression. arXiv preprint arXiv:2601.04786 , 2026.

- Wei Fu, Jiaxuan Gao, Xujie Shen, Chen Zhu, Zhiyu Mei, Chuyi He, Shusheng Xu, Guo Wei, Jun Mei, Jiashu Wang, et al. AReaL: A large-scale asynchronous reinforcement learning system for language reasoning. arXiv preprint arXiv:2505.24298 , 2025.
- Aaron Grattafiori, Abhimanyu Dubey, Abhinav Jauhri, Abhinav Pandey, Abhishek Kadian, Ahmad Al-Dahle, Aiesha Letman, Akhil Mathur, Alan Schelten, Alex Vaughan, et al. The Llama 3 herd of models. arXiv preprint arXiv:2407.21783 , 2024.
- Chaoqun He, Renjie Luo, Yuzhuo Bai, Shengding Hu, Zhen Thai, Junhao Shen, Jinyi Hu, Xu Han, Yujie Huang, Yuxiang Zhang, et al. OlympiadBench: A challenging benchmark for promoting AGI with olympiad-level bilingual multimodal scientific problems. In Proceedings of the 62nd Annual Meeting of the Association for Computational Linguistics (Volume 1: Long Papers) , pp. 3828-3850, 2024.
- Dan Hendrycks, Collin Burns, Saurav Kadavath, Akul Arora, Steven Basart, Eric Tang, Dawn Song, and Jacob Steinhardt. Measuring mathematical problem solving with the math dataset. arXiv preprint arXiv:2103.03874 , 2021.
- Xanh Ho, Anh-Khoa Duong Nguyen, Saku Sugawara, and Akiko Aizawa. Constructing a multi-hop QA dataset for comprehensive evaluation of reasoning steps. arXiv preprint arXiv:2011.01060 , 2020.
- Haoyang Hong, Jiajun Yin, Yuan Wang, Jingnan Liu, Zhe Chen, Ailing Yu, Ji Li, Zhiling Ye, Hansong Xiao, Yefei Chen, et al. Multi-agent deep research: Training multi-agent systems with M-GRPO. arXiv preprint arXiv:2511.13288 , 2025.
- Sirui Hong, Mingchen Zhuge, Jonathan Chen, Xiawu Zheng, Yuheng Cheng, Jinlin Wang, Ceyao Zhang, Zili Wang, Steven Ka Shing Yau, Zijuan Lin, Liyang Zhou, Chenyu Ran, Lingfeng Xiao, Chenglin Wu, and Jürgen Schmidhuber. MetaGPT: Meta programming for a multi-agent collaborative framework. In The Twelfth International Conference on Learning Representations , 2024.
- Jian Hu, Xibin Wu, Zilin Zhu, Xianyu, Weixun Wang, Dehao Zhang, and Yu Cao. OpenRLHF: An easy-to-use, scalable and high-performance RLHF framework. arXiv preprint arXiv:2405.11143 , 2024.
- Dongfu Jiang, Yi Lu, Zhuofeng Li, Zhiheng Lyu, Ping Nie, Haozhe Wang, Alex Su, Hui Chen, Kai Zou, Chao Du, et al. VerlTool: Towards holistic agentic reinforcement learning with tool use. arXiv preprint arXiv:2509.01055 , 2025.
- Bowen Jin, Hansi Zeng, Zhenrui Yue, Dong Wang, Hamed Zamani, and Jiawei Han. Search-R1: Training LLMs to reason and leverage search engines with reinforcement learning. arXiv preprint arXiv:2503.09516 , 2025.
- Mandar Joshi, Eunsol Choi, Daniel S Weld, and Luke Zettlemoyer. TriviaQA: A large scale distantly supervised challenge dataset for reading comprehension. arXiv preprint arXiv:1705.03551 , 2017.
- Wouter Kool, Herke van Hoof, and Max Welling. Buy 4 reinforce samples, get a baseline for free! In ICLR 2019 Workshop , 2019.
- Tom Kwiatkowski, Jennimaria Palomaki, Olivia Redfield, Michael Collins, Ankur Parikh, Chris Alberti, Danielle Epstein, Illia Polosukhin, Jacob Devlin, Kenton Lee, et al. Natural questions: a benchmark for question answering research. Transactions of the Association for Computational Linguistics , 7:453-466, 2019.
- Hanyu Lai, Xiao Liu, Yanxiao Zhao, Han Xu, Hanchen Zhang, Bohao Jing, Yanyu Ren, Shuntian Yao, Yuxiao Dong, and Jie Tang. ComputerRL: Scaling end-to-end online reinforcement learning for computer use agents. arXiv preprint arXiv:2508.14040 , 2025.
- Weizhen Li, Jianbo Lin, Zhuosong Jiang, Jingyi Cao, Xinpeng Liu, Jiayu Zhang, Zhenqiang Huang, Qianben Chen, Weichen Sun, Qiexiang Wang, et al. Chain-of-agents: End-to-end agent foundation models via multi-agent distillation and agentic RL. arXiv preprint arXiv:2508.13167 , 2025a.
- Zhuofeng Li, Haoxiang Zhang, Seungju Han, Sheng Liu, Jianwen Xie, Yu Zhang, Yejin Choi, James Zou, and Pan Lu. In-the-flow agentic system optimization for effective planning and tool use. arXiv preprint arXiv:2510.05592 , 2025b.
- Aixin Liu, Bei Feng, Bing Xue, Bingxuan Wang, Bochao Wu, Chengda Lu, Chenggang Zhao, Chengqi Deng, Chenyu Zhang, Chong Ruan, et al. DeepSeek-V3 technical report. arXiv preprint arXiv:2412.19437 , 2024.
- Bo Liu, Leon Guertler, Simon Yu, Zichen Liu, Penghui Qi, Daniel Balcells, Mickel Liu, Cheston Tan, Weiyan Shi, Min Lin, et al. SPIRAL: Self-play on zero-sum games incentivizes reasoning via multi-agent multi-turn reinforcement learning. arXiv preprint arXiv:2506.24119 , 2025a.
- Shuo Liu, Tianle Chen, Zeyu Liang, Xueguang Lyu, and Christopher Amato. LLM collaboration with multi-agent reinforcement learning. arXiv preprint arXiv:2508.04652 , 2025b.

- Zichen Liu, Changyu Chen, Wenjun Li, Penghui Qi, Tianyu Pang, Chao Du, Wee Sun Lee, and Min Lin. Understanding R1-zero-like training: A critical perspective. arXiv preprint arXiv:2503.20783 , 2025c.
- Xufang Luo, Yuge Zhang, Zhiyuan He, Zilong Wang, Siyun Zhao, Dongsheng Li, Luna K Qiu, and Yuqing Yang. Agent lightning: Train any AI agents with reinforcement learning. arXiv preprint arXiv:2508.03680 , 2025.
- Alex Mallen, Akari Asai, Victor Zhong, Rajarshi Das, Daniel Khashabi, and Hannaneh Hajishirzi. When not to trust language models: Investigating effectiveness of parametric and non-parametric memories. arXiv preprint arXiv:2212.10511 , 2022.
- Zhanfeng Mo, Xingxuan Li, Yuntao Chen, and Lidong Bing. Multi-agent tool-integrated policy optimization. arXiv preprint arXiv:2510.04678 , 2025.
- Long Ouyang, Jeffrey Wu, Xu Jiang, Diogo Almeida, Carroll Wainwright, Pamela Mishkin, Chong Zhang, Sandhini Agarwal, Katarina Slama, Alex Ray, et al. Training language models to follow instructions with human feedback. In Advances in Neural Information Processing Systems , volume 35, pp. 27730-27744, 2022.
- Chanwoo Park, Seungju Han, Xingzhi Guo, Asuman E Ozdaglar, Kaiqing Zhang, and Joo-Kyung Kim. MAPoRL: Multi-agent post-co-training for collaborative large language models with reinforcement learning. In Proceedings of the 63rd Annual Meeting of the Association for Computational Linguistics (Volume 1: Long Papers) , pp. 30215-30248, 2025.
- Ofir Press, Muru Zhang, Sewon Min, Ludwig Schmidt, Noah A Smith, and Mike Lewis. Measuring and narrowing the compositionality gap in language models. arXiv preprint arXiv:2210.03350 , 2022.
- Chen Qian, Wei Liu, Hongzhang Liu, Nuo Chen, Yufan Dang, Jiahao Li, Cheng Yang, Weize Chen, Yusheng Su, Xin Cong, et al. ChatDev: Communicative agents for software development. In Proceedings of the 62nd Annual Meeting of the Association for Computational Linguistics (Volume 1: Long Papers) , pp. 15174-15186, 2024.
- Cheng Qian, Emre Can Acikgoz, Qi He, Hongru Wang, Xiusi Chen, Dilek Hakkani-Tür, Gokhan Tur, and Heng Ji. ToolRL: Reward is all tool learning needs. arXiv preprint arXiv:2504.13958 , 2025.
- Rafael Rafailov, Archit Sharma, Eric Mitchell, Christopher D Manning, Stefano Ermon, and Chelsea Finn. Direct preference optimization: Your language model is secretly a reward model. In Advances in Neural Information Processing Systems , volume 36, 2024.
- John Schulman, Filip Wolski, Prafulla Dhariwal, Alec Radford, and Oleg Klimov. Proximal policy optimization algorithms. arXiv preprint arXiv:1707.06347 , 2017.
- Zhihong Shao, Peiyi Wang, Qihao Zhu, Runxin Xu, Junxiao Song, Xiao Bi, Haowei Zhang, Mingchuan Zhang, YK Li, Y Wu, et al. DeepSeekMath: Pushing the limits of mathematical reasoning in open language models. arXiv preprint arXiv:2402.03300 , 2024.
- Guangming Sheng, Chi Zhang, Zilingfeng Ye, Xibin Wu, Wang Zhang, Ru Zhang, Yanghua Peng, Haibin Lin, and Chuan Wu. HybridFlow: A flexible and efficient RLHF framework. arXiv preprint arXiv:2409.19256 , 2024.
- Nisan Stiennon, Long Ouyang, Jeffrey Wu, Daniel Ziegler, Ryan Lowe, Chelsea Voss, Alec Radford, Dario Amodei, and Paul F Christiano. Learning to summarize with human feedback. In Advances in Neural Information Processing Systems , volume 33, pp. 3008-3021, 2020.
- Richard S Sutton and Andrew G Barto. Reinforcement Learning: An Introduction . MIT press, 2018.
- Weihao Tan, Wentao Zhang, Xinrun Xu, Haochong Xia, Ziluo Ding, Boyu Li, Bohan Zhou, Junpeng Yue, Jiechuan Jiang, Yewen Li, et al. Cradle: Empowering foundation agents towards general computer control. In International Conference on Machine Learning , 2025.
- Gemini Team, Rohan Anil, Sebastian Borgeaud, Jean-Baptiste Alayrac, Jiahui Yu, Radu Soricut, Johan Schalkwyk, Andrew M Dai, Anja Hauth, Katie Millican, et al. Gemini: A family of highly capable multimodal models. arXiv preprint arXiv:2312.11805 , 2023.
- Khanh-Tung Tran, Dung Dao, Minh-Duong Nguyen, Quoc-Viet Pham, Barry O'Sullivan, and Hoang D Nguyen. Multi-agent collaboration mechanisms: A survey of LLMs. arXiv preprint arXiv:2501.06322 , 2025.
- Harsh Trivedi, Niranjan Balasubramanian, Tushar Khot, and Ashish Sabharwal. MuSiQue: Multihop questions via single-hop question composition. Transactions of the Association for Computational Linguistics , 10:539-554, 2022.

- Ziyu Wan, Yunxiang Li, Xiaoyu Wen, Yan Song, Hanjing Wang, Linyi Yang, Mark Schmidt, Jun Wang, Weinan Zhang, Shuyue Hu, et al. ReMA: Learning to meta-think for LLMs with multi-agent reinforcement learning. arXiv preprint arXiv:2503.09501 , 2025.
- Hanlin Wang, Chak Tou Leong, Jiashuo Wang, Jian Wang, and Wenjie Li. SPA-RL: Reinforcing LLM agents via stepwise progress attribution. arXiv preprint arXiv:2505.20732 , 2025a.
- Junyang Wang, Haiyang Xu, Haitao Jia, Xi Zhang, Ming Yan, Weizhou Shen, Ji Zhang, Fei Huang, and Jitao Sang. Mobile-Agent-v2: Mobile device operation assistant with effective navigation via multi-agent collaboration. In Advances in Neural Information Processing Systems , volume 37, pp. 2686-2710, 2024.
- Liang Wang, Nan Yang, Xiaolong Huang, Binxing Jiao, Linjun Yang, Daxin Jiang, Rangan Majumder, and Furu Wei. Text embeddings by weakly-supervised contrastive pre-training. arXiv preprint arXiv:2212.03533 , 2022.
- Weixun Wang, Shaopan Xiong, Gengru Chen, Wei Gao, Sheng Guo, Yancheng He, Ju Huang, Jiaheng Liu, Zhendong Li, Xiaoyang Li, et al. Reinforcement learning optimization for large-scale learning: An efficient and user-friendly scaling library. arXiv preprint arXiv:2506.06122 , 2025b.
- Zihan Wang, Kangrui Wang, Qineng Wang, Pingyue Zhang, Linjie Li, Zhengyuan Yang, Kefan Yu, Minh Nhat Nguyen, Licheng Liu, Eli Gottlieb, et al. RAGEN: Understanding self-evolution in LLM agents via multi-turn reinforcement learning. arXiv preprint arXiv:2504.20073 , 2025c.
- Xiangyuan Xue, Yifan Zhou, Guibin Zhang, Zaibin Zhang, Yijiang Li, Chen Zhang, Zhenfei Yin, Philip Torr, Wanli Ouyang, and Lei Bai. CoMAS: Co-evolving multi-agent systems via interaction rewards. arXiv preprint arXiv:2510.08529 , 2025a.
- Zhenghai Xue, Longtao Zheng, Qian Liu, Yingru Li, Xiaosen Zheng, Zejun Ma, and Bo An. SimpleTIR: End-to-end reinforcement learning for multi-turn tool-integrated reasoning. arXiv preprint arXiv:2509.02479 , 2025b.
- An Yang, Anfeng Li, Baosong Yang, Beichen Zhang, Binyuan Hui, Bo Zheng, Bowen Yu, Chang Gao, Chengen Huang, Chenxu Lv, et al. Qwen3 technical report. arXiv preprint arXiv:2505.09388 , 2025.
- Zhilin Yang, Peng Qi, Saizheng Zhang, Yoshua Bengio, William W Cohen, Ruslan Salakhutdinov, and Christopher D Manning. HotpotQA: A dataset for diverse, explainable multi-hop question answering. arXiv preprint arXiv:1809.09600 , 2018.
- Qiying Yu, Zheng Zhang, Ruofei Zhu, Yufeng Yuan, Xiaochen Zuo, Yu Yue, Tiantian Fan, Gaohong Liu, Lingjun Liu, Xin Liu, et al. DAPO: An open-source LLM reinforcement learning system at scale. arXiv preprint arXiv:2503.14476 , 2025.
- Huining Yuan, Zelai Xu, Zheyue Tan, Xiangmin Yi, Mo Guang, Kaiwen Long, Haojia Hui, Boxun Li, Xinlei Chen, Bo Zhao, et al. MARSHAL: Incentivizing multi-agent reasoning via self-play with strategic LLMs. arXiv preprint arXiv:2510.15414 , 2025.
- Weihao Zeng, Yuzhen Huang, Qian Liu, Wei Liu, Keqing He, Zejun Ma, and Junxian He. SimpleRL-Zoo: Investigating and taming zero reinforcement learning for open base models in the wild. arXiv preprint arXiv:2503.18892 , 2025.
- Guibin Zhang, Hejia Geng, Xiaohang Yu, Zhenfei Yin, Zaibin Zhang, Zelin Tan, Heng Zhou, Zhongzhi Li, Xiangyuan Xue, Yijiang Li, et al. The landscape of agentic reinforcement learning for LLMs: A survey. arXiv preprint arXiv:2509.02547 , 2025a.
- Kai Zhang, Xiangchao Chen, Bo Liu, Tianci Xue, Zeyi Liao, Zhihan Liu, Xiyao Wang, Yuting Ning, Zhaorun Chen, Xiaohan Fu, et al. Agent learning via early experience. arXiv preprint arXiv:2510.08558 , 2025b.
- Kaiyan Zhang, Runze Liu, Xuekai Zhu, Kai Tian, Sihang Zeng, Guoli Jia, Yuchen Fan, Xingtai Lv, Yuxin Zuo, Che Jiang, Ziyang Liu, Jianyu Wang, Yuru Wang, Ruotong Zhao, Ermo Hua, Yibo Wang, Shijie Wang, Junqi Gao, Xinwei Long, Youbang Sun, Zhiyuan Ma, Ganqu Cui, Lei Bai, Ning Ding, Biqing Qi, and Bowen Zhou. MARTI: A framework for multi-agent LLM systems reinforced training and inference, 2025c. URL https://github.com/TsinghuaC3I/MARTI .
- Wentao Zhang, Liang Zeng, Yuzhen Xiao, Yongcong Li, Ce Cui, Yilei Zhao, Rui Hu, Yang Liu, Yahui Zhou, and Bo An. AgentOrchestra: Orchestrating hierarchical multi-agent intelligence with the tool-environment-agent (TEA) protocol. arXiv preprint arXiv:2506.12508 , 2025d.
- Yujie Zhao, Lanxiang Hu, Yang Wang, Minmin Hou, Hao Zhang, Ke Ding, and Jishen Zhao. Stronger together: On-policy reinforcement learning for collaborative LLMs. arXiv preprint arXiv:2510.11062 , 2025.
- Chujie Zheng, Shixuan Liu, Mingze Li, Xiong-Hui Chen, Bowen Yu, Chang Gao, Kai Dang, Yuqiong Liu, Rui Men, An Yang, et al. Group sequence policy optimization. arXiv preprint arXiv:2507.18071 , 2025.

- Lianmin Zheng, Liangsheng Yin, Zhiqiang Xie, Chuyue Livia Sun, Jeff Huang, Cody Hao Yu, Shiyi Cao, Christos Kozyrakis, Ion Stoica, Joseph E Gonzalez, et al. SGLang: Efficient execution of structured language model programs. In Advances in Neural Information Processing Systems , volume 37, pp. 62557-62583, 2024.
- Yifei Zhou, Andrea Zanette, Jiayi Pan, Sergey Levine, and Aviral Kumar. ArCHer: Training language model agents via hierarchical multi-turn RL. In International Conference on Machine Learning , pp. 62178-62209. PMLR, 2024.
- Zilin Zhu, Chengxing Xie, Xin Lv, and slime Contributors. slime: An LLM post-training framework for RL scaling. https://github.com/THUDM/slime , 2025.
- Daniel M Ziegler, Nisan Stiennon, Jeffrey Wu, Tom B Brown, Alec Radford, Dario Amodei, Paul Christiano, and Geoffrey Irving. Fine-tuning language models from human preferences. arXiv preprint arXiv:1909.08593 , 2019.

## A Proofs

## A.1 Proof of Lemma 4.2

Lemma4.2 . Under Assumptions 4.1, for any agent k ,

<!-- formula-not-decoded -->

where µ k ≜ 1 |Y k | ∑ a i t ∈Y k R i , σ 2 k ≜ 1 |Y k | ∑ a i t ∈Y k ( R i -µ k ) 2 are the mean and variance when sampling time steps uniformly from Y k (i.e., when agent k is active). ∆ k is a score-reward covariance correction term.

Proof. By definition, so

<!-- formula-not-decoded -->

<!-- formula-not-decoded -->

We can factor the expectation:

<!-- formula-not-decoded -->

Next, we use the standard variance decomposition

<!-- formula-not-decoded -->

<!-- formula-not-decoded -->

<!-- formula-not-decoded -->

<!-- formula-not-decoded -->

Substituting this into the previous expression yields

<!-- formula-not-decoded -->

with ∆ k = Cov( ∥ z ( k ) ∥ 2 , ( R i -µ ) 2 2 ) .

<!-- formula-not-decoded -->

The second term captures the residual score-reward correlation. In large-scale LLM training, rewards are typically low-dimensional signals of final task quality (e.g., pass/fail for reasoning, correctness for coding), while z ( k ) i,t depends mainly on the local token-level stochasticity of the policy. Empirically, their covariance is often much smaller than the main scaling factor E k [ ∥ z ( k ) i,t ∥ 2 ]( σ 2 k +( µ k -µ ) 2 ) /σ 2 .

## A.2 Proof of Proposition 4.3

Proposition 4.3 (Gradient-Norm Inflation). As either the normalized mean deviation | µ k -µ | /σ or the normalized variance ratio σ 2 k /σ 2 becomes large, the second moment of ˜ g global k grows at least linearly. Consequently, along any training process for which there exists a sequence of iterations indexed by m such that

<!-- formula-not-decoded -->

where ˜ g global m = (˜ g global 1 ,m , . . . , ˜ g global K,m ) stacking all LLM agents' gradients.

Proof. The key message is immediate from Lemma 4.1: the global-normalized gradient for agent k is amplified whenever the reward statistics of the agent-active subset Y k do not match the global reward statistics. Concretely, Lemma 4.1 gives

<!-- formula-not-decoded -->

The multiplier ( σ 2 k +( µ k -µ ) 2 ) /σ 2 contains two sources of inflation: (i) a variance mismatch σ 2 k /σ 2 , meaning rewards observed when agent k is active have a different spread than the global rewards; and (ii) a mean misalignment ( µ k -µ ) 2 /σ 2 , meaning agentk 's active rewards are shifted relative to the global mean. Either effect increases the second moment of ˜ g global k proportionally, hence causing larger gradient fluctuations.

For the claimed blow-up statement along training, apply Equation (14) at iteration m :

<!-- formula-not-decoded -->

Thus, if there exists a subsequence with σ 2 k,m +( µ k,m -µ m ) 2 σ 2 m → ∞ , then the second moment of the globalnormalized gradient necessarily diverges unless E [ ∥ z ( k ) i,t,m ∥ 2 ] or ∆ k,m cancels this growth. Finally, since the stacked gradient satisfies ∥ ˜ g global m ∥ 2 = ∑ K j =1 ∥ ˜ g global j,m ∥ 2 , divergence of any component implies E [ ∥ ˜ g global m ∥ 2 ] → ∞ , which completes the proof.

## B Experimental Details

## B.1 Hyperparameters for Math

For the Math task, uniform hyperparameters are employed across all methods. The maximum prompt and response lengths are set to 8192 and 4096 tokens, respectively. We utilize a two-agent orchestration framework (comprising a Solver Agent and a Verifier Agent), allowing for a maximum of two solver-verifier loops. The actor learning rate is fixed at 1 × 10 -6 for each agent, utilizing on-policy updates. We employ group-based rollouts with a group size of 8. A binary rule-based reward function is used (1 for success, 0 for failure), while invalid actions incur a penalty with a coefficient of 0.1. The batch sizes for training and evaluation are 32 and 64, respectively. During evaluation, we use nucleus sampling with top\_p = 0 . 95 and a temperature of 0.6.

## B.2 Hyperparameters for Multi-Turn Search

Similarly, all methods share identical hyperparameter configurations for the multi-turn search task. The maximum limits for prompts and responses are 4096 and 800 tokens, respectively, with each episode capped at a maximum of 4 turns. This task utilizes a three-agent architecture (Verifier, Search, and Answer). The actor learning rate is set to 1 × 10 -6 per agent, with 5 update iterations. We use group-based rollouts with a group size of 5. The reward structure employs the same binary rule-based criteria; however, the invalid-action penalty coefficient is set to 0.01. Training and evaluation batch sizes are 128 and 256, respectively. Evaluation sampling parameters remain consistent with the Math task (top\_p = 0 . 95 , temperature 0.6).

## B.3 Hardware and System Configuration

All experiments are conducted on NVIDIA H100 GPUs.

## B.4 Cost Estimation

The API costs ($) are estimated using OpenRouter market prices via the Together provider ( https:// openrouter.ai/provider/together ). The pricing for Qwen2.5-7B is set at $0.30 per million (M) tokens for both input and output. For Llama-3.2-3B-Instruct, the pricing is $0.06 per million (M) tokens for both input and output.

## C Prompt Templates

In multi-agent configurations, each agent receives a composite prompt structured as follows: (i) an environmentprovided observation prompt ( env\_prompt ) describing the task (and interaction history, where applicable); (ii) the accumulated team interaction context ( team\_context ); and (iii) the specific role instruction for the agent.

## C.1 Math Task

The shared environment prompt for the multi-agent setup is defined as follows:

## Environment Prompt

You are a member of an expert multi-agent team tasked with solving the math problem. The team's math problem is: {task\_description}

The Solver Agent receives the following prompt:

## Solver Agent Prompt

You are a "Solver Agent". Your job is to carefully reason through the math problem step by step and derive the correct

```
# Task Introduction {env_prompt} # Your Teammates' Outputs {team_context} # Your Role answer. When reasoning, consider your teammates' outputs (if any) as auxiliary context. You should give the final answer within \boxed{}.
```

The Verifier Agent receives the following prompt:

## Verifier Agent Prompt

You are a "Verifier Agent". Your responsibility is to critically review the most recent solution provided by the "Solver Agent". Check each reasoning step, formula, and conclusion for accuracy, completeness, and logical consistency. At the end of your output, you MUST provide your verdict within &lt;verify&gt; &lt;/verify&gt; using exactly one of:

```
# Task Introduction {env_prompt} # Your Teammates' Outputs {team_context} # Your Role (1) <verify>approve</verify> if all steps and the final answer are correct. (2) <verify>reject</verify> if you detect any issue.
```

## C.2 Multi-Turn Search Task (Verifier-Search-Answer)

The shared environment prompt for the multi-agent setup is defined as follows:

## Environment Prompt

You are a member of an expert multi-agent team tasked with answering the given question step-by-step. The question is: {task\_description}

Your team can access an external search engine to retrieve external information. At each step, you and your teammates must collaborate to make progress toward answering the question.

Prior to this step, your team has already taken {step\_count} step(s). Below is the interaction history where &lt;search&gt; &lt;/search&gt; wrapped the past search queries and &lt;information&gt; &lt;/information&gt; wrapped the corresponding retrieved information returned by the external search engine. History: {memory\_context}

The Verifier Agent receives the following prompt:

## Verifier Agent Prompt

```
# Task Introduction {env_prompt}
```

# Your Role

You are a "Verifier Agent" acting as a router. Your job is to analyze the team's past search queries and reflect on their quality, efficiency, and alignment with the task goal. Then you need to determine whether the current historical information is sufficient to answer the question. Based on this assessment, you will decide how to route the task.

Your responsibilities: - Review past search queries enclosed within &lt;search&gt; &lt;/search&gt; and external information enclosed within &lt;information&gt; &lt;/information&gt;. - Evaluate whether previous queries were reasonable and aligned with the task objective. - Identify potential issues (if any), including repeated or redundant queries; imprecise queries that are too broad, vague, or missing critical constraints/entities; misaligned queries that drift away from the actual task goal. - Assess whether the available information is complete and sufficient to generate a high-quality answer, and make a routing decision based on information sufficiency.

You are now at step {step}. You should first reason step-by-step about the past events. After completing your reasoning, give your routing decision:

- (1) If the information is sufficient to answer the question: return &lt;verify&gt;yes&lt;/verify&gt;
- (2) If the information is insufficient to answer the question: return &lt;verify&gt;no&lt;/verify&gt;

The Search Agent receives the following prompt:

## Search Agent Prompt

```
# Task Introduction {env_prompt} # Your Teammates' Outputs at Step {step} {team_context} # Your Role
```

You are a "Search Agent". Your primary responsibility is to call a search engine to gather external information that helps answer a given question. The search engine should be invoked using the format: &lt;search&gt;your query&lt;/search&gt;. Before conducting the search, you should reason step-by-step about the question, any previous queries, and retrieved information, as well as your teammates' outputs (if available). This reasoning process MUST be enclosed within &lt;think&gt; &lt;/think&gt; tags. Once you've finished your reasoning, provide your final search query enclosed within &lt;search&gt; &lt;/search&gt;.

The Answer Agent receives the following prompt:

## Answer Agent Prompt

# Task Introduction

{env\_prompt}

# Your Role

You are an "Answer Agent". Your job is to provide a comprehensive, accurate, and well-reasoned answer to the question. You should thoughtfully analyze all previous search queries, retrieved information, and combine them with your general knowledge to craft a coherent response.

You should first conduct a reasoning process. This process MUST be enclosed within &lt;think&gt; &lt;/think&gt; tags. After completing your reasoning, provide your final answer within &lt;answer&gt; &lt;/answer&gt; tags. For example, &lt;answer&gt;Beijing&lt;/answer&gt;.

## D Pseudo Code

## Algorithm 1 Training Multi-Agent LLM Systems with Dr. MAS

```
1: Require: Multi-agent orchestra O ; logical agents { 1 , . . . , K } with LLM IDs { m k } K k =1 ; LLM sharing flag s ∈ { 0 , 1 } ; task distribution p ( X ) ; rollout group size N ; clipping ϵ ; KL penalty β (optional) 2: // (A) Agent-Model assignment: map logical agents to physical LLM worker groups ( wg_id ) 3: Initialize wg_to_agents_mapping ←∅ 4: if s = 0 then 5: // Non-sharing: each agent k has a dedicated LLM worker group 6: for k = 1 to K do 7: Create worker group wg_id for agent k (one ActorRollout backend) 8: wg_to_agents_mapping [ wg_id ] ←{ k } 9: end for 10: else 11: // Sharing: agents configured with the same LLM are mapped to one shared worker group 12: M←{ m k } K k =1 // distinct LLM IDs 13: for each m ∈ M do 14: A ( m ) ←{ k | m k = m } // agents using LLM m 15: Create shared worker group wg_id for model m (shared weights across A ( m ) ) 16: wg_to_agents_mapping [ wg_id ] ←A ( m ) 17: end for 18: end if 19: // Dispatch table used by the orchestrator during rollouts: agent_id → wg_id 20: Build agent_to_wg_mapping from wg_to_agents_mapping 21: // (B) Training loop: distributed rollouts + Dr. MAS normalization + per-wg_id updates 22: for each training iteration do 23: // Snapshot current policy for importance ratios 24: Update old policies: θ old ← θ 25: // (B1) Distributed rollout collection: execute multi-agent orchestration at scale 26: // Actor backends run with sglang , scheduled by a shared resource pool (e.g., Ray placement groups) 27: Initialize aggregated batch B ← ∅ 28: Parallel for i = 1 to N 29: Sample task x ∼ p ( X ) and run O to generate a trajectory τ i 30: Let R i ← R ( τ i ) // trajectory-level reward shared by all steps 31: For each step t in τ i : 32: choose active agent k i t (by O ) and route request via agent_to_wg_mapping 33: sample action a i t from the dispatched backend policy and log step tuple into B 34: B ← B ∪ { ( i, t, k i t , wg_id , a i t , R i ) } 35: End parallel for 36: // (B2) Dr. MAS: agent-wise advantage normalization on active-step subsets Y k 37: for k = 1 to K do 38: Y k ←{ a i t | ( i, t, k i t , · , a i t , R i ) ∈ B , k i t = k } 39: µ k ← 1 |Y k | ∑ a i t ∈Y k R i 40: σ 2 k ← 1 |Y k | ∑ a i t ∈Y k ( R i -µ k ) 2 , σ k ← √ σ 2 k 41: For each step ( i, t ) with k i t = k : A i,k agent ← R i -µ k σ k + ε 42: end for 43: // (B3) Optimization: partition B by wg_id and update each LLM backend (shared/non-shared handled automatically) 44: // Trainer forms per-model micro-batches {B wg } and performs clipped updates per worker group 45: for each worker group id wg_id in wg_to_agents_mapping do 46: B wg ←{ ( i, t, k i t , wg_id , a i t , R i , A i,k i t agent ) ∈ B | wg_id = agent_to_wg_mapping [ k i t ] } 47: Update θ wg_id on B wg with clipped objective (clipping ϵ ) using A i,k i t agent (optionally add KL regularization with weight β ) 48: end for 49: end for
```

## E Additional Experiments

## E.1 Gradient-Norm Instability on Math

Figure 6 shows training accuracy and gradient norms for the two-agent math orchestration (Qwen3-4B, non-sharing). Similar to the search setting in Figure 4, GRPO produces clear gradient-norm spikes during training, especially in the early and middle stages. In contrast, Dr. MAS keeps the gradient norms much smoother for both agents and leads to steadier improvement in training accuracy.

Figure 6 Comparison of training accuracy and gradient norm between GRPO and Dr. MAS. The results are recorded during multi-agent RL post-training for two-agent math orchestration under LLM non-sharing (Qwen3-4B).

<!-- image -->

## E.2 Gradient-Norm Explosion

Figure 7 highlights the instability of vanilla GRPO on the multi-turn search task (Qwen2.5-7B, non-sharing). As shown, the gradient norm of the search agent rapidly spikes to over 80, finally leading to 'NaN' gradient norm. In contrast, Dr. MAS stabilizes the optimization, maintaining relatively low gradient norms across all agents, ensuring steady convergence.

Figure 7 Training dynamics of the three-agent search task (Qwen2.5-7B, non-sharing). Vanilla GRPO suffers from serious gradient spikes that lead to 'NaN' on the search agent, whereas Dr. MAS maintains stable gradients and converges effectively.

<!-- image -->

## E.3 Training Curves of Ablation Study

Figure 8 presents the training curves for different advantage normalization variants described in Section 5.4. The global normalization baseline ( µ, σ ) shows unstable training and slow improvement. Introducing either agent-wise mean ( µ k , σ ) or agent-wise standard deviation ( µ, σ k ) leads to noticeably smoother curves and faster gains. The fully agent-wise version ( µ k , σ k ) , i.e., Dr. MAS, achieves the most stable training and the highest final performance.

Figure 8 Training curves for different advantage normalization variants in the ablation study.

<!-- image -->

## F Illustrative Examples of Multi-Agent LLM Collaboration

## F.1 Multi-Turn Search Task: Hierarchical Coordination

<!-- image -->

<!-- image -->

## F.2 Math Task: Iterative Coordination

<!-- image -->

<!-- image -->

<!-- image -->