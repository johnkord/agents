# Research Paper Catalogue

> 123 papers · Last updated: 2026-04-10
>
> This document catalogues every research paper in the collection, organized by theme. Beyond simple categorization, it maps the **intellectual landscape**: the competing schools of thought, the tensions between opposing ideas, and the emerging consensus positions that are shaping how agents are built.

---

## Table of Contents

- [I. The Intellectual Landscape](#i-the-intellectual-landscape)
  - [Core Tensions](#core-tensions)
  - [Schools of Thought](#schools-of-thought)
  - [The Emerging Consensus (2025–2026)](#the-emerging-consensus-20252026)
- [II. Paper Catalogue by Category](#ii-paper-catalogue-by-category)
  - [A. Foundational Reasoning & Prompting](#a-foundational-reasoning--prompting)
  - [B. Context Engineering](#b-context-engineering)
  - [C. Memory Systems](#c-memory-systems)
  - [D. Agent Architecture & Frameworks](#d-agent-architecture--frameworks)
  - [E. Tool Use & Interface Design](#e-tool-use--interface-design)
  - [F. Planning & Reasoning Strategies](#f-planning--reasoning-strategies)
  - [G. Multi-Agent Systems](#g-multi-agent-systems)
  - [H. Agent Protocols & Interoperability](#h-agent-protocols--interoperability)
  - [I. Agent Training (RL & Self-Improvement)](#i-agent-training-rl--self-improvement)
  - [J. Safety, Security & Guardrails](#j-safety-security--guardrails)
  - [K. KV Cache & Inference Optimization](#k-kv-cache--inference-optimization)
  - [L. Code Agents & Software Engineering](#l-code-agents--software-engineering)
  - [M. Evaluation & Benchmarks](#m-evaluation--benchmarks)
  - [N. Surveys & Taxonomies](#n-surveys--taxonomies)
  - [O. Deep Research Agents](#o-deep-research-agents)
  - [P. Self-Reflection, Verification & Self-Correction](#p-self-reflection-verification--self-correction)
- [III. Cross-Cutting Tensions in Detail](#iii-cross-cutting-tensions-in-detail)
- [IV. Timeline View](#iv-timeline-view)

---

## I. The Intellectual Landscape

### Core Tensions

The field of AI agent research is not a monolith. It contains deep, active disagreements about how agents should be designed, trained, and governed. Understanding these tensions is more valuable than memorizing any single paper.

#### 1. Scaffolding vs. Weights: Where Should Intelligence Live?

**The scaffold camp** argues that a capable base LLM plus well-engineered external structure (prompts, tools, memory systems, workflows) is sufficient and preferable. Intelligence lives in the *architecture around* the model.

> Representatives: ReAct, Reflexion, MemGPT, CoALA, SWE-agent, 12-Factor Agents, Context Engineering papers

**The weights camp** argues that agent capabilities should be internalized through training — via RL, self-play, or mid-training on agentic trajectories. Intelligence should live *inside* the model.

> Representatives: Tool-R0, MAGE, daVinci-Dev, Exploratory Memory-Augmented RL, DR-MAS

**The emerging synthesis**: Both camps are converging. Papers like Pensieve (StateLM) and daVinci-Dev show that models trained with agentic mid-training data *still* require good scaffolding at inference time. The question is shifting from "which one?" to "what ratio?" — and the answer appears to be task-dependent.

#### 2. Autonomy vs. Control: How Much Rope?

**The autonomy-maximizers** push toward fully autonomous agents that self-plan, self-correct, and operate without human intervention, arguing that human-in-the-loop is a crutch that limits agent capability.

> Representatives: Voyager, ADAS, Tool-R0, MAGE, Generative Agents

**The control-maximizers** argue that unconstrained agents are dangerous and unreliable, advocating for guardrails, formal verification, human oversight, and safety-by-construction.

> Representatives: LlamaFirewall, AgentSentry, AgentSys, VIGIL, Auton (constraint manifolds), Securing-MCP

**The pragmatic middle**: Most production-oriented work (12-Factor Agents, Anthropic's building guide, τ-bench) adopts a "graduated autonomy" position — agents should earn trust through evaluation, and human contact should be a tool call, not a failure mode.

#### 3. Simplicity vs. Sophistication: The Architecture Debate

**The minimalists** argue that most problems don't need agents at all — and if they do, a simple while-loop with an LLM call suffices. Frameworks are premature abstraction. Prompt chaining and routing patterns handle 90% of use cases.

> Representatives: Anthropic's "Building Effective Agents," 12-Factor Agents, Brittle ReAct

**The systems builders** argue that complex tasks demand complex architectures: OS-like memory management, hierarchical planning, formal verification, multi-agent coordination, and structured communication protocols.

> Representatives: AgentOS, Monadic Context Engineering, Neural Paging, MetaGPT, Auton, StructuredAgent

**The evidence**: The Brittle ReAct paper (2024) showed that much of ReAct's performance comes from exemplar similarity, not the reasoning-acting loop itself — a strong data point for the minimalists. But SWE-bench results consistently show that structured tool interfaces, memory management, and planning outperform simple prompting on complex multi-step tasks.

#### 4. Stateless vs. Stateful: The Memory Question

**The stateless-reducer pattern** treats each agent step as a pure function of the full conversation history. No hidden state, full reproducibility, and trivially debuggable.

> Representatives: 12-Factor Agents, functional programming approaches

**The stateful-runtime pattern** argues that maintaining persistent state (Python environments, database connections, DAG-structured memory) across turns is essential for complex tasks and dramatically reduces token waste.

> Representatives: CaveAgent, MemGPT, Contextual Memory Virtualisation, Pensieve, Aeon

**The tension**: Stateless is simpler and more testable. Stateful is more efficient and capable. CaveAgent demonstrated +10.5% task success and 28.4% token reduction with persistent runtime state. The cost is debuggability and reproducibility.

#### 5. Single-Agent vs. Multi-Agent: When to Scale Out

**The single-agent school** argues that a well-prompted single model with good tools can handle most tasks, and multi-agent systems add coordination overhead, error propagation, and debugging complexity.

> Representatives: AgentArk (distilling multi-agent into single), SWE-agent, Anthropic's guidance

**The multi-agent school** argues that specialization, debate, and division of labor produce better results, especially for complex tasks that require different expertise or perspectives.

> Representatives: MetaGPT, AutoGen, CORAL, Mixture-of-Agents, DR-MAS, CASTER

**The interesting finding**: AgentArk (2026) showed you can distill multi-agent debate dynamics into a single model's weights, getting multi-agent quality at single-agent cost. This suggests multi-agent systems may be a *training technique* more than a *deployment architecture*.

#### 6. Formal vs. Empirical: How to Validate Agent Design

**The formalists** seek mathematical frameworks, provable properties, and principled design spaces for agents — category theory, POMDPs, information-theoretic analysis.

> Representatives: Monadic Context Engineering, Neural Paging, Formalizing Agent Designs, MCP Information Fidelity (martingale analysis), Auton (POMDP formalization)

**The empiricists** argue that benchmarks, ablation studies, and large-scale experiments are the only reliable guide — formal models inevitably simplify away the details that matter.

> Representatives: Structured Context Engineering (9,649 experiments), SWE-bench, AgentBench, τ-bench, GAIA2

**The gap**: Formal work establishes beautiful theoretical properties but rarely evaluates on realistic benchmarks. Empirical work achieves impressive numbers but often lacks explanatory power for *why* things work.

#### 7. Open Capability vs. Closed Safety: The Tool Access Dilemma

**The capability maximizers** give agents access to rich tool sets — file systems, code execution, web browsing, APIs — because more tools means more capable agents.

> Representatives: DynaSaur (agents create their own tools), OpenHands, Gorilla, Voyager

**The safety minimizers** demonstrate that every tool is an attack surface — prompt injection through tool outputs, tool poisoning via MCP, data exfiltration through seemingly innocent API calls.

> Representatives: Securing MCP, VIGIL, AgentSentry, LlamaFirewall, AgentSys

**The unsolved problem**: There is no principled way to give an agent powerful tools while fully preventing misuse. Current defenses are heuristic and model-dependent (GPT-4 blocks ~71% of unsafe MCP calls, but that leaves 29%).

#### 8. Generation vs. Verification: The Asymmetry Thesis

**The generation-first camp** argues the primary challenge is producing correct outputs — invest in better reasoning, better training, deeper chains of thought. If generation is good enough, verification is unnecessary.

> Representatives: DeepSeek-R1, MAGE, Tool-R0, long CoT reasoning models

**The verification-first camp** argues that checking is fundamentally easier than generating — the "asymmetry of verification." Invest in lightweight verification modules that can catch errors cheaply, then use the savings to run more generation attempts.

> Representatives: DeepVerifier, CoRefine, SPOC, Structured Reflection for Tool Use

**The emerging synthesis**: The most effective systems combine both. DeepVerifier (2026) showed that rubric-guided verification yields 8-11% accuracy gains on deep research tasks, while CoRefine's 211K-parameter controller achieves 92.6% precision on halt decisions — using 190x fewer tokens than 512-sample majority voting. SPOC demonstrated that interleaving generation and verification in a single pass (spontaneous self-correction) yields 8.8-20% gains on math benchmarks. The direction is clear: **verification should be a first-class architectural component, not an afterthought.**

The key insight from this body of work: verification scales better than generation. A small verifier that decides when to stop, rethink, or try a new approach provides more value per token than a proportionally larger generator.

---

### Schools of Thought

| School | Core Belief | Key Papers | Era |
|--------|------------|------------|-----|
| **Prompt Engineering** | The right prompt unlocks agent capability | CoT, ReAct, Self-Discover | 2022–2023 |
| **Context Engineering** | Context is a system, not a string; agent failures are context failures | Structured CE, Monadic CE, SWE-Pruner, Pensieve | 2025–2026 |
| **OS-Inspired Architecture** | Treat the context window as RAM, external storage as disk; apply OS abstractions | MemGPT, AgentOS, Neural Paging, Contextual Memory Virtualisation, AgentSys | 2023–2026 |
| **Formal Methods** | Agent designs should be mathematically principled and provably correct | Monadic CE, Formalizing Agent Designs, MCP Information Fidelity, Auton | 2025–2026 |
| **RL-Trained Agents** | Train agent behavior through reinforcement learning, not just prompting | Tool-R0, MAGE, DR-MAS, Exploratory Memory RL, daVinci-Dev | 2025–2026 |
| **Safety-First** | Security and alignment must be built in, not bolted on | LlamaFirewall, AgentSentry, VIGIL, AgentSys, Securing MCP | 2025–2026 |
| **Pragmatic Minimalism** | Use the simplest architecture that works; avoid frameworks | 12-Factor Agents, Anthropic guide, Brittle ReAct | 2024–2025 |
| **Multi-Agent Coordination** | Complex tasks need specialized agents working together | MetaGPT, AutoGen, CORAL, MoA, CASTER, DR-MAS | 2023–2026 |
| **Agentic Training** | Models should be trained natively for agentic interaction, not just fine-tuned | daVinci-Dev, Pensieve (StateLM), Tool-R0, MAGE | 2026 |
| **Deep Research** | Autonomous retrieval, analysis, and synthesis at analyst level is the defining agentic capability | Deep Research Survey, Step-DeepResearch, SFR-DR, DeepPlanner, RL Design Choices | 2025–2026 |
| **Self-Reflective Agents** | Verification and self-correction should be trainable first-class capabilities, not just prompted behaviors | SPOC, CoRefine, Structured Reflection, RE-Searcher, DeepVerifier, Dyna-Think | 2025–2026 |

---

### The Emerging Consensus (2025–2026)

Despite the tensions, several positions are consolidating:

1. **Context engineering has superseded prompt engineering** as the dominant paradigm. The phrase "agent failures are context failures" (Anthropic/Schmid) is widely accepted.

2. **Tool interface design matters more than model prompting.** SWE-agent's ACI concept, Anthropic's tool optimization insights, and RIG's 12.2% accuracy gain from structured context all point the same direction.

3. **Memory is the critical unsolved problem.** The explosion of memory papers in 2025–2026 (Aeon, Hippocampus, AriadneMem, Anatomy of Agentic Memory, Adaptive Memory Admission, MemExRL, Evaluating Memory Structure) signals the field believes memory is where the next breakthrough will come.

4. **Agent safety is an architectural problem, not just an alignment problem.** The most effective defenses (AgentSys, LlamaFirewall) use structural isolation — not just better prompting.

5. **Evaluation is maturing but still insufficient.** GAIA2 (dynamic, async environments) and τ-bench (reliability metrics) represent a shift from "can it solve this?" to "can it reliably solve this in realistic conditions?"

6. **MCP has won the tool protocol war** but needs security hardening. Four independent papers study MCP's design, security, and information fidelity, while the protocol survey confirms MCP+A2A as the convergence point.

7. **Verification scales better than generation.** DeepVerifier, CoRefine, and SPOC all demonstrate that lightweight verification modules provide outsized accuracy gains at minimal cost. The "asymmetry of verification" — it's easier to check than to produce — is becoming a design principle.

8. **Deep Research is converging as the defining agentic benchmark.** Eight papers in 2025 establish deep research (autonomous multi-step web retrieval + synthesis) as the North Star capability for agent systems. Failure taxonomies (DEFT's 14 modes, DeepVerifier's 5 categories) reveal that agents fail primarily at evidence integration and verification, not comprehension.

9. **Self-reflection should be trained, not just prompted.** SPOC's single-pass interleaved verification, Structured Reflection's trainable Reflect→Call→Final strategy, and RE-Searcher's goal-oriented reflection all show that RL-trained reflection outperforms prompted reflection. The era of "let's think step by step" as a self-correction strategy is ending.

10. **World model simulation improves agent planning.** Dyna-Think demonstrates that integrating compressed world model simulation into the thinking process — predicting what will happen after an action — improves both in-domain and out-of-domain performance while reducing token usage.

---

## II. Paper Catalogue by Category

### A. Foundational Reasoning & Prompting

These papers established the core patterns that all modern agents build upon.

| Paper | Year | arXiv | Core Contribution |
|-------|------|-------|-------------------|
| [Chain-of-Thought Prompting](../papers/docling/chain-of-thought-prompting-2022.md) | 2022 | 2201.11903 | Demonstrated that asking LLMs to show reasoning steps unlocks emergent problem-solving; the seed of all "thinking" patterns |
| [ReAct: Synergizing Reasoning and Acting](../papers/docling/react-reasoning-acting-2022.md) | 2022 | 2210.03629 | Interleaved Thought→Action→Observation loop; foundation for most modern agent architectures |
| [Reflexion: Verbal Reinforcement Learning](../papers/docling/reflexion-verbal-reinforcement-2023.md) | 2023 | 2303.11366 | Self-reflection as episodic memory; 91% HumanEval without weight updates; proved that verbal feedback can replace gradient updates |
| [Tree of Thoughts](../papers/docling/tree-of-thoughts-2023.md) | 2023 | 2305.10601 | Tree search over reasoning paths; 4%→74% on Game of 24; introduced deliberate exploration to LLM reasoning |
| [LATS: Language Agent Tree Search](../papers/docling/lats-tree-search-2023.md) | 2023 | 2310.04406 | Combined MCTS with LLM agents; 92.7% HumanEval; bridged classical AI search with LLM reasoning |
| [Self-Discover: Self-Composed Reasoning](../papers/docling/self-discover-reasoning-structures-2024.md) | 2024 | 2402.03620 | LLMs self-compose task-specific reasoning structures; +32% over CoT; 10-40x fewer inference calls |
| [Brittle Foundations of ReAct](../papers/docling/brittle-react-prompting-2024.md) | 2024 | 2405.13966 | **Critical counterpoint**: showed ReAct gains come from exemplar similarity, not the reasoning-acting interleave itself |

**Key tension within this category**: The progression from CoT→ReAct→ToT→LATS suggests ever-more-elaborate reasoning structures help, but Brittle ReAct (2024) challenged this narrative by showing simpler explanations for ReAct's success. Self-Discover represents a middle path: let the model choose its own reasoning structure rather than imposing one.

---

### B. Context Engineering

The successor paradigm to prompt engineering, treating context as a system to be engineered rather than a string to be written.

| Paper | Year | arXiv | Core Contribution |
|-------|------|-------|-------------------|
| [Monadic Context Engineering](../papers/docling/monadic-context-engineering-2025.md) | 2025 | 2512.22431 | Category-theory framework (Functors, Monads) for composable agent workflows; mathematically principled context composition |
| [Structured Context Engineering](../papers/docling/structured-context-engineering-2026.md) | 2026 | 2602.05447 | 9,649-experiment empirical study: model capability (21pp gap) dwarfs format choice; killed the YAML-vs-JSON debate |
| [SWE-Pruner: Self-Adaptive Context Pruning](../papers/docling/swe-pruner-context-pruning-2026.md) | 2026 | 2601.16746 | Agents that selectively prune their own context for coding tasks; context management as a first-class agent capability |
| [Active Context Compression](../papers/docling/active-context-compression-2026.md) | 2026 | 2601.07190 | Autonomous memory management inspired by slime mold; 22.7% token reduction with identical accuracy |
| [Long-Context Reasoning Limits](../papers/docling/long-context-reasoning-limits-2026.md) | 2026 | 2602.16069 | Demonstrates that simply providing more context can *hurt* reasoning; the right context matters more than all context |
| [ContextCov: Agent Constraints](../papers/docling/contextcov-agent-constraints-2026.md) | 2026 | 2603.00822 | Derives and enforces executable constraints from agent instruction files; bridging specification and runtime |

**Ideology**: Context engineering represents a philosophical shift: the model is fixed, the context is the control surface. This is in tension with the RL-training school (Category I) which says: no, train the model to handle context better.

**Key finding**: Structured Context Engineering's 9,649-experiment study definitively showed that model capability matters more than context format — but that structured context helps frontier models more than open-source ones. This suggests context engineering has *diminishing returns* as model capability increases.

---

### C. Memory Systems

The most actively researched area in 2025–2026, driven by the recognition that stateless agents hit hard ceilings on long-horizon tasks.

| Paper | Year | arXiv | Core Contribution |
|-------|------|-------|-------------------|
| [MemGPT: LLMs as Operating Systems](../papers/docling/memgpt-llm-operating-system-2023.md) | 2023 | 2310.08560 | **Foundational**: OS-inspired virtual context management; context window = RAM, external storage = disk; self-directed paging |
| [Generative Agents (Stanford)](../papers/docling/generative-agents-stanford-2023.md) | 2023 | 2304.03442 | Memory streams, reflection, and planning for 25 agents in a sandbox; first demonstration of emergent social memory |
| [Aeon: Neuro-Symbolic Memory](../papers/docling/aeon-memory-management-2026.md) | 2026 | 2601.15311 | High-performance memory combining neural retrieval with symbolic structure; bridging the sub-symbolic/symbolic divide |
| [Anatomy of Agentic Memory](../papers/docling/anatomy-agentic-memory-2026.md) | 2026 | 2602.19320 | Taxonomy and empirical analysis revealing that most memory systems fail silently; evaluation methodology is the bottleneck |
| [Evaluating Memory Structure](../papers/docling/evaluating-memory-structure-2026.md) | 2026 | 2602.11243 | Systematic comparison of memory architectures; no single structure dominates across all tasks |
| [Adaptive Memory Admission](../papers/docling/adaptive-memory-admission-2026.md) | 2026 | 2603.04549 | Learned admission control for what enters long-term memory; not everything is worth remembering |
| [Hippocampus Memory Module](../papers/docling/hippocampus-memory-module-2026.md) | 2026 | 2602.13594 | Brain-inspired memory consolidation; episodic→semantic transfer mimicking biological memory |
| [AriadneMem: Lifelong Memory](../papers/docling/ariadnemem-lifelong-memory-2026.md) | 2026 | 2603.03290 | Threading lifelong memory across arbitrary-length sessions; the persistence problem |
| [MemExRL: Indexed Experience Memory](../papers/docling/memexrl-indexed-experience-2026.md) | 2026 | 2603.04257 | Indexed experience replay for long-horizon tasks; bridging RL experience replay with LLM agent memory |
| [Contextual Memory Virtualisation](../papers/docling/contextual-memory-virtualisation-2026.md) | 2026 | 2602.22402 | DAG-based state management with snapshot/branch/trim; 86% token reduction via structurally lossless trimming |
| [Neural Paging](../papers/docling/neural-paging-context-management-2026.md) | 2026 | 2603.02228 | Differentiable page controller (neural MMU); $O(N \cdot K^2)$ complexity with provable robustness bounds |
| [Pensieve: Stateful Context](../papers/docling/pensieve-stateful-context-2026.md) | 2026 | 2602.12108 | StateLM: models trained with internal memory tools; 52% on BrowseComp-Plus vs ~5% for standard LLMs |

**Key tension within this category**: 

- **OS metaphor vs. biological metaphor**: MemGPT, AgentOS, Neural Paging, and Contextual Memory Virtualisation use operating-system abstractions (RAM/disk, paging, virtual memory). Hippocampus and Generative Agents use biological metaphors (hippocampal consolidation, memory streams, reflection). Both are productive, but they suggest different design primitives.

- **What to remember**: Adaptive Memory Admission Control tackles the most fundamental question — not how to store memory, but *whether* to store it. Most systems remember everything and rely on retrieval. The admission control approach argues this is backwards: be selective about what enters memory in the first place.

- **External vs. internal memory management**: MemGPT (2023) pioneered LLM self-managed memory via external tool calls. Pensieve (2026) argues the LLM should be *trained* with memory tools as native capabilities, not bolted on. This mirrors the broader scaffolding vs. weights tension.

---

### D. Agent Architecture & Frameworks

How agents are structured, from simple loops to OS-like systems.

| Paper | Year | arXiv | Core Contribution |
|-------|------|-------|-------------------|
| [CoALA: Cognitive Architectures](../papers/docling/cognitive-architectures-coala-2023.md) | 2023 | 2309.02427 | Formal framework unifying all agent architectures; memory taxonomy (working, episodic, semantic, procedural) |
| [DSPy: Declarative LM Pipelines](../papers/docling/dspy-declarative-lm-pipelines-2023.md) | 2023 | 2310.03714 | Programming model replacing prompt engineering with modules + compiler; automated context optimization |
| [ADAS: Automated Design of Agentic Systems](../papers/docling/adas-automated-design-2024.md) | 2024 | 2408.08435 | Meta-agent that discovers novel architectures; 7-9% improvement over hand-designed; agents designing agents |
| [AgentOS: Token-Level to System-Level](../papers/docling/architecting-agentos-2026.md) | 2026 | 2602.20934 | LLM as "Reasoning Kernel" with context window as Addressable Semantic Space; full OS mapping onto agents |
| [Auton: Declarative Agent Framework](../papers/docling/auton-agentic-framework-2026.md) | 2026 | 2602.23720 | Separates Cognitive Blueprint (spec) from Runtime Engine; POMDP formalization with constraint manifold safety |
| [Formalizing Agent Designs](../papers/docling/formalizing-agent-designs-2026.md) | 2026 | 2602.08276 | Most "innovations" are prompt-level variations of ReAct; Structural Context Model for formal comparison |
| [CaveAgent: Stateful Runtime](../papers/docling/caveagent-stateful-runtime-2026.md) | 2026 | 2601.01569 | LLM-as-Runtime-Operator; persistent Python runtime across turns; +10.5% success, -28.4% tokens |
| [Agent Skills Architecture](../papers/docling/agent-skills-architecture-2026.md) | 2026 | 2602.12430 | How to structure, acquire, and secure reusable agent skills; the composability problem |
| [HyFunc: Agentic Function Calls](../papers/docling/hyfunc-agentic-function-calls-2026.md) | 2026 | 2602.13665 | Hybrid-model cascade for fast function calling; dynamic templating for reduced latency |
| [Theory of Code Space](../papers/docling/theory-of-code-space-2026.md) | 2026 | 2603.00601 | Asks whether code agents understand software architecture; the comprehension vs. pattern-matching question |

**Key tension within this category**:

- **Formalizing Agent Designs (2026)** makes a provocative claim: strip away implementation details and most "novel" agent architectures are just ReAct with different prompts. This directly challenges the value of architectural innovation papers like AgentOS, Auton, and CaveAgent.

- **Declarative vs. imperative**: DSPy and Auton advocate declaring *what* the agent should do and letting a compiler/runtime figure out *how*. AgentOS and CaveAgent are more imperative, building specific runtime machinery. The declarative camp argues for portability and verification; the imperative camp argues for performance and control.

---

### E. Tool Use & Interface Design

How agents interact with the external world through tools, APIs, and computer interfaces.

| Paper | Year | arXiv | Core Contribution |
|-------|------|-------|-------------------|
| [Toolformer](../papers/docling/toolformer-self-taught-tools-2023.md) | 2023 | 2302.04761 | Self-supervised tool learning; 6.7B model matches 175B GPT-3 with tools; tools as a capability multiplier |
| [Gorilla: LLM + Massive APIs](../papers/docling/gorilla-api-calling-2023.md) | 2023 | 2305.15334 | Retrieval-augmented API calling; smaller model + documentation beats larger model; documentation is context |
| [SWE-agent: Agent-Computer Interface](../papers/docling/swe-agent-aci-2024.md) | 2024 | 2405.15793 | ACI design principles; demonstrated that interface design > prompt optimization for coding agents |
| [DynaSaur: Dynamic Action Creation](../papers/docling/dynasaur-dynamic-actions-2024.md) | 2024 | 2411.01747 | Agents that write their own new tools as code; growing action library that transcends predefined tool sets |
| [Agentic RAG Survey](../papers/docling/agentic-rag-survey-2025.md) | 2025 | 2501.09136 | Agentic design patterns (reflection, planning, tool use) applied to RAG pipelines; RAG as an agent task |

**Ideology**: The core belief here is that **tools are a stronger leverage point than model capability**. Toolformer showed a 6.7B model can match GPT-3 (175B) with tools. Gorilla showed docs + small model beats big model. SWE-agent showed interface design matters more than prompting. This is the "tools school" — invest in your tools, not your model.

---

### F. Planning & Reasoning Strategies

How agents decompose complex tasks, plan ahead, and adapt when plans fail.

| Paper | Year | arXiv | Core Contribution |
|-------|------|-------|-------------------|
| [Voyager: Lifelong Learning](../papers/docling/voyager-lifelong-learning-2023.md) | 2023 | 2305.16291 | First LLM-powered lifelong learner; skill library as code; open-ended exploration without weight updates |
| [Agentic Reasoning Survey](../papers/docling/agentic-reasoning-survey-2026.md) | 2026 | 2601.12538 | Comprehensive survey of reasoning strategies: fast (intuitive) vs. slow (deliberate), and their interaction |
| [HiMAC: Hierarchical Long-Horizon](../papers/docling/himac-hierarchical-long-horizon-2026.md) | 2026 | 2603.00977 | Hierarchical macro-micro learning; abstract planning + concrete execution; addressing the planning horizon problem |
| [Think Fast and Slow: Cognitive Depth](../papers/docling/think-fast-slow-cognitive-depth-2026.md) | 2026 | 2602.12662 | Adaptive step-level cognitive depth; agents that choose how deeply to think based on task difficulty |
| [StructuredAgent: AND/OR Planning](../papers/docling/structuredagent-andor-planning-2026.md) | 2026 | 2603.05294 | AND/OR tree decomposition for web tasks; structured planning that handles parallel and sequential dependencies |
| [Art of Scaling Test-Time Compute](../papers/docling/art-of-scaling-test-time-compute-2025.md) | 2025 | 2512.02008 | First large-scale study of test-time scaling; no single strategy universally dominates; introduces "reasoning horizon" |

**Key tension within this category**:

- **Fixed vs. adaptive reasoning depth**: CoT imposes constant-depth reasoning. Think Fast and Slow (2026) argues agents should dynamically adjust "cognitive depth" per step — sometimes a quick heuristic, sometimes deep deliberation. This maps to Kahneman's System 1 / System 2 distinction.

- **Open-ended vs. structured planning**: Voyager (open-ended exploration, no predefined plan) vs. StructuredAgent (AND/OR tree decomposition) represent opposed philosophies. Voyager says: let the agent discover; StructuredAgent says: decompose formally. HiMAC tries to bridge them with hierarchical levels.

---

### G. Multi-Agent Systems

Multiple agents working together — or against each other.

| Paper | Year | arXiv | Core Contribution |
|-------|------|-------|-------------------|
| [MetaGPT: Multi-Agent with SOPs](../papers/docling/metagpt-multi-agent-sop-2023.md) | 2023 | 2308.00352 | SOPs from software engineering; document-centric communication; ~60% reduced hallucination cascading |
| [AutoGen: Multi-Agent Conversation](../papers/docling/autogen-multi-agent-conversation-2023.md) | 2023 | 2308.08155 | ConversableAgent abstraction; conversation patterns; human-in-the-loop native |
| [Mixture-of-Agents (MoA)](../papers/docling/mixture-of-agents-2024.md) | 2024 | 2406.04692 | Layered multi-model ensemble; open-source ensemble beats GPT-4; collaboration as aggregation |
| [AgentArk: Distilling Multi-Agent](../papers/docling/agentark-distill-multi-agent-2026.md) | 2026 | 2602.03955 | Distill multi-agent debate into single model; process reward model quality > student model size |
| [CORAL: Agent-to-Agent Communication](../papers/docling/coral-agent-to-agent-communication-2026.md) | 2026 | 2601.09883 | Dynamic info-flow orchestration via A2A; 63.64% on GAIA vs 55.15% for workflow-based approach |
| [CASTER: Context-Aware Task Routing](../papers/docling/caster-multi-agent-routing-2026.md) | 2026 | 2601.19793 | Breaking cost-performance barrier via intelligent task routing among heterogeneous agents |
| [DR-MAS: Stable RL Multi-Agent](../papers/docling/dr-mas-stable-rl-multi-agent-2026.md) | 2026 | 2602.08847 | Per-agent reward normalization for stable multi-agent RL training; +15.2% on search tasks |
| [Adaptive Scalable Agent Coordination](../papers/docling/adaptive-scalable-agent-coordination-2026.md) | 2026 | 2602.08009 | Dynamic ad-hoc networking perspective on agent coordination; adapting to changing topologies |
| [Managing Uncertainty in Multi-Agent](../papers/docling/managing-uncertainty-multi-agent-2026.md) | 2026 | 2602.23005 | How multi-agent systems handle ambiguity and incomplete information; the uncertainty propagation problem |

**Key tension within this category**:

- **Centralized vs. decentralized coordination**: MetaGPT uses centralized SOPs where a manager assigns roles. CORAL uses decentralized A2A communication where agents self-organize. CASTER routes tasks to specialists based on context. The tradeoff: centralized is predictable but brittle; decentralized is flexible but harder to debug.

- **Multi-agent as deployment vs. training technique**: AgentArk's finding — that multi-agent debate can be distilled into a single model — challenges the assumption that you *need* multiple agents at inference time. Perhaps multi-agent systems are most valuable during training/development, and single-agent deployment is more practical.

---

### H. Agent Protocols & Interoperability

Standards for how agents communicate with tools and each other.

| Paper | Year | arXiv | Core Contribution |
|-------|------|-------|-------------------|
| [Agent Protocols Survey](../papers/docling/agent-protocols-survey-2025.md) | 2025 | 2504.16736 | First comprehensive survey of agent-to-agent and agent-to-tool communication protocols |
| [Agent Interoperability Protocols Survey](../papers/docling/agent-interoperability-protocols-survey-2025.md) | 2025 | 2505.02279 | Detailed comparison of MCP, ACP, A2A, and ANP protocols; strengths and limitations of each |
| [Agentic AI Frameworks: Architectures & Protocols](../papers/docling/agentic-ai-frameworks-protocols-2025.md) | 2025 | 2508.10146 | Broader view: how frameworks, protocols, and design patterns interconnect |
| [MCP Design Choices](../papers/docling/mcp-design-choices-2026.md) | 2026 | 2602.15945 | From tool orchestration to code execution: studying what design decisions MCP embeds |
| [MCP Server Description Smells](../papers/docling/mcp-server-description-smells-2026.md) | 2026 | 2602.18914 | 73% of MCP servers have description smells; "code-first, description-last" anti-pattern; tool descriptions as the critical interface |
| [MCP Information Fidelity](../papers/docling/mcp-information-fidelity-2026.md) | 2026 | 2602.13320 | Martingale analysis of information loss through tool chains; formal model of context degradation |
| [MCP-Atlas Benchmark](../papers/docling/mcp-atlas-benchmark-2026.md) | 2026 | 2602.00933 | Large-scale benchmark for tool-use competency with real MCP servers; measuring practical tool capability |
| [Agent Protocol Security Comparison](../papers/docling/agent-protocol-security-comparison-2026.md) | 2026 | 2602.11327 | Security threat modeling across MCP, A2A, Agora, and ANP; each protocol has distinct attack surfaces |
| [CORAL: A2A Communication](../papers/docling/coral-agent-to-agent-communication-2026.md) | 2026 | 2601.09883 | Natural-language A2A as a paradigm beyond rigid workflow graphs |

**Ideology**: MCP has become the de facto standard for agent-tool communication (4+ papers analyzing it in early 2026 alone). But the community is discovering that MCP's *design* embeds important assumptions about trust, description quality, and information fidelity that aren't always met. The "Server Description Smells" paper reveals a 73% rate of quality issues — a major practical concern.

---

### I. Agent Training (RL & Self-Improvement)

Training agents through reinforcement learning, self-play, and new training paradigms.

| Paper | Year | arXiv | Core Contribution |
|-------|------|-------|-------------------|
| [Tool-R0: Self-Evolving Agents](../papers/docling/tool-r0-self-evolving-agents-2026.md) | 2026 | 2602.21320 | Zero-data self-play RL; Generator + Solver co-evolution; 92.5% improvement over base model without any human data |
| [MAGE: Meta-RL Exploration](../papers/docling/mage-meta-rl-exploration-2026.md) | 2026 | 2603.03680 | Meta-RL for strategic exploration and exploitation; 100% on Webshop; generalizes to unseen opponents |
| [Exploratory Memory-Augmented RL](../papers/docling/exploratory-memory-augmented-rl-2026.md) | 2026 | 2602.23008 | Hybrid on/off-policy RL with memory; 128.6% improvement over GRPO; strong OOD adaptation without weight updates |
| [DR-MAS: Stable Multi-Agent RL](../papers/docling/dr-mas-stable-rl-multi-agent-2026.md) | 2026 | 2602.08847 | Per-agent reward normalization for multi-agent RL stability |
| [daVinci-Dev: Agent-Native Mid-Training](../papers/docling/davinci-dev-agent-midtraining-2026.md) | 2026 | 2601.18418 | Agentic mid-training as scalable alternative to post-training; "agent-native data" with authentic tool interactions |
| [Data Engineering for Terminal Agents](../papers/docling/data-engineering-terminal-agents-2026.md) | 2026 | 2602.21193 | How data quality and engineering choices shape terminal-based agent capabilities |

**Ideology**: This is the "weights camp" — the belief that agent capability should be internalized through training, not just scaffolded through prompting. The key insight from Tool-R0 is that self-play can work *with zero human data*, suggesting RL training could be an unbounded capability path. MAGE's meta-RL approach goes further: agents that *learn how to learn* to use tools.

**The counter-argument** (from the scaffolding camp): RL-trained agents still require good tools, good interfaces, and structured context at inference time. daVinci-Dev's impressive SWE-bench results still use agentic scaffolding at test time. The training improves the *engine*, but context engineering is still the *fuel*.

---

### J. Safety, Security & Guardrails

Protecting agents from adversarial attacks and preventing agent misuse.

| Paper | Year | arXiv | Core Contribution |
|-------|------|-------|-------------------|
| [LlamaFirewall](../papers/docling/llamafirewall-agent-guardrails-2025.md) | 2025 | 2505.03574 | Meta's open-source guardrail framework: PromptGuard 2, Agent Alignment Checks, CodeShield |
| [Securing MCP: Tool Poisoning](../papers/docling/securing-mcp-tool-poisoning-2025.md) | 2025 | 2512.06556 | Three MCP attack classes (Poisoning, Shadowing, Rug Pulls); RSA-signed manifests + LLM-on-LLM vetting |
| [Attention-Based PI Defense (NDSS 2026)](../papers/docling/attention-defense-prompt-injection-2025.md) | 2025 | 2512.08417 | Token-level attention features detect indirect prompt injection; RENNERVATE outperforms 15 defense methods |
| [AgentSentry: Temporal Causal PI Defense](../papers/docling/agentsentry-prompt-injection-defense-2026.md) | 2026 | 2602.22724 | Models PI as temporal causal takeover; counterfactual re-execution for attack localization |
| [ICON: Inference-Time PI Correction](../papers/docling/icon-prompt-injection-defense-2026.md) | 2026 | 2602.20708 | Corrects prompt injection effects at inference time rather than preventing them |
| [AgentSys: Hierarchical Memory Isolation](../papers/docling/agentsys-secure-memory-management-2026.md) | 2026 | 2602.07398 | OS-inspired memory isolation; 2.19% attack success rate through architectural separation alone |
| [VIGIL: Verify-Before-Commit](../papers/docling/vigil-tool-stream-injection-2026.md) | 2026 | 2601.05755 | Verification step before tool output commits to agent context; catching injection at the tool boundary |

**Key tension within this category**:

- **Detection vs. prevention**: Do you detect attacks (AgentSentry, RENNERVATE) or prevent them architecturally (AgentSys, VIGIL)? Detection is more flexible but allows attacks to partially succeed. Prevention is stronger but more restrictive and may break legitimate use cases.

- **Model-level vs. system-level defense**: LlamaFirewall and ICON operate at the model/prompt level. AgentSys and VIGIL operate at the system architecture level. The systems approach (AgentSys's 2.19% attack success rate) appears more robust, but the model approach is easier to deploy as a drop-in layer.

- **The 71% problem**: Securing MCP showed GPT-4 blocks ~71% of unsafe tool calls via heuristic guardrails. That means 29% get through. There is no known approach that achieves near-100% safety without severely restricting agent capability. This is arguably the most important unsolved problem in the field.

---

### K. KV Cache & Inference Optimization

Making inference cheaper and faster, especially for long contexts and reasoning chains.

| Paper | Year | arXiv | Core Contribution |
|-------|------|-------|-------------------|
| [KVzip: Query-Agnostic Cache Compression](../papers/docling/kvzip-kv-cache-compression-2025.md) | 2025 | 2505.23416 | NeurIPS 2025 Oral; 3-4x cache reduction via context reconstruction scoring; outperforms query-aware methods |
| [Hold Onto That Thought](../papers/docling/hold-onto-thought-kv-reasoning-2025.md) | 2025 | 2512.12008 | KV compression tested on reasoning tasks; aggressive eviction paradoxically produces *longer* traces |
| [Crystal-KV: CoT-Aware Cache](../papers/docling/crystal-kv-cot-cache-2026.md) | 2026 | 2601.16986 | KV cache management tailored for chain-of-thought; "answer-first principle" — not all thinking tokens matter equally |
| [Learning to Evict](../papers/docling/learning-to-evict-kv-cache-2026.md) | 2026 | 2602.10238 | Learned eviction policies that outperform hand-crafted heuristics; the meta-learning approach to cache management |

**Why these matter for agents**: Agents generate long reasoning traces and accumulate large contexts. KV cache compression directly affects agent cost and latency. The surprising finding from Hold Onto That Thought — that compressing the cache can make the model *think more* to compensate — reveals a non-obvious tradeoff between memory efficiency and compute efficiency.

---

### L. Code Agents & Software Engineering

Agents specialized for software development tasks.

| Paper | Year | arXiv | Core Contribution |
|-------|------|-------|-------------------|
| [SWE-agent: Agent-Computer Interface](../papers/docling/swe-agent-aci-2024.md) | 2024 | 2405.15793 | ACI concept; interface design > prompt optimization for coding agents |
| [OpenHands: AI Software Developers](../papers/docling/openhands-software-agents-2024.md) | 2024 | 2407.16741 | CodeAct paradigm (code as action); sandboxed execution; open platform for agent development |
| [daVinci-Dev: Agent-Native Mid-Training](../papers/docling/davinci-dev-agent-midtraining-2026.md) | 2026 | 2601.18418 | "Agent-native data" for mid-training; 56.1%/58.5% SWE-Bench Verified at 32B/72B; SOTA open recipe |
| [SWE-Adept: Deep Codebase Analysis](../papers/docling/swe-adept-deep-codebase-analysis-2026.md) | 2026 | 2603.01327 | Agentic framework for structured issue resolution; deep codebase analysis before acting |
| [Repository Intelligence Graph](../papers/docling/repo-intelligence-graph-2026.md) | 2026 | 2601.10112 | Deterministic build-derived architectural map for code agents; 12.2% accuracy gain, 53.9% faster |
| [Theory of Code Space](../papers/docling/theory-of-code-space-2026.md) | 2026 | 2603.00601 | Do code agents understand architecture? Tests comprehension vs. pattern matching |
| [Data Engineering for Terminal Agents](../papers/docling/data-engineering-terminal-agents-2026.md) | 2026 | 2602.21193 | How data engineering choices shape terminal-based agent capabilities |

**Key tension within this category**:

- **Understanding vs. manipulation**: Theory of Code Space asks whether agents genuinely understand software architecture or just pattern-match on code tokens. Repository Intelligence Graph sidesteps this question by providing deterministic architectural context — giving the agent explicit understanding rather than requiring it to derive understanding. This mirrors the broader scaffolding vs. weights debate.

- **Specialized vs. general training**: daVinci-Dev shows that agentic mid-training on code dramatically improves performance. But it requires specialized, expensive data pipelines. SWE-agent achieves strong results with interface design alone. The question: is it worth the training investment when good tools get you most of the way?

---

### M. Evaluation & Benchmarks

How we measure whether agents actually work.

| Paper | Year | arXiv | Core Contribution |
|-------|------|-------|-------------------|
| [SWE-bench: Real GitHub Issues](../papers/docling/swe-bench-github-issues-2023.md) | 2023 | 2310.06770 | 2,294 real software engineering tasks; the standard benchmark for coding agents |
| [AgentBench: Evaluating LLMs as Agents](../papers/docling/agentbench-evaluating-llms-2023.md) | 2023 | 2308.03688 | 8-environment benchmark; revealed massive gap between commercial and open-source models |
| [AgentBoard: Analytical Evaluation](../papers/docling/agentboard-evaluation-2024.md) | 2024 | 2401.13178 | Progress rate metric; fine-grained multi-turn evaluation across 9 environments |
| [OSWorld: Real Computer Environments](../papers/docling/osworld-computer-benchmark-2024.md) | 2024 | 2404.07972 | Real OS environments; best model 12.24% vs human 72.36%; GUI grounding as bottleneck |
| [τ-bench: Tool-Agent-User Interaction](../papers/docling/tau-bench-tool-agent-user-2024.md) | 2024 | 2406.12045 | pass^k reliability metric; even GPT-4o <50%; by ReAct/Reflexion authors |
| [Agent-as-a-Judge](../papers/docling/agent-as-a-judge-2024.md) | 2024 | 2410.10934 | Evaluate agents with agents; ~0.85 human correlation vs 0.65 for LLM-as-Judge |
| [GAIA2: Dynamic Async Benchmarks](../papers/docling/gaia2-async-agents-benchmark-2026.md) | 2026 | 2602.11964 | Dynamic, asynchronous environments; testing agents under realistic concurrent conditions |
| [MCP-Atlas Benchmark](../papers/docling/mcp-atlas-benchmark-2026.md) | 2026 | 2602.00933 | Large-scale MCP tool-use benchmark with real servers |

**Evolution of evaluation**:

The field has progressed through three evaluation eras:
1. **Static benchmarks** (2023): Can the agent solve this task? (SWE-bench, AgentBench)
2. **Reliability metrics** (2024): How consistently can it solve it? (τ-bench's pass^k, AgentBoard's progress rate)
3. **Realistic environments** (2025–2026): Can it solve it in dynamic, concurrent, real-world conditions? (GAIA2, OSWorld)

The gap between automated benchmarks and real-world reliability remains the field's dirty secret. τ-bench showed that even GPT-4o achieves <50% reliability on tool-use tasks.

---

### N. Surveys & Taxonomies

Comprehensive overviews of the field.

| Paper | Year | arXiv | Core Contribution |
|-------|------|-------|-------------------|
| [LLM-Based Agents Survey (Xi et al.)](../papers/docling/llm-agents-survey-2023.md) | 2023 | 2309.07864 | 86-page survey; Brain-Perception-Action framework |
| [Autonomous Agents Survey (Wang et al.)](../papers/docling/autonomous-agents-survey-wang-2023.md) | 2023 | 2308.11432 | Unified framework: Profiling + Memory + Planning + Action modules |
| [Agentic Reasoning Survey](../papers/docling/agentic-reasoning-survey-2026.md) | 2026 | 2601.12538 | Comprehensive survey of reasoning strategies for agents |
| [Art of Scaling Test-Time Compute](../papers/docling/art-of-scaling-test-time-compute-2025.md) | 2025 | 2512.02008 | Survey of test-time scaling strategies, compute budgets, and reasoning horizons |
| [Agentic AI Frameworks: Architectures & Protocols](../papers/docling/agentic-ai-frameworks-protocols-2025.md) | 2025 | 2508.10146 | Survey connecting frameworks, protocols, and design patterns |
| [Agent Interoperability Protocols Survey](../papers/docling/agent-interoperability-protocols-survey-2025.md) | 2025 | 2505.02279 | Detailed comparison of MCP, ACP, A2A, ANP |
| [Agent Protocols Survey](../papers/docling/agent-protocols-survey-2025.md) | 2025 | 2504.16736 | First comprehensive protocol survey |
| [Anatomy of Agentic Memory](../papers/docling/anatomy-agentic-memory-2026.md) | 2026 | 2602.19320 | Taxonomy of memory systems with empirical analysis of failures |
| [Agentic RAG Survey](../papers/docling/agentic-rag-survey-2025.md) | 2025 | 2501.09136 | Survey of agentic patterns applied to RAG |
| [Deep Research Agent Survey](../papers/docling/deep-research-survey-2025.md) | 2025 | 2512.02038 | Comprehensive survey of deep research agents: 3-stage roadmap, 4 key components (query planning, information acquisition, memory management, answer generation), optimization techniques |

---

### O. Deep Research Agents

Autonomous systems that retrieve, analyze, and synthesize web-scale information into structured research outputs — the emerging North Star capability for agent systems.

| Paper | Year | arXiv | Core Contribution |
|-------|------|-------|-------------------|
| [Deep Research Agent Survey](../papers/docling/deep-research-survey-2025.md) | 2025 | 2512.02038 | Comprehensive survey: 3-stage roadmap (retrieve→reason→generate), 4 components (query planning, information acquisition, memory management, answer generation), optimization via prompting/SFT/agentic RL |
| [DeepVerifier: Self-Evolving Verification](../papers/docling/deep-verifier-self-evolving-2026.md) | 2026 | 2601.15808 | Rubric-guided verification pipeline for DRAs; DRA Failure Taxonomy (5 categories, 13 sub-types); "asymmetry of verification" thesis; 8-11% accuracy gains |
| [Step-DeepResearch](../papers/docling/step-deep-research-2025.md) | 2025 | 2512.20491 | Atomic capability decomposition (planning, seeking, reflection, writing); progressive training (mid-training→SFT→RL); Checklist-style Judger; ADR-Bench for Chinese deep research; 32B model rivaling proprietary services |
| [FINDER & DEFT Failure Taxonomy](../papers/docling/useful-deep-research-agents-2025.md) | 2025 | 2512.01948 | FINDER benchmark (100 tasks, 419 checklist items); DEFT taxonomy (14 failure modes across reasoning/retrieval/generation); reveals DRAs struggle with evidence integration, not comprehension |
| [RL Design Choices for Deep Research](../papers/docling/rl-deep-research-design-2025.md) | 2025 | 2510.15862 | Systematic ablation: RLOO > GRPO, AI feedback > rule-based rewards, error-tolerant rollout; SOTA 7B-scale agent across 10 benchmarks |
| [SFR-DeepResearch](../papers/docling/sfr-deep-research-rl-2025.md) | 2025 | 2509.06283 | Continual RL on reasoning models for autonomous single-agent DR; self-managed memory buffer; 28.7% on Humanity's Last Exam |
| [DeepPlanner: Advantage Shaping](../papers/docling/deep-planner-advantage-shaping-2025.md) | 2025 | 2510.12979 | Planning tokens have higher entropy than execution tokens; entropy-based advantage shaping allocates larger RL updates to planning; SOTA with 10x less training data |
| [ResearStudio: Controllable Agents](../papers/docling/researstudio-controllable-agents-2025.md) | 2025 | 2510.12194 | First human-intervenable deep research framework; Collaborative Workshop (transparency + symmetrical control + role fluidity); SOTA on GAIA in fully autonomous mode |
| [RE-Searcher: Robust Agentic Search](../papers/docling/re-searcher-self-reflection-2025.md) | 2025 | 2509.26048 | Goal-oriented planning + self-reflection for search robustness; quantifies search fragility from query perturbation; explicit Search→Reflect→Answer loop |
| [MAS²: Self-Rectifying Multi-Agent](../papers/docling/mas2-self-rectifying-2025.md) | 2025 | 2509.24323 | Generator-implementer-rectifier tri-agent meta-system; Collaborative Tree Optimization for training; 19.6% gains on deep research; cross-backbone generalization |

**The deep research paradigm**: These papers collectively establish deep research as a distinct agent paradigm — not just "search with an LLM" but autonomous long-horizon decision-making requiring planning, multi-turn retrieval, evidence integration, cross-source verification, and structured synthesis.

**Key findings across the category**:

- **Search ≠ Research** (Step-DeepResearch): Retrieval accuracy is necessary but insufficient. True research requires intent decomposition, logical structuring, and cross-source verification — capabilities that must be trained as "atomic capabilities," not just prompted.

- **DRAs fail at verification, not comprehension** (FINDER/DEFT): The most common failure modes are strategic content fabrication (39%), insufficient evidence integration (32%), and fact-checking failures — not task misunderstanding.

- **Verification is asymmetrically easier than generation** (DeepVerifier): A separate verification pipeline with rubric-guided evaluation yields 8-11% accuracy gains. This "asymmetry of verification" thesis suggests investing in verifiers provides better returns than scaling generators.

- **RL design choices matter enormously** (RL Design Choices): RLOO significantly outperforms GRPO for deep research training. AI feedback (even from cheap models) far outperforms rule-based F1 rewards. Error-tolerant test-time rollout (letting agents recover from mistakes rather than terminating) provides "free" accuracy gains.

- **Planning tokens need special treatment** (DeepPlanner): Planning tokens exhibit 2.4x higher entropy than execution tokens during RL training. Entropy-based advantage shaping that allocates larger updates to these high-uncertainty planning decisions dramatically improves planning quality.

- **Human control and autonomy coexist** (ResearStudio): The first framework enabling real-time human intervention (pause, edit plan, resume) during deep research achieves SOTA on GAIA even in fully autonomous mode — proving that controllability doesn't sacrifice capability.

- **Single-agent vs. multi-agent for DR**: SFR-DeepResearch and Step-DeepResearch achieve strong results with single-agent architectures, while MAS² shows the value of specialized meta-agent teams. The emerging view: single agents generalize better; multi-agent systems excel at specific workflows.

**The training recipe for deep research** (emerging consensus from RL Design Choices, Step-DeepResearch, SFR-DR):
1. Start from a reasoning-optimized model (not base or instruction-tuned)
2. Progressive training: agentic mid-training → SFT on atomic capabilities → RL with AI-feedback rewards
3. Use RLOO over GRPO for policy optimization
4. Synthesize challenging training data (existing benchmarks are too easy)
5. Implement self-managed context/memory (agents that clean their own memory)
6. Error-tolerant inference (let agents recover from parsing errors and bad tool calls)

---

### P. Self-Reflection, Verification & Self-Correction

Making agents that check their own work — moving from prompted reflection to trained, internalized self-correction as a first-class capability.

| Paper | Year | arXiv | Core Contribution |
|-------|------|-------|-------------------|
| [CoRefine: Confidence-Guided Self-Refinement](../papers/docling/corefine-self-refinement-2026.md) | 2026 | 2602.08948 | 211K-parameter Conv1D controller using token-level confidence (HALT/RETHINK/ALTERNATIVE); 190x token reduction vs 512-sample voting; 92.6% precision on confident halts |
| [SPOC: Spontaneous Self-Correction](../papers/docling/spontaneous-self-correction-2025.md) | 2025 | 2506.06923 | Interleaved solution+verification in single pass; dual-role proposer+verifier model; PairSFT + message-wise online RL; +8.8-20% on math benchmarks |
| [Structured Reflection for Tool Use](../papers/docling/structured-reflection-tool-use-2025.md) | 2025 | 2509.18847 | Trainable Reflect→Call→Final strategy; DAPO+GSPO training; multi-dimensional reward (format, tool-name, parameter, semantic); Tool-Reflection-Bench |
| [Reflection-Driven Control](../papers/docling/reflection-driven-control-2025.md) | 2025 | 2512.21354 | Plan-Reflect-Verify framework; pluggable Reflex Module (self-checker + reflective prompt engine + reflective memory); closed-loop control for code generation; AAAI 2026 Workshop |
| [Dyna-Think: World Model Simulation](../papers/docling/dyna-think-world-model-2025.md) | 2025 | 2506.00320 | Internalizes world model simulation into thinking; DIT (imitation learning) + DDT (Dyna-style training); critique generation for world model training; 2x fewer tokens than R1 at similar BoN performance |
| [DeepVerifier: Rubric-Guided Verification](../papers/docling/deep-verifier-self-evolving-2026.md) | 2026 | 2601.15808 | Verification as a separate pipeline from generation; rubric-guided evaluation; DRA Failure Taxonomy for systematic error categorization |
| [RE-Searcher: Goal-Oriented Self-Reflection](../papers/docling/re-searcher-self-reflection-2025.md) | 2025 | 2509.26048 | Explicit goal articulation + reflection on whether retrieved evidence satisfies the goal; resists spurious search cues; robustness through reflection |

**The evolution of self-reflection in agents**:

```
Era 1 (2023): Prompted Reflection
  Reflexion: "What went wrong?" → verbal feedback → episodic memory
  Limitation: Reflection quality depends on prompting; no guarantee of improvement

Era 2 (2024-2025): Structured Reflection  
  Structured Reflection: Trainable Reflect→Call→Final with RL
  RE-Searcher: Goal-oriented planning + reflection after each search
  Limitation: Still multi-turn; adds latency; needs explicit triggers

Era 3 (2025-2026): Internalized Verification
  SPOC: Spontaneous interleaved solution+verification in single pass
  CoRefine: Confidence-as-control-signal with neural controller
  Dyna-Think: World model simulation inside the thinking process
  Key shift: Verification becomes *part of generation*, not a separate step
```

**Key tensions within this category**:

- **External vs. internal verification**: DeepVerifier uses a separate verification pipeline (external). SPOC and Dyna-Think internalize verification into the model's generation process (internal). CoRefine takes a hybrid approach — a tiny external controller (211K params) consuming the model's internal confidence signals. The tradeoff: external verification is more reliable but slower; internal verification is faster but may share the generator's blind spots.

- **When to verify**: CoRefine's HALT/RETHINK/ALTERNATIVE framework makes the "when to stop" decision a first-class problem. The controller learns problem-specific stopping criteria — halting early on confident answers, exploring more on hard problems. This adaptive compute allocation is more efficient than fixed verification budgets.

- **Confidence as a signal, not a guarantee**: CoRefine's key insight — use confidence for adaptive compute allocation rather than as a direct correctness estimate. Even imperfectly calibrated confidence can guide useful refinement decisions. This reframing avoids the well-known problem of LLM overconfidence.

- **World models for agents**: Dyna-Think demonstrates that agents can improve by predicting what will happen next (compressed world model simulation) rather than just acting and observing. Critique-style world model training — where the model learns to critique its own predictions — is particularly effective for improving downstream policy performance.

**Design principles for self-correcting agents** (emerging from this body of work):

1. **Make reflection trainable**: Use RL to train explicit Reflect→Act patterns, not just prompting. Multi-dimensional rewards (format, tool-name, parameter quality, semantic correctness) provide richer learning signals.
2. **Use confidence as a control signal**: Token-level and trace-level confidence patterns can drive HALT/RETHINK/ALTERNATIVE decisions without ground-truth verification.
3. **Interleave verification with generation**: Single-pass approaches (SPOC) that generate solution→verify→revise without external prompts are more efficient and deployable.
4. **Separate the roles, share the weights**: SPOC's dual-role (proposer+verifier) formulation with shared parameters achieves multi-agent benefits at single-model cost.
5. **Build pluggable verification modules**: Reflection-Driven Control's Reflex Module (self-checker + reflective prompt engine + reflective memory) is reusable across tasks without retraining the base agent.

---

## III. Cross-Cutting Tensions in Detail

### The Context Window Paradox

More context should help agents, but multiple papers show it can hurt:
- **Long-Context Reasoning Limits** shows extra context degrades reasoning
- **Hold Onto That Thought** shows aggressive KV eviction causes longer (compensatory) reasoning
- **Structured Context Engineering** shows diminishing returns of format optimization as model capability increases
- **Active Context Compression** shows 22.7% token reduction with *identical* accuracy — meaning those tokens were noise

**The implication**: The goal isn't to fill the context window — it's to curate it. Context engineering is as much about what to *exclude* as what to include.

### The Training vs. Scaffolding Spectrum

Not a binary choice — every paper occupies a position on this spectrum:

```
Pure Scaffolding ←──────────────────────→ Pure Training
  │                                              │
  ReAct            MemGPT        daVinci-Dev    Tool-R0
  SWE-agent        Pensieve      MAGE
  12-Factor        CaveAgent     DR-MAS
  Context Eng.     AgentArk      SPOC
                   RE-Searcher   Struct. Reflection
                   CoRefine      SFR-DR
```

The trend is rightward: more training, more internalized capability. But the most successful systems (daVinci-Dev, Pensieve, Step-DeepResearch) use *both* — trained models with good scaffolding.

### The Verification Gap

A new cross-cutting theme revealed by the deep research and self-reflection papers:

- **Deep research papers** show that DRAs fail primarily at verification: strategic content fabrication (39%), evidence integration failures (32%), fact-checking errors (FINDER/DEFT)
- **Self-reflection papers** show that prompted self-correction is unreliable, but *trained* self-correction works (SPOC +8.8-20%, Structured Reflection significant multi-turn improvement)
- **The bridge**: DeepVerifier and CoRefine demonstrate that lightweight verification modules can catch errors that even powerful generators produce. The cost of verification is 1-10% of the cost of regeneration.

This suggests a new architectural principle: **every agent that generates should also verify**, and verification should be a trained capability, not just a prompt.

### The Trust Problem

Every category touches trust from a different angle:
- **Safety papers**: Can we trust agents not to be hijacked?
- **Evaluation papers**: Can we trust agents to perform reliably?
- **Memory papers**: Can we trust agents to remember correctly?
- **Protocol papers**: Can we trust tool descriptions to be accurate?
- **Multi-agent papers**: Can we trust agents to coordinate without cascading errors?

No paper claims to have solved trust. The field's honest position: graduated trust through evaluation, guardrails as safety nets, and human-in-the-loop as the ultimate fallback.

---

## IV. Timeline View

### 2022: The Foundations
- Chain-of-Thought Prompting, ReAct

### 2023: The Cambrian Explosion
- Reflexion, Tree of Thoughts, LATS, Voyager, Generative Agents
- MemGPT, CoALA, DSPy, Toolformer, Gorilla
- MetaGPT, AutoGen, SWE-bench, AgentBench
- Three major surveys (Xi, Wang, LLM-Agents)

### 2024: Maturation & Critical Analysis
- SWE-agent (ACI concept), DynaSaur, OpenHands
- ADAS (agents designing agents), Mixture-of-Agents
- OSWorld, τ-bench, AgentBoard, Agent-as-a-Judge
- **Brittle ReAct** — the first major critical paper challenging the foundations
- Self-Discover — toward self-adaptive reasoning

### 2025 H1: The Protocol & Safety Year
- Context engineering becomes a named discipline (Monadic CE)
- MCP becomes the de facto standard; first security analyses (Securing MCP)
- Agent protocol surveys codify the MCP + A2A convergence
- LlamaFirewall, Attention-based PI Defense (NDSS 2026)
- KVzip (NeurIPS 2025 Oral) — inference optimization matures
- Agentic RAG, Agentic AI Frameworks surveys
- Test-time scaling survey (Art of Scaling)
- Spontaneous Self-Correction (SPOC) — single-pass interleaved verification
- Dyna-Think — world model simulation meets agent reasoning

### 2025 H2: The Deep Research & Self-Reflection Wave
- **Deep research agents emerge as a distinct paradigm**: SFR-DeepResearch (28.7% HLE), Step-DeepResearch (61.42 ResearchRubrics), ResearStudio (SOTA GAIA)
- **RL design for deep research systematized**: RLOO > GRPO, AI feedback > rule-based rewards, error-tolerant rollout
- **Planning optimization**: DeepPlanner shows planning tokens need special RL treatment (entropy-based advantage shaping)
- **Self-reflection becomes trainable**: Structured Reflection (DAPO+GSPO for Reflect→Call→Final), RE-Searcher (goal-oriented reflection)
- **Deep research failure taxonomies**: FINDER/DEFT (14 failure modes), DeepVerifier DRA Taxonomy (5 categories, 13 sub-types)
- **Human-AI collaboration for research**: ResearStudio's Collaborative Workshop (pause, edit, resume)
- **Multi-agent meta-systems**: MAS² (generator-implementer-rectifier with Collaborative Tree Optimization)
- **Comprehensive DR survey**: Deep Research Agent Survey establishes the 3-stage roadmap

### 2026 (Jan–Mar): The Memory, Training & Verification Wave
- **Memory explosion**: Aeon, Hippocampus, AriadneMem, Anatomy, Adaptive Admission, MemExRL, Evaluating Memory Structure, Contextual Memory Virtualisation, Neural Paging, Pensieve
- **RL training surge**: Tool-R0, MAGE, DR-MAS, Exploratory Memory RL
- **Agentic mid-training**: daVinci-Dev establishes a new training paradigm
- **Architecture formalization**: AgentOS, Auton, Formalizing Agent Designs, CaveAgent
- **MCP deep dives**: Design Choices, Server Smells, Information Fidelity, Atlas Benchmark
- **Safety maturation**: AgentSentry, ICON, AgentSys, VIGIL
- **Code agent specialization**: SWE-Adept, RIG, Theory of Code Space
- **Multi-agent advances**: AgentArk (distillation), CORAL (A2A), CASTER, GAIA2
- **Verification as architecture**: CoRefine (confidence-guided self-refinement, 211K controller), DeepVerifier (rubric-guided verification for DRAs)

---

> **Applied**: The [Research Agent Blueprint](../blueprints/research-agent/) synthesizes insights from Pensieve (self-context engineering), CASTER (multi-agent workflow), Agentic RAG (retrieval orchestration), and HiMAC (hierarchical planning) into a working .NET 10 implementation using the Microsoft Agent Framework. The new deep research papers (Step-DeepResearch, SFR-DR, RL Design Choices) validate the blueprint's architectural choices: single-agent with tools, progressive refinement, and self-managed context.

> **Reading this catalogue**: Start with the [Core Tensions](#core-tensions) to understand the intellectual landscape. Then dive into the category most relevant to your work. New readers interested in agent design should prioritize Categories O (Deep Research) and P (Self-Reflection) for the cutting edge, and Categories A (Foundations) and D (Architecture) for fundamentals. Papers are linked to their full Docling-extracted markdown in `papers/docling/`.
