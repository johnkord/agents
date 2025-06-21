# Agents Research & Knowledge Base

> A comprehensive research repository on AI agent design, engineering, and implementation.
> 
> Started: 2026-03-07 | Last updated: 2026-04-10

---

## 📖 Research Documents

### Fundamentals
| # | Document | Description |
|---|---|---|
| 01 | [What Are AI Agents?](research/fundamentals/01-what-are-agents.md) | First principles: definitions, the agent loop, workflows vs agents distinction (Anthropic), autonomy spectrum, failure modes, and the three eras of agent design |

### Techniques
| # | Document | Description |
|---|---|---|
| 02 | [Context Engineering](research/techniques/02-context-engineering.md) | The core discipline — "the art of providing the right information in the right format at the right time" (Schmid). Context as a system, the seven principles, token budgets, pre-fetching, error compaction |
| 04 | [Tool Use & Function Calling](research/techniques/04-tool-use-function-calling.md) | ACI (Agent-Computer Interface), tool design principles, poka-yoke, tools as structured outputs, MCP protocol, and the tool explosion problem |
| 05 | [Memory Systems](research/techniques/05-memory-systems.md) | CoALA memory taxonomy (working, episodic, semantic, procedural), storage backends, retrieval strategies, consolidation patterns, and the stateless-reducer tension |
| 08 | [Planning & Reasoning](research/techniques/08-planning-reasoning.md) | Chain-of-thought, tree-of-thought, reflexion, hierarchical decomposition, adaptive re-planning, metacognition, and knowing when to stop |
| 10 | [Language Selection for Agents](research/techniques/10-language-selection-for-agents.md) | Language analysis for agent-generated code: static vs dynamic typing, GC vs manual memory, Rust, Go, C#, Java, TypeScript, compile times, and the context engineering framing |
| 11 | [Self-Reflection & Verification](research/techniques/11-self-reflection-verification.md) | Self-correction, verification strategies, reflection loops, and when self-repair helps vs hurts |

### Architecture Patterns
| # | Document | Description |
|---|---|---|
| 03 | [Architecture Patterns](research/patterns/03-architecture-patterns.md) | Workflows (prompt chaining, routing, parallelization) and Agents (ReAct, Plan-Execute, Autonomous) — with Anthropic's simplicity-first principle and anti-patterns |
| 06 | [Multi-Agent Systems](research/patterns/06-multi-agent-systems.md) | Communication topologies, human-as-tool-call, the framework landscape (2026), and why most teams move away from frameworks |

### Evaluation
| # | Document | Description |
|---|---|---|
| 07 | [Evaluation & Reliability](research/evaluation/07-evaluation-reliability.md) | Benchmarking, LLM-as-judge, compounding error math, sandboxed testing, guardrails, and the reliability ladder |

---

## 🏗️ Blueprints

Implementation guides for building specific agent types:

| Blueprint | Description | Status |
|---|---|---|
| [Generic Agent](blueprints/generic-agent/09-implementation-blueprint.md) | Full implementation blueprint with agent loop, context assembler, tool executor, guardrails, and tools library | ✅ Complete |
| [Research Agent](blueprints/research-agent/) | General-purpose research agent (.NET 10 + Microsoft Agent Framework) | ✅ Complete |
| [Coding Agent (Forge)](blueprints/coding-agent/) | SWE-bench-style coding agent with verification, guardrails, and session management | 🔧 In Progress |
| [Life Agent](blueprints/life-agent/) | Audio lifelogging and life-augmentation agent | 🔧 In Progress |
| [MCP Server](blueprints/mcp-server/) | Model Context Protocol server implementation | 🔧 In Progress |

---

## 📚 Knowledge Base

Reference materials and taxonomies:

| Document | Description |
|---|---|
| [Agent & Copilot Taxonomy](knowledge-base/agent-taxonomy.md) | Comprehensive classification by autonomy level, domain, architecture, and design decision matrix |
| [Research Paper Catalogue](knowledge-base/paper-catalogue.md) | 91 papers organized by theme with intellectual landscape analysis, core tensions, schools of thought, and timeline view |

### Additional Knowledge Base

| Document | Description |
|---|---|
| [Audio Lifelogging Research](knowledge-base/audio-lifelogging-research.md) | Continuous recording, transcription, and memory augmentation |
| [Human Wellness Research](knowledge-base/human-wellness-research.md) | What a life agent should track — health, habits, longevity |
| [Long-Running Life Augmentation Agents](knowledge-base/long-running-life-augmentation-agents.md) | Persistent, cloud-hosted, proactive AI agents for daily life |

### Guides

| Document | Description |
|---|---|
| [GitHub Copilot Customization Guide](research/copilot-customization-guide.md) | Custom instructions, agent skills, prompt files, MCP servers, and hooks |

---

## 📄 Research Papers (126 papers)

126 academic papers downloaded from arXiv and converted to Markdown via [Docling](https://github.com/docling-project/docling). Full conversions are in [`papers/docling/`](papers/docling/). See the [Research Paper Catalogue](knowledge-base/paper-catalogue.md) for the complete organized listing with intellectual landscape analysis.

Below are highlights from the foundational papers:

### Foundational Agent Frameworks
| Paper | Year | Key Contribution |
|---|---|---|
| [Chain-of-Thought Prompting](papers/docling/chain-of-thought-prompting-2022.md) | 2022 | Foundational reasoning technique; emergent ability at scale; CoT as context engineering |
| [ReAct: Synergizing Reasoning and Acting](papers/docling/react-reasoning-acting-2022.md) | 2022 | Interleaved Thought→Action→Observation loop; foundation for most modern agents |
| [Reflexion: Verbal Reinforcement Learning](papers/docling/reflexion-verbal-reinforcement-2023.md) | 2023 | Self-reflection as episodic memory; 91% HumanEval without weight updates |
| [Tree of Thoughts](papers/docling/tree-of-thoughts-2023.md) | 2023 | Tree search over reasoning paths; 4%→74% on Game of 24 |
| [CoALA: Cognitive Architectures for Language Agents](papers/docling/cognitive-architectures-coala-2023.md) | 2023 | Formal framework unifying all agent architectures; memory taxonomy |
| [LATS: Language Agent Tree Search](papers/docling/lats-tree-search-2023.md) | 2023 | MCTS + LM agents; 92.7% HumanEval |
| [Generative Agents: Stanford Smallville](papers/docling/generative-agents-stanford-2023.md) | 2023 | 25 agents in a sandbox; memory streams, reflection, emergent social behavior |
| [Voyager: Lifelong Learning Agent](papers/docling/voyager-lifelong-learning-2023.md) | 2023 | First LLM-powered embodied lifelong learner; skill library as code; 3.3x more items discovered |
| [DSPy: Compiling Declarative LM Pipelines](papers/docling/dspy-declarative-lm-pipelines-2023.md) | 2023 | Programming model replacing prompt engineering with modules + compiler; automated context engineering |
| [MemGPT: LLMs as Operating Systems](papers/docling/memgpt-llm-operating-system-2023.md) | 2023 | OS-inspired virtual context management; self-directed memory paging |
| [SELF-DISCOVER: Self-Composed Reasoning](papers/docling/self-discover-reasoning-structures-2024.md) | 2024 | LLMs self-compose task-specific reasoning structures; +32% over CoT; 10-40x fewer inference calls |
| [Brittle Foundations of ReAct](papers/docling/brittle-react-prompting-2024.md) | 2024 | Critical analysis: ReAct gains come from exemplar similarity, not interleaved reasoning |

### Tool Use & Interface Design
| Paper | Year | Key Contribution |
|---|---|---|
| [Toolformer](papers/docling/toolformer-self-taught-tools-2023.md) | 2023 | Self-supervised tool learning; 6.7B model matches 175B GPT-3 with tools |
| [Gorilla: LLM Connected with Massive APIs](papers/docling/gorilla-api-calling-2023.md) | 2023 | Retrieval-augmented API calling; smaller model + docs beats larger model |
| [SWE-agent: Agent-Computer Interface](papers/docling/swe-agent-aci-2024.md) | 2024 | ACI design principles; interface design > prompt optimization |
| [DynaSaur: Dynamic Action Creation](papers/docling/dynasaur-dynamic-actions-2024.md) | 2024 | Agents write new tools as code; growing action library |
| [OpenHands: Open Platform for AI Software Developers](papers/docling/openhands-software-agents-2024.md) | 2024 | CodeAct paradigm (code as universal action); sandboxed execution; 53%+ SWE-bench |

### Multi-Agent Systems
| Paper | Year | Key Contribution |
|---|---|---|
| [MetaGPT: Multi-Agent with SOPs](papers/docling/metagpt-multi-agent-sop-2023.md) | 2023 | SOPs from human software engineering; document-centric communication; ~60% reduced hallucination cascading |
| [AutoGen: Multi-Agent Conversation](papers/docling/autogen-multi-agent-conversation-2023.md) | 2023 | ConversableAgent abstraction; conversation patterns (two-agent, sequential, group, nested); human-in-the-loop native |
| [Mixture-of-Agents (MoA)](papers/docling/mixture-of-agents-2024.md) | 2024 | Layered multi-model architecture; open-source ensemble beats GPT-4 |
| [ADAS: Automated Design of Agentic Systems](papers/docling/adas-automated-design-2024.md) | 2024 | Meta agent discovers novel agent architectures; 7-9% improvement over hand-designed |
| [Agent Protocols Survey](papers/docling/agent-protocols-survey-2025.md) | 2025 | First comprehensive survey of agent communication protocols; MCP + A2A convergence |

### Memory & Context
| Paper | Year | Key Contribution |
|---|---|---|
| [Pensieve: Stateful Context Management](papers/docling/pensieve-stateful-context-2026.md) | 2026 | StateLM for self-directed memory management; 83.9% on needle-in-a-haystack at 2M tokens |
| [Active Context Compression](papers/docling/active-context-compression-2026.md) | 2026 | 22.7% token savings with 0% accuracy loss via information-density-preserving compression |
| [SWE-Pruner: Context Pruning for Code Agents](papers/docling/swe-pruner-context-pruning-2026.md) | 2026 | Goal-driven pruning cuts 23-54% of tokens while improving accuracy |

### Evaluation & Benchmarks
| Paper | Year | Key Contribution |
|---|---|---|
| [SWE-bench: Real GitHub Issues](papers/docling/swe-bench-github-issues-2023.md) | 2023 | 2,294 real software engineering tasks; best model went from 1.96% → 53%+ with agentic tools |
| [AgentBench: Evaluating LLMs as Agents](papers/docling/agentbench-evaluating-llms-2023.md) | 2023 | 8-environment benchmark; massive gap between commercial and open-source LLMs as agents |
| [AgentBoard](papers/docling/agentboard-evaluation-2024.md) | 2024 | Progress rate metric; fine-grained multi-turn evaluation across 9 environments |
| [OSWorld: Real Computer Benchmarks](papers/docling/osworld-computer-benchmark-2024.md) | 2024 | Real OS environments; best model 12.24% vs human 72.36%; GUI grounding is the bottleneck |
| [τ-bench: Tool-Agent-User Interaction](papers/docling/tau-bench-tool-agent-user-2024.md) | 2024 | pass^k reliability metric; even GPT-4o <50%; by ReAct/Reflexion authors |
| [Agent-as-a-Judge](papers/docling/agent-as-a-judge-2024.md) | 2024 | Evaluate agents with agents; ~0.85 human correlation vs 0.65 for LLM-as-a-Judge |
| [Agentic RAG Survey](papers/docling/agentic-rag-survey-2025.md) | 2025 | Agentic design patterns (reflection, planning, tool use) applied to RAG pipelines |

### Surveys & Taxonomies
| Paper | Year | Key Contribution |
|---|---|---|
| [LLM-Based Agents Survey (Xi et al.)](papers/docling/llm-agents-survey-2023.md) | 2023 | Comprehensive 86-page survey; Brain-Perception-Action framework |
| [Autonomous Agents Survey (Wang et al.)](papers/docling/autonomous-agents-survey-wang-2023.md) | 2023 | Unified framework: Profiling + Memory + Planning + Action modules; applications survey |

### Deep Research & Verification
| Paper | Year | Key Contribution |
|---|---|---|
| [Deep Research Survey](papers/docling/deep-research-survey-2025.md) | 2025 | Comprehensive survey of deep research systems |
| [DeepVerifier: Self-Evolving Verification](papers/docling/deep-verifier-self-evolving-2026.md) | 2026 | Decompose verification into sub-questions per failure taxonomy |

---

## 📝 Research Notes & Design Documents

Internal design documents and experiment observations from building agents:

| Document | Description |
|---|---|
| [MCP Server Transport Modes](research/mcp-server-transport-modes.md) | Critical comparison of stdio, HTTP, and Streamable HTTP transport modes |
| [Improving Task Tracking](research/improving-task-tracking.md) | Research and design for Forge's task tracking system |
| [PDF-to-Markdown Tools Comparison](research/pdf-to-markdown-tools-comparison.md) | Evaluation of tools for converting academic papers |
| [Operational Observations Design](research/operational-observations-design.md) | Design for tracking agent operational patterns |
| [Phase 5 Experiment Observations](research/phase-5-experiment-observations.md) | Findings from Forge coding agent experiments |
| [Phase 5A: Hypothesis-Driven Debugging](research/phase-5a-hypothesis-debugging-design.md) | Design for structured debugging approach |
| [Phase 5B: Proactive Clarification](research/phase-5b-proactive-clarification-design.md) | Design for agents that ask before acting |
| [Phase 5C: Episodic Consolidation](research/phase-5c-episodic-consolidation-design.md) | Design for session memory consolidation |
| [Phase 6D: Trajectory Analysis](research/phase-6d-trajectory-analysis-design.md) | Design for progress metrics and trajectory analysis |

---

## 🗺️ Reading Order

**If you're new to agent building**, read in this order:

1. **[What Are Agents?](research/fundamentals/01-what-are-agents.md)** — Foundations and the workflows-vs-agents distinction
2. **[Context Engineering](research/techniques/02-context-engineering.md)** — The most important skill ("agent failures are context failures")
3. **[Architecture Patterns](research/patterns/03-architecture-patterns.md)** — Know your options; start with the simplest
4. **[Tool Use](research/techniques/04-tool-use-function-calling.md)** — How agents take action; invest more here than in prompts
5. **[Memory Systems](research/techniques/05-memory-systems.md)** — How agents remember
6. **[Planning & Reasoning](research/techniques/08-planning-reasoning.md)** — How agents think
7. **[Multi-Agent Systems](research/patterns/06-multi-agent-systems.md)** — Scaling with teams (but try single-agent first)
8. **[Evaluation](research/evaluation/07-evaluation-reliability.md)** — Making agents reliable
9. **[Implementation Blueprint](blueprints/generic-agent/09-implementation-blueprint.md)** — Build one
10. **[Language Selection](research/techniques/10-language-selection-for-agents.md)** — Choosing the right language for agent-generated code

---

## 🔑 Key Insights (Quick Reference)

### Core Mental Models

- **Agents = LLM + Tools + Loop + Memory + Goal** — but most production "agents" are actually **workflows** (predefined code paths with LLM steps)
- **Context engineering > prompt engineering** — context is a *system*, not a string (Schmid)
- **Agent failures are context failures, not model failures** — Anthropic spent more time optimizing tools than prompts for SWE-bench
- **ACI (Agent-Computer Interface) > UI** — tool descriptions, error messages, and output formats are the agent's interface
- **The model is the engine, context is the fuel** — garbage in, garbage out, regardless of model capability
- **Own your prompts, own your context window, own your control flow** (12-Factor Agents)

### The One-Liners

- **Build for failure first, success second** — $0.95^{10}$ per-step accuracy = 60% end-to-end
- **Start as a copilot, graduate to autonomous** — trust must be earned through evidence from evals
- **Use the simplest architecture that achieves your reliability goals** — don't use a framework where a while-loop suffices (Anthropic)
- **Tool descriptions are a form of context engineering** — they're the primary way you communicate intent to the model
- **Memory is the scaffolding that turns a stateless function into a stateful agent**
- **The evaluator's rubric IS the specification**
- **Working memory is the most underappreciated memory type**
- **The handoff summary is the most important piece of multi-agent communication**
- **Metacognition turns a reactive system into a self-monitoring system** — the best tool is sometimes `request_clarification`

### The Design Principles

1. **Relevance over recency** — Not everything recent is relevant
2. **Compression without loss** — Fit more signal into less tokens
3. **Structure signals intent** — How you format context changes how the model uses it
4. **Examples > instructions** — Show, don't tell
5. **Single responsibility tools** — One tool, one job
6. **Rich error messages** — Compact errors into context; the error IS the model's feedback
7. **Verify after modify** — Always check your work
8. **Graceful degradation** — Agents that say "I can't" are better than agents that hallucinate
9. **Human contact is a tool call** — Not a failure state, but a smart decision (12-Factor)
10. **Poka-yoke your tools** — Make incorrect usage impossible (absolute paths > relative paths)

---

##  Directory Structure

```
agents/
├── README.md                          ← You are here
├── LICENSE                            ← MIT License
├── .github/
│   ├── copilot-instructions.md        ← Copilot custom instructions
│   └── skills/
│       ├── convert-paper/SKILL.md     ← Paper conversion skill
│       ├── forge-improve/SKILL.md     ← Forge session analysis skill
│       └── research-agent-investigation/SKILL.md
├── research/
│   ├── fundamentals/
│   │   └── 01-what-are-agents.md
│   ├── techniques/
│   │   ├── 02-context-engineering.md
│   │   ├── 04-tool-use-function-calling.md
│   │   ├── 05-memory-systems.md
│   │   ├── 08-planning-reasoning.md
│   │   ├── 10-language-selection-for-agents.md
│   │   └── 11-self-reflection-verification.md
│   ├── patterns/
│   │   ├── 03-architecture-patterns.md
│   │   └── 06-multi-agent-systems.md
│   ├── evaluation/
│   │   └── 07-evaluation-reliability.md
│   └── copilot-customization-guide.md
├── knowledge-base/
│   ├── agent-taxonomy.md
│   ├── paper-catalogue.md             ← 126 papers with intellectual landscape
│   ├── audio-lifelogging-research.md
│   ├── human-wellness-research.md
│   └── long-running-life-augmentation-agents.md
├── papers/
│   ├── pdfs/                          ← Source PDFs from arXiv
│   └── docling/                       ← Markdown conversions (126 papers)
├── blueprints/
│   ├── generic-agent/
│   │   └── 09-implementation-blueprint.md
│   ├── research-agent/               ← .NET 10 + Microsoft Agent Framework
│   ├── coding-agent/                 ← Forge coding agent (in progress)
│   ├── life-agent/                   ← Audio lifelogging agent (in progress)
│   └── mcp-server/                   ← MCP server implementation (in progress)
└── scripts/
    └── convert_papers.py              ← arXiv download + Docling pipeline
```

---

## 🔮 Roadmap

### Phase 1: Research Foundation ✅
- [x] 11 research documents covering fundamentals, techniques, patterns, and evaluation
- [x] Agent & Copilot taxonomy
- [x] Generic agent implementation blueprint (Python)

### Phase 2: Academic Research Integration ✅
- [x] 126 research papers collected from arXiv (2022–2026)
- [x] Automated PDF → Markdown pipeline via Docling
- [x] Research paper catalogue with intellectual landscape analysis
- [x] Research findings integrated throughout knowledge base documents

### Phase 3: Implementation Blueprints (In Progress)
- [x] Research Agent — .NET 10 + Microsoft Agent Framework (complete, clean build)
- [x] Language selection analysis for agent-generated code
- [x] Copilot customization guide
- [ ] Coding agent (Forge) — verification, guardrails, session management
- [ ] Life agent — audio lifelogging and life-augmentation
- [ ] MCP server — Model Context Protocol implementation
- [ ] Multi-agent orchestrator

### Phase 4: Future
- [ ] Evaluation harness and benchmark suite
- [ ] MCP server implementations for common tools
- [ ] Self-improving agents (learning from trajectories)
- [ ] Cost optimization strategies
