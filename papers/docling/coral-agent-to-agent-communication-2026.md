## Beyond Rule-Based Workflows: An Information-Flow-Orchestrated Multi-Agents Paradigm via Agent-to-Agent Communication from CORAL

Xinxing Ren 1 , 2 , ∗ Quagmire Zang 3 , ∗ Caelum Forder 1 , ∗ Suman Deb 1 , ∗ Ahsen Tahir 1 , 5 , ∗ Roman J. Georgio 1 Peter Carroll 1 Zekun Guo 4 , †

1 Coral Protocol 2 Brunel University of London 3 Universit´ eit L¨ etzebuerg

4 5

University of Hull National University of Computer and Emerging Sciences

∗

Equal contribution

## Abstract

Most existing Large Language Model (LLM)-based Multi-Agent Systems (MAS) rely on predefined workflows, where human engineers enumerate task states in advance and specify routing rules and contextual injections accordingly. Such workflow-driven designs are essentially rule-based decision trees, which suffer from two fundamental limitations: they require substantial manual effort to anticipate and encode possible task states, and they cannot exhaustively cover the state space of complex real-world tasks. To address these issues, we propose an Information-FlowOrchestrated Multi-Agent Paradigm via Agent-toAgent (A2A) Communication from CORAL, in which a dedicated information flow orchestrator continuously monitors task progress and dynamically coordinates other agents through the A2A toolkit using natural language, without relying on predefined workflows. We evaluate our approach on the general-purpose benchmark GAIA, using the representative workflow-based MAS OWL as the baseline while controlling for agent roles and underlying models. Under the pass@1 setting, our method achieves 63.64% accuracy, outperforming OWL's 55.15% by 8.49 percentage points with comparable token consumption. Further case-level analysis shows that our paradigm enables more flexible task monitoring and more robust handling of edge cases. Our implementation is publicly available at: https://github.com/Coral-Protocol/ Beyond-Rule-Based-Workflows.

## 1 Introduction

Recent advances in LLMs have enabled the development of intelligent agents capable of performing complex reasoning and decision-making tasks. Following the Agentic Benchmark Checklist proposed in [Zhu et al. , 2025], agentic tasks are characterized by: (i) sustained multi-step interactions with an external environment, (ii) iterative information gathering under partial observability, and (iii) adaptive strategy refinement based on environmental feedback. Such agentic systems have achieved remarkable performance in a wide range

† Corresponding author of applications, including code generation [Hong et al. , 2023; Yang et al. , 2024], web browsing [Wei et al. , 2025], finance [Yu et al. , 2025], and scientific discovery [Su et al. , 2025]. As task complexity increases, many agentic problems naturally require diverse expertise and coordinated decision making, which has motivated a growing research focus on llmbased MAS [Guo et al. , 2024]. Collaborative multi-agent approaches such as OWL [Hu et al. , 2025] and MetaGPT [Hong et al. , 2023] have demonstrated that coordinated MAS can outperform single-agent systems on complex, generalpurpose tasks requiring heterogeneous skill sets as well as on challenging code generation problems.

Most existing MAS are constructed using predefined workflows, as exemplified by OWL [Hu et al. , 2025], MetaGPT [Hong et al. , 2023], and AutoAgent [Tang et al. , 2025]. Fundamentally, a workflow can be viewed as a rule-based decision tree, where human engineers predefine discrete task states and specify routing policies and contextual injections conditioned on these states. While effective for well-scoped tasks, this paradigm suffers from inherent limitations: (1) it requires substantial manual effort to anticipate task states and design corresponding routing logic; and (2) for complex realworld tasks, it is theoretically infeasible to exhaustively enumerate all possible states in advance. As a result, workflowbased supervision often struggles to reliably monitor task execution and handle unforeseen edge cases.

Figure 1 illustrates the representative workflow-based MAS design of OWL. The upper part shows a decisionmaking tree representation, where tasks are decomposed into stateful subtasks and iteratively routed to worker agents, with the entire task re-decomposed upon subtask failure. While this design provides a clear control flow, it relies on a predefined set of task states and routing rules that must be manually specified by human engineers. The lower part presents a representative failure case: a web agent retrieves all U.S. Survivor winners but fails to obtain birth dates for some of them while still marking the subtask as successful, causing subsequent subtasks to proceed on incomplete information and leading to an incorrect final answer. This partial fulfillment makes it difficult for the agent to determine whether the subtask should be considered a success or a failure, highlighting a core limitation of workflow-based supervision, that a limited set of predefined states is insufficient to monitor the full

Figure 1: Decision-making tree representation of the OWL architecture.

<!-- image -->

task execution process, while exhaustively anticipating and encoding edge cases remains infeasible for human engineers.

Motivated by these observations, we ask whether it is possible to transfer the responsibility of constructing and supervising workflows from human engineers to the agents themselves. A similar shift has occurred in autonomous driving [Chen et al. , 2024], moving from handcrafted state machines to end-to-end perception-driven control [Prakash et al. , 2021; Hu et al. , 2023], motivating a corresponding transition toward agent-driven coordination in MAS. To realize this vision, we propose an Information-Flow-Orchestrated Multi-Agent Paradigm via A2A Communication Toolkit . As illustrated in Figure 2, a dedicated information flow orchestrator continuously monitors task progress and dynamically coordinates other agents through the A2A communication toolkit from CORAL using natural language, without relying on predefined workflows.

To validate the effectiveness of our proposed paradigm, we evaluate it on the general-purpose benchmark GAIA [Mialon et al. , 2024], using the classical workflow-based multi-agent system OWL as the baseline. For a fair comparison, both systems employ identical agent roles and the same underlying large language models. Under the pass@1 evaluation setting, our method achieves an accuracy of 63.64%, outperforming OWL's 55.15% by 8.49 percentage points with comparable token consumption. Further case-level analysis shows that our paradigm enables more flexible task monitoring and more robust handling of edge cases.

Our contributions can be summarized as follows:

- We propose an Information-Flow-Orchestrated Multi-Agent Paradigm via A2A Communication , which shifts workflow construction and supervision from human-designed state machines to agent-driven supervision and coordination.
- Under a controlled experimental setting where both sys-

tems employ identical agent roles and the same underlying language models, our approach achieves 63.64% accuracy on the GAIA benchmark (pass@1), outperforming the classical workflow-based MAS OWL ( 55.15% ) by 8.49 percentage points with comparable token consumption.

- Through detailed case-level analysis, we demonstrate that our paradigm enables more flexible task monitoring and exhibits greater robustness to edge cases that are difficult to handle under workflow-based MAS.

## 2 Related Work and Preliminaries

Multi-Agent Systems. Existing MAS can be broadly divided into domain-specific and general-purpose categories. Domain-specific MAS tailor agent collaboration to particular tasks, such as software engineering (e.g., MetaGPT [Hong et al. , 2023], SWE-Search [Antoniades et al. , 2025]), data analysis (AutoKaggle [Li et al. , 2024]), engineering simulation (SimuGen [Ren et al. , 2025]), and scientific idea generation (VIRSCI [Su et al. , 2025]), leveraging strong domain priors and specialized workflows. In contrast, general-purpose MAS, also referred to as generalist agents and formalized by the GAIA benchmark [Mialon et al. , 2024], aim to solve open-ended tasks across domains. Representative systems include OpenAI's Deep Research, OWL [Hu et al. , 2025], and no-code platforms such as AutoAgent [Tang et al. , 2025]. Due to their higher variability and unpredictability, generalpurpose tasks make it difficult to predefine exhaustive task states, motivating our focus on this setting.

Dynamic Multi-Agent Systems. As shown in Table 1, recent works have explored the design of topological scaffolds to coordinate groups of agents. GTPSwarm [Zhuge et al. , 2024] formulates agent collaboration as a learnable graph structure, MasRouter [Yue et al. , 2025] learns embedding spaces to map queries to agents and interaction topologies, and Conductor [Anonymous, 2025] trains a dedicated conductor to jointly perform task decomposition and agent routing. However, these approaches typically determine the MAS topology and routing policies prior to task execution, which limits their ability to monitor emergent edge cases and adapt dynamically during task progression. Puppeteer [Dang et al. , 2025] performs step-wise agent routing at runtime. Nevertheless, due to the lack of explicit natural-language instructions to routed agents-relying instead on concatenating previous agents' outputs into the next agent's context-its robustness remains limited. In contrast, our proposed A2Abased paradigm differs fundamentally from prior work. At each step of task execution, the information flow orchestrator actively monitors task progress and issues explicit, stepspecific inquiries or instructions to subsequent agents via the A2A communication toolkit, enabling fine-grained coordination and adaptive handling of emergent edge cases.

Figure 2: Overview of the proposed Information-Flow-Orchestrated Multi-Agent Paradigm via Agent-to-Agent (A2A) Communication. A dedicated information flow orchestrator monitors task progress and dynamically coordinates agents through natural-language A2A interactions, eliminating the need for predefined workflows.

<!-- image -->

<!-- image -->

Table 1: Comparison of dynamic multi-agent systems.

| Method                         | Dynamic Orchestration   | Adaptive Routing   | Explicit Natural Language Instructions   |
|--------------------------------|-------------------------|--------------------|------------------------------------------|
| GTPSwarm [Zhuge et al. , 2024] | ✓                       | ×                  | ×                                        |
| MasRouter [Yue et al. , 2025]  | ✓                       | ×                  | ×                                        |
| Conductor [Anonymous, 2025]    | ✓                       | ×                  | ×                                        |
| Puppeteer [Dang et al. , 2025] | ✓                       | ✓                  | ×                                        |
| Ours (A2A-based)               | ✓                       | ✓                  | ✓                                        |

## 3 Information-Flow-Centric Multi-Agent Coordination

Agent-to-Agent Communication Toolkit. We utilize a set of A2A communication toolkits from CORAL

<!-- formula-not-decoded -->

which are available to all agents for sending and receiving natural-language messages.

The toolkit wait for mention induces a blocking operation

<!-- formula-not-decoded -->

where agent a i ∈ A enters a waiting state until it receives a message m from another agent.

The toolkit send messages defines a message-sending operation

<!-- formula-not-decoded -->

where agent a i sends a natural-language message with content c ∈ M to a designated agent a j ∈ A .

Together, these toolkits induce an asynchronous communication process

<!-- formula-not-decoded -->

enabling agents to actively coordinate through naturallanguage communication, without relying on humanengineered routing rules or manual context engineering.

Information-Flow-Orchestrated Multi-Agent Paradigm. We consider a multi-agent system defined by a finite set of agents

<!-- formula-not-decoded -->

among which a distinguished agent

<!-- formula-not-decoded -->

is designated as the information flow orchestrator.

We impose an asymmetric communication constraint

<!-- formula-not-decoded -->

such that the information flow orchestrator may communicate with any agent, while all other agents communicate exclusively with the information flow orchestrator.

The query

<!-- formula-not-decoded -->

is first received by the information flow orchestrator a o at time step t = 0 .

At each step t , the information flow orchestrator generates a coordination message based on the task query and the accumulated inter-agent communication history. Let

<!-- formula-not-decoded -->

denote the message history between the information flow orchestrator and other agents. The information flow orchestrator's outgoing message is generated as

<!-- formula-not-decoded -->

where p o denotes the prompt that specifies the role and responsibilities of the information flow orchestrator, including: (i) monitoring the task execution process to ensure reliability and consistency; (ii) inquiring appropriate agents when additional reasoning is required to derive or refine task instructions; and (iii) relaying or dispatching task instructions to appropriate agents for execution.

The generated message is then sent to a selected agent a j ∈ A in the form

<!-- formula-not-decoded -->

where the content c o,t is expressed in natural language and takes the form of either an inquiry or an instruction . The message m o → j o,t is appended to the message history H .

Upon receiving a message from the information flow orchestrator, agent a j either produces a direct response or invokes external tools to obtain intermediate results before responding. Let

<!-- formula-not-decoded -->

denote the (optional) result obtained via tool invocation. The agent response is then generated as

<!-- formula-not-decoded -->

where p j denotes the prompt associated with agent a j , which may vary across agents depending on their roles.

The agent response is sent back to the information flow orchestrator as

<!-- formula-not-decoded -->

and appended to the message history H .

This interaction process proceeds through iterative message exchanges between the information flow orchestrator and other agents.

The information flow orchestrator is equipped with a dedicated submit answer tool , and may decide to submit a final answer based on the accumulated message history H , the original query q , and its prompt p o , i.e.,

<!-- formula-not-decoded -->

The submission criteria is explicitly defined in the information flow orchestrator prompt p o . In addition, a fixed execution-time budget of 30 minutes is enforced, after which the information flow orchestrator is required to submit its current best answer.

## 4 Evaluation and Analysis

## 4.1 Benchmark: GAIA

The more general a task is, the harder it becomes for human engineers to exhaustively anticipate and encode all possible edge cases that may arise during execution. To evaluate our paradigm under such settings, we adopt GAIA [Liu et al. , 2025] as the benchmark in this work.

GAIAis a benchmark designed for generalist AI assistants, covering diverse domains and requiring multimodal reasoning, code execution, and live web search. We conduct our evaluation on the GAIA validation set, which consists of 165 tasks with difficulty levels ranging from Level 1 to Level 3. Each task has a unique, objectively verifiable ground-truth answer.

## 4.2 Baseline and Experimental Settings

We formulate the objective of our experiments around two research questions (RQs):

RQ1 : Can our A2A-based MAS paradigm match the performance of a workflow-based MAS in terms of task completion rate and cost?

RQ2 : Can our A2A-based MAS paradigm surpass the performance of a workflow-based MAS in terms of task completion rate and cost?

To answer these questions, we choose OWL [Hu et al. , 2025] as the baseline. OWL is a mature and representative workflow-based MAS designed for general-purpose tasks, and it represents the state of the art among open-source MAS evaluated on the GAIA benchmark.

For a fair comparison, we adopt the same set of agent roles as in OWL, including a planner, web agent, document agent, and reasoning &amp; coding agent, while excluding the coordinator role, as its routing functionality after task decomposition partially overlaps with that of our information flow orchestrator. In our paradigm, these agents are organized under an information flow orchestrator. All agents are equipped with the proposed A2A communication toolkit and corresponding prompts. Detailed descriptions of the toolkit and prompts are provided in Appendix A.

To evaluate RQ1 , we use a strong language model, Grok 4.1 Fast, and assign it to all agents in both our paradigm and OWL. We reimplement OWL in our experimental setup because the original OWL paper reports accuracy but does not provide token consumption statistics, which are required for cost comparison.

To evaluate RQ2 , we adopt a heterogeneous model configuration. Specifically, we assign Grok 4.1 Fast to the main agents in both systems (the information flow orchestrator/planner in our paradigm, and the planner/coordinator in OWL), while assigning a weaker model, GPT 4.1 Mini, to the worker agents (web agent, document agent, and reasoning &amp; coding agent). This setting reflects the intuition that weaker worker agents are more likely to produce partial results or errors, thereby increasing the occurrence of edge cases during task execution.

All OWL experiments are reproduced based on its official open-source implementation. To match the experimental configuration reported in the original OWL paper, we adjust the maximum number of replanning attempts from the default value of 2 in the codebase to 3. In addition, for both OWL and our paradigm, the temperature of all language models is set to 0, following the setting used in the OWL paper, to ensure a fair and deterministic comparison.

## 4.3 Main Results

Table 2 reports the pass@1 accuracy on the GAIA validation set across different difficulty levels under the two experimental settings. Correspondingly, Figure 3 shows the cumulative distribution functions (CDFs) of token consumption for the same settings.

When all agents are equipped with Grok 4.1 Fast, our paradigm achieves the same overall accuracy as OWL, at 64.24%. In terms of token consumption, our paradigm incurs slightly higher usage. This difference

| Method                       | Level 1 (53)   | Level 2 (86)   | Level 3 (26)   | Overall (165)   |
|------------------------------|----------------|----------------|----------------|-----------------|
| All Agents: Grok 4.1 Fast    |                |                |                |                 |
| Our Paradigm (A2A-based MAS) | 0.7547         | 0.6163         | 0.5000         | 0.6424          |
| OWL (Workflow-based MAS)     | 0.8113         | 0.5814         | 0.5000         | 0.6424          |
| Main Agents: Grok 4.1 Fast   |                |                |                |                 |
| Our Paradigm (A2A-based MAS) | 0.7925         | 0.6047         | 0.4231         | 0.6364          |
| OWL (Workflow-based MAS)     | 0.7358         | 0.5116         | 0.3077         | 0.5515          |

Table 2: Pass@1 accuracy on the GAIA validation set across different difficulty levels. Numbers in parentheses indicate the number of tasks per level. Bold numbers indicate the best performance within each configuration.

<!-- image -->

- (a) Token consumption CDF with all agents using Grok 4.1 Fast.
- (b) Token consumption CDF with heterogeneous agent models.

<!-- image -->

Figure 3: Cumulative distribution functions (CDFs) of token consumption for OWL and the proposed Information-Flow-Orchestrated MAS under different model configurations.

is expected, as coordination between agents is realized through agents autonomously invoking send messages and wait for mention , rather than through manually predefined context concatenation. These results answer RQ1 , demonstrating that our A2A-based MAS paradigm can match a workflow-based MAS in both task completion rate and cost.

In the heterogeneous setting, where only the main agents use Grok 4.1 Fast and the worker agents use the weaker GPT 4.1 Mini, the performance gap becomes more pronounced. OWL's overall accuracy drops to 55.15%, whereas our paradigm maintains an accuracy of 63.64%. This improvement is consistent across all difficulty levels (Level 1, Level 2, and Level 3). Regarding token consumption, our paradigm still exhibits slightly higher usage on simpler tasks. However, for more challenging tasks requiring more than 0.6M tokens, our paradigm consistently consumes fewer tokens than OWL.

This behavior can be attributed to the different coordination mechanisms. In OWL, handling such tasks often triggers replanning, which entails re-executing previously completed subtasks. In contrast, the information flow orchestrator in our paradigm maintains a global view of the task execution process and can often resolve issues by adjusting task instructions, without re-executing previously completed subtasks. These results answer RQ2 , showing that our paradigm can surpass workflow-based MAS in both accuracy and effi- ciency under more challenging conditions.

## 4.4 Case-Level Analysis of Task Coordination and Handling of Edge Cases

To better understand why our paradigm is able to maintain high accuracy even when worker agents are equipped with weaker models, we conduct a detailed case-level analysis of the execution logs from our experiments. Through this analysis, we observe that the information flow orchestrator exhibits several recurring coordination behaviors during task execution.

In particular, we identify four distinct task coordination patterns and three different strategies for handling edge cases that emerge from the information flow orchestrator's interactions with other agents. These patterns are not predefined by human-designed workflows, but instead arise from the orchestrator's continuous monitoring of task progress and its adaptive use of A2A toolkit.

## Emergent task coordination patterns.

Figure 4 illustrates the four emergent task coordination patterns observed from the information flow orchestrator during our case-level analysis.

Direct Agent Dispatch. For non-decomposable tasks, the information flow orchestrator directly assigns the task to an appropriate agent without invoking task decomposition.

Figure 4: Case-level analysis of emergent task coordination patterns from the information flow orchestrator.

<!-- image -->

Avoiding unnecessary decomposition not only improves the likelihood of successful completion, but also reduces token consumption. This observation aligns with prior findings that excessive planning and decomposition can be detrimental for non-decomposable tasks [Kim et al. , 2025].

Planner-Mediated Decomposition. For tasks that are naturally decomposable, the information flow orchestrator consults the planner to decompose the task into subtasks, or requests replanning when necessary. This pattern closely resembles the coordination strategy commonly adopted by workflow-based MAS, and serves as a compatible operating mode when explicit task structure is beneficial.

Instruction Refinement. When an agent encounters difficulties, the information flow orchestrator does not always escalate the issue to the planner for re-decomposition. Instead, it may refine or adjust the previous task instruction and allow the same agent to continue. This strategy helps maintain a cleaner and more compact context, while avoiding redundant token consumption caused by reprocessing subtasks that have already been completed.

Agent Substitution. Similar to instruction refinement, the information flow orchestrator does not immediately resort to replanning upon failure. When a task cannot be completed by a particular agent, it may directly reassign the task to a different agent. This enables the system to explore alternative execution paths without restarting the task or incurring the overhead of full task re-decomposition.

## Emergent Edge Cases Handling Strategies.

Figure 5 presents the three emergent edge case handling strategies exhibited by the information flow orchestrator, together with representative cases and their corresponding outcomes under OWL for comparison.

Dynamic Explicitization and Tightening of Success Criteria. In the first case, a web agent is instructed to search the web for all U.S. Survivor winners through August 2023, including their names and birth dates . The web agent successfully retrieves all winner names, but fails to find birth dates for several individuals. Under our paradigm, the information flow orchestrator detects that entries with unknown birth dates do not satisfy the implicit success criteria of the original query. It explicitly identifies this mismatch and dynamically refines the task requirements to enforce completeness before allowing further execution. In OWL, however, since the subtask is not marked as failed, downstream subtasks are executed on an incorrect premise.

Real-Time Auditing and Correction of Intermediate Semantic Assumptions. In the second case, a web agent is asked to list the studio albums released before 1999 by Fiona Apple and Paula Cole, together with their release years . The agent returns the following results: Tidal (1996) and When the Pawn... (1999) for Fiona Apple, and Harbinger (1994) , This Fire (1996) , and Amen (1999) for Paula Cole. In our paradigm, the information flow orchestrator explicitly audits the intermediate semantic assumption that albums released in 1999 satisfy the condition 'before 1999.' It prunes the invalid entries ( When the Pawn... and Amen ) before they propagate into downstream subtasks. In contrast, OWL proceeds to subsequent subtasks without correction, as the intermediate result is not flagged as erroneous.

Continuous Monitoring and Correction of Instruction Alignment. In the third case, a reasoning and coding agent is instructed to access an Excel file, extract data for books read in 2022, and compute reading rates in words per day . The agent reports having identified ten books and computes the slowest reading rate, but notes that page counts are used as a proxy for word counts due to missing direct word count information . Upon detecting the mismatch between the requested metric and the proxy used, the information flow orchestrator escalates the issue to the planner, which issues a refined instruction: for each book in the extracted list, retrieve the total word count from reliable online sources . In OWL, by contrast, the subtask is marked as successful, and subsequent steps proceed under this misaligned assumption.

Figure 5: Case-level analysis of emergent edges cases handling from the information flow orchestrator.

<!-- image -->

## 5 Conclusion and Future Work

In this work, we propose an Information-FlowOrchestrated Multi-Agent Paradigm via Agent-to-Agent Communication to address two fundamental limitations of workflow-based multi-agent systems: (1) the substantial manual effort required from human engineers to design task states, routing logic, and context concatenation rules; and (2) the inherent difficulty of exhaustively anticipating all edge cases that may arise during complex task execution. We evaluate our paradigm on the general-purpose benchmark GAIA, using the representative workflow-based MAS OWL as the baseline, while controlling for agent roles and underlying language models. Under the pass@1 setting, our method achieves an accuracy of 63.64% , outperforming OWL's 55.15% by 8.49 percentage points with nearly identical token consumption. Beyond aggregate metrics, our caselevel analysis reveals that the information flow orchestrator exhibits four distinct task coordination patterns and three different strategies for handling edge cases , which emerge from its adaptive interactions with other agents rather than from predefined workflows.

For future work, our current evaluation focuses on generalpurpose tasks, motivated by the assumption that such settings are more likely to expose diverse and unforeseen edge cases. An important next step is to evaluate the proposed paradigm on domain-specific tasks, where stronger structural priors and specialized workflows are available. This would help clarify how information-flow-orchestrated coordination interacts with domain knowledge, and whether similar emergent coordination behaviors arise under more constrained task distributions.

## References

[Anonymous, 2025] Anonymous. Learning to orchestrate agents in natural language with the conductor. In Submitted to The Fourteenth International Conference on Learning Representations , 2025. under review.

- [Antoniades et al. , 2025] Antonis Antoniades, Albert ¨ Orwall, Kexun Zhang, Yuxi Xie, Anirudh Goyal, and William Yang Wang. Swe-search: Enhancing software agents with monte carlo tree search and iterative refinement. In The Thirteenth International Conference on Learning Representations , 2025.
- [Chen et al. , 2024] Li Chen, Penghao Wu, Kashyap Chitta, Bernhard Jaeger, Andreas Geiger, and Hongyang Li. Endto-end autonomous driving: Challenges and frontiers. IEEE Transactions on Pattern Analysis and Machine Intelligence , 2024.
- [Dang et al. , 2025] Yufan Dang, Chen Qian, Xueheng Luo, Jingru Fan, Zihao Xie, Ruijie Shi, Weize Chen, Cheng Yang, Xiaoyin Che, Ye Tian, Xuantang Xiong, Lei Han, Zhiyuan Liu, and Maosong Sun. Multi-agent collaboration via evolving orchestration. In The Thirty-ninth Annual Conference on Neural Information Processing Systems , 2025.
- [Guo et al. , 2024] Taicheng Guo, Xiuying Chen, Yaqi Wang, Ruidi Chang, Shichao Pei, Nitesh V. Chawla, Olaf Wiest, and Xiangliang Zhang. Large language model based multiagents: A survey of progress and challenges. In Kate Larson, editor, Proceedings of the Thirty-Third International Joint Conference on Artificial Intelligence, IJCAI24 , pages 8048-8057. International Joint Conferences on Artificial Intelligence Organization, 8 2024. Survey Track.
- [Hong et al. , 2023] Sirui Hong, Mingchen Zhuge, Jonathan Chen, Xiawu Zheng, Yuheng Cheng, Jinlin Wang, Ceyao Zhang, Zili Wang, Steven Ka Shing Yau, Zijuan Lin, et al. Metagpt: Meta programming for a multi-agent collaborative framework. In The Twelfth International Conference on Learning Representations , 2023.
- [Hu et al. , 2023] Yihan Hu, Jiazhi Yang, Li Chen, Keyu Li, Chonghao Sima, Xizhou Zhu, Siqi Chai, Senyao Du, Tianwei Lin, Wenhai Wang, et al. Planning-oriented autonomous driving. In Proceedings of the IEEE/CVF conference on computer vision and pattern recognition , pages 17853-17862, 2023.
- [Hu et al. , 2025] Mengkang Hu, Yuhang Zhou, Wendong Fan, Yuzhou Nie, Ziyu Ye, Bowei Xia, Tao Sun, Zhaoxuan Jin, Yingru Li, Zeyu Zhang, Yifeng Wang, Qianshuo Ye, Bernard Ghanem, Ping Luo, and Guohao Li. OWL: Optimized workforce learning for general multi-agent assistance in real-world task automation. In The Thirty-ninth Annual Conference on Neural Information Processing Systems , 2025.
- [Kim et al. , 2025] Yubin Kim, Ken Gu, Chanwoo Park, Chunjong Park, Samuel Schmidgall, A Ali Heydari, Yao Yan, Zhihan Zhang, Yuchen Zhuang, Mark Malhotra, et al. Towards a science of scaling agent systems. arXiv preprint arXiv:2512.08296 , 2025.
- [Li et al. , 2024] Ziming Li, Qianbo ZANG, David Ma, Jiawei Guo, Tuney Zheng, Minghao Liu, Xinyao Niu, Yue Wang, Jian Yang, Jiaheng Liu, et al. Autokaggle: A multiagent framework for autonomous data science competitions. In ICLR 2025 Worshop Emergent Possibilities and Challenges in Deep Learning for Code , 2024.
- [Liu et al. , 2025] Jiarun Liu, Shiyue Xu, Shangkun Liu, Yang Li, Wen Liu, Min Liu, Xiaoqing Zhou, Hanmin Wang, Shilin Jia, Shaohua Tian, et al. Joyagentjdgenie: Technical report on the gaia. arXiv preprint arXiv:2510.00510 , 2025.
- [Mialon et al. , 2024] Gr´ egoire Mialon, Cl´ ementine Fourrier, Thomas Wolf, Yann LeCun, and Thomas Scialom. GAIA: a benchmark for general AI assistants. In The Twelfth International Conference on Learning Representations , 2024.
- [Prakash et al. , 2021] Aditya Prakash, Kashyap Chitta, and Andreas Geiger. Multi-modal fusion transformer for end-to-end autonomous driving. In Proceedings of the IEEE/CVF conference on computer vision and pattern recognition , pages 7077-7087, 2021.
- [Ren et al. , 2025] Xinxing Ren, Qianbo Zang, and Zekun Guo. Simugen: Multi-modal agentic framework for constructing block diagram-based simulation models. In Workshop on Scaling Environments for Agents , 2025.
- [Su et al. , 2025] Haoyang Su, Renqi Chen, Shixiang Tang, Zhenfei Yin, Xinzhe Zheng, Jinzhe Li, Biqing Qi, Qi Wu, Hui Li, Wanli Ouyang, Philip Torr, Bowen Zhou, and Nanqing Dong. Many heads are better than one: Improved scientific idea generation by a LLM-based multiagent system. In Wanxiang Che, Joyce Nabende, Ekaterina Shutova, and Mohammad Taher Pilehvar, editors, Proceedings of the 63rd Annual Meeting of the Association for Computational Linguistics (Volume 1: Long Papers) , pages 28201-28240, Vienna, Austria, July 2025. Association for Computational Linguistics.
- [Tang et al. , 2025] Jiabin Tang, Tianyu Fan, and Chao Huang. Autoagent: A fully-automated and zerocode framework for llm agents. arXiv preprint arXiv:2502.05957 , 2025.
- [Wei et al. , 2025] Jason Wei, Zhiqing Sun, Spencer Papay, Scott McKinney, Jeffrey Han, Isa Fulford, Hyung Won Chung, Alex Tachard Passos, William Fedus, and Amelia Glaese. Browsecomp: A simple yet challenging benchmark for browsing agents. arXiv preprint arXiv:2504.12516 , 2025.
- [Yang et al. , 2024] John Yang, Carlos E Jimenez, Alexander Wettig, Kilian Lieret, Shunyu Yao, Karthik Narasimhan, and Ofir Press. Swe-agent: agent-computer interfaces enable automated software engineering. In Proceedings of the 38th International Conference on Neural Information Processing Systems , pages 50528-50652, 2024.
- [Yu et al. , 2025] Yangyang Yu, Haohang Li, Zhi Chen, Yuechen Jiang, Yang Li, Jordan W Suchow, Denghui Zhang, and Khaldoun Khashanah. Finmem: A performance-enhanced llm trading agent with layered memory and character design. IEEE Transactions on Big Data , 2025.
- [Yue et al. , 2025] Yanwei Yue, Guibin Zhang, Boyang Liu, Guancheng Wan, Kun Wang, Dawei Cheng, and Yiyan Qi. MasRouter: Learning to route LLMs for multi-agent systems. In Wanxiang Che, Joyce Nabende, Ekaterina

Shutova, and Mohammad Taher Pilehvar, editors, Proceedings of the 63rd Annual Meeting of the Association for Computational Linguistics (Volume 1: Long Papers) , pages 15549-15572, Vienna, Austria, July 2025. Association for Computational Linguistics.

- [Zhu et al. , 2025] Yuxuan Zhu, Tengjun Jin, Yada Pruksachatkun, Andy K Zhang, Shu Liu, Sasha Cui, Sayash Kapoor, Shayne Longpre, Kevin Meng, Rebecca Weiss, Fazl Barez, Rahul Gupta, Jwala Dhamala, Jacob Merizian, Mario Giulianelli, Harry Coppock, Cozmin Ududec, Antony Kellermann, Jasjeet S Sekhon, Jacob Steinhardt, Sarah Schwettmann, Arvind Narayanan, Matei Zaharia, Ion Stoica, Percy Liang, and Daniel Kang. Establishing best practices in building rigorous agentic benchmarks. In The Thirty-ninth Annual Conference on Neural Information Processing Systems Datasets and Benchmarks Track , 2025.
- [Zhuge et al. , 2024] Mingchen Zhuge, Wenyi Wang, Louis Kirsch, Francesco Faccio, Dmitrii Khizbullin, and J¨ urgen Schmidhuber. Gptswarm: Language agents as optimizable graphs. In Forty-first International Conference on Machine Learning , 2024.

## A Agent Toolkit Details

Table 3 summarizes the toolkits equipped by each agent in our system. We categorize all tools into three classes: A2A communication tools , domain-specific tools , and auxiliary tools . All agents are equipped with a set of A2A communication tools, namely send message and wait for mention , which provide a uniform interface for inter-agent coordination. In contrast, the Web Agent, Document Agent, and Reasoning &amp; Coding Agent are selectively equipped with domain-specific tools corresponding to their functional roles. These domain-specific tools are inherited from the open-source OWL codebase and enable web retrieval, document processing, multimodal understanding, and code execution. Finally, the Information Flow Orchestrator is additionally granted an auxiliary tool, submit answer , which centralizes task termination and final answer submission.

Table 3: Agent roles and their equipped toolkits, categorized into A2A communication tools, domain-specific tools, and auxiliary tools.

| Agent                           | A2A Communication Tools         | Domain-Specific Tools                                                                                                                            | Auxiliary Tools   |
|---------------------------------|---------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------|-------------------|
| Information Flow Orches- trator | send message , wait for mention | -                                                                                                                                                | submit answer     |
| Planner                         | send message , wait for mention | -                                                                                                                                                | -                 |
| Web Agent                       | send message , wait for mention | search google , search wiki revisions , search wiki , search archived webpage , browse url , extract document content , ask question about video | -                 |
| Document Agent                  | send message , wait for mention | extract document content , ask question about image , ask question about audio , ask question about video , execute code                         | -                 |
| Reasoning &Coding Agent         | send message , wait for mention | execute code , extract excel content , extract document content                                                                                  | -                 |

## B Agent Prompt

For each agent, we explicitly define a role-specific prompt that specifies its core responsibilities. These responsibilities include both role-aligned task duties , which reflect the agent's functional specialization, and inter-agent communication duties , which govern how the agent exchanges information with other agents during task execution.

## Prompt of Information Flow Orchestrator

=====

RULES

INFORMATION

You

OF

are an

advanced

FLOW

ORCHESTRATOR

=====

information\_flow\_orchestrator.

## Core Responsibilities:

1. Inquiry and Relay Management
2. -MONITOR the task process and make sure its reliability and healthy.
3. -INQUIRE proper agents to perform any additional reasoning required to support progress or resolve uncertainty.
4. -RELAY task-level content and necessarily previous results to proper agents.
5. -Confirm the generated answer with proper agent(s) and reach a consistent consensus before sumbitted.
2. Communication with Other Agents
7. -Use send\_message to communicate with other agents.
8. -Use wait\_for\_mentions to receive messages from other agents.
3. Submit Final Answer
10. -Confirm the final answer with the planner or proper agent(s) and reach a consistent consensus.
11. -Call submit\_answer\_tool to submit the final answer when it is generated and verified.

## Prompt of Planner

===== RULES OF PLANNER AGENT =====

You are an advanced planner\_agent to decompose task into subtask, replan the task based on previosu attempted trajactories and cooperate with other agents in coral server.

## Core Responsibilities:

1. Task Decompostion -You must send all decomposed subtasks to information\_flow\_orchestrator in the format of a numbered list within &lt;tasks&gt; tags, as shown below: &lt;tasks&gt; &lt;task&gt;Subtask 1&lt;/task&gt; &lt;task&gt;Subtask 2&lt;/task&gt;
2. &lt;/tasks&gt;
3. -You MUST NOT explicitly mention what agents and what tools to use in the subtasks, just let the agent decide what to do.
4. -Though it's not a must, you should try your best effort to make each subtask achievable for an agent.

## 2. Task Progress Reasoning

- -When asked to perform tasks including but not limited to verification, critique, assessing the reliability of intermediate results, replanning, reflection, questioning, or critique, provide the necessary reasoning to support task progress.
3. Communication with Other Agents
- -Use send\_message to communicate with other agents.
- -Use wait\_for\_mentions to receive messages from other agents.

## Prompt of Web Agent

- ===== RULES OF WEB AGENT ===== You are an advanced web\_agent powered by web browsing/searching capabilities, but you are not able to run code script. Core Capabilities: 1. Web Browsing and Searching -Call proper tools to solve web-searching-related questions. 2. Communication with Other Agents -Use send\_message to communicate with other agents. -Use wait\_for\_mentions to receive messages from other agents.

## Prompt of Documentation Processing Agent

===== RULES OF DOCUMENTATION PROCESSING AGENT =====

You are an advanced document\_processing\_agent powered by documentation processing capabilities.

## Core Capabilities:

1. Process Documents and Multimodal Data
2. -Call proper tools to solve documentation-processing-related questions.
2. Communication with Other Agents
4. -Use send\_message to communicate with other agents.
5. -Use wait\_for\_mentions to receive messages from other agents.

## Prompt of Reasoning &amp; Coding Agent

===== RULES OF REASONING CODING AGENT ===== You are an advanced reasoning\_coding\_agent powered by reasoning, coding and running code script capabilities. Core Capabilities: 1. Reasoning and Coding -Call proper tools to solve coding-related questions. 2. Communication with Other Agents -Use send\_message to communicate with other agents. -Use wait\_for\_mentions to receive messages from other agents.