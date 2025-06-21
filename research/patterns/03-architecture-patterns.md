# Agent Architecture Patterns

> *Last updated: 2026-07-15*

## Overview

Agent architecture is the structural design of how an agent's components (LLM, tools, memory, planning, execution) are connected and coordinated. Choosing the right architecture for your problem is often more important than choosing the right model.

Anthropic's "Building Effective Agents" guide (2024) drew an important distinction that frames everything in this document:

> **Workflows** are systems where LLMs and tools are orchestrated through predefined code paths. **Agents** are systems where LLMs dynamically direct their own processes and tool usage.

Both are valid. Workflows offer predictability and consistency for well-defined tasks; agents offer flexibility when model-driven decision-making is needed. **The most successful implementations use simple, composable patterns rather than complex frameworks.**

The cardinal rule: find the simplest solution possible, and only increase complexity when it demonstrably improves outcomes. Many applications need nothing more than a single optimized LLM call with good retrieval and in-context examples.

This document catalogs the major patterns, their trade-offs, and when to use each.

---

## Pattern 1: Simple ReAct Loop

**The foundational pattern.** Named after the 2022 paper "ReAct: Synergizing Reasoning and Acting in Language Models."

```
┌─────────┐
│  Input   │
└────┬─────┘
     │
     ▼
┌─────────────────────────────────┐
│  Thought: (reasoning step)       │◄──────┐
│  Action: (tool to call)          │       │
│  Observation: (tool result)      │───────┘
│  ... repeat ...                  │
│  Final Answer: (output)          │
└─────────────────────────────────┘
```

**How it works:**
1. Model receives task + tool descriptions
2. Model reasons about what to do (Thought)
3. Model selects and invokes a tool (Action)
4. Tool result is appended to context (Observation)
5. Repeat until model decides it has the answer

**Strengths:**
- Simple to implement and debug
- Works well for straightforward tool-use tasks
- Transparent reasoning chain

**Weaknesses:**
- No explicit planning — the model is myopic (one step at a time)
- Context grows linearly with steps — long tasks overflow
- No error recovery strategy — one bad step can derail everything
- No parallelism — strictly sequential

**Critical caveat (Brittle Foundations of ReAct):** Verma et al. (2024) showed that ReAct's benefits come primarily from **exemplar-query similarity**, not the interleaved Thought-Act format. The reasoning traces may be post-hoc rationalizations rather than genuine reasoning. Before adopting ReAct, test whether simpler approaches with well-matched few-shot examples achieve comparable results. The takeaway: invest in exemplar selection as much as the reasoning scaffold.

**Best for:** Simple tool-use tasks with < 10 steps. Good starting point.

**Implementation sketch:**

```python
def react_loop(llm, tools, task, max_steps=15):
    messages = [
        {"role": "system", "content": build_system_prompt(tools)},
        {"role": "user", "content": task}
    ]
    
    for step in range(max_steps):
        response = llm.generate(messages)
        
        if response.has_final_answer:
            return response.final_answer
        
        if response.has_tool_call:
            result = execute_tool(response.tool_call, tools)
            messages.append({"role": "assistant", "content": response.raw})
            messages.append({"role": "tool", "content": result})
    
    return "Max steps reached without resolution"
```

---

## Pattern 2: Plan-and-Execute

**Separates planning from execution** to overcome ReAct's myopia.

```
┌─────────┐
│  Input   │
└────┬─────┘
     │
     ▼
┌──────────────┐
│   Planner    │──── Creates step-by-step plan
└──────┬───────┘
       │
       ▼
┌──────────────┐     ┌──────────────┐
│   Executor   │────▶│   Executor   │────▶ ... ────▶ Result
│   (Step 1)   │     │   (Step 2)   │
└──────────────┘     └──────────────┘
       │                    │
       ▼                    ▼
  (Observation)        (Observation)
       │                    │
       └────────┬───────────┘
                ▼
         ┌──────────────┐
         │   Re-planner │──── Adjusts plan based on observations
         └──────────────┘
```

**How it works:**
1. **Planner LLM call**: Given the task, produce a numbered plan
2. **Executor LLM calls**: Execute each step (potentially using ReAct for each step)
3. **Re-planner LLM call**: After each step, optionally revise remaining plan based on observations

**Strengths:**
- Better for complex, multi-step tasks
- Plan provides transparency and predictability
- Can validate plan before execution (human review, automated checks)
- Re-planning enables adaptation

**Weaknesses:**
- More LLM calls = higher latency and cost
- Plan quality depends heavily on the planner's understanding
- Rigid plans can be worse than flexible reactive behavior for exploratory tasks

**Best for:** Well-defined tasks with clear sequential structure. Software engineering, data pipelines, research with known methodology.

**Critical insight**: The plan itself becomes part of the context for execution steps. This is a form of _self-generated context engineering_ — the agent creates structured knowledge for its future self.

---

## Pattern 3: Router / Dispatcher

**A front-end classifier routes requests to specialized sub-agents.**

```
                    ┌─────────┐
                    │  Input   │
                    └────┬─────┘
                         │
                         ▼
                  ┌──────────────┐
                  │   Router     │
                  │   (classify) │
                  └──────┬───────┘
                         │
           ┌─────────────┼─────────────┐
           ▼             ▼             ▼
    ┌────────────┐ ┌────────────┐ ┌────────────┐
    │  Code Agent │ │  Data Agent│ │ Research   │
    │             │ │            │ │ Agent      │
    └────────────┘ └────────────┘ └────────────┘
```

**How it works:**
1. Router classifies the incoming request (via LLM classification or rule-based logic)
2. Request is dispatched to the appropriate specialized agent
3. Specialized agent handles the task with its own tools and context
4. Result is returned through the router

**Strengths:**
- Each sub-agent can be optimized independently (different models, tools, prompts)
- Clear separation of concerns
- Easy to add new capabilities (add a new sub-agent)
- Different token budgets per domain

**Weaknesses:**
- Router misclassification sends requests to wrong agent
- Cross-domain tasks are hard (requires multiple agents or handoff)
- More infrastructure to manage

**Best for:** Products/platforms serving diverse request types. Customer service, internal tools with multiple domains.

**Advanced variant: Hierarchical routing** — routers can be nested. A top-level router sends to domain routers, which send to capability-specific agents.

---

## Pattern 4: Evaluator-Optimizer (Generator-Critic)

**One model generates, another evaluates, iterate until quality threshold is met.**

```
┌─────────┐
│  Input   │
└────┬─────┘
     │
     ▼
┌──────────────┐
│  Generator   │◄──────────────────────┐
│  (draft)     │                       │
└──────┬───────┘                       │
       │                               │
       ▼                               │
┌──────────────┐     ┌──────────────┐  │
│  Evaluator   │────▶│  Feedback    │──┘
│  (critique)  │     │  (improve)   │
└──────────────┘     └──────────────┘
       │
       ▼ (when passes evaluation)
┌──────────────┐
│   Output     │
└──────────────┘
```

**How it works:**
1. Generator produces a candidate output
2. Evaluator assesses quality against criteria
3. If insufficient, feedback is sent back to generator for revision
4. Repeat until evaluation passes or max iterations reached

**Strengths:**
- Dramatically improves output quality through iteration
- Evaluator can be a different (possibly cheaper) model
- Evaluator criteria can be specific and measurable
- Natural quality gate before returning to user

**Weaknesses:**
- Multiple LLM calls per output — expensive
- Risk of infinite refinement loops
- Evaluator might have blind spots matching generator's
- Latency multiplied by iteration count

**Best for:** Code generation, writing, analysis — any task where quality varies and iterative refinement works.

**Key insight**: The evaluator's rubric IS the specification. Write your evaluator criteria as carefully as you'd write unit tests.

```python
EVALUATOR_RUBRIC = """
Score this code on:
1. Correctness: Does it handle edge cases? (0-10)
2. Readability: Clear names, good structure? (0-10)  
3. Efficiency: No unnecessary operations? (0-10)
4. Safety: Input validation, error handling? (0-10)

PASS threshold: All scores >= 7
If any score < 7, provide specific improvement instructions.
"""
```

---

## Pattern 5: Orchestrator-Workers

**A central orchestrator delegates subtasks to specialized workers, synthesizes results.**

```
                    ┌─────────┐
                    │  Input   │
                    └────┬─────┘
                         │
                         ▼
                  ┌──────────────┐
                  │ Orchestrator │
                  │  (decompose  │
                  │   & assign)  │
                  └──────┬───────┘
                         │
           ┌─────────────┼─────────────┐
           ▼             ▼             ▼
    ┌────────────┐ ┌────────────┐ ┌────────────┐
    │  Worker A  │ │  Worker B  │ │  Worker C  │
    │  (subtask) │ │  (subtask) │ │  (subtask) │
    └──────┬─────┘ └──────┬─────┘ └──────┬─────┘
           │              │              │
           └──────────────┼──────────────┘
                         │
                         ▼
                  ┌──────────────┐
                  │ Orchestrator │
                  │  (synthesize)│
                  └──────────────┘
                         │
                         ▼
                  ┌──────────────┐
                  │    Output    │
                  └──────────────┘
```

**How it works:**
1. Orchestrator analyzes task and decomposes into subtasks
2. Workers execute subtasks (potentially in parallel)
3. Orchestrator collects results and synthesizes final output
4. May iterate if synthesis reveals gaps

**Strengths:**
- Parallelism — workers can run concurrently
- Each worker has focused context (no context pollution across subtasks)
- Orchestrator maintains big-picture view
- Natural map-reduce structure

**Weaknesses:**
- Decomposition quality is critical — bad decomposition = bad results
- Inter-subtask dependencies require careful coordination
- Worker isolation means they can't share discoveries easily
- Orchestrator becomes a bottleneck/single point of failure

**Best for:** Research tasks, document analysis, code review across multiple files, any embarrassingly parallel problem.

---

## Pattern 6: Pipeline / Chain

**Fixed sequence of specialized stages, each transforming the output for the next.**

```
Input ──▶ Stage 1 ──▶ Stage 2 ──▶ Stage 3 ──▶ Output
          (extract)   (analyze)   (format)
```

**How it works:**
1. Each stage has a specific, narrow responsibility
2. Output of one stage is input to the next
3. No branching or loops (unlike ReAct)

**Strengths:**
- Predictable execution (always same number of LLM calls)
- Each stage can be independently optimized and tested
- Simpler than dynamic agents for well-understood workflows
- Easy to monitor and debug

**Weaknesses:**
- Inflexible — can't adapt to unexpected inputs
- Errors cascade forward
- Overkill for simple tasks, insufficient for complex ones

**Best for:** ETL processes, document processing, content pipelines with known structure.

---

## Pattern 7: Autonomous Agent with Tool Belt

**The most general pattern — a powerful model with many tools and broad instructions, running with minimal human oversight.**

```
┌─────────────────────────────────────────────┐
│  Autonomous Agent                            │
│                                              │
│  ┌──────────┐                               │
│  │   LLM    │ ← Rich system prompt           │
│  │ (reason) │ ← Dynamic context              │
│  └────┬─────┘ ← Tool belt (10-50+ tools)     │
│       │                                      │
│  ┌────▼─────────────────────────────────┐    │
│  │  Tool Belt                            │    │
│  │  ┌───┐ ┌───┐ ┌───┐ ┌───┐ ┌───┐      │    │
│  │  │FS │ │Web│ │DB │ │API│ │Git│ ...   │    │
│  │  └───┘ └───┘ └───┘ └───┘ └───┘      │    │
│  └──────────────────────────────────────┘    │
│                                              │
│  ┌──────────────────────────────────────┐    │
│  │  Memory (conversation + persistent)   │    │
│  └──────────────────────────────────────┘    │
│                                              │
│  ┌──────────────────────────────────────┐    │
│  │  Guardrails & Limits                  │    │
│  └──────────────────────────────────────┘    │
└─────────────────────────────────────────────┘
```

This is what systems like GitHub Copilot agent mode, Devin, Claude Code, and others implement. It's the most flexible but also the hardest to make reliable.

**Key design decisions:**
- Tool selection strategy (present all tools? dynamically select?)
- When to stop (fixed iterations? self-assessment? human checkpoint?)
- How to handle parallel tool calls
- Sandboxing and safety boundaries

---

## Pattern 8: CodeAct (Code as Universal Action)

**Introduced by OpenHands (Wang et al., 2024, ICLR 2025):** Instead of structured tool calls, agents express ALL actions as executable code.

```
┌─────────────────────────────────────────────┐
│  CodeAct Agent                               │
│                                              │
│  ┌──────────┐                               │
│  │   LLM    │ → Generates Python/Bash code   │
│  └────┬─────┘                                │
│       │                                      │
│  ┌────▼─────────────────────────────────┐    │
│  │  Sandboxed Runtime (Docker)           │    │
│  │  - Full Linux environment             │    │
│  │  - pip install available              │    │
│  │  - File system, git, network          │    │
│  │  - Browser automation                 │    │
│  └──────────────────────────────────────┘    │
└─────────────────────────────────────────────┘
```

**Key insight**: Code is the most flexible action representation:
- **Composable**: Chain operations with loops, conditionals, variables
- **Self-extending**: Agent can write helper functions and install packages dynamically
- **Turing-complete**: Any computable action is expressible
- **Auditable**: Code is readable by humans

**Trade-offs vs. structured tool calls:**
| Dimension | Structured Tools | CodeAct |
|---|---|---|
| Flexibility | Limited to defined tools | Unlimited |
| Safety | Easier to constrain | Requires sandboxing |
| Debugging | Clear tool traces | Code may be opaque |
| Composability | Chained by orchestrator | Native (programming constructs) |

OpenHands achieved state-of-the-art on SWE-bench (53%+) using this pattern.

---

## Choosing the Right Pattern

| Pattern | Complexity | Flexibility | Reliability | Cost | Best For |
|---|---|---|---|---|---|
| ReAct | Low | Medium | Medium | Low | Simple tool use |
| Plan-Execute | Medium | Medium | High | Medium | Structured tasks |
| Router | Medium | High | High | Low-Med | Multi-domain products |
| Evaluator-Optimizer | Medium | Low | High | High | Quality-critical output |
| Orchestrator-Workers | High | High | Medium | High | Parallel subtasks |
| Pipeline | Low | Low | High | Low | Known workflows |
| Autonomous | High | Very High | Variable | Variable | General-purpose |
| Deep Research Agent | High | High | High | High | Research & analysis |
| Generator-Verifier | Medium | Medium | Very High | Medium | Accuracy-critical |
| Meta-MAS (MAS²) | Very High | High | High | Very High | Complex multi-step |
| Human-Intervenable | High | Very High | Very High | Medium | High-stakes research |

## Hybrid Architectures

Real systems combine patterns. Anthropic emphasizes that "these building blocks aren't prescriptive — they're common patterns that developers can shape and combine to fit different use cases":

- **Router → Plan-Execute per domain**: Route to specialized agents, each using plan-execute
- **Orchestrator with Evaluator workers**: Workers generate, evaluator refines each worker output
- **Autonomous agent with Pipeline sub-tasks**: Top-level autonomous, but certain well-known sub-tasks use fixed pipelines for reliability
- **ReAct with periodic re-planning**: ReAct for 3-5 steps, then pause to re-plan
- **Plan-Reflect-Verify → Deep Research**: Plan-Execute + pluggable Reflex Module + self-managed memory = deep research agent
- **Generator-Verifier + ReAct**: Standard ReAct loop with a lightweight verification check after every N steps
- **Parallelization → Aggregation**: Anthropic identifies two key variants:
  - **Sectioning**: Break a task into independent subtasks run in parallel (e.g., one model processes the query while another screens for safety)
  - **Voting**: Run the same task multiple times with different prompts/temperatures to get diverse outputs, then aggregate

## Research-Validated Advanced Patterns

### Mixture-of-Agents (MoA)

Wang et al. (2024) demonstrated a **layered multi-model architecture** where each layer contains multiple LLM agents. Each agent in a layer takes all outputs from the previous layer as auxiliary context, refining and combining them:

```
Layer 1:  [Model A]  [Model B]  [Model C]  ← Each gets the original query
Layer 2:  [Model A]  [Model B]  [Model C]  ← Each gets all Layer 1 outputs
Layer 3:           [Aggregator]             ← Synthesizes final answer
```

Key finding: Using only **open-source LLMs**, MoA surpassed GPT-4 Omni on AlpacaEval (65.1% vs 57.5%). This demonstrates that collective, diverse expertise can exceed individual model capability. The pattern works best with **heterogeneous agents** (different models with different strengths) rather than homogeneous copies.

A simpler variant — **"More Agents Is All You Need"** (Li et al., 2024) — shows that simply sampling multiple responses and voting already improves performance, following a log-linear scaling relationship.

### LATS — Tree Search for Agents

LATS (Zhou et al., 2023) applies **Monte Carlo Tree Search** to agent execution, unifying ReAct + Tree of Thoughts + Reflexion. At each step, the agent explores multiple action paths, evaluates them using environment feedback, and backpropagates success signals:

- Same GPT-4 goes from **80% → 93%** on HumanEval just by adding tree search
- Failed trajectories generate reflections that improve future exploration
- UCT (Upper Confidence Bound for Trees) balances exploration vs exploitation

**When to use:** High-value tasks where accuracy justifies the compute cost (coding, complex reasoning). Not appropriate for real-time interactions.

### Automated Agent Design (ADAS)

Hu et al. (2024) showed that **agent architectures can be automatically discovered** by a meta agent. Meta Agent Search writes Python code defining new agents, evaluates them, and maintains an archive of designs that informs future search. Discovered agents outperformed hand-designed ones by 7-9% across coding, science, and math benchmarks — and **transferred across domains and models**.

Implication: The architecture patterns in this document are human-designed starting points. The space of possible agent architectures is far larger than what humans have explored, and automated search will likely discover non-obvious, superior designs.

### Deep Research Agent Architecture (2025)

The 2025 wave of deep research papers converged on a distinctive architecture for autonomous research:

```
┌──────────────────────────────────────────────┐
│                Deep Research Agent             │
│                                                │
│  ┌──────────┐  ┌──────────┐  ┌──────────────┐ │
│  │  Planner │→ │ Executor │→ │   Verifier   │ │
│  │(what next)│  │  (search, │  │ (check facts, │ │
│  │          │  │   browse, │  │  assess gaps) │ │
│  │          │  │   code)  │  │              │ │
│  └─────┬────┘  └──────────┘  └──────┬───────┘ │
│        │                             │         │
│        ▼                             ▼         │
│  ┌─────────────────────────────────────────┐   │
│  │      Self-Managed Working Memory         │   │
│  │  (notes, evidence, draft sections)       │   │
│  └─────────────────────────────────────────┘   │
└──────────────────────────────────────────────┘
```

Two viable architectures have emerged:

**Single-Agent Autonomous** (SFR-DeepResearch, Step-DeepResearch):
- One model handles planning, search, analysis, and writing
- Self-managed memory: the agent maintains its own working notes
- RL-trained with outcome rewards (rubric or checklist scores)
- Simpler, fewer coordination failures, easier to train

**Meta-Multi-Agent** (ResearStudio, MAS²):
- Specialized roles (planner, searcher, writer, verifier)
- External orchestration or self-organization
- Better for human-intervenable workflows
- Higher coordination overhead

**Key insight from SFR-DeepResearch**: A single 32B model with continual RL can match or exceed multi-agent systems that are individually larger. The training recipe matters more than the architecture — Autonomous Architecture + RL Training > Scaffolded Multi-Agent.

### Generator-Verifier Pattern (2025–2026)

A growing body of evidence shows that **separating generation from verification** yields outsized returns:

```
┌───────────┐     ┌────────────┐     ┌──────────┐
│ Generator │────▶│  Verifier  │────▶│  Output  │
│ (primary  │     │ (lightweight│     │  (if     │
│  model)   │     │  checker)  │     │  passes) │
└───────────┘     └─────┬──────┘     └──────────┘
                        │
                   ┌────▼────┐
                   │ Feedback │
                   │ (reject/ │
                   │ critique)│
                   └────┬────┘
                        │
                        ▼
                  Back to Generator
```

Variants:

1. **External Verifier** (DeepVerifier): Rubric-guided verification pipeline with outcome/process/rubric rewards. 5-category DRA Failure Taxonomy. The verifier is trained separately.

2. **Confidence Controller** (CoRefine): 211K-parameter controller that reads confidence traces from the generator and decides HALT/RETHINK/ALTERNATIVE. No backbone fine-tuning needed.

3. **Dual-Role Single Model** (SPOC): Same model acts as both generator and verifier in an interleaved single pass. Trained with PairSFT + message-wise online RL.

**The Asymmetry Thesis** (DeepVerifier): Verification is fundamentally easier than generation. A separate, lighter-weight verification module provides outsized returns because checking an answer is computationally cheaper than producing a correct one. This means: invest disproportionately in verification infrastructure.

### Meta-Multi-Agent System — MAS² (2025)

MAS² (Self-Rectifying Multi-Agent Systems) proposes a **tri-agent meta-system** with **Collaborative Tree Optimization**:

```
┌─────────────┐     ┌───────────────┐     ┌─────────────┐
│  Generator  │────▶│ Implementer   │────▶│  Rectifier  │
│ (propose    │     │ (execute      │     │ (evaluate & │
│  solutions) │     │  solutions)   │     │  improve)   │
└─────────────┘     └───────────────┘     └──────┬──────┘
                                                  │
                                           ┌──────▼──────┐
                                           │ Collaborative│
                                           │ Tree Search  │
                                           └─────────────┘
```

**Collaborative Tree Optimization**:
- Generator proposes K solutions at each node
- Implementer turns proposals into executable actions
- Rectifier evaluates and provides structured feedback
- Tree expands the most promising branches

The key innovation is that agents improve *each other's capabilities* through structured interaction — the rectifier's feedback systematically addresses the generator's weaknesses, creating a self-improving system.

### Human-Intervenable Research Agent (ResearStudio, 2025)

ResearStudio introduces a **Collaborative Workshop** model where a human can intervene at any stage:

```
Human (Instructor)
  │
  ├── Define research topic
  ├── Review/modify plan ◄── Agent proposes plan
  ├── Guide search focus ◄── Agent presents findings
  ├── Edit draft sections ◄── Agent generates report
  └── Final approval
```

Three components:
1. **MAS-based Research**: Multi-agent system handles autonomous research phases
2. **Self-Managed Memory**: Structured memory for evidence, notes, citations
3. **Instructor Interface**: Human can intervene at planning, search, analysis, or writing stages

**Design principle**: The agent should be capable of full autonomy but *pausable* at natural checkpoints for human guidance. This is particularly important for high-stakes research where factual accuracy matters.

**Golden rule**: Use the simplest architecture that achieves your reliability goals. Complexity is a tax on debuggability.

---

## Anti-Patterns to Avoid

### 1. The God Agent
One massive prompt, every tool, no structure. Works for demos, fails in production.

### 2. Over-Decomposition
Breaking tasks into so many sub-agents that coordination overhead exceeds the task itself.

### 3. Synchronous Everything
Sequential execution when subtasks are naturally parallel. Users wait unnecessarily.

### 4. No Escape Hatch
Agents without a way to say "I can't do this" or "I need human help." They'll hallucinate instead.

### 5. Copy-Paste Architecture
Using the same architecture for every problem. A research agent and a CRUD agent have fundamentally different needs.

### 6. Framework Lock-in
The 12-Factor Agents project found that most production-grade agents are built without frameworks. Framework abstractions often obscure the underlying prompts and responses, making debugging harder. If you use a framework, ensure you understand *exactly* what's happening under the hood. Most patterns can be implemented in a few lines of direct API calls.

---

*Next: [Tool Use and Function Calling](../techniques/04-tool-use-function-calling.md)*
