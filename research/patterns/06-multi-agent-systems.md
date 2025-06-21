# Multi-Agent Systems: Coordination, Communication, and Emergence

> *Last updated: 2026-03-07*

## Why Multiple Agents?

Single agents hit ceilings:
- **Context window limits**: One agent can't hold all the information for a complex project
- **Specialization**: A single prompt can't make a model expert at coding AND research AND data analysis
- **Parallelism**: Sequential execution is too slow for large tasks
- **Reliability**: A single point of failure; one hallucination can cascade

Multi-agent systems address these by distributing intelligence across specialized, coordinated agents.

**But first, a critical reality check**: The 12-Factor Agents project (Factor 10) emphasizes building **small, focused agents** over monolithic ones. The industry has largely learned that you should decompose at the *agent* level, not the *task* level. Instead of one big agent that does everything, build several small agents that each do one thing well — and coordinate them with ordinary software, not another LLM.

The key question before going multi-agent: **Can you solve this with one focused agent and a good tool set?** If yes, do that. Multi-agent coordination adds latency, cost, and debugging complexity.

## Communication Topologies

How agents talk to each other defines the system's behavior:

### Hub-and-Spoke (Orchestrated)
```
         ┌──── Agent A
         │
Hub ─────┼──── Agent B
         │
         └──── Agent C
```
One central orchestrator coordinates all communication. Agents don't talk to each other.

**Pros**: Simple control flow, easy to debug, clear authority
**Cons**: Bottleneck at hub, hub must understand all domains, single point of failure

### Peer-to-Peer (Collaborative)
```
Agent A ◄──► Agent B
   ▲            ▲
   │            │
   └──► Agent C ◄┘
```
Agents communicate directly with each other.

**Pros**: No bottleneck, emergent collaboration, resilient
**Cons**: Hard to debug, potential for infinite loops, unclear authority

### Hierarchical
```
           Strategist
          /          \
    Manager A      Manager B
    /      \        /      \
Worker 1  Worker 2  Worker 3  Worker 4
```
Multiple levels of coordination. Each level adds abstraction.

**Pros**: Scales to large problems, clear delegation, mirrors human organizations
**Cons**: Communication overhead, telephone game (information loss through layers)

### Pipeline
```
Agent A ──► Agent B ──► Agent C ──► Output
(Research)  (Analyze)   (Write)
```
Each agent completes its work before passing to the next.

**Pros**: Simple, predictable, each agent has clean input/output contract
**Cons**: No parallelism, can't go back to earlier stages without full restart

### Blackboard
```
         ┌─────────────────┐
         │   Blackboard     │
         │  (shared state)  │
         └──┬──┬──┬──┬─────┘
            │  │  │  │
          A  B  C  D  (agents read/write to shared state)
```
Agents share a common workspace (blackboard) and contribute to it independently.

**Pros**: Flexible, agents can work asynchronously, good for creative tasks
**Cons**: Race conditions, coordination complexity, requires careful state management

## Multi-Agent Frameworks & Patterns

### Pattern 1: Debate (Adversarial Collaboration)

Two or more agents argue different positions, a judge synthesizes:

```
┌─────────────┐     ┌─────────────┐
│  Advocate A  │     │  Advocate B  │
│  (argues for)│     │(argues agst) │
└──────┬───────┘     └──────┬───────┘
       │                    │
       └────────┬───────────┘
                ▼
         ┌──────────────┐
         │    Judge      │
         │  (synthesize) │
         └──────────────┘
```

**Why this works**: LLMs have a tendency toward agreeable, surface-level responses. Adversarial debate forces deeper analysis.

**Use cases**: 
- Code review (one agent writes, another critiques)
- Decision analysis (agents argue for different options)
- Fact-checking (one agent claims, another verifies)

```python
def debate(topic: str, rounds: int = 3) -> str:
    advocate_a_msgs = [{"role": "system", "content": "Argue FOR the proposed approach. Be specific and evidence-based."}]
    advocate_b_msgs = [{"role": "system", "content": "Argue AGAINST the proposed approach. Find weaknesses."}]
    
    for round in range(rounds):
        a_argument = llm.generate(advocate_a_msgs + [{"role": "user", "content": topic}])
        advocate_b_msgs.append({"role": "user", "content": f"Counterargue: {a_argument}"})
        
        b_argument = llm.generate(advocate_b_msgs)
        advocate_a_msgs.append({"role": "user", "content": f"Respond to critique: {b_argument}"})
    
    judge_prompt = f"""
    Topic: {topic}
    Arguments FOR: {collect_a_arguments}
    Arguments AGAINST: {collect_b_arguments}
    
    Synthesize the strongest points from both sides into a balanced recommendation.
    """
    return llm.generate([{"role": "user", "content": judge_prompt}])
```

### Pattern 2: Ensemble Voting

Multiple agents solve the same problem independently, results are aggregated.

**Research validation**: Two papers provide strong evidence for this pattern:

- **Mixture-of-Agents (MoA)** (Wang et al., 2024): A layered architecture where each agent sees all outputs from the previous layer. Using only open-source LLMs, MoA surpassed GPT-4 Omni on AlpacaEval (65.1% vs 57.5%). Key insight: **diverse models > multiple copies of the same model** — heterogeneous agents with complementary strengths produce the best synthesis.

- **"More Agents Is All You Need"** (Li et al., 2024): Performance scales with agent count following a **log-linear relationship**. Improvement correlates with task difficulty — harder tasks benefit more from additional agents. This simple sampling-and-voting approach is **orthogonal** to other enhancements (can be combined with CoT, self-consistency, etc.).

```python
async def ensemble_solve(problem: str, n_agents: int = 5) -> str:
    # Generate N independent solutions
    solutions = await asyncio.gather(*[
        solve_independently(problem, agent_id=i, temperature=0.7 + i*0.1)
        for i in range(n_agents)
    ])
    
    # Have a meta-agent compare and select the best
    selection_prompt = f"""
    Problem: {problem}
    
    {n_agents} independent solutions were generated:
    {format_solutions(solutions)}
    
    Analyze each solution. Select the best one, or synthesize a superior 
    answer combining the strongest elements. Explain your reasoning.
    """
    return llm.generate(selection_prompt)
```

**When to use**: High-stakes decisions where accuracy matters more than cost. Works because LLM errors are somewhat random — multiple independent runs are unlikely to make the same mistake.

### Pattern 3: Specialization Assembly Line

Different agents handle different aspects of a complex task:

**MetaGPT (Hong et al., 2023, ICLR 2024)** formalized this pattern by applying **Standard Operating Procedures (SOPs) from human software engineering** to multi-agent collaboration:

```
MetaGPT Role Hierarchy:
  Product Manager → generates requirements document
  Architect → produces system design document
  Engineer → writes code following the design
  QA Engineer → creates and runs tests
```

**Key innovations:**
- **Document-centric communication**: Agents share structured documents (PRDs, design docs, code) rather than free-form messages — reducing hallucination cascading by ~60%
- **Role-specific expertise**: Each agent has domain-constrained tools and output schemas
- **Sequential dependency**: Each role's output becomes a structured input for the next
- **Human process modeling**: The SOP pattern maps directly from how human teams work

This demonstrates that **multi-agent coordination benefits enormously from structured protocols** rather than free-form conversation.

```python
class ResearchPipeline:
    def __init__(self):
        self.searcher = Agent(
            system_prompt="You are a research specialist. Find relevant sources.",
            tools=[web_search, academic_search]
        )
        self.analyzer = Agent(
            system_prompt="You are an analytical specialist. Extract key findings and identify patterns.",
            tools=[read_document, extract_data]
        )
        self.writer = Agent(
            system_prompt="You are a technical writer. Synthesize research into clear documents.",
            tools=[create_file, format_markdown]
        )
        self.reviewer = Agent(
            system_prompt="You are a critical reviewer. Check for accuracy, gaps, and bias.",
            tools=[fact_check, search]
        )
    
    async def execute(self, topic: str) -> str:
        sources = await self.searcher.run(f"Find sources about: {topic}")
        analysis = await self.analyzer.run(f"Analyze these sources: {sources}")
        draft = await self.writer.run(f"Write a report based on: {analysis}")
        review = await self.reviewer.run(f"Review this report: {draft}")
        
        if review.needs_revision:
            draft = await self.writer.run(f"Revise based on: {review.feedback}\n\nOriginal: {draft}")
        
        return draft
```

### Pattern 4: Agent Swarm

Dynamic creation and destruction of agents based on task needs:

```python
class AgentSwarm:
    def __init__(self, max_agents: int = 10):
        self.max_agents = max_agents
        self.active_agents: dict[str, Agent] = {}
        self.results: dict[str, Any] = {}
    
    async def solve(self, task: str):
        # Orchestrator decomposes task
        subtasks = await self.decompose(task)
        
        # Spawn agents for each subtask
        for subtask in subtasks:
            agent_type = self.select_agent_type(subtask)
            agent = self.spawn_agent(agent_type, subtask)
            self.active_agents[subtask.id] = agent
        
        # Run in parallel with dependency awareness
        results = await self.execute_with_dependencies(subtasks)
        
        # Synthesis
        return await self.synthesize(results)
    
    def spawn_agent(self, agent_type: str, subtask: SubTask) -> Agent:
        """Create a purpose-built agent for a specific subtask"""
        config = AGENT_CONFIGS[agent_type]
        return Agent(
            system_prompt=config.prompt_template.format(subtask=subtask),
            tools=config.tools,
            max_steps=config.max_steps,
            model=config.model  # Can use different models for different tasks!
        )
```

## Inter-Agent Communication Protocols

### Standardized Protocols (Emerging 2024-2025)

The Agent Protocols Survey (Yang et al., 2025, arXiv:2504.16736) identifies a convergence toward two complementary standards:

**MCP (Model Context Protocol) — Anthropic**: Agent ↔ Tool communication
- JSON-RPC 2.0 transport, standardized tool discovery and invocation
- Becoming the de facto standard (adopted by Cursor, Windsurf, OpenAI, Google)
- 1000+ MCP servers in community registries

**A2A (Agent-to-Agent) — Google**: Agent ↔ Agent communication
- Agent Cards for capability discovery
- Task lifecycle management with streaming
- Built for enterprise multi-agent orchestration

The emerging standard stack:
```
Transport:      HTTP + JSON-RPC / REST
Discovery:      Agent Cards / Tool Schemas
Agent↔Tool:     MCP
Agent↔Agent:    A2A
Security:       OAuth 2.0
Orchestration:  Framework-specific (LangGraph, CrewAI, etc.)
```

**Security remains the biggest open challenge**: prompt injection via tools, tool impersonation, data exfiltration, and privilege escalation all lack robust mitigations.

### AutoGen Conversation Framework (Wu et al., 2023)

AutoGen (Microsoft Research) formalizes multi-agent interaction around a **ConversableAgent** abstraction. Every agent — human or AI — implements the same interface for sending and receiving messages. Key patterns:
- **Two-agent chat**: Simple back-and-forth (e.g., coder + reviewer)
- **Sequential chat**: Chain of pairwise conversations
- **Group chat**: Multiple agents in a shared conversation with a speaker-selection policy
- **Nested chat**: A conversation between agents can itself be an "inner" chat within a broader orchestration

AutoGen makes **human-in-the-loop** native — humans are just another ConversableAgent. This design is elegant and aligns with the 12-Factor pattern of modeling human contact as a tool call.

### Message Passing

Agents communicate via structured messages:

```python
@dataclass
class AgentMessage:
    sender: str
    recipient: str
    type: Literal["request", "response", "info", "error", "handoff"]
    content: str
    metadata: dict = field(default_factory=dict)
    
    # For handoffs — what the receiving agent needs to know
    context_transfer: Optional[dict] = None  # Key facts to carry forward
    task_state: Optional[str] = None  # Where we are in the workflow
```

### Shared Context / Blackboard

Agents read and write to a shared state:

```python
class SharedBlackboard:
    def __init__(self):
        self.state: dict[str, Any] = {}
        self.history: list[BlackboardEntry] = []
        self.lock = asyncio.Lock()
    
    async def write(self, agent_id: str, key: str, value: Any):
        async with self.lock:
            old = self.state.get(key)
            self.state[key] = value
            self.history.append(BlackboardEntry(
                agent=agent_id, action="write", key=key,
                old_value=old, new_value=value, timestamp=now()
            ))
    
    async def read(self, key: str) -> Any:
        return self.state.get(key)
    
    async def subscribe(self, key_pattern: str, callback):
        """Agent gets notified when matching keys change"""
        ...
```

### Handoff Protocol

When one agent needs to transfer control to another:

```python
class Handoff:
    """Everything the receiving agent needs to continue the work"""
    
    def __init__(self):
        self.task_description: str = ""          # What needs to be done
        self.context_summary: str = ""           # What's been learned so far
        self.completed_steps: list[str] = []     # What's already done
        self.remaining_steps: list[str] = []     # What's left
        self.important_constraints: list[str] = []  # Gotchas, limits
        self.artifacts: dict[str, str] = {}      # Files/data created
        self.failed_approaches: list[str] = []   # What was tried and didn't work
```

**Critical insight**: The handoff summary is the most important piece. If it's low quality, the receiving agent starts from scratch. If it's high quality, the receiving agent picks up seamlessly. This is context engineering applied to inter-agent communication.

### Human-in-the-Loop as a Tool Call

The 12-Factor Agents framework (Factor 7) makes an elegant design choice: **contact humans with tool calls.** Instead of treating human oversight as a special architectural concern, model it as just another tool:

```python
def ask_human(question: str, context: str = "") -> str:
    """Ask a human for input, clarification, or approval.
    Use when: you're uncertain about a decision, need approval for a 
    destructive action, or the task specification is ambiguous.
    Returns: The human's response as a string."""
    # In practice: send to Slack, email, web UI, etc.
    return notify_and_wait(question, context)
```

This means the LLM can *decide when* to escalate to a human, using the same mechanism it uses for any other tool. No special architectural support needed — the agent just calls `ask_human` when it's uncertain. It's simpler and more flexible than hardcoded checkpoints.

## Coordination Challenges

### 1. The Telephone Game Problem
Information degrades as it passes through multiple agents. Each agent summarizes, and each summary loses nuance.

**Mitigation**: Include raw data alongside summaries. Let receiving agents access original sources.

### 2. Conflicting Actions
Two agents both try to edit the same file simultaneously.

**Mitigation**: Locking, turn-taking, or designated ownership of files/resources.

### 3. Redundant Work
Without awareness of each other, agents may solve the same subtask.

**Mitigation**: Shared task board (blackboard pattern), or orchestrator tracking.

### 4. Emergent Deadlock
Agent A waits for Agent B's output, Agent B waits for Agent A's output.

**Mitigation**: Timeouts, dependency graph analysis before dispatch, fallback behaviors.

### 5. Cost Explosion
N agents × M steps × K tokens = serious API costs.

**Mitigation**: Cheaper models for simpler agents, caching, tight step limits.

## When to Use Multi-Agent vs. Single Agent

| Factor | Single Agent | Multi-Agent |
|---|---|---|
| Task complexity | Simple to moderate | High |
| Domain breadth | Narrow | Wide (multiple domains) |
| Parallelization potential | Low | High |
| Required reliability | Moderate | High (via redundancy) |
| Acceptable latency | Low | Higher tolerance |
| Budget | Constrained | Flexible |
| Debugging needs | Must be easy | Can accept complexity |

**Rule of thumb**: If you can solve it with one agent in < 15 steps without context window pressure, use one agent. Multi-agent when you genuinely need parallelism, specialization, or context isolation.

## Emerging Patterns (2025-2026)

### Agent-as-a-Service
Agents exposed as APIs that other agents can call, creating an ecosystem:
```
Your Agent ──API call──► GitHub Agent (SaaS)
           ──API call──► Database Agent (internal)
           ──API call──► Research Agent (third-party)
```

### Self-Organizing Agent Networks
Agents that dynamically form teams based on task requirements, without centralized orchestration. Still largely research-stage.

### Human-Agent Teams
Agents as team members alongside humans, each with complementary strengths:
- **Agents**: Fast information processing, tireless execution, broad knowledge
- **Humans**: Judgment, creativity, stakeholder communication, ethical reasoning

The best multi-agent systems of 2026 include humans as first-class agents with defined interfaces.

### The Framework Landscape (2026)

Multi-agent frameworks have proliferated:
- **Claude's Agent SDK**: Lightweight, focuses on tool use and orchestration
- **Strands Agents SDK (AWS)**: Enterprise-grade, AWS service integrations
- **LangGraph**: State machine-based agent orchestration with durable execution
- **AutoGen (Microsoft)**: Research-oriented multi-agent conversations — the ConversableAgent abstraction (Wu et al., 2023) is the most rigorous formalization of agent communication patterns
- **CrewAI**: Role-based agent teams with shared memory
- **MetaGPT**: SOP-driven multi-agent with document-centric communication (Hong et al., 2023)

However, the pattern from production is clear: **most teams that ship customer-facing multi-agent products eventually move away from frameworks** and build thin orchestration layers around direct API calls. Frameworks help with prototyping; production demands control.

---

*Next: [Evaluation and Reliability](../evaluation/07-evaluation-reliability.md)*
