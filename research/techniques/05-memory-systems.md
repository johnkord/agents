# Memory Systems for Agents

> *Last updated: 2026-03-07*

## The Memory Problem

LLMs are stateless. Every inference call starts from scratch — the model has no inherent memory of prior interactions. Yet meaningful agent behavior requires continuity: remembering what was tried, what worked, what the user prefers, what the codebase looks like.

**Memory is the scaffolding that turns a stateless function into a stateful agent.**

The 12-Factor Agents framework makes a critical architectural point (Factor 5): **unify your execution state and business state.** Many agent systems maintain separate "agent state" and "application state" — the agent's conversation history in one place, the actual work product in another. This split creates synchronization bugs. Instead, make the agent's state *be* the application state whenever possible.

A practical example of this: Claude Code's `CLAUDE.md` files. Rather than maintaining memory in a separate database, the agent reads and writes persistent instructions in a plain markdown file that lives *in the project*. The memory IS the artifact. Humans can read, edit, and version-control it alongside their code.

## Memory Taxonomy

The **CoALA framework** (Sumers et al., 2023) provides the most rigorous academic taxonomy of agent memory, decomposing it into four types that map to cognitive science:

| Memory Type | Stores | Example | Persistence |
|---|---|---|---|
| **Working** | Current task state, scratchpad | "I've read 3/5 files, found the bug on line 47" | Ephemeral |
| **Episodic** | Past experiences and outcomes | "Last time I refactored auth, user wanted tests first" | Long-term |
| **Semantic** | Facts, knowledge, relationships | "This project uses Jest, prefers functional style" | Long-term |
| **Procedural** | Learned procedures and strategies | "To deploy: run tests → build → push → verify" | Long-term |

This taxonomy was validated by analyzing 20+ agent systems (ReAct, Reflexion, Generative Agents, Voyager, etc.) and showing they all use subsets of these four types.

```
                        Agent Memory
                            │
              ┌─────────────┼─────────────┐
              ▼             ▼             ▼
         Short-Term    Working        Long-Term
          Memory       Memory         Memory
              │             │             │
     ┌────────┤      ┌──────┤      ┌──────┤──────┐
     ▼        ▼      ▼      ▼      ▼      ▼      ▼
  Context  Message  Scratch- Task  Episodic Semantic Procedural
  Window   History   pad    State  Memory   Memory   Memory
```

### Short-Term Memory

**What it is**: Information available within the current context window.

**Includes:**
- Recent conversation messages
- Current tool results
- System prompt contents

**Characteristics:**
- Fast (already in context)
- Limited (token budget)
- Volatile (lost when context window resets)

**Management strategies:**
- Sliding window (keep last N messages)
- Relevance-weighted selection
- Compression/summarization of older messages

### Working Memory

**What it is**: The agent's actively maintained understanding of the current task.

**Includes:**
- Current plan and progress
- Scratchpad notes
- Hypotheses being tested
- Key facts extracted from tool calls

**This is the most underappreciated memory type.** Most agent frameworks don't have an explicit working memory — they rely on the conversation history, which is a poor substitute.

**Implementation:**

```python
class WorkingMemory:
    def __init__(self):
        self.plan: list[PlanStep] = []
        self.facts: dict[str, str] = {}  # key findings
        self.hypotheses: list[str] = []
        self.scratchpad: str = ""
        self.errors_encountered: list[ErrorRecord] = []
    
    def to_context_string(self) -> str:
        """Serialize working memory for injection into prompt"""
        sections = []
        
        if self.plan:
            plan_str = "\n".join(
                f"  {'✅' if s.done else '🔄' if s.in_progress else '⬜'} {s.description}"
                for s in self.plan
            )
            sections.append(f"Current Plan:\n{plan_str}")
        
        if self.facts:
            facts_str = "\n".join(f"  - {k}: {v}" for k, v in self.facts.items())
            sections.append(f"Key Facts:\n{facts_str}")
        
        if self.errors_encountered:
            err_str = "\n".join(f"  - {e.tool}: {e.message}" for e in self.errors_encountered[-3:])
            sections.append(f"Recent Errors:\n{err_str}")
        
        if self.scratchpad:
            sections.append(f"Notes:\n  {self.scratchpad}")
        
        return "\n\n".join(sections)
```

### Long-Term Memory

**What it is**: Persistent information that survives across sessions and conversations.

Three subtypes, borrowed from cognitive science:

#### Episodic Memory
**What happened** — records of past interactions and their outcomes.

```python
# Episodic memory record
{
    "episode_id": "ep_20260307_001",
    "timestamp": "2026-03-07T14:30:00Z",
    "task": "Refactor authentication module",
    "outcome": "success",
    "steps_taken": 12,
    "tools_used": ["read_file", "search", "edit_file", "run_tests"],
    "key_decisions": [
        "Chose OAuth2 over JWT for session management",
        "Added rate limiting middleware"
    ],
    "errors_encountered": [
        "Initially tried to modify read-only config — switched to env vars"
    ],
    "user_feedback": "Good, but next time check existing tests first"
}
```

**Use case**: Learning from past experiences. "Last time I refactored auth, the user wanted me to check tests first."

#### Semantic Memory
**What is known** — facts, knowledge, relationships.

```python
# Semantic memory entries
{
    "entity": "project/auth-service",
    "facts": {
        "language": "TypeScript",
        "framework": "Express",
        "test_framework": "Jest",
        "database": "PostgreSQL",
        "auth_method": "OAuth2",
        "deployment": "Kubernetes",
        "coding_style": "functional, avoid classes"
    },
    "relationships": [
        {"type": "depends_on", "target": "project/user-service"},
        {"type": "owned_by", "target": "team/platform"}
    ]
}
```

**Use case**: Project knowledge, user preferences, domain facts. "This project uses Jest for testing and prefers functional style."

#### Procedural Memory
**How to do things** — learned procedures and strategies.

```python
# Procedural memory entry
{
    "procedure": "deploy_to_staging",
    "steps": [
        "1. Run full test suite: npm test",
        "2. Build Docker image: docker build -t app:staging .",
        "3. Push to registry: docker push registry.internal/app:staging",
        "4. Update k8s deployment: kubectl set image deployment/app app=registry.internal/app:staging",
        "5. Verify health: curl https://staging.internal/health"
    ],
    "learned_from": "ep_20260301_003",
    "caveats": [
        "Must run from repo root, not subdirectory",
        "Takes ~3 minutes for pods to roll over"
    ]
}
```

**Use case**: Repeatable workflows the agent has learned or been taught.

**Voyager's Skill Library (Wang et al., 2023)** provides the most compelling example of procedural memory in practice. Voyager, a lifelong learning agent in Minecraft, stores every successfully learned skill as **executable JavaScript code** in a retrievable library. When facing a new task, it searches the library for relevant skills and composes them. Results: 3.3x more unique items discovered and 15.3x faster milestone completion vs. baselines. The key insight: **procedural memory should be executable code, not natural language descriptions** — code is unambiguous, composable, and directly actionable.

## Virtual Context / Memory Management

### MemGPT: OS-Inspired Memory Hierarchy (Packer et al., 2023)

MemGPT applies **operating systems memory management** concepts to LLM context windows:

```
┌──────────────────────────────────────┐
│  Main Context (Registers/L1 Cache)    │ ← ~8K tokens, immediate access
│  - System prompt                      │
│  - Current conversation tail          │
│  - Active working memory              │
├──────────────────────────────────────┤
│  Conversation Buffer (RAM)            │ ← Overflow from main context
│  - Recent messages paged out          │
│  - Retrievable on demand              │
├──────────────────────────────────────┤
│  Archival Storage (Disk)              │ ← Persistent, unlimited
│  - Full conversation history          │
│  - Documents, knowledge base          │
│  - Searchable via embeddings          │
└──────────────────────────────────────┘
```

**Key innovation**: The LLM itself manages its memory through **function calls**:
- `core_memory_append(key, value)` — add to working memory
- `core_memory_replace(old, new)` — update working memory
- `conversation_search(query)` — retrieve from conversation buffer
- `archival_memory_insert(content)` — persist to long-term storage
- `archival_memory_search(query)` — retrieve from archival storage

**Heartbeat mechanism**: After a memory management operation, the model gets another turn to continue processing — enabling multi-step memory operations within a single user turn.

**Implications for agent design**: Context windows are like physical RAM — finite and precious. Agents need virtual memory systems that intelligently page information in and out. MemGPT demonstrates this can be self-directed by the LLM rather than requiring external orchestration.

## Memory Storage Backends

| Backend | Best For | Retrieval | Scale |
|---|---|---|---|
| **In-context (prompt)** | Working memory, short-term | Instant (already loaded) | ~100K tokens |
| **Vector database** (Pinecone, Qdrant, Chroma) | Semantic search over docs/episodes | Semantic similarity | Millions of docs |
| **Key-value store** (Redis) | Fast fact lookup, session state | Exact key match | Billions of keys |
| **Relational DB** (PostgreSQL) | Structured episodic records | SQL queries | Billions of rows |
| **Graph database** (Neo4j) | Entity relationships, knowledge graphs | Graph traversal | Millions of nodes |
| **File system** | Persistent scratchpads, artifacts | Path-based | Disk-limited |

## Memory Retrieval Strategies

The hardest part of memory isn't storage — it's **retrieval**. Getting the right memory at the right time.

The most influential memory retrieval architecture comes from **Generative Agents** (Park et al., 2023) — the Stanford "Smallville" experiment where 25 AI agents simulated a town. Their three-factor retrieval model has become the reference standard:

```
Retrieval Score = α × recency + β × importance + γ × relevance
```

- **Recency:** Exponential decay from last access time (recent memories surface more easily)
- **Importance:** LLM-rated 1-10 at creation time (mundane = low, life events = high)
- **Relevance:** Embedding similarity to current situation

This is more sophisticated than pure vector similarity and models how humans actually recall information. Ablation studies showed **removing any single factor significantly degraded agent behavior** — all three are needed together.

Below are specific implementation strategies:

### Strategy 1: Semantic Similarity

Embed the current query, find the nearest memories:

```python
def retrieve_semantic(query: str, memory_store, top_k: int = 5):
    query_embedding = embed(query)
    results = memory_store.similarity_search(query_embedding, k=top_k)
    return [r.content for r in results if r.score > SIMILARITY_THRESHOLD]
```

**Problem**: Semantic similarity ≠ relevance. "How do I deploy?" is semantically similar to docs about deployment AND docs about asking questions.

### Strategy 2: Recency-Weighted

Prefer recent memories, decay older ones:

```python
def retrieve_recency_weighted(query: str, memory_store, top_k: int = 5):
    results = memory_store.similarity_search(embed(query), k=top_k * 3)
    for r in results:
        age_hours = (now() - r.timestamp).total_seconds() / 3600
        r.score *= math.exp(-0.1 * age_hours)  # Exponential decay
    results.sort(key=lambda r: r.score, reverse=True)
    return results[:top_k]
```

### Strategy 3: Importance Scoring

Score memories by importance (how impactful/critical the information is):

```python
def score_importance(memory) -> float:
    score = 0.0
    if memory.type == "error": score += 0.3       # Errors are important to remember
    if memory.type == "user_preference": score += 0.5  # User prefs are critical
    if memory.referenced_count > 5: score += 0.2  # Frequently referenced = important
    if memory.has_user_feedback: score += 0.4      # Explicit feedback is gold
    return min(score, 1.0)
```

### Strategy 4: Context-Aware Retrieval

Use the current task context to inform retrieval:

```python
def retrieve_context_aware(query: str, task_context: TaskContext, memory_store):
    # Retrieve candidates
    candidates = memory_store.similarity_search(embed(query), k=20)
    
    # Re-rank based on task context
    for c in candidates:
        relevance = 0.0
        if c.project == task_context.current_project:
            relevance += 0.3
        if any(tool in c.tools_used for tool in task_context.available_tools):
            relevance += 0.2
        if c.task_type == task_context.task_type:
            relevance += 0.3
        c.final_score = c.similarity_score * 0.4 + relevance * 0.6
    
    candidates.sort(key=lambda c: c.final_score, reverse=True)
    return candidates[:5]
```

### Strategy 5: LLM-as-Retriever

Use the LLM itself to decide what memories are relevant:

```python
def retrieve_llm_guided(query: str, memory_summaries: list[str]):
    prompt = f"""Given the current task: {query}
    
    Which of these past memories would be most helpful?
    
    {chr(10).join(f'{i}. {s}' for i, s in enumerate(memory_summaries))}
    
    Return the numbers of the top 3 most relevant memories."""
    
    selected_indices = llm.generate(prompt)
    return [memories[i] for i in selected_indices]
```

## Memory Consolidation

Raw memories accumulate noise. Consolidation refines and compresses:

### Reflection (from Generative Agents & Reflexion)

The most powerful consolidation mechanism is **reflection** — periodically synthesizing individual memories into higher-level abstractions:

```
Individual memories:
  "Klaus painted a new landscape today"
  "Klaus mentioned wanting to exhibit his work"  
  "Klaus spent 3 hours at the easel yesterday"
              ↓ (reflection triggers when importance sum exceeds threshold)
Higher-level memory:
  "Klaus is passionate about his art and is considering showing it publicly"
```

Critically, **reflections become new memories** that can themselves be reflected upon — creating a recursive hierarchy of increasing abstraction. This is how agents maintain coherent long-term behavior without drowning in low-level details.

Reflexion (Shinn et al., 2023) applies a specialized form of this to task execution: after a failed attempt, the agent generates a verbal self-reflection ("I should have checked the tests first") that becomes episodic memory for the next attempt. This achieved 91% on HumanEval vs 80% baseline — the self-reflections act as a form of learned, task-specific memory.

### Summarization
Periodically summarize episodes into higher-level learnings:

```
Raw episodes:
- 2026-03-01: Failed to deploy because forgot to run migrations
- 2026-03-03: Deployment succeeded after running migrations first  
- 2026-03-05: Almost forgot migrations again, caught it in pre-deploy check

Consolidated learning:
- "Always run database migrations before deployment. Add migration check to pre-deploy checklist."
```

### Contradiction Resolution
When memories conflict, resolve:

```
Memory A (2026-01): "Use REST API endpoint /api/v1/users"
Memory B (2026-03): "REST API v1 is deprecated, use /api/v2/users"

Resolution: Keep Memory B, mark Memory A as superseded.
```

### Forgetting
Not all memories should persist. A good forgetting policy:
- Forget: One-off errors that were immediately corrected
- Forget: Intermediate working memory from completed tasks
- Keep: User preferences and feedback
- Keep: Environment/project knowledge
- Keep: Learned procedures and patterns

## Practical Memory Architecture for a Production Agent

```
┌──────────────────────────────────────────────────┐
│  Agent Turn Execution                             │
│                                                   │
│  1. Receive user message                          │
│  2. Retrieve relevant memories:                   │
│     - Semantic search on user query               │
│     - Load project context (semantic memory)      │
│     - Check for similar past tasks (episodic)     │
│     - Load user preferences                       │
│  3. Assemble context window:                      │
│     [system prompt]                               │
│     [retrieved memories]                          │
│     [working memory / scratchpad]                 │
│     [conversation history (compressed)]           │
│     [user message]                                │
│  4. LLM inference → action                        │
│  5. Execute action, get observation               │
│  6. Update working memory                         │
│  7. Repeat from 3 until task complete             │
│  8. Post-task:                                    │
│     - Create episodic record                      │
│     - Update semantic memory (new facts learned)  │
│     - Extract procedural learning if applicable   │
│     - Consolidate if memory store is growing       │
└──────────────────────────────────────────────────┘
```

## Open Problems in Agent Memory

1. **When to retrieve vs. when to reason from scratch**: Sometimes memories are misleading stale data
2. **Privacy and memory**: Should an agent remember sensitive information across sessions?
3. **Memory hallucination**: Agents can "remember" things that never happened if memory retrieval is noisy
4. **Cross-user learning**: Can agents learn from one user's experience and apply to another? (Ethically complex)
5. **Memory capacity planning**: How much memory is too much? When does retrieval noise exceed signal?
6. **Temporal reasoning**: Understanding that a memory from 6 months ago might be outdated
7. **Memory as an attack surface**: If an agent persists user-provided information to memory and retrieves it later, adversarial content can survive across sessions ("memory poisoning")
8. **The stateless-reducer tension**: 12-Factor Agents (Factor 12) argues agents should be "stateless reducers" — given the same context, they produce the same output. This is in tension with rich memory systems. The resolution: memory is an *input* to the reducer, not internal hidden state.

---

*Next: [Multi-Agent Systems](../patterns/06-multi-agent-systems.md)*
