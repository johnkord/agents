## Failure Makes the Agent Stronger: Enhancing Accuracy through Structured Reflection for Reliable Tool Interactions

Junhao Su 1 * Yuanliang Wan 1 * Junwei Yang 1 *

Hengyu Shi 2 Tianyang Han 2 Junfeng Luo 1 , 2 B Yurui Qiu 1 † B

1 Vision Agent Team, Meituan 2 MeiGen AI Team, Meituan {luojunfeng,qiuyurui}@meituan.com

## Abstract

Tool-augmented large language models (LLMs) are typically trained via supervised imitation learning or coarse-grained reinforcement learning, approaches that primarily optimize one-shot tool calls. Existing practices of self-reflection largely rely on heuristic prompting or unidirectional reasoning traces: the model is encouraged to 'think more,' rather than to treat error diagnosis and correction as a learnable capability. This makes them fragile in multi-turn interaction settings-once a call fails, the model tends to repeat the same mistake instead of recovering. To address this issue, we propose structured reflection, which transforms the 'from error to repair' process into a first-class, controllable, and trainable action. The agent produces a concise yet precise reflection process: specifically, the model diagnoses the error based on evidence from the previous step and then proposes a correct and executable follow-up call. During training, we combine DAPO and GSPO's objective functions and design a more principled reward mechanism tailored to tool calling, optimizing the stepwise strategy Reflect → Call → Final. To evaluate this capability, we introduce Tool-Reflection-Bench, a lightweight benchmark dataset that programmatically verifies structural validity, executability, parameter correctness, and result consistency. Tasks in the benchmark are constructed as miniature trajectories of Erroneous Call → Reflection → Corrected Call and are split into disjoint training and testing sets. Experiments on BFCL v3 and Tool-Reflection-Bench show that our method achieves significant improvements in multi-turn tool-call success rates and error recovery, while also reducing redundant calls. These results demonstrate that making reflection explicit and treating it as an optimization objective can substantially enhance the reliability of tool interaction, providing a reproducible pathway for agents to grow stronger by learning from failure. We will release all the code and datasets as open source once the paper is accepted by the community.

## 1 Introduction

The integration of external tools with large language models through tool calling represents a significant breakthrough in the development of agents. It transforms large language models from mere text generators into highly practical tools for interacting with humans [1, 2], significantly enhancing the ability of AI agents to solve complex real-world tasks [3-5]. Tool calling bridges the gap between

∗ Equal Contribution. B Corresponding Authors. † Project Leader.

<!-- image -->

(a) Results on BFCL v3

<!-- image -->

(b) Results on Tool-Reflection-Bench test set

Figure 1: In the experiments on BFCL v3 and Tool-Reflection-Bench, our method significantly improves the multi-turn tool-calling accuracy of several open-source LLMs on BFCL v3. At the same time, it substantially enhances the error-repair rate for tool calls on the Tool-Reflection-Bench test set, achieving performance that even surpasses that of closed-source LLMs with comparable parameter sizes.

the vast internal knowledge of LLMs and external resources, enabling LLMs to access up-to-date information, perform delicate computations, and more, thereby unlocking their broad potential for applications across multiple domains [6-8].

Currently, the training of tool-call capabilities in large language models typically relies on supervised fine-tuning and reinforcement learning [9, 10], where these methods optimize the ability for single-turn tool calls through carefully designed reward mechanisms. However, these approaches face several challenges in the context of tool calling. First, the issue of rewards in tool calling is particularly prominent-small errors in parameter selection or formatting often render the entire function call invalid, thus limiting the effective learning signal [11]. Second, existing methods generally rely on unidirectional reasoning, which, while sufficient for simpler scenarios, has clear limitations: when LLMs make mistakes during tool calls, they often struggle to locate the root cause of the error [12]. While generating correct function calls is crucial, it is even more important for LLMs to learn how to identify and correct their own mistakes [13].

To address the above-mentioned issues, we propose an innovative reflection process aimed at error localization and correction through explicit reflection steps, which differs from existing forward reasoning methods. Specifically, we design a process in which the LLM intentionally makes mistakes during tool calls, carefully crafts reflection content based on the errors, and then generates the correct call. Through this approach, we transform the self-correction ability of large models from a heuristic process [14] into a clear, trainable capability. Our training approach is primarily reinforcement learning-based. During the reinforcement learning process, we specifically design a customized reward mechanism tailored for tool-calling scenarios, with a particular emphasis on multi-turn interactions. Concretely, the reward design encompasses multiple dimensions, including format reward, tool-name reward, parameter reward, and semantic reward of reflection, which together provide the model with multi-dimensional feedback and effectively guide its learning, and we further combine DAPO's decoupled clipping range and dynamic sampling-expanding exploration while skipping near-zero-advantage rollouts-with GSPO's sequence-level importance sampling and same-granularity clipping, which avoids token/sequence mismatch and stabilizes optimization. With this training methodology, our approach equips LLMs with genuine self-reflection and error-correction capabilities. On the BFCL v3 benchmark, our method yields significant improvements in LLM accuracy for multi-turn tool calling, thereby demonstrating its effectiveness in real-world applications.

We construct a Tool-Reflection-Bench based on the BUTTON dataset [15] style. First, we collected tool-call failure cases from real-world scenarios and various benchmarks, analyzing and summarizing several common failure patterns. Next, We selected several existing tool-call datasets [4, 16] and randomly combined them according to the call style of the BUTTON dataset and introduced these failure patterns into the data, disrupting the originally correct call processes to generate failure

cases. Finally, we meticulously designed a reflection process to repair these failures, resulting in successful tool calls. The training set includes the complete process described above to train LLMs to achieve true self-correction capabilities, while the test set only contains the first two steps, used to evaluate the self-correction abilities of the LLMs. By constructing the Tool-Reflection-Bench in this manner, combined with our custom reward mechanism for tool calling, we have made breakthroughs in LLMs' self-correction abilities during training. Particularly in multi-turn tool-calling scenarios, we observed significant improvements in accuracy. Through the reasoning process from failure to correction, LLMs can more effectively identify and learn from potential mistakes, thus enhancing the model's stability and robustness in interactions. This makes the agent's behavior more robust and powerful.

In summary, our contributions are as follows:

- We introduce an explicit, trainable reflection process that diagnoses the cause of a failed tool call using prior evidence and proposes a corrected, executable call. This transforms the "from failure to repair" process from a heuristic method into a learnable action strategy, enabling LLMs to genuinely possess self-reflection and error-correction capabilities, thereby enhancing the agent's multi-turn interactions with users.
- We design a more effective reward mechanism for tool call, tailored for RL training, using a GRPO-style objective function. This approach employs multi-dimensional rewards-format executability, tool name accuracy, parameter correctness, and semantic consistency-to mitigate sparse rewards and propagate signals across multi-turn trajectories.
- We propose Tool-Reflection-Bench, which collects failure patterns from real interaction scenarios and benchmark datasets, injects perturbations into correct calls, and attaches a reflection process to repair the calls. This allows for training LLMs in their Self-Correction ability in tool-calling scenarios.
- Our method significantly improves the accuracy of multi-turn tool calls and the ability to recover from tool call errors, while maintaining competitive single-turn tool call performance. We validate this by experiments on BFCL v3 [17] and Tool-Reflection-Bench.

## 2 Related Works

## 2.1 Tool-augmented Large Language Models

Integrating external tools into large language models has become a key approach to enhancing their functionality, surpassing the simple task of text generation. Traditional LLMs are limited by static knowledge, constrained to the data they were trained on. However, tool-augmented models extend the capabilities of LLMs by enabling them to interact with external resources [18, 19] (such as APIs [20], databases, and computational engines) through tool calls. This extension allows LLMs to access real-time data, perform external computations, and even interface with external hardware, making them more practical for solving complex real-world tasks that require dynamic information or specific external operations [21]. ToolBench [4] demonstrates the feasibility of integrating external tool calls into LLMs. Through such systems, LLMs can handle more specialized tasks. However, one major challenge of tool augmentation is how to effectively train LLMs to use these tools. Existing training methods, such as supervised fine-tuning and reinforcement learning, typically focus on optimizing single tool calls. This type of interaction often does not involve multi-turn tool calls or responses, which makes the limitations of current methods particularly apparent when errors occur during tool usage. In such cases, the model's ability to recover from errors becomes crucial.

## 2.2 Self-Correction in LLMs

Self-correction in large language models refers to the model's ability to diagnose its own errors and correct them based on previous actions [22, 23]. However, this area has not been fully explored. Existing self-correction techniques mostly rely on heuristic methods or unidirectional reasoning processes [24].

Self-Refine framework [25], which involves having LLMs provide an initial response, followed by a reflection process where the model identifies flaws and makes improvements. Specifically, the same LLM acts as both the responder and the evaluator: the model first generates an initial response, then

Figure 2: We illustrate the effectiveness of our method with an example. As shown in the figure, the left side presents the tool panel, while the upper-right part depicts industry-standard self-correction approaches, where models attempt to fix errors through heuristic trial-and-error reasoning or by relying on external feedback. In contrast, the lower-right part shows our approach: we introduce an explicit forced reflection process &lt; r &gt; , enabling the model to truly master the ability to repair errors based on its own failures.

<!-- image -->

self-reflects and iteratively revises the output. This approach has been shown to enhance the performance of LLMs in certain domains. However, subsequent studies [26, 27] have found that relying solely on the model itself often fails to detect subtle errors. Some research [28, 29] has introduced auxiliary verifiers (such as additional models or mechanisms [30, 31]) to help check the correctness of the initial response. This external self-checking assistance avoids unnecessary repeated revisions, improving efficiency and enhancing the model's reasoning and verification capabilities. However, this approach remains highly sensitive to the specific phrasing of the prompts, with different prompt wordings leading to varying results [23].

However, even though these methods have somewhat improved the capabilities of LLMs, their essence still relies on external feedback to assist in correcting their own errors. In contrast, our work introduces a reflection method that transforms self-correction into a trainable and controllable capability. The reflection process is an explicit part of the task, where the model actively evaluates its previous actions, identifies errors, and generates explicit corrections. This process is achieved through error localization, diagnosis, and correction, which goes beyond simple unidirectional reasoning and can be integrated into the training process. By providing supervisory signals during training, our approach enables LLMs to truly possess self-correction capabilities, which are then reflected in tool calling tasks.

## 3 Method

## 3.1 Tool-Reflection-Bench

The construction of Tool-Reflection-Bench consists of the following steps: perturbation-based disruptions, positive samples transformations, and the reflection repair process. The original positive samples are derived from BUTTON [15] transformations and self-constructed based on few-shot prompts. The entire benchmark is divided into a training set and a test set, with approximately 5,000 samples in the training set, in addition to the reflection-augmented data constructed as described above, the training set also contains a very small portion of original data drawn from BUTTON [15] and XLAM [18]. And around 1,000 samples in the test set, the test set is exclusively composed of perturbation-derived items and does not include raw, unperturbed positives from BUTTON or XLAM.

## 3.1.1 Perturbation-based Disruptions

Let the initial correct message sequence be

<!-- formula-not-decoded -->

where m sys 0 is the system prompt, m usr 1 the user query, m ast 2 i the assistant's i -th tool call in structured form (e.g., &lt;call&gt;[{. . . },{. . . },...]&lt;/call&gt; ), m tool 2 i +1 the tool return (JSON), and m fi nal n the final answer.

We define a set of disruption operators

<!-- formula-not-decoded -->

each operating on an assistant call m ast 2 k and instantiating a common failure mode:

1. P 1 call-order swap : replace the current tool call with the next-round tool call dialogue and force an error.
2. P 2 redundant call : repeat the same tool at the step (unchanged/irrelevant arguments) and force an error.
3. P 3 missing call : replace the intended tool by another tool and force an error.
4. P 4 argument error : randomly corrupt the arguments of a call (missing/typed/alias/boundary) and force an error.

These operators specify how a correct tool call can be broken.

## 3.1.2 Positive Samples Transformations

Given a clean trajectory D + and a chosen operator P j ∈ P acting on step 2 k , we produce the negative (erroneous) context; no repair is performed in this step. We construct the erroneous call

<!-- formula-not-decoded -->

and simulate the tool's error feedback with a LLM L :

<!-- formula-not-decoded -->

This yields the negative trajectory prefix

<!-- formula-not-decoded -->

which will later serve as evidence of failure. At this stage, the item consists only of the broken call and its error signal.

## 3.1.3 Reflection Repair Process

Given a clean trajectory D + and its perturbed prefix D -, we present the LLM with a paired view of the step2 k evidence:

<!-- formula-not-decoded -->

The model outputs a response.

The finalized item is packaged as

<!-- formula-not-decoded -->

where r briefly diagnoses the discrepancy, and c proposes the fixed tool call. We then apply human supervision to obtain ( r ⋆ , c ⋆ ) , with c ⋆ set to the original correct call:

<!-- formula-not-decoded -->

<!-- formula-not-decoded -->

<!-- formula-not-decoded -->

where D + &gt; 2 k +1 is the untouched suffix of D + (including m fi nal n ). We retain x only if: (i) tags/JSON are well-formed; (ii) c ⋆ is executable; (iii) r ⋆ correctly cites the clean-broken contrast.

## 3.2 Reward Design

Preliminary. Given a model completion C and a ground truth G , we decompose both into three (possibly empty) parts:

<!-- formula-not-decoded -->

Here c ref ( reflection ) is the diagnosis text wrapped in &lt;reflect&gt;&lt;/reflect&gt; , C calls is the multiset of tool calls wrapped in &lt;call&gt;&lt;/call&gt; s produced by the model, and cfi nal is the message wrapped in &lt;final&gt;&lt;/final&gt; . The ground truth can alse be decomposed into three parts mentioned above.

Component scores. We compute three component scores:

<!-- formula-not-decoded -->

where Sim ∈ [0 , 1] is a semantic similarity function, and I [ · ] is the indicator:

<!-- formula-not-decoded -->

We say EqualCalls( C calls , G calls ) holds iff the two sets of produced calls can be put in a one-to-one correspondence such that for every matched pair the tool name is identical and the argument is identical.

Normalization with presence masks. Our goal is to keep the aggregated score in [0 , 1] even when an instance specifies only a subset of targets (e.g., only &lt;call&gt; without &lt;reflect&gt; or &lt;final&gt; ). To this end we use normalization to renormalize over the parts that actually appear in the ground truth, so the maximum remains 1 regardless of how many parts are present.

We define

<!-- formula-not-decoded -->

̸

̸

Let ( w r , w c , w f ) ≥ 0 be normalized base weights (e.g., w r + w c + w f = 1 ). We renormalize over the active parts via

<!-- formula-not-decoded -->

The aggregated structure/semantics score is then

<!-- formula-not-decoded -->

This normalization yields a consistent scoring standard across fully and partially supervised instances, avoiding artificial deflation of scores when some targets are absent.

Format/penalty factor. We designed structural penalties tailored for tool-call data formats. Specifically, P miss accounts for cases where the tool is not invoked at all, while P extra and P count penalize redundant calls and mismatches in the total number of calls, respectively. Let

<!-- formula-not-decoded -->

Here n and m denote the number of tools invoked in the ground truth and completion calls. Define the three components:

̸

<!-- formula-not-decoded -->

̸

̸

<!-- formula-not-decoded -->

̸

̸

<!-- formula-not-decoded -->

Let EqualCalls be the schema-strict equality on bags of calls. We use a reduction factor

<!-- formula-not-decoded -->

Aggregate the penalty as

<!-- formula-not-decoded -->

and define the instance-wise format factor

<!-- formula-not-decoded -->

Here β extra , γ count , λ m ≥ 0 control the strength of extra-part, count-mismatch, and overall scaling penalties, respectively; ( w ref , w calls , wfi nal ) ≥ 0 are part weights.

(a) The reward curve of llama-3.1-8b-Instruct during RL training (b) The reward curve of qwen2.5-7b-Instruct during RL training

<!-- image -->

Figure 3: The reward curves of llama-3.1-8B and Qwen2.5-7B during training, showing an overall upward trend.

## Core reward and backoff. The core reward is

<!-- formula-not-decoded -->

Early in training, S contains a binary component ( s call ∈{ 0 , 1 } ) and F applies hard penalties; small formatting or argument errors can drive R core close to zero. This yields sparse or unstable gradients and large variance across samples. To stabilize learning and provide a dense shaping signal when the exact-match objective is not yet achieved, we introduce a similarity backoff:

<!-- formula-not-decoded -->

where w b ∈ (0 , 1] and concat( · ) linearizes the messages. We use clip [0 , 1] ( x ) = max(0 , min(1 , x )) to keep rewards bounded.

## 3.3 RL for Tool-Reflection-Bench

We adopt a reinforcement-learning objective for tool calling that combines two complementary ideas: (i) DAPO-style decoupled clipping [32]: we use a decoupled clipping range with different lower/upper bounds ( ε low , ε high ) and a clip-higher policy (a looser upper bound when r &gt; 1 for positive advantages), and we skip uninformative prompt groups whose rollouts carry negligible learning signal; (ii) GSPO-style sequence-level importance sampling [33]: we compute the importance ratio at the sequence level and apply clipping at the same granularity as the sequence-level reward, which avoids the mismatch between token-wise importance sampling and sequence-level rewards and stabilizes optimization.

Table 1: Comparison across dimensions (Base, Miss\_Func, Miss\_Param, Long\_Context, Multi-turn Overall) on BFCL v3.

| Models                   | Method      | Base               | Miss_Func          | Miss_Param        | Long_Context      | Multi-turn Overall   |
|--------------------------|-------------|--------------------|--------------------|-------------------|-------------------|----------------------|
| Llama-3.1-8B-Instruct-FC | Origin Ours | 5.0 9.5 ( ↑ 95%)   | 6.5 7.0 ( ↑ 8%)    | 4.5 5.0 ( ↑ 11%)  | 4.5 7.0 ( ↑ 56%)  | 5.12 7.12 ( ↑ 39%)   |
| Qwen2.5-7B-Instruct-FC   | Origin Ours | 16.5 22.0 ( ↑ 33%) | 11.0 13.0 ( ↑ 18%) | 9.0 13.5 ( ↑ 50%) | 7.5 11.0 ( ↑ 47%) | 11.00 14.88 ( ↑ 35%) |
|                          |             | 18.0 ↑             | 19.0               | 13.5              | 14.5 21.5 ( ↑     | 16.25 20.75 ( ↑ 28%) |
| Qwen3-4B-Instruct        | Origin Ours | 25.0 ( 39%)        | 19.5 ( ↑ 3%)       | 17.0 ( ↑ 26%)     | 48%)              |                      |

Objective. Let ( q, a ) denote the dialog context and the ground-truth targets, and let { o i } G i =1 be G candidates sampled from the behavior policy π θ old ( · | q ) . Each completion o i is scored by the reward in Sec. §3.2, yielding R i ∈ [0 , 1] . We maximize a sequence-level, asymmetrically clipped objective and minimize its negative as the loss:

<!-- formula-not-decoded -->

where clip( x, a, b ) = min { b, max { a, x }} and typically ε high &gt; ε low ('clip-higher').

Prompt-group dynamic filtering. DAPO skips prompt groups whose candidates provide almost no learning signal (e.g., all-correct or all-wrong). Concretely, define batch-normalized advantages and a group-level acceptance criterion:

<!-- formula-not-decoded -->

and require sufficient reward dispersion within the group:

<!-- formula-not-decoded -->

If (28) fails, we drop the zero-information rollouts and (optionally) draw up to K additional candidates from π θ old , then re-apply the filter. Only indices in S ( q, a ) contribute to the expectation in (26).

Sequence-level importance ratio. For a completion o i = ( o i, 1 , . . . , o i, | o i | ) , we use the geometricmean, length-normalized importance ratio:

<!-- formula-not-decoded -->

and perform clipping at the same sequence granularity as the reward (see (26)), thereby avoiding token/sequence granularity mismatch.

## 4 Experiments

## 4.1 Experiment Settings

In this part, we will detail the experimental setup, including datasets, hyperparameters, base models, and evaluation metrics.

Datasets. We conduct training on our self-constructed Tool-Reflection-Bench. After human supervision and post-editing, we retained approximately 5k samples in JSONL format to ensure compatibility with RL training under the Swift [34] framework.

Implementation Details. We train models for 1 epoch (a total of 1,000 steps) on 5,000 training samples, using the reward function defined in Sec.3.2. For each training instance, 4 completions were sampled to form a group. The training parameters were set as follows: temperature = 0.85, repetition penalty = 1.1, epsilon = 0.2, epsilon-high = 0.28, with a dynamic sampling strategy adopted.

Base Models. To verify the generalizability of Tool-Reflection-Bench and our training methodology, we conducted experiments using Llama3.1-8B [35], Qwen2.5-7B-Instruct [36], and Qwen3-4B [37] as base models.

Evaluation Metrics. We evaluated multi-turn tool-calling performance using the Berkeley Function Calling Leaderboard (BFCL) v3 [17], with evaluation dimensions covering multi-turn-base, multi-turn-long-context, multi-turn-miss-func, and multi-turn-miss-param, and the evaluation metric being accuracy. To assess the model's repair capability when tool calls fail, we used ToolReflection-Bench, with the evaluation metric being repair rate, Repair@n denotes that for the same data instance, if at least one out of n trials succeeds, the metric is recorded as 1; otherwise, it is 0.

## 4.2 Experiment Results

## 4.2.1 Result on BFCL v3

Comparison with base models. Weconduct performance evaluation on the multi-turn category of BFCL v3 to assess the benefits of enhancing the model's self-reflection capability in multi-turn tool calling, the detailed results are showed in Table. 1. Compared the results against the corresponding base models. The most striking lift appears on Llama-3.1-8B : Base rises from 5.0 to 9.5 (+95%) and Long\_Context from 4.5 to 7.0 (+56%). Qwen2.5-7B shows the largest Miss\_Param gain (9.0 → 13.5, +50%), evidencing stronger parameter repair. Qwen3-4B attains an amazing absolute Multi-turn Overall (20.75, +28%) with a sizable Long\_Context improvement (+48%). In contrast, its Miss\_Func gain is modest (19.0 → 19.5, +3%), indicating tool selection remains comparatively harder-consistent with our method's emphasis on reflection-driven parameter correction and longcontext recovery.

## 4.2.2 Result on Tool-Reflection-Bench

As shown in Table. 2, across open-source baselines, repair rates are low at one try (Repair@1 ≤ 9.6%) and only mildly improve with more tries. Training with our method yields consistent gains for all bases: Llama-3.1-8B-Instruct jumps from 0 . 7 / 5 . 1 / 6 . 8 to 4.7/20.5/26.4 (Repair@1/3/5), a large improvement especially at higher n ; Qwen2.5-7B-Instruct improves from 2 . 4 / 6 . 1 / 8 . 0 to 9.3/10.3/11.4 ; Qwen3-4B-Instruct rises from 9 . 6 / 10 . 6 / 10 . 6 to 14.9/18.5/19.5 (best Repair@1 among our models). All finetuned models surpass the closed-source LongCat-Lite-8K-Chat across n ∈{ 1 , 3 , 5 } , indicating that our reflection-aware reward and RL objective substantially enhance repairability and yield more reliable multi-try recovery. It is also worth noting that when tool calls fail and require repair, our method achieves superior performance compared to closed-sourced models of the same scale such as LongCat-Lite-8K-Chat [38], GPT-4o-mini [39, 40], GPT-4.1-mini [41].

Table 2: Experimental Results of Open-Source and Closed-Source Models on the Tool-ReflectionBench Test Set.

| Models                                                      | Repair@1 (%)                              | Repair@3 (%)                              | Repair@5 (%)                              |
|-------------------------------------------------------------|-------------------------------------------|-------------------------------------------|-------------------------------------------|
| Close-Sourced Models                                        | Close-Sourced Models                      | Close-Sourced Models                      | Close-Sourced Models                      |
| LongCat-Lite-8K-Chat GPT-4o-mini GPT-4.1-mini               | 2.3 6.1 3.1                               | 3.4 8.7 4.3                               | 4.9 9.0 5.1                               |
| Open-Sourced Models                                         | Open-Sourced Models                       | Open-Sourced Models                       | Open-Sourced Models                       |
| Llama-3.1-8B-Instruct Qwen2.5-7B-Instruct Qwen3-4B-Instruct | 0.7 2.4 9.6                               | 5.1 6.1 10.6                              | 6.8 8.0 10.6                              |
| Open-Sourced Models Trained on Our Method                   | Open-Sourced Models Trained on Our Method | Open-Sourced Models Trained on Our Method | Open-Sourced Models Trained on Our Method |
| Llama-3.1-8B-Instruct Qwen2.5-7B-Instruct Qwen3-4B-Instruct | 4.7 9.3 14.9                              | 20.5 10.3 18.5                            | 26.4 11.4 19.5                            |

## 5 Conclusion

This paper proposes a structured reflection method for handling tool call failures, transforming the 'from error to repair' process into an explicit, controllable, and trainable action. Our approach overcomes the limitations of previous heuristic, feedback-based self-correction methods in terms of controllability and stability. We further construct Tool-Reflection-Bench for both training and evaluation, and design a task-specific reward function tailored to the tool-calling scenario. In the reinforcement learning stage, we combine the strengths of DAPO and GSPO to enhance training effectiveness. Experimental results show that the proposed method significantly improves multiturn tool call accuracy on BFCL v3 as well as error repair performance on Tool-Reflection-Bench. Overall, our method and dataset effectively enhance the reliability of tool interactions and offer a new perspective on enabling agents to acquire new capabilities by learning from failure.

## 6 Prompt for Perturbation-based Disruptions

In this section, we provide simplified prompts for generating the four types of tool call perturbations, enabling the community to reproduce our setting. The full prompts and implementation code will be released upon the paper's acceptance.

## 6.1 Prompt for Call-Order Swap

How to construct an error tool call example

## System

Goal. Prepend a controlled erroneous &lt;call&gt; and a consistent tool-error message before the first assistant message, so the model must diagnose and repair.

## Procedure.

1. Extract calls: Traverse messages and collect all assistant &lt;call&gt;...&lt;/call&gt; blocks (regex).
2. Choose function name: Parse the last call's JSON to get "name" ; fall back to a regex if needed.
3. Synthesize wrong call (empty args):
4. Fabricate tool error (pretty JSON string):
5. Insert pair: Place the wrong assistant call and the tool error before the original first assistant message.
6. Elicit reflection: Query the LLM with the System/User prompts above to obtain the reflection text, then prepend &lt;reflect&gt;...&lt;/reflect&gt; to the original assistant message (the original correct call remains).

```
<call>[{"name":"<FUNC_FROM_LAST_CALL>","arguments":{}}]</call>
```

```
{"tool":"<FUNC_FROM_LAST_CALL>","status":"warning", "message":"The called function executed but returned partial/mismatched ↪ → data because the arguments did not match the expected schema for ↪ → this call.", "result": null}
```

Notes. Using the last call's function ensures schema plausibility; empty arguments induce a controlled failure; the synthetic tool message supplies concrete evidence for the subsequent reflection and repair.

## How to generate a reflection

## System

You are an AI assistant that analyzes failed tool calls and provides reflective summaries. Given an original tool call and a fabricated error response, generate a brief reflection explaining why the call likely failed and how to correct it. Be concrete and concise.

```
User Fill the placeholders {{...}} exactly. Original tool call: {{ORIGINAL_CALL}} Error response: {{FAKE_RESPONSE}} Please provide a short reflection on the failure cause and the corrective action.
```

## An Example

```
User
```

```
Original tool call: <call>[{"name":"searchArtistsByArtStyle","arguments":{}}]</call> Error response: call.",
```

```
{"tool":"searchArtistsByArtStyle","status":"warning", "message":"The called function executed but returned partial/mismatched data ↪ → because the arguments did not match the expected schema for this "result": null}
```

Please provide a brief reflection on why this tool call failed and what could be improved. Keep it concise and helpful.

## 6.2 Prompt for Redundant Call

How to construct a redundant tool call example

## System

Goal. Inject a redundant tool call inside an existing &lt;call&gt; list and a matching redundant tool response, so the agent must identify and remove the duplication.

## Procedure.

1. Extract calls: Traverse the dialogue and collect all assistant-side &lt;call&gt;...&lt;/call&gt; blocks (regex).
2. Pick a target (not the first): Uniformly sample an assistant call position from { 2 , . . . , |C|} .
3. Duplicate within the list: Parse the target call's JSON. If it is a list, append a deepcopied first element; if it is a single dict, make a two-element list by duplicating it.
4. Fabricate a redundant tool response: Parse the following tool message. Duplicate its first item (or the dict itself) and mark it as redundant, e.g.
5. Keep the ground-truth call: The correct call is the original (non-duplicated) first element of the target call list.
6. Place the repair evidence: After the redundant tool message, insert an assistant message with &lt;reflect&gt; diagnosing the redundancy and a correct &lt;call&gt; (the non-

```
{"status":"redundant","message":"This item duplicates a previous result."}
```

duplicated one), followed by a clean tool response (the original, without the redundant copy).

Notes. This perturbation preserves schema but injects duplication at both call and response sides, creating a realistic 'over-call' pattern for reflection-and-repair.

## How to generate a reflection

## System

You are an AI assistant that analyzes redundant tool calls and provides reflective summaries. Given a tool-call list and its redundant tool response, write a brief reflection that (i) identifies the duplication, and (ii) states the correct next action (use only the necessary call with proper arguments). Keep the reflection concise and actionable.

```
User exactly.
```

```
Fill the placeholders {{...}} Tool call list (after duplication): {{TOOL_CALL_LIST}} Redundant tool response: {{REDUNDANT_RESPONSE}}
```

Please provide a short reflection that points out the redundancy and explains how to proceed correctly.

## An Example

User Tool call list (after duplication): &lt;call&gt;[ {"name":"searchArtistsByArtStyle","arguments":{"style":"impressionism"}}, {"name":"searchArtistsByArtStyle","arguments":{"style":"impressionism"}} ]&lt;/call&gt; Redundant tool response: [ {"tool":"searchArtistsByArtStyle","status":"ok","items":[...]}, {"tool":"searchArtistsByArtStyle","status":"redundant", "message":"This item duplicates a previous result.","items":[...]} ] Please provide a brief reflection on why this redundant call occurred and how to proceed. Keep it concise and helpful.

## 6.3 Prompt for Missing Call

How to construct a missing-call perturbation example

## System

Goal. Remove a necessary assistant &lt;call&gt; and make the subsequent call fail due to missing context, so the agent must recover the omitted call and then proceed correctly.

## Procedure.

1. Extract calls: Parse all assistant-side &lt;call&gt;...&lt;/call&gt; blocks (regex).
2. Select a removable call (not the last): Uniformly sample an index i ∈ { 1 , . . . , |C|1 } .

3. Find paired tool messages: Locate the tool reply immediately after call i (the one to remove), and the tool reply after call i +1 (the 'next' call).
4. Delete call i and its tool reply.
5. Degrade the next call: For the assistant &lt;call&gt; at (original) i +1 , keep the function but set "arguments":{} (empty).
6. Return an error for the next tool: Replace that tool reply with an error JSON indicating 'missing required arguments'.
7. Reflection and repair insertion: After the error tool reply, insert:
6. (a) an assistant message containing &lt;reflect&gt; that explains the omission and a reinstated correct &lt;call&gt; (the removed call i );
7. (b) the original tool reply for the removed call i ;
8. (c) the corrected next assistant call (its original, non-empty arguments);
9. (d) the corrected next tool reply (its original content).

Notes. This perturbation creates a realistic 'missing prerequisite call' failure: the subsequent step cannot execute without information from the omitted call. The reflection must (i) identify the omission and (ii) restore the correct call before proceeding.

## How to generate a reflection

## System

You are an AI assistant that analyzes missing tool calls and provides reflective summaries. Given the omitted call (that should have been executed) and the resulting error response from the next step, write a concise reflection that (i) identifies what was missing, and (ii) states how to proceed: first reinstate the omitted call with correct arguments, then continue.

## User

Fill the placeholders {{...}}

exactly.

```
Missing tool call (the one that should have been made): Error response (from the next step):
```

```
{{MISSING_CALL}} {{ERROR_RESPONSE}}
```

Please provide a short reflection that explains the omission and the corrective sequence of actions.

## An Example

## User

```
Missing tool call: <call>[{"name":"fetchUserProfile","arguments":{"user_id":"u_1293"}}]</call> Error response (from the next step): [ {"status":"error", "message":"Missing required arguments. The function call failed because necessary ↪ → parameters were not provided.", "result": null} ]
```

Please provide a brief reflection on what was missing and how to proceed. Keep it concise and helpful.

## 6.4 Prompt for Argument Error

How to construct an argument-error perturbation example

## System

Goal. Corrupt the arguments of an existing assistant &lt;call&gt; so that the paired tool reply returns a parameter-validation error, forcing the agent to diagnose mismatched/invalid arguments and repair with the correct call.

## Procedure.

1. Extract calls: Parse all assistant-side &lt;call&gt;...&lt;/call&gt; blocks via regex.
2. Select a call: Uniformly sample one index i ∈ { 1 , . . . , |C|} and locate its immediate tool reply.
3. Corrupt arguments: Keep "name" unchanged; replace "arguments" with perturbed values (e.g., wrong types, out-of-range numbers, empty strings, unknown keys). The JSON stays well-formed:

<!-- formula-not-decoded -->

4. Synthesize error reply: Replace the paired tool message with a structured error indicating invalid parameters (e.g., "error\_code":"INVALID\_PARAMETERS" and an informative message).
5. Reflection and repair insertion: Immediately after the error, insert:
3. (a) an assistant message with &lt;reflect&gt; that contrasts the wrong vs. correct arguments and states the fix;
4. (b) the original (correct) call and its original (successful) tool reply.

Notes. Do not alter the function name; only arguments are corrupted. Keep JSON/tags valid to isolate the failure mode to argument errors.

## How to generate a reflection

## System

You are an AI assistant that analyzes incorrect tool-call parameters and provides a reflective summary. Given the correct call, the wrong call (with corrupted arguments), and the error response, write a brief reflection that (i) pinpoints which arguments are incorrect and why, and (ii) states the corrected call. Be concrete and concise.

## User

Fill the placeholders {{...}} exactly. Correct tool call (ground truth): {{CORRECT\_CALL}} Wrong tool call made (arguments corrupted): {{WRONG\_CALL}} Error response: {{ERROR\_RESPONSE}}

Please provide a short reflection that identifies the parameter issues and the corrective action.

## An Example

```
User Correct tool call: <call>[{"name":"bookFlight", "arguments":{"from":"SFO","to":"JFK","date":"2025-11-02","passengers ↪ → ":1}}]</call> Wrong tool call made: <call>[{"name":"bookFlight", "arguments":{"from":999999,"to":"","date":null,"passengers":"many"}}]</call ↪ → > Error response: [{"status":"error", "message":"Parameter validation failed for bookFlight. One or more arguments are ↪ → invalid.", "result": null, "error_code":"INVALID_PARAMETERS"}] Please provide a brief reflection on which parameters are incorrect and how to fix them. Keep it concise and helpful.
```

## 7 Training Data Case Study

In this section, we extract one complete sample from each of the four perturbation modes for analysis.

## 7.1 Case Study of Call-Order Swap

Setup. The user requests end-to-end logistics for a 10-person business trip (NYC → MIA): search and book round-trip flights, search and book hotel rooms, and arrange airport-hotel ground transportation. The toolset exposes search\_flights , book\_flight , search\_hotels , book\_hotel , and arrange\_transportation , where the last function depends on confirmed itinerary details (arrival time and hotel).

Failure trace (order dependency violated). The agent correctly issues two discovery calls, then prematurely attempts to arrange transportation before booking:

```
<tool_call>{"name":"search_flights", ...}</tool_call> <tool_call>{"name":"search_hotels", ...}</tool_call> <tool_call>{"name":"arrange_transportation", "arguments":{"arrival_date":"2023-03-21", "passenger_count":10, "pickup_location":"MIA Airport", "dropoff_location":"(to be decided)"}}</tool_call>
```

The tool returns a dependency error because dropoff\_location cannot be finalized prior to book\_hotel and pickup timing should be aligned to book\_flight :

```
Tool: {"status":"error", "message":"Transportation requires confirmed flight and hotel details.", "result": null}
```

Model reflection and repair. The agent emits a concise &lt;reflect&gt; that identifies the order-dependency (transport must follow booking), then repairs by booking first and only then arranging transport:

```
<reflect>
```

Failure stems from an order dependency: transportation must follow confirmed

```
flight and hotel, as pickup/dropoff depend on the booked itinerary. Correct plan: (1) book flight; (2) book hotel; (3) arrange transportation. </reflect> <tool_call>{"name":"book_flight", ...}</tool_call> <tool_call>{"name":"book_hotel", ...}</tool_call> <tool_call>{"name":"arrange_transportation", "arguments":{"arrival_date":"2023-03-21", "passenger_count":10, "pickup_location":"MIA", "dropoff_location":"Oceanview Hotel"}}</tool_call>
```

Why the reward favors the fix. Under our reward in Sec. §3.2: (i) the reflection receives a high semantic score s ref for explicitly diagnosing the order constraint; (ii) the repaired calls exactly match schema and parameters, yielding s call = 1 (orderinvariant matching across the multiset of calls), while the format/penalty factor F upweights the repaired trajectory because executability and dependency checks now pass; (iii) the premature arrange\_transportation in the failed rollout triggers penalties in F (dependency/executability), so R core is suppressed and the dynamic sampling filter (Sec. §3.3) down-weights such uninformative rollouts. Overall, the corrected plan yields a strictly larger R total than the erroneous one.

Summary. This example shows that explicit reflection converts a latent, long-range constraint ('book before transport') into a short textual diagnosis that the policy can optimize against. Our order-invariant call equality avoids over-penalizing benign output permutations, while dependency-aware format shaping in F enforces inter-call preconditions. Empirically, such cases account for a substantial share of multi-turn gains, whereas single-turn metrics remain stable.

## 7.2 Case Study of Redundant Call

Setup and failure mode. The user requests the list of services for all NYC banks operating for &gt; 10 years. The agent first queries the bank roster with get\_operational\_banks , then correctly queries services for B001 , but erroneously repeats the same service lookup for B001 instead of proceeding to unseen banks ( B002-B004 ). This is a canonical redundant call error: wasted tool budget without adding new information.

```
... <tool_call> {"name":"get_operational_banks", "arguments":{"city":"New York City","min_years_operational":10}} </tool_call> User (tool): [{"name":"get_operational_banks","results":{"banks":[ {"bank_id":"B001"}, {"bank_id":"B002"}, {"bank_id":"B003"}, {"bank_id":"B004"}]}}] <tool_call> {"name":"get_bank_services","arguments":{"bank_id":"B001"}} </tool_call> User (tool): [{"name":"get_bank_services","arguments":{"bank_id":"B001"}, "results":{"services":[...]} }] # Redundant (erroneous) repeat: <tool_call> {"name":"get_bank_services","arguments":{"bank_id":"B001"}} </tool_call> User (tool): [{"name":"get_bank_services","arguments":{"bank_id":"B001"}, "results":{"services":[...], "note":"Duplicate request"}}] ...
```

Reflection and repair (ground truth). The reflection diagnoses duplication and prescribes deduplication; the corrected plan proceeds with the remaining, unseen IDs:

```
<reflect> The last call repeated B001, yielding redundant information. Avoid duplicates; continue with B002, B003, B004 exactly once each. </reflect> <tool_call>{"name":"get_bank_services","arguments":{"bank_id":"B002"}}</tool_call> <tool_call>{"name":"get_bank_services","arguments":{"bank_id":"B003"}}</tool_call> <tool_call>{"name":"get_bank_services","arguments":{"bank_id":"B004"}}</tool_call>
```

Whythe model failed. The failure arises from (i) insufficient state tracking over the set of alreadyseen entities (here, bank IDs), and (ii) weak inductive bias against issuing calls whose marginal information gain is near zero. In multi-turn settings, local myopic policies often re-issue the last successful pattern without cross-step deduplication.

̸

How the reward steers recovery. Our scoring treats call sets as order-invariant but schema-strict; redundant calls trigger the count-mismatch component in the format factor F (penalizing | C calls | = | G calls | ) while EqualCalls fails due to multiset mismatch. The reflection text receives a positive semantic score if it explicitly identifies the duplication and prescribes the missing IDs, encouraging concise, actionable self-correction. Together, the structure score S and format factor F downweight redundant completions and upweight the repaired sequence.

Summary. This case shows that explicit reflection converts a silent efficiency bug into a supervised correction step: the agent (1) cites the duplicated identifier, (2) enumerates the remaining targets, and (3) completes them exactly once. Empirically, such reflection-shaped supervision reduces redundant tool usage and improves multi-turn success without harming single-turn accuracy.

## 7.3 Case Study of Missing Call

Setup. The user asks to register four tax documents: (i) W-2 (ABC Corp), (ii) 1099-INT (First National Bank), (iii) property tax statement (county assessor), and (iv) Form 1098 (mortgage lender). The tool schema exposes a single function add\_tax\_documents(name, value, category, priority) with name,value required.

Baseline failure ( missing calls ). The baseline assistant emits only two &lt;tool\_call&gt; s (W-2, 1099INT) and then stops, yielding a 50% recall on required calls. Formally, let G calls contain the four intended calls and C calls the two produced calls. Then | G calls | = 4 , | C calls | = 2 , and the call-set equality test fails: EqualCalls( C calls , G calls ) = 0 . This is a typical missing-call error in multi-item requests: the model recognizes the pattern 'one item → one call' but truncates the sequence, leaving later items unprocessed.

Structured reflection ( diagnosis ). Our method takes the partially executed trajectory as negative evidence and the original request as positive intent and generates an explicit reflection:

```
<reflect> 'I missed 2 tool call(s). The user listed multiple items, and each item requires a separate call. I should enumerate all items and complete the remaining calls.' </reflect>
```

The reflection correctly localizes the failure (under-counting of required calls), quantifies the deficit (missed = 2 ), and states the repair rule (enumerate all items ⇒ one call per item).

Repairs ( corrective calls ). Conditioned on the reflection, the agent appends the missing tool calls for the remaining items:

- name: Property tax statement; value: county assessor record; category: personal ;
- name: Form 1098; value: mortgage interest statement; category: personal .

The assignments work → W-2,1099-INT and personal → property tax, 1098 are semantically consistent: the former are employment/bank income records; the latter are household liabilities/taxes. (Any schema-compatible categorization would pass executability; ours also preserves natural semantics.)

Why this matters. This case highlights a frequent multi-turn brittleness: once the agent produces a plausible prefix of calls, it prematurely concludes and fails to cover all requested items. By making missingness an explicit, trainable concept, structured reflection converts a sparse binary signal (success/failure) into actionable supervision:

1. Detection: Compare item cardinalities and arguments; compute I [ | C calls | &lt; | G calls | ] and list uncovered entities.
2. Diagnosis: Attribute the error to enumeration/coverage rather than formatting or parameters.
3. Repair: Synthesize the exact missing calls with schema-valid arguments; preserve alreadycorrect calls.

Summary. Empirically, such instances improve the model's coverage discipline : after training, we observe higher multi-item completion rates with negligible increase in redundant calls, indicating that the model learned 'one-mentioned-item ⇒ one-call' as a robust policy rather than overcalling.

## 7.4 Case Study of Argument Error

Setup. The tool schema exposes multiple functions with schema-strict parameters (e.g., check\_plant\_water\_level(plant\_location:string) , start\_watering(plant\_location:string, duration:number) , start\_trimming(hedge\_location:string) , . . . ). The user requests two primary actions in the backyard: (i) trim hedges and (ii) water all potted plants for about 10 minutes; afterwards ensure plants have enough water and dispose clippings.

Baseline failure ( argument error ). The assistant issues

<!-- formula-not-decoded -->

omitting the required key plant\_location . The tool returns a schema warning that the arguments 'did not match expected schema.' Under our reward, the call-level indicator s call is 0 because the produced call fails schema equality (tool name matches, but the argument map does not).

Structured reflection ( diagnosis ). The reflection generated by our process states that the call 'failed because it did not include the required arguments needed by the function's schema,' and prescribes: 'ensure all necessary parameters are provided according to the function's documentation.' This localizes the error to parameter mis-specification (not tool selection or ordering), and points to the concrete fix-satisfy the schema.

Repairs ( efficient plan consistent with the request ). Given the user's 10-minute target and the backyard scope, the corrected action set executes the two core operations with schema-valid arguments:

- start\_watering(plant\_location="backyard", duration=10)
- start\_trimming(hedge\_location="backyard")

These can be dispatched in parallel (independent resources), achieving the requested time budget while ensuring plants receive sufficient water and hedges are trimmed. This replaces the invalid pre-check with a direct, time-bounded watering call that already satisfies the user's constraint.

Why this matters. Argument errors are common in tool use and typically yield sparse feedback ('schema mismatch'). By forcing the model to (i) recognize the missing required field and (ii) restate the schema-conformant fix, the reflection step converts a low-information error into actionable supervision. In our benchmark, such instances consistently improve:

1. Schema adherence: higher exact-match rate on name / arguments .
2. Planning under constraints: selection of parameterized calls ( duration=10 ) aligned with user constraints instead of brittle pre-checks with empty arguments.
3. Stability: fewer retries and warnings downstream because calls are executable on the first attempt.

Summary. This case illustrates how reflection-guided repair turns a malformed &lt;call&gt; into a compact, correct, and time-efficient action plan.

## 8 Test Data Case Study

In this section, we present two representative test cases and their corresponding evaluation results as a case study, providing an intuitive demonstration of the effectiveness of our method and the model's self-reflection capability for tool-call repair.

## 8.1 Case I

Setting. The tool set exposes three functions: getRecipes(max\_time, meal\_type) , getSmoothieIngredients(max\_time) , and findComplementaryRecipes(recipes, ingredients) . The user asks for breakfast recipes under 15 minutes and smoothie pairings under 5 minutes.

```
Failure mode (pre-training). The baseline model immediately issues [{"name":"findComplementaryRecipes","parameters":{}}]
```

which violates the function schema (both recipes and ingredients are required). The tool returns a schema-warning. Under our reward, this yields s call = 0 and triggers format penalties F &lt; 1 due to missing required parameters.

Reflection-driven repair (post-training). After RL on Tool-Reflection-Bench, the model first reflects that the failure arises from absent inputs, then correctly decomposes the task into produce inputs → compose :

```
[{"name":"getRecipes","parameters":{"max_time":15,"meal_type":"breakfast"}}] [{"name":"getSmoothieIngredients","parameters":{"max_time":5}}] [{"name":"findComplementaryRecipes", "parameters":{"recipes": <from getRecipes>, "ingredients": <from ↪ → getSmoothieIngredients>}}]
```

This satisfies the schema strictly (tool names and parameter maps match), making the call set correct and executable.

Why our method helps. (i) Reward shaping: The instance accrues a hard penalty when required fields are absent; after repair, s call flips to 1 and F → 1 , raising R core = S · F substantially. (ii) Sequence-level RL: The GSPO-style sequence-ratio with dual clipping aligns the optimization granularity with the sequence reward, while DAPO-style dynamic filtering removes near-zeroadvantage rollouts (all-wrong/all-correct), sharpening learning signals for this failure mode.

Takeaway. Compared to the baseline that jumps to composition with empty inputs, the trained policy learns to (a) diagnose the schema error, (b) stage prerequisite calls to produce the missing inputs, and (c) complete the composition with a schema-valid call set. This precisely matches our benchmark's objective: enable robust, multi-turn tool use via reflection and repair.

## 8.2 Case II

Setting. Available tools include get\_current\_season() , get\_seeds\_by\_season(season) , filter\_seeds\_by\_availability(seeds) , purchase\_seeds(seeds, quantity) , and calculate\_total\_cost(purchased\_seeds) . The user asks to buy 10 packets of seasonal vegetable seeds and report the total cost .

Failure mode (pre-training). The baseline calls the aggregator first, with no inputs:

```
[{"name":"calculate_total_cost","parameters":{}}]
```

This violates the required schema ( purchased\_seeds missing), producing a warning and yielding s call = 0 and a strong format penalty F &lt; 1 in our reward.

Reflection-driven repair (post-training). After RL on Tool-Reflection-Bench, the model first reflects that costing requires purchased items, then executes a staged pipeline to materialize prerequisites before aggregation:

```
[{"name":"get_current_season","parameters":{}}] [{"name":"get_seeds_by_season","parameters":{"season":"<CUR_SEASON>"}}] [{"name":"filter_seeds_by_availability","parameters":{"seeds":<SEASONAL_SEEDS>}}] [{"name":"purchase_seeds","parameters":{"seeds":<AVAILABLE_SEEDS>,"quantity":10}}] [{"name":"calculate_total_cost","parameters":{"purchased_seeds":<PURCHASED>}}]
```

Each call now matches tool name and parameter map exactly (schema-strict), so s call = 1 and F → 1 .

Why it works. Reward design penalizes missing required fields and redundant structure, while granting full credit only when the &lt;call&gt; set exactly matches the ground truth (schema-strict, order-invariant). The sequence-level RL objective (GSPO-style ratio, dual clipping) aligns optimization with sequence rewards, and DAPO-style dynamic filtering removes near-zero-advantage groups, concentrating updates on informative failures. Together these guide the policy to diagnose schema errors, stage prerequisite calls, and complete the costing correctly.

Takeaway. The trained policy no longer 'guesses' totals from empty inputs. Instead, it plans → acquires data → purchases → aggregates , a behavior precisely targeted by our reflection-and-repair rewards.

## 9 Theoretical Analysis

We analyze the main design choices of our reward in Sec. §3.2 and the RL objective in Sec. §3.3. Throughout, Sim ∈ [0 , 1] , all weights are nonnegative, presence masks are indicators, and clip( x, a, b ) = min { b, max { a, x }} . To avoid symbol overloading, we denote by r fmt the formatpenalty attenuation scalar used in Sec. §3.2 (called r there), and by r seq the sequence-level importance ratio in Sec. §3.3.

## 9.1 Consistency of Presence-Mask Normalization

Recall

<!-- formula-not-decoded -->

where w · ≥ 0 , I · ∈ { 0 , 1 } , at least one I · = 1 , s ref , s fi nal ∈ [0 , 1] , and s call ∈ { 0 , 1 } .

Lemma 1 (Convex-combination form). Let A = { k ∈ { r,c,f } : I k = 1 } and define

<!-- formula-not-decoded -->

Then α k ≥ 0 , ∑ k ∈A α k = 1 , and

<!-- formula-not-decoded -->

Proof. Since I k = 1 iff k ∈ A , the numerator equals ∑ k ∈A w k s k and W act = ∑ k ∈A w k &gt; 0 . Divide both to obtain the stated form.

## Proposition 1 (Boundedness, stability, and scale invariance). With W act &gt; 0 :

- (a) S ∈ [0 , 1] and, more sharply, S ∈ [min k ∈A s k , max k ∈A s k ] .
- (b) If one only toggles absent parts (keeps A and { w k } k ∈A unchanged), then S is unchanged.
- (c) For any λ &gt; 0 , replacing each active weight by λw k leaves S unchanged.

Proof. (a) By Lemma 1, S is a convex combination of { s k } k ∈A ; the interval bound follows from s k ∈ [0 , 1] . (b) Absent-part toggles do not change A nor the active w k . (c) Common scaling cancels in numerator/denominator.

Corollary 1 (Continuity and Lipschitzness). Fix A and w k for k ∈ A . Then S is an affine (hence continuous) map of ( s k ) k ∈A with

<!-- formula-not-decoded -->

so S is 1 -Lipschitz w.r.t. the ℓ ∞ -norm on the active scores.

Remark. The definition via clip [0 , 1] ( · ) in (25) is not needed for S since the convex-combination form already implies S ∈ [0 , 1] .

## 9.2 Format Factor: Boundedness, Monotonicity, and EqualCalls Attenuation

Let

<!-- formula-not-decoded -->

and define the attenuation scalar

<!-- formula-not-decoded -->

Consider

<!-- formula-not-decoded -->

This is equivalent to the piecewise definition in (23) since P miss = P extra = P count =0 implies the inner value equals 1 .

## Proposition 2 (Core properties of F ).

- (a) Boundedness and regularity. F ∈ [0 , 1] for all inputs; F is continuous, piecewise affine in ( P miss , P extra , P count ) and 1 -Lipschitz w.r.t. its scalar argument before clipping.
- (b) Monotonicity. For fixed ( λ m , r fmt ) , F is nonincreasing in P miss , P extra , P count and nonincreasing in λ m and in r fmt .
- (c) EqualCalls attenuation improves F . If EqualCalls holds so that r fmt is replaced by r reduce ≤ 1 , then F weakly increases.
- (d) Plateau characterization. F = 1 iff λ m P total r fmt = 0 (e.g., P total = 0 or λ m = 0 ). If λ m &gt; 0 and r fmt &gt; 0 , then F = 0 iff P total ≥ 1 / ( λ m r fmt ) .

Corollary 2 (Sensitivity bound). Off the plateaus ( 1 -λ m P total r fmt ∈ (0 , 1) ),

<!-- formula-not-decoded -->

## 9.3 Core Reward with Similarity Backoff: Signal and Variance Control

Let R core = S · F as in (24). The total reward uses a backoff when R core is very small:

<!-- formula-not-decoded -->

with w b ∈ (0 , 1] and ε &gt; 0 . Note R core ∈ [0 , 1] already, hence clipping is redundant but harmless and keeps the two branches notationally symmetric.

We analyze its effect under a standard policy-gradient estimator ∇ θ E [ R total ] = E [ R total ∇ θ log π θ ( · )] .

Lemma 3 (Uniform bounded variance of the reward). Since R total ∈ [0 , 1] , we have Var( R total ) ≤ 1 4 for any data distribution.

Lemma 4 (Non-degenerate gradient second moment on the backoff branch). Let B = { R core &lt; ε } with P ( B ) = p &gt; 0 . Assume Sim(concat( C ) , concat( G )) ≥ σ a.s. on B for some σ &gt; 0 , and E [ ∥∇ θ log π θ ( · ) ∥ 2 1 B ] &gt; 0 . Then

<!-- formula-not-decoded -->

Implication. When R core requently approaches 0 (in the early stages of training), the backoff branch ensures that the second moment of the gradients does not degenerate; combined with the variance upper bound from Lemma 3, this helps stabilize the optimization updates.

## 9.4 Sequence-Level Importance Sampling and Clipping

Let the sampled completion be o = ( o 1 , . . . , o T ) , and define the sequence-level (geometric-mean, length-normalized) ratio

<!-- formula-not-decoded -->

Proposition 3 (Length-independent ratio range under bounded log-ratios). If log ρ t ∈ [ -L, L ] a.s. for some L &gt; 0 , then

<!-- formula-not-decoded -->

whereas the unnormalized product ratio ranges in [ e -LT , e LT ] .

Implication. The geometric mean aligns the ratio granularity with the sequence-level reward in (26), prevents exponential blow-up with T , and-together with dual clipping-reduces variance at the sequence level.

## 9.5 Dynamic Filtering of Prompt Groups (DAPO-style)

Let a prompt group produce G rollouts { o i } G i =1 with rewards R i ∈ [0 , 1] and batch z -scored advantages

<!-- formula-not-decoded -->

Define the accepted set

<!-- formula-not-decoded -->

Write the per-sample (sequence-level, dual-clipped) PPO-like term as

<!-- formula-not-decoded -->

and denote its gradient by g i ( θ ) = ∇ θ ℓ i ( θ ) . Assume the usual score-function bound and clipped ratio range:

<!-- formula-not-decoded -->

Auniform bound on per-rollout gradients. Since r seq ,i ( θ ) is the geometric mean of token ratios,

<!-- formula-not-decoded -->

Using ( ⋆ ) and that the clipped branch is constant on plateaus, there exists a finite C ψ = (1+ ε high ) B π such that

<!-- formula-not-decoded -->

Lemma 5 (Zero or near-zero advantages). (a) If ˆ A i = 0 , removing o i leaves the group-wise expected gradient unchanged.

(b) If | ˆ A i | ≤ τ adv, then, for any θ ,

<!-- formula-not-decoded -->

Proof. (a) The contribution is proportional to ˆ A i . (b) Apply (46) and take expectations.

Bias and variance effects with 1 G normalization. Let the fi ltered group gradient be

<!-- formula-not-decoded -->

Define the discarded set S c = { 1 , . . . , G } \ S . Then

<!-- formula-not-decoded -->

<!-- formula-not-decoded -->

using Lemma 5(b). Moreover,

<!-- formula-not-decoded -->

thus, discarding near-zero advantageous terms induces at most an O ( τ 2 adv ) -level change in the second moment; with respect to the 1 G normalization, it does not introduce any additional scaling bias.

Acceptance constraints avoid degeneracy. The constraints 0 &lt; |S| &lt; G and Var( { R i } ) &gt; τ var ensure: (i) the batch standardization s R is well-defined; (ii) both positive and negative (or at least non-identical) signals are present, preventing the trivial zero-gradient case where all ˆ A i are identical. Consequently, ˜ g ( θ ) is a non-degenerate direction whenever useful learning signal exists.

Asymptotic unbiasedness with vanishing threshold. If the threshold decays τ ( t ) adv ↓ 0 and the law of ˆ A i has a continuous density at 0 , then the discard probability P ( | ˆ A i | ≤ τ ( t ) adv ) → 0 , and

<!-- formula-not-decoded -->

i.e., the dynamic filtering becomes asymptotically unbiased while retaining finite-time variancereduction benefits.

Summary. Dynamic filtering deletes rollouts whose contributions are provably negligible (zero or O ( τ adv ) ), thereby reducing variance and compute without altering the expected update in the limit τ adv → 0 ; using the same 1 /G normalization as (26) avoids spurious scaling bias.

## 9.6 Convergence Considerations for the Clipped Sequence-Level Objective

Consider the surrogate objective J RL ( θ ) in (26), where rewards are bounded in [0 , 1] and the sequence-level importance ratios are dual-clipped to [ 1 -ε low , 1 + ε high ] .

## Assumptions.

- (A1) Bounded scores. There exists B π &lt; ∞ such that for all histories ( q, o &lt;t ) and tokens o t , ∥ ∥ ∇ θ log π θ ( o t | q, o &lt;t ) ∥ ∥ ≤ B π .
- (A2) Bounded rewards &amp; finite clipping. For each rollout o i , R i ∈ [0 , 1] and r seq ,i ( θ ) ∈ [ 1 -ε low , 1 + ε high ] with 0 &lt; ε low , ε high &lt; ∞ .

- (A3) Non-degenerate batch dispersion. On accepted groups, Var( { R i } G i =1 ) ≥ τ var &gt; 0 , so ˆ A i = ( R i -¯ R ) / std( R ) are well-defined.
- (A4) Vanishing filtering. τ ( t ) adv ↓ 0 and the law of ˆ A i has a continuous density at 0 , so P ( | ˆ A i | ≤ τ ( t ) adv ) → 0 .
- (A5) Stepsizes. Robbins-Monro conditions: ∑ t η t = ∞ and ∑ t η 2 t &lt; ∞ .

Lemma 6 (Bounds on per-sample gradients and second moments). Let o = ( o 1 , . . . , o | o | ) and r seq ( θ ) denote the (clipped) sequence ratio. Then

<!-- formula-not-decoded -->

Moreover, the PPO-style term is piecewise smooth and its gradient magnitude is bounded by C 1 := (1 + ε high ) B π | ˆ A | ; together with (A3), | ˆ A | ≤ 1 √ τ var yields a uniform second-moment bound E [ ∥∇ θ ℓ i ( θ ) ∥ 2 ] ≤ C 2 &lt; ∞ .

Lemma 7 (Asymptotic unbiasedness under vanishing filtering). Let g ( θ ) denote the full (unfiltered) stochastic gradient and ˜ g τ ( θ ) = 1 G ∑ i : | ˆ A i | &gt;τ g i ( θ ) the filtered version with 1 G normalization. Under (A4) and the bounded second moments above,

<!-- formula-not-decoded -->

Theorem 1 (Convergence to a stationary point of the surrogate). Suppose (A1)-(A5) hold. Then the iterates of stochastic gradient ascent on J RL ( θ ) with the dynamic filtering scheme converge almost surely to the set of stationary points of the surrogate objective.

Proof sketch. By Lemma 6 and the reward boundedness (Lemma 3), the stochastic gradients have uniformly bounded second moments; the objective is bounded and piecewise smooth (kinks of measure zero). Lemma 7 guarantees that the bias due to filtering vanishes as τ ( t ) adv → 0 . Therefore the noisy gradient process forms a Robbins-Monro stochastic approximation with asymptotically unbiased gradients and square-summable noise, yielding a.s. convergence to stationary points of J RL (e.g., Kushner-Yin/Bottou).

Remarks. (i) The min-with-clipping introduces bias w.r.t. the true off-policy objective, but ensures variance control and stability; the theorem concerns the surrogate we optimize. (ii) Sequence-level ratios and sequence-level clipping align the gradient scale with the sequence reward, avoiding token/sequence granularity mismatch and contributing to the boundedness needed above. (iii) In practice, we keep τ var and the clip window fixed and decay τ adv, which satisfies the lemmas' conditions and matches our training protocol.

## References

- [1] MAOLIN WANG, YINGYI ZHANG, CUNYIN PENG, YICHENG CHEN, WEI ZHOU, JINJIE GU, CHENYI ZHUANG, RUOCHENG GUO, BOWEN YU, WANYU WANG, et al. Function calling in large language models: Industrial practices, challenges, and future directions. 2025.
- [2] Changle Qu, Sunhao Dai, Xiaochi Wei, Hengyi Cai, Shuaiqiang Wang, Dawei Yin, Jun Xu, and J Wen. Tool learning with large language models: A survey. corr abs/2405.17935(2024). arXiv preprint arXiv:2405.17935 , 2024.
- [3] Shijue Huang, Wanjun Zhong, Jianqiao Lu, Qi Zhu, Jiahui Gao, Weiwen Liu, Yutai Hou, Xingshan Zeng, Yasheng Wang, Lifeng Shang, et al. Planning, creation, usage: Benchmarking llms for comprehensive tool utilization in real-world complex scenarios. arXiv preprint arXiv:2401.17167 , 2024.

- [4] Yujia Qin, Shihao Liang, Yining Ye, Kunlun Zhu, Lan Yan, Yaxi Lu, Yankai Lin, Xin Cong, Xiangru Tang, Bill Qian, et al. Toolllm: Facilitating large language models to master 16000+ real-world apis. arXiv preprint arXiv:2307.16789 , 2023.
- [5] Changle Qu, Sunhao Dai, Xiaochi Wei, Hengyi Cai, Shuaiqiang Wang, Dawei Yin, Jun Xu, and J Wen. Tool learning with large language models: A survey. corr abs/2405.17935(2024). arXiv preprint arXiv:2405.17935 , 2024.
- [6] Ruizhe Zhong, Xingbo Du, Shixiong Kai, Zhentao Tang, Siyuan Xu, Hui-Ling Zhen, Jianye Hao, Qiang Xu, Mingxuan Yuan, and Junchi Yan. Llm4eda: Emerging progress in large language models for electronic design automation. arXiv preprint arXiv:2401.12224 , 2023.
- [7] Adrian Theuma and Ehsan Shareghi. Equipping language models with tool use capability for tabular data analysis in finance. arXiv preprint arXiv:2401.15328 , 2024.
- [8] Yilun Hao, Yongchao Chen, Yang Zhang, and Chuchu Fan. Large language models can plan your travels rigorously with formal verification tools. CoRR , 2024.
- [9] Hardy Chen, Haoqin Tu, Fali Wang, Hui Liu, Xianfeng Tang, Xinya Du, Yuyin Zhou, and Cihang Xie. Sft or rl? an early investigation into training r1-like reasoning large visionlanguage models. arXiv preprint arXiv:2504.11468 , 2025.
- [10] Cheng Qian, Emre Can Acikgoz, Qi He, Hongru Wang, Xiusi Chen, Dilek Hakkani-Tür, Gokhan Tur, and Heng Ji. Toolrl: Reward is all tool learning needs. arXiv preprint arXiv:2504.13958 , 2025.
- [11] Barrett Martin Lattimer, Varun Gangal, Ryan McDonald, and Yi Yang. Sparse rewards can self-train dialogue agents. arXiv preprint arXiv:2409.04617 , 2024.
- [12] Xuefeng Li, Haoyang Zou, and Pengfei Liu. Torl: Scaling tool-integrated rl. arXiv preprint arXiv:2503.23383 , 2025.
- [13] Junjie Ye, Yilong Wu, Sixian Li, Yuming Yang, Tao Gui, Qi Zhang, Xuanjing Huang, Peng Wang, Zhongchao Shi, Jianping Fan, et al. Tl-training: A task-feature-based framework for training large language models in tool use. arXiv preprint arXiv:2412.15495 , 2024.
- [14] Ling Yang, Zhaochen Yu, Tianjun Zhang, Minkai Xu, Joseph E Gonzalez, Bin Cui, and Shuicheng Yan. Supercorrect: Supervising and correcting language models with error-driven insights. arXiv preprint arXiv:2410.09008 , 9, 2024.
- [15] Mingyang Chen, Haoze Sun, Tianpeng Li, Fan Yang, Hao Liang, Keer Lu, Bin Cui, Wentao Zhang, Zenan Zhou, and Weipeng Chen. Facilitating multi-turn function calling for llms via compositional instruction tuning. arXiv preprint arXiv:2410.12952 , 2024.
- [16] Weiwen Liu, Xu Huang, Xingshan Zeng, Xinlong Hao, Shuai Yu, Dexun Li, Shuai Wang, Weinan Gan, Zhengying Liu, Yuanqing Yu, et al. Toolace: Winning the points of llm function calling. arXiv preprint arXiv:2409.00920 , 2024.
- [17] Shishir G Patil, Huanzhi Mao, Fanjia Yan, Charlie Cheng-Jie Ji, Vishnu Suresh, Ion Stoica, and Joseph E Gonzalez. The berkeley function calling leaderboard (bfcl): From tool use to agentic evaluation of large language models. In Forty-second International Conference on Machine Learning .
- [18] Jianguo Zhang, Tian Lan, Ming Zhu, Zuxin Liu, Thai Hoang, Shirley Kokane, Weiran Yao, Juntao Tan, Akshara Prabhakar, Haolin Chen, et al. xlam: A family of large action models to empower ai agent systems. arXiv preprint arXiv:2409.03215 , 2024.
- [19] Bingguang Hao, Maolin Wang, Zengzhuang Xu, Cunyin Peng, Yicheng Chen, Xiangyu Zhao, Jinjie Gu, and Chenyi Zhuang. Funreason: Enhancing large language models' function calling via self-refinement multiscale loss and automated data refinement. arXiv preprint arXiv:2505.20192 , 2025.
- [20] Minghao Li, Yingxiu Zhao, Bowen Yu, Feifan Song, Hangyu Li, Haiyang Yu, Zhoujun Li, Fei Huang, and Yongbin Li. Api-bank: A comprehensive benchmark for tool-augmented llms. arXiv preprint arXiv:2304.08244 , 2023.

- [21] Chen Chen, Xinlong Hao, Weiwen Liu, Xu Huang, Xingshan Zeng, Shuai Yu, Dexun Li, Shuai Wang, Weinan Gan, Yuefeng Huang, et al. Acebench: Who wins the match point in tool learning? arXiv e-prints , pages arXiv-2501, 2025.
- [22] Jie Huang, Xinyun Chen, Swaroop Mishra, Huaixiu Steven Zheng, Adams Wei Yu, Xinying Song, and Denny Zhou. Large language models cannot self-correct reasoning yet. arXiv preprint arXiv:2310.01798 , 2023.
- [23] Fengyuan Liu, Nouar AlDahoul, Gregory Eady, Yasir Zaki, and Talal Rahwan. Self-reflection makes large language models safer, less biased, and ideologically neutral. arXiv preprint arXiv:2406.10400 , 2024.
- [24] Matthew Renze and Erhan Guven. Self-reflection in llm agents: Effects on problem-solving performance. arXiv preprint arXiv:2405.06682 , 2024.
- [25] Aman Madaan, Niket Tandon, Prakhar Gupta, Skyler Hallinan, Luyu Gao, Sarah Wiegreffe, Uri Alon, Nouha Dziri, Shrimai Prabhumoye, Yiming Yang, et al. Self-refine: Iterative refinement with self-feedback. Advances in Neural Information Processing Systems , 36:4653446594, 2023.
- [26] Zhenyu Wu, Qingkai Zeng, Zhihan Zhang, Zhaoxuan Tan, Chao Shen, and Meng Jiang. Large language models can self-correct with key condition verification. arXiv preprint arXiv:2405.14092 , 2024.
- [27] Juraj Vladika, Ihsan Soydemir, and Florian Matthes. Correcting hallucinations in news summaries: Exploration of self-correcting llm methods with external knowledge. arXiv preprint arXiv:2506.19607 , 2025.
- [28] Yuhua Jiang, Yuwen Xiong, Yufeng Yuan, Chao Xin, Wenyuan Xu, Yu Yue, Qianchuan Zhao, and Lin Yan. Pag: Multi-turn reinforced llm self-correction with policy as generative verifier. arXiv preprint arXiv:2506.10406 , 2025.
- [29] Xutong Zhao, Tengyu Xu, Xuewei Wang, Zhengxing Chen, Di Jin, Liang Tan, Zishun Yu, Zhuokai Zhao, Yun He, Sinong Wang, et al. Boosting llm reasoning via spontaneous selfcorrection. arXiv preprint arXiv:2506.06923 , 2025.
- [30] RI Saveliev and MV Dendiuk. Self-reflective retrieval-augmented generation (self-rag) in analytical systems. In Forestry Education and Science: Current Challenges and Development Prospects. International Science-Practical Conference, October 23-25, 2024, Lviv, Ukraine , 2024.
- [31] Jiazhan Feng, Shijue Huang, Xingwei Qu, Ge Zhang, Yujia Qin, Baoquan Zhong, Chengquan Jiang, Jinxin Chi, and Wanjun Zhong. Retool: Reinforcement learning for strategic tool use in llms. arXiv preprint arXiv:2504.11536 , 2025.
- [32] Qiying Yu, Zheng Zhang, Ruofei Zhu, Yufeng Yuan, Xiaochen Zuo, Yu Yue, Weinan Dai, Tiantian Fan, Gaohong Liu, Lingjun Liu, et al. Dapo: An open-source llm reinforcement learning system at scale. arXiv preprint arXiv:2503.14476 , 2025.
- [33] Chujie Zheng, Shixuan Liu, Mingze Li, Xiong-Hui Chen, Bowen Yu, Chang Gao, Kai Dang, Yuqiong Liu, Rui Men, An Yang, et al. Group sequence policy optimization. arXiv preprint arXiv:2507.18071 , 2025.
- [34] Yuze Zhao, Jintao Huang, Jinghan Hu, Xingjun Wang, Yunlin Mao, Daoze Zhang, Zeyinzi Jiang, Zhikai Wu, Baole Ai, Ang Wang, et al. Swift: a scalable lightweight infrastructure for fine-tuning. In Proceedings of the AAAI Conference on Artificial Intelligence , volume 39, pages 29733-29735, 2025.
- [35] Abhimanyu Dubey, Abhinav Jauhri, Abhinav Pandey, Abhishek Kadian, Ahmad Al-Dahle, Aiesha Letman, Akhil Mathur, Alan Schelten, Amy Yang, Angela Fan, et al. The llama 3 herd of models. arXiv e-prints , pages arXiv-2407, 2024.

- [36] Binyuan Hui, Jian Yang, Zeyu Cui, Jiaxi Yang, Dayiheng Liu, Lei Zhang, Tianyu Liu, Jiajun Zhang, Bowen Yu, Keming Lu, et al. Qwen2. 5-coder technical report. arXiv preprint arXiv:2409.12186 , 2024.
- [37] An Yang, Anfeng Li, Baosong Yang, Beichen Zhang, Binyuan Hui, Bo Zheng, Bowen Yu, Chang Gao, Chengen Huang, Chenxu Lv, et al. Qwen3 technical report. arXiv preprint arXiv:2505.09388 , 2025.
- [38] Meituan LongCat Team, Bei Li, Bingye Lei, Bo Wang, Bolin Rong, Chao Wang, Chao Zhang, Chen Gao, Chen Zhang, Cheng Sun, et al. Longcat-flash technical report. arXiv preprint arXiv:2509.01322 , 2025.
- [39] OpenAI. Hello gpt-4o. https://openai.com/index/hello-gpt-4o/ , May 2024. Accessed: 2025-09-25.
- [40] OpenAI. Gpt-4o system card, 2024. Accessed: 2025-09-25.
- [41] OpenAI. Introducing gpt-4.1 in the api. https://openai.com/index/gpt-4-1/ , April 2025. Accessed: 2025-09-25.