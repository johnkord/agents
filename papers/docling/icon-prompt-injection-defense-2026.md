## ICON: Indirect Prompt Injection Defense for Agents based on Inference-Time Correction

Che Wang 1 2 Fuyao Zhang 2 Jiaming Zhang 2 ziqi Zhang 1 Yinghui Wang 3 Longtao Huang 4 Jianbo Gao 1 Zhong Chen 1 Wei Yang Bryan Lim 2

## Abstract

Large Language Model (LLM) agents are susceptible to Indirect Prompt Injection (IPI) attacks, where malicious instructions in retrieved content hijack the agent's execution. Existing defenses typically rely on strict filtering or refusal mechanisms, which suffer from a critical limitation: over-refusal, prematurely terminating valid agentic workflows. We propose ICON , a probingto-mitigation framework that neutralizes attacks while preserving task continuity. Our key insight is that IPI attacks leave distinct over-focusing signatures in the latent space. We introduce a Latent Space Trace Prober to detect attacks based on high intensity scores. Subsequently, a Mitigating Rectifier performs surgical attention steering that selectively manipulate adversarial query key dependencies while amplifying task relevant elements to restore the LLM's functional trajectory. Extensive evaluations on multiple backbones show that ICON achieves a competitive 0.4% ASR, matching commercial grade detectors, while yielding a over 50% task utility gain. Furthermore, ICON demonstrates robust Out of Distribution (OOD) generalization and extends effectively to multi-modal agents, establishing a superior balance between security and efficiency.

## 1. Introduction

Recent advancements in Large Language Model (LLMs) Agents have revolutionized autonomous task execution, enabling sophisticated reasoning, planning, and seamless tool orchestration (Anthropic, 2024; OpenAI &amp; Josh Achiam, 2024). By integrating external data sources and diverse APIs, these agents can act upon complex user intentions

1 School of Computer Science, Peking University, China 2 College of Computing and Data Science, Nanyang Technological University, Singapore 3 Ant Group 4 Alibaba. Correspondence to: Che Wang &lt; chewang@stu.pku.edu.cn &gt; .

Figure 1. Our defense, ICON , presents a better security-utility trade-off (lower attack success rate, meanwhile higher utility) than other defenses against adaptive indirect prompt injection attacks.

<!-- image -->

within dynamic environments (e.g., Cursor (Anysphere, 2024)). However, this increased agency introduces a critical security frontier: Indirect Prompt Injection (IPI) Attacks. Unlike direct jailbreaks, IPIs are embedded within retrieved content (e.g., emails, web pages). Because LLMs fundamentally conflate instructions with data, these malicious payloads can easily hijack the agent's decision-making process, forcing it to execute unauthorized actions (tool calling) while bypassing traditional safety guardrails.

Furthermore, recent studies (Zhan et al., 2024; Liu et al., 2025; Zhan et al., 2025a) have highlighted the emergence of adaptive IPI attacks, which are increasingly indistinguishable from benign task contexts. By strategically exploiting the subtle semantic boundaries between instructions and data, these adversaries craft malicious payloads that bypass traditional guardrails.

Existing solutions can not effectively defend against adaptive IPI attacks, as shown in Tab. 1. Heuristic defenses include input templating (Willison, 2023) and post-hoc tool filtering (Zhu et al., 2025). Their limitation is that they rely on surface-level patterns that are easily circumvented by diverse adversarial variants. While safety fine-tuning (Chen et al., 2025) offers stronger security, it tends to oversensitize the LLM and to excessive over-refusal, prematurely terminating complex agentic loops. Moreover, the high computational overhead of retraining makes fine-tuning imprac-

Table 1. Defense Comparison against Adaptive IPI Attacks.

| Functionality   | Template   | Tool-Filter   | Fine-tuning   | ICON   |
|-----------------|------------|---------------|---------------|--------|
| Security        | ✗          | ✗             | ✓             | ✓      |
| Utility         | ✗          | ✗             | ✗             | ✓      |
| Efficiency      | ✓          | ✓             | ✗             | ✓      |

tical for agile deployment in dynamic environments. These defenses tend to sacrifice task utility for higher security. Consequently, there is a pressing need for a plug-and-play defense mechanism that can surgically neutralize adaptive attacks without compromising the agent's functional continuity.

To bridge these gaps, we propose ICON (Inference-Time Correction), a novel framework to detect and rectify compromised trajectories against adaptive IPI. Our approach is grounded in a key empirical insight: Successful IPIs that trigger a specific adversarial intent tend to induce abnormally concentrated attention on a small subset of injected tokens, compared to benign trajectories that require integrating long-horizon context. We first introduce Focus Intensity Score (FIS) to quantify attention anomalies across critical layers and heads. Next, we develop the Latent Space Trace Prober (LSTP) and Mitigating Rectifier (MR). LSTP serves as a lightweight detector for high-intensity attention signatures. Upon detection, MR performs surgical attention steering rather than aborting the task. It selectively suppresses adversarial dependencies while reinforcing taskrelevant features, effectively stabilizing the execution flow and restoring the agent's original intent.

As shown in Fig. 1, experiments demonstrate that ICON achieves superior ASR reduction and UA enhancement on out-of-distribution (OOD) benchmarks. For instance, although trained on TrojanTools (Anonymous, 2025), ICON maintains lower ASR on AgentDojo (Debenedetti et al., 2024) than fine-tuned models (e.g., Qwen3Guard) and yields a 69% average Utility improvement . Furthermore, our method remains effective in multimodal LLMs,achieving 42% average Utility recovery . Meanwhile, ICON exhibits exceptional efficiency, requiring only hundreds of samples and one minute to converge. Thus, it offers a superior tradeoff between security, utility, and computational cost compared to existing defenses. Our contributions are summarized as follows:

- Mechanism Insight: We uncover that indirect prompt injections manifest as intrinsic 'attention collapse' patterns, allowing for detection based on latent dynamics rather than surface-level semantics.
- Trajectory rectification: We introduce a mitigating rectifier that performs targeted attention intervention on detected heads to restore benign tool-use behavior and preserve multi-step utility (achieving a &gt; 50% utility gain).
- Efficiency and generality: ICON trains in minutes on a

small synthesized set and transfers across agents/models, including multimodal agents, achieving a strong securityutility trade-off under adaptive attacks.

## 2. Related Works

## 2.1. Prompt Injection Attacks &amp; Defense

Prompt injection (PI) attacks represents a critical vulnerability for agents. Direct prompt injections mean users attempt to bypass internal safety guardrails. Differently, indirect prompt injections (IPI) leverage untrusted third-party data to attack. Such third-party data makes attacks much harder to detect, since adversarial instructions often appear benign within the retrieved context. IPI attacks typically aim to hijack an agent's tool use capabilities by embedding malicious payloads in external sources (Wu et al.; Greshake et al., 2023; Zhan et al., 2025b). In this work, we focus on IPI attacks, which pose a greater threat to real-world agent scenarios nowadays. Recent research has demonstrated how template-based prompts can mislead LLMs into executing adversaries' instructions (Willison, 2022; Perez et al., 2024; Zhan et al., 2024). Furthermore, studies also explored optimization-based techniques, such as using powerful LLMs or gradients to generate universal attack prompts (Liu et al., 2025; Anonymous, 2025; Zhang et al., 2024). Besides, adaptive attacks have proven considerably more potent, as they can dynamically bypass static guardrails that are otherwise effective against vanilla ones.

Prompt Injection Defense. Recently, researchers have proposed various strategies to mitigate IPI attacks. Early efforts focused on prompt-level mitigation strategies (Willison, 2022; San, 2023), which typically employ structural constraints, such as using delimiters to isolate instructions from untrusted content or reiterating system instructions within each dialogue turn to reinforce the model's adherence to the original task. Another line of research explores external detectors to intercept malicious inputs at the input stage, including Deberta detectors (ProtectAI.com, 2023) and training-free heuristics such as MELON (Zhu et al., 2025) and perplexity-based detection (Jain et al., 2023), which are post-filter methods. However, these static approaches often struggle with the utility-security trade-off, as they may degrade model performance or introduce significant latency. Furthermore, while industry-leading models (e.g., Llama-Guard (Llama Team, 2024) and Qwen-Guard (Zhao et al., 2025)) utilize fine-tuned safety guardrails, such solutions require large-scale and high-quality safety datasets and are often computationally prohibitive for many agentic applications. In contrast, ICON achieves a superior balance between security, efficiency, and utility.

## 2.2. Model Intervention Techniques

Prior studies in model interpretability have demonstrated that internal components, particularly attention heads and layers, exhibit heterogeneous importance across diverse tasks (Zhang et al.; Todd et al.). For instance, induction heads (Crosbie &amp; Shutova, 2025) specialize in in-context learning by capturing recurring patterns within input data, whereas successor heads (Gould et al.) manage the incrementing of tokens in natural sequences. Among various interpretability driven methods (Chuang et al., 2024), Latent Space Manipulation (LSM) has emerged as a flexible, model-agnostic approach. Rather than modifying model parameters or architecture, LSM operates directly on internal representations such as hidden states or attention weights at inference time.

Model Probing is often referred to as diagnostic classification or internal state analysis (Hung et al., 2025; Xia et al., 2025). This approach is grounded in the discovery that specific linguistic or logical tasks consistently manifest as distinct activation patterns within a model's high dimensional latent space. Afterwards, Latent Space Manipulation (LSM) can operates directly on internal representations such as hidden states or attention weights at inference time.

Model Steering encompasses controllable text generation techniques (Liang et al., 2024) that guide LLMs to produce outputs with desired attributes, such as safety properties. A prominent example is LM-Steer (Han et al., 2024), which introduces a simple yet effective mechanism to steer generation by linearly transforming the output embeddings of a frozen language model. Let c ∈ R d denote the context vector at a given decoding step. In standard decoding, the probability of generating token v is calculated as:

<!-- formula-not-decoded -->

where V denotes the vocabulary set and e v ∈ R d represents the output embedding of v . To introduce control, LM-Steer modifies each output state using:

<!-- formula-not-decoded -->

where W ∈ R d × d is a steering matrix, I is the identity matrix, and ϵ is a scalar used to adjust the steering intensity. By modifying the geometry of the output space in a controlled and interpretable manner, LM-Steer enables efficient adjustment of model behavior without additional training.

## 3. Problem Formulation &amp; Threat Model

Tool-Augmented LLM Agent. We adopt the formalization of tool-augmented agent M from prior research (Yao et al., 2022; Zhu et al., 2025). An agent is defined as a system comprising an LLM and a set of external tools, F = { f 1 , . . . , f n } , for interacting with an environment. Given a user instruction I u , the agent executes an iterative reasoning &amp; acting process. At time step t , the agent maintains a trajectory state

<!-- formula-not-decoded -->

where A 1: t -1 and O 1: t -1 denote the sequences of past actions and observations, respectively. Specifically, an action A i consists of tool calls derived from F , while an observation O i captures execution outputs or retrieved content.

Attacker's Capability. We assume a grey-box adversary with no access to the target LLM's internal parameters or architectural details, but may infer agent context via intermediate servers. This assumption is consistent with prior work (Zhu et al., 2025; Anonymous, 2025). Specifically, the attacker has three constraints: external injection, context awareness, and malicious redirection. External injection means the adversary can only inject a payload δ into the observation stream O t via external sources (e.g., compromised MCP servers (Ray, 2025) or retrieval corpora). Context awareness means the adversary leverages partial task knowledge (e.g., Shopping, Payment) to ensure δ is semantically consistent with benign content, bypassing surface-level filters. Malicious redirection means the attacker's goal is to steer the agent's policy toward an unauthorized tool call f adv ∈ F while maintaining a stealthy latent trace.

Defender's Capability. We define the defender as the system operator with white-box access to the local LLMs. The defender can inspect and intervene on model internals (e.g., hidden states, attention weights). Defender's goal is twofold: 1) accurately detect IPI signatures within the compromised state S t ⊕ δ , and 2) actively mitigate their adversarial impact during inference. Specifically, we focus on scrutinizing the dynamics of the latent space. The defender targets the internal representations { h ( l ) } L l =1 during the critical transition from processing a compromised observation O 1: t -1 to generating the subsequent action A t .

Dual Optimization Objective. Drawing inspiration from adversarial training (Zhang et al., 2025b;a), we formalize the defense against IPI attacks as a decoupled optimization problem consisting of two objectives: the attacker's goal and defender's goal.

Attacker's Goal. To generate the most challenging samples for the defender and maximize stealthiness, the adversary seeks a context-aware instruction δ that minimizes the discrepancy between the compromised and benign reasoning flows. The attack should also successfully trigger the malicious intent f adv . The objective is defined as:

<!-- formula-not-decoded -->

where S t is the benign trajectory, ⊕ denotes the injection

operation, and D is a discrepancy function (i.e., LLM-asOptimizer) analyze and optimize the stealthiness of δ .

Defender's Goal. Given the adaptive trajectory from the attacker, the defender aims to jointly minimize the detection error and maximize the task utility. We formalize this as a dual-objective optimization of the prober parameters ϕ and the steering hyperparameter γ and τ of the rectification operator R :

<!-- formula-not-decoded -->

where L is the detection loss of the prober against ground truth y . The second term maximizes the log-probability of the correct task output y under the rectification operator R . Here, R ( · ; γ ; τ ) denotes the inference-time attention steering controlled by γ and τ , which restores the agent's focus without updating model weights.

## 4. Methodology

## 4.1. Motivation

ICON is inspired by the contrast between an agent's holistic context integration and the localized focus inherent in IPI attacks. While benign reasoning relies on long-horizon dependencies within the trajectory S t for planning consistency, a successful injection necessitates a 'forced focus' on the adversarial segment to trigger specific target tools f adv . This structural anomaly in the attention mechanism makes the model prioritizes the malicious payload over global task logic, thus creates a distinct latent trace that allows ICON to differentiate genuine reasoning from adversarial redirection.

## 4.2. Overview

The design of ICON is driven by two critical challenges in safeguarding LLM agents. First, adversarial diversity: current heuristic-based defenses (e.g., tool filtering (Zhu et al., 2025) or static prompts (Willison, 2023)) are easily bypassed by adaptive attacks that seamlessly blend malicious intent into the agent's context. Second, execution continuity: conventional safety mechanisms predominantly focus on binary refusal, which prematurely terminates the agent's multi-step reasoning loop and severely degrades task utility.

To address these, we propose a pipeline that first generates adaptive attacks (Sec. 4.3) to delineate and expand the agent's safety boundaries against benign contexts (Offline) for training defense modules. Then, it is followed by an online framework for real-time detection and utility recovery (Sec. 4.4). The defense mechanism, illustrated in Figure 2, follows a two-step paradigm to ensure both Robust Security and Task Utility . The Latent Space Trace Prober (LSTP) serves as a real-time gatekeeper, scrutinizing the agent's latent space for anomalous signals. Upon detection, the Mitigating Rectifier (MR) adaptively steers attention weights. By selectively suppressing adversarial query-key dependencies and redistributing focus toward the benign task context, the MR restores functional integrity, ensuring the agent completes its trajectory without execution loop termination.

## 4.3. Adaptive Attack Data Synthesis

To provide high-quality training data for the prober and calibrate the rectifier, we develop an offline data synthesis module. The primary goal of this module is to generate boundary-case IPI samples that satisfy the optimization objective in Eq.4. Unlike static datasets, our approach simulates a context-aware adversary to ensure the synthesized attacks are semantically seamless and latent-stealthy. We employ an LLM-as-Optimizer framework to iteratively solve the attacker's objective.

This process focuses on two dimensions of the attack space: (a). Contextual Tool Alignment : Instead of random injection, the optimizer selects the most plausible malicious tool f adv ∈ F that aligns with the current trajectory S t . This ensures that the final attack samples do not trigger surface-level semantic filters due to logical inconsistency. (b). Iterative Stealthiness Optimization : Starting from a random initialization, the optimizer refines the instruction δ to minimize the discrepancy D . This is achieved through a closed-loop feedback mechanism: the optimizer analyzes the agent's prior failure or success attempts and adaptively updates the payload towards the benign samples in the latent space while still fulfilling the success constraint M ( S t ⊕ δ ) = f adv .

This synthesis process is not intended to study the attack itself, but rather to delineate the adaptive-benign boundary. By generating a diverse set of stealthy instructions across various agentic tasks, we construct a robust training distribution. This allows the subsequent LSTP and MR to learn the fundamental over-focusing signal of IPI attacks rather than over-fitting to specific keyword patterns.

## 4.4. Probing and Mitigating Techniques

In this section, we introduce three synergistic components: the Focus Intensity Score (FIS), the Latent Space Trace Prober (LSTP), and the Mitigating Rectifier (MR). As the diagnostic foundation, the FIS quantifies attention entropy to identify anomalous focusing patterns, providing the critical head-selection signals required by both the LSTP for precise detection and the MR for surgical intervention. By integrating these mechanisms, ICON effectively transitions from identifying latent adversarial traces to performing realtime attention steering, ensuring robust security without compromising the agent's functional utility.

Figure 2. The overall architecture of ICON . The framework comprises two core modules: (1) The Latent Space Trace Prober for identifying intrinsic anomalies in the model's internal representations, and (2) The Mitigating Rectifier for neutralizing threats and restoring functional integrity. Together, these mechanisms enable robust detection and effective rectification of indirect prompt injections.

<!-- image -->

## 4.5. Focus Intensity Score (FIS)

To quantify the specialization of attention heads during the generation process, we introduce the Focus Intensity Score. Our key insight is that attention heads with lower entropy tend to capture specific, task-critical semantic patterns, which are often exploited or disrupted during IPI attacks.

Unlike previous methods that only consider the last token of input or start token of generation (Hung et al., 2025), we analyze the entire generated sequence Y to evaluate its global dependency on the context S t . Let L and H denote the total number of layers and the number of heads per layer, respectively. For a generated sequence of length N Y , the attention weight matrix of the h -th head in layer l is denoted as A l,h ∈ R N Y × N , where N is the sequence length of S t (current input). Each element a i,j represents the attention weight from the i -th generated token (query) to the j -th context token (key).

We first compute the generation-normalized token entropy for each query i to align the focus intensity with the generation scale:

<!-- formula-not-decoded -->

where ϵ is a small constant for numerical stability. Denote FIS for a specific head as S l,h . FIS can be defined as the complement of the average entropy across the entire generated sequence. And the aggregate score for layer l is the mean FIS of its constituent heads:

<!-- formula-not-decoded -->

## 4.6. Latent Space Trace Prober (LSTP)

The Latent Space Trace Prober (LSTP) is a lightweight detector for real-time identification of IPI attacks during inference. To ensure robust Out-of-Distribution (OOD) generalization across diverse scenarios, the prober should capture critical adversarial patterns in the latent space rather than memorize specific parameter values. To distill consistent signatures from raw attention weights, we develop a multistage transformation that maps variable-length sequences into a fixed-dimensional feature space. This process effectively isolates the 'attention sink' dynamics characteristic of autoregressive transformers, allowing the prober to detect subtle deviations induced by adversarial instructions.

Temporal Feature Aggregation. As the number of generated tokens N Y varies per sample, we apply a temporal aggregation function G to compress the entropy sequence { E l,h,i } N Y i =1 into a fixed-length descriptor. We select three significant features to characterize the focus dynamics of each head h :

- Minimum Entropy: Identifies the most extreme collapse of attention, often signifying an intensive response to an adversarial trigger.
- Mean Entropy: Reflects the overall concentration of the head throughout the generation process.

- Standard Deviation: Measures the volatility and stability of the attention focus across time steps.

Consequently, the feature vector for head h in layer l is defined as v l,h ∈ R 3 :

<!-- formula-not-decoded -->

Feature Fusion and Prober Architecture. To construct a comprehensive representation, we select a subset of K attack sensitive layers L ∗ ⊆ { 1 , . . . , L } , based on FIS of each layer. The global feature vector z is formed by concatenating head-wise features across all selected layers:

<!-- formula-not-decoded -->

For example, if a model with H = 32 heads and K = 4 layers, this yields a representation z ∈ R 3 × 32 × 4 = R 384 .

To capture local dependencies and sequential anomalies in attention shifts, we employ a hybrid architecture comprising a 1D-Convolutional Neural Network (CNN) and a MultiLayer Perceptron (MLP). The CNN layers facilitate the extraction of inter-head and inter-layer correlations. Furthermore, to ensure invariance to response duration, we utilize an Adaptive Global Maximum Pooling (GMP) layer. By extracting the most salient activations across the generation timeline, the prober can effectively identify both short, aggressive injections and long, subtle adversarial redirections:

<!-- formula-not-decoded -->

where H ( final ) denotes the high-level feature maps before the output layer. This architectural design ensures that the prober maintains high computational efficiency while preserving superior sensitivity to intrinsic latent traces, thereby facilitating robust generalization even to OOD datasets.

## 4.7. Mitigating Rectifier (MR)

Prior studies (Arditi et al., 2024; Wu et al., 2025) primarily rely on refusal vectors to thwart attacks. However, such interventions often induce prematurely termination of the execution loop, a fatal disruption for multi-step autonomous agents. To address this, this module performs a surgical latent rectification rather than a binary rejection.

Upon detection, the MR executes attention functionality restoration. By suppressing anomalous query-key dependencies while implicitly amplifying task-relevant context, it steers the model's latent representation back toward the benign functional manifold. This ensures the agent neutralizes malicious instructions without compromising the semantic continuity of its original plan.

Dual-Factor Attention Intervention. To ensure precision, we introduces a dual-hyperparameter mechanism that decouples the scope ( τ ) and intensity ( γ ) of the intervention:

- Steering Scope ( τ ): For each identified anomalous head h ∈ H adv (based on FIS), we define a suppression threshold θ l,h governed by the topk ratio τ . Specifically, θ l,h is determined as the (1 -τ ) -th percentile of the attention weights in head h , defining the saliency budget for intervention. The steering mask M l,h is formally defined as:

<!-- formula-not-decoded -->

where a l,h ( i, j ) is the raw attention weight and I ( · ) denotes the indicator function.

- Steering Intensity ( γ ): Weapply a Contrastive Steering Operation to the attention matrix using the coefficient γ :

<!-- formula-not-decoded -->

In this formulation, γ &lt; 1 functions as an intensive suppression coefficient. This mechanism is conducted before multihead attention softmax mechanism that exploits the Softmax redistribution effect: by diminishing anomalous attention peaks, the subsequent normalization naturally reallocates focus toward the benign task context (where M l,h = 0 ). This selective re-weighting effectively performs latent-level signal-to-noise enhancement, restoring the agent's functionality without damaging the global semantic structure.

## 5. Experiments

## 5.1. Experimental Setup

We evaluate the effectiveness of ICON by integrating it into the ReAct (Yao et al., 2022) framework, with a specific focus on the mitigation of IPI attacks. Our experiments are conducted across three representative foundation LLMs: QWEN (Yang et al., 2025), LLAMA (Touvron et al., 2023), and MISTRAL (Ministral, 2025). For Visual Large Models, we utilize QWEN-VL, INTERNVL (Chen et al., 2024), and MINICPM (Yu et al., 2025). These models were selected for their diverse architectures, ranging from reasoning-optimized to general-purpose designs, and their robust native support for tool-calling mechanisms.

Datasets. We utilize two widely used datasets, InjectAgent (Zhan et al., 2024) and AgentDojo (Debenedetti et al., 2024) to evaluate the effectiveness of ICON . For training, we adopt TrojanTools (Anonymous, 2025), which provides diverse tool selections in order to demonstrate the out-ofdistribution capability of our ICON . For visual tasks, we

Table 2. Evaluation of ICON against IPI attacks on various agentic LLMs.

| FMs   | BU   | Attack             | No Defense   | No Defense   | Repeat Prompt   | Repeat Prompt   | Delimiting   | Delimiting   | MELON   | MELON   | LLM-Detector   | LLM-Detector   | ICON (Ours)   | ICON (Ours)   |
|-------|------|--------------------|--------------|--------------|-----------------|-----------------|--------------|--------------|---------|---------|----------------|----------------|---------------|---------------|
| FMs   | BU   | Attack             | ASR ↓        | UA ↑         | ASR ↓           | UA ↑            | ASR ↓        | UA ↑         | ASR ↓   | UA ↑    | ASR ↓          | UA ↑           | ASR ↓         | UA ↑          |
|       |      | Ignore Instruction | 25.0         | 49.7         | 11.7            | 54.2            | 15.3         | 52.9         | 1.3     | 48.7    | 0.0            | 47.2           | 0.0           | 58.2          |
|       |      | Combined Attacks   | 30.3         | 44.2         | 13.2            | 53.1            | 18.4         | 50.2         | 2.4     | 45.5    | 0.0            | 43.8           | 0.0           | 52.7          |
|       |      | TrojanTools        | 42.7         | 35.3         | 25.4            | 45.6            | 31.8         | 43.7         | 5.8     | 36.8    | 0.6            | 38.6           | 1.2           | 50.5          |
|       |      | Avg                | 32.7         | 43.1         | 16.8            | 51.0            | 21.8         | 48.9         | 3.2     | 43.7    | 0.2            | 43.2           | 0.4           | 53.8          |
|       |      | Ignore Instruction | 23.4         | 25.7         | 14.6            | 30.1            | 16.8         | 28.1         | 0.6     | 26.1    | 0.0            | 25.2           | 0.0           | 32.6          |
|       |      | Combined Attacks   | 36.7         | 22.2         | 20.3            | 28.6            | 22.4         | 25.8         | 0.8     | 21.9    | 0.0            | 22.5           | 0.0           | 29.3          |
|       |      | TrojanTools        | 47.1         | 14.3         | 28.9            | 25.5            | 30.5         | 24.6         | 3.5     | 14.8    | 0.0            | 15.1           | 0.8           | 27.1          |
|       |      | Avg                | 35.7         | 20.7         | 21.3            | 28.1            | 23.2         | 26.2         | 1.6     | 20.9    | 0.0            | 20.9           | 0.3           | 29.7          |
|       |      | Ignore Instruction | 16.3         | 12.5         | 10.3            | 17.8            | 12.1         | 16.5         | 0.2     | 12.1    | 0.0            | 11.4           | 0.0           | 17.6          |
|       |      | Combined Attacks   | 33.6         | 11.1         | 19.8            | 15.6            | 23.6         | 14.1         | 0.5     | 11.5    | 0.0            | 10.9           | 0.0           | 16.1          |
|       |      | TrojanTools        | 42.9         | 8.5          | 23.2            | 12.3            | 27.9         | 10.2         | 2.6     | 9.3     | 0.0            | 9.7            | 0.9           | 13.9          |
|       |      | Avg                | 30.9         | 10.7         | 17.8            | 15.2            | 21.2         | 13.6         | 1.1     | 11.0    | 0.0            | 10.7           | 0.3           | 15.9          |

Notes: ASR = Attack Success Rate (%, lower is better for defender). UA = Utility under Attack (%, higher is better for defender). LLM-Detector = Qwen3Guard. Bold indicates best result. Bold indicates second-best UA. ICON maximizes UA with negligible impact on ASR.

Table 3. OOD Performance of ICON trained on TrojanTools.

| Model        | Metric   | InjectAgent   | InjectAgent       | AgentDojo   | AgentDojo         |
|--------------|----------|---------------|-------------------|-------------|-------------------|
| Model        | Metric   | No Def.       | ICON (Ours)       | No Def.     | ICON (Ours)       |
| QWEN-3-8B    | ADR      | 0%            | 80.1% ± 1.9 ± 2.3 | 0%          | 98.0% ± 1.2 ± 2.7 |
|              | URR      | 0%            | 62.3%             | 0%          | 69.6%             |
| LLAMA-3.1-8B | ADR      | 0%            | 85.4% ± 1.3       | 0%          | 97.3% ± 1.8       |
| LLAMA-3.1-8B | URR      | 0%            | 47.6% ± 3.1       | 0%          | 55.9% ± 3.3       |

Notes: ADR = Attack Detection Rate. URR = Utility Recover Rate. Values are reported with standard deviations computed over 5 cross validation.

utilize Visual Prompt Injection Benchmarks (Wan et al., 2024) provided by Meta.

Baselines. Following recent literature (Zhu et al., 2025; Bhagwatkar et al.), we benchmark ICON against three defense mechanisms: template-based, filter-based, and finetuning-based. Template-based defenses include Repeat Prompt (Debenedetti et al., 2024), which reiterates system instructions, and Delimiting (Willison, 2023), which uses special tokens to isolate untrusted content. For filteringbased defense, we use MELON (Zhu et al., 2025), a stateof-the-art tool-filtering framework. For fine-tuning-based defense, we utilize Qwen3Guard (Zhao et al., 2025) and Gemini (Team et al., 2023), the most advanced (M)LLM currently available, to establish a performance upper bound for intrinsic model robustness.

## 5.2. Main Results and Analysis

Table 2 summarizes the performance of ICON across three backbone LLMs under various IPI attacks. The results demonstrate that ICON not only provides robust security but also significantly excels in maintaining task utility, out-

Table 4. Efficiency comparison of different methods.

| Method        | Training Cost    | Dataset Size   | Parameters   |
|---------------|------------------|----------------|--------------|
| Qwen3Guard    | Over 10 hours    | 1.19M          | 8B           |
| MELON         | 0                | 0              | 0            |
| Repeat Prompt | 0                | 0              | 0            |
| ICON (Ours)   | Less than 2 mins | 255            | 31553        |

performing all existing baselines.

Competitive Security. In terms of security, ICON achieves an average ASR of 0.4%, which is comparable to the performance of the LLM-Detector (0.2% average ASR). While the latter serves as a strong upper-bound baseline, ICON attains a similar level of protection through a much more lightweight probing mechanism. This demonstrates that capturing latent traces is a highly effective alternative to using a dedicated external guardrail model for identifying sophisticated attacks.

Preservation of Task Utility. The most distinct advantage of ICON is its ability to maintain agent utility under attack. While fine-tuning based defenses (LLM Detectors) often secure the system by rejecting inputs, which inevitably terminates the task. But our Mitigating Rectifier recovers a substantial portion of the model's functional capability. Specifically, ICON provides an average 10% improvement in UA compared to other defense baselines. In the QWEN-3 experiments, for instance, our method maintains a UA of 53.8%, whereas the next best defense drops below 49%.

Baselines Comparison. Compared to the tool-filtering method MELON , which acts as a post-filter for action selection, ICON reduces the ASR by an additional 3% while

providing a 10% UA boost. Since MELON cannot re-run the reasoning process after a filter trigger, it inevitably loses utility. Furthermore, while template-based methods ( Repeat Prompt and Delimiting ) show some effectiveness against traditional Ignore Instruction attacks, they struggle significantly with adaptive TrojanTools . Interestingly, we observe that template methods slightly improve UA on weaker backbones like LLAMA and MISTRAL, likely because structured prompts help these models better parse user intent despite their limited native reasoning capabilities.

## 5.2.1. EFFICIENCY ANALYSIS.

We further evaluate the computational overhead of ICON compared to representative baselines in Table 4. While fine tuning-based defenses like QWEN3GUARD offer robust safety, they necessitate extensive fine-tuning (over 10 hours) on massive datasets (1.19M samples), incurring significant resource costs. In contrast, ICON is highly computationally agile, requiring less than 2 minutes of training on only 255 samples, with a minimal parameter count of approximately 31k. While heuristic-based defenses (e.g., MELON, Repeat Prompt) incur zero training cost, they rely on rigid, static policies that often fail against adaptive attacks. In summary, ICON thus occupies a sweet spot : it maintains the near-zero deployment overhead characteristic of heuristic methods while providing the learned, latent-aware security typically associated with heavy-weight fine-tuning.

## 5.3. Out-of-Distribution Performance

To evaluate the generalization capability of ICON , we test a prober trained exclusively on the TrojanTools dataset against unseen benchmarks, including AgentDojo (Debenedetti et al., 2024) and InjecAgent (Zhan et al., 2024). As presented in Table 3, the results demonstrate that ICON generalizes effectively to different distributions without requiring additional fine-tuning.

Specifically, ICON achieves a detection rate exceeding 97% on AgentDojo and remains robust on InjecAgent with a detection rate over 80%. More importantly, the utility recovery rate consistently exceeds 60% for both QWEN and LLAMA backbones on Qwen3-8B. This strong zero shot transfer performance suggests that ICON captures intrinsic latent traces of IPI attacks rather than over-fitting to specific semantic patterns in the training data, confirming its practical viability for real-world, evolving adversarial environments.

## 5.4. Visualization of Attention Steering

To provide an intuitive understanding of the Rectifier's mechanism, we visualize the attention maps of a critical head 21 from layer 22-25 in the QWEN-3-8B model. As illustrated in Figure 3, the impact of the ICON intervention is evident in the redistribution of attention weights. Under a

Figure 3. Visualization of Mitigate Rectifier's Functionality

<!-- image -->

Table 5. Effectiveness of ICON on Various Agentic MLLMs.

| FMs      | BU   | No Def.   | No Def.   | Repeat   | Repeat   | LLM-Det.   | LLM-Det.   | Ours   | Ours   |
|----------|------|-----------|-----------|----------|----------|------------|------------|--------|--------|
| FMs      | BU   | ASR ↑     | UA ↓      | ASR ↑    | UA ↓     | ASR ↑      | UA ↓       | ASR ↑  | UA ↓   |
| QWEN3VL  | 70.5 | 69.9      | 21.3      | 47.8     | 46.5     | 0.0        | 21.9       | 3.8    | 44.9   |
| INTERNVL | 64.5 | 47.4      | 33.9      | 41.6     | 26.5     | 0.0        | 34.1       | 2.6    | 47.8   |
| MINICPM  | 72.0 | 59.7      | 29.0      | 59.6     | 22.0     | 0.0        | 30.3       | 2.4    | 48.8   |
| Avg      | 69.0 | 59.0      | 28.1      | 49.7     | 31.7     | 0.0        | 28.8       | 2.9    | 47.2   |

Notes:

LLM-Detector = Gemini.

successful IPI attack (left), the model exhibits an extreme attention collapse, where the focus is disproportionately concentrated on specific token indices (approximately at positions 100 and 300) corresponding to the malicious payload. This localized 'over-focusing' indicates that the adversarial instruction has hijacked the head's functional priority. Conversely, after applying the MR (right), these anomalous adversarial peaks are effectively suppressed through our selective masking and steering operation. The attention is subsequently dispersed across the broader agent trajectory, restoring the model's ability to integrate global contextual information. This visualization confirms that the Rectifier successfully 'steers' the latent representation back to the benign task direction, thereby enabling the agent to execute its original plan despite the presence of adversarial triggers.

## 5.5. Robustness Across Modalities

To further validate the architectural agnosticism and crossmodal generalizability of ICON , we extend our evaluation to MLLMs. Unlike text-only scenarios, multi-modal IPI attacks can be embedded within visual inputs, posing a more complex threat to the agent's latent space. As presented in Table 5, ICON demonstrates robust defensive resilience in these vision-language contexts. In terms of security, our method achieves a competitive average ASR of 2.9%, a drastic reduction compared to the Repeat Prompt baseline (49.7%). While the commercial LLM-Detector achieves a zero ASR, its average utility (UA) is restricted to 28.8%, suggesting a tendency toward binary refusal that disrupts task continuity. In contrast, ICON effectively maintains the task execution loop, reaching a 47.2% UA, which is an absolute improvement of nearly 20% over Gemini. This underscores

that our attention steering mechanism successfully identifies and neutralizes adversarial triggers within multi-modal structures without sacrificing the agent's capabilities.

## 6. Conclusion

This paper introduces ICON, a defense framework against prompt injection attacks-particularly indirect prompt injection. ICON comprises two core defense modules and an adaptive module. Specifically, it employs a Focus Intensity Score to identify critical layers and attention heads, then leverages a latent-space trace prober to precisely detect adaptive attacks. A mitigating rectifier subsequently steers the LLM away from malicious intent toward its intended task. Extensive experiments across multiple LLMs and modalities demonstrate that ICON achieves an exceptional balance among security, utility, and efficiency: it maintains an ASR of just 0.4% while delivering over 50% higher utility than commercial-grade baselines. Most notably, its extreme efficiency requiring less than two minutes of training-makes ICON a practical and scalable safeguard for real-world autonomous agents.

## Impact Statement

This paper presents work whose goal is to advance the field of machine learning, specifically in the area of AI security and agent robustness. By developing more resilient autonomous agents, our work helps mitigate the risks associated with adversarial manipulation in large language models. There are many potential societal consequences of our work, most of which contribute to the safe and ethical deployment of AI, and none of which we feel must be specifically highlighted here beyond its contribution to general system reliability.

## References

Sandwitch defense. 2023. URL https: //learnprompting.org/docs/prompt\_ hacking/defensive\_measures/sandwich\_ defense.

Anonymous. Trojantools: Adaptive indirect prompt injection on LLM agents via malicious tool-calling. In Submitted to The Fourteenth International Conference on Learning Representations , 2025. URL https:// openreview.net/forum?id=PFbt5pWWh7 . under review.

Anthropic. Claude sonnet, 2024. URL https: //www.anthropic.com/transparency/ model-report .

Anysphere. Cursor: The ai coder editor, 2024. URL https: //cursor.com/agents .

- Arditi, A., Obeso, O., Syed, A., Paleka, D., Panickssery, N., Gurnee, W., and Nanda, N. Refusal in language models is mediated by a single direction. Advances in Neural Information Processing Systems , 37:136037-136083, 2024.

Bhagwatkar, R., Kasa, K., Puri, A., Huang, G., Rish, I., Taylor, G. W., Dvijotham, K. D., and Lacoste, A. Indirect prompt injections: Are firewalls all you need, or stronger benchmarks? In Lock-LLM Workshop: Prevent Unauthorized Knowledge Use from Large Language Models .

Chen, S., Zharmagambetov, A., Mahloujifar, S., Chaudhuri, K., Wagner, D., and Guo, C. Secalign: Defending against prompt injection with preference optimization. In Proceedings of the 2025 ACM SIGSAC Conference on Computer and Communications Security , pp. 2833-2847, 2025.

Chen, Z., Wu, J., Wang, W., Su, W., Chen, G., Xing, S., Zhong, M., Zhang, Q., Zhu, X., Lu, L., et al. Internvl: Scaling up vision foundation models and aligning for generic visual-linguistic tasks. In Proceedings of the IEEE/CVF conference on computer vision and pattern recognition , pp. 24185-24198, 2024.

- Chuang, Y.-S., Qiu, L., Hsieh, C.-Y., Krishna, R., Kim, Y., and Glass, J. Lookback lens: Detecting and mitigating contextual hallucinations in large language models using only attention maps. In Proceedings of the 2024 Conference on Empirical Methods in Natural Language Processing , pp. 1419-1436, 2024.
- Crosbie, J. and Shutova, E. Induction heads as an essential mechanism for pattern matching in in-context learning. In Findings of the Association for Computational Linguistics: NAACL 2025 , pp. 5034-5096, 2025.
- Debenedetti, E., Zhang, J., Balunovic, M., Beurer-Kellner, L., Fischer, M., and Tram` er, F. Agentdojo: A dynamic environment to evaluate prompt injection attacks and defenses for LLM agents. In The Thirty-eight Conference on Neural Information Processing Systems Datasets and Benchmarks Track , 2024. URL https: //openreview.net/forum?id=m1YYAQjO3w .
- Gould, R., Ong, E., Ogden, G., and Conmy, A. Successor heads: Recurring, interpretable attention heads in the wild. In The Twelfth International Conference on Learning Representations .
- Greshake, K., Abdelnabi, S., Mishra, S., Endres, C., Holz, T., and Fritz, M. Not what you've signed up for: Compromising real-world llm-integrated applications with indirect prompt injection. In Proceedings of the 16th ACM workshop on artificial intelligence and security , pp. 7990, 2023.
- Han, C., Xu, J., Li, M., Fung, Y., Sun, C., Jiang, N., Abdelzaher, T., and Ji, H. Word embeddings are steers for language models. In Ku, L.-W., Martins, A., and Srikumar, V. (eds.), Proceedings of the 62nd Annual Meeting of the Association for Computational Linguistics (Volume 1: Long Papers) . Association for Computational Linguistics, August 2024.
- Hung, K.-H., Ko, C.-Y., Rawat, A., Chung, I.-H., Hsu, W. H., and Chen, P.-Y. Attention tracker: Detecting prompt injection attacks in llms. In Findings of the Association for Computational Linguistics: NAACL 2025 , pp. 23092322, 2025.
- Jain, N., Schwarzschild, A., Wen, Y., Somepalli, G., Kirchenbauer, J., Chiang, P.-y., Goldblum, M., Saha, A., Geiping, J., and Goldstein, T. Baseline defenses for adversarial attacks against aligned language models. arXiv preprint arXiv:2309.00614 , 2023.
- Liang, X., Wang, H., Wang, Y., Song, S., Yang, J., Niu, S., Hu, J., Liu, D., Yao, S., Xiong, F., et al. Controllable text generation for large language models: A survey. CoRR , 2024.
- Liu, X., Jha, S., McDaniel, P., Li, B., and Xiao, C. Autohijacker: Automatic indirect prompt injection against blackbox LLM agents, 2025. URL https://openreview. net/forum?id=2VmB01D9Ef .
- Llama Team, A. . M. The llama 3 herd of models, 2024. URL https://arxiv.org/abs/2407.21783 .
- Ministral. Mistral models, 2025. URL https://docs. mistral.ai/api/ .
- OpenAI and Josh Achiam, e. a. Gpt-4 technical report, 2024. URL https://arxiv.org/abs/2303.08774 .
- Perez, F., Ribeiro, I., and Studio, A. Ignore previous prompt: Attack techniques for language models. 2024.
- ProtectAI.com. Fine-tuned deberta-v3 for prompt injection detection, 2023. URL https://huggingface.co/ProtectAI/ deberta-v3-base-prompt-injection .
- Ray, P. P. A survey on model context protocol: Architecture, state-of-the-art, challenges and future directions. Authorea Preprints , 2025.
- Team, G., Anil, R., Borgeaud, S., Alayrac, J.-B., Yu, J., Soricut, R., Schalkwyk, J., Dai, A. M., Hauth, A., Millican, K., et al. Gemini: a family of highly capable multimodal models. arXiv preprint arXiv:2312.11805 , 2023.
- Todd, E., Li, M., Sharma, A. S., Mueller, A., Wallace, B. C., and Bau, D. Function vectors in large language models. In The Twelfth International Conference on Learning Representations .
- Touvron, H., Lavril, T., Izacard, G., Martinet, X., Lachaux, M.-A., Lacroix, T., Rozi` ere, B., Goyal, N., Hambro, E., Azhar, F., et al. Llama: Open and efficient foundation language models. arXiv preprint arXiv:2302.13971 , 2023.
- Wan, S., Nikolaidis, C., Song, D., Molnar, D., Crnkovich, J., Grace, J., Bhatt, M., Chennabasappa, S., Whitman, S., Ding, S., Ionescu, V., Li, Y., and Saxe, J. Cyberseceval 3: Advancing the evaluation of cybersecurity risks and capabilities in large language models, 2024. URL https://arxiv.org/abs/2408.01605 .
- Willison, S. prompt injection attacks against gpt3. 2022. URL https://simonwillison.net/ 2022/Sep/12/prompt-injection/ .
- Willison, S. Delimiters won't save you from prompt injection. 2023. URL https://simonwillison.net/ 2023/May/11/delimiters-wont-save-you .
- Wu, C. H., Shah, R. R., Koh, J. Y., Salakhutdinov, R., Fried, D., and Raghunathan, A. Dissecting adversarial robustness of multimodal lm agents. In The Thirteenth International Conference on Learning Representations .

- Wu, L., Wang, M., Xu, Z., Cao, T., Oo, N., Hooi, B., and Deng, S. Automating steering for safe multimodal large language models. In Proceedings of the 2025 Conference on Empirical Methods in Natural Language Processing , pp. 792-814, 2025.
- Xia, J., Zhu, H., Pang, S., Lu, Z., Li, B., Zhou, Y., and Xue, J. One head to rule them all: Amplifying lvlm safety through a single critical attention head. In The Thirty-ninth Annual Conference on Neural Information Processing Systems , 2025.
- Yang, A., Li, A., Yang, B., Zhang, B., Hui, B., Zheng, B., Yu, B., Gao, C., Huang, C., Lv, C., et al. Qwen3 technical report. arXiv preprint arXiv:2505.09388 , 2025.
- Yao, S., Zhao, J., Yu, D., Du, N., Shafran, I., Narasimhan, K. R., and Cao, Y. React: Synergizing reasoning and acting in language models. In The eleventh international conference on learning representations , 2022.
- Yu, T., Wang, Z., Wang, C., Huang, F., Ma, W., He, Z., Cai, T., Chen, W., Huang, Y., Zhao, Y., et al. Minicpm-v 4.5: Cooking efficient mllms via architecture, data, and training recipe. arXiv preprint arXiv:2509.18154 , 2025.
- Zhan, Q., Liang, Z., Ying, Z., and Kang, D. Injecagent: Benchmarking indirect prompt injections in toolintegrated large language model agents. In Findings of the 62nd Annual Meeting of the Association for Computational Linguistics, ACL 2024 , pp. 10471-10506. Association for Computational Linguistics (ACL), 2024.
- Zhan, Q., Fang, R., Panchal, H. S., and Kang, D. Adaptive attacks break defenses against indirect prompt injection attacks on LLM agents. In Chiruzzo, L., Ritter, A., and Wang, L. (eds.), Findings of the Association for Computational Linguistics: NAACL 2025 , pp. 7101-7117, Albuquerque, New Mexico, April 2025a. Association for Computational Linguistics. ISBN 9798-89176-195-7. doi: 10.18653/v1/2025.findings-naacl. 395. URL https://aclanthology.org/2025. findings-naacl.395/ .
- Zhan, Q., Fang, R., Panchal, H. S., and Kang, D. Adaptive attacks break defenses against indirect prompt injection attacks on llm agents. In Findings of the Association for Computational Linguistics: NAACL 2025 , pp. 71017117, 2025b.
- Zhang, C., Jin, M., Yu, Q., Liu, C., Xue, H., and Jin, X. Goal-guided generative prompt injection attack on large language models. In 2024 IEEE International Conference on Data Mining (ICDM) , pp. 941-946. IEEE, 2024.
- Zhang, F., Zhang, J., Wang, C., Sun, X., Hao, Y., Guan, G., Li, W., Huang, L., and Lim, W. Y. B. Dualtap: A
- dual-task adversarial protector for mobile mllm agents. arXiv preprint arXiv:2511.13248 , 2025a.
- Zhang, J., Ye, J., Ma, X., Li, Y., Yang, Y., Chen, Y., Sang, J., and Yeung, D.-Y. Anyattack: Towards large-scale self-supervised adversarial attacks on vision-language models. In Proceedings of the Computer Vision and Pattern Recognition Conference , pp. 19900-19909, 2025b.
- Zhang, Q., Singh, C., Liu, L., Liu, X., Yu, B., Gao, J., and Zhao, T. Tell your model where to attend: Post-hoc attention steering for llms. In The Twelfth International Conference on Learning Representations .
- Zhao, H., Yuan, C., Huang, F., Hu, X., Zhang, Y., Yang, A., Yu, B., Liu, D., Zhou, J., Lin, J., et al. Qwen3guard technical report. arXiv preprint arXiv:2510.14276 , 2025.
- Zhu, K., Yang, X., Wang, J., Guo, W., and Wang, W. Y. Melon: Provable defense against indirect prompt injection attacks in ai agents. arXiv preprint arXiv:2502.05174 , 2025.