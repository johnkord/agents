# Long-Running & Life Augmentation Agents

> Deep research into persistent, cloud-hosted, proactive AI agents that autonomously manage tasks, delegate to sub-agents, and augment daily life.

## Table of Contents

1. [Vision & Definition](#1-vision--definition)
2. [What Has Been Tried](#2-what-has-been-tried)
3. [Architectural Patterns](#3-architectural-patterns)
4. [Proactive vs. Reactive Agents](#4-proactive-vs-reactive-agents)
5. [Memory & Personalization for Long-Running Agents](#5-memory--personalization-for-long-running-agents)
6. [User Interaction Patterns](#6-user-interaction-patterns)
7. [What Works & What Fails](#7-what-works--what-fails)
8. [Creative Use Cases for a Life Augmentation Agent](#8-creative-use-cases-for-a-life-augmentation-agent)
9. [Privacy, Security & Trust](#9-privacy-security--trust)
10. [Recommended Architecture](#10-recommended-architecture)
11. [Key Papers & References](#11-key-papers--references)
12. [User Sentiment & Real-World Validation](#12-user-sentiment--real-world-validation)
13. [Audio Lifelogging & Continuous Transcription](#13-audio-lifelogging--continuous-transcription)

---

## 1. Vision & Definition

A **life augmentation agent** is a persistent, cloud-hosted AI system that runs continuously (or is awakened by triggers), maintains a model of its user's life, and proactively delegates tasks to specialized sub-agents to improve the user's daily experience. Unlike a reactive chatbot that waits for prompts, this agent:

- **Runs indefinitely** in the background (cloud-native, always-on)
- **Proactively initiates** actions without explicit user commands
- **Delegates work** to specialized sub-agents (research, scheduling, monitoring, communication)
- **Maintains long-term memory** of user preferences, history, and context
- **Learns and adapts** from user feedback over time
- **Interacts minimally** — surfacing only what matters, when it matters

This is the "user-centric agent" vision articulated by Zhang et al. (2026): *"The future of digital services should shift from a platform-centric to a user-centric agent... prioritizing privacy, aligning with user-defined goals, and granting users control over their preferences and actions."* (arXiv:2602.15682)

---

## 2. What Has Been Tried

### 2.1 Academic Research (2023–2026)

The idea of proactive, autonomous agents has exploded in academic research, particularly from late 2024 onward:

**Foundational Work:**
- **Generative Agents** (Park et al., 2023) — Simulated 25 agents with day-long autonomy in a Smallville sandbox. Demonstrated memory retrieval, reflection, and planning over extended timeframes. Showed that believable long-running behavior is possible but requires careful memory architecture.
- **Voyager** (Wang et al., 2023) — Lifelong learning agent in Minecraft. Demonstrated skill accumulation over 100+ hours, with a persistent skill library. Key insight: agents can self-improve if they maintain a growing capability repertoire.

**Proactive Agent Research (2024–2026):**
- **Proactive Agent** (Lu et al., 2024) — First systematic approach to shifting LLMs from reactive to proactive. Created ProactiveBench (6,790 events). Found that fine-tuned models achieve 66.47% F1 in proactive assistance — *meaning even the best models fail to anticipate user needs ~1/3 of the time*.
- **Ask-before-Plan** (Zhang et al., 2024, EMNLP) — Showed that proactive clarification before action dramatically improves planning quality. The Clarification-Execution-Planning (CEP) framework with 3 specialized agents outperforms monolithic approaches.
- **ContextAgent** (Yang et al., 2025, NeurIPS) — First context-aware proactive agent using wearable sensory data. 8.5% higher accuracy in proactive predictions vs baselines. Key: multi-dimensional context extraction from video/audio on AR glasses.
- **ProPerSim** (Kim et al., 2025, ICLR 2026) — Proactive AND personalized assistant simulation. ProPerAssistant uses retrieval-augmented preference-aligned learning that continually adapts via user ratings. Key finding: combining proactivity + personalization is more than additive.
- **BAO** (Yao et al., 2026) — Behavioral Agentic Optimization via RL. Addresses the critical autonomy-satisfaction tradeoff: overly proactive agents annoy users. BAO's behavior regularization suppresses redundant interactions.
- **IntentRL** (Luo et al., 2026) — Trains proactive agents to clarify latent user intents before long-running research tasks. Addresses the "autonomy-interaction dilemma": high autonomy on ambiguous queries leads to prolonged execution with unsatisfactory outcomes.
- **ProAgentBench** (Tang et al., 2026) — 28,000+ events from 500+ hours of real user sessions. Key finding: *real-world data shows bursty interaction patterns (B=0.787) completely absent in synthetic data*. Models trained on synthetic data fail in production.

### 2.2 Industry & Open-Source Projects

**AutoGPT / BabyAGI (2023):**
The original "run an LLM in a loop forever" experiments. AutoGPT set a high-level goal ("grow a business") and looped GPT-4 with tools. Key learnings:
- **Rapid context degradation**: After 10-20 steps, the agent would lose track of its original goal, hallucinate sub-tasks, or loop endlessly between the same actions
- **No principled stopping criteria**: Agents couldn't determine when they were "done" or when to yield to humans
- **Cost explosion**: Continuous LLM invocation without intelligent routing consumed enormous API budgets
- **Compounding errors**: Each LLM call had some error rate; over many steps, errors compounded catastrophically
- As Latent.Space summarized: *"AutoGPTs have continuous modes which are fully autonomous but very likely to go wrong and therefore have to be closely monitored"*

**Lindy.ai (2024–present):**
A production personal AI assistant that manages inbox, meetings, and calendar. Focuses on narrow, well-defined tasks rather than open-ended autonomy. Their approach: tightly scoped agents for specific workflows (email triage, meeting prep, calendar optimization). Reports 400K+ users. Key insight: *narrow scope + high reliability > broad ambitions + frequent failures*.

**Replit Agent (2024–2025):**
Long-running coding agent for persistent development sessions. LangGraph-backed with checkpointing. Michele Catasta (VP AI): *"It's easy to build the prototype of a coding agent, but deceptively hard to improve its reliability."* They invested heavily in fine-grained control flow rather than pure LLM autonomy.

**Rabbit R1 / Humane AI Pin (2024):**
Hardware-based "life augmentation" devices. Both launched to enormous hype and largely failed. Key lessons:
- **Latency kills**: Users expect sub-second responses for frequent interactions. Cloud-hosted LLM inference is too slow for "always-on" use
- **Battery life**: Continuous sensing + cloud communication drains batteries within hours
- **UI/UX mismatch**: Ambient AI assistants need fundamentally different interaction patterns than chat interfaces
- **Overpromise, underdeliver**: Marketing implied general intelligence; reality was narrow, unreliable capabilities

**12-Factor Agents (HumanLayer, 2025):**
Practical engineering guide from Dex Horthy, who talked to 100+ SaaS builders. Key insights for long-running agents:
- **Factor 5: Unify execution state and business state** — Agent state must be durable and inspectable
- **Factor 6: Launch/Pause/Resume** — Agents must be pausable and resumable (critical for long-running tasks)
- **Factor 7: Contact humans with tool calls** — Human interaction is just another tool, not a special case
- **Factor 10: Small, focused agents** — Monolithic agents fail; specialized agents composed together succeed
- **Factor 11: Trigger from anywhere** — Agents should respond to cron jobs, webhooks, user messages alike
- **Factor 12: Stateless reducer** — Each agent invocation should be a pure function of its context

**LangGraph Cloud (LangChain, 2024):**
Purpose-built infrastructure for deploying long-running agents. Provides:
- Horizontally-scaling task queues with Postgres checkpointer
- Background jobs for long-running tasks (polling or webhook completion)
- Cron jobs for scheduled tasks
- Double-texting handling (managing new user input on running threads)
- Time-travel debugging (inspect, edit, resume past states)

---

## 3. Architectural Patterns

### 3.1 Event Sourcing for Agents (ESAA)

The ESAA architecture (dos Santos Filho, 2026, arXiv:2602.23193) separates **cognitive intention** from **state mutation**:

```
Agent emits structured intentions (JSON)
    ↓
Deterministic orchestrator validates
    ↓
Persists events in append-only log (activity.jsonl)
    ↓
Applies effects (file writes, API calls)
    ↓
Projects verifiable materialized view (roadmap.json)
```

Key properties:
- **Immutability**: Completed task events are immutable and hash-verified
- **Replay**: Full trajectory can be replayed from the event log
- **Forensic traceability**: Every decision has a provenance chain
- **Multi-agent**: Demonstrated with 4 concurrent agents, different LLMs, 50 tasks
- **Separation of concerns**: LLM does thinking, deterministic code does execution

This maps almost directly to Temporal.io-style durable workflows and is enormously relevant for a life augmentation agent that needs reliability over days/weeks.

### 3.2 Orchestrator-Workers with Task Queues

From the Anthropic "Building Effective Agents" guide (2024):
- Central orchestrator LLM breaks down tasks and delegates to worker LLMs
- Workers are specialized for specific domains (research, scheduling, communication)
- Results synthesized by orchestrator
- Key: this pattern is "well-suited for complex tasks where you can't predict the subtasks needed"

For a life agent, the orchestrator maintains a persistent task queue:
```
User's life context → Orchestrator → Priority queue → Worker agents → Results → User
                           ↑                                              |
                           └────────── feedback loop ─────────────────────┘
```

### 3.3 Declarative Workflow Orchestration

Daunis (2025, arXiv:2512.19769) showed that most agent workflows can be expressed through a unified DSL rather than imperative code. At PayPal scale: 60% reduction in development time, 3x deployment velocity. Complex workflows expressed in <50 lines of DSL vs 500+ lines of imperative code.

For life augmentation: define recurring agent workflows declaratively (morning briefing, email triage, expense tracking) and let the runtime handle scheduling, retry, and monitoring.

### 3.4 Internet of Agentic AI

Yang & Zhu (2026, arXiv:2602.03145) propose a framework where autonomous, heterogeneous agents across cloud and edge dynamically form coalitions for task-driven workflows. Key concepts:
- **Minimum-effort coalition selection**: Find the smallest group of agents to accomplish a task
- **Network locality**: Prefer agents close to the data/user
- **Economic implementability**: Incentive-compatible coordination
- **MCP as coordination layer**: Uses Model Context Protocol above

### 3.5 Peak-Aware Long-Horizon Orchestration

APEMO (Shi & DiFranzo, 2026, arXiv:2602.17910) introduces temporal-affective scheduling:
- Detects trajectory instability through behavioral proxies
- Targets computational resources at **peak moments** (critical decisions) and **endings** (final user-facing output)
- Under fixed compute budgets, APEMO consistently enhances trajectory-level quality
- Reframes alignment as a **temporal control problem** — critical for agents running over hours/days

---

## 4. Proactive vs. Reactive Agents

This is perhaps the most critical dimension for a life augmentation agent. A reactive agent waits for "Hey, do X." A proactive agent notices you need X before you ask.

### 4.1 The Proactivity Spectrum

From the research, three levels emerge:

| Level | Description | Example |
|-------|-------------|---------|
| **Reactive** | Responds only to explicit requests | "Schedule a meeting with Bob" |
| **Suggestion-based** | Notices patterns and suggests actions | "You usually meet Bob on Tuesdays — should I schedule?" |
| **Fully proactive** | Acts autonomously based on context | Notices Bob emailed about Q2 planning, cross-references your calendar, schedules meeting, drafts agenda |

### 4.2 The Autonomy-Satisfaction Tradeoff

BAO (2026) identifies a critical Pareto frontier: **more proactive ≠ better**. Overly proactive agents that constantly interrupt with suggestions are *worse* than reactive ones. The sweet spot requires:
- **Timing prediction**: When to intervene (ProAgentBench found bursty patterns matter)
- **Relevance filtering**: Not every observed intent deserves action
- **Cost-of-interruption modeling**: ProMemAssist (2025, UIST) models the user's working memory to balance assistance value vs. interruption cost

### 4.3 Knowledge Gap Navigation

PROPER (Kaur et al., 2026) introduces a two-agent architecture:
1. **Dimension Generating Agent (DGA)**: Identifies implicit dimensions the user hasn't considered
2. **Response Generating Agent (RGA)**: Balances explicit needs with discovered implicit needs

The insight: proactive agents should address needs the user *doesn't know they have*, not just automate what they've already expressed. This achieved 84% quality gains in single-turn evaluations.

### 4.4 The Advisor/Coach/Delegate Paradigm

Zhu et al. (2026, arXiv:2602.12089) ran a 243-person behavioral experiment comparing:
- **Advisor**: Proactive recommendations
- **Coach**: Reactive feedback on user's actions  
- **Delegate**: Autonomous execution

Key finding: **users preferred the Advisor but achieved the highest gains with the Delegate**. This "preference-performance misalignment" is critical: users *want* to feel in control but *benefit* from delegation. Moreover, delegation created positive externalities — even non-adopters in delegate groups received higher-quality offers.

Implication for life agents: default to delegation for high-confidence tasks, with transparent reporting. Allow users to override, but don't make them drive every decision.

---

## 5. Memory & Personalization for Long-Running Agents

### 5.1 O-Mem: Active User Profiling

O-Mem (Wang et al., 2025, arXiv:2511.13593) is a memory framework based on **active user profiling**:
- Dynamically extracts and updates user characteristics from interactions
- Hierarchical retrieval of persona attributes and topic-related context
- Achieves SOTA on LoCoMo (51.67%) and PERSONAMEM (62.99%) benchmarks
- Key: avoids semantic-grouping-before-retrieval pitfall that loses critical but semantically distant user info

### 5.2 AMemGym: Interactive Memory Benchmarking

AMemGym (Cheng et al., 2026, ICLR) reveals that existing memory systems (RAG, long-context LLMs, agentic memory) all have significant gaps in long-horizon conversations. Key insight: **on-policy evaluation** (interactive) reveals failures invisible in off-policy (static) evaluation.

### 5.3 Working Memory Modeling for Proactive Timing

ProMemAssist (Pu et al., 2025, UIST) models the user's **cognitive working memory** in real-time:
- Represents perceived information as memory items and episodes
- Uses encoding mechanisms (displacement, interference) from cognitive psychology
- Timing predictor balances assistance value vs. interruption cost
- In user studies: more selective assistance, higher engagement vs. LLM-only baseline

### 5.4 The Personalized Agent Survey

The comprehensive survey by Xu et al. (2026, arXiv:2602.22680) identifies four interdependent personalization components:
1. **Profile modeling**: Explicit (stated preferences) + implicit (behavioral signals)
2. **Memory**: Short-term working memory + long-term episodic/semantic memory
3. **Planning**: User-adapted goal decomposition and strategy selection
4. **Action execution**: Personalized tool selection and output formatting

Key trade-off: **deeper personalization requires more user data**, creating tension with privacy.

---

## 6. User Interaction Patterns

### 6.1 Notification Fatigue

The biggest failure mode for proactive agents. From the research:
- ProAgentBench shows real user sessions have bursty patterns (B=0.787) — users interact in concentrated bursts, then go silent
- During silent periods, proactive interruptions are maximally disruptive
- BAO's behavior regularization specifically penalizes "redundant interactions"
- Solution: **preference-aligned timing** — learn when the user is receptive

### 6.2 Trust Calibration

From "Choose Your Agent" (2026):
- Users systematically under-trust delegation despite its superior outcomes
- Trust builds through **transparency** (showing reasoning) and **track record** (successful completions)
- "Adoption-compatible interaction rules are a prerequisite to improving human welfare"
- Start with low-stakes delegation (weather reminders, meeting summaries), build to high-stakes (financial decisions, communication on behalf)

### 6.3 The Right Interface

From Egocentric Co-Pilot (2026, WWW):
- Smart glasses + multimodal input (speech + gaze) enables "always-on assistive layer over daily life"
- But introduces latency requirements: cloud offloading adds ~200ms+ vs. on-device inference
- Hierarchical Context Compression supports long-horizon QA over continuous first-person video

From "The Next Paradigm" (2026):
- Desktop/mobile app with proactive notifications + on-demand deep interaction
- Device-cloud pipeline: lightweight on-device model for urgency triage, cloud model for complex reasoning
- User should control preference dials (proactivity level, domains to manage, notification frequency)

### 6.4 Human-in-the-Loop as a Tool

From 12-Factor Agents: "Contact humans with tool calls" — the agent should model human interaction exactly like any other tool:
```
contact_human(
  channel="push_notification",
  message="I found a cheaper flight for your trip to NYC next week. Should I rebook?",
  urgency="medium",
  timeout="4h",
  fallback="keep_current_booking"
)
```

If the human doesn't respond within the timeout, the agent takes the fallback action. This makes human-in-the-loop a first-class, timeout-aware capability rather than a blocking dependency.

---

## 7. What Works & What Fails

### 7.1 What Works

| Category | Examples | Why It Works |
|----------|----------|-------------|
| **Narrow, well-defined tasks** | Email triage, meeting prep, expense categorization | Clear success criteria, bounded scope, verifiable output |
| **Information synthesis** | Morning briefings, research summaries, news digests | LLMs excel at summarization; failures are low-cost |
| **Schedule optimization** | Calendar management, reminder timing, travel planning | Structured data, clear constraints, measurable outcomes |
| **Monitoring & alerting** | Price tracking, deadline monitoring, health metric trends | Event-driven triggers, simple decision logic |
| **Routine automation** | Form filling, template generation, data entry | Repetitive tasks with clear patterns |

### 7.2 What Fails

| Category | Why It Fails | Reference |
|----------|-----------|-----------|
| **Open-ended goal pursuit** | Compounding errors over long horizons; context degradation | AutoGPT postmortems |
| **High-stakes autonomy** | Users don't trust AI for financial/legal/medical decisions | Choose Your Agent (2026) |
| **Social communication** | Nuance, tone, relationship context are hard to model | EchoGuard (2026) |
| **Real-time continuous sensing** | Battery, latency, cost constraints for always-on processing | Rabbit R1, Humane AI Pin failures |
| **Unbounded task decomposition** | LLMs hallucinate sub-tasks, struggle with novel problem structures | ProActiveBench real-world data |
| **Cross-modal understanding** | Combining calendar + email + location + health data coherently | ContextAgent (2025) improvements still limited |

### 7.3 Critical Failure Patterns

1. **The Infinite Loop**: Agent creates task → fails → creates retry task → fails → ... Without proper stopping conditions, agents burn through budgets. Solution: circuit breakers, max iteration limits, exponential backoff.

2. **Context Window Exhaustion**: Long-running agents accumulate history. Eventually the context window fills and the agent "forgets" its goals. Solution: hierarchical memory with compression (Active Context Compression, 2026), event sourcing with materialized views (ESAA).

3. **Preference Drift**: User preferences change over time. An agent trained on January behavior may be wrong by March. Solution: continuous adaptation with recency weighting (O-Mem, ProPerSim).

4. **The 80% Problem**: From 12-Factor Agents — most agent frameworks get you to 80% quality, but the last 20% requires abandoning the framework and building custom. Solution: own your prompts, own your control flow, build on primitives not frameworks.

5. **Notification Fatigue Spiral**: Proactive agent sends too many suggestions → user ignores them → agent interprets silence as neutral → continues sending → user disables agent entirely. Solution: explicit feedback channels, decreasing proactivity in response to non-engagement.

---

## 8. Creative Use Cases for a Life Augmentation Agent

### 8.1 The "Morning Protocol" Agent

Every morning, the agent:
1. Scans today's calendar, identifies preparation needs
2. Checks email for anything requiring pre-meeting context
3. Reviews weather + commute for schedule impacts
4. Surfaces any overnight deadline changes or urgent messages
5. Generates a personalized briefing pushed to phone/watch

Delegate agents: CalendarAgent, EmailTriageAgent, WeatherAgent, CommutePlanner

### 8.2 The "Background Research" Agent

User mentions interest in a topic (via conversation, browsing, or explicit request):
1. Agent queues a deep research task
2. ResearchAgent searches academic papers, blogs, news
3. Runs over hours/days, building a knowledge graph
4. Synthesizes findings into a briefing document
5. Proactively surfaces when user next has free time

This is exactly the IntentRL (2026) pattern — clarify intent first, then execute autonomously.

### 8.3 The "Financial Health" Agent

Continuous monitoring:
- Tracks spending against budget categories
- Monitors bills for price increases or better alternatives
- Watches subscriptions for unused services
- Alerts on unusual charges
- Monthly financial health summary

Requires: bank API integration, category classification, anomaly detection

### 8.4 The "Social Maintenance" Agent

The hardest and most nuanced use case:
- Tracks last contact date with important people
- Suggests reaching out when it's been too long
- Notices birthdays, life events from social media
- Drafts (but never sends without approval) personal messages
- Manages RSVP tracking

Key constraint: **never send on behalf without explicit approval**. This is the "advisor" pattern from Choose Your Agent — suggest, don't act.

### 8.5 The "Health & Wellness" Agent

- Monitors sleep patterns (via wearable data)
- Tracks medication schedules
- Suggests exercise based on calendar gaps
- Monitors mood patterns from interaction data (very carefully, with explicit consent)
- Connects health data to productivity patterns

ProMemAssist (2025) provides the cognitive model: intervene when the user's working memory is overloaded, not when they're in flow state.

### 8.6 The "Travel Optimization" Agent

For frequent travelers:
- Monitors prices for upcoming trips
- Manages loyalty programs and point optimization
- Handles rebooking during disruptions
- Creates location-aware travel guides
- Manages expense reports post-trip

### 8.7 The "Knowledge Worker Copilot" Agent

Always-on background support:
- Monitors project deadlines across tools (Jira, GitHub, email)
- Identifies tasks that are blocked or at risk
- Pre-fetches context before meetings (related documents, recent changes)
- Suggests task prioritization based on deadlines, impact, and dependencies
- Creates end-of-week summaries

### 8.8 The "Home Automation Intelligence Layer"

Layer on top of smart home:
- Learns household patterns (wake times, preferences, routines)
- Proactively adjusts based on calendar (early meeting → earlier alarm)
- Coordinates grocery lists with meal planning
- Manages maintenance reminders (HVAC filters, car service)
- Integrates weather forecasts with heating/cooling optimization

---

## 9. Privacy, Security & Trust

### 9.1 The Privacy Paradox

AgentScope (2026, arXiv:2603.04902) evaluates contextual privacy across agentic workflows accessing calendars, email, and personal files. Life augmentation agents need deep access to personal data, but:
- Users want personalization (requires data) but fear surveillance
- Cross-domain data aggregation (calendar + health + finance) creates sensitive profiles
- Current LLM providers process data on external servers

### 9.2 User-Centric Architecture

From "The Next Paradigm" (2026):
- **On-device processing** for sensitive data (triage, classification)
- **Cloud processing** only for complex reasoning, with minimal data transmission
- **User-controlled data policies**: what data the agent can access, retain, and share
- **Audit logs**: every data access and decision is traceable (cf. ESAA event sourcing)

### 9.3 Trust Hierarchy

For a life agent, different actions require different trust levels:

| Trust Level | Actions | Control |
|------------|---------|---------|
| **Full autonomy** | Reading calendar, checking weather, monitoring prices | No approval needed |
| **Notify & act** | Sending standard replies, rescheduling non-critical meetings | Notification + undo window |
| **Ask & act** | Sending personal messages, making purchases, changing plans | Explicit approval required |
| **Never autonomous** | Financial transactions above threshold, medical decisions, legal actions | Human must initiate |

---

## 10. Recommended Architecture

Based on all the research, here's a synthesized architecture for a life augmentation agent:

### 10.1 Core Components

```
┌─────────────────────────────────────────────────────┐
│                  Life Agent Core                     │
│                                                      │
│  ┌───────────┐   ┌────────────┐   ┌──────────────┐ │
│  │ Event Bus │──→│Orchestrator│──→│ Task Queue    │ │
│  │(triggers) │   │  (planner) │   │(priority heap)│ │
│  └───────────┘   └────────────┘   └──────────────┘ │
│        ↑               │                  │          │
│        │               ↓                  ↓          │
│  ┌───────────┐   ┌────────────┐   ┌──────────────┐ │
│  │ User      │   │ Memory     │   │ Worker Pool  │ │
│  │ Interface │←──│ (O-Mem +   │   │ (sub-agents) │ │
│  │ (multi-ch)│   │  profiles) │   │              │ │
│  └───────────┘   └────────────┘   └──────────────┘ │
│                        │                  │          │
│                        ↓                  ↓          │
│               ┌────────────────────────────┐        │
│               │  Event Store (append-only) │        │
│               │  + State Snapshots         │        │
│               └────────────────────────────┘        │
└─────────────────────────────────────────────────────┘
```

### 10.2 Key Design Decisions

1. **Event-sourced state** (ESAA pattern): All agent actions recorded in append-only log. State reconstructable from events. Enables debugging, audit, and replay.

2. **Stateless orchestrator** (12-Factor #12): Each orchestrator invocation is a pure function of current state + new event. No hidden state. Enables horizontal scaling and crash recovery.

3. **Priority-based task queue**: Tasks have urgency, importance, and deadline. Orchestrator continuously re-prioritizes. Inspired by APEMO peak-aware scheduling.

4. **Multi-channel input/output**: Triggers from cron (morning briefing), webhooks (email arrival), user messages (chat), sensors (location change). Output via push notifications, email, chat, or ambient display.

5. **Specialized worker agents**: CalendarAgent, ResearchAgent, EmailAgent, FinanceAgent, HealthAgent. Each has narrow scope, specific tools, and measurable outcomes.

6. **Adaptive proactivity**: Use ProPerSim-style user modeling to learn intervention timing. Start conservative (suggestions only), build trust, increase autonomy over time.

7. **O-Mem-style memory**: Active user profiling with hierarchical retrieval. Short-term working memory (today's context), medium-term episodic memory (recent interactions), long-term semantic memory (stable preferences).

8. **Human-as-a-tool**: Human interaction modeled as a tool call with timeout and fallback (12-Factor #7). Never block on human response for time-sensitive tasks.

### 10.3 Technology Stack Recommendations

- **Orchestration**: Durable workflow engine (Temporal.io, Azure Durable Functions, or custom event-sourced loop)
- **Agent Framework**: Microsoft Agent Framework (for .NET) or LangGraph (for Python) — both support checkpointing and persistence
- **Memory Store**: Vector DB (Qdrant/Pinecone) for semantic retrieval + PostgreSQL for structured user profiles
- **Event Store**: Append-only log (PostgreSQL with immutable rows, or EventStoreDB)
- **Task Queue**: Redis-backed priority queue or cloud-native queue (Azure Service Bus, SQS)
- **Notifications**: Multi-channel push (FCM, APNs, email, Slack/Teams webhook)
- **LLM**: Tiered — fast/cheap model for triage/classification, powerful model for reasoning/planning
- **Monitoring**: OpenTelemetry for distributed tracing across agent invocations

### 10.4 Critical Engineering Principles

1. **Circuit breakers**: Max iterations per task, budget limits per day, error thresholds
2. **Graceful degradation**: If a worker agent fails, others continue operating
3. **Idempotent operations**: Every action can be safely retried
4. **Transparent reasoning**: Every decision should have a human-readable explanation
5. **Progressive trust**: Start with low-autonomy defaults, increase based on track record
6. **Cost accounting**: Track per-task LLM costs, optimize routing to smaller models where possible
7. **Offline-first**: Agent should handle network interruptions gracefully with local queuing

---

## 11. Key Papers & References

### 11.1 Papers Downloaded & Converted (Batch 6)

| Paper | arXiv | Key Contribution |
|-------|-------|-----------------|
| Proactive Agent | 2410.12361 | First systematic reactive→proactive shift; ProactiveBench |
| Ask-before-Plan | 2406.12639 | Proactive clarification + CEP framework (EMNLP 2024) |
| ContextAgent | 2505.14668 | Wearable sensory context for proactive agents (NeurIPS 2025) |
| ProAgent Sensory | 2512.06721 | End-to-end proactive system on AR glasses |
| ProPerSim | 2509.21730 | Proactive + personalized assistants (ICLR 2026) |
| BAO | 2602.11351 | Behavioral agentic optimization; autonomy-satisfaction Pareto |
| IntentRL | 2602.03468 | RL for proactive intent clarification in deep research |
| ProAgentBench | 2602.04482 | Real-world proactive assistance benchmark (28K+ events) |
| PROPER | 2601.09926 | Knowledge gap navigation for proactivity |
| O-Mem | 2511.13593 | Omni memory for personalized long-horizon agents |
| Personalized LLM Agents Survey | 2602.22680 | Comprehensive survey: profile, memory, planning, action |
| AMemGym | 2603.01966 | Interactive memory benchmarking for long-horizon (ICLR 2026) |
| ProMemAssist | 2507.21378 | Working memory modeling for proactive timing (UIST'25) |
| Egocentric Co-Pilot | 2603.01104 | Always-on smart glasses agent (WWW 2026) |
| Next Paradigm: User-Centric | 2602.15682 | User-centric agent vs platform-centric service |
| Choose Your Agent | 2602.12089 | Advisor vs Coach vs Delegate tradeoffs (N=243) |
| ESAA | 2602.23193 | Event sourcing architecture for autonomous agents |
| Alignment in Time | 2602.17910 | Peak-aware orchestration for long-horizon systems |
| Internet of Agentic AI | 2602.03145 | Distributed agent coalition formation |
| Declarative Agent Workflows | 2512.19769 | DSL for agent workflow orchestration (PayPal scale) |

### 11.2 Blog Posts & Practical Resources

| Resource | Key Takeaway |
|----------|-------------|
| [Anthropic: Building Effective Agents](https://www.anthropic.com/engineering/building-effective-agents) | Start simple, use orchestrator-workers for complex tasks, agents for open-ended problems |
| [12-Factor Agents](https://github.com/humanlayer/12-factor-agents) | Stateless reducer pattern, pause/resume, human-as-tool, small focused agents |
| [LangGraph Cloud](https://blog.langchain.dev/langgraph-cloud/) | Task queues, cron, background jobs, time-travel debugging for long-running agents |
| [Latent.Space: Anatomy of Autonomy](https://www.latent.space/p/agents) | AutoGPT/BabyAGI dissection, 5 capability levels, self-driving car analogy |
| Lindy.ai | Production personal assistant — narrow scope + high reliability wins |

### 11.3 Papers Already in Collection (Relevant)

- `generative-agents-stanford-2023` — Long-running agent simulation patterns
- `voyager-lifelong-learning-2023` — Lifelong skill accumulation
- `memgpt-llm-operating-system-2023` — OS-like memory management for agents
- `caveagent-stateful-runtime-2026` — Stateful agent runtime with persistent containers
- `ariadnemem-lifelong-memory-2026` — Lifelong episodic-semantic memory
- `anatomy-agentic-memory-2026` — Comprehensive memory architecture taxonomy
- `active-context-compression-2026` — Autonomous memory management
- `agentic-ai-frameworks-protocols-2025` — Architecture & protocol landscape

---

## 12. User Sentiment & Real-World Validation

> Added March 2026. Synthesized from 11 papers with real user studies plus web sources (Reddit, product reviews). Full analysis: `blueprints/life-agent/docs/user-sentiment-research.md`.

### 12.1 The Preference-Performance Paradox

The single most important finding across all research: **users systematically prefer the interaction mode that gives them worse outcomes.**

Choose Your Agent (2026, N=243, 6,561 trading decisions):
- 44% preferred Advisor ("show me options, I'll decide")
- 19.3% preferred Delegate ("just do it for me")
- 21.4% preferred **no AI at all**
- Yet Delegate produced statistically better economic outcomes (β=0.084, p=.034)
- Users who preferred AI reported 20% higher mental effort than autonomy-seekers (p<.01) — they willingly accepted cognitive load for perceived control

This means life agent designers face a fundamental tension: the optimal interaction pattern (delegation) is the one users resist most. The solution is progressive trust — start with advisory, earn the right to delegate through demonstrated reliability.

### 12.2 The "Less Is More" Principle (Empirically Proven)

ProMemAssist (UIST 2025, N=12): delivered **60% fewer messages** while achieving **2.6× higher positive engagement** and lower frustration (NASA-TLX: 2.32 vs 3.14, p<.05). The mechanism: modeling the user's working memory load in real-time and deferring messages when cognitive capacity is low.

ProPerSim (ICLR 2026, 32 personas): the system started at 24 recommendations/hour and **naturally converged to ~6/hour** through preference learning. This emergent "learning to be quiet" behavior improved satisfaction from 2.2/4 to 3.3/4 over 14 days.

BAO (CMU/Salesforce/MIT): without behavior regularization, agent User Involvement Rate shoots from 0.21 to **0.91** — agents pester users 91% of the time. With regularization (information-seeking + over-thinking penalties), UR drops to 0.2148.

**Design principle**: Max 6 proactive notifications per hour. Decrease on non-engagement. The cost of a false positive (unnecessary interruption) exceeds the cost of a false negative (missed opportunity to assist).

### 12.3 Trust Fragility

Trust is asymmetric — one failure outweighs many successes:
- ProMemAssist P5 indicated a single unhelpful suggestion could **break trust with the entire system**
- Trust was influenced more by quality of assistance than timing of delivery
- Algorithm aversion — tendency to abandon AI after observing even small errors — significantly reduces adoption
- "Poorly calibrated proactivity can undermine user trust, agency, and interaction quality" (PROPER, 2026)

**Design principle**: Optimize all proactive features for precision over recall. Don't ship a feature that fails >20% of the time.

### 12.4 Real Product Outcomes

| Product | Result | Lesson |
|---------|--------|--------|
| **Lindy.ai** | 400K+ users | Narrow scope + high reliability beats broad ambitions |
| **Rabbit R1** | Failed basic tasks (wrong location for weather, CAPTCHA loops for restaurant booking) | Hardware + breadth of capability doesn't compensate for unreliable execution |
| **AutoGPT/BabyAGI** | Rapid context degradation after 10-20 steps | Unbounded autonomy without grounding fails catastrophically |
| **Replit Agent** | "Easy to build the prototype, deceptively hard to improve reliability" | The demo-to-production gap is enormous |
| **Smart glasses** (various) | RayNeo X2 Pro: ~20 min battery in continuous mode | Always-on hardware is bottlenecked by power, not by AI capability |

### 12.5 What Real Users Want (Direct Evidence)

Reddit (r/artificial, 2025) — a neurodivergent parent posted:
> *"I struggle with staying on top of daily tasks. I'd love to find an AI assistant that can: Talk to me, not just respond when I ask. Give me reminders and nudges, even when I'm distracted. Help manage tasks, routines, and my health needs (I'm autistic/ADHD). Adapt to my life as a parent."*

No commenter had a real solution. Other Reddit threads (r/singularity) reported teams being cut from 50→30 people using AI agents, editorial staff of 17 laid off, software engineers shifting to "supervising AI" — but all in workplace automation, not personal life augmentation.

**The gap**: workplace AI agent adoption is accelerating; personal life agent adoption has no viable product yet.

### 12.6 Surprising Empirical Findings

1. **Delegation benefits non-users**: non-users in Delegate groups had 21.6% higher surplus than baseline groups — AI improved outcomes for people who didn't even use it (market spillover)
2. **Introverts benefit most** from personalized AI (larger improvement than extraverts in ProPerSim)
3. **Chain-of-thought hurts proactive timing**: CoT prompting degrades performance on proactive prediction — recall dropped from 94.4% to 17.1% in one model (ProAgentBench)
4. **Humans are bursty, LLMs aren't**: real interaction burstiness B=0.787 vs LLM-simulated B=0.166 (ProAgentBench) — any LLM-generated training data misses this entirely
5. **Knowing when > knowing what**: timing prediction 64.4% accurate, content prediction maxes at 30.5% semantic similarity — solve timing first, content second
6. **Explicit feedback required**: ProPerSim showed implicit signals alone offer "limited benefit" — the agent must ask for ratings, not just observe behavior

---

## 13. Audio Lifelogging & Continuous Transcription

> Full research document: `knowledge-base/audio-lifelogging-research.md` (14 sections, 17 academic papers, extensive product/community analysis)

Audio lifelogging — always-on recording with live transcription, speaker attribution, and knowledge extraction — is the richest possible input for a life augmentation agent. It captures commitments, decisions, context, and social interactions at the moment they happen, with zero user effort.

### 13.1 State of the Art (2024–2025)

The field has matured from academic concept to consumer product:

| Product/Tool | Form Factor | Status | Key Capability |
|---|---|---|---|
| **say** (u/8ta4) | macOS app + Electron | Open source, 2+ years daily use | Deepgram streaming → LLM structuring → queryable archive |
| **Omi** | BLE pendant (nRF/ESP32, ~$24) | Open source (MIT), 7.8k GitHub stars | Hardware + Flutter app + cloud pipeline |
| **Limitless** | Pendant | Acquired by Meta (2025) | Meeting transcription + speaker ID |
| **Plaud.ai** | Credit-card form factor | Shipping product | Business meeting transcription |
| **Bee** (Shopify) | Mobile app | Shipping product | Personal AI diary from conversations |

**Key technical components**: Deepgram nova-3 streaming ASR (~$0.0043/min), ECAPA-TDNN speaker embeddings (0.8% EER), Pyannote diarization (open source), Silero VAD (on-device filtering eliminates ~60% silence).

### 13.2 Critical Constraints

1. **iOS is not viable for always-on recording**: No persistent background audio entitlement, orange indicator dot, unpredictable app killing. Apple Watch battery cannot sustain 24/7 mic. A dedicated BLE pendant is the only reliable architecture.
2. **Noisy environments**: Transcription accuracy degrades significantly in crowds, showers, wind. No viable waterproof mic solution exists (IP67 housings kill mic sensitivity).
3. **Speaker attribution in the wild**: ECAPA-TDNN's 0.8% EER is measured in controlled conditions. Ambient audio with overlapping speakers, distance, and background noise reduces accuracy. Enrollment UX (10-second samples per contact) adds onboarding friction.
4. **Privacy/legal**: Recording laws vary by jurisdiction (one-party vs two-party consent). GDPR right to erasure conflicts with append-only architectures. Illinois BIPA applies to voiceprint biometrics. Privacy-by-design (transcribe → discard audio) is the minimum viable approach.
5. **Cost at scale**: ~$1/day for Deepgram with heavy use. VAD pre-filtering is essential. Deepgram offers $200 free credit (~4 months runway).

### 13.3 Implications for Life Agents

Audio lifelogging transforms a life agent from reactive (user must type/speak commands) to truly ambient (agent passively absorbs context from all conversations):

- **Episodic memory becomes conversational memory**: The agent doesn't just remember tasks — it remembers what was *said*, by whom, in what context. "What did Alex say about the deadline?" becomes answerable.
- **Commitment capture**: Spoken "I need to..." or "Let's plan to..." automatically become tracked tasks (see scenario S60).
- **Social context**: The agent builds a knowledge graph of what different people care about, what they told you, and when. This enables social scenarios (gift suggestions, relationship maintenance) that were previously impossible without manual data entry.
- **Cognitive offloading**: The most consistent finding from daily users is that recording *reduces* cognitive load — people speak more freely when they know everything is captured and searchable.

### 13.4 Open Research Problems

1. **Waterproof/harsh-environment recording** — no solution exists
2. **Real-time commitment/intent detection** from ambient speech (high false-positive rate)
3. **Long-term conversational memory retrieval** at scale (15M+ words/year)
4. **Multi-language speaker diarization** (enrollment per language needed?)
5. **Emotional tone tracking** over time (Pierce & Mann, 2021) — feasible but not yet integrated into any lifelogging product
6. **Consent UX** — how to gracefully handle two-party consent jurisdictions in always-on recording

### 13.5 Key Academic References

- Vemuri et al. (2006) — iRemember: proactive information retrieval from personal audio
- Harvey et al. (2016) — Audio lifelogging for event summarization
- Hodges et al. (2006) — SenseCam visual lifelogs for memory augmentation
- Lee & Dey (2008) — Lifelogging memory appliance for automatic recording
- Pierce & Mann (2021) — Affective lifelogging with multimodal sensing
- Desplanques et al. (2020) — ECAPA-TDNN speaker verification

*See `knowledge-base/audio-lifelogging-research.md` §4 for full paper list (17 papers).*

---

*Document created: March 2026*
*Papers downloaded: 20 new papers (Batch 6)*
*Total papers in collection: 126*
*User sentiment research added: March 2026 (11 papers + web sources)*
*Audio lifelogging research added: March 2026 (17 papers + product/community analysis)*
