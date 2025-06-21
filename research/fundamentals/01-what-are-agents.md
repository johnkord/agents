# What Are AI Agents? — First Principles

> *Last updated: 2026-03-07*

## Definition

An **AI agent** is a system that uses a language model as its core reasoning engine to autonomously decide what actions to take, execute those actions via tools, observe the results, and iterate until a goal is satisfied. The critical distinction from a simple chatbot or completion endpoint is the **action-observation loop**: agents _do things_ in the world rather than merely generating text.

More precisely, an agent = **LLM + Tools + Loop + Memory + Goal**.

### Workflows vs. Agents

An important architectural distinction (formalized by Anthropic) separates the broader category of **agentic systems** into two types:

- **Workflows**: LLMs and tools orchestrated through *predefined code paths*. The developer controls the sequence of steps. LLMs are used at specific points for classification, generation, or transformation — but the overall flow is deterministic.
- **Agents**: LLMs *dynamically direct their own processes and tool usage*, maintaining control over how they accomplish tasks. The flow is not predetermined — the model decides what to do next at each step.

This distinction matters because **most production "AI agents" are actually workflows** — mostly deterministic code with LLM steps sprinkled in at just the right points to make the experience feel intelligent. The 12-Factor Agents project (18k+ stars) found that few production, customer-facing AI products use the full "here's your prompt, here's a bag of tools, loop until done" pattern. Rather, they are comprised of mostly just software.

**The implication**: Don't reach for a fully autonomous agent when a workflow will do. Find the simplest solution possible and only increase complexity when a simpler approach demonstrably falls short.

## The Cognitive Architecture Analogy

Think of an agent as a software system modeled loosely on human cognition:

| Human Cognition | Agent Equivalent |
|---|---|
| Working memory | Context window (prompt + recent history) |
| Long-term memory | Vector stores, databases, file systems |
| Sensory input | Tool outputs, user messages, environment observations |
| Reasoning | LLM inference (chain-of-thought, planning) |
| Motor output | Tool calls (API calls, code execution, file writes) |
| Metacognition | Self-reflection, error correction, re-planning |

This analogy is imperfect but useful. It highlights that building a good agent isn't just about picking the right model — it's about designing the _surrounding cognitive architecture_.

A comprehensive academic survey (Xi et al., 2023, "The Rise and Potential of LLM-Based Agents") formalizes this with the **Brain-Perception-Action** framework:
- **Brain (LLM Core):** Language understanding, knowledge storage, reasoning, and learning
- **Perception:** Text, vision, audio — how the agent receives information
- **Action:** Text generation, tool use, environment interaction — how the agent affects the world

This maps closely to our cognitive analogy and underscores that agents are not just models — they're systems with sensing, processing, and effecting components.

A second comprehensive survey (Wang et al., 2023, "A Survey on Autonomous Machine Intelligence", arXiv:2308.11432) proposes a complementary **unified framework** decomposing agents into four modules:
- **Profiling:** Defines the agent's role and behavior (persona, expertise, constraints)
- **Memory:** Short-term (context window) and long-term (external storage) — see Doc 05
- **Planning:** With/without feedback; decomposition, reflection, refinement — see Doc 08
- **Action:** Tool use, code execution, environment interaction — see Doc 04

This four-module framework is more practitioner-oriented than the Brain-Perception-Action framework, and maps directly to implementation decisions. It also surveys agent applications across social science, natural science, and engineering — demonstrating the breadth of agent deployment.

The **CoALA framework** (Sumers et al., 2023, "Cognitive Architectures for Language Agents") goes further, proposing a rigorous cognitive architecture with:
- **Modular memory:** Working, episodic, semantic, and procedural memory as distinct components
- **Structured action space:** Internal actions (reasoning, retrieval, learning) and external actions (grounding, acting)
- **Decision cycle:** Observe → Retrieve → Reason → Act → Learn

CoALA unifies systems ranging from simple ReAct agents to complex architectures like Generative Agents under a single formal framework. It demonstrates that the space of possible agent architectures is vast and most current systems only explore a small corner of it.

Anthropically, thinks of this as the **Agent-Computer Interface (ACI)** — the surfaces through which the agent interacts with its environment (tools, APIs, file systems). Just as decades of HCI (Human-Computer Interface) research went into making software usable by humans, we need similar investment in making environments usable by agents. Tool descriptions, parameter naming, error messages, output formats — all of these are the agent's UX.

## Core Principles

### 1. The Agent Loop

The fundamental unit of agency is the **perceive → think → act → observe** loop:

```
while not goal_achieved:
    observation = perceive(environment)
    context = build_context(observation, memory, goal)
    thought = llm.reason(context)
    action = select_action(thought)
    result = execute(action)
    memory.update(result)
    goal_achieved = evaluate(result, goal)
```

Every agent framework (LangChain, CrewAI, AutoGen, custom loops) implements some variation of this. The differences lie in:
- How context is constructed (context engineering)
- How actions are selected (tool routing, planning)
- How memory is managed (windowed, summarized, persistent)
- How evaluation happens (self-check, external validation, human-in-the-loop)

### 2. Grounding

Agents must be **grounded** — connected to real data, tools, and environments. An ungrounded agent is just a chatbot hallucinating. Grounding mechanisms include:
- **Tool use**: Search, calculators, code execution, API calls
- **Retrieval-Augmented Generation (RAG)**: Pulling relevant documents into context
- **Code execution**: Running generated code to verify correctness
- **Environment interaction**: File systems, browsers, terminals, databases

### 3. Autonomy Spectrum

Not all agents need full autonomy. There's a spectrum:

```
Completion → Copilot → Semi-autonomous Agent → Fully Autonomous Agent
    ↑           ↑              ↑                        ↑
 No loop    Human-in-     Human approves          Runs to
             loop per      critical steps       completion
             turn                               autonomously
```

**Key insight**: Start with copilot-level autonomy. Earn trust. Increase autonomy as reliability improves. The most production-ready systems are copilots that occasionally escalate to human judgment.

### 4. Failure Modes Are the Real Design Challenge

The hard part of building agents isn't getting them to work — it's getting them to fail gracefully. Common failure modes:

| Failure Mode | Description | Mitigation |
|---|---|---|
| **Looping** | Agent repeats same action forever | Max iteration limits, loop detection |
| **Hallucinated tools** | Agent invokes tools that don't exist | Strict tool schemas, validation |
| **Context overflow** | Too much information, model loses focus | Context management, summarization |
| **Goal drift** | Agent pursues tangential objectives | Regular goal re-checking |
| **Cascading errors** | One bad step poisons subsequent reasoning | Checkpointing, error recovery |
| **Overconfidence** | Agent declares success prematurely | Verification steps, assertions |

## The Three Eras of Agent Design

### Era 1: Prompt-and-Pray (2023)
- Chain-of-thought prompting
- Simple ReAct loops
- Fragile, no reliability guarantees
- "Let the model figure it out"

### Era 2: Engineered Scaffolding (2024-2025)
- Structured tool schemas
- Multi-step planning with validation
- Retrieval-augmented context
- Human-in-the-loop guardrails
- Framework explosion (LangChain, LlamaIndex, etc.)
- Foundational research: ReAct (Yao et al., 2022), Reflexion (Shinn et al., 2023), Tree of Thoughts (Yao et al., 2023), Toolformer (Schick et al., 2023), Chain-of-Thought (Wei et al., 2022)
- Open platforms: OpenHands (Wang et al., 2024) — open-source platform with CodeAct paradigm (code as universal action space), sandboxed execution, 188+ contributors
- Emerging evaluation: SWE-bench, HumanEval, AgentBoard, AgentBench, τ-bench
- Critical analysis: Verma et al. (2024) showed ReAct's benefits are largely driven by exemplar-query similarity, not the interleaved reasoning format — a key corrective to cargo-culting ReAct

### Era 3: Context Engineering (2025-2026)
- Heavy focus on _what goes into the prompt_, not just the model
- Dynamic context assembly based on task state
- Sophisticated memory architectures
- Model-agnostic agent designs
- Emphasis on evaluation and reliability
- "The prompt is the program"
- The rise of ACI (Agent-Computer Interface) design
- 12-Factor Agents principles: own your prompts, own your context window, own your control flow

We are firmly in Era 3. **Context engineering** — the discipline of dynamically providing the right information and tools, in the right format, at the right time — is now the dominant skill in agent building. As Tobi Lutke (Shopify CEO) put it: context engineering is "the art of providing all the context for the task to be plausibly solvable by the LLM." Andrej Karpathy has echoed the same shift.

The key insight of this era: **most agent failures are context failures, not model failures.** A mediocre model with excellent context routinely outperforms a frontier model with poor context.

## What Makes a Good Agent?

A good agent scores well on these dimensions:

1. **Reliability**: Does it consistently produce correct results?
2. **Efficiency**: Does it solve problems in a reasonable number of steps?
3. **Graceful degradation**: When it can't solve something, does it recognize this?
4. **Transparency**: Can a human understand what it did and why?
5. **Composability**: Can it be combined with other agents/systems?
6. **Adaptability**: Can it handle novel situations not in its training data?

## Key Takeaways

- Agents are not magic. They are software systems with predictable failure modes.
- The LLM is the CPU; everything else (tools, memory, context) is the rest of the computer.
- **Most production agents are workflows**, not fully autonomous loops. Use the simplest architecture that works.
- Context engineering > prompt engineering. What you feed the model matters more than how you ask.
- **Agent failures are context failures.** Before blaming the model, examine what information was (or wasn't) in context.
- Invest in ACI design as much as you'd invest in HCI design. Tool descriptions are a form of prompt engineering.
- Start as a copilot, graduate to autonomous agent.
- Build for failure first, success second.
- Own your prompts, own your context window, own your control flow. Don't hide behind framework abstractions you don't understand.

---

*Next: [Context Engineering Deep Dive](../techniques/02-context-engineering.md)*
