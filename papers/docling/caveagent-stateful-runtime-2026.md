## CaveAgent: Transforming LLMs into Stateful Runtime Operators

Maohao Ran ∗ 1 , 2 , Zhenglin Wan ∗ 1 , 3 , Cooper Lin 4 , Yanting Zhang 1 , Hongyu Xin 1 , Hongwei Fan 7 , Yibo Xu 1 , Beier Luo 4 , Yaxin Zhou 5 , Wangbo Zhao 3 , Lijie Yang 8 , Lang Feng 6 , Fuchao Yang 6 , Jingxuan Wu 9 , Yiqiao Huang 10 , Chendong Ma 2 , Dailing Jiang 2 , Jianbo Deng 1 , Sirui Han 1 , Yang You 3 , Bo An 6 , Yike Guo 1 , Jun Song 1 , 2†

1 HKUST 2 HKBU 3 NUS 4 HKU 5 CMU 6 NTU, Singapore 7 Imperial College London 8 Princeton 9 UNC, Chapel Hill 10 Harvard

Abstract: LLM-based agents are increasingly capable of complex task execution, yet current agentic systems remain constrained by text-centric paradigms that struggle with long-horizon tasks due to fragile multi-turn dependencies and context drift. We present CaveAgent, a framework that shifts LLM tool use from 'LLM-as-TextGenerator' to 'LLM-as-Runtime-Operator.' CaveAgent introduces a dual-stream architecture: a semantic stream for lightweight reasoning and a runtime stream backed by a persistent Python environment for stateful execution. Rather than treating the LLM's text context as the primary workspace, CaveAgent elevates the persistent runtime as the central locus. Beyond leveraging code generation to resolve interdependent sub-tasks (e.g., loops, conditionals) in a single step, CaveAgent introduces Stateful Runtime Management: it injects, manipulates, and retrieves complex Python objects (e.g., DataFrames, database connections) that persist across turns, unlike existing codebased approaches that remain text-bound. CaveAgent further provides a runtime-integrated skill management system that extends the Agent Skills open standard, enabling ecosystem interoperability through executable skill injections. This persistence mechanism serves as a high-fidelity external memory that reduces context drift in multi-turn interactions and preserves processed data for downstream applications with less information loss. Evaluations on Tau 2 -bench and BFCL across six SOTA LLMs demonstrate consistent improvements: +10.5% success rate on retail tasks, 28.4% reduction in total token consumption, and 59% token reduction on dataintensive tasks. The accessible runtime state further provides programmatically verifiable feedback, enabling automated evaluation and reward signal generation without human annotation and establishing a structural foundation for future research in Reinforcement Learning with Verifiable Rewards (RLVR).

<!-- image -->

<!-- image -->

<!-- image -->

Date

: Feb 19, 2026 (v.2)

Code

: https://github.com/acodercat/cave-agent

Main Contact : Zhenglin Wan (vanzl@u.nus.edu), Jun Song (junsong@hkbu.edu.hk)

Figure 1: Town Simulation: a toy example for Stateful Runtime-Mediated Multi-Agent Collaboration.

<!-- image -->

<!-- image -->

∗ Joint first author &amp; Equal Contribution.

† Corresponding to junsong@hkbu.edu.hk. Work conducted at the Hong Kong Generative AI Research and Development Center (HKGAI), led by HKUST.

## 1. Introduction

Large Language Models (LLMs) have demonstrated strong knowledge acquisition and reasoning capabilities across diverse natural language processing tasks. Building on these capabilities, tool-integrated reasoning (TIR) enables LLM agents to interact with external tools and APIs in a multi-turn manner 1 (Lu et al., 2023, Shen et al., 2023, Patil et al., 2024, Qu et al., 2025), expanding their information access and solution space. This has extended LLM agents to various domains, including scientific discovery (Boiko et al., 2023, Bran et al., 2023), mathematical problem-solving (Gao et al., 2023, Chen et al., 2022), Web GUI navigation (Zhou et al., 2023, Yao et al., 2022a), and robotics (Driess et al., 2023, Zitkovich et al., 2023).

Despite this progress, the conventional protocol for tool use requires LLMs to conform to predefined JSON schemas and generate structured JSON objects containing precise tool names and arguments (Qin et al., 2023, Achiam et al., 2023). For example, to retrieve stock data, the model must synthesize a JSON string like {"tool": "get\_stock", "params": {"ticker": "AAPL", "date": "today"}} , requiring exact adherence to syntax and field constraints. This imposes three architectural limitations: 1) Rigid Control Flow: each turn executes a single tool call (or parallel batch) and serializes output back to context, introducing latency for tasks requiring sequential orchestration (Wu et al., 2024, Shen et al., 2023); 2) Statelessness: each call is an isolated transaction with no persistent state across turns, so intermediate results must be serialized back into text, causing token overhead for complex data structures and cascading error propagation (Qiao et al., 2023, Kim et al., 2023); and 3) Limited Composability: JSON schemas express flat function signatures and cannot natively represent loops, conditionals, or variable dependencies, forcing multi-step logic into error-prone multi-turn dialogue (Kim et al., 2023, Wang et al., 2024).

While recent works attempt to address these issues with code-based tool use (Wang et al., 2024, Yang et al., 2024), they predominantly adopt a process-oriented paradigm where the runtime state remains internalized and text-bound . This creates a 'textualization bottleneck': variables are accessible to external systems only through text output, requiring serialization into text strings (e.g., printing a DataFrame) to communicate with the user (Wang et al., 2024, Yao et al., 2022b). This prevents the direct input and output of structured, manipulatable objects, making it inefficient or impossible to handle complex non-textual data (e.g., large datasets, videos) (Qiao et al., 2023) and interact with downstream tasks. To address these limitations:

We aim to build a system that utilizes Python's "everything is an object' philosophy to enable full Object-Oriented function calling and interaction, delegating context engineering to a persistent runtime and allowing the direct injection and retrieval of high-fidelity objects without serialization loss, thereby fully leveraging the strong code-generation capability of LLMs, and enabling modular distribution of executable tool capabilities through a portable Agent-Skill standard.

We present CaveAgent , an open-source framework that introduces the concept of Stateful Runtime Management for LLM agents, shifting code-based tool use from 'process-oriented function calling' to persistent 'object-oriented state manipulation.' CaveAgent operates on a dual-stream architecture with two distinct streams: a semantic stream for reasoning and a runtime stream for state management and code execution. This represents a fundamental inversion of the conventional paradigm: whereas existing agents treat the LLM's semantic context as the primary workspace with external tools as auxiliary , CaveAgent elevates the persistent runtime as the primary locus of computation and state, with the semantic stream serving as a lightweight orchestrator that generates code to manipulate it.

By injecting complex data structures (e.g., graphs, DataFrames) directly into the runtime as persistent objects, CaveAgent achieves a form of context engineering : the agent manipulates high-fidelity data via concise variable references, decoupling storage from the limited context window. Any intermediate result (e.g., DataFrames, planning trees, or key metadata) can be stored in persistent variables that the agent actively retrieves for later use or downstream applications (code as action, state as memory). This reduces progressive context degradation in multi-turn interactions (Ouyang et al., 2022, Luo et al., 2025), enables context compression, and provides error-free recall through the runtime serving as an external memory.

The persistent environment also enables few-step resolution of complex logical dependencies by using code to interact with multiple interdependent tools, allowing the agent to compose workflows (e.g., data filtering followed by analysis) in a few turns rather than through error-prone multi-round function calling (Wang et al., 2023a, Qin et al., 2023). The runtime's transparency makes agent behavior fully verifiable, supporting checks on both intermediate programmatic states and final output objects of any data type, thereby enabling fine-grained reward signals for Reinforcement Learning.

Finally, CaveAgent supports artifact handoff without information loss by returning native Python objects rather than text representations, enabling direct use in downstream tasks such as UI rendering, visualization, and structured validation. The runtime can be serialized and reloaded, preserving the agent's complete state across sessions. This transforms the LLM from

1 In this paper, we use tool use and function calling interchangeably.

Figure 2: Key Advantages of CaveAgent

<!-- image -->

an isolated text generator into a stateful, interoperable computational component within broader software ecosystems.

The function-calling paradigm in CaveAgent also extends beyond single-agent capabilities to enable Runtime-Mediated Multi-Agent Coordination , as shown in Figure 1 and Figure 2. Unlike conventional frameworks where agents coordinate via lossy text message passing (Li et al., 2023, Park et al., 2023), CaveAgent enables agents to interact through direct state manipulation. A supervisor agent can programmatically inject variables into a sub-agent's runtime to alter its environment or task context without ambiguous natural language instructions. Multiple agents can also operate on a unified shared runtime, achieving implicit synchronization: when one agent modifies a shared object (e.g., updating a global 'weather' entity in a town simulation), the change is immediately visible to all peers through direct reference. This transforms multi-agent collaboration from serialized dialogue into precise, verifiable state flow (details in Appendix E). We summarize our contributions as follows:

- We introduce CaveAgent , a tool-use framework built on Stateful Runtime Management . CaveAgent shifts the paradigm from process-oriented function calling to persistent, object-oriented state management. It achieves context compression and context-grounded memory recall by delegating context engineering to a persistent runtime, eliminating the token overhead and precision loss of textual serialization while enabling few-step resolution of logically interdependent tasks. CaveAgent also introduces runtime-integrated skill management that extends the Agent Skills open standard: because the architecture treats tools as first-class Python objects, skills can deliver executable artifacts (functions, variables, and type definitions) directly into the runtime upon activation, unifying tool registration and skill distribution into a single mechanism.
- The framework's programmatic inspectability is a structural property of the architecture: runtime state is deterministically accessible at any point, providing automated evaluation and fine-grained reward signals for Reinforcement Learning with Verifiable Rewards (RLVR) without requiring subjective human annotation.
- We evaluate CaveAgent on standard benchmarks (e.g., Tau 2 -Bench) and provide case studies across various domains including geospatial analysis (Appendix G.3) and AutoML multi-agent coordination (Appendix E.4). We also demonstrate the potential to extend the paradigm to Stateful Runtime-Mediated Multi-Agent Coordination with qualitative results, opening directions for future research.

Figure 3: Framework Overview

<!-- image -->

## 2. CaveAgent: Stateful Runtime Management

## 2.1. Core Methodologies

In this section, we present the design of CaveAgent. As illustrated in Figure 3, CaveAgent adopts a dual-stream architecture , maintaining two synchronized streams throughout the interaction lifecycle: a Semantic Stream for lightweight reasoning, and a Runtime Stream for stateful execution, observation, and context engineering. Unlike conventional agents where the LLM's text context serves as the primary workspace and external tools are called as auxiliary services, CaveAgent inverts this relationship: the persistent runtime becomes the central locus of data storage, computation, and state management, while the semantic stream is reduced to a lightweight controller that generates code to operate on the runtime.

We model the agent's task as a sequential decision process over a horizon T . At each turn t ∈ [ 1 , T ] , the agent receives a query or observation xt and must produce a response yt . Unlike traditional formulations where the entire state is re-serialized into xt , we introduce a latent runtime state S t (we call it "in-runtime context"). The system evolution is thus defined by:

<!-- formula-not-decoded -->

<!-- formula-not-decoded -->

where ht represents the semantic history ('in-prompt context') and ct is the executable code generated by the agent. The key design choice is the decoupling of ht and S t : the semantic stream tracks intent and lightweight reasoning for code generation, while the runtime stream maintains all data and execution state via the code generated by the semantic stream.

The Runtime Stream: The execution kernel of the runtime stream is a persistent Python kernel (an IPython interactive shell). We conceptualize each interaction turn t not as an isolated API call, but as a cell execution in a virtual Jupyter notebook.

- Persistent Namespace: The state S t comprises the global namespace N t , containing all variables, functions, and imported modules. When the agent executes code ct (e.g., x = 5 ), the modification to N t persists to N t + 1 . This allows subsequent turns to reference x directly without requiring the LLM to memorize or re-output its value.
- Stateful Injection: Tools are not only described in text; they are injected into N 0 as live Python objects. This allows the agent to interact with stateful objects via calling tools that modify the object's internal state across turns.

The runtime stream can also assign values to new variables during interaction and inject them into the persistent namespace (in-runtime context). This enables large context in complex tasks, such as large DataFrames, graphs, or other data structures, to be managed entirely by the Python runtime stream as stateful variables. Their values are preserved natively in persistent

runtime memory without requiring repeated serialization into text, eliminating the risk of hallucination from lossy textual representations.

Code as Action, State as Memory The agent stores key information (such as reasoning chains and intermediate data analysis results) as persistent variables in the runtime context, retaining only a lightweight description and reference in its in-prompt context. The runtime thus functions as an external memory, allowing the agent to retrieve stored data as native Python objects, achieving context compression and reducing progressive context degradation in multi-turn interactions. This property addresses persistent challenges in agentic tool use, specifically memory, dynamic decision-making, and long-horizon reasoning (Patil et al.).

A natural question arises: what distinguishes storing intermediate results in runtime variables from persisting them to files? We identify three key differences. First, type fidelity : runtime variables preserve native Python object types with full method interfaces (e.g., a DataFrame retains .groupby() , .merge() operations), whereas file-based storage requires serialization that may lose type information or fail entirely for non-serializable objects such as database connections, trained models with custom layers, or open file handles. Second, access latency : runtime variables enable zero-cost retrieval within the same memory space, while file I/O introduces disk latency and requires explicit read/write operations in generated code. Third, lifecycle management : runtime variables are automatically scoped to the agent session and garbage-collected appropriately, whereas file-based approaches require explicit cleanup logic to avoid accumulating temporary artifacts. That said, file-based persistence offers advantages for large-scale data exceeding memory capacity and for checkpointing across agent restarts. We note that CaveAgent also supports storing intermediate results in files, but storing them in runtime variables better respects the persistent runtime paradigm, yielding a more unified system.

Programmatic state retrieval enables the extraction of manipulated Python objects for direct use in downstream applications. Unlike conventional agents that produce text outputs requiring parsing and reconstruction, CaveAgent exposes native objects (DataFrames, class instances, arrays) with full type fidelity. This enables UI rendering via direct object binding, RL reward computation through programmatic state inspection, validation via unit test assertions against returned structures, and lossless object passing in multi-agent systems (Appendix E). The agent thus transforms the LLM from an isolated text generator into the operator of a stateful, interoperable computational component.

The Semantic Stream: Parallel to the runtime stream, the semantic stream uses the LLM to generate code that manipulates the runtime. It is also responsible for:

- Prompt Construction: Dynamically generating system instructions that describe the signatures of available tools in N t , without dumping their full state (which may be large) into the in-prompt context window.
- Observation Shaping: Captures execution outputs and enforces a length constraint τ ( · ) to prevent context explosion. This feedback mechanism guides the agent to interact with the persistent state efficiently, prioritizing concise and relevant information over verbose raw dumps in the in-prompt context ht + 1 .

This dual-stream design addresses the 'Context Explosion' problem: large data remains in the Runtime Stream ( S t ), while only high-level reasoning flows through the Semantic Stream ( ht ). By making the runtime (not the LLM's context window) the authoritative store of all data and intermediate state, CaveAgent avoids the token overhead and precision loss inherent in text-centric architectures. Unlike JSON-based function calling, which fails on inter-dependent tool calls (Lu et al., 2025), CaveAgent dispatches interdependent tool chains in a few turns via executable code. Unlike code-based approaches with internalized runtimes, CaveAgent opens the runtime as a bidirectional interface for injecting and retrieving structured objects of any type. Algorithm 1 in Appendix A shows the iteration loop. We next describe the core designs of CaveAgent.

## 2.1.1. Variable and Function Injection

CaveAgent treats Python objects and functions as first-class citizens within the runtime. Each injectable entity is wrapped in a container that automatically extracts metadata: signatures, type hints, and docstrings for functions; names, types, and descriptions for variables. This metadata is aggregated into the system prompt as a lightweight 'API reference,' while the actual objects are mapped directly into the execution engine's namespace as global symbols. This design enables Object-Oriented Interaction : instead of stateless JSON calls (e.g., tool: "sort", args: {...} ), the model invokes methods on stateful objects directly (e.g., processor.process(data) ), chaining method calls and manipulating attributes naturally. The agent interacts via executable Python programs with native control flow (loops, conditionals) and stateful data passing, delivering final output as a native Python object rather than a textual approximation.

Table 1: Comparison of Standard Agent Skills vs. CaveAgent Skills.

| Property                                                                 | Standard Skills                                                                         | CaveAgent Skills                                                                                                                   |
|--------------------------------------------------------------------------|-----------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------|
| Skill format LLM interaction Capability delivery Data flow Tool delivery | SKILL.md Reads text prompts Textual instructions Text-in, text-out Cannot deliver tools | SKILL.md + injection.py Operates on injected objects Executable runtime artifacts Object-in, object-out Injects tools into runtime |

## 2.1.2. Dynamic Context Synchronization

The Semantic Stream is 'blind' to the Runtime Stream by default. To inspect runtime state, the agent must explicitly generate code (e.g., print(df.head()) ), enforcing an Active Attention mechanism that selectively pulls only relevant slices into the token context. To prevent context explosion from verbose outputs, an Observation Shaping layer enforces a length constraint: when output exceeds L max , the system returns a structured error prompting the agent to use summary methods. This feedback loop guides efficient interaction with persistent state.

## 2.1.3. Security Check via Static Analysis

CaveAgent mitigates code execution risks via AST-based static analysis with modular policy rules: ImportRule (blocking unauthorized modules), FunctionRule (prohibiting dangerous calls like eval() ), and AttributeRule (preventing sandbox bypass). Violations return structured errors to the semantic stream, enabling self-correction without breaking interaction continuity.

## 2.2. Runtime-Integrated Skill Management

CaveAgent extends the Agent Skills open standard 2 by introducing an injection.py module alongside the standard SKILL.md fi le. While standard skills provide textbased prompts that guide LLM behavior, CaveAgent skills additionally export Functions , Variables , and Type definitions that are injected directly into the persistent runtime upon activation. The framework employs progressive disclosure: skill metadata (name and description) is loaded at startup for routing decisions, while full instructions and runtime injections are loaded on-demand when the agent invokes activate\_skill() . This bridges declarative skill definitions with CaveAgent's stateful runtime paradigm: skills deliver executable artifacts into the runtime, not merely textual instructions to the LLM. As shown in Figure 4, domain expertise is packaged as both human-readable instructions for the language model and machine-executable artifacts for the runtime.

## Discussion: From Tool Disclosure to Object Disclosure.

The skill activation pattern (lazy, metadata-driven retrieval of executable artifacts) generalizes beyond tools to any runtime object. Intermediate results stored as persistent variables ('Code as Action, State as Memory') can similarly

Figure 4: CaveAgent's Skill Management extends the Agent Skills open standard with runtime injection. Standard skills provide text prompts; CaveAgent skills additionally export functions, variables, and types into the persistent runtime.

<!-- image -->

be discovered via semantic search over their metadata (names, types, descriptions), bringing relevant state into the agent's context only when needed. Unlike retrieval-augmented generation over text chunks, the retrieved items are live Python objects with full type fidelity . This extends progressive disclosure from tool management to general workflow-state management.

2 https://agentskills.io , a portable specification adopted across the AI ecosystem including Claude Code, Gemini CLI, and Cursor.

Table 2: Performance comparison on the Tau 2 Benchmark across different models and domains (3 Runs). Bold indicates CaveAgent outperforms Function Calling. The number inside each cell represents the success rate of each task (%).

| Model                | Domain         | Function Calling   | Function Calling   | Function Calling   | Function Calling   | CaveAgent   | CaveAgent   | CaveAgent   | CaveAgent               |
|----------------------|----------------|--------------------|--------------------|--------------------|--------------------|-------------|-------------|-------------|-------------------------|
| Model                | Domain         | Run 1              | Run 2              | Run 3              | Avg.↑              | Run 1       | Run 2       | Run 3       | Avg.↑                   |
| Open Source          |                |                    |                    |                    |                    |             |             |             |                         |
| DeepSeek-V3.2 (685B) | Airline Retail | 56.0 79.8          | 56.0               | 54.0               | 55.3 77.2          | 62.0        | 60.0 82.5   | 58.0 78.1   | 60.0 (+4.7) 81.9 (+4.7) |
|                      |                |                    | 77.2               | 74.6               |                    | 85.1        |             |             |                         |
| Qwen3-Coder (30B)    | Airline        | 36.0               | 40.0               | 38.0               | 38.0               | 36.0        | 42.0        | 44.0        | 40.7 (+2.7)             |
|                      | Retail         | 41.2               | 43.0               | 39.5               | 41.2               | 51.8        | 54.4        | 57.9        | 54.7 (+13.5)            |
| Kimi-K2-0905 (1000B) | Airline        | 52.0               | 56.0               | 54.0               | 54.0               | 58.0        | 54.0        | 54.0        | 55.3 (+1.3)             |
|                      | Retail         | 62.3               | 60.5               | 59.6               | 60.8               | 69.3        | 72.8        | 71.9        | 71.3 (+10.5)            |
| Closed Source        |                |                    |                    |                    |                    |             |             |             |                         |
| Claude Sonnet 4.5    | Airline        | 56.0               | 54.0               | 62.0               | 57.3               | 56.0        | 52.0        | 62.0        | 56.7 (-0.6)             |
| Claude Sonnet 4.5    | Retail         | 68.4               | 67.5               | 81.6               | 72.5               | 73.7        | 75.4        | 80.7        | 76.6 (+4.1)             |
| GPT-5.1              | Airline        | 50.0               | 58.0               | 50.0               | 52.7               | 58.0        | 56.0        | 54.0        | 56.0 (+3.3)             |
| GPT-5.1              | Retail         | 64.0               | 66.7               | 66.7               | 65.8               | 65.8        | 69.3        | 73.6        | 69.6 (+3.8)             |
| Gemini 3 Pro         | Airline        | 64.0               | 62.0               | 58.0               | 61.3               | 68.0        | 68.0        | 68.0        | 68.0 (+6.7)             |
| Gemini 3 Pro         | Retail         | 72.8               | 72.8               | 66.7               | 70.8               | 77.2        | 76.3        | 75.4        | 76.3 (+5.5)             |

## 3. Experiments

In this section, we validate CaveAgent by answering four questions:

- [Q1.] Can CaveAgent perform on par with or surpass standard function-calling paradigms on widely-used benchmarks involving basic function-calling tasks? This is to showcase the basic function calling capabilities of CaveAgent.
- [Q2.] Can CaveAgent successfully perform state management across multi-turns correctly and efficiently?
- [Q3.] How token-efficient is CaveAgent compared to traditional JSON-based and Codeact style function calling?
- [Q4.] How does CaveAgent adapt to complex scenarios that require manipulating complex data objects? This is to showcase CaveAgent's unique advantages.

## 3.1. [Q1] Standard Function Calling Benchmarks

We evaluate on Tau 2 -bench (Yao et al., 2024) and BFCL (Patil et al.) using six SOTA LLMs: DeepSeek-V3.2 (685B MoE), Qwen3 Coder (30B MoE), Kimi K2 0905 (1000B MoE), Claude Sonnet 4.5 , GPT-5.1 , and Gemini 3 Pro . For each model, we compare its native function-calling mechanism against CaveAgent, where the LLM serves solely as a text generation engine bypassing internal function-calling modules. Temperature settings follow official recommendations.

## 3.1.1. Results on Tau 2 -bench

Tau 2 -bench evaluates multi-turn tool use in realistic conversational scenarios (Airline and Retail domains), requiring agents to maintain consistency across turns. We follow the original evaluation protocols with DeepSeek V3 as user simulator, testing each model three times per domain. Since CaveAgent executes Python code, we employ runtime instrumentation with wrapper functions to capture and compare function invocations against ground truth, ensuring fair cross-paradigm evaluation.

Performance Analysis. The results on Tau 2 -bench are summarized in Table 2. Key findings include:

(1). CaveAgent outperforms JSON-based function calling in 11 out of 12 settings across models from 30B to over 1000B parameters, with consistent improvements for DeepSeek-V3.2 ( +5.3% ) and Gemini 3 Pro ( +6.1% ), showing that offloading state management to a deterministic code runtime improves performance.

Table 3: BFCL Benchmark Summary (avg. of 3 runs). Bold indicates CaveAgent outperforms Function Calling.

| Model                      |   FC Avg.(%) |   CaveAgent Avg.(%) |    ∆ |
|----------------------------|--------------|---------------------|------|
| DeepSeek-V3.2 (685B)       |         86.9 |                94   |  7.1 |
| DeepSeek-V3.2 (w/o prompt) |         53.1 |                94   | 40.9 |
| Qwen3-Coder (30B)          |         89.8 |                94.4 |  4.6 |
| Kimi-K2-0905 (1000B)       |         89.2 |                94.7 |  5.5 |
| Claude Sonnet 4.5          |         94.4 |                94.4 |  0   |
| GPT-5.1                    |         89.6 |                88.9 | -0.7 |
| Gemini 3 Pro               |         94.3 |                94.3 |  0   |

(2). Gains are amplified in state-intensive Retail scenarios, where complex transaction modifications require maintaining state consistency across turns. CaveAgent achieves double-digit gains for Qwen3 and Kimi K2, validating that Stateful Runtime Management reduces serialization-induced errors (detailed trajectory analysis in Appendix G.1).

(3). The code-specialized Qwen3-Coder (30B) exhibits the largest improvement ( +13.5% in Retail), rivaling larger models. CaveAgent leverages the inherent coding proficiency of LLMs , allowing code-centric models to focus on logic generation rather than verbose context tracking.

## 3.1.2. Results on BFCL

To complement Tau 2 -bench's multi-turn evaluation, we assess atomic function-calling precision on the Berkeley Function Calling Leaderboard (BFCL) (Patil et al.), which comprises ∼ 2,000 question-function-answer pairs across four categories of increasing complexity: simple, multiple, parallel, and parallel-multiple function calling. We use Executable Evaluation (functional correctness) by executing generated code and comparing results against ground truth. The summary is shown in Table 3 (detailed per-run results in Appendix Table 7).

Performance Analysis. DeepSeek-V3.2 without explicit parallel-execution prompting achieves only 53.1% under the JSON paradigm due to its strong inductive bias toward sequential execution (Liu et al., 2025). CaveAgent achieves 94.0% without any prompt intervention, as Python code naturally supports parallel execution via independent statements while preserving inter-tool dependency reasoning. The 30B Qwen3-Coder with CaveAgent (94.4%) outperforms the much larger GPT-5.1 (89.6%) and matches Claude Sonnet 4.5 , demonstrating that CaveAgent leverages the coding proficiency of smaller LLMs. For SOTA models already at near-ceiling performance (Claude Sonnet 4.5, Gemini 3 Pro), gains are negligible since remaining errors stem from ambiguous queries rather than model incapacity. The advantages of our paradigm are most apparent in tasks requiring manipulation of complex data objects over long-horizon interactions, which we assess next.

## 3.2. [Q2] Case Study: Stateful Management

We design a benchmark targeting dimensions of state manipulation that existing benchmarks do not address, measuring an agent's ability to read, modify, and persist variables across turns. A key design principle is programmatic validation : we directly inspect runtime state after execution against ground-truth expectations, rather than parsing text outputs. For each dimension, we curate test cases with linearly dependent queries and initial variable states (see Appendix D). Results are shown in Table 4.

Type Proficiency evaluates manipulation of Python primitives, user-defined class instances, and scientific types (DataFrames, ndarrays). Results yield uniformly high scores (96.5%-100%), validating that code-based manipulation of complex types is tractable for current LLMs.

Multi-Variable tests how accuracy scales with 5-25 concurrent variables across five tiers (15 evaluation points each). Top models maintain 100% accuracy throughout, demonstrating that concurrent state management scales effectively within CaveAgent's architecture.

Multi-Turn assesses state persistence across 40-turn interactions in two scenarios: Smart Home (device state consistency) and Financial Account (numerical precision over multi-step operations). While DeepSeek-V3.2 maintains perfect accuracy,

Table 4: Stateful Management Benchmark Results (success rate %) across three evaluation dimensions. (1) For Type Proficiency, we designed 36, 36, and 42 cases for Simple, Object, and Scientific types, respectively. (2) For Multi-variable Stateful Management, we established 15 evaluation points for each variable-count tier. (3) For Multi-turn Stateful Management, we developed two scenarios, each consisting of 40 turns distributed across two conversations.

|               | Type Proficiency (%)   | Type Proficiency (%)   | Type Proficiency (%)   | Type Proficiency (%)   | Multi-Variable (%)   | Multi-Variable (%)   | Multi-Variable (%)   | Multi-Variable (%)   | Multi-Variable (%)   | Multi-Variable (%)   | Multi-Turn (%)   | Multi-Turn (%)   | Multi-Turn (%)   |
|---------------|------------------------|------------------------|------------------------|------------------------|----------------------|----------------------|----------------------|----------------------|----------------------|----------------------|------------------|------------------|------------------|
| Model         | Simple (36)            | Object (36)            | Sci. (42)              | Avg                    | 5V (15)              | 10V (15)             | 15V (15)             | 20V (15)             | 25V (15)             | Avg                  | Home (40)        | Fin. (40)        | Avg              |
| DeepSeek-V3.2 | 100                    | 100                    | 100                    | 100                    | 100                  | 100                  | 100                  | 100                  | 100                  | 100                  | 100              | 100              | 100              |
| Qwen3 Coder   | 100                    | 94.4                   | 95.2                   | 96.5                   | 94.4                 | 100                  | 80.0                 | 80.0                 | 100                  | 90.9                 | 77.5             | 85.0             | 81.3             |
| Kimi K2 0905  | 100                    | 100                    | 100                    | 100                    | 100                  | 100                  | 100                  | 100                  | 100                  | 100                  | 90.0             | 100              | 95.0             |
| Gemini 3 Pro  | 100                    | 100                    | 100                    | 100                    | 100                  | 100                  | 100                  | 100                  | 100                  | 100                  | 97.5             | 100              | 98.7             |

<!-- image -->

- (a) Compare Execution steps and task success rates.

(b) Compare Prompt tokens and completion tokens.

Figure 5: Performance comparison between CaveAgent and traditional JSON-based Function Calling across three scenarios, focusing on execution steps, success rates, prompt token and completion token consumptions. Here, the steps means the number of turns needed for task completion, prompt tokens refers to the cumulative input tokens sent to the model across all turns (including system prompts, conversation history, and tool results), and completion tokens refers to the cumulative output tokens generated by the model (including reasoning, function calls, or code generation). The Func Call in right figure represents the abbrevation of Function Calling.

other models exhibit degradation on long-horizon state tracking. The consistently high accuracy across top models validates our thesis: when LLMs interact through code with persistent runtime state, reliable and verifiable agent behavior becomes achievable.

## 3.3. [Q3] Token Efficiency Study

We evaluate token efficiency across three domains (IoT, finance, e-commerce) with logically interdependent tool operations using DeepSeek V3.2. Figure 5 and Table 5 show results. CaveAgent achieves 28.4% lower total token consumption (504K vs. 704K) while improving success rate from 94.6% to 100%. The gain stems from resolving multiple dependencies in single code executions, reducing steps from 236 to 145 and prompt tokens by 32.7%. While CaveAgent consumes 36.3% more completion tokens (code is more verbose than JSON), prompt tokens dominate overall consumption and accumulate across turns.

Table 5: Summary of Improvements

| Metric            | CaveAgent   | Function Calling   | Improvement   |
|-------------------|-------------|--------------------|---------------|
| Total Steps       | 145         | 236                | -38.6%        |
| Prompt Tokens     | 444,679     | 660,588            | -32.7%        |
| Completion Tokens | 59,440      | 43,600             | +36.3%        |
| Total Tokens      | 504,119     | 704,188            | -28.4%        |
| Avg. Success Rate | 100%        | 94.6%              | +5.4 pp       |

Table 6: Performance Comparison of three Function Calling paradigms across three task categories. CaveAgent (highlighted in green) achieves superior performance. Improvements relative to the best baseline are marked in parentheses.

| Task Category   | Method                             | Success Rate ↑            | Prompt Tokens ↓         | Compl. Tokens ↓     | Total Tokens ↓                 |
|-----------------|------------------------------------|---------------------------|-------------------------|---------------------|--------------------------------|
| Data Query      | CaveAgent CodeAct Style JSON-based | 100.0% (+20%) 80.0% 80.0% | 118,901 232,990 278,239 | 4,584 17,219 16,413 | 123,485 (-51%) 250,209 294,652 |
| Data Query      | FC                                 |                           |                         |                     |                                |
| Data Analysis   | CaveAgent                          | 100.0% (Tie)              | 110,550                 | 5,832               | 116,382 (-2%)                  |
| Data Analysis   | CodeAct Style                      | 100.0%                    | 112,990                 | 6,232               | 119,222                        |
| Data Analysis   | JSON-based FC                      | 10.0%                     | 1,328,779               | 8,024               | 1,336,803                      |
| Visualization   | CaveAgent                          | 90.0% (+50%)              | 374,855                 | 30,250              | 405,105 (-39%)                 |
| Visualization   | CodeAct Style                      | 40.0%                     | 957,447                 | 43,144              | 1,000,591                      |
| Visualization   | JSON-based FC                      | 30.0%                     | 644,778                 | 17,899              | 662,677                        |

## 3.4. [Q4] Case Study: Data-intensive Scenario

We evaluate three architectures on a data-intensive benchmark comprising 30 tasks across data query, analysis, and visualization using stock market data (Apple and Google, 2020-2025). CodeAct Style disables CaveAgent's variable injection/retrieval; JSON-based Function Calling operates without code execution. Results are shown in Table 6.

Results. On Data Query , CaveAgent achieved 100% accuracy (123K tokens) by storing results in runtime variables, while both baselines failed at 80% due to context overflow from serializing large datasets. On Data Analysis , CaveAgent and CodeAct both achieved 100% with comparable tokens ( ∼ 116-119K), but Function Calling managed only 10% (1.3M tokens) without code execution. On Visualization , CaveAgent achieved 90% (405K tokens) by retrieving chart data from runtime variables; CodeAct reached 40% (1M tokens) and Function Calling 30% (662K tokens). These results demonstrate that decoupling intermediate state from prompt context avoids the token accumulation causing context overflow in conventional architectures, with advantages growing with task complexity and data volume.

## 4. Conclusion

We present CaveAgent , a framework that transforms LLM tool use from stateless JSON function calling to persistent, object-oriented stateful runtime management. CaveAgent enables agents to maintain high-fidelity memory of complex objects and execute sophisticated logic via Python code. By extending the Agent Skills open standard with runtime injection, CaveAgent demonstrates that the persistent runtime paradigm enables a new mode of tool distribution, skills that deliver executable artifacts rather than solely textual instructions, unifying tool registration and skill management into a single portable mechanism. Experiments on Tau 2 -bench show that this approach outperforms SOTA baselines in multi-turn success rates (+10.5%) and token efficiency. Beyond performance gains, a key contribution is the programmatic verifiability enabled by CaveAgent's architecture: because runtime state is deterministically inspectable, agent behavior can be evaluated automatically without human annotation, establishing a structural foundation for Reinforcement Learning with Verifiable Rewards and runtime-mediated multi-agent coordination. Qualitative case studies are provided in Appendix G.

Limitations. CaveAgent requires tools to be Python-callable or wrapped as Python functions; tools with non-Python interfaces (e.g., native binaries, proprietary REST APIs) require manual wrapper creation. The persistent runtime consumes memory proportional to the complexity of stored objects; for extremely long sessions with large data artifacts, memory management becomes a concern. While agent behavior is in principle programmatically verifiable, designing comprehensive fine-grained benchmarks for stateful evaluation remains future work. Finally, the multi-agent coordination capabilities are demonstrated qualitatively; rigorous quantitative evaluation of runtime-mediated multi-agent systems is left for future investigation.

## 5. Background and Related Work

## 5.1. Tool Learning &amp; Function Calling

The foundational approach to LLM tool use relies on a JSON-centric paradigm. The ReAct framework (Yao et al., 2022b) generates actions as free-text strings requiring heuristic parsing, making it prone to hallucination and format errors (Patil et al., 2024, Liu et al., 2023). To address this, JSON-Schema function calling (Patil et al., 2024, Qin et al., 2023) constrains action generation to structured schemas, formalized by GPT-4 Function Calling and adopted by frameworks such as AutoGen (Wu et al., 2024). Constrained decoding methods like xGrammar (Dong et al., 2025) can further enforce syntactically valid JSON at low overhead. However, format correctness does not resolve deeper architectural limitations: JSON is a static interchange format lacking native control flow, its verbose syntax incurs token overhead (Wang et al., 2024), and rigid schemas increase the probability of hallucination on complex nested structures (Patil et al., 2024). The entire pipeline operates as a text-based serialization loop : the schema is flattened into the prompt, the LLM predicts a JSON payload, and middleware serializes results back into text, inheriting context explosion and error propagation (Packer et al., 2023, Wang et al., 2023b).

## 5.2. Code as Action &amp; Programmatic Reasoning

Recognizing these limitations, the 'Code as Action' paradigm uses executable Python as a unified medium for reasoning and tool invocation. Wang et al. (2024) proposed CodeAct , reducing multi-turn overhead by up to 30% and improving task success rates by 20%. This shift leverages the Turing-complete nature of code to express loops, conditionals, and variable dependencies. The paradigm extends to domain-specific reasoning: ViperGPT (Surís et al., 2023) composes vision modules into executable subroutines, while Program of Thoughts (Chen et al., 2022) and PAL (Gao et al., 2023) delegate arithmetic and symbolic logic to a Python interpreter. However, current code agents retain architectural limitations: CodeAct provides no explicit APIs for external object injection or retrieval, relying on a textualization bottleneck where intermediate states must be serialized via print and external data must be loaded through file I/O (Wang et al., 2024). This makes it difficult to inject pre-existing Python objects (e.g., in-memory DataFrames, trained models) and risks context explosion with complex artifacts (Liu et al., 2024, Packer et al., 2023).

## 5.3. Context Management &amp; Stateful Architectures

Packer et al. (2023) introduced MemGPT , an OS-inspired virtual context management system with tiered memory for long-horizon tasks. Qiao et al. (2023) proposed TaskWeaver , a code-first framework preserving data structures across turns. However, existing approaches rely on RAG or textual summarization, inherently lossy methods that strip complex runtime objects of structural integrity and executable properties. CaveAgent uses Variable Injection to treat the Python runtime itself as high-fidelity external memory, allowing variables to persist in their native object form without re-tokenization overhead.

## 5.4. Multi-Agent Coordination

Li et al. (2023) proposed CAMEL for role-playing cooperation; Qian et al. (2024) introduced ChatDev with chat-chain workflows; Hong et al. (2023) developed MetaGPT encoding SOPs into prompts. These frameworks rely on text-based message passing , introducing serialization bottlenecks when transferring complex state between agents. CaveAgent enables Runtime-Mediated State Flow : agents collaborate by directly injecting and retrieving variables in a shared runtime, shifting coordination from 'communication by talking' to 'communication by shared state.'

Figure 6 illustrates the evolution of paradigm shifts in agentic tool use that motivates our work.

## 6. Acknowledgment

- We thank Rui Zhou, a professional UI designer at Metasequoia Tech, for his assistance with the figure design in this paper.
- We thank Qiuyang Mang, a Ph.D student in Computer Science at UC Berkeley, for the discussion about the core design of our framework.

## References

- Josh Achiam, Steven Adler, Sandhini Agarwal, Lama Ahmad, Ilge Akkaya, Florencia Leoni Aleman, Diogo Almeida, Janko Altenschmidt, Sam Altman, Shyamal Anadkat, et al. Gpt-4 technical report. arXiv preprint arXiv:2303.08774 , 2023.
- Daniil A Boiko, Robert MacKnight, and Gabe Gomes. Emergent autonomous scientific research capabilities of large language models. arXiv preprint arXiv:2304.05332 , 2023.
- Andres M Bran, Sam Cox, Oliver Schilter, Carlo Baldassari, Andrew D White, and Philippe Schwaller. Chemcrow: Augmenting large-language models with chemistry tools. arXiv preprint arXiv:2304.05376 , 2023.
- Wenhu Chen, Xueguang Ma, Xinyi Wang, and William W Cohen. Program of thoughts prompting: Disentangling computation from reasoning for numerical reasoning tasks. arXiv preprint arXiv:2211.12588 , 2022.
- Yixin Dong, Charlie F Ruan, Yaxing Cai, Ziyi Xu, Yilong Zhao, Ruihang Lai, and Tianqi Chen. Xgrammar: Flexible and efficient structured generation engine for large language models. Proceedings of Machine Learning and Systems , 7, 2025.
- Danny Driess, Fei Xia, Mehdi SM Sajjadi, Corey Lynch, Aakanksha Chowdhery, Ayzaan Wahid, Jonathan Tompson, Quan Vuong, Tianhe Yu, Wenlong Huang, et al. Palm-e: An embodied multimodal language model. 2023.
- Luyu Gao, Aman Madaan, Shuyan Zhou, Uri Alon, Pengfei Liu, Yiming Yang, Jamie Callan, and Graham Neubig. Pal: Program-aided language models. In International Conference on Machine Learning , pages 10764-10799. PMLR, 2023.
- Sirui Hong, Mingchen Zhuge, Jonathan Chen, Xiawu Zheng, Yuheng Cheng, Jinlin Wang, Ceyao Zhang, Zili Wang, Steven Ka Shing Yau, Zijuan Lin, et al. Metagpt: Meta programming for a multi-agent collaborative framework. In The Twelfth International Conference on Learning Representations , 2023.
- Geunwoo Kim, Pierre Baldi, and Stephen McAleer. Language models can solve computer tasks. Advances in Neural Information Processing Systems , 36:39648-39677, 2023.
- Guohao Li, Hasan Hammoud, Hani Itani, Dmitrii Khizbullin, and Bernard Ghanem. Camel: Communicative agents for" mind" exploration of large language model society. Advances in Neural Information Processing Systems , 36:51991-52008, 2023.
- Aixin Liu, Aoxue Mei, Bangcai Lin, Bing Xue, Bingxuan Wang, Bingzheng Xu, Bochao Wu, Bowei Zhang, Chaofan Lin, Chen Dong, et al. Deepseek-v3. 2: Pushing the frontier of open large language models. arXiv preprint arXiv:2512.02556 , 2025.
- Nelson F Liu, Kevin Lin, John Hewitt, Ashwin Paranjape, Michele Bevilacqua, Fabio Petroni, and Percy Liang. Lost in the middle: How language models use long contexts. Transactions of the Association for Computational Linguistics , 12:157-173, 2024.
- Xiao Liu, Hao Yu, Hanchen Zhang, Yifan Xu, Xuanyu Lei, Hanyu Lai, Yu Gu, Hangliang Ding, Kaiwen Men, Kejuan Yang, et al. Agentbench: Evaluating llms as agents. arXiv preprint arXiv:2308.03688 , 2023.
- Jiarui Lu, Thomas Holleis, Yizhe Zhang, Bernhard Aumayer, Feng Nan, Felix Bai, Shuang Ma, Shen Ma, Mengyu Li, Guoli Yin, Zirui Wang, and Ruoming Pang. Toolsandbox: A stateful, conversational, interactive evaluation benchmark for llm tool use capabilities, 2025. URL https://arxiv.org/abs/2408.04682 .
- Pan Lu, Baolin Peng, Hao Cheng, Michel Galley, Kai-Wei Chang, Ying Nian Wu, Song-Chun Zhu, and Jianfeng Gao. Chameleon: Plug-and-play compositional reasoning with large language models. Advances in Neural Information Processing Systems , 36:43447-43478, 2023.
- Yun Luo, Zhen Yang, Fandong Meng, Yafu Li, Jie Zhou, and Yue Zhang. An empirical study of catastrophic forgetting in large language models during continual fine-tuning. IEEE Transactions on Audio, Speech and Language Processing , 2025.
- Long Ouyang, Jeffrey Wu, Xu Jiang, Diogo Almeida, Carroll Wainwright, Pamela Mishkin, Chong Zhang, Sandhini Agarwal, Katarina Slama, Alex Ray, et al. Training language models to follow instructions with human feedback. Advances in neural information processing systems , 35:27730-27744, 2022.
- Charles Packer, Vivian Fang, Shishir\_G Patil, Kevin Lin, Sarah Wooders, and Joseph\_E Gonzalez. Memgpt: Towards llms as operating systems. 2023.

- Joon Sung Park, Joseph O'Brien, Carrie Jun Cai, Meredith Ringel Morris, Percy Liang, and Michael S Bernstein. Generative agents: Interactive simulacra of human behavior. In Proceedings of the 36th annual acm symposium on user interface software and technology , pages 1-22, 2023.
- Shishir G Patil, Huanzhi Mao, Fanjia Yan, Charlie Cheng-Jie Ji, Vishnu Suresh, Ion Stoica, and Joseph E Gonzalez. The berkeley function calling leaderboard (bfcl): From tool use to agentic evaluation of large language models. In Forty-second International Conference on Machine Learning .
- Shishir G Patil, Tianjun Zhang, Xin Wang, and Joseph E Gonzalez. Gorilla: Large language model connected with massive apis. Advances in Neural Information Processing Systems , 37:126544-126565, 2024.
- Chen Qian, Wei Liu, Hongzhang Liu, Nuo Chen, Yufan Dang, Jiahao Li, Cheng Yang, Weize Chen, Yusheng Su, Xin Cong, et al. Chatdev: Communicative agents for software development. In Proceedings of the 62nd Annual Meeting of the Association for Computational Linguistics (Volume 1: Long Papers) , pages 15174-15186, 2024.
- Bo Qiao, Liqun Li, Xu Zhang, Shilin He, Yu Kang, Chaoyun Zhang, Fangkai Yang, Hang Dong, Jue Zhang, Lu Wang, et al. Taskweaver: A code-first agent framework. arXiv preprint arXiv:2311.17541 , 2023.
- Yujia Qin, Shihao Liang, Yining Ye, Kunlun Zhu, Lan Yan, Yaxi Lu, Yankai Lin, Xin Cong, Xiangru Tang, Bill Qian, et al. Toolllm: Facilitating large language models to master 16000+ real-world apis. arXiv preprint arXiv:2307.16789 , 2023.
- Changle Qu, Sunhao Dai, Xiaochi Wei, Hengyi Cai, Shuaiqiang Wang, Dawei Yin, Jun Xu, and Ji-Rong Wen. Tool learning with large language models: A survey. Frontiers of Computer Science , 19(8):198343, 2025.
- Yongliang Shen, Kaitao Song, Xu Tan, Dongsheng Li, Weiming Lu, and Yueting Zhuang. Hugginggpt: Solving ai tasks with chatgpt and its friends in hugging face. Advances in Neural Information Processing Systems , 36:38154-38180, 2023.
- Dídac Surís, Sachit Menon, and Carl Vondrick. Vipergpt: Visual inference via python execution for reasoning. In Proceedings of the IEEE/CVF international conference on computer vision , pages 11888-11898, 2023.
- Xingyao Wang, Zihan Wang, Jiateng Liu, Yangyi Chen, Lifan Yuan, Hao Peng, and Heng Ji. Mint: Evaluating llms in multi-turn interaction with tools and language feedback. arXiv preprint arXiv:2309.10691 , 2023a.
- Xingyao Wang, Zihan Wang, Jiateng Liu, Yangyi Chen, Lifan Yuan, Hao Peng, and Heng Ji. Mint: Evaluating llms in multi-turn interaction with tools and language feedback. arXiv preprint arXiv:2309.10691 , 2023b.
- Xingyao Wang, Yangyi Chen, Lifan Yuan, Yizhe Zhang, Yunzhu Li, Hao Peng, and Heng Ji. Executable code actions elicit better llm agents. In Forty-first International Conference on Machine Learning , 2024.
- Qingyun Wu, Gagan Bansal, Jieyu Zhang, Yiran Wu, Beibin Li, Erkang Zhu, Li Jiang, Xiaoyun Zhang, Shaokun Zhang, Jiale Liu, et al. Autogen: Enabling next-gen llm applications via multi-agent conversations. In First Conference on Language Modeling , 2024.
- Ke Yang, Jiateng Liu, John Wu, Chaoqi Yang, Yi R Fung, Sha Li, Zixuan Huang, Xu Cao, Xingyao Wang, Yiquan Wang, et al. If llm is the wizard, then code is the wand: A survey on how code empowers large language models to serve as intelligent agents. arXiv preprint arXiv:2401.00812 , 2024.
- Shunyu Yao, Howard Chen, John Yang, and Karthik Narasimhan. Webshop: Towards scalable real-world web interaction with grounded language agents. Advances in Neural Information Processing Systems , 35:20744-20757, 2022a.
- Shunyu Yao, Jeffrey Zhao, Dian Yu, Nan Du, Izhak Shafran, Karthik R Narasimhan, and Yuan Cao. React: Synergizing reasoning and acting in language models. In The eleventh international conference on learning representations , 2022b.
- Shunyu Yao, Noah Shinn, Pedram Razavi, and Karthik Narasimhan. tau -bench: A benchmark for tool-agent-user interaction in real-world domains. arXiv preprint arXiv:2406.12045 , 2024.
- Shuyan Zhou, Frank F Xu, Hao Zhu, Xuhui Zhou, Robert Lo, Abishek Sridhar, Xianyi Cheng, Tianyue Ou, Yonatan Bisk, Daniel Fried, et al. Webarena: A realistic web environment for building autonomous agents. arXiv preprint arXiv:2307.13854 , 2023.
- Brianna Zitkovich, Tianhe Yu, Sichun Xu, Peng Xu, Ted Xiao, Fei Xia, Jialin Wu, Paul Wohlhart, Stefan Welker, Ayzaan Wahid, et al. Rt-2: Vision-language-action models transfer web knowledge to robotic control. In Conference on Robot Learning , pages 2165-2183. PMLR, 2023.

## Appendix

| A Pseudo Code   | A Pseudo Code                                      | A Pseudo Code                                      |   15 |
|-----------------|----------------------------------------------------|----------------------------------------------------|------|
| B               | What Happens in Semantic Stream                    | What Happens in Semantic Stream                    |   16 |
|                 | B.1                                                | System Prompt Construction . . . . . .             |   16 |
|                 | B.2                                                | Context Injection Format . . . . . . . .           |   17 |
|                 | B.3                                                | Runtime Feedback Prompts . . . . . .               |   18 |
| C               | What Happened in Runtime Stream                    | What Happened in Runtime Stream                    |   19 |
|                 | C.1                                                | Environment Initialization via Injection           |   19 |
|                 | C.2                                                | The Interleaved Execution Paradigm .               |   19 |
|                 | C.3                                                | Illustrative Case Study . . . . . . . . .          |   20 |
| D               | Test Cases in Stateful Management Benchmark        | Test Cases in Stateful Management Benchmark        |   21 |
|                 | D.1                                                | Type Proficiency Cases . . . . . . . . .           |   21 |
|                 | D.2                                                | Multi-variable Cases . . . . . . . . . .           |   21 |
|                 | D.3                                                | Multi-turn Cases . . . . . . . . . . . .           |   24 |
| E               | Stateful Runtime-Mediated Multi-Agent Coordination | Stateful Runtime-Mediated Multi-Agent Coordination |   25 |
|                 | E.1                                                | Meta-Agent Runtime Control . . . . .               |   25 |
|                 | E.2                                                | State-Mediated Communication . . . .               |   26 |
|                 | E.3                                                | Shared-Runtime Synchronization . . .               |   26 |
|                 | E.4                                                | AutoML Training Loop . . . . . . . . .             |   26 |
| F               | Detailed BFCL Benchmark Results                    | Detailed BFCL Benchmark Results                    |   26 |
| G               | Features                                           | Features                                           |   26 |
|                 | G.1                                                | Case Analysis in Tau 2 -bench . . . . . .          |   26 |
|                 | G.2                                                | Smart Home . . . . . . . . . . . . . . .           |   29 |
|                 | G.3                                                | Geospatial Analysis . . . . . . . . . . .          |   29 |

## Algorithm 1 CaveAgent Interaction Loop

̸

```
Require: Query q , Tools T , Max Turns T max 1: S 0 ← InitKernel(); Inject( S 0 , T ) ▷ Init Runtime Stream 2: D ← GenSigs( T ); H 0 ←{ Sys ( D ) , User ( q ) } ▷ Init Semantic Stream 3: for t = 1 to T max do 4: Phase 1 (Reasoning): Rt ← LLM ( Ht -1 ) ▷ Sample thought & code 5: if Rt contains code block ct then 6: Phase 2 (Security): V ← ASTCheck ( ct , Π ) ▷ Pre-exec validation 7: if V = / 0 then 8: ot ← FormatError ( V ) 9: else 10: Phase 3 (Execution): ot , S t ← Run ( S t -1 , ct ) ▷ Stateful update 11: Phase 4 (Shaping): ot ← Shape ( ot , L max ) ▷ Truncate & format 12: end if 13: Ht ← Ht -1 ∪{ ( Rt , ot ) } ▷ Sync observation to history 14: else 15: return Rt ▷ Output final answer 16: end if 17: end for 18: return "Max steps reached"
```

## A. Pseudo Code

Algorithm 1 shows the general workflow of CaveAgent.

<!-- image -->

Stateless,Text-Only I/0

Stateless Execution, Text-Only I/0

Figure 6: Evolution of Agentic Tool Use

Stateful Execution,Text&amp; Object I/O

## B. What Happens in Semantic Stream

The following sections detail the prompt templates used to instruct the Semantic Stream in CaveAgent. The system prompt is dynamically constructed by combining the Agent Identity, Context Information (functions, variables, types), and Instructions.

## B.1. System Prompt Construction

The full system prompt is composed using the following template structure. The placeholders (e.g., {functions} ) are populated at runtime with the specific tools and variables available in the current environment.

## System Prompt Template

```
{current_time}
```

```
{agent_identity} Current time: You have access to: <functions> {functions} </functions> <variables> {variables} </variables> <types> {types} </types> Instructions: {instructions} {additional_context}
```

Below are the default values for the key components referenced in the template above.

## Component: Agent Identity

You are a tool-augmented agent specializing in Python programming that enables function-calling through LLM code generation. You have to leverage your coding capabilities to interact with tools through a Python runtime environment, allowing direct access to execution results and runtime state. The user will give you a task and you should solve it by writing Python code in the Python environment provided.

## Component: Core Instructions

1. Carefully read and analyze the user's input.
2. If the task requires Python code: - Generate appropriate Python code to address the user's request. - Your code will then be executed in a Python environment, and the execution result will be returned to you as input for the next step.
- During each intermediate step, you can use 'print()' to save whatever important information you will then need in the following steps. - These print outputs will then be given to you as input for the next step. - Review the result and generate additional code as needed until the task is completed.
3. CRITICAL EXECUTION CONTEXT: You are operating in a persistent Jupyter-like environment where: - Each code block you write is executed in a new cell within the SAME continuous session - ALL variables, functions, and imports persist across cells automatically - You can directly reference any variable created in previous cells without using locals(), globals(), or any special access methods.
4. If the task doesn't require Python code, provide a direct answer based on your knowledge.
5. Always provide your final answer in plain text, not as a code block.
6. You must not perform any calculations or operations yourself, even for simple tasks like sorting or addition.
7. Write your code in a {python\_block\_identifier} code block. In each step, write all your code in only one block.
8. Never predict, simulate, or fabricate code execution results.
9. To solve the task, you must plan forward to proceed in a series of steps, in a cycle of Thought and Code sequences.

## B.2. Context Injection Format

Examples of how context is formatted for the LLM.

## Example: Function Injection

- &lt;functions&gt;

- function: buy\_stock(symbol: str, quantity: int) -&gt; Transaction description: Executes a stock purchase for the current portfolio.

doc:

Args:

symbol: The ticker symbol of the stock (e.g., 'AAPL').

quantity: The number of shares to purchase.

Returns:

A Transaction object recording the details of the purchase.

- &lt;/functions&gt;

## Example: Variable Injection

## &lt;variables&gt;

- name: portfolio

type: Portfolio

description: The user's current investment portfolio object.

- name: market\_data

type: DataFrame

description: A pandas DataFrame containing historical price data.

- &lt;/variables&gt;

```
Example: Type Schema Injection <types> Portfolio: doc: Manages a collection of stock holdings and cash balance. methods: - get_total_value() -> float - get_holdings() -> Dict[str, int] - add_cash(amount: float) -> None Transaction: doc: An immutable record of a stock transaction. fields: - id: str - symbol: str - quantity: int - price_at_execution: float - timestamp: datetime </types>
```

## B.3. Runtime Feedback Prompts

The agent operates in a closed feedback loop. After each code execution step, the runtime environment captures the output (stdout or errors) and constructs a new user message to guide the agent's next action.

## B.3.1. Standard Execution Output

This prompt is used when code executes successfully. It provides the standard output and explicitly reminds the agent that the variable state has been preserved.

## Execution Output Template

```
<execution_output> {execution_output} </execution_output>
```

IMPORTANT CONTEXT REMINDER: - Based on this output, should we continue with more operations?

- If the output includes an error, please review the error carefully and modify your code to fix the error if needed.
- If yes, provide the next code block. If no, provide the final answer (not as a code block).
- You are in the SAME Jupyter-like session. All variables from your previous code blocks are still available and can be accessed directly by name.
- You DO NOT need to use locals(), globals(), or any special methods to access them.
- Think of this exactly like working in Jupyter: when you create a variable in cell 1, you can simply use it by name in cell 2, 3, 4, etc.

## B.3.2. Error Handling &amp; Constraints

The system includes specific templates for handling edge cases, such as context window limits and security violations.

Output Length Exceeded: Used when the code generates excessive output (e.g., printing a massive DataFrame), prompting the agent to summarize instead.

## Output Truncation Template

The code execution generated output\_length characters of output, which exceeds the maximum limit of max\_length characters. Please modify your code to:

1. Avoid printing large datasets or lengthy content
2. Use summary statistics instead of full data (e.g., print shape, head(), describe() for dataframes)
3. Print only essential information needed for the task """

Security Violation: Used when the static analysis security checker blocks unsafe code (e.g., os.system ).

## Security Error Template

&lt;security\_error&gt;

{error}

&lt;/security\_error&gt;

Code blocked for security reasons. Please modify your code to avoid this violation.

## C. What Happened in Runtime Stream

While the Semantic Stream governs reasoning and planning, the Runtime Stream serves as the execution engine and persistent memory. This stream operates as a dedicated Python kernel where data manipulation, tool invocation, and state transitions occur. The two streams follow a strict chronological topology, synchronized through interleaved exchange of code instructions and execution feedback.

## C.1. Environment Initialization via Injection

The runtime lifecycle begins with Context Injection . Before the reasoning cycle starts, the user (or the system orchestration layer) initializes the runtime environment by injecting native Python objects directly into the global namespace.

- Function Injection: Tool definitions are loaded as executable Python callables. Unlike RESTful API wrappers, these are native functions that can be inspected and invoked directly.
- Variable Injection: Domain-specific data, such as DataFrames, graph structures, or class instances, are instantiated within the runtime stream's memory.

This initialization phase populates the &lt;functions&gt; and &lt;variables&gt; blocks described in Section B.

## C.2. The Interleaved Execution Paradigm

Once initialized, the workflow proceeds as a synchronized dialogue between the Semantic Stream (Reasoning) and the Runtime Stream (Execution). We conceptualize this as a dual-column timeline where actions are interleaved strictly in chronological order:

1. Semantic Turn (Left Cell): The LLM analyzes the current task and available context. It generates a Thought followed by a discrete Code Block (the instruction). This represents the input to the runtime.
2. Runtime Turn (Right Cell): The system extracts the code block and executes it within the persistent Python kernel. This execution constitutes the state transition St → St + 1 . Crucially, this is not a stateless function call; it is a stateful operation where:
- New variables defined in this cell are persisted in memory.
- Existing objects (e.g., a list or a database connection) are mutated in place.
- Side effects (e.g., saving a file) are realized immediately.
3. Feedback Loop: Upon completion of the Runtime Turn, the standard output ( stdout ), standard error ( stderr ), or the return value of the last expression is captured. This raw execution result is wrapped in the &lt;execution\_output&gt; tags and injected back into the Semantic Stream, triggering the next Semantic Turn.

This mechanism ensures that the agent's reasoning is always grounded in the current, actual state of the runtime environment.

## C.3. Illustrative Case Study

To intuitively demonstrate the temporal synchronization and state dependency between the two streams, we present a concrete walkthrough in Figure 7. This example illustrates a toy data analysis task where the agent must filter a dataset and perform calculations on the result.

The workflow proceeds in a 'zig-zag' pattern, alternating between reasoning (Left) and execution (Right):

1. Initialization ( T 0 ): The user injects a pandas DataFrame named df . Note that the Semantic Stream only receives a lightweight pointer (variable name and documentation) instead of the whole data, while the Runtime Stream holds the actual heavy data object in memory.
2. Step 1 ( T 1 → T 2 ): The agent generates code to filter the data. Crucially, the Runtime Stream does not return the full filtered dataset as text. Instead, it creates a new variable high\_vol in the local scope and returns only a status update. This exemplifies our Stateful Management : the 'result' of the tool use is a state change in memory, not a text string.
3. Step 2 ( T 3 → T 4 ): The agent references the previously created variable high\_vol to compute a statistic. This demonstrates Context Compression : the agent manipulates the data via variable references without ever consuming context tokens to 'read' the full dataset.

## Timeline of Interleaved Execution (Semantic vs. Runtime)

Figure 7: A visualization of the CaveAgent workflow. The process alternates between the Semantic Stream (generating instructions) and the Runtime Stream (executing and maintaining state). Note how the variable high\_vol is maintained in the Runtime Stream ( T 2 ) and accessed in the subsequent step ( T 4 ) without re-loading or serialization, illustrating the efficiency of Stateful Runtime Management.

| Time   | Semantic Stream (Reasoning & Code)                                                                                                                                                                                   | Runtime Stream (State & Execution)                                                                                                                 |
|--------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------|
| T 0    | (Waiting for Input)                                                                                                                                                                                                  | [Initialization] • df = pd.read_csv("market.csv") loaded into memory. • <variables> metadata sent to Left Stream.                                  |
| T 1    | User: 'Find stocks with volume > 1M.' Agent Thought: I need to filter the dataframe df . I will save the result to a new variable. Code: high_vol = df[df['volume'] > 1e6] print(f"Filtered. Rows: {len(high_vol)}") | (Waiting for Instruction)                                                                                                                          |
| T 2    | (Waiting for Feedback)                                                                                                                                                                                               | [Execution] 1. Evaluates filter logic on df . 2. New State: Variable high_vol created in RAM. 3. Output: 'Filtered. Rows: 42' sent to Left Stream. |
| T 3    | Input: 'Filtered. Rows: 42' Agent Thought: Now I need to calculate the average price of these high-volume stocks. Code: avg_price = high_vol['price'].mean() print(round(avg_price, 2))                              | (Waiting for Instruction)                                                                                                                          |
| T 4    | (Waiting for Feedback)                                                                                                                                                                                               | [Execution] 1. Accesses persistent object high_vol . 2. Computes mean. 3. Output: 154.20 sent to Left Stream.                                      |

Viewing the runtime stream as a Jupyter notebook with multiple cells, where each cell corresponds to the execution at each time step, helps illustrate how states remain persistent across steps.

## D. Test Cases in Stateful Management Benchmark

In this section, we provide the examples of our test cases in Stateful Management Benchmark.

## D.1. Type Proficiency Cases

The Type Proficiency category evaluates the agent's ability to perform precise, state-aware manipulation of Python runtime elements. This section tests the agent's working memory across three structural tiers: Simple Types (primitive types such as lists, dictionaries, and strings), Object Types (custom classes), and Scientific Types (high-dimensional complex data). Proficiency in these domains is a prerequisite for complex reasoning tasks.

## D.1.1. Simple Types

Figure 8 shows the examples of our test cases of Simple types.

Figure 8: Illustration of test cases of Simple Types. The results show the agent's capability to manipulate any object types.

| Simple Types            | Simple Types                                                                            | Simple Types                                                                                                              |
|-------------------------|-----------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------|
| Turn                    | User Query (Input)                                                                      | Immediate Validation (State Assertion)                                                                                    |
| Case: string_split_join | Case: string_split_join                                                                 | Case: string_split_join                                                                                                   |
| T1                      | 'Set text to 'a,b,c', split by comma, and rejoin with ' &#124; ' as separator...'       | validate_str_split • Assert text == "a &#124; b &#124; c".                                                                |
| T2                      | 'Sort the parts of text alphabetically while keeping the ' &#124; ' separator format.'  | validate_str_sort • Assert text remains "a &#124; b &#124; c". • Checks persistence of structure.                         |
| T3                      | 'Reverse the order of parts in text but keep the ' &#124; ' separator...'               | validate_str_reverse • Assert text == "c &#124; b &#124; a".                                                              |
| Case: dict_nested       | Case: dict_nested                                                                       | Case: dict_nested                                                                                                         |
| T1                      | 'Change the math score to 90 in data['scores']['math'].'                                | validate_dict_nested_update • Assert data['scores']['math'] == 90.                                                        |
| T2                      | 'The student just took a science test. Add a science score of 88 to data['scores'].'    | validate_dict_nested_add • Assert key 'science' exists with value 88.                                                     |
| T3                      | 'There was a curve on all tests. Add 5 points to every score in the scores dictionary.' | validate_dict_increment • Assert math == 95 (90+5). • Assert science == 93 (88+5). • Assert english == 95 (Initial 90+5). |

## D.1.2. Object Types

Figure 9 shows the examples of our test cases of Object types.

## D.1.3. Scientific Types

Figure 10 shows the examples of our test cases of Scientific types.

## D.2. Multi-variable Cases

Since there are 5 tiers of variable numbers, we select the variable number = 20 to demonstrate our test case since different variable number shares similar patterns of test cases. Figure 11 shows one example of test case where the agent is required to process 20 variables in 3 turns.

Figure 9: Illustration of test cases of Object Types. The results show the agent's capability to manipulate custom class instances (Stack, ShoppingCart, Person) and verifying their internal attributes and method side-effects.

| Object Types         | Object Types                                                                                       | Object Types                                                                                   |
|----------------------|----------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------|
| Turn                 | User Query (Input)                                                                                 | Immediate Validation (State Assertion)                                                         |
| Case: stack_advanced | Case: stack_advanced                                                                               | Case: stack_advanced                                                                           |
| T1                   | 'Push 'A', 'B', 'C', 'D' in order.'                                                                | validate_stack_multi_push • Assert stack.size() == 4 .                                         |
| T2                   | 'User wants to go back to first page. Pop until only 1 item remains , store count in result_num .' | validate_stack_pop_until • Assert stack.size() == 1 . • Assert result_num == 3 (Popped D,C,B). |
| T3                   | 'Verify we're at the right page. Peek at stack's top and store in result_str .'                    | validate_stack_peek_after • Assert result_str == 'A' . • Assert stack.size() == 1 .            |
| Case: cart_quantity  | Case: cart_quantity                                                                                | Case: cart_quantity                                                                            |
| T1                   | 'Add 3 Apples at $10.00 each to cart with quantity.'                                               | validate_cart_qty_add • Assert len(cart.items) == 1 . • Assert items[0]['quantity'] == 3 .     |
| T2                   | 'Also add 2 Oranges at $5.00 each...'                                                              | validate_cart_qty_add2 • Assert len(cart.items) == 2 .                                         |
| T3                   | 'Calculate total (price * quantity)... store in result_num .'                                      | validate_cart_qty_total • Assert result_num == 40.0 . • Logic: ( 3 × 10 )+( 2 × 5 ) .          |

Figure 10: Illustration of Scientific Types test cases. This benchmark challenges the agent with high-dimensionality operations, including relational merges ( dataframe\_merge ), structural reshaping ( dataframe\_pivot ), and tensor axis manipulation ( ndarray\_reshape ), going beyond simple arithmetic.

| Scientific Types                            | Scientific Types                                                                | Scientific Types                                                                                      |
|---------------------------------------------|---------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------|
| Turn                                        | User Query (Input)                                                              | Immediate Validation (State Assertion)                                                                |
| Case: dataframe_merge (Relational Logic)    | Case: dataframe_merge (Relational Logic)                                        | Case: dataframe_merge (Relational Logic)                                                              |
| T1                                          | 'Merge df and df2 on product column. Store in result_df .'                      | validate_df_merge • Assert len(result_df) == 3 . • Assert column "supplier" exists.                   |
| T2                                          | 'Update result_df to keep only rows where supplier is 'SupA'.'                  | validate_df_merge_filter • Assert len(result_df) == 2 . • Logic: Keeps 'Phone' and 'Shirt'.           |
| T3                                          | 'Calculate the sum of prices in result_df . Store in result_value .'            | validate_df_merge_sum • Assert result_value == 550.0 . • Logic: 500 . 0 + 50 . 0 .                    |
| Case: dataframe_pivot (Structure Reshaping) | Case: dataframe_pivot (Structure Reshaping)                                     | Case: dataframe_pivot (Structure Reshaping)                                                           |
| T1                                          | 'Create pivot table from df_sales : region=rows, quar- ter=cols, sales=values.' | validate_df_pivot • Assert result_df.shape == (3, 2) . • Checks dimensions (3 regions, 2 quarters).   |
| T2                                          | 'Calculate total sum of all sales...'                                           | validate_df_pivot_sum • Assert result_value == 890 . • Verifies data integrity post-pivot.            |
| T3                                          | 'Find which region has highest total sales (sum of Q1+Q2). Store sum...'        | validate_df_pivot_max_region • Assert result_value == 380 . • Logic: South ( 200 + 180 ).             |
| Case: ndarray_reshape (Tensor Manipulation) | Case: ndarray_reshape (Tensor Manipulation)                                     | Case: ndarray_reshape (Tensor Manipulation)                                                           |
| T1                                          | 'Reshape array to shape (2, 4). Store in result_array .'                        | validate_array_reshape • Assert result_array.shape == (2, 4) . • Checks memory layout transformation. |
| T2                                          | 'Sum result_array along axis 1 (row sums).'                                     | validate_array_sum_axis • Assert result equals [70, 48] . • Validates axis-wise reduction.            |
| T3                                          | 'Calculate the total sum of result_array ...'                                   | validate_array_total • Assert result_value == 118 . • Logic: 70 + 48 .                                |

Figure 11: High-Dimensional State Management (Full Transcript). We present the raw input queries for the startup\_journey case. The high information density requires the agent to parse and update over 10 distinct variables (Integers, Floats, Strings, Lists, Dictionaries) in a single turn (e.g., T3) without hallucination or omitting details.

| Multi-Variable Management: Tracking 20 Concurrent States (Full Context)   | Multi-Variable Management: Tracking 20 Concurrent States (Full Context)                                                                                                                                                                                                                                                                                                                                                                                                                                                                      | Multi-Variable Management: Tracking 20 Concurrent States (Full Context)                                                                                    |
|---------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Turn                                                                      | Complex User Query (Full Text)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               | State Verification (Partial View)                                                                                                                          |
| Case: startup_journey (20 Variables)                                      | Case: startup_journey (20 Variables)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         | Case: startup_journey (20 Variables)                                                                                                                       |
| T1                                                                        | 'I'm documenting our startup TechStart. We're in the Software industry, led by CEO Alice Johnson, headquartered in San Francisco. We have 50 employees, founded in 2020, 1 office, 2 products. Revenue is $5M (5000000) with 10% profit margin (0.1), not public yet so no stock price or market cap. We're profitable and hiring but not international yet. Departments: ['Engineering', 'Sales', 'Marketing']. Locations: ['SF']. Financials: funding 10000000, round 'Series A'. Contacts: email 'info@techstart.com', phone '555-0100'.' | validate_startup_init • employees → 50 • revenue → 5,000,000.0 • profit_margin → 0.1 • public → False • stock_price → 0.0 (Initial)                        |
| T2                                                                        | 'Big growth update! Set employees to 150, offices to 3, products to 5. Set revenue to $15M (15000000), profit_margin to 0.15. Set international to true. Append 'HR' and 'Finance' to departments. Append 'NYC' and 'London' to locations. Add 'valuation': 100000000 to financials while keeping existing entries. Add 'support': '555-0200' to contacts while keeping existing entries.'                                                                                                                                                   | validate_startup_growth • employees → 150 • depts → [..., 'HR', 'Finance'] • financials → +{'valuation': 100M} • Assert founded_year == 2020 (Un- changed) |
| T3                                                                        | 'We're going public! Append ' Inc.' to company_name. Set industry to 'Enterprise Software'. Set employees to 500, offices to 10, products to 10. Set revenue to $50M (50000000), profit_margin to 0.2, stock_price to 25.0, market_cap to $500M (500000000). Set public to true. Append 'Legal' and 'IR' to departments. Append 'Tokyo' and 'Berlin' to locations. Add 'ipo': true to financials while keeping existing entries. Add 'ir': 'irtechstart.com' to contacts while keeping existing entries.'                                    | validate_startup_ipo • public → True • stock_price → 25.0 • company_name → "TechStart Inc." • market_cap → 500,000,000.0                                   |

## D.3. Multi-turn Cases

These test cases evaluate the agent's capability to process sequential instructions and maintain state precision over longhorizon scenarios. Unlike single-turn tasks where information is self-contained, these scenarios require the agent to maintain persistent memory of the system's status, as subsequent queries depend on the outcome of previous actions. We categorize these multi-turn benchmarks into two domains: Smart Home Control and Financial Account Management .

## D.3.1. Smart Home

In the Smart Home scenario, the agent acts as a central automation controller responsible for managing a suite of simulated IoT devices, including smart lighting, thermostats, motorized blinds, security cameras, and media players.

This benchmark specifically targets two advanced capabilities in stateful management:

- Users frequently issue relative commands rather than absolute ones (e.g., 'turn up the music more ' or 'dim the lights a bit '). To execute these correctly, the agent must recall the exact discrete level set in previous turns (e.g., incrementing volume from 'medium' to 'high') rather than resetting to a default value.
- The agent must dynamically adjust device states based on simulated environmental contexts (e.g., 'sunset', 'motion detected') and complex user-defined conditions (e.g., 'if the temperature drops below 10 ◦ C, set heating to 22 ◦ C').

As illustrated in Figure 12, the weekend\_party case spans a simulated 24-hour cycle. The agent must maintain a coherent environment state, transitioning from a quiet morning to a loud party and finally to a secure night mode, without drifting from the user's cumulative intent.

Figure 12: State persistence in long-horizon interactions. We visualize 5 key moments from the 20-turn weekend\_party scenario. The agent must maintain a coherent environment state (lighting, temperature, security, audio) over a simulated 24-hour period. Crucially, it handles relative instructions (e.g., "turn up music", "lower music more") by tracking the exact discrete levels (e.g., Medium=40, Party=60) defined in the environment schema.

| Time / Turn       | User Query (Intent & Context)                                                                                                   | State Evolution & Validation                                                                                                |
|-------------------|---------------------------------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------|
| Turn 3 1:00 PM    | 'Party prep! Guests arriving soon. Adjust thermostat for comfort, set music to medium, open blinds fully , make lights bright.' | validate_party_turn_3 • Music: OFF → 40% (Medium) • Blinds: Closed → 100% (Full) • Light: Dim → 80% (Bright)                |
| Turn 5 4:00 PM    | 'Party mode! Full swing now. Turn up the music and make lights very bright . Verify camera is recording.'                       | validate_party_turn_5 • Music: 50% → 60% (Party) • Light: 80% → 90% (Very Bright) • Camera: Assert status == Recording      |
| Turn 7 7:00 PM    | 'Evening party. Close blinds completely , set mood lighting... turn up music more .'                                            | validate_party_turn_7 • Blinds: Partial → 0% (Closed) • Music: 60% → 70% (Up More) • Light: 90% → 60% (Mood)                |
| Turn 10 10:00 PM  | 'Guests leaving. Lower music more , lock door, turn off bedroom light.'                                                         | validate_party_turn_10 • Music: 80% → < 60% (Lowered) • Door: Unlocked → Locked • Bed Light: ON → OFF                       |
| Turn 17 Sun 10 AM | 'Lazy morning... Finally getting up. Turn on bedroom light, open blinds, raise thermostat.'                                     | validate_party_turn_17 • Long-horizon consistency check • Thermostat: Eco (18) → Comfort (21) • Blinds: Closed → 70% (Open) |

## D.3.2. Financial Account

The Financial Account benchmark evaluates the agent's capability to maintain strict numerical integrity and execute state-dependent logic within a banking ledger system. Unlike the relative adjustments in Smart Home, this domain demands exact integer arithmetic, where the agent must process a continuous stream of transactions, including deposits, interest applications, and loan amortizations, without cumulative drift.

This scenario imposes two constraints designed to stress-test the agent's reasoning stability:

- Operations require strict integer truncation (e.g., calculating 20% of 1105 as 221 , not 221 . 0 ). Since the output of each turn (e.g., current balance) serves as the immutable basis for subsequent calculations (e.g., compound interest), a single arithmetic error in early turns triggers a cascading failure, rendering the entire subsequent interaction trajectory incorrect.
- The agent must evaluate complex logic gates based on dynamic runtime states rather than static instructions. As demonstrated in the carol\_debt\_paydown case (Figure 13), queries often involve comparative functions (e.g., 'pay the smaller of 15% of balance or 15% of loan') or threshold checks (e.g., upgrading to 'premium' status only if net worth becomes positive). This requires the agent to retrieve, compare, and act upon multiple variable states simultaneously before executing a transaction.

Figure 13: Numerical precision and state-dependent reasoning. In the carol\_debt\_paydown scenario, the agent must perform exact integer arithmetic while navigating complex logic gates (e.g., Turn 4's "smaller of", Turn 14's "net worth check"). A single miscalculation in early turns (e.g., T2 interest) would cascade, causing failures in subsequent logic checks (e.g., failing the T16 payoff condition), thus rigorously testing long-horizon numerical stability.

| Turn   | Conditional Query (Logic & Math)                                                                                            | State Calculation & Assertions                                                                                                     |
|--------|-----------------------------------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------|
| T1     | 'Initialize account... Name 'Carol', Balance 500 , Status 'standard', Interest 8% (Loan rate), Loan 2000 .'                 | validate_carol_turn_1 • Balance: 500 • Loan: 2000 • Status: 'standard'                                                             |
| T2     | 'Monthly loan interest due. Apply interest rate (8%) to loan balance and add to debt.'                                      | validate_carol_turn_2 • Interest = 2000 × 0 . 08 = 160 • New Loan = 2000 + 160 = 2160                                              |
| T4     | 'Pay the smaller of 15% of balance or 15% of loan_balance. Subtract from both.' ( Context: T3 Paycheck +800 → Balance 1300) | validate_carol_turn_4 • [Logic] IF min ( 1300 × . 15 , 2160 × . 15 ) • Calc: min ( 195 , 324 )= 195 • New Loan = 2160 - 195 = 1965 |
| T8     | 'Pay the larger of 40% of balance or 500 toward loan.' ( Context: Balance grew to 1574 after T7)                            | validate_carol_turn_8 • [Logic] Compare: 1574 × 0 . 4 (629) vs 500 • Action: Pay 629 • Verify exact integer subtraction.           |
| T14    | 'Check upgrade: IF loan_balance < balance, upgrade status to 'premium'.' ( Context: Loan reduced to 1172, Balance 1646)     | validate_carol_turn_14 • [Logic] Condition: 1172 < 1646 (True) • Status → 'premium' • Triggers T15 bonus paycheck.                 |
| T16    | ' IF balance > loan_balance, pay off entire loan. Otherwise pay 75%...'                                                     | validate_carol_turn_16 • [Logic] Action: Payoff Condition Met. • Loan → 0.0 • Balance reduced by remaining debt.                   |

## E. Stateful Runtime-Mediated Multi-Agent Coordination

The function-calling paradigm in CaveAgent introduces three key contributions for multi-agent coordination; Figure 1 illustrates an example. In this paper, we focus on qualitative analysis and provide case studies to facilitate understanding, leaving rigorous quantitative evaluation for future work. We introduce the high-level ideas below.

## E.1. Meta-Agent Runtime Control

Sub-agents are injected as first-class objects into a meta-agent's runtime, enabling the meta-agent to programmatically access and manipulate child agent states through generated code. Rather than following predefined communication protocols, the meta-agent dynamically sets variables in sub-agent runtimes, triggers execution, and retrieves results, enabling adaptive pipeline construction, iterative refinement loops, and conditional branching based on intermediate states.

## E.2. State-Mediated Communication

Inter-agent data transfer bypasses message passing entirely. Agents communicate through direct runtime variable injection: the meta-agent retrieves objects from one agent's runtime and injects them into another's as native Python artifacts (DataFrames, trained models, statistical analyses), preserving type fidelity and method interfaces without serialization loss.

## E.3. Shared-Runtime Synchronization

For peer-to-peer coordination, multiple agents can operate on a unified runtime instance, achieving implicit synchronization without explicit messaging. When one agent modifies a shared object, all peers perceive the change immediately through direct reference. New entities injected into the shared runtime become instantly discoverable, enabling collaborative manipulation of a unified world model with low coordination overhead.

How the town simulation demonstrates this capability. When the meta-agent modifies the weather state, all resident agents observe the change through direct attribute access; when a new location and manager are injected, existing agents can immediately query and interact with them.

Together, these patterns transform multi-agent systems from lossy text-based message exchange into typed, verifiable state flow, enabling automated validation of inter-agent handoffs and integration with downstream pipelines.

## E.4. AutoML Training Loop

We demonstrate CaveAgent's hierarchical agent coordination through an AutoML training loop where an orchestrator agent programmatically manages sub-agent runtimes (Figure 14). The orchestrator injects raw data into a feature engineering agent's runtime via inject() , triggers execution, then retrieves the transformed DataFrame via retrieve() and injects it into a trainer agent's runtime, all as native Python objects without serialization. After training, the orchestrator extracts evaluation metrics directly from the trainer's runtime, validates against target requirements via a check\_requirements() function, and injects performance feedback back into both sub-agents for the next iteration. Crucially, sub-agents themselves are injected as variables into the orchestrator's runtime, enabling the orchestrator to dynamically access and manipulate their internal states through generated code. This iterative refinement loop continues until programmatic convergence criteria are met, demonstrating CaveAgent's unique capability for hierarchical multi-agent coordination with typed, bidirectional state flow and automated convergence verification.

## F. Detailed BFCL Benchmark Results

Table F shows the detailed per-run results of BFCL benchmark.

## G. Features

## G.1. Case Analysis in Tau 2 -bench

To validate the architectural advantages of CaveAgent, we analyzed trajectory differences on the Tau 2 -bench retail benchmark. CaveAgent achieved a 72.8% success rate (83/114) compared to 62.3% (71/114) for the baseline JSON agent (Kimi K2 backbone), yielding a 10.5% improvement. We conducted a root cause analysis on the 24 tasks where CaveAgent succeeded but the baseline failed.

## G.1.1. Failure Taxonomy of the Baseline

Baseline failures were categorized into five distinct patterns (Figure 15). The dominant failure mode (37.5%) was Missing Critical Action , where the agent retrieved necessary information but failed to execute the final operation (e.g., return, cancel). This was often coupled with Incomplete State Exploration (16.7%), where the agent heuristically queried subsets of data (e.g., checking only one recent order) rather than performing the exhaustive search required by the query.

## G.1.2. Architectural Advantages: Loops and Conditionals

The analysis reveals that CaveAgent's improvements stem from its ability to generate programming constructs, specifically loops (used in 92% of winning cases) and conditionals (83%), which resolve the semantic gaps inherent in single-step function calling.

<!-- image -->

<!-- image -->

## Initial

```
Feature Engineering Agent feature_engineer = CaveAgent( runtime=PythonRuntime( variables=[ Variable("df", None,"Raw dataframe") ）， instructions="You are a feature engineer.Create and transform features based on feedback." Trainer Agent trainer = CaveAgent( runtime=PythonRuntime( variables=[ Variable("df", None，"Processed dataframe with features"), Variable("model"，None，"Trained model"), Variable("metrics"，{}， "Evaluation metrics"), Variable("hyperparameters", {"lr":0.001，"epochs":10}，"Model hyperparameters") ]， )， instructions="You are an ML trainer. Trainmodels and adjust hyperparameters based on feedback." Orchestrator orchestrator =CaveAgent( runtime=PythonRuntime( variables=[ # Inputs Variable("raw_df"， None,"Raw dataset"), Variable("requirements", {"accuracy": 0.95}, "Target metrics"), Variable("max_iterations"，10, "Maximum iterations"), #Sub-agents Variable("feature_engineer", feature_engineer), Variable("trainer"， trainer), # State Variable("iteration"，θ), Variable("training_log"，[]), functions=[Function(check_requirements) ]，
```

<!-- image -->

<!-- image -->

<!-- image -->

## Phases

## Phase 1: Feature Engineering

<!-- image -->

```
iteration += 1 print(f"=== ITERATION {iteration} ===") feature_engineer.runtime.inject("df"，raw_df) # Inject data into feature engineer feature_engineer.run("Create features for the dataset.") # Retrieve processed dataframe processed_df = feature_engineer.runtime.retrieve("df") # Create basic features df["age_squared"]= df["age"] ** 2 df["income_per_age"] = df["income"] / df["age"]
```

## Phase 2: Training

<!-- image -->

```
# Inject processed data into trainer trainer.runtime.inject("df"，processed_df) trainer.run("Train a model and evaluate.") # Retrieve metrics metrics = trainer.runtime.retrieve("metrics") print(f"Metrics:{metrics}") X = df.drop("target"， axis=1) y = df["target"] X_train, X_test, y_train, y_test = train_test_split(x, y，test_size=0.2) model =RandomForestClassifier(n_estimators=hyperpa rameters.get("n_estimators"，100)) model.fit(x_train, y_train) metrics ={"accuracy":model.score(x_test, y_test)} print(f"Accuracy:{metrics['accuracy']:.4f}")
```

## Phase3:Check&amp;FeedbackLoop

<!-- image -->

```
#Check requirements result = check_requirements(metrics,requirements) training_log.append({"iteration":iteration，"metrics": metrics}) if result["passed"]: print("√ Requirements met!") else: print(f"x Accuracy {metrics['accuracy']}< {requirements['accuracy']}. Improving...") iteration += 1 print(f"=== ITERATION {iteration} ===") #Feature engineer improves feature_engineer.run(f"Improve features.Current accuracy: {metrics['accuracy']}") processed_df =feature_engineer.runtime.retrieve("df") # Trainer adjusts and retrains trainer.runtime.inject("df"，processed_df) trainer.run(f"Tune hyperparameters. Current accuracy: {metrics['accuracy']}") metrics = trainer.runtime.retrieve("metrics") metrics}) #Create better features df["income_bucket"] = pd.qcut(df["income"]， q=5, labels=False) df["age_income_interaction"] = df["age"] * df["income"] #Adjust hyperparameters hyperparameters["n_estimators"] = 200 # Retrain model = RandomForestClassifier(n_estimators=hyperparameters["n_ estimators"]) model.fit(x_train, y_train) metrics = {"accuracy": model.score(x_test,y_test)} print(f"Accuracy:{metrics['accuracy']:.4f}")
```

Figure 14: Multi-agent AutoML pipeline: an orchestrator coordinates feature engineering and training sub-agents through runtime variable injection. Native Python objects (DataFrames, trained models) flow losslessly between agents across iterative refinement cycles.

## Final Output

```
=== ITERATION 1 === Accuracy:0.8234 × Accuracy 0.8234< 0.95. Improving... === ITERATION 2 === Accuracy: 0.9567 √Requirements met! Training Log: [x]Iter 1: acc=0.8234 (target: 0.95) []Iter 2:acc=0.9567 (target:0.95)
```

Table 7: Detailed Performance comparison on BFCL Benchmark (3 Runs). Data is presented in score/total format. The Avg. columns indicate the average overall percentage across 3 runs. Bold indicates CaveAgent outperforms Function Calling. Simp. = Simple Function, Mult. = Multiple Function, Para. = Parallel Function, P-M. = Parallel Multiple Function, Ov. = Overall.

| Model                      | Run      | Function Calling                                | Function Calling        | Function Calling        | Function Calling           | Function Calling   |                                                         |                                         |                                  |                            |               |
|----------------------------|----------|-------------------------------------------------|-------------------------|-------------------------|----------------------------|--------------------|---------------------------------------------------------|-----------------------------------------|----------------------------------|----------------------------|---------------|
| Model                      | Run      | Simp. Mult.                                     |                         | Para.                   | P-M. Ov.                   | Avg.(%)            | Simp. Mult.                                             | Para.                                   | P-M.                             | Ov.                        | Avg.(%)       |
| Open Source                |          |                                                 |                         |                         |                            |                    |                                                         |                                         |                                  |                            |               |
| DeepSeek-V3.2 (685B)       | R1 R2 R3 | 354/400 183/200 353/400 185/200 360/400 185/200 | 175/200 167/200 173/200 | 159/200 159/200 154/200 | 871/1000 864/1000 872/1000 | 86.9               | 382/400 192/200 386/400 193/200 384/400 192/200         | 185/200 184/200 186/200                 | 178/200 178/200 180/200 942/1000 | 937/1000 941/1000          | 94.0 (+7.1)   |
| DeepSeek-V3.2 (w/o prompt) | R1 R2 R3 | 312/400 162/200 316/400 162/200 314/400 161/200 | 33/200 29/200 35/200    | 26/200 23/200 21/200    | 533/1000 530/1000 531/1000 | 53.1               | 382/400 192/200 386/400 193/200 384/400 192/200         | 185/200 184/200 186/200                 | 178/200 178/200 180/200          | 937/1000 941/1000 942/1000 | 94.0 (+40.9)  |
| Qwen3-Coder (30B)          | R1 R2 R3 | 381/400 185/200 381/400 185/200 381/400 185/200 | 166/200 166/200 164/200 | 167/200 167/200 167/200 | 899/1000 899/1000 897/1000 | 89.8               | 386/400 191/200 387/400 189/200 386/400 190/200         | 187/200 189/200 189/200                 | 180/200 181/200 178/200          | 944/1000 946/1000 943/1000 | 94.4 (+4.6)   |
| Kimi-K2-0905 (1000B)       | R1 R2 R3 | 372/400 183/200 368/400 181/200 373/400 185/200 | 170/200 167/200 173/200 | 168/200 171/200 165/200 | 893/1000 887/1000 896/1000 | 89.2               | 387/400 191/200 381/400 189/200 379/400 191/200         | 186/200 188/200 188/200                 | 187/200 186/200 187/200          | 951/1000 944/1000 945/1000 | 94.7 (+5.5)   |
| Closed Source              |          |                                                 |                         |                         |                            |                    |                                                         |                                         |                                  |                            |               |
| Claude Sonnet 4.5          | R1 R2 R3 | 387/400 189/200 388/400 190/200 387/400 190/200 | 184/200 183/200 184/200 | 183/200 182/200 184/200 | 943/1000 943/1000 945/1000 | 94.4               | 382/400 189/200 384/400 189/200 385/400 189/200         | 185/200 185/200 184/200                 | 187/200 186/200 186/200          | 943/1000 944/1000 944/1000 | 94.4 ( ∼ 0.0) |
| GPT-5.1                    | R1 R2 R3 | 366/400 183/200 367/400 186/200 367/400 185/200 | 174/200 173/200 174/200 | 173/200 169/200 172/200 | 896/1000 895/1000 898/1000 | 89.6               | 367/400 186/200 354/400 356/400 180/200 191/200 194/200 | 172/200 184/200 174/200 170/200 184/200 | 176/200 174/200 175/200          | 901/1000 886/1000 881/1000 | 88.9 (-0.7)   |
| Gemini 3 Pro               | R1 R2 R3 | 380/400 190/200 380/400 192/200 384/400 190/200 | 187/200 188/200 188/200 | 185/200 183/200 182/200 | 942/1000 943/1000 944/1000 | 94.3               | 382/400 378/400 380/400 194/200                         | 187/200 184/200                         | 186/200 185/200 185/200          | 943/1000 944/1000 943/1000 | 94.3 ( ∼ 0.0) |

Figure 15: Distribution of failure modes in baseline JSON agent trajectories. Note: Categories are non-exclusive as complex tasks may exhibit multiple failures.

<!-- image -->

Exhaustive State Exploration via Loops. Tasks requiring global search (e.g., "return the order sent to Texas") baffled the baseline agent, which typically checked only 1-2 arbitrary orders. In contrast, CaveAgent generated for -loops to iterate

through all user orders. For instance, in Task 26, the agent iterated through user.orders , checked order.address.state for "TX", and correctly identified the target order without hallucination.

```
# CaveAgent: Systematic iteration ensures no order is missed for order_id in user_details.orders: order = get_order_details(order_id) if "TX" in order.address.state: return_delivered_order_items(order_id , ...)
```

Listing 1: Snippet from Task 26 showing exhaustive search.

Complex Conditional Logic. The baseline struggled with tasks involving fallback logic (e.g., "modify item, but if price &gt;$3000, cancel order"). In Task 90, the JSON agent ignored the price constraint and attempted modification regardless. CaveAgent successfully modeled this decision tree using explicit if/else blocks, checking variable states ( variant.price ) before execution.

Precise Attribute Reasoning. While JSON agents rely on the LLM's internal attention to compare values (often leading to errors like cancelling the wrong order in Task 59), CaveAgent offloads reasoning to the Python interpreter. By storing intermediate results (e.g., timestamps) in variables and using comparison functions (e.g., min() ), CaveAgent ensured precise argument selection for actions requiring temporal or numerical comparisons.

## G.2. Smart Home

Figure 16 illustrates the mechanistic advantage of CaveAgent through a toy smart-home example. The architecture separates the Semantic Stream (logic generation) from the Runtime Stream (state storage). This design enables two key capabilities absent in standard JSON agents:

- State Persistence: Variables (e.g., Thermostat , Door ) are initialized once and retain their state across multiple turns, eliminating the need to hallucinate or re-query context.
- Control Flow Execution: The agent generates executable Python code with conditionals
- (e.g., if not door\_lock.is\_locked: ), allowing for precise, context-dependent state transitions rather than blind API execution.

## G.3. Geospatial Analysis

To illustrate CaveAgent's advantages in domain-specific applications, we consider an urban planning scenario in which a user draws two arbitrary regions on an interactive map and queries the differences in population density and urban development between them. The interactive map converts user-drawn regions into GeoJSON polygon objects, variable-length coordinate arrays with high-precision floating-point pairs, which are injected directly into CaveAgent's persistent runtime as first-class Python variables. Upon receiving the natural language query, the LLM generates a single code block that references these injected geometries to perform zonal statistics against WorldPop raster data and extract land use features from OpenStreetMap via osmnx , chaining multiple domain-specific operations through native variable passing. Under the conventional JSON function calling paradigm, the same task would require at least five sequential LLM turns, querying population and land use statistics separately for each region before synthesizing text-serialized results, while also confronting the challenge of encoding complex polygon geometries as JSON string parameters, which risks truncation and introduces serialization overhead. CaveAgent resolves the entire query in a single turn with lossless data flow: geometries maintain full numerical precision, intermediate results (e.g., GeoDataFrames) persist as manipulable runtime objects, and the LLM synthesizes the final response from deterministic execution output (Figure 17). This case study demonstrates CaveAgent's suitability for scientific and analytical domains where computation involves complex non-serializable data structures and precision-sensitive results.

Figure 16: Demonstration of CaveAgent in a smart-home example: the Semantic Stream interacts with the Runtime Stream by generating code that manipulates stateful objects (variables) in the persistent runtime.

<!-- image -->

Figure 17: Geospatial Analysis: a user draws two regions on an interactive map. CaveAgent injects the GeoJSON polygons as Python variables and resolves the entire spatial query in a single turn with lossless data flow.

<!-- image -->