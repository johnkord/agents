## Think Fast and Slow: Step-Level Cognitive Depth Adaptation for LLM Agents

Ruihan Yang ♡∗ , Fanghua Ye ♠ † , Xiang Wei ♠ , Ruoqing Zhao ♠ , Kang Luo ♠ , Xinbo Xu ♡ , Bo Zhao ♠ , Ruotian Ma ♠ , Shanyi Wang ♠ , Zhaopeng Tu ♠ † , Xiaolong Li ♠ , Deqing Yang ♡ † , Linus ♠

♡ Fudan University

{ rhyang17 } @fudan.edu.cn {

♠ Tencent Hunyuan fanghua.ye.21 } @gmail.com

Project Page: https://github.com/rhyang2021/CogRouter

## Abstract

Large language models (LLMs) are increasingly deployed as autonomous agents for multi-turn decision-making tasks. However, current agents typically rely on fixed cognitive patterns: non-thinking models generate immediate responses, while thinking models engage in deep reasoning uniformly. This rigidity is inefficient for long-horizon tasks, where cognitive demands vary significantly from step to step, with some requiring strategic planning and others only routine execution. In this paper, we introduce COGROUTER , a framework that trains agents to dynamically adapt cognitive depth at each step. Grounded in ACT-R theory, we design four hierarchical cognitive levels ranging from instinctive responses to strategic planning. Our two-stage training approach includes Cognition-aware Supervised Fine-tuning (COSFT) to instill stable level-specific patterns, and Cognition-aware Policy Optimization (COPO) for step-level credit assignment via confidence-aware advantage reweighting. The key insight is that appropriate cognitive depth should maximize the confidence of the resulting action. Experiments on ALFWorld and ScienceWorld demonstrate that COGROUTER achieves state-of-the-art performance with superior efficiency. With Qwen2.5-7B, it reaches an 82.3% success rate, outperforming GPT-4o (+40.3%), OpenAI-o3 (+18.3%), and GRPO (+14.0%), while using 62% fewer tokens.

## 1 Introduction

Recent advances in large language models (LLMs) (OpenAI, 2023; Gemini Team, 2023) have enabled their deployment as autonomous agents capable of planning and executing complex multi-turn tasks across domains, including code generation (Yuan et al., 2024; Lu et al., 2025), software engineering (Jimenez et al., 2024), web interaction (Yao et al., 2023a; Zhou et al., 2024), and embodied environments (Yang et al., 2025b). In these settings, agents must continuously perceive observations, reason about actions, and execute decisions over long horizons. Unlike single-turn tasks where a fixed thinking strategy may suffice, agentic environments are highly dynamic and impose varying cognitive demands at each step within a task.

Consider the 'use thermometer' task in ScienceWorld (Wang et al., 2022), where the agent must first locate the thermometer and then measure the temperature of an unknown substance. In the initial exploration phase, the agent must plan strategically, deciding which rooms to search and in what order to efficiently locate the required objects. When encountering unexpected situations, such as searching the workshop but finding no thermometer, the agent needs to reflect on the failed search and revise its exploration plan. In contrast, many mid-trajectory steps are routine. After executing 'open door to bathroom' and observing that the door opens, the natural next action is simply 'go to bathroom' without extended thinking. This illustrates that agentic tasks exhibit

∗ Work done during an internship at Tencent Hunyuan.

† Corresponding authors.

significant step-wise heterogeneity in thinking demands, ranging from instinctive reactions to strategic planning.

However, most agents rely on fi xed cognitive patterns . Non-thinking models produce reflexive actions at every step, while thinking models ( e . g ., DeepSeek-R1 (DeepSeek-AI et al., 2025a)) engage in deep chain-of-thought reasoning throughout the entire trajectory. This rigidity creates a critical inefficiency: thinking models consume excessive tokens on routine steps where simple responses suffice, while nonthinking models fail on complex decisions requiring strategic planning. A natural solution is to train agents that adaptively allocate cognitive resources. However, as shown in Figure 1, even when initialized with balanced cognitive capabilities, standard reinforcement learning (RL) methods tend to collapse toward uniform deep thinking. While recent work has explored adaptive thinking for single-turn tasks (Lou et al.,

Figure 1: Illustration of the cognitive rigidity issue : While COPO maintains an adaptive cognitive distribution (bottom), standard RL methods like GRPO (top) collapse to uniform deep thinking ( L 4 ), wasting computational resources on routine steps. L 1 L 4 represent increasing cognitive depth, from instinctive responses to strategic planning.

<!-- image -->

2025; Zhang et al., 2025b), these methods typically employ task-level adaptation with binary switching, failing to capture the step-wise, multi-level cognitive demands within agentic tasks.

This raises a central challenge: How can we train agents to adaptively allocate cognitive depth across multiple levels at each step, balancing both task performance and computational efficiency? To address this, we introduce COGROUTER, a framework grounded in the Adaptive Control of Thought-Rational (ACT-R) theory (Anderson, 1982), which posits that humans dynamically allocate cognitive resources from automatic procedural execution to deliberate reasoning. COGROUTER trains agents to modulate their cognitive depth across four hierarchical levels : from instinctive responses ( L 1 ) to strategic planning ( L 4 ).

Training such adaptive allocation is non-trivial due to the sparsity of rewards in agentic environments. We propose a two-stage approach: Cognition-aware Supervised Fine-tuning (COSFT) to instill stable, level-specific cognitive patterns, followed by Cognition-aware Policy Optimization (COPO). COPO is a novel RL algorithm that enables step-level credit assignment via confidence-aware advantage reweighting. The core insight is that an appropriate cognitive depth should facilitate confident action prediction.

We evaluate COGROUTER on two challenging interactive benchmarks, ALFWorld (Shridhar et al., 2020) and ScienceWorld (Wang et al., 2022). Experimental results demonstrate that COGROUTER achieves state-of-the-art performance with superior token efficiency. Using Qwen2.5-7B as the base model, our approach achieves an 82.3% average success rate, significantly outperforming GPT-4o (+40.3%), OpenAI-o3 (+18.3%), and GRPO (+14.0%), while reducing token consumption by 62% compared to GRPO.

In summary, our contributions are three-fold:

1. We identify and formalize the 'cognitive rigidity' issue in LLM agents, illustrating how fixed reasoning patterns lead to inefficiency in long-horizon tasks with heterogeneous step-wise demands.
2. We propose COGROUTER, a framework grounded in ACT-R theory that defines four hierarchical cognitive levels and utilizes a two-stage training pipeline (COSFT and COPO) to enable dynamic cognitive adaptation.
3. We introduce COPO, a novel RL algorithm that performs step-level credit assignment via confidence-aware advantage reweighting, allowing the model to learn efficient reasoning without collapsing to uniform deep thinking.

## 2 Preliminary

Partially Observable Markov Decision Process In this paper, we model the agentic task as a partially observable Markov decision process (POMDP) M = ( X , S , A , O , T , R ) , where X denotes the set of instructions, S represents the set of environment states, A is the action space, and O is the observation space. The state transition function is T : S × A → ∆ ( S ) , and the reward function is R : S × A → R . At each time step t , the agent with a policy πθ receives an observation ot ∈ O , which is correlated with the current state st ∈ S . The agent maintains an interaction history τ t = { x , ( o 0, a 0 ) , . . . , ( ot -1 , at -1 ) , ot } and samples an action at ∼ πθ ( · | τ t ) , which induces a state transition st + 1 ∼ T ( · | τ t , at ) . An episode terminates at step T , yielding trajectory τ = { x , ( o 0, a 0 ) , . . . , ( oT , aT ) } and terminal reward R ( τ ) . The agent's objective is to learn a policy πθ that maximizes the expected terminal reward:

<!-- formula-not-decoded -->

GRPO for Agentic Tasks Group Relative Policy Optimization (GRPO) (Shao et al., 2024) is an effective method for training models without the need for a separate critic network. In agentic tasks, GRPO operates at the trajectory level. Given a task query x ∈ X , the agent samples a set of G complete trajectories { τ ( i ) } G i = 1 using the policy πθ . Each trajectory τ ( i ) = { x , ( o ( i ) 0 , a ( i ) 0 ) , . . . , ( o ( i ) T , a ( i ) T ) } begins with the instruction x , followed by a sequence of observation-action pairs over T time steps. The trajectory-level reward Ri = R ( τ ( i ) ) measures overall task success. The advantage for each trajectory is computed as

<!-- formula-not-decoded -->

This trajectory-level advantage A ( i ) is uniformly assigned to all steps within the trajectory, serving as the learning signal for policy optimization. The objective function for GRPO is

<!-- formula-not-decoded -->

where | a ( i ) | = ∑ T t = 0 | a ( i ) t | is the total number of action tokens in trajectory τ ( i ) , and r ( i ) t , n ( θ ) = πθ ( a ( i ) t , n | x , τ ( i ) t , a ( i ) t , &lt; n ) πθ old ( a ( i ) t , n | x , τ ( i ) t , a ( i ) t , &lt; n ) denotes the importance sampling ratio for the n -th token in the action a ( i ) t .

## 3 Methodology

We propose COGROUTER (Figure 2), a framework for training models to dynamically select the appropriate cognitive level at each step. We first define a set of distinct cognitive levels , grounded in ACT-R theory (§ 3.2). Building on this design, we introduce a two-stage training pipeline: 1) Cognition-aware Supervised Fine-tuning (COSFT), which integrates level-specific cognitive patterns into the model's base capabilities (§3.3); and 2) Cognition-aware Policy Optimization (COPO), which optimizes cognitive level selection through reinforcement learning with confidenceaware advantage reweighting (§3.4).

## 3.1 Task Formulation

In a standard Partially Observable Markov Decision Process (POMDP), the agent generates actions directly from the interaction history. However, complex agentic tasks often demand deeper reasoning at specific steps. To address this, we introduce N cognitive levels , denoted as L = {L 1 , . . . , L N } , where each level L i represents a distinct depth of reasoning. At each time step t , the agent with

<!-- image -->

<!-- image -->

Figure 2: Overview of the COGROUTER framework. We define four cognitive levels L 1 L 4 , then introduce a two-stage training process: (1) Cognition-aware Supervised Fine-tuning (COSFT) , which guides the model to learn stable cognitive patterns across levels with balanced data; (2) Cognition-aware Policy Optimization (COPO) , which applies RL with confidence-aware reweighting to help the model adaptively choose suitable levels based on context complexity.

<!-- image -->

policy πθ , first selects a cognitive level l t ∈ L based on the interaction history: l t ∼ πθ ( · | τ t ) . Given l t , it generates an intermediate thinking process: tht ∼ πθ ( · | τ t , l t ) , and then produces an executable action conditioned on this reasoning: at ∼ πθ ( · | τ t , tht ) . We denote the complete structured output at step t as yt = [ l t , tht , at ] . The objective is to maximize the expected terminal reward:

<!-- formula-not-decoded -->

This formulation requires the agent to adaptively select an appropriate cognitive level based on the complexity of the current state and to generate corresponding reasoning, balancing depth with efficiency. 1

## 3.2 Cognitive Level Design

Adaptive Control of Thought-Rational ( ACT-R ) (Anderson, 1982; 1993) posits that human cognition operates across a spectrum from automatic procedural execution to effortful declarative reasoning, with processing depth inversely related to cognitive load. Motivated by this framework, we propose four cognitive levels for agent decision-making that dynamically balance cognitive depth with computational efficiency (see full prompt in Appendix C).

Level 1 L 1 (Instinctive Response) : L 1 represents autonomous processing, corresponding to ACT-R's procedural stage where compiled production rules execute automatically without working memory engagement (Anderson and Lebiere, 1998). The agent produces immediate responses based on learned patterns, with no explicit reasoning structure. This mode optimizes for speed in routine scenarios.

Level 2 L 2 (Situational Awareness) : L 2 introduces monitored execution with basic deliberation. The agent assesses the Current State and Available Actions before selecting a response. This level aligns with ACT-R's goal-directed procedural processing, where situational information is maintained in working memory to guide action selection (Taatgen, 2013).

1 Note that the reward R ( τ ) depends jointly on the cognitive level l t , reasoning process tht , and action at , and does not necessarily increase with reasoning depth.

Level 3 L 3 (Experience Integration) : L 3 engages retrospective reasoning by incorporating historical information. Beyond situational assessment, the agent specifies the Goal , performs Reflection on past actions and outcomes, and integrates experiential insights into decision-making. This corresponds to ACT-R's knowledge compilation stage, where declarative memories are retrieved and consolidated to refine procedural knowledge (Anderson, 1982).

Level 4 L 4 (Strategic Planning) : L 4 represents the most cognitively demanding mode, requiring extensive prospective simulation . The agent evaluates multiple candidate actions by mentally simulating their future consequences and impact. This level mirrors ACT-R's fully declarative stage, engaging complex problem-solving through chunk retrieval and strategic evaluation (Anderson, 2007).

## 3.3 Cognition-Aware Supervised Fine-tuning

While prior work on controllable reasoning often emphasizes the reinforcement learning (RL) stage, we argue that the supervised fine-tuning (SFT) stage is equally critical for establishing a solid foundation: 1) The model should first acquire stable cognitive patterns for each cognitive level to prevent format leakage or mode collapse during subsequent RL training; and 2) The model may inherently favor certain cognitive patterns, which can interfere with learning to select levels based on task complexity. Enforcing a balanced distribution over all cognitive levels during SFT helps mitigate this bias.

To implement the structured output yt = [ l t , tht , at ] defined in § 3.1, we introduce a structured format. Each step t includes three components: 1) the cognitive level, enclosed in &lt;level&gt; tags; 2) the internal thinking process corresponding to the selected level, enclosed in &lt;think&gt; tags; 3) the final executable action, enclosed in &lt;action&gt; tags. Using this structured format, the training data is constructed by augmenting expert trajectories with cognitive annotations as follows:

1. We collect a dataset of successful trajectories T ∗ = { ( x , o 0, a ∗ 0 , . . . , oT , a ∗ T ) } , comprising only observation-action pairs generated by expert model ( i . e ., GPT-4o).
2. At each step t , we randomly sample a cognitive level l t to ensure balanced coverage across all levels.
3. Given the history τ ∗ t = { x , ( o 0, a ∗ 0 ) , . . . , ( ot -1 , a ∗ t -1 ) , ot } and the ground-truth action a ∗ t , we prompt the expert model to complete the thinking process tht corresponding to the selected level l t (see Appendix C for the detailed prompt).

This procedure yields a balanced dataset D cog = { ( τ ∗ t , l t , th t , a ∗ t ) } T t = 0 with approximately uniform distribution across four cognitive levels. The SFT objective minimizes the negative log-likelihood over all steps:

<!-- formula-not-decoded -->

where πθ is the policy parameterized by θ , and yt = [ l t , th t , a ∗ t ] denotes the structured output. As an ablation, we also construct an alternative dataset where the expert model directly selects the cognitive level at each step rather than random sampling.

## 3.4 Cognition-Aware Policy Optimization

While group-based RL algorithms ( e . g ., GRPO) have proven highly effective for training LLMs on reasoning tasks, they assign trajectory-level advantages uniformly across all steps, without distinguishing whether the cognitive pattern at each step is contextually appropriate. To address this, we propose Cognition-Aware Policy Optimization (COPO) , which enables step-level credit assignment based on action prediction confidence (Figure 2). The key insight is that an appropriate cognitive pattern should facilitate confident and correct action selection. COPO modulates step-level advantages using the model's prediction confidence, allowing it to learn which cognitive depth is appropriate for each step. The algorithm is shown in Algorithm 1.

## Algorithm 1 Cognition-Aware Policy Optimization (COPO)

- 1: Initialize: Policy πθ .
- 2: for each training iteration do
- 3: Collect a batch of trajectories B = { τ ( i ) } by running policy πθ .
- 4: Compute trajectory-level rewards Ri and advantages A ( i ) for each τ ( i ) ∈ B .
- 5: for each successful trajectory τ ( i ) with Ri &gt; 0 do
- 6: for each step t in trajectory τ ( i ) do
- 7: Construct cognitive group e ( i ) t by generating thinking processes under all 4 levels.
- 9: Normalize confidence scores C ( i , k ) norm, t within the cognitive group using Eq. 3.
- 8: Compute action prediction confidence C ( i , k ) t for each level k using Eq. 2.
- 10: Compute confidence-aware weights g ( C ( i , k ) t ) using Eq. 4.
- 12: end for
- 11: Compute step-level advantages A ( i , k ) t using Eq. 5.
- 13: end for
- 14: For failed trajectories ( Ri ≤ 0), keep original advantages without cognitive group expansion.
- 15: Update policy πθ by maximizing the COPO objective (Eq. 6).
- 16: end for
- 17: Output: Optimized policy πθ .

Reward Design Given trajectory τ ( i ) , the trajectory-level reward Ri consists of two components:

<!-- formula-not-decoded -->

where R task i ∈ { 0, 1 } indicates task success based on environment feedback, and R format i ∈ { 0, 1 } indicates whether all actions in the trajectory conform to the required structured format ( i . e ., , properly using &lt;level&gt; , &lt;think&gt; , and &lt;action&gt; tags). We set R format i = 0 if any action deviates from this format, imposing a strict penalty to enforce format consistency.

Cognitive Group Construction For each successful trajectory ( Ri &gt; 0), we expand the training data by constructing a cognitive group at each step. Specifically, we regenerate the thinking process under all four cognitive levels while keeping the observation o ( i ) t and action a ( i ) t fixed. This yields the cognitive group e ( i ) t = { e ( i , k ) t } 4 k = 1 , where each variant is defined as e ( i , k ) t = [ l k , th ( i , k ) t , a ( i ) t ] , corresponding to cognitive level l k . Importantly, all variants produce the same action a ( i ) t , but differ in the thinking process that leads to it. To assess the suitability of each cognitive level, we evaluate how confidently the model predicts the action when conditioned on each thinking process:

<!-- formula-not-decoded -->

where a ( i ) t , n is the n -th token in the action a ( i ) t . Higher confidence scores indicate stronger alignment between the thinking process and the resulting action.

Confidence-Aware Advantage Reweighting To compare cognitive levels within each group, we normalize the confidence scores:

<!-- formula-not-decoded -->

where µ ( i ) t and σ ( i ) t are the mean and standard deviation of confidence scores within the cognitive group e ( i ) t . We then apply a temperature-scaled softmax to convert these normalized scores into

relative weights

<!-- formula-not-decoded -->

where m is a temperature parameter that controls the sharpness of the distribution 2 . Since ∑ 4 k = 1 g ( C ( i , k ) t ) = 1 by construction, these weights redistribute the total advantage across cognitive levels without changing its magnitude. These weights are used to modulate the step-level advantage as follows:

<!-- formula-not-decoded -->

where A ( i ) is the trajectory-level advantage (Eq. 1). For successful trajectories ( Ri &gt; 0), this reweighting amplifies advantages for cognitive levels that facilitate confident action predictions, while attenuating uncertain ones. For failed trajectories, no cognitive group is constructed, and the advantage remains at the trajectory level.

COPO Optimization For successful trajectories ( i ∈ I + ), we use the cognitive groups e ( i ) t with reweighted advantages A ( i , k ) t for each level from Equation 5. For failed trajectories ( i ∈ I ), we retain only the original structured output y ( i ) t with trajectory-level advantage A ( i ) . The objective function is

<!-- formula-not-decoded -->

where | e ( i ) | = ∑ t , k | e ( i , k ) t | and | y ( i ) | = ∑ t | y ( i ) t | are the total token counts for normalization, r ( i , k ) t , n = πθ ( e ( i , k ) t , n | x , τ ( i ) t , e ( i , k ) t , &lt; n ) πθ old ( e ( i , k ) t , n | x , τ ( i ) t , e ( i , k ) t , &lt; n ) and r ( i ) t , n = πθ ( y ( i ) t , n | x , τ ( i ) t , y ( i ) t , &lt; n ) πθ old ( y ( i ) t , n | x , τ ( i ) t , y ( i ) t , &lt; n ) denote the importance sampling ratios for successful and failed trajectories respectively, and r = clip ( r , 1 -ϵ , 1 + ϵ ) is the clipped ratio.

## 4 Experiments

## 4.1 Experimental Setup

Benchmarks We conduct experiments on two challenging environments: 1) ALFWorld (Shridhar et al., 2020) is an embodied environment designed to evaluate LLM agents on multi-step decisionmaking across six categories of household tasks: Pick &amp; Place (Pick), Examine in Light (Look), Clean &amp;Place (Clean), Heat &amp; Place (Heat), Cool &amp; Place (Cool), and Pick Two &amp; Place (Pick2). In each task, the agent receives a textual goal and must complete it through multi-turn interaction with the environment. A task is considered successful if the goal is achieved within the maximum number of allowed steps. 2) ScienceWorld (Wang et al., 2022) is a text-based environment that tests scientific reasoning across 30 task types aligned with the elementary science curriculum. At the end of each task, the agent receives a score between 0 and 100 based on performance. Further details can be found in Appendix B.

2 We set m = 2 in our experiments.

Evaluation Following prior work (Zhang et al., 2025c; Xi et al., 2024), we evaluate models on the test sets of both environments: 200 simulations for ALFWorld and the same for ScienceWorld. For ALFWorld, we report the success rate for each task category and overall. For ScienceWorld, we use both the average final score and success rate as the evaluation metrics. We also compute the average output token usage per task ( #Tokens ) as a measure of model efficiency.

Training Settings We use Llama3.1-8B (Meta, 2024) and Qwen2.5-7B (Yang et al., 2024a) as base models, with training data drawn from ScienceWorld (2,120 simulations) and ALFWorld (2,420 simulations). For COSFT, we randomly sample 500 environments from each benchmark, collect expert trajectories using GPT-4o (OpenAI, 2025a), and prompt it to complete thinking processes at randomly sampled cognitive levels (§ 3.3). For COPO, we use the remaining simulations, sample different 16 groups per rollout with a group size of 8, and run 150 iterations. Additional details are provided in Appendix D.

Baselines We compare our method against several baselines (see Appendix G for implementation details):

- Prompt-based methods , including ReAct (Yao et al., 2023b) and Reflexion (Shinn et al., 2023), a self-refinement method that summarizes each trial to guide future decisions. We also evaluate R1-distilled variants of Llama3.1-8B and Qwen2.5-7B, prompted with ReAct;
- Training-based methods , covering both offline and online methods. Offline methods include SFT and ETO (Song et al., 2024). We also include cognition-aware SFT (COSFT; see §3.3), which uniformly samples cognitive levels during training, and its variant (COSFTexp), which uses expert-selected levels based on contextual complexity. Online methods include GRPO (Shao et al., 2024), GiGPO (Feng et al., 2025), and AdaptThink (Zhang et al., 2025b).

We also include several non-thinking and thinking frontier models for comparison.

## 4.2 Main Results

Wecompare COGROUTER against state-of-the-art frontier models and both offline and online training methods. Key findings are summarized below.

Fixed cognitive patterns and binary switching are insufficient for long-horizon agentic tasks. As shown in Table 1, models with fixed cognitive patterns struggle on agentic tasks. Non-thinking models like DeepSeek-V3 achieve only 29.0% average success rate across both benchmarks (1092.3 tokens), while thinking models like DeepSeek-R1 reach 56.5% but consume nearly 10 × more tokens (11366.8). The problem is exacerbated in smaller models: vanilla Qwen2.5-7B achieves merely 26.5% (1155.8 tokens), while its R1-Distill variant collapses to 2.5% despite using 24 × more tokens (27421.2). AdaptThink (Zhang et al., 2025b), which employs binary switching between thinking and non-thinking modes, substantially improves over fixed-pattern baselines. However, it still underperforms COGROUTER by 19.3% on Qwen2.5-7B and 18.5% on LLama3.1-8B. This performance gap arises from two core limitations: 1) binary switching cannot capture multi-level cognitive demands, some steps require intermediate reasoning such as situational awareness or experience integration; 2) designed for single-turn tasks, AdaptThink uses a trajectory-level reward that uniformly encourages non-thinking across all steps, pushing the model toward selecting nonthinking regardless of step-wise complexity. These findings underscore the need for multi-level adaptive cognition and step-wise credit assignment to effectively handle long-horizon agentic tasks.

Supervised fine-tuning alone cannot learn adaptive thinking. While COSFTexp achieves stronger performance than SFT (+13.2% average success rate with Qwen2.5-7B), it consumes significantly more tokens (3562.4 compared to 577.5). As shown in Figure 3, COSFTexp inherits strong biases toward L 2 and L 3 thinking from the expert model. This highlights a core weakness of supervised fine-tuning: it mimics the expert's thinking distribution without learning to adapt cognitive depth

Table 1: Performance comparison across different methods on ALFWorld and ScienceWorld. SR : Success Rate (%). #Tokens : Average tokens per trajectory. COPO achieves state-of-the-art performance with superior token efficiency across both benchmarks.

| Method                                       | ALFWorld              | ALFWorld              | ScienceWorld          | ScienceWorld          | ScienceWorld          | Average               | Average               |
|----------------------------------------------|-----------------------|-----------------------|-----------------------|-----------------------|-----------------------|-----------------------|-----------------------|
|                                              | SR                    | #Tokens               | Score                 | SR                    | #Tokens               | SR                    | #Tokens               |
| Frontier Large Models                        | Frontier Large Models | Frontier Large Models | Frontier Large Models | Frontier Large Models | Frontier Large Models | Frontier Large Models | Frontier Large Models |
| gpt-4o-2024-08-06 (OpenAI, 2025a)            | 61.5                  | 860.8                 | 57.0                  | 22.5                  | 1009.9                | 42.0                  | 935.4                 |
| deepseek-v3-0324 (DeepSeek-AI et al., 2025b) | 52.0                  | 1171.2                | 13.0                  | 6.0                   | 1013.4                | 29.0                  | 1092.3                |
| openai-o3 (OpenAI, 2025b)                    | 74.0                  | 4290.9                | 72.4                  | 54.0                  | 5184.0                | 64.0                  | 4737.5                |
| deepseek-r1-0528 (DeepSeek-AI et al., 2025a) | 73.0                  | 6230.9                | 58.9                  | 40.0                  | 16502.7               | 56.5                  | 11366.8               |
| gemini-2.5-pro-0506 (Gemini Team, 2025)      | 70.0                  | 4896.5                | 65.2                  | 44.0                  | 8942.4                | 57.0                  | 6919.5                |
| Qwen2.5-7B-Instruct                          | Qwen2.5-7B-Instruct   | Qwen2.5-7B-Instruct   | Qwen2.5-7B-Instruct   | Qwen2.5-7B-Instruct   | Qwen2.5-7B-Instruct   | Qwen2.5-7B-Instruct   | Qwen2.5-7B-Instruct   |
| ReAct (Yao et al., 2023b)                    | 50.5                  | 666.4                 | 8.0                   | 2.5                   | 1645.1                | 26.5                  | 1155.8                |
| + R1-Distill                                 | 4.5                   | 33620.5               | 2.5                   | 0.5                   | 21221.9               | 2.5                   | 27421.2               |
| Reflexion (Shinn et al., 2023)               | 42.5                  | 2340.8                | 10.1                  | 1.0                   | 3396.8                | 21.8                  | 2868.8                |
| SFT                                          | 62.5                  | 494.1                 | 54.1                  | 19.0                  | 660.8                 | 40.8                  | 577.5                 |
| ETO (Song et al., 2024)                      | 69.5                  | 540.6                 | 53.9                  | 21.5                  | 694.3                 | 45.5                  | 617.4                 |
| AdaptThink (Zhang et al., 2025b)             | 81.5                  | 1372.1                | 62.5                  | 44.5                  | 1314.2                | 63.0                  | 1343.2                |
| COSFT exp                                    | 78.5                  | 3151.0                | 61.2                  | 29.5                  | 3973.8                | 54.0                  | 3562.4                |
| COSFT                                        | 57.0                  | 3912.7                | 61.1                  | 35.5                  | 3643.4                | 46.3                  | 3778.1                |
| + GRPO (Shao et al., 2024)                   | 83.5                  | 4994.6                | 71.1                  | 53.0                  | 3739.9                | 68.3                  | 4367.3                |
| + GiGPO (Feng et al., 2025)                  | 88.0                  | 2955.5                | 67.3                  | 47.0                  | 4602.8                | 67.5                  | 3779.2                |
| + COPO (Ours)                                | 92.5                  | 1739.4                | 84.6                  | 72.0                  | 1543.4                | 82.3                  | 1641.4                |
| Llama3.1-8B-Instruct                         | Llama3.1-8B-Instruct  | Llama3.1-8B-Instruct  | Llama3.1-8B-Instruct  | Llama3.1-8B-Instruct  | Llama3.1-8B-Instruct  | Llama3.1-8B-Instruct  | Llama3.1-8B-Instruct  |
| ReAct (Yao et al., 2023b)                    | 13.0                  | 959.4                 | 13.7                  | 3.0                   | 1268.1                | 8.0                   | 1113.8                |
| + R1-Distill                                 | 2.5                   | 21720.8               | 2.2                   | 0.0                   | 6940.8                | 1.3                   | 14330.8               |
| Reflexion (Shinn et al., 2023)               | 18.5                  | 3645.8                | 17.8                  | 2.5                   | 5072.4                | 10.5                  | 4359.1                |
| SFT                                          | 54.5                  | 632.0                 | 59.9                  | 24.0                  | 906.6                 | 39.3                  | 769.3                 |
| ETO (Song et al., 2024)                      | 65.0                  | 730.0                 | 62.5                  | 38.5                  | 784.2                 | 51.8                  | 757.1                 |
| AdaptThink (Zhang et al., 2025b)             | 79.5                  | 1607.2                | 52.3                  | 45.5                  | 1666.8                | 62.5                  | 1637.0                |
| COSFT exp                                    | 82.0                  | 2903.2                | 73.3                  | 49.5                  | 4588.9                | 65.8                  | 3746.1                |
| COSFT                                        | 55.5                  | 3546.6                | 56.4                  | 26.0                  | 3728.0                | 40.8                  | 3637.3                |
| + GRPO (Shao et al., 2024)                   | 84.0                  | 3941.1                | 75.1                  | 50.5                  | 5757.6                | 67.3                  | 4849.4                |
| + GiGPO (Feng et al., 2025)                  | 89.5                  | 6112.0                | 78.2                  | 64.0                  | 4651.3                | 76.8                  | 5381.7                |
| + COPO (Ours)                                | 91.5                  | 811.6                 | 83.7                  | 70.5                  | 963.8                 | 81.0                  | 887.7                 |

based on context. Reinforcement learning is therefore essential for discovering step-wise optimal cognitive patterns.

CoPOprevents format collapse through confidence-aware credit assignment. RLbaselines struggle to adapt cognitive depth at the step level (Figure 3). Although all methods begin with the same COSFT initialization and a balanced level distribution, GRPO and GiGPO quickly collapse to predominantly L 4 thinking ( e . g ., 76.8% and 76.6% L 4 on ALFWorld with Qwen2.5-7B). This collapse stems from coarse credit assignment: GRPO distributes advantages uniformly across all tokens, failing to distinguish whether deep thinking is contextually appropriate at each step. GiGPO's statebased grouping also struggles, as identical states may require different cognitive depths depending on task progression. As a result, both methods converge to fixed L 4 thinking, since deeper thinking often correlates with higher final rewards. CoPO prevents this collapse via confidence-aware advantage reweighting (§3.4), which evaluates each cognitive level based on how confidently the model predicts its action under that cognitive pattern. This enables step-wise adaptive thinking, applying deeper thinking only when beneficial ( e . g ., 55.0% L 1 , 18.7% L 2, 14.0% L 3, 12.3% L 4 on ALFWorld with Qwen2.5-7B). Moreover, CoPO converges substantially faster than prior RL methods (Figure 4). With Qwen2.5-7B, it reaches 90% success rate on ALFWorld within 100 steps, whereas

<!-- image -->

Figure 3: Cognitive level distribution after training. All RL methods (GRPO, GiGPO, COPO) are initialized from COSFT. While GRPO and GiGPO collapse to predominantly L 4 thinking, COPO learns adaptive allocation.

<!-- image -->

Figure 4: Training curves showing success rate across RL iterations for GRPO, GiGPO and COPO on ALFWorld and ScienceWorld. COPO achieves faster convergence to higher success rates.

<!-- image -->

GRPO plateaus at 83.3% after 150 steps; on ScienceWorld, CoPO reaches 76.6% by step 80 compared to GRPO's 59.7% ceiling. Similar trends appear for Llama3.1-8B. This acceleration arises from CoPO's step-level, confidence-guided credit assignment, which provides more precise learning signals than trajectory-level baselines.

COGROUTER achieves state-of-the-art performance with superior token efficiency. Table 1 shows that COGROUTER consistently outperforms all baselines across both benchmarks. With Qwen2.5-7B, it achieves an average success rate of 82.3%, surpassing larger frontier models such as GPT-4o (+40.3%) and OpenAI-o3 (+18.3%), as well as both offline and online methods, including ETO (+36.8%) and GRPO (+14.0%). Similar gains are observed with Llama3.1-8B, where COGROUTER reaches 81.0%, outperforming GRPO by 13.7% and GiGPO by 4.2%. Notably, COGROUTER achieves this with superior token efficiency: using 62% fewer tokens than GRPO (1641.4 compared to 4367.3) and 57% fewer than GiGPO (1641.4 compared to 3779.2) with Qwen2.5-7B. These results highlight that adaptive cognitive depth is both more effective and more efficient than fixed cognitive patterns in long-horizon agentic tasks.

## 5 Analysis

## 5.1 Quantitative Analysis

While § 4.2 demonstrates COPO's superior performance and efficiency, its underlying mechanism remains unclear. To reveal why COPO succeeds, we analyze the learned cognitive level distributions along two dimensions: 1) Trajectory-level : how cognitive patterns evolve across trajectory stages, and 2) Task-level : how cognitive depth scales with task complexity.

COPO learns to allocate cognitive resources dynamically based on the trajectory stage. We examine how the four cognitive levels are distributed across task progress in ScienceWorld, using Qwen2.5-7B as the base model (Figure 5, left). COPO exhibits distinct, contextually appropriate patterns for each level. L 4 peaks at initialization (21.9% at 0% progress) when complex tasks require

Figure 5: Cognitive level distributions across trajectory progress (left) and task complexity (right) for COPO and GRPO on ScienceWorld.

<!-- image -->

<!-- image -->

global goal evaluation and long-horizon planning, then declines to 6.8% as task structure becomes clear. L 2 also dominates initial stages (26.3% at 0% progress), where environmental awareness is critical for parsing observations and assessing available actions, then stabilizes around 10% as the agent maintains situational monitoring. As trajectories progress, L 1 surges from 48.5% initially to over 80% in late stages, reaching 94.8% at task completion with minimal L 3L 4 usage, reflecting that most steps become routine executions requiring no deliberation. L 3 maintains relatively uniform distribution around 10% across all stages, emerging contextually only when errors occur or past experience must inform decisions. This structured allocation is cognitively plausible: strategic planning dominates initialization, situational awareness guides exploration, routine execution increases as procedures crystallize, and reflection appears when needed. In contrast, GRPO saturates trajectories with L 4 thinking from start to finish (over 50% throughout), collapsing into uniform deep reasoning regardless of step complexity. 3 This leads to higher token consumption without performance gains. Detailed case studies are provided in Appendix F.

COPO adapts cognitive depth proportionally to task complexity. To investigate how cognitive levels adapt across tasks of varying complexity, we compare COPO and GRPO on ScienceWorld with Qwen2.5-7B as base model (Figure 5, right). We categorize tasks into Short (S), Medium (M), and Long (L) based on oracle trajectory lengths, where longer trajectories indicate higher complexity (see Appendix B for details). COPO demonstrates clear complexity-aware adaptation: as task difficulty increases from S to L, L 4 rises from 7.6% to 22.4% and L 3 increases from 8.6% to 15.2%, reflecting greater need for strategic planning and experiential reflection in complex scenarios. In contrast, L 1 decreases from 72.6% to 55.4% and L 2 drops from 11.2% to 7.0%, as instinctive responses can handle fewer steps when tasks become complex. This graduated allocation reveals that COPO scales cognitive effort proportionally with task complexity. In contrast, GRPO maintains nearly uniform distributions across all difficulty levels. The percentages of L 1 , L 2, L 3, and L 4 remain stable from short to long tasks, indicating an inability to adapt cognitive allocation to actual task demands.

## 5.2 Ablation Study

To validate the key design choices of COGROUTER, we conduct ablation studies as follows: 1) the confidence metric for advantage reweighting in COPO, and 2) training strategies for cognitive level distribution. Results are presented in Table 2.

## Average log-probability is the most effective confidence metric for assessing cognitive suitability.

We evaluate our choice of average log-probability (§3.4) as the confidence metric by comparing three alternatives: Max Probs uses the maximum log-probability across action tokens, Min Probs uses the minimum, and Entropy measures distributional uncertainty over the vocabulary. All variants are trained with Qwen2.5-7B initialized from COSFT. As shown in Table 2, average logprobability yields the strongest performance (92.5% SR on ALFWorld, 72.0% on ScienceWorld). Min Probs performs substantially worse (81.5%, 67.5%), as a single low-probability token dominates the confidence score regardless of overall prediction quality, creating unreliable credit assignment.

3 GiGPO similarly collapses to L 4 thinking. See Appendix E.2 for detailed analysis.

Table 2: Ablation studies on confidence metrics and training strategies. SR: Success Rate (%). #Tokens: Average tokens per trajectory. L 1 L 4 : Cognitive level distribution (%).

| Variant                    | ALFWorld                   | ALFWorld                   | ALFWorld                   | ALFWorld                   | ALFWorld                   | ALFWorld                   | ScienceWorld               | ScienceWorld               | ScienceWorld               | ScienceWorld               | ScienceWorld               | ScienceWorld               |
|----------------------------|----------------------------|----------------------------|----------------------------|----------------------------|----------------------------|----------------------------|----------------------------|----------------------------|----------------------------|----------------------------|----------------------------|----------------------------|
|                            | SR                         | #Tokens                    | L 1                        | L 2                        | L 3                        | L 4                        | SR                         | #Tokens                    | L 1                        | L 2                        | L 3                        | L 4                        |
| Confidence Metric Ablation | Confidence Metric Ablation | Confidence Metric Ablation | Confidence Metric Ablation | Confidence Metric Ablation | Confidence Metric Ablation | Confidence Metric Ablation | Confidence Metric Ablation | Confidence Metric Ablation | Confidence Metric Ablation | Confidence Metric Ablation | Confidence Metric Ablation | Confidence Metric Ablation |
| COPO (Ours)                | 92.5                       | 1739.4                     | 55.0                       | 18.7                       | 14.0                       | 12.3                       | 72.0                       | 1543.4                     | 72.6                       | 11.2                       | 8.6                        | 7.6                        |
| w/ Max Probs               | 89.0                       | 1664.9                     | 65.7                       | 12.3                       | 11.4                       | 8.4                        | 69.5                       | 2167.5                     | 68.3                       | 15.2                       | 9.5                        | 7.1                        |
| w/ Min Probs               | 81.5                       | 1227.7                     | 92.7                       | 0.7                        | 2.6                        | 4.0                        | 67.5                       | 2063.6                     | 69.5                       | 12.7                       | 10.7                       | 7.1                        |
| w/ Entropy                 | 79.0                       | 1584.6                     | 73.9                       | 11.5                       | 6.3                        | 8.3                        | 70.0                       | 1873.9                     | 72.0                       | 12.7                       | 8.6                        | 6.7                        |
| Training Strategy Ablation | Training Strategy Ablation | Training Strategy Ablation | Training Strategy Ablation | Training Strategy Ablation | Training Strategy Ablation | Training Strategy Ablation | Training Strategy Ablation | Training Strategy Ablation | Training Strategy Ablation | Training Strategy Ablation | Training Strategy Ablation | Training Strategy Ablation |
| w/o Cold Start             | 86.0                       | 2496.2                     | 0.3                        | 35.0                       | 33.5                       | 31.2                       | 64.5                       | 3750.6                     | 0.0                        | 27.1                       | 35.5                       | 37.3                       |
| w/ COSFT expert            | 87.5                       | 2871.6                     | 0.0                        | 7.9                        | 92.1                       | 0.0                        | 62.0                       | 4383.0                     | 0.0                        | 24.3                       | 69.1                       | 0.6                        |
| w/ Corr &Incorr            | 83.0                       | 1375.8                     | 83.1                       | 4.3                        | 7.7                        | 4.8                        | 66.0                       | 1755.6                     | 77.6                       | 9.9                        | 6.9                        | 5.6                        |

Entropy similarly degrades performance (79.0%, 70.0%), as it measures distributional uncertainty over the full vocabulary rather than whether the cognitive pattern supports the action being executed. Max Probs achieves moderate results (89.0%, 69.5%) but overweights local peaks, ignoring the global coherence of the action sequence. These results validate average log-probability as the optimal confidence metric for COPO.

Balanced initialization and success-only reweighting are crucial for effective adaptation. We examine two critical design decisions in COPO training: using balanced COSFT for cold-start initialization, and applying confidence-based reweighting only to successful trajectories. We compare against three variants (Table 2). Both w/o Cold Start (directly applying RL without SFT) and w/ CoSFTexpert (initializing from GPT-4o's expert-selected levels) demonstrate the necessity of balanced distribution. Without cold start, the model exhibits severe distribution collapse with extreme skew ( e . g ., 0.3% L 1 on ALFWorld, 0.0% on ScienceWorld). With expert initialization, it inherits teacher preferences, collapsing to L 3 (92.1%) while ignoring L 1 and L 4 (0.0%). Both prevent effective exploration during RL, as the model either fails to establish stable cognitive formatting or becomes trapped in biased patterns. This leads to degraded performance (86.0% and 87.5% compared to 92.5%) and increased token usage (2496.2 and 2871.6 compared to 1739.4). Applying reweighting to both successful and failed trajectories ( w/ Corr &amp; Incorr ) creates adverse incentives. Failed trajectories receive stronger penalties for high confidence, encouraging the model to avoid certainty. The model exploits this by collapsing to L 1 (83.1%) to evade 'confident mistakes', since L 1 's minimal thinking yields lower confidence and lighter penalties. This reduces tokens (1375.8) but sacrifices performance (83.0%). In contrast, COPO achieves 92.5% SR with 1739.4 tokens and adaptive distribution.

## 6 Related Work

Interactive and Agentic Environments. Recent studies have introduced evaluation tasks for language agents requiring long-horizon planning and strategic reasoning in multi-turn, goal-driven settings, including code generation (Jimenez et al., 2024; Team et al., 2026), embodied intelligence (Xi et al., 2024; Shridhar et al., 2020; Wang et al., 2022), social deduction games (Yang et al., 2024b; 2025c; tse Huang et al., 2025), and emotional intelligence (Zhang et al., 2025a; Chen et al., 2024; Yi et al., 2025). Unlike single-turn reasoning tasks, these environments feature sparse terminal rewards over long horizons and heterogeneous step-wise complexity. Some decisions require strategic planning while many are routine executions. This motivates adaptive cognitive depth allocation rather than uniform cognitive patterns.

Hybrid and Adaptive Reasoning. Recent large reasoning models such as OpenAI-o1 (OpenAI, 2025b), DeepSeek-R1 (DeepSeek-AI et al., 2025a), and Gemini-2.0-Flash-Thinking (Gemini Team,

2025) demonstrate impressive capabilities through extended chain-of-thought reasoning. However, they apply uniform deep thinking throughout trajectories. To address efficiency, recent work explores adaptive thinking (Zhang et al., 2025b; Lou et al., 2025; Yang et al., 2025a), enabling models to dynamically switch between thinking and non-thinking modes based on problem difficulty. While effective for single-turn reasoning tasks, these binary approaches cannot capture the stepwise, multi-level cognitive demands within agentic tasks, where some steps require situational awareness or experience integration beyond simple binary switching. Moreover, they operate at the trajectory level, uniformly incentivizing efficiency across all steps rather than adapting to step-specific complexity. We propose step-level adaptive cognitive allocation grounded in ACT-R theory (Anderson, 1982), introducing four hierarchical cognitive levels with confidence-aware credit assignment for fine-grained, step-wise adaptation.

RL for Language Models. Reinforcement learning has proven effective for aligning large language models (LLMs) (Ouyang et al., 2022; Rafailov et al., 2024) and enhancing their reasoning capabilities (Hu et al., 2025; Wang et al., 2025; Xin et al., 2025). Recently, group-based RL algorithms such as GRPO (Shao et al., 2024), Dr.GRPO (Liu et al., 2025), and DAPO (Yu et al., 2025) have been introduced. These methods estimate advantages from batches of samples without relying on critic models. While effective in single-turn reasoning tasks, they face limitations in multi-turn, agentic settings. Specifically, they distribute trajectory-level advantages uniformly across tokens, making it difficult to evaluate whether the applied cognitive pattern was appropriate at each step, especially under sparse terminal rewards. GiGPO (Feng et al., 2025) addresses this by grouping steps via anchor states. However, state similarity alone does not guarantee cognitive relevance or appropriateness of reasoning. To tackle this challenge, we propose COPO, which estimates the cognitive depth at each step using action prediction confidence. This enables more precise, step-level credit assignment.

## 7 Conclusion

In this study, we addressed the inefficiency of fixed cognitive patterns in long-horizon agentic tasks. We introduced COGROUTER, a framework grounded in ACT-R theory that enables agents to dynamically allocate cognitive depth across four levels, from instinctive responses to strategic planning. Through COSFT and COPO, we demonstrated that agents can learn to match their reasoning depth to step complexity. Our COPO algorithm effectively solves step-level credit assignment by leveraging action prediction confidence, preventing the mode collapse observed in standard trajectory-level RL methods. Experiments on ALFWorld and ScienceWorld confirm that COGROUTER achieves state-of-the-art performance while reducing token consumption compared to uniform reasoning models. This work highlights the critical role of adaptive cognitive allocation in building efficient and effective language agents. Future research directions include exploring adaptive mechanisms for broader ranges of tasks and investigating self-evolving cognitive hierarchies.

## References

John R Anderson. Acquisition of cognitive skill. Psychological Review , 89(4):369-406, 1982.

John R Anderson. Rules of the Mind . Psychology Press, 1993.

John R Anderson. How Can the Human Mind Occur in the Physical Universe? Oxford University Press, 2007.

John R Anderson and Christian Lebiere. The Atomic Components of Thought . Psychology Press, 1998.

Jiangjie Chen, Xintao Wang, Rui Xu, Siyu Yuan, Yikai Zhang, Wei Shi, Jian Xie, Shuang Li, Ruihan Yang, Tinghui Zhu, Aili Chen, Nianqi Li, Lida Chen, Caiyu Hu, Siye Wu, Scott Ren, Ziquan Fu, and Yanghua Xiao. From Persona to Personalization: A Survey on Role-Playing Language Agents, 2024. URL https://arxiv.org/abs/2404.18231 .

DeepSeek-AI, Daya Guo, Dejian Yang, Haowei Zhang, Junxiao Song, Ruoyu Zhang, Runxin Xu, Qihao Zhu, Shirong Ma, Peiyi Wang, Xiao Bi, Xiaokang Zhang, Xingkai Yu, Yu Wu, Z. F. Wu, Zhibin Gou, Zhihong Shao, Zhuoshu Li, Ziyi Gao, Aixin Liu, Bing Xue, Bingxuan Wang, Bochao Wu, Bei Feng, Chengda Lu, Chenggang Zhao, Chengqi Deng, Chenyu Zhang, Chong Ruan, Damai Dai, Deli Chen, Dongjie Ji, Erhang Li, Fangyun Lin, Fucong Dai, Fuli Luo, Guangbo Hao, Guanting Chen, Guowei Li, H. Zhang, Han Bao, Hanwei Xu, Haocheng Wang, Honghui Ding, Huajian Xin, Huazuo Gao, Hui Qu, Hui Li, Jianzhong Guo, Jiashi Li, Jiawei Wang, Jingchang Chen, Jingyang Yuan, Junjie Qiu, Junlong Li, J. L. Cai, Jiaqi Ni, Jian Liang, Jin Chen, Kai Dong, Kai Hu, Kaige Gao, Kang Guan, Kexin Huang, Kuai Yu, Lean Wang, Lecong Zhang, Liang Zhao, Litong Wang, Liyue Zhang, Lei Xu, Leyi Xia, Mingchuan Zhang, Minghua Zhang, Minghui Tang, Meng Li, Miaojun Wang, Mingming Li, Ning Tian, Panpan Huang, Peng Zhang, Qiancheng Wang, Qinyu Chen, Qiushi Du, Ruiqi Ge, Ruisong Zhang, Ruizhe Pan, Runji Wang, R. J. Chen, R. L. Jin, Ruyi Chen, Shanghao Lu, Shangyan Zhou, Shanhuang Chen, Shengfeng Ye, Shiyu Wang, Shuiping Yu, Shunfeng Zhou, Shuting Pan, S. S. Li, Shuang Zhou, Shaoqing Wu, Shengfeng Ye, Tao Yun, Tian Pei, Tianyu Sun, T. Wang, Wangding Zeng, Wanjia Zhao, Wen Liu, Wenfeng Liang, Wenjun Gao, Wenqin Yu, Wentao Zhang, W. L. Xiao, Wei An, Xiaodong Liu, Xiaohan Wang, Xiaokang Chen, Xiaotao Nie, Xin Cheng, Xin Liu, Xin Xie, Xingchao Liu, Xinyu Yang, Xinyuan Li, Xuecheng Su, Xuheng Lin, X. Q. Li, Xiangyue Jin, Xiaojin Shen, Xiaosha Chen, Xiaowen Sun, Xiaoxiang Wang, Xinnan Song, Xinyi Zhou, Xianzu Wang, Xinxia Shan, Y. K. Li, Y. Q. Wang, Y. X. Wei, Yang Zhang, Yanhong Xu, Yao Li, Yao Zhao, Yaofeng Sun, Yaohui Wang, Yi Yu, Yichao Zhang, Yifan Shi, Yiliang Xiong, Ying He, Yishi Piao, Yisong Wang, Yixuan Tan, Yiyang Ma, Yiyuan Liu, Yongqiang Guo, Yuan Ou, Yuduan Wang, Yue Gong, Yuheng Zou, Yujia He, Yunfan Xiong, Yuxiang Luo, Yuxiang You, Yuxuan Liu, Yuyang Zhou, Y. X. Zhu, Yanhong Xu, Yanping Huang, Yaohui Li, Yi Zheng, Yuchen Zhu, Yunxian Ma, Ying Tang, Yukun Zha, Yuting Yan, Z. Z. Ren, Zehui Ren, Zhangli Sha, Zhe Fu, Zhean Xu, Zhenda Xie, Zhengyan Zhang, Zhewen Hao, Zhicheng Ma, Zhigang Yan, Zhiyu Wu, Zihui Gu, Zijia Zhu, Zijun Liu, Zilin Li, Ziwei Xie, Ziyang Song, Zizheng Pan, Zhen Huang, Zhipeng Xu, Zhongyu Zhang, and Zhen Zhang. DeepSeek-R1: Incentivizing Reasoning Capability in LLMs via Reinforcement Learning, 2025a. URL https://arxiv.org/abs/2501.12948 .

DeepSeek-AI, Aixin Liu, Bei Feng, Bing Xue, Bingxuan Wang, Bochao Wu, Chengda Lu, Chenggang Zhao, Chengqi Deng, Chenyu Zhang, Chong Ruan, Damai Dai, Daya Guo, Dejian Yang, Deli Chen, Dongjie Ji, Erhang Li, Fangyun Lin, Fucong Dai, Fuli Luo, Guangbo Hao, Guanting Chen, Guowei Li, H. Zhang, Han Bao, Hanwei Xu, Haocheng Wang, Haowei Zhang, Honghui Ding, Huajian Xin, Huazuo Gao, Hui Li, Hui Qu, J. L. Cai, Jian Liang, Jianzhong Guo, Jiaqi Ni, Jiashi Li, Jiawei Wang, Jin Chen, Jingchang Chen, Jingyang Yuan, Junjie Qiu, Junlong Li, Junxiao Song, Kai Dong, Kai Hu, Kaige Gao, Kang Guan, Kexin Huang, Kuai Yu, Lean Wang, Lecong Zhang, Lei Xu, Leyi Xia, Liang Zhao, Litong Wang, Liyue Zhang, Meng Li, Miaojun Wang, Mingchuan Zhang, Minghua Zhang, Minghui Tang, Mingming Li, Ning Tian, Panpan Huang, Peiyi Wang, Peng Zhang, Qiancheng Wang, Qihao Zhu, Qinyu Chen, Qiushi Du, R. J. Chen, R. L. Jin, Ruiqi Ge, Ruisong Zhang, Ruizhe Pan, Runji Wang, Runxin Xu, Ruoyu Zhang, Ruyi Chen, S. S. Li, Shanghao Lu, Shangyan Zhou, Shanhuang Chen, Shaoqing Wu, Shengfeng Ye, Shengfeng Ye, Shirong Ma, Shiyu Wang, Shuang Zhou, Shuiping Yu, Shunfeng Zhou, Shuting Pan, T. Wang, Tao Yun, Tian Pei, Tianyu Sun, W. L. Xiao, Wangding Zeng, Wanjia Zhao, Wei An, Wen Liu, Wenfeng Liang, Wenjun Gao, Wenqin Yu, Wentao Zhang, X. Q. Li, Xiangyue Jin, Xianzu Wang, Xiao Bi, Xiaodong Liu, Xiaohan Wang, Xiaojin Shen, Xiaokang Chen, Xiaokang Zhang, Xiaosha Chen, Xiaotao Nie, Xiaowen Sun, Xiaoxiang Wang, Xin Cheng, Xin Liu, Xin Xie, Xingchao Liu, Xingkai Yu, Xinnan Song, Xinxia Shan, Xinyi Zhou, Xinyu Yang, Xinyuan Li, Xuecheng Su, Xuheng Lin, Y. K. Li, Y. Q. Wang, Y. X. Wei, Y. X. Zhu, Yang Zhang, Yanhong Xu, Yanhong Xu, Yanping Huang, Yao Li, Yao Zhao, Yaofeng Sun, Yaohui Li, Yaohui Wang, Yi Yu, Yi Zheng, Yichao Zhang, Yifan Shi, Yiliang Xiong, Ying He, Ying Tang, Yishi Piao, Yisong Wang, Yixuan Tan, Yiyang Ma, Yiyuan Liu, Yongqiang Guo, Yu Wu, Yuan Ou, Yuchen Zhu, Yuduan Wang, Yue Gong, Yuheng Zou, Yujia He, Yukun Zha, Yunfan Xiong, Yunxian Ma, Yuting Yan, Yuxiang Luo, Yuxiang You, Yuxuan Liu, Yuyang Zhou, Z. F. Wu, Z. Z. Ren, Zehui Ren, Zhangli Sha, Zhe Fu, Zhean Xu, Zhen Huang, Zhen Zhang, Zhenda Xie, Zhengyan Zhang, Zhewen Hao, Zhibin Gou, Zhicheng Ma, Zhigang Yan,

Zhihong Shao, Zhipeng Xu, Zhiyu Wu, Zhongyu Zhang, Zhuoshu Li, Zihui Gu, Zijia Zhu, Zijun Liu, Zilin Li, Ziwei Xie, Ziyang Song, Ziyi Gao, and Zizheng Pan. DeepSeek-V3 Technical Report, 2025b. URL https://arxiv.org/abs/2412.19437 .

Lang Feng, Zhenghai Xue, Tingcong Liu, and Bo An. Group-in-Group Policy Optimization for LLM Agent Training, 2025. URL https://arxiv.org/abs/2505.10978 .

Gemini Team. Gemini: A family of highly capable multimodal models. Technical report, Google, 12 2023. URL https://storage.googleapis.com/deepmind-media/gemini/gemini\_1\_report. pdf .

Gemini Team. Gemini 2.5: Pushing the Frontier with Advanced Reasoning, Multimodality, Long Context, and Next Generation Agentic Capabilities, 2025. URL https://arxiv.org/abs/2507. 06261 .

Jingcheng Hu, Yinmin Zhang, Qi Han, Daxin Jiang, Xiangyu Zhang, and Heung-Yeung Shum. Open-reasoner-zero: An open source approach to scaling up reinforcement learning on the base model, 2025. URL https://arxiv.org/abs/2503.24290 .

Carlos E Jimenez, John Yang, Alexander Wettig, Shunyu Yao, Kexin Pei, Ofir Press, and Karthik R Narasimhan. SWE-bench: Can language models resolve real-world github issues? In The Twelfth International Conference on Learning Representations , 2024. URL https://openreview.net/forum? id=VTF8yNQM66 .

Bill Yuchen Lin, Yicheng Fu, Karina Yang, Faeze Brahman, Shiyu Huang, Chandra Bhagavatula, Prithviraj Ammanabrolu, Yejin Choi, and Xiang Ren. SwiftSage: A Generative Agent with Fast and Slow Thinking for Complex Interactive Tasks, 2023. URL https://arxiv.org/abs/2305.17390 .

Zichen Liu, Changyu Chen, Wenjun Li, Penghui Qi, Tianyu Pang, Chao Du, Wee Sun Lee, and Min Lin. Understanding R1-Zero-Like Training: A Critical Perspective, 2025. URL https: //arxiv.org/abs/2503.20783 .

Chenwei Lou, Zewei Sun, Xinnian Liang, Meng Qu, Wei Shen, Wenqi Wang, Yuntao Li, Qingping Yang, and Shuangzhi Wu. AdaCoT: Pareto-Optimal Adaptive Chain-of-Thought Triggering via Reinforcement Learning, 2025. URL https://arxiv.org/abs/2505.11896 .

Yifei Lu, Fanghua Ye, Jian Li, Qiang Gao, Cheng Liu, Haibo Luo, Nan Du, Xiaolong Li, and Feiliang Ren. CodeTool: Enhancing Programmatic Tool Invocation of LLMs via Process Supervision. arXiv preprint arXiv:2503.20840 , 2025.

Meta. Llama 3 Model Card. 2024. URL https://github.com/meta-llama/llama3/blob/main/ MODEL\_CARD.md .

OpenAI. GPT-4 Technical Report, 2023. URL https://arxiv.org/abs/2303.08774 .

OpenAI. Hello gpt-4o, 2025a. URL https://openai.com/index/hello-gpt-4o/ .

OpenAI. OpenAI o3-mini. https://openai.com/index/openai-o3-mini/ , 2025b. Accessed: 202501-31.

Long Ouyang, Jeff Wu, Xu Jiang, Diogo Almeida, Carroll L. Wainwright, Pamela Mishkin, Chong Zhang, Sandhini Agarwal, Katarina Slama, Alex Ray, John Schulman, Jacob Hilton, Fraser Kelton, Luke Miller, Maddie Simens, Amanda Askell, Peter Welinder, Paul Christiano, Jan Leike, and Ryan Lowe. Training language models to follow instructions with human feedback, 2022. URL https://arxiv.org/abs/2203.02155 .

Rafael Rafailov, Archit Sharma, Eric Mitchell, Stefano Ermon, Christopher D. Manning, and Chelsea Finn. Direct Preference Optimization: Your Language Model is Secretly a Reward Model, 2024. URL https://arxiv.org/abs/2305.18290 .

Zhihong Shao, Peiyi Wang, Qihao Zhu, Runxin Xu, Junxiao Song, Xiao Bi, Haowei Zhang, Mingchuan Zhang, Y. K. Li, Y. Wu, and Daya Guo. DeepSeekMath: Pushing the Limits of Mathematical Reasoning in Open Language Models, 2024. URL https://arxiv.org/abs/2402.03300 .

Guangming Sheng, Chi Zhang, Zilingfeng Ye, Xibin Wu, Wang Zhang, Ru Zhang, Yanghua Peng, Haibin Lin, and Chuan Wu. HybridFlow: A Flexible and Efficient RLHF Framework. In Proceedings of the Twentieth European Conference on Computer Systems , EuroSys '25, page 1279-1297. ACM, March 2025. doi: 10.1145/3689031.3696075. URL http://dx.doi.org/10.1145/3689031.3696075 .

Noah Shinn, Federico Cassano, Edward Berman, Ashwin Gopinath, Karthik Narasimhan, and Shunyu Yao. Reflexion: Language Agents with Verbal Reinforcement Learning, 2023. URL https://arxiv.org/abs/2303.11366 .

Mohit Shridhar, Xingdi Yuan, Marc-Alexandre Cˆ ot´ e, Yonatan Bisk, Adam Trischler, and Matthew Hausknecht. Alfworld: Aligning text and embodied environments for interactive learning. arXiv preprint arXiv:2010.03768 , 2020.

Yifan Song, Da Yin, Xiang Yue, Jie Huang, Sujian Li, and Bill Yuchen Lin. Trial and Error: ExplorationBased Trajectory Optimization for LLM Agents, 2024. URL https://arxiv.org/abs/2403.02502 .

Niels A Taatgen. The nature and transfer of cognitive skills. Psychological Review , 120(3):439-471, 2013.

Kimi Team, Tongtong Bai, Yifan Bai, Yiping Bao, S. H. Cai, Yuan Cao, Y. Charles, H. S. Che, Cheng Chen, Guanduo Chen, Huarong Chen, Jia Chen, Jiahao Chen, Jianlong Chen, Jun Chen, Kefan Chen, Liang Chen, Ruijue Chen, Xinhao Chen, Yanru Chen, Yanxu Chen, Yicun Chen, Yimin Chen, Yingjiang Chen, Yuankun Chen, Yujie Chen, Yutian Chen, Zhirong Chen, Ziwei Chen, Dazhi Cheng, Minghan Chu, Jialei Cui, Jiaqi Deng, Muxi Diao, Hao Ding, Mengfan Dong, Mengnan Dong, Yuxin Dong, Yuhao Dong, Angang Du, Chenzhuang Du, Dikang Du, Lingxiao Du, Yulun Du, Yu Fan, Shengjun Fang, Qiulin Feng, Yichen Feng, Garimugai Fu, Kelin Fu, Hongcheng Gao, Tong Gao, Yuyao Ge, Shangyi Geng, Chengyang Gong, Xiaochen Gong, Zhuoma Gongque, Qizheng Gu, Xinran Gu, Yicheng Gu, Longyu Guan, Yuanying Guo, Xiaoru Hao, Weiran He, Wenyang He, Yunjia He, Chao Hong, Hao Hu, Jiaxi Hu, Yangyang Hu, Zhenxing Hu, Ke Huang, Ruiyuan Huang, Weixiao Huang, Zhiqi Huang, Tao Jiang, Zhejun Jiang, Xinyi Jin, Yu Jing, Guokun Lai, Aidi Li, C. Li, Cheng Li, Fang Li, Guanghe Li, Guanyu Li, Haitao Li, Haoyang Li, Jia Li, Jingwei Li, Junxiong Li, Lincan Li, Mo Li, Weihong Li, Wentao Li, Xinhang Li, Xinhao Li, Yang Li, Yanhao Li, Yiwei Li, Yuxiao Li, Zhaowei Li, Zheming Li, Weilong Liao, Jiawei Lin, Xiaohan Lin, Zhishan Lin, Zichao Lin, Cheng Liu, Chenyu Liu, Hongzhang Liu, Liang Liu, Shaowei Liu, Shudong Liu, Shuran Liu, Tianwei Liu, Tianyu Liu, Weizhou Liu, Xiangyan Liu, Yangyang Liu, Yanming Liu, Yibo Liu, Yuanxin Liu, Yue Liu, Zhengying Liu, Zhongnuo Liu, Enzhe Lu, Haoyu Lu, Zhiyuan Lu, Junyu Luo, Tongxu Luo, Yashuo Luo, Long Ma, Yingwei Ma, Shaoguang Mao, Yuan Mei, Xin Men, Fanqing Meng, Zhiyong Meng, Yibo Miao, Minqing Ni, Kun Ouyang, Siyuan Pan, Bo Pang, Yuchao Qian, Ruoyu Qin, Zeyu Qin, Jiezhong Qiu, Bowen Qu, Zeyu Shang, Youbo Shao, Tianxiao Shen, Zhennan Shen, Juanfeng Shi, Lidong Shi, Shengyuan Shi, Feifan Song, Pengwei Song, Tianhui Song, Xiaoxi Song, Hongjin Su, Jianlin Su, Zhaochen Su, Lin Sui, Jinsong Sun, Junyao Sun, Tongyu Sun, Flood Sung, Yunpeng Tai, Chuning Tang, Heyi Tang, Xiaojuan Tang, Zhengyang Tang, Jiawen Tao, Shiyuan Teng, Chaoran Tian, Pengfei Tian, Ao Wang, Bowen Wang, Chensi Wang, Chuang Wang, Congcong Wang, Dingkun Wang, Dinglu Wang, Dongliang Wang, Feng Wang, Hailong Wang, Haiming Wang, Hengzhi Wang, Huaqing Wang, Hui Wang, Jiahao Wang, Jinhong Wang, Jiuzheng Wang, Kaixin Wang, Linian Wang, Qibin Wang, Shengjie Wang, Shuyi Wang, Si Wang, Wei Wang, Xiaochen Wang, Xinyuan Wang, Yao Wang, Yejie Wang, Yipu Wang, Yiqin Wang, Yucheng Wang, Yuzhi Wang, Zhaoji Wang, Zhaowei Wang, Zhengtao Wang, Zhexu Wang, Zihan Wang, Zizhe Wang, Chu Wei, Ming Wei, Chuan Wen, Zichen Wen, Chengjie Wu, Haoning Wu, Junyan Wu, Rucong Wu, Wenhao Wu, Yuefeng Wu, Yuhao Wu, Yuxin Wu, Zijian Wu, Chenjun Xiao, Jin Xie, Xiaotong Xie, Yuchong Xie, Yifei Xin, Bowei Xing, Boyu Xu, Jianfan Xu, Jing Xu, Jinjing Xu, L. H. Xu, Lin Xu, Suting Xu, Weixin Xu, Xinbo Xu, Xinran Xu,

Yangchuan Xu, Yichang Xu, Yuemeng Xu, Zelai Xu, Ziyao Xu, Junjie Yan, Yuzi Yan, Guangyao Yang, Hao Yang, Junwei Yang, Kai Yang, Ningyuan Yang, Ruihan Yang, Xiaofei Yang, Xinlong Yang, Ying Yang, Yi Yang, Yi Yang, Zhen Yang, Zhilin Yang, Zonghan Yang, Haotian Yao, Dan Ye, Wenjie Ye, Zhuorui Ye, Bohong Yin, Chengzhen Yu, Longhui Yu, Tao Yu, Tianxiang Yu, Enming Yuan, Mengjie Yuan, Xiaokun Yuan, Yang Yue, Weihao Zeng, Dunyuan Zha, Haobing Zhan, Dehao Zhang, Hao Zhang, Jin Zhang, Puqi Zhang, Qiao Zhang, Rui Zhang, Xiaobin Zhang, Y. Zhang, Yadong Zhang, Yangkun Zhang, Yichi Zhang, Yizhi Zhang, Yongting Zhang, Yu Zhang, Yushun Zhang, Yutao Zhang, Yutong Zhang, Zheng Zhang, Chenguang Zhao, Feifan Zhao, Jinxiang Zhao, Shuai Zhao, Xiangyu Zhao, Yikai Zhao, Zijia Zhao, Huabin Zheng, Ruihan Zheng, Shaojie Zheng, Tengyang Zheng, Junfeng Zhong, Longguang Zhong, Weiming Zhong, M. Zhou, Runjie Zhou, Xinyu Zhou, Zaida Zhou, Jinguo Zhu, Liya Zhu, Xinhao Zhu, Yuxuan Zhu, Zhen Zhu, Jingze Zhuang, Weiyu Zhuang, Ying Zou, and Xinxing Zu. Kimi K2.5: Visual Agentic Intelligence, 2026. URL https://arxiv.org/abs/2602.02276 .

Jen tse Huang, Eric John Li, Man Ho Lam, Tian Liang, Wenxuan Wang, Youliang Yuan, Wenxiang Jiao, Xing Wang, Zhaopeng Tu, and Michael R. Lyu. How Far Are We on the Decision-Making of LLMs? Evaluating LLMs' Gaming Ability in Multi-Agent Environments, 2025. URL https: //arxiv.org/abs/2403.11807 .

Minzheng Wang, Yongbin Li, Haobo Wang, Xinghua Zhang, Nan Xu, Bingli Wu, Fei Huang, Haiyang Yu, and Wenji Mao. Adaptive Thinking via Mode Policy Optimization for Social Language Agents, 2025. URL https://arxiv.org/abs/2505.02156 .

Ruoyao Wang, Peter Jansen, Marc-Alexandre Cˆ ot´ e, and Prithviraj Ammanabrolu. ScienceWorld: Is your Agent Smarter than a 5th Grader?, 2022. URL https://arxiv.org/abs/2203.07540 .

Zhiheng Xi, Yiwen Ding, Wenxiang Chen, Boyang Hong, Honglin Guo, Junzhe Wang, Dingwen Yang, Chenyang Liao, Xin Guo, Wei He, Songyang Gao, Lu Chen, Rui Zheng, Yicheng Zou, Tao Gui, Qi Zhang, Xipeng Qiu, Xuanjing Huang, Zuxuan Wu, and Yu-Gang Jiang. AgentGym: Evolving Large Language Model-based Agents across Diverse Environments, 2024. URL https: //arxiv.org/abs/2406.04151 .

Ran Xin, Zeyu Zheng, Yanchen Nie, Kun Yuan, and Xia Xiao. Scaling up Multi-Turn Off-Policy RL and Multi-Agent Tree Search for LLM Step-Provers, 2025. URL https://arxiv.org/abs/2509. 06493 .

An Yang, Baosong Yang, Binyuan Hui, Bo Zheng, Bowen Yu, Chang Zhou, Chengpeng Li, Chengyuan Li, Dayiheng Liu, Fei Huang, Guanting Dong, Haoran Wei, Huan Lin, Jialong Tang, Jialin Wang, Jian Yang, Jianhong Tu, Jianwei Zhang, Jianxin Ma, Jianxin Yang, Jin Xu, Jingren Zhou, Jinze Bai, Jinzheng He, Junyang Lin, Kai Dang, Keming Lu, Keqin Chen, Kexin Yang, Mei Li, Mingfeng Xue, Na Ni, Pei Zhang, Peng Wang, Ru Peng, Rui Men, Ruize Gao, Runji Lin, Shijie Wang, Shuai Bai, Sinan Tan, Tianhang Zhu, Tianhao Li, Tianyu Liu, Wenbin Ge, Xiaodong Deng, Xiaohuan Zhou, Xingzhang Ren, Xinyu Zhang, Xipin Wei, Xuancheng Ren, Xuejing Liu, Yang Fan, Yang Yao, Yichang Zhang, Yu Wan, Yunfei Chu, Yuqiong Liu, Zeyu Cui, Zhenru Zhang, Zhifang Guo, and Zhihao Fan. Qwen2 Technical Report, 2024a. URL https://arxiv.org/abs/2407.10671 .

An Yang, Anfeng Li, Baosong Yang, Beichen Zhang, Binyuan Hui, Bo Zheng, Bowen Yu, Chang Gao, Chengen Huang, Chenxu Lv, Chujie Zheng, Dayiheng Liu, Fan Zhou, Fei Huang, Feng Hu, Hao Ge, Haoran Wei, Huan Lin, Jialong Tang, Jian Yang, Jianhong Tu, Jianwei Zhang, Jianxin Yang, Jiaxi Yang, Jing Zhou, Jingren Zhou, Junyang Lin, Kai Dang, Keqin Bao, Kexin Yang, Le Yu, Lianghao Deng, Mei Li, Mingfeng Xue, Mingze Li, Pei Zhang, Peng Wang, Qin Zhu, Rui Men, Ruize Gao, Shixuan Liu, Shuang Luo, Tianhao Li, Tianyi Tang, Wenbiao Yin, Xingzhang Ren, Xinyu Wang, Xinyu Zhang, Xuancheng Ren, Yang Fan, Yang Su, Yichang Zhang, Yinger Zhang, Yu Wan, Yuqiong Liu, Zekun Wang, Zeyu Cui, Zhenru Zhang, Zhipeng Zhou, and Zihan Qiu. Qwen3 Technical Report, 2025a. URL https://arxiv.org/abs/2505.09388 .

- Ruihan Yang, Jiangjie Chen, Yikai Zhang, Siyu Yuan, Aili Chen, Kyle Richardson, Yanghua Xiao, and Deqing Yang. SelfGoal: Your Language Agents Already Know How to Achieve High-level Goals, 2024b. URL https://arxiv.org/abs/2406.04784 .
- Ruihan Yang, Fanghua Ye, Jian Li, Siyu Yuan, Yikai Zhang, Zhaopeng Tu, Xiaolong Li, and Deqing Yang. The Lighthouse of Language: Enhancing LLM Agents via Critique-Guided Improvement, 2025b. URL https://arxiv.org/abs/2503.16024 .
- Ruihan Yang, Yikai Zhang, Aili Chen, Xintao Wang, Siyu Yuan, Jiangjie Chen, Deqing Yang, and Yanghua Xiao. ARIA: Training Language Agents with Intention-Driven Reward Aggregation, 2025c. URL https://arxiv.org/abs/2506.00539 .
- Shunyu Yao, Howard Chen, John Yang, and Karthik Narasimhan. WebShop: Towards Scalable Real-World Web Interaction with Grounded Language Agents, 2023a. URL https://arxiv.org/ abs/2207.01206 .
- Shunyu Yao, Jeffrey Zhao, Dian Yu, Nan Du, Izhak Shafran, Karthik R Narasimhan, and Yuan Cao. React: Synergizing reasoning and acting in language models. In The Eleventh International Conference on Learning Representations , 2023b. URL https://openreview.net/forum?id=WE\_ vluYUL-X .
- Zihao Yi, Qingxuan Jiang, Ruotian Ma, Xingyu Chen, Qu Yang, Mengru Wang, Fanghua Ye, Ying Shen, Zhaopeng Tu, Xiaolong Li, and Linus. Too Good to be Bad: On the Failure of LLMs to Role-Play Villains, 2025. URL https://arxiv.org/abs/2511.04962 .
- Qiying Yu, Zheng Zhang, Ruofei Zhu, Yufeng Yuan, Xiaochen Zuo, Yu Yue, Weinan Dai, Tiantian Fan, Gaohong Liu, Lingjun Liu, Xin Liu, Haibin Lin, Zhiqi Lin, Bole Ma, Guangming Sheng, Yuxuan Tong, Chi Zhang, Mofan Zhang, Wang Zhang, Hang Zhu, Jinhua Zhu, Jiaze Chen, Jiangjie Chen, Chengyi Wang, Hongli Yu, Yuxuan Song, Xiangpeng Wei, Hao Zhou, Jingjing Liu, Wei-Ying Ma, Ya-Qin Zhang, Lin Yan, Mu Qiao, Yonghui Wu, and Mingxuan Wang. DAPO: An Open-Source LLM Reinforcement Learning System at Scale, 2025. URL https://arxiv.org/abs/2503.14476 .
- Siyu Yuan, Kaitao Song, Jiangjie Chen, Xu Tan, Yongliang Shen, Ren Kan, Dongsheng Li, and Deqing Yang. EASYTOOL: Enhancing LLM-based Agents with Concise Tool Instruction, 2024. URL https://arxiv.org/abs/2401.06201 .
- Bang Zhang, Ruotian Ma, Qingxuan Jiang, Peisong Wang, Jiaqi Chen, Zheng Xie, Xingyu Chen, Yue Wang, Fanghua Ye, Jian Li, Yifan Yang, Zhaopeng Tu, and Xiaolong Li. Sentient agent as a judge: Evaluating higher-order social cognition in large language models, 2025a. URL https://arxiv.org/abs/2505.02847 .
- Jiajie Zhang, Nianyi Lin, Lei Hou, Ling Feng, and Juanzi Li. AdaptThink: Reasoning Models Can Learn When to Think, 2025b. URL https://arxiv.org/abs/2505.13417 .
- Zijing Zhang, Ziyang Chen, Mingxiao Li, Zhaopeng Tu, and Xiaolong Li. Rlvmr: Reinforcement learning with verifiable meta-reasoning rewards for robust long-horizon agents. arXiv preprint arXiv:2507.22844 , 2025c.
- Shuyan Zhou, Frank F. Xu, Hao Zhu, Xuhui Zhou, Robert Lo, Abishek Sridhar, Xianyi Cheng, Tianyue Ou, Yonatan Bisk, Daniel Fried, Uri Alon, and Graham Neubig. WebArena: A Realistic Web Environment for Building Autonomous Agents, 2024. URL https://arxiv.org/abs/2307. 13854 .

## A Notations

Table 3 summarizes the core notations used in this paper.

Table 3: Summary of Notations

| Symbol                                                | Task Formulation                                                                                                                                                                                                                                                                                                          |
|-------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
|                                                       | Meaning                                                                                                                                                                                                                                                                                                                   |
| M τ π θ R                                             | Partially Observable Markov Decision Process (POMDP) tuple Interaction trajectory containing instructions, observations, and actions The language agent policy parameterized by θ Reward function evaluating task success                                                                                                 |
| Symbol                                                | Cognitive Modeling                                                                                                                                                                                                                                                                                                        |
| L l t th t y t D cog                                  | Set of four cognitive levels {L 1 , L 2 , L 3 , L 4 } derived from ACT-R theory Selected cognitive level at time step t , where l t ∈ L Internal thinking process generated corresponding to l t Structured output at step t , defined as y t = [ l t , th t , a t ] Cognition-aware dataset used for the SFT stage       |
| Symbol                                                | CoPO Optimization Meaning                                                                                                                                                                                                                                                                                                 |
| A ( i ) e ( i ) t C ( i , k ) t g ( · ) A ( i , k ) t | Trajectory-level advantage calculated via standard GRPO Cognitive group consisting of counterfactual reasoning paths at step t Action prediction confidence under cognitive level k Temperature-scaled softmax function for weight normalization Confidence-aware step-level advantage for fine-grained credit assignment |

## B Task Details

Weevaluate our framework on two challenging agentic environments: ALFWorld and ScienceWorld . These benchmarks assess the agent's ability to perform long-horizon planning, reasoning, and environment interaction.

ALFWorld ALFWorld (Shridhar et al., 2020) is a household environment built on TextWorld, where agents must explore rooms and apply commonsense reasoning to complete tasks. The action space includes operations such as picking up and placing items, observing the environment, and interacting with furniture. The environment provides feedback based on a set of predefined logical rules. ALFWorld defines six types of tasks: Pick &amp; Place, Examine in Light, Clean &amp; Place, Heat &amp; Place, Cool &amp; Place, and Pick Two &amp; Place. We use the success rate as the evaluation metric. The maximum number of interaction steps is set to 30 for evaluation.

ScienceWorld ScienceWorld (Wang et al., 2022) is a benchmark environment designed to evaluate agents' scientific reasoning abilities, based on a standard elementary science curriculum. It comprises 30 task types, including using measurement instruments and conducting mechanics experiments. The action space is task-specific, and the simulator returns the effects of each action. Following prior work (Lin et al., 2023), we categorize tasks into three groups based on the average length of oracle trajectories: Short ( ∗ Len ≤ 20, avg. 11.76 steps), Medium (20 &lt; ∗ Len ≤ 50, avg. 28.58 steps), and Long ( ∗ Len &gt; 50, avg. 94.30 steps). These lengths reflect the oracle's optimal paths. We use task

score as the primary evaluation metric and report the success rate as a complementary measure. The maximum number of interaction steps is limited to 100 for evaluation.

## C Instruction Prompt Examples

The system prompts for two agentic environments are presented in Listing 1. The output format for our COGROUTER framework introduced in §3.2 is presented in Listing 2.

Listing 1: Prompt details for ScienceWorld and ALFWorld.

## ALFWorld Instruction:

```
Interact with a household to solve a task. Imagine you are an intelligent agent in a household environment and your target is to perform actions to complete the task goal. At the beginning of your interactions , you will be given the detailed description of the current environment and your goal to accomplish. For each of your turn , you will be given a list of actions which you can choose one to perform in this turn. After each turn , the environment will give you immediate feedback based on which you plan your next few steps. If the environment outputs 'Nothing happened ', that means the previous action is invalid and you should try more options. Reminder: the action must be chosen from the given available actions. Any actions except provided available actions will be regarded as illegal. Your current task is: {task_description} Prior to this step , you have already taken {step_count} step(s). Below are the most recent {history_length} observations and the corresponding actions you took: {action_history} You are now at step {current_step} and your current observation is: {current_observation} Your admissible actions of the current situation are: {admissible actions} Now it's your turn to generate next step response. ScienceWorld Instruction: You are an agent for science world. Every round I will give you an observation , you have to respond an action based on the observation to finish the given task. Here are the actions you may take: [ {{"action": "open OBJ", "description": "open a container"}}, {{"action": "close OBJ", "description": "close a container"}}, {{"action": "activate OBJ", "description": "activate a device"}}, {{"action": "deactivate OBJ", "description": "deactivate a device"}}, {{"action": "connect OBJ to OBJ", "description": "connect electrical components"}}, {{"action": "disconnect OBJ", "description": "disconnect electrical components"}}, {{"action": "use OBJ [on OBJ]", "description": "use a device/item"}}, {{"action": "look around", "description": "describe the current room"}}, {{"action": "look at OBJ", "description": "describe an object in detail"}}, {{"action": "look in OBJ", "description": "describe a container 's contents "}}, {{"action": "read OBJ", "description": "read a note or book"}}, {{"action": "move OBJ to OBJ", "description": "move an object to a container "}}, {{"action": "pick up OBJ", "description": "move an object to the inventory "}},
```

```
{{"action": "put down OBJ", "description": "drop an inventory item"}}, {{"action": "pour OBJ into OBJ", "description": "pour a liquid into a container"}}, {{"action": "dunk OBJ into OBJ", "description": "dunk a container into a liquid"}}, {{"action": "mix OBJ", "description": "chemically mix a container"}}, {{"action": "go to LOC", "description": "move to a new location"}}, {{"action": "eat OBJ", "description": "eat a food"}}, {{"action": "flush OBJ", "description": "flush a toilet"}}, {{"action": "focus on OBJ", "description": "signal intent on a task object "}}, {{"action": "wait", "description": "take no action for 10 iterations"}}, {{"action": "wait1", "description": "take no action for 1 iteration"}}, {{"action": "task", "description": "describe current task"}}, {{"action": "inventory", "description": "list your inventory"}} ] Your current task is: {task_description} Prior to this step , you have already taken {step_count} step(s). Below are the most recent {history_length} observations and the corresponding actions you took: {action_history} You are now at step {current_step} and your current observation is: {current_observation} Now it's your turn to generate next step response. Listing 2: The output format for CogRouter. There are four thinking levels: Level 1 -Instinctive Response: Immediate reaction based on intuition , no analysis. Level 2 -Situational Awareness: Assess current state and available actions before acting. Level 3 -Experience Integration: Reflect on past actions and outcomes to inform current decisions. Level 4 -Strategic Planning: Assess the task goal , past lessons , and current state to analyze the future impact of each candidate action and optimize the decision. You must first choose an appropriate level of thinking (one of the four levels) to respond based on the given scenario. The chosen level MUST be enclosed within <level ></level > tags. Next , reason step -by-step using the chosen thinking level. This reasoning process MUST be enclosed within <think ></think > tags. For Level 1 (Instinctive Response), use the fixed text: "Okay, I think I have finished thinking." For Levels 2-4, provide detailed reasoning as shown in examples. Once you've finished your reasoning , you should choose an admissible action for current step and present it within <action ></action > tags. [Output Format] Your output must adhere to the following format: EXAMPLE 1: <level >1</level > <think >Okay , I think I have finished thinking.</think> <action >your_next_action </action > EXAMPLE 2: <level >2</level > <think >
```

```
Current state: [Current state and inventory] Available actions: [What actions are valid right now] Reasoning: [Choose the best action and explain why] </think > <action >your_next_action </action > EXAMPLE 3: <level >3</level > <think > Goal: [What needs to be accomplished] Current state: [Current state and inventory] Available actions: [What actions are valid right now] Reflection: [How effective were recent actions , what was learned] Reasoning: [Choose the best action based on experience] </think > <action >your_next_action </action > EXAMPLE 4: <level >4</level > <think > Goal: [What needs to be accomplished] Current state: [Current state and inventory] Available actions: [What actions are valid right now] Reflection: [How effective were recent actions , what was learned] Evaluation: [Assess the potential effectiveness of each candidate action] Reasoning: [Choose the optimal action with strategic reasoning] </think > <action >your_next_action </action >
```

## D Implementation Details

## D.1 Training Data Collection

We construct training datasets for both COSFT and COPO using the training splits of ALFWorld (2,420 simulations) and ScienceWorld (2,120 simulations). For COSFT, we randomly sample 500 environments from each benchmark to create a dataset with uniform coverage across the four cognitive levels. For each sampled environment, we first collect expert trajectories using GPT-4o without reasoning augmentation, then prompt GPT-4o to generate structured thinking processes based on a randomly selected cognitive level. Prompt templates for different levels are shown in Listing 4. This procedure yields roughly 25% of data for each level, enabling the model to learn consistent cognitive patterns across all thinking modes. An example of the final data format is shown as follow.

Listing 3: Example of COSFT training data format showing the structured output with cognitive level, thinking process, and action.

```
CoSFT Example: {"conversations": [{"from": "human", "value": "You are an agent for science world. Every round I will give you an observation , you have to respond an action based on the observation to finish the given task. Here are the actions you may take: [valid actions omitted] There are four thinking levels: Level 1 -Instinctive Response: Immediate reaction based on intuition , no analysis. Level 2 -Situational Awareness: Assess current state and available actions before acting. Level 3 -Experience Integration: Reflect on past actions and outcomes to inform current decisions. Level 4 -Strategic Planning: Assess the task goal , past lessons , and current state to analyze the future impact of each candidate action and optimize the decision. At each step , you must first choose an appropriate level of thinking (one of the four levels) to respond based on the given scenario. The chosen level MUST be enclosed within <level > </level > tags. Next , reason step -by-step using the chosen thinking level. This reasoning process MUST be enclosed within <think > </think > tags. For Level 1 ( Instinctive Response), use the fixed text: "Okay, I think I have finished thinking." For Levels 2-4, provide detailed reasoning as shown in examples. Once you've finished your reasoning , you should choose an admissible action for current step and present it within <action > </action > tags. [Output format examples omitted] Your current task is: Your task is to boil water. For compounds without a boiling point , combusting the substance is also acceptable. First , focus on the substance. Then , take actions that will cause it to change its state of matter. Your current observation is: Your task is to boil water. For compounds without a boiling point , combusting the substance is also acceptable. First , focus on the substance. Then , take actions that will cause it to change its state of matter.This room is called the hallway. In it, you see: a picture , a substance called air, the agent. You also see: A door to the green house (that is open), A door to the living room (that is open), A door to the art studio (that is open), A door to the kitchen (that is open), A door to the bedroom (that is open), A door to the workshop (that is open). Now it's your turn to generate next step response."}, {"from": "gpt", "value": "<level >1</level ><think >Okay , I think I have finished thinking.</think><action >open door to kitchen </action >?} ], "system": ""}
```

For COPO, we use the remaining environments: 1,920 from ALFWorld and 1,620 from ScienceWorld. We adopt an online learning setup where the agent collects trajectories through environment interaction. During the cognitive group expansion process (see Section 3.4), we apply the same prompt templates (Listing 4) to generate thinking processes at all four levels while keeping the actions fixed.

```
Cognitive Level 2 Based on the following information , generate a thinking process that leads to the next step action. Task Description: {task_description} Observation: {current_obs} History of Previous Actions and Observations: {history} Next Action: {action} You need to generate a thinking process following the exact format: <think > Current state: [Analyze the current environment state] Available actions: [What actions are possible right now] Reasoning: [Choose the best action and explain why] </think > Cognitive Level 3 Based on the following information , generate a thinking process that leads to the next step action. Task Description: {task_description} Observation: {current_obs} History of Previous Actions and Observations: {history} Next Action: {action} You need to generate a thinking process following the exact format: <think > Goal: [What needs to be accomplished] Current state: [Analyze the current environment state] Available actions: [What actions are possible right now] Reflection: [How effective were recent actions , what was learned] Reasoning: [Choose the best action based on experience] </think > Cognitive Level 4 Based on the following information , generate a thinking process that leads to the next step action. Task Description: {task_description} Observation: {current_obs} History of Previous Actions and Observations: {history} Next Action: {action} You need to generate a thinking process following the exact format: <think > Goal: [What needs to be accomplished] Current state: [Analyze the current environment state] Available actions: [What actions are possible right now] Reflection: [How effective were recent actions , what was learned]
```

Listing 4: Prompt templates for generating thinking processes at different cognitive levels.

```
Evaluation: [Assess the potential effectiveness of each candidate action] Reasoning: [Choose the optimal action with strategic reasoning] </think >
```

## D.2 Training Details

Our COPO algorithm extends group-based RL frameworks by introducing cognitive group expansion , which enables step-wise credit assignment across different cognitive levels. Unlike GRPO, which applies trajectory-level updates, COPO generates alternate thinking processes for each decision point in successful trajectories (reward Ri &gt; 0), keeping the observation ot and action at fi xed. This allows the model to learn which cognitive level best suits each state-action pair, based on observed outcomes. To reduce computational cost, we skip cognitive group expansion when all trajectories in a group share the same outcome reward ( i . e ., all succeed or all fail). In such cases, the trajectory-level advantage is zero, so reweighting would yield no gradient signal regardless of the expansion.

Table 4 presents the training time over 150 iterations and the final performance of each online method ( i . e ., GRPO, GiGPO, and COPO). Compared to GRPO, COPO requires approximately 1.41.5 × training time due to the additional expansion of the cognitive group. However, this remains acceptable given the substantial performance gains, demonstrating a favorable trade-off between computational cost and task performance.

Table 4: Training time and performance comparison of different methods on ALFWorld and ScienceWorld.

| Method               | ALFWorld             | ALFWorld             | ScienceWorld         | ScienceWorld         |
|----------------------|----------------------|----------------------|----------------------|----------------------|
|                      | Training time        | Performance          | Training time        | Performance          |
| Qwen2.5-7B-Instruct  | Qwen2.5-7B-Instruct  | Qwen2.5-7B-Instruct  | Qwen2.5-7B-Instruct  | Qwen2.5-7B-Instruct  |
| GRPO                 | 38h 25m 11s          | 83.5                 | 61h 2m 16s           | 53.0                 |
| GiGPO                | 49h 33m 16s          | 88.0                 | 78h 21m 20s          | 47.0                 |
| CoPO                 | 54h 58m 27s          | 92.5                 | 86h 15m 50s          | 72.0                 |
| Llama3.1-8B-Instruct | Llama3.1-8B-Instruct | Llama3.1-8B-Instruct | Llama3.1-8B-Instruct | Llama3.1-8B-Instruct |
| GRPO                 | 36h 44m 38s          | 84.0                 | 51h 16m 3s           | 50.5                 |
| GiGPO                | 54h 15m 59s          | 89.5                 | 88h 14m 38s          | 64.0                 |
| CoPO                 | 48h 24m 30s          | 91.5                 | 81h 36m 45s          | 70.5                 |

## E Extended Results

## E.1 Training Dynamics of Cognitive Level Distribution

Figure 6 illustrates the evolution of cognitive level distribution during COPO training: the agent learns to allocate L 1 for routine steps while preserving higher-level reasoning ( L 2L 4 ) for situations requiring deeper deliberation.

Figure 6: Dynamics of cognitive level distribution during training.

<!-- image -->

## E.2 Additional Analysis on Cognitive Level Collapse

To further understand why baseline RL methods fail to maintain adaptive cognitive allocation, we compare the cognitive level distributions across trajectory progress for COPO, GiGPO, and GRPO on ScienceWorld, using Qwen2.5-7B as the base model. GRPO collapses to uniform L 4 thinking due to coarse trajectory-level credit assignment. Although GiGPO introduces step-level grouping, it similarly collapses, maintaining over 90% L 4 usage throughout the trajectory with minimal stagewise variation (Figure 7). This collapse arises from biased credit assignment: in GiGPO's state-based grouping, steps with deeper thinking often appear in successful trajectories and receive higher advantages, regardless of whether such depth was actually necessary. As a result, GiGPO defaults to deeper thinking as a universally safe choice, rather than allocating cognitive effort adaptively. In contrast, COPO employs confidence-aware reweighting to assess the appropriateness of each cognitive level at every step. It estimates how confidently the model predicts actions under different thinking modes, providing a fine-grained signal that guides more adaptive cognitive allocation.

Figure 7: Cognitive level distributions across trajectory progress for CoPO, GiGPO, and GRPO on ScienceWorld. Both GRPO and GiGPO collapse to predominantly L 4 thinking throughout trajectories, while COPO exhibits stage-aware adaptation.

<!-- image -->

## F Case Study

We analyze a representative trajectory from ScienceWorld's use thermometer task using Qwen2.5-7B as the base model (Figure 8). At initialization (steps 1), the agent employs L 4 (Strategic Planning) to formulate a global plan: locate the thermometer, identify the measurement target, and execute the measurement. During the execution phase (steps 4-12), the agent predominantly uses L 1 (Instinctive

<!-- image -->

Figure 8: Case study of COGROUTER's adaptive cognitive switching on ScienceWorld's use thermometer task. The agent adaptively switches cognitive levels based on situational demands: employing L 4 for initial strategic planning (step 0), L 1 for routine navigation and manipulation (steps 6-8), L 3 for obstacle reflection when encountering unexpected situations (step 9).

<!-- image -->

Response) for routine navigation and object manipulation. When encountering an unexpected observation at step 8 ('the door is locked'), the agent switches to L 3 (Experience Integration) to reflect on the obstacle and adjust the plan. This adaptive cognitive switching demonstrates that COGROUTER learns to allocate cognitive depth based on situational demands, achieving task success with 62% fewer tokens than GRPO's uniform L 4 thinking.

## G Baselines

## G.1 Prompt-based Methods

ReAct (Yao et al., 2023b) is a prompting framework that interleaves thinking processes with taskspecific actions. For both ALFWorld and ScienceWorld, we prompt the model to generate a thought before each action using the format: Thought: ..., Action: ... . This format is applied to both frontier models (GPT-4o, DeepSeek-V3, Claude-4-Sonnet, OpenAI-o3, DeepSeek-R1, Gemini-2.5-Pro) and fine-tuned open-source models (Qwen2.5-7B-Instruct, Llama3.1-8B-Instruct). Reflexion (Shinn et al., 2023) is a self-refinement framework that enables agents to learn from trial-and-error. After each failed episode, the agent generates a reflection summarizing the failure and proposing improvements, which is prepended to the context in subsequent trials for the same task. We allow a maximum of 3 trials per task.

## G.2 Training-based Methods

We consider both offline and online methods using Qwen2.5-7B and Llama3.1-8B as base models. Detailed training settings are provided in Table 5.

Offline Methods. For SFT , we construct a supervised dataset by randomly sampling 500 environments from the ALFWorld and ScienceWorld training sets, consistent with COSFT. For each environment, we collect expert trajectories and prompt GPT-4o to generate the corresponding reasoning steps in ReAct format, producing ReAct-style successful trajectories. These are used to fine-tune the base model via standard language modeling loss. ETO (Song et al., 2024) employs Direct Preference Optimization (DPO) (Rafailov et al., 2024), training on contrastive trajectory pairs. Preferred examples are taken from the SFT-generated successful trajectories, while rejected ones are sampled by rolling out the base model in the same environments.

Online Methods. All online RL methods are trained for 150 iterations on environments not used in SFT: 1920 in ALFWorld and 1620 in ScienceWorld. In each iteration, we randomly sample 128 environments to collect trajectories for policy updates. All models are initialized from the same COSFT cold-start checkpoint for fair comparison. GRPO (Shao et al., 2024) performs trajectorylevel policy updates by grouping trajectories into 8 clusters. GiGPO (Feng et al., 2025) extends GRPO with step-level credit assignment using anchor state grouping. We use the same group size (8) for computing trajectory-level advantages. For step-level rewards, we set the weighting coefficient ω = 1.0 and the discount factor γ = 0.95. AdaptThink (Zhang et al., 2025b) teaches models to adaptively select between thinking and non-thinking modes. We adapt it to our multi-step agentic scenarios. We first perform SFT cold start by sampling 500 environments and constructing a balanced 1:1 dataset of thinking and non-thinking steps, where non-thinking uses &lt;think&gt;&lt;/think&gt; (empty think tags) to skip thinking. We then train with GRPO using a modified reward r ( τ ) = 1 | τ | ∑ | τ | t = 1 1 ( NoThink t ) · δ + R ( τ ) , where 1 ( NoThink t ) indicates whether step t uses non-thinking mode ( i . e ., generates &lt;think&gt;&lt;/think&gt; ), and R ( τ ) is the task reward from §3.4. The first term represents the proportion of non-thinking steps in each trajectory. We set δ = 0.05 and use importance sampling during training to balance exploration of both modes, with all other settings following GRPO.

## H Hyperparameters

Full hyperparameter details are provided in Table 5. For offline methods, we use the LLaMA-Factory codebase. 4 For online methods, we implement our algorithms based on the VERL framework (Sheng et al., 2025).

Table 5: Hyperparameters for all experiments across different methods and benchmarks. All RL methods (GRPO, GiGPO, COPO) use the same hyperparameters for fair comparison.

|                        | Qwen2.5-7B   | Qwen2.5-7B   | Llama3.1-8B   | Llama3.1-8B   |
|------------------------|--------------|--------------|---------------|---------------|
|                        | ALFWorld     | ScienceWorld | ALFWorld      | ScienceWorld  |
| Learning Rate          | 2e-6         | 2e-6         | 2e-6          | 2e-6          |
| Batch Size             | 32           | 32           | 32            | 32            |
| Number of Epoch        | 3            | 3            | 3             | 3             |
| Max Sequence Length    | 8192         | 8192         | 8192          | 8192          |
| Learning Rate          | 2e-6         | 2e-6         | 2e-6          | 2e-6          |
| Batch Size             | 32           | 32           | 32            | 32            |
| Number of Epoch        | 3            | 3            | 3             | 3             |
| Max Sequence Length    | 8192         | 8192         | 8192          | 8192          |
| Learning Rate          | 2e-6         | 2e-6         | 2e-6          | 2e-6          |
| Batch Size             | 32           | 32           | 32            | 32            |
| Number of Epoch        | 3            | 3            | 3             | 3             |
| Max Sequence Length    | 8192         | 8192         | 8192          | 8192          |
| Max Prompt Length      | 18000        | 30000        | 18000         | 30000         |
| Max Response Length    | 1024         | 1024         | 1024          | 1024          |
| Group Size             | 8            | 8            | 8             | 8             |
| Groups per Rollout     | 16           | 16           | 16            | 16            |
| KL Coefficient         | 0.1          | 0.2          | 0.1           | 0.2           |
| Learning Rate          | 5e-7         | 5e-7         | 2e-7          | 2e-7          |
| Rollout Temperature    | 1.0          | 1.0          | 1.0           | 1.0           |
| Validation Temperature | 0.4          | 0.4          | 0.4           | 0.4           |
| Mini-batch Size        | 64           | 64           | 64            | 64            |
| Iteration              | 150          | 150          | 150           | 150           |
| Max Prompt Length      | 18000        | 30000        | 18000         | 30000         |
| Max Response Length    | 1024         | 1024         | 1024          | 1024          |
| Group Size             | 8            | 8            | 8             | 8             |
| Groups per Rollout     | 16           | 16           | 16            | 16            |
| KL Coefficient         | 0.1          | 0.2          | 0.1           | 0.2           |
| Learning Rate          | 5e-7         | 5e-7         | 2e-7          | 2e-7          |
| Rollout Temperature    | 1.0          | 1.0          | 1.0           | 1.0           |
| Validation Temperature | 0.4          | 0.4          | 0.4           | 0.4           |
| Mini-batch Size        | 64           | 64           | 64            | 64            |
| Iteration              | 150          | 150          | 150           | 150           |
| Max Prompt Length      | 18000        | 30000        | 18000         | 30000         |
| Max Response Length    | 1024         | 1024         | 1024          | 1024          |
| Group Size             | 8            | 8            | 8             | 8             |
| Groups per Rollout     | 16           | 16           | 16            | 16            |
| KL Coefficient         | 0.1          | 0.2          | 0.1           | 0.2           |
| Learning Rate          | 5e-7         | 5e-7         | 2e-7          | 2e-7          |
| Rollout Temperature    | 1.0          | 1.0          | 1.0           | 1.0           |
| Validation Temperature | 0.4          | 0.4          | 0.4           | 0.4           |
| Mini-batch Size        | 64           | 64           | 64            | 64            |
| Iteration              | 150          | 150          | 150           | 150           |

## I Necessity of Adaptive Thinking

To evaluate the importance of adaptive thinking, we compare COGROUTER against two types of baselines on ALFWorld using Qwen2.5-7B: 1) Fixed cognitive levels , where separate models are trained via SFT using a single reasoning format ( L 1 , L 2, L 3, or L 4 ), followed by GRPO; and 2) FreeForm , trained with standard SFT using an unstructured reasoning format, also followed by GRPO. As shown in Figure 9, COGROUTER achieves a 92.5% SR with only 1739.42 tokens, outperforming all baselines in both accuracy and efficiency. The fixed-level models exhibit a clear trade-off: L 1 achieves 76.5% SR with 357.04 tokens, while L 4 reaches 86.5% SR at the cost of 4640.98 tokens, still 6% below COGROUTER despite using 2.7 × more tokens. FreeForm attains 81.5% SR with 4068.88 tokens, suggesting it applies deep reasoning uniformly, without adjusting to task complexity. These results indicate that neither shallow nor deep reasoning alone is sufficient: shallow approaches underperform on complex steps, while deep ones waste computation on simple cases. Adaptive thinking addresses this by dynamically adjusting reasoning depth to task demands.

Figure 9: Comparison of cognitive configurations on ALFWorld (Qwen2.5-7B). COGROUTER outperforms both fixed cognitive levels and unstructured reasoning, highlighting the need for adaptive thinking.

<!-- image -->