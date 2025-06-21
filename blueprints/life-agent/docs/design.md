# Life Agent — Design Document

> A persistent, cloud-hosted orchestrator that proactively manages tasks, delegates to specialized sub-agents, and augments daily life.

**Status**: Draft  
**Author**: Generated from research synthesis (20 papers, 5 industry references)  
**Date**: 2026-03-08  
**Depends on**: Research Agent blueprint (existing), Microsoft Agent Framework 1.0.0+

### Key Decisions

| Decision | Choice | Notes |
|----------|--------|-------|
| **Hosting** | AKS (`jk-aks-2`, westus2, Standard_D2s_v5) | Existing cluster; follows `discord-sky` deployment pattern |
| **Persistence** | SQLite (WAL mode) + Azure Disk PV | Embedded, zero-ops. Same PV pattern as existing workloads. |
| **Channels (v1)** | CLI + Discord | Discord bot via Discord.Net Gateway; follows existing `discord-sky-bot` pattern |
| **Phase 1-2 Workers** | Research, Reminders, Monitoring, Morning Briefing | Email triage + finance deferred to Phase 3+ |
| **Secrets** | Kubernetes Secrets | Same pattern as `discord-sky-secrets`; env vars from Secret + ConfigMap |
| **Calendar/Email** | Google (Calendar API + Gmail API) | OAuth2 with refresh tokens stored in K8s Secret |
| **Container Registry** | `ribacr123.azurecr.io` | Existing ACR; push as `ribacr123.azurecr.io/life-agent:<sha>` |

---

## Table of Contents

1. [Problem Statement](#1-problem-statement)
2. [Goals & Non-Goals](#2-goals--non-goals)
3. [System Overview](#3-system-overview)
4. [Core Architecture](#4-core-architecture)
5. [Domain Model](#5-domain-model)
6. [Event Sourcing & Persistence](#6-event-sourcing--persistence)
7. [Orchestrator Design](#7-orchestrator-design)
8. [Worker Agents](#8-worker-agents)
9. [Memory & Personalization](#9-memory--personalization)
10. [Proactivity Engine](#10-proactivity-engine)
    - [10.5 Wellness & Human Flourishing Model](#105-wellness--human-flourishing-model)
11. [User Interaction Layer](#11-user-interaction-layer)
12. [Trust & Autonomy Model](#12-trust--autonomy-model)
13. [Scheduling & Triggers](#13-scheduling--triggers)
14. [Safety & Reliability](#14-safety--reliability)
15. [Technology Choices](#15-technology-choices)
16. [Project Structure](#16-project-structure)
17. [Implementation Dependencies](#17-implementation-dependencies)
18. [Phased Rollout](#18-phased-rollout)
19. [Open Questions](#19-open-questions)

---

## 1. Problem Statement

Today's AI assistants are reactive — they wait for a prompt, execute a single task, and terminate. Real personal productivity requires a **persistent agent** that:

- Runs continuously in the background, monitoring events and deadlines
- Proactively surfaces relevant information and takes action without being asked
- Delegates complex tasks (research, scheduling, monitoring) to specialized sub-agents
- Accumulates knowledge about the user over time and adapts
- Manages its own task queue with priorities, deadlines, and retry logic

The Research Agent blueprint demonstrated that a multi-agent pipeline (Planner → Researcher ↔ Analyst → Synthesizer → Verifier) can produce high-quality output for a single query. The Life Agent extends this pattern from **single-shot execution** to **continuous orchestration** — a long-running process that manages multiple concurrent tasks, each potentially delegated to a different worker agent (one of which could be the Research Agent itself).

### What the research tells us

- **AutoGPT/BabyAGI (2023)** proved the concept of "LLM in a loop" and also proved it fails without: circuit breakers, budget controls, principled stopping criteria, and memory management (Latent.Space analysis)
- **ESAA (2026)** provides an event-sourcing architecture that separates cognitive intention from state mutation — the right persistence primitive for long-running agents
- **BAO (2026)** shows that proactivity must be carefully calibrated; more intervention ≠ better outcomes. There is a Pareto frontier between autonomy and user satisfaction. Without behavior regularization, agents demand user attention **91% of the time** (UR 0.9064 vs 0.2148 with regularization)
- **Choose Your Agent (2026, N=243)** found users prefer "advisor" mode (44%) but achieve highest gains with "delegate" mode (19.3%) — a critical preference-performance misalignment (β=0.084, p=.034). 21.4% preferred no AI at all.
- **ProMemAssist (UIST 2025, N=12)** proved "less is more": delivering 60% fewer messages achieved **2.6× higher engagement** and statistically lower frustration (NASA-TLX: 2.32 vs 3.14, p<.05)
- **ProPerSim (ICLR 2026)** showed the system "taught itself to shut up" — recommendation frequency naturally dropped from 24/hour to ~6/hour through preference learning. User satisfaction rose from 2.2/4 to 3.3/4 over 14 days.
- **12-Factor Agents** establishes engineering principles: stateless reducer, pause/resume, human-as-tool, small focused agents, trigger from anywhere
- **Declarative Agent Workflows (2025)** shows that complex agent orchestration can be expressed as a DSL, achieving 60% dev time reduction at PayPal scale

### Who is asking for this

This isn't just an academic exercise — real people are looking for exactly this system. From Reddit (r/artificial, 2025), a neurodivergent parent posted:

> *"I struggle with staying on top of daily tasks. I'd love to find an AI assistant that can: Talk to me, not just respond when I ask. Give me reminders and nudges, even when I'm distracted. Help manage tasks, routines, and my health needs (I'm autistic/ADHD). Adapt to my life as a parent. Be affordable and respect my privacy."*

No commenter could point to an existing solution. The gap between what people need (proactive, adaptive, persistent) and what exists (reactive, stateless, single-shot) is the core motivation for this project.

---

## 2. Goals & Non-Goals

### Goals

| # | Goal | Success Metric |
|---|------|----------------|
| G1 | Persistent background execution | Agent survives restarts; resumes from durable state |
| G2 | Task delegation to specialized workers | ≥3 worker agent types (research, reminders, monitoring) |
| G3 | Event-sourced state | Full audit trail; any state reconstructable from event log |
| G4 | Proactive action | Agent initiates ≥1 useful action/day without explicit user request |
| G5 | User feedback loop | User can approve, reject, or modify agent suggestions; agent adapts |
| G6 | Multi-channel I/O | Triggers from cron, webhooks, user messages; output via push/email/chat |
| G7 | Cost-controlled | Per-day LLM budget cap; tiered model routing (cheap triage, expensive reasoning) |

### Non-Goals (for now)

| # | Non-Goal | Why Not |
|---|----------|---------|
| N1 | Real-time video sensing (camera) | Hardware dependency; battery constraints (Rabbit R1 postmortem, RayNeo X2 ~20 min). **Audio lifelogging is now a stretch goal** — see §8.4 and `knowledge-base/audio-lifelogging-research.md` |
| N2 | Financial transaction execution | Trust threshold too high for v1; advisor mode only for finance |
| N3 | Social media posting on behalf of user | Reputation risk; suggestion-only |
| N4 | Multi-user / shared household | Adds auth, privacy, conflict resolution; defer to v2 |
| N5 | Custom plugin marketplace | Focus on curated, high-quality worker agents first |

---

## 3. System Overview

```
                    ┌──────────────────────────────────────┐
                    │           TRIGGERS                    │
                    │  ┌──────┐ ┌───────┐ ┌────────────┐  │
                    │  │ Cron │ │Webhook│ │User Message│  │
                    │  └──┬───┘ └──┬────┘ └─────┬──────┘  │
                    └─────┼────────┼────────────┼──────────┘
                          │        │            │
                          ▼        ▼            ▼
              ┌───────────────────────────────────────────────┐
              │              EVENT BUS                         │
              │  Normalizes all inputs into typed events       │
              └───────────────────┬───────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────────┐
│                        ORCHESTRATOR                                  │
│                                                                      │
│  ┌────────────────┐  ┌─────────────────┐  ┌──────────────────────┐ │
│  │ Event Handler  │─▶│ Task Planner    │─▶│ Priority Queue       │ │
│  │                │  │ (LLM-assisted)  │  │ (urgency × deadline) │ │
│  └────────────────┘  └─────────────────┘  └──────────┬───────────┘ │
│                                                       │              │
│  ┌─────────────────────────────────────┐             │              │
│  │ User Model (O-Mem)                  │◀────────────┤              │
│  │ • Preferences, schedules, patterns  │             │              │
│  │ • Feedback history                  │             │              │
│  │ • Trust levels per domain           │             │              │
│  └─────────────────────────────────────┘             │              │
│                                                       ▼              │
│  ┌──────────────────────────────────────────────────────────┐       │
│  │                   WORKER DISPATCH                         │       │
│  │                                                           │       │
│  │  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌────────────┐  │       │
│  │  │ Research │ │ Schedule │ │ Monitor  │ │ Notify     │  │       │
│  │  │ Agent    │ │ Agent    │ │ Agent    │ │ Agent      │  │       │
│  │  └──────────┘ └──────────┘ └──────────┘ └────────────┘  │       │
│  │  ┌──────────┐ ┌──────────┐ ┌──────────┐                 │       │
│  │  │ Email    │ │ Finance  │ │ Custom   │                 │       │
│  │  │ Agent    │ │ Agent    │ │ Agent…   │                 │       │
│  │  └──────────┘ └──────────┘ └──────────┘                 │       │
│  └──────────────────────────────────────────────────────────┘       │
│                              │                                       │
│                              ▼                                       │
│  ┌──────────────────────────────────────────────────────────┐       │
│  │                   EVENT STORE                             │       │
│  │  Append-only log  │  State snapshots  │  Audit trail     │       │
│  └──────────────────────────────────────────────────────────┘       │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
              ┌───────────────────────────────────────┐
              │          OUTPUT CHANNELS               │
              │  Push │ Email │ Chat │ Dashboard       │
              └───────────────────────────────────────┘
```

---

## 4. Core Architecture

### 4.1 Design Principles

These are derived from both the academic research and practical industry experience:

| # | Principle | Source | Rationale |
|---|-----------|--------|-----------|
| P1 | **Stateless orchestrator** | 12-Factor Agents #12 | Each invocation is `f(events, state) → (actions, newState)`. No hidden state. Crash-safe. |
| P2 | **Event-sourced persistence** | ESAA (2026) | All mutations recorded as immutable events. State is a projection. Full replay, audit, debugging. |
| P3 | **Small, focused worker agents** | 12-Factor #10, Anthropic | Monolithic agents fail. Each worker has narrow scope, specific tools, measurable outcomes. |
| P4 | **Human-as-a-tool** | 12-Factor #7 | User interaction is modeled as a tool call with timeout and fallback. Never block indefinitely. |
| P5 | **Progressive trust** | Choose Your Agent (2026) | Start conservative (suggest-only), build track record, increase autonomy. Per-domain trust levels. |
| P6 | **Cost accounting** | AutoGPT postmortems | Per-task and per-day LLM cost tracking. Tiered routing: cheap model for triage, expensive for reasoning. |
| P7 | **Circuit breakers** | AutoGPT, 12-Factor | Max iterations/task, error thresholds, budget limits, exponential backoff. Never loop forever. |
| P8 | **Separation of intention and execution** | ESAA (2026) | LLM emits structured intentions. Deterministic code validates and executes. LLM never touches I/O directly. |
| P9 | **False-positive asymmetry** | ProMemAssist (2025) | The cost of an unnecessary interruption exceeds the cost of a missed opportunity to help. Optimize for precision over recall in proactive suggestions. |
| P10 | **The 80% rule** | User sentiment research | A worker that fails >20% of the time is worse than no worker at all — the cleanup cost of failures exceeds the benefit of successes. Don't ship workers until reliability is proven. |

### 4.2 Layered Architecture

```
┌──────────────────────────────────────────────────┐
│  Presentation Layer                               │
│  Push notifications, email, chat, dashboard       │
├──────────────────────────────────────────────────┤
│  API Layer                                        │
│  REST/gRPC: submit task, query status, feedback   │
├──────────────────────────────────────────────────┤
│  Orchestration Layer                              │
│  Event handler → Task planner → Dispatcher        │
├──────────────────────────────────────────────────┤
│  Agent Layer                                      │
│  Worker agents (research, schedule, monitor, …)   │
├──────────────────────────────────────────────────┤
│  Memory Layer                                     │
│  User model, task memory, episodic memory         │
├──────────────────────────────────────────────────┤
│  Persistence Layer                                │
│  Event store, state snapshots, file storage       │
├──────────────────────────────────────────────────┤
│  Infrastructure Layer                             │
│  Queue, scheduler, secrets, observability         │
└──────────────────────────────────────────────────┘
```

---

## 5. Domain Model

### 5.1 Core Entities

```csharp
// A task is the fundamental unit of work
public sealed class LifeTask
{
    public string Id { get; init; }
    public string Title { get; init; }
    public string? Description { get; init; }
    public TaskOrigin Origin { get; init; }          // User, Proactive, Scheduled, Webhook
    public TaskPriority Priority { get; set; }       // Critical, High, Medium, Low
    public TaskStatus Status { get; set; }           // Queued, Delegated, WaitingOnHuman, Completed, Failed, Cancelled
    public string? AssignedWorker { get; set; }      // Which worker agent type
    public TrustLevel RequiredTrust { get; init; }   // FullAuto, NotifyAndAct, AskAndAct, NeverAuto
    public DateTimeOffset? Deadline { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; init; } = 3;
    public string? ParentTaskId { get; init; }       // For sub-task decomposition
    public TaskResult? Result { get; set; }
}

public enum TaskOrigin { User, Proactive, Scheduled, Webhook, Continuation }
public enum TaskPriority { Critical, High, Medium, Low }
public enum TaskStatus { Queued, Delegated, WaitingOnHuman, Completed, Failed, Cancelled }
public enum TrustLevel { FullAuto, NotifyAndAct, AskAndAct, NeverAuto }

// Result of a completed task
public sealed class TaskResult
{
    public bool Success { get; init; }
    public string Summary { get; init; }          // Human-readable outcome
    public string? DetailedOutput { get; init; }  // Full output (e.g., research report)
    public DateTimeOffset CompletedAt { get; init; }
    public decimal LlmCostUsd { get; init; }      // Cost tracking
}

// User's model — accumulated over time
public sealed class UserProfile
{
    public string UserId { get; init; }
    public Dictionary<string, string> Preferences { get; set; }  // Key-value pairs
    public Dictionary<string, TrustLevel> DomainTrust { get; set; }  // Per-domain autonomy
    public ProactivitySettings Proactivity { get; set; }
    public List<SchedulePattern> KnownPatterns { get; set; }     // Learned routines
    public DateTimeOffset LastInteraction { get; set; }
}

public sealed class ProactivitySettings
{
    public float ProactivityLevel { get; set; } = 0.3f;  // 0.0 = fully reactive, 1.0 = fully proactive
    public TimeOnly QuietHoursStart { get; set; } = new(22, 0);
    public TimeOnly QuietHoursEnd { get; set; } = new(7, 0);
    public int MaxNotificationsPerHour { get; set; } = 3;
    public HashSet<string> EnabledDomains { get; set; }   // Which domains can be proactive
}
```

### 5.2 Events

```csharp
// All state changes are captured as events (ESAA pattern)
public abstract record LifeEvent
{
    public string EventId { get; init; } = Ulid.NewUlid().ToString();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string? CorrelationId { get; init; }  // Groups related events
}

// Task lifecycle events
public record TaskCreated(LifeTask Task) : LifeEvent;
public record TaskDelegated(string TaskId, string WorkerType) : LifeEvent;
public record TaskCompleted(string TaskId, TaskResult Result) : LifeEvent;
public record TaskFailed(string TaskId, string Reason, int RetryCount) : LifeEvent;
public record TaskCancelled(string TaskId, string Reason) : LifeEvent;

// Human interaction events
public record HumanApprovalRequested(string TaskId, string Question, TimeSpan Timeout, string FallbackAction) : LifeEvent;
public record HumanApprovalReceived(string TaskId, bool Approved, string? UserComment) : LifeEvent;
public record HumanApprovalTimedOut(string TaskId, string FallbackAction) : LifeEvent;
public record UserFeedbackReceived(string TaskId, FeedbackType Type, string? Comment) : LifeEvent;

// Proactivity events
public record ProactiveOpportunityDetected(string Domain, string Description, float Confidence) : LifeEvent;
public record ProactiveSuggestionSent(string TaskId, string Channel, string Message) : LifeEvent;
public record ProactiveSuggestionDismissed(string TaskId) : LifeEvent;

// System events
public record ScheduledTriggerFired(string TriggerId, string CronExpression) : LifeEvent;
public record WebhookReceived(string Source, string Payload) : LifeEvent;
public record BudgetThresholdReached(decimal DailySpendUsd, decimal LimitUsd) : LifeEvent;

// Audio lifelogging events (Phase 5)
public record AudioSegmentTranscribed(string SegmentId, string Transcript, string? SpeakerLabel, TimeSpan Duration) : LifeEvent;
public record SpeakerIdentified(string SegmentId, string SpeakerName, float Confidence) : LifeEvent;
public record ConversationSummarized(string ConversationId, string Summary, List<string> ActionItems, List<string> Entities) : LifeEvent;

public enum FeedbackType { Helpful, NotHelpful, TooFrequent, WrongTiming, WrongContent }
```

---

## 6. Event Sourcing & Persistence

### 6.1 Event Store

All state changes are persisted as an append-only event log before any side effects execute. This is the ESAA pattern adapted for a life agent.

```
events/
├── 2026-03-08.events.jsonl     ← One file per day, append-only
├── 2026-03-09.events.jsonl
└── ...

state/
├── current.snapshot.json       ← Latest materialized state (rebuilt from events)
├── user-profile.json           ← User model (updated on feedback events)
└── tasks/
    ├── {taskId}.json           ← Per-task state (active tasks only)
    └── archive/                ← Completed/cancelled tasks
```

**Why event sourcing?**
- **Audit trail**: Every decision, delegation, and user interaction is recorded
- **Replay**: Can reconstruct any past state for debugging
- **Undo**: Can compensate for a wrong action by appending a reversal event
- **Analytics**: Event stream enables offline analysis of agent behavior patterns
- **Crash recovery**: On restart, replay events since last snapshot

### 6.2 State Projection

The orchestrator materializes state from events on startup:

```csharp
public sealed class LifeAgentState
{
    public Dictionary<string, LifeTask> ActiveTasks { get; } = new();
    public PriorityQueue<string, (TaskPriority, DateTimeOffset)> TaskQueue { get; } = new();
    public UserProfile UserProfile { get; set; }
    public DailyBudget Budget { get; set; }
    public DateTimeOffset LastSnapshotAt { get; set; }

    // Rebuild from event log
    public void Apply(LifeEvent evt) => evt switch
    {
        TaskCreated e => AddTask(e.Task),
        TaskDelegated e => MarkDelegated(e.TaskId, e.WorkerType),
        TaskCompleted e => MarkCompleted(e.TaskId, e.Result),
        TaskFailed e => HandleFailure(e.TaskId, e.Reason, e.RetryCount),
        UserFeedbackReceived e => UpdateUserModel(e),
        BudgetThresholdReached e => PauseLowPriorityTasks(),
        // ... etc
    };
}
```

### 6.3 Snapshot Strategy

- Snapshot after every N events (e.g., 100) or every M minutes (e.g., 30)
- On startup: load latest snapshot + replay events since snapshot
- Snapshots are idempotent — safe to create at any point

---

## 7. Orchestrator Design

The orchestrator is the brain — a stateless function that processes events and produces actions.

### 7.1 Core Loop

```csharp
public sealed class LifeAgentOrchestrator
{
    // The orchestrator is a pure function:
    // f(currentState, incomingEvent) → (actions[], newEvents[])
    //
    // It NEVER performs side effects directly.
    // It emits intentions (actions) that the runtime executes.

    public (IReadOnlyList<AgentAction> Actions, IReadOnlyList<LifeEvent> Events)
        Process(LifeAgentState state, LifeEvent incomingEvent)
    {
        var actions = new List<AgentAction>();
        var events = new List<LifeEvent>();

        switch (incomingEvent)
        {
            case TaskCreated created:
                // Classify and route
                var (worker, trust) = ClassifyTask(state, created.Task);
                if (trust == TrustLevel.FullAuto || trust == TrustLevel.NotifyAndAct)
                {
                    actions.Add(new DelegateToWorker(created.Task.Id, worker));
                    events.Add(new TaskDelegated(created.Task.Id, worker));
                    if (trust == TrustLevel.NotifyAndAct)
                        actions.Add(new NotifyUser($"Starting: {created.Task.Title}", "low"));
                }
                else
                {
                    actions.Add(new RequestHumanApproval(created.Task.Id,
                        $"May I proceed with: {created.Task.Title}?",
                        timeout: TimeSpan.FromHours(4),
                        fallback: "queue_for_later"));
                    events.Add(new HumanApprovalRequested(created.Task.Id, ...));
                }
                break;

            case TaskCompleted completed:
                actions.Add(new NotifyUser(completed.Result.Summary, "medium"));
                // Check if this unlocks dependent tasks
                var dependents = FindDependentTasks(state, completed.TaskId);
                foreach (var dep in dependents)
                    actions.Add(new DelegateToWorker(dep.Id, dep.AssignedWorker));
                break;

            case TaskFailed failed:
                if (failed.RetryCount < state.ActiveTasks[failed.TaskId].MaxRetries)
                    actions.Add(new RetryTask(failed.TaskId, delay: TimeSpan.FromMinutes(5)));
                else
                    actions.Add(new NotifyUser($"Failed after {failed.RetryCount} attempts: {failed.Reason}", "high"));
                break;

            case ScheduledTriggerFired trigger:
                var tasks = GenerateScheduledTasks(state, trigger);
                foreach (var task in tasks)
                    events.Add(new TaskCreated(task));
                break;

            // ... other event handlers
        }

        return (actions, events);
    }
}
```

### 7.2 Task Classification

The orchestrator uses an LLM (cheap/fast model) to classify incoming tasks:

1. **Domain detection**: What domain does this task belong to? (research, scheduling, monitoring, communication, finance, health)
2. **Worker routing**: Which worker agent handles this domain?
3. **Trust assessment**: What autonomy level does this task require, given the user's domain trust settings?
4. **Priority scoring**: urgency × importance × deadline proximity
5. **Decomposition**: Does this need to be split into sub-tasks?

Classification should use a small, fast model (e.g., GPT-4o-mini) since it runs frequently and doesn't require deep reasoning.

### 7.3 The LLM-Assisted Planner

For complex tasks that need decomposition, the orchestrator invokes a Planner (reusing the pattern from the Research Agent):

```
User: "Plan my trip to Tokyo in April"
  ↓
Planner decomposes into:
  T1: Research flights (→ MonitorAgent: track prices)
  T2: Research hotels (→ ResearchAgent)
  T3: Check visa requirements (→ ResearchAgent)
  T4: Block calendar dates (→ ScheduleAgent: AskAndAct since it modifies calendar)
  T5: Create packing list (→ FullAuto, low priority, near departure)
  T6: Set reminder for travel insurance (→ ReminderAgent)
```

---

## 8. Worker Agents

Each worker is a self-contained agent with narrow scope, specific tools, and a clear success/failure definition.

### 8.1 Worker Interface

```csharp
public interface IWorkerAgent
{
    string WorkerType { get; }                    // "research", "schedule", "monitor", etc.
    string Description { get; }
    IReadOnlyList<string> SupportedDomains { get; }

    Task<TaskResult> ExecuteAsync(
        LifeTask task,
        UserProfile userProfile,
        CancellationToken ct);
}
```

### 8.2 Planned Workers

| Worker | Domain | Tools | Trust Default | Notes |
|--------|--------|-------|---------------|-------|
| **ResearchAgent** | Research, learning | Web search, content extraction, synthesis | FullAuto | **Phase 1** — Reuse existing blueprint directly |
| **ReminderAgent** | Reminders, follow-ups | Discord notification, scheduled delivery | NotifyAndAct | **Phase 1** — Time-triggered delivery via Discord DM |
| **MonitorAgent** | Prices, deadlines, metrics | Web scraping, API polling | FullAuto for monitoring, AskAndAct for action | **Phase 2** — Watches and alerts; action needs approval |
| **SummaryAgent** | Daily/weekly briefings | Aggregation across all domains | FullAuto | **Phase 2** — Compiles morning briefing via Discord DM |
| **ScheduleAgent** | Calendar, meetings | Google Calendar API, timezone | AskAndAct | **Phase 3** — Modifies shared state; needs approval |
| **EmailTriageAgent** | Inbox management | Gmail API, classification | NotifyAndAct | **Phase 3** — Categorize, summarize, draft (never send auto) |
| **FinanceAdvisorAgent** | Budget, expenses, bills | Bank API (read-only), categorization | NeverAuto | **Phase 4** — Advisory only |
| **AudioLifelogAgent** | Conversation capture, memory | Deepgram nova-3, ECAPA-TDNN, speaker gallery | AskAndAct | **Phase 5** — Always-on transcription via BLE pendant; see §8.4 |

### 8.3 Research Agent Integration

The existing Research Agent blueprint becomes a worker. The integration is straightforward because it already follows the stateless-reducer pattern:

```csharp
public sealed class ResearchWorker : IWorkerAgent
{
    public string WorkerType => "research";

    public async Task<TaskResult> ExecuteAsync(LifeTask task, UserProfile profile, CancellationToken ct)
    {
        // Reuse the existing ResearchOrchestrator
        var orchestrator = new ResearchOrchestrator(config, loggerFactory, priorState: null, progress);
        var result = await orchestrator.ResearchAsync(task.Description, ct);

        return new TaskResult
        {
            Success = result.Report is not null,
            Summary = $"Research complete: {result.Findings.Count} findings, {result.Sources.Count} sources",
            DetailedOutput = result.Report,
            CompletedAt = DateTimeOffset.UtcNow,
            LlmCostUsd = EstimateCost(result),
        };
    }
}
```

### 8.4 Audio Lifelogging Pipeline

> Full research: `knowledge-base/audio-lifelogging-research.md`

Always-on audio capture is a stretch goal that would provide the richest possible context for a life agent — a searchable, speaker-attributed transcript of every conversation. Research shows this is technically feasible today (the open-source `say` tool and the Omi wearable have proven the concept), with costs ~$1/day via Deepgram.

**Architecture (BLE Pendant + Phone + Cloud):**

```
Omi Pendant (nRF) ──BLE──▶ iPhone App (Flutter) ──WSS──▶ Life Agent Backend (AKS)
   │ mic capture                 │ Silero VAD               │
   │ low-power audio             │ BLE management            ├── Deepgram nova-3 (streaming ASR)
   │ codec                       │ background audio mode     ├── Pyannote / ECAPA-TDNN (speaker diarization)
   └─────────────────            └───────────────            ├── LLM structuring (summaries, entities, action items)
                                                             └── SQLite + vector index (episodic memory)
```

**Why a BLE pendant, not Apple Watch/iPhone directly:**
- iOS kills background audio recording unpredictably (no persistent background entitlement)
- Apple Watch battery cannot sustain 24/7 mic recording
- iOS shows an orange indicator dot whenever mic is active
- Dedicated pendants (Omi ~$24 dev kit) are purpose-built: tiny, long battery, BLE-optimized

**Speaker attribution pipeline:**
1. On enrollment: record 10-second sample per contact → generate ECAPA-TDNN embedding (0.8% equal error rate)
2. On each utterance: Deepgram diarization separates speakers → match embeddings against gallery
3. Result: transcripts labeled with actual names ("Jordan: ...", "Alex: ...")

**What gets extracted from transcripts:**

| Extraction | Method | Feeds Into |
|---|---|---|
| Speaker identity | ECAPA-TDNN + gallery match | Episodic memory, social context |
| Action items | LLM extraction | Task creation (auto-creates LifeTasks) |
| Decisions made | LLM extraction | Knowledge graph, meeting history |
| Topics/tags | LLM classification | Semantic memory, daily briefing |
| Key entities | NER | People, places, dates → knowledge graph |
| Emotional tone | Sentiment analysis | Affective memory (Pierce & Mann, 2021) |

**Key real-world findings** (from u/8ta4, 2+ years of daily use):
- Cognitive load *decreased*: "can speak freely without stressing about remembering"
- Emergent use cases: ADHD management, dream journaling, rubber duck debugging
- Cost: ~$1/day via Deepgram with VAD filtering (~60% silence eliminated)
- Biggest unsolved problem: noisy environments (shower, crowd) destroy transcription accuracy
- No viable waterproof mic solution exists yet (IP67 housings kill mic sensitivity)

**Dependencies:** Omi pendant hardware + Flutter app (open source, MIT), Deepgram API key, ECAPA-TDNN model (SpeechBrain, open source), Pyannote (open source). All audio processing happens server-side; no audio stored after transcription (privacy by design).

---

## 9. Memory & Personalization

### 9.1 Three-Tier Memory Architecture

Derived from O-Mem (2025), the Personalized LLM Agents survey (2026), and the existing ResearchMemory:

| Tier | Content | Retention | Storage |
|------|---------|-----------|---------|
| **Working Memory** | Current task context, active conversations | Session-scoped | In-process |
| **Episodic Memory** | Past task results, interactions, feedback | 90 days rolling | Event store (queryable) |
| **Conversational Memory** | Speaker-attributed transcripts from audio lifelogging | Permanent (append-only) | SQLite + vector embeddings (see §8.4) |
| **Semantic Memory** | User preferences, patterns, learned facts | Permanent (updatable) | User profile JSON + vector index |

### 9.2 Active User Profiling (O-Mem Pattern)

The agent continuously updates its user model from interactions:

```
User rejects Monday morning research reports
  → Update: Preference("research_delivery_time", "evening")
  → Update: Pattern("monday_mornings", "low_receptivity")

User always approves calendar suggestions but rejects email drafts
  → Update: DomainTrust("schedule", TrustLevel.NotifyAndAct)
  → Update: DomainTrust("email", TrustLevel.AskAndAct)
```

### 9.3 Preference Learning

From ProPerSim (ICLR 2026): the agent maintains a **preference-aligned behavior model** that adapts from explicit feedback (ratings, approvals/rejections) and implicit signals (which notifications are opened, response latency, dismissal patterns).

**Critical finding**: ProPerSim demonstrated that **explicit feedback massively outperforms implicit cues** — providing action-recommendation history alone, without associated reward signals, offered "limited benefit." This means we must actively solicit feedback (👍/👎 reactions, "too much" / "wrong timing" buttons) rather than relying solely on passive behavioral observation. Every agent output should include lightweight feedback affordances.

Key implementation: every `UserFeedbackReceived` event updates the user profile, and the profile is included in every worker agent invocation context.

---

## 10. Proactivity Engine

### 10.1 Proactive Opportunity Detection

A background process that periodically scans for proactive opportunities. This is the core differentiator from reactive agents.

```csharp
public sealed class ProactivityEngine
{
    // Runs on a timer (e.g., every 15 minutes during non-quiet hours)
    public async Task<IReadOnlyList<ProactiveOpportunity>> ScanAsync(
        LifeAgentState state, UserProfile profile, CancellationToken ct)
    {
        var opportunities = new List<ProactiveOpportunity>();

        // Check each registered proactive scanner
        foreach (var scanner in _scanners)
        {
            if (!profile.Proactivity.EnabledDomains.Contains(scanner.Domain))
                continue;

            var detected = await scanner.ScanAsync(state, profile, ct);
            opportunities.AddRange(detected);
        }

        // Filter by confidence threshold (adjusted by user's proactivity level)
        var threshold = 1.0f - profile.Proactivity.ProactivityLevel; // Higher proactivity = lower threshold
        return opportunities
            .Where(o => o.Confidence >= threshold)
            .OrderByDescending(o => o.Confidence)
            .Take(profile.Proactivity.MaxNotificationsPerHour)
            .ToList();
    }
}
```

### 10.2 Example Proactive Scanners

| Scanner | Checks | Example Output |
|---------|--------|----------------|
| **DeadlineScanner** | Tasks with approaching deadlines | "Project proposal due in 2 days — no draft found" |
| **FollowUpScanner** | Emails/messages awaiting response | "No reply from Bob re: Q2 budget (sent 3 days ago)" |
| **RoutineScanner** | Deviations from learned patterns | "You usually review expenses on Fridays — skip this week?" |
| **PriceScanner** | Monitored items with price changes | "Tokyo flights dropped 22% since last check" |
| **BriefingScanner** | Time-of-day triggers | "Morning: 3 meetings today, 2 emails need response" |
| **ExerciseScanner** | Activity gaps (7+ days, 9-day rule) | "9 days without exercise — habit decay point. You have a gap at 11am." |
| **SleepScanner** | Chronic sleep debt (3+ nights <6hr) | "3 nights under 6 hours this week. Cognitive impairment equivalent to 0.10% BAC." |
| **PreventiveCareScanner** | Overdue appointments (dental, PCP, eye, screenings) | "Annual physical is 3 months overdue. Last cholesterol check: 5 years ago." |
| **MedicationScanner** | Missed doses, approaching refills | "2 days without medication confirmation. Refill due in 5 days." |
| **SocialScanner** | Low social interaction frequency | "2 consecutive weeks below your social baseline. Want to plan something?" |
| **OverworkScanner** | Work-life imbalance, missing days off | "10+ hour days 3 times this week. Sleep dropped 45 minutes." |
| **CompoundWellbeingScanner** | Multi-domain simultaneous decline | Fires when ≥3 wellness domains deteriorate together (see §10.5.5) |

### 10.3 Notification Fatigue Prevention

From BAO (2026) and ProAgentBench (2026), validated by user sentiment research:

1. **Rate limiting**: Max N notifications per hour (user-configurable). ProPerSim showed systems naturally converge to ~6/hour from an initial 24/hour — treat this as an empirical ceiling.
2. **Quiet hours**: No proactive notifications during user-defined quiet periods
3. **Engagement tracking**: If user dismisses 3 consecutive suggestions, temporarily reduce proactivity level. A single unhelpful suggestion can break trust with the entire system (ProMemAssist P5).
4. **Batching**: Group low-urgency items into periodic digests rather than individual notifications
5. **Cost-of-interruption model** (ProMemAssist): Estimate user's current cognitive load before interrupting. ProMemAssist achieved 2.6× higher engagement by delivering 60% fewer messages — the right message at the right time beats many messages.
6. **Behavior regularization** (BAO): Without explicit guardrails, agents demand user attention 91% of the time (UR 0.9064). Information-Seeking Regularization penalizes consecutive interactions without information gain; Over-Thinking Regularization prevents premature token budget exhaustion.
7. **False-positive asymmetry**: Missing a chance to help < interrupting unnecessarily. Optimize all proactive scanners for **precision over recall**.

### 10.4 Empirical Baselines

Hard numbers from user studies that should inform our proactivity calibration:

| Metric | Value | Source |
|--------|-------|--------|
| Optimal notification rate | ~6/hour (self-converged from 24) | ProPerSim (32 personas, ICLR 2026) |
| Positive engagement rate | 24.6% (vs 9.34% baseline) with fewer messages | ProMemAssist (N=12, UIST 2025) |
| User burden with regularization | UR 0.2148 | BAO (CMU/Salesforce/MIT) |
| User burden without regularization | UR 0.9064 (unusable) | BAO |
| Timing prediction accuracy (SOTA) | 64.4% | ProAgentBench (Deepseek-V3.2) |
| Content prediction accuracy (SOTA) | 30.5% semantic similarity | ProAgentBench |
| Proactive assistance F1 (SOTA) | 66.47% | Proactive Agent (2024) |
| User satisfaction improvement over 14 days | 2.2/4 → 3.3/4 | ProPerSim |

---

## 10.5 Wellness & Human Flourishing Model

> Full research: `knowledge-base/human-wellness-research.md` (14 domains, evidence-based targets, agent behaviors)

The Life Agent is not just a task manager — its deeper purpose is ensuring the human it's paired with is thriving physically, mentally, and socially. This requires a principled model of what "wellness" means for the agent's purposes, clear boundaries on what it should and should NOT do, and integration with the proactivity engine (§10.1).

### 10.5.1 Design Philosophy

Three axioms guide all wellness behavior:

1. **Track upstream behaviors, not intimate bodily details.** Monitor exercise frequency, not weight. Monitor social interactions, not emotional states. Monitor appointment attendance, not blood test results. The agent is a behavioral companion, not a medical device.

2. **The failure mode is not lack of knowledge but lack of follow-through.** People know they should exercise, sleep well, and see a dentist. They fail because no system watches whether they actually did it and nobody nudges at the right moment. The agent fills this gap.

3. **Compound signals matter more than individual metrics.** No single metric (sleep, exercise, social contact) is alarming in isolation — a bad week is normal. But when multiple domains deteriorate simultaneously (poor sleep + no exercise + social withdrawal), the compound signal is a powerful predictor of mental health crisis. The agent should detect compound deterioration, not individual bad days.

### 10.5.2 Wellness Domains

The agent monitors six high-level domains, each with evidence-based targets from WHO, CDC, USPSTF, and the U.S. Surgeon General:

| Domain | Key Targets | Data Sources | Scanner(s) |
|--------|------------|--------------|------------|
| **Physical activity** | 150 min/wk aerobic, 2x/wk resistance, 8K steps/day | Wearable, self-report, audio | `ExerciseScanner` |
| **Sleep** | 7-9 hours, <60 min variance in schedule | Wearable, phone screen-on/off | `SleepScanner` |
| **Preventive care** | Annual PCP, dental every 6mo, eye per age bracket, USPSTF screenings | Calendar, self-report | `PreventiveCareScanner` |
| **Medication adherence** | Daily confirmation, refill tracking | Self-report, audio | `MedicationScanner` |
| **Social connection** | 2-3 meaningful interactions/week, family cadence | Audio pipeline, calendar | `SocialScanner` |
| **Stress & recovery** | <10h work days, days off, nature exposure, vacation usage | Calendar, wearable | `OverworkScanner` |

Mental health is intentionally NOT a separate tracked domain — it is an emergent property of the other five. The agent detects mental health concerns through compound deterioration across domains (see §10.5.5), never through direct mood surveillance.

### 10.5.3 The Wellness Proactivity Stack

Wellness monitoring integrates into the existing proactivity engine (§10.1) at three levels:

**Level 1: Passive Observation (Daily)**
- Data collection only: sleep duration logged, steps counted, social conversations detected by audio pipeline, medication confirmations recorded
- No notifications generated at this level
- Feeds into weekly and monthly analysis

**Level 2: Periodic Review (Weekly)**
- Weekly wellness summary in the end-of-week briefing (S2)
- Non-judgmental: "Exercise: 3/5 target days. Sleep: 6.8h avg. Social: 4 conversations. Meds: 6/7 days."
- Trend tracking: "Exercise trending down from last month's 4.2/5 average"
- Appointment reminders when items become overdue

**Level 3: Intervention (Triggered)**
- Single-domain: fires when one domain degrades significantly (e.g., no exercise for 9 days → S69)
- Compound: fires when 3+ domains deteriorate simultaneously (S73 — The Compound Wellbeing Signal)
- Crisis: fires immediately on detection of crisis language (S75 — resource delivery, not counseling)

### 10.5.4 Hard Boundaries

Things the agent must **never** do:

| Boundary | Rationale |
|----------|-----------|
| Diagnose medical conditions | Liability, harm, not qualified |
| Recommend specific medications or dosages | Medical practice boundary |
| Monitor weight or BMI | Eating disorder risk; counterproductive for many |
| Count calories or judge food choices | Eating disorder risk |
| Track brushing/flossing | Crosses the dignity line |
| Monitor bathroom habits | Privacy violation |
| Provide therapy or counseling | Not trained; displaces professional help |
| Follow up after crisis resource delivery | Clinical territory |
| Fabricate social interaction (chatbot companionship) | Not a substitute for human connection |
| Continue nagging after dismissal | Annoyance → trust destruction |

### 10.5.5 The Compound Wellbeing Signal

The most important concept in the wellness model. Individual bad days are noise. Simultaneous multi-domain decline is signal.

```
Triggers when ≥3 of the following are true:
  - No exercise for 7+ days
  - Sleep averaging <6 hours for 5+ days
  - No social conversations detected for 5+ days (audio pipeline)
  - No leaving the house for 3+ days (if location data available)
  - Mood self-report declining for 1+ week (if opted in)
  - Medication adherence dropped below 60% (if applicable)

Fires at most: once per 30 days
Response: Single, carefully worded check-in (see S73)
After firing: Suppress all individual wellness nudges for 48 hours
If dismissed: Don't re-fire for 30 days
```

This is the scenario that justifies the entire wellness system. No individual scanner would detect that someone is quietly withdrawing from life. The compound signal catches it.

### 10.5.6 Audio Pipeline Integration

The audio lifelogging pipeline (§8.4) is the wellness model's most powerful data source:

| Audio Signal | Wellness Domain | Agent Action |
|---|---|---|
| Social conversations detected (speaker ID) | Social connection | Count interactions, identify who |
| Spoken health intention ("I should start running") | All | Log and follow up in 7 days (S82) |
| Spoken health complaint (3+ times/week) | Preventive care | Private log, suggest mentioning to doctor (S67) |
| Medication mention ("I forgot my pills") | Medication | Immediate check-in |
| Crisis language | Mental health | Immediate resource delivery (S75) |
| No social audio for 5+ days | Social isolation | Feed into compound signal |
| Exercise planning ("gym tomorrow") | Physical activity | Follow up on intention |

### 10.5.7 Longitudinal Value

The wellness model's full value emerges over time:

- **Week 1-4**: Data collection. Agent observes patterns silently.
- **Month 2-3**: Agent begins gentle nudges based on established baselines.
- **Month 6**: Enough data for meaningful trend analysis and correlations.
- **Year 1**: Annual health review (S80) — the first time anyone has compiled a year-long view of their health behaviors.
- **Year 2+**: Year-over-year comparison, life stage transitions, long-term habit tracking.

This is the unique value proposition of a persistent agent: it never forgets, it connects patterns across months, and it can tell you things about yourself that you'd never notice in real-time.

> See `knowledge-base/human-wellness-research.md` for the full evidence base. See `scenarios-new.md` §15-20 (S64–S82) for all wellness scenarios.

---

## 11. User Interaction Layer

### 11.1 Channels

| Channel | Direction | Use Case | Latency |
|---------|-----------|----------|---------|
| **Push notification** | Agent → User | Urgent alerts, approval requests | Seconds |
| **Email digest** | Agent → User | Daily/weekly summaries, research reports | Batched |
| **Discord** | Bidirectional | Ad-hoc requests, conversational feedback | Real-time |
| **Dashboard (web)** | Bidirectional | Task overview, settings, history, audit trail | On-demand |
| **CLI** | User → Agent | Developer mode; task submission, debugging | Real-time |

### 11.2 Human-as-Tool Pattern

From 12-Factor Agents: human interaction is a tool call with timeout and fallback.

```csharp
public sealed class HumanInteractionTool
{
    public async Task<HumanResponse> RequestApproval(
        string taskId,
        string message,
        string channel,           // "push" | "email" | "chat"
        string urgency,           // "low" | "medium" | "high" | "critical"
        TimeSpan timeout,         // How long to wait
        string fallbackAction)    // What to do if no response
    {
        // Send via channel
        await _notificationService.SendAsync(channel, message, urgency);

        // Wait with timeout
        var response = await _responseQueue.WaitAsync(taskId, timeout);

        if (response is null)
        {
            // Timeout — execute fallback
            _eventStore.Append(new HumanApprovalTimedOut(taskId, fallbackAction));
            return new HumanResponse { TimedOut = true, FallbackExecuted = fallbackAction };
        }

        _eventStore.Append(new HumanApprovalReceived(taskId, response.Approved, response.Comment));
        return response;
    }
}
```

### 11.3 Feedback Mechanisms

Every agent output includes lightweight feedback options:

- **Thumbs up/down**: Binary quality signal
- **"Too much" / "Wrong timing"**: Proactivity calibration
- **"Not relevant"**: Domain/content filtering
- **Free-text comment**: Rich feedback for edge cases

---

## 12. Trust & Autonomy Model

### 12.1 The Preference-Performance Paradox

The central design tension, empirically validated by Choose Your Agent (2026, N=243, 6,561 decisions):

| Mode | User Preference | Economic Outcome |
|------|----------------|------------------|
| **Advisor** (see suggestion, decide yourself) | **44%** preferred | Good but suboptimal |
| **Coach** (AI shapes your thinking) | 15.2% preferred | Moderate |
| **Delegate** (AI decides for you) | 19.3% preferred | **Best outcomes** (β=0.084, p=.034) |
| **No AI** | 21.4% preferred | Baseline |

Users systematically prefer the mode that gives them worse outcomes. They trade economic efficiency for perceived autonomy. This isn't irrational — it reflects a deep human need for control. Our design must respect this by defaulting to Advisor and earning the right to Delegate.

Additional trust dynamics from user studies:
- In Advisor mode, users only followed AI's recommendation **70.6% of the time**
- In Coach mode, users retained their initial decision **69.5% of the time** even when AI recommended differently
- Higher pre-game trust predicted greater AI usage (r~.25, p<.01), but this trust was fragile
- Users who preferred any AI mode reported **20% higher mental effort** than autonomy-seekers (p<.01)
- **Algorithm aversion**: the tendency to abandon AI after observing even small errors was documented as a significant adoption barrier

### 12.2 Trust Fragility

Trust is asymmetric — one failure outweighs many successes.

> *ProMemAssist P5 indicated that a single unhelpful suggestion could break trust with the entire system.*

> *"Trust was influenced more by the quality of the assistance than the timing of delivery."* — ProMemAssist

> *"Poorly calibrated proactivity can undermine user trust, agency, and interaction quality when assistance is mistimed or misaligned."* — PROPER (2026)

Design implication: proactive agents must be calibrated toward **high precision over high recall**. Getting the content right matters more than getting the moment right — though bad timing still damages the relationship.

### 12.3 Trust Levels Per Domain

Given the paradox above, the solution is gradual trust escalation that respects user preference for control while nudging toward better outcomes.

```
Initial state (day 1):
  research:    FullAuto       ← Low risk, high value
  reminders:   NotifyAndAct   ← Low risk
  monitoring:  FullAuto       ← Read-only
  scheduling:  AskAndAct      ← Modifies shared state
  email:       AskAndAct      ← Communication on behalf
  finance:     NeverAuto      ← High risk
  wellness:    NotifyAndAct   ← Observe and inform; never prescribe

After 30 days with positive feedback:
  scheduling:  NotifyAndAct   ← Earned trust via track record
  email:       NotifyAndAct   ← (for routine replies only)
  wellness:    FullAuto       ← (for appointment reminders only)
```

### 12.4 Trust Escalation Rules

```
For each domain D:
  IF last 10 AskAndAct tasks in D were all APPROVED by user
  AND user hasn't explicitly locked trust level for D
  THEN suggest: "I've gotten 10/10 right for {D}. Want me to start doing these automatically?"

For each domain D:
  IF user rejects 3 consecutive suggestions in D
  THEN demote trust level by one step
  AND notify: "I'll ask before acting on {D} tasks from now on"
```

### 12.5 The 21.4% Problem

Over one-fifth of participants in Choose Your Agent preferred no AI at all. The Life Agent must:
- **Never become inescapable**: mute/pause/disable must always be one command away
- **Prove value early**: the first proactive action should be unambiguously useful (morning briefing is ideal — low risk, high perceived value)
- **Degrade gracefully into a tool**: if the user isn't receptive to proactivity, the agent should still be useful as a reactive assistant

---

## 13. Scheduling & Triggers

### 13.1 Trigger Types

```csharp
public abstract record Trigger
{
    public string TriggerId { get; init; }
    public string? AssociatedTaskTemplate { get; init; }  // Creates this task type when fired
}

public record CronTrigger(string CronExpression) : Trigger;          // "0 7 * * 1-5" = weekday 7am
public record WebhookTrigger(string Endpoint, string Secret) : Trigger;  // External service callbacks
public record EventTrigger(string EventPattern) : Trigger;           // React to internal events
public record IntervalTrigger(TimeSpan Interval) : Trigger;          // Every N minutes/hours
```

### 13.2 Built-in Schedules

| Schedule | Cron | Task |
|----------|------|------|
| Morning briefing | `0 7 * * 1-5` | Compile calendar + email + weather + wellness summary |
| Weekly review | `0 18 * * 5` | Summarize week's tasks, outcomes, pending items, wellness pulse |
| Proactivity scan | `*/15 * * * *` | Check for proactive opportunities |
| Price monitoring | `0 */6 * * *` | Check monitored items for changes |
| Follow-up check | `0 10 * * *` | Scan for stale conversations needing follow-up |
| Budget reconcile | `0 20 1 * *` | Monthly expense review |
| Wellness weekly review | `0 20 * * 0` | Compile exercise, sleep, social, medication adherence scores |
| Preventive care audit | `0 9 1 * *` | Check for overdue appointments and screenings |
| Medication reminder | User-configured | Daily medication adherence ping (if opted in) |

---

## 14. Safety & Reliability

### 14.1 Circuit Breakers

| Breaker | Threshold | Action |
|---------|-----------|--------|
| Task retry limit | 3 retries per task | Cancel task, notify user |
| Daily LLM budget | Configurable USD cap | Pause non-critical tasks, notify user |
| Worker timeout | 10 min per worker invocation | Kill worker, mark task failed |
| Error rate | >50% failure in last 10 tasks | Pause orchestrator, require manual restart |
| Notification rate | User-configured max/hour | Queue excess notifications for next batch |

### 14.2 Graceful Degradation

If a worker agent fails, the system continues operating:
- Other workers are unaffected (process isolation)
- Failed tasks enter retry queue with exponential backoff
- If a worker type is consistently failing, its tasks are paused and user is notified
- The orchestrator itself never crashes due to a worker failure

### 14.3 Idempotent Operations

Every action the system takes must be safe to retry:
- Event store uses event IDs for deduplication
- Worker agents check for existing results before re-executing
- Notification service deduplicates by task ID + channel
- State snapshots are deterministic projections of the event log

### 14.4 The 80% Rule

From user sentiment research: **agents that work 80% of the time create worse outcomes than no agent at all.** The 20% failure cases destroy trust (§12.2) and create cleanup work that exceeds the benefit of the 80% successes.

Practical implications:
- **Do not ship a worker agent until it achieves >95% reliability** on its defined task set
- Track per-worker success rate as a first-class metric
- Auto-disable workers whose rolling success rate drops below 90%, with user notification
- Better to have 3 excellent workers than 7 mediocre ones — this is why Lindy.ai (400K+ users) succeeded with narrow scope while Rabbit R1 failed trying to do everything

### 14.5 Observability

- **Structured logging**: Every orchestrator decision, worker invocation, and user interaction
- **OpenTelemetry traces**: Distributed tracing across orchestrator → worker → LLM calls
- **Metrics**: Task throughput, latency percentiles, LLM cost per task, user feedback ratios
- **Trust metrics**: Per-worker success rate, trust escalation/demotion counts, consecutive rejection streaks
- **Proactivity metrics**: Notifications sent/dismissed/engaged, rate of "too much" feedback, effective notification rate vs target
- **Dashboard**: Real-time view of active tasks, queue depth, budget usage, error rates, worker reliability scores

---

## 15. Technology Choices

### 15.1 Runtime & Framework

| Component | Choice | Rationale |
|-----------|--------|-----------|
| **Runtime** | .NET 10 | Consistency with Research Agent; excellent async/concurrency support |
| **Agent framework** | Microsoft Agent Framework | Already proven in Research Agent blueprint; ChatClientAgent, tool binding |
| **Hosting** | ASP.NET Core + BackgroundService | Long-running process with API endpoints; native .NET hosting model |
| **Container** | Docker on AKS (`jk-aks-2`) | Single replica Deployment in `life-agent` namespace. Image pushed to `ribacr123.azurecr.io/life-agent:<sha>`. Resource limits: 250m CPU / 512Mi (matching existing `discord-sky-bot` pattern). Liveness/readiness on `/healthz`. |

### 15.2 Persistence

| Component | Choice | Rationale |
|-----------|--------|-----------|
| **Event store** | SQLite (WAL mode, append-only table) | Zero-ops, embedded, single-file. WAL mode supports concurrent reads + single writer — sufficient for a single-user agent. |
| **State snapshots** | SQLite JSON column | Co-located with events; single file simplifies backup/migration |
| **User profile** | SQLite | Structured data with JSON fields for flexible preferences |
| **Vector search** (future) | sqlite-vec extension or migrate to PostgreSQL + pgvector | Defer until semantic memory is needed |
| **File storage** | Azure Disk PV (`default` StorageClass, `WaitForFirstConsumer`) | SQLite db + research reports. Same storage pattern as existing cluster workloads. |

SQLite WAL mode gives us ACID with zero operational overhead. The entire agent's state is a single `.db` file that can be backed up by copying it. If we outgrow SQLite (multi-instance scaling, concurrent writers), migrate to PostgreSQL (the cluster already has `my-postgres-postgresql-0` in the `default` namespace) — the event-sourced schema translates directly.

**AKS storage details** (from cluster inspection):
- StorageClass: `default` (Azure Disk CSI, `WaitForFirstConsumer` binding) — same as 5 other PVCs on the cluster
- Alternatively: `azurefile` for ReadWriteMany if we ever need multi-pod (used by `discord-sky-memory` and `sendie-server-data`)
- PVC size: 1Gi (generous for SQLite; most existing PVCs are 1-5Gi)

**Backup strategy**: CronJob on AKS that copies the SQLite file to Azure Blob Storage daily.

### 15.3 Communication

| Component | Choice | Rationale |
|-----------|--------|-----------|
| **Task queue** | In-process `Channel<T>` | Single instance on AKS; no need for external queue |
| **Scheduling** | `IHostedService` with `PeriodicTimer` (or Quartz.NET for complex cron) | Native .NET; cron expressions for scheduled tasks |
| **Chat integration** | **Discord** via [Discord.Net](https://discordnet.dev/) Gateway | Primary interaction channel. Bidirectional: user submits tasks via Discord commands/DMs, agent responds in channel/DM. Follows existing `discord-sky-bot` pattern (Gateway connection, no public endpoint needed). |
| **CLI** | `dotnet run` with stdin/stdout | Developer mode; task submission, debugging, local testing |

Push notifications and email deferred to Phase 3+. Discord covers bidirectional messaging for v1. The existing `discord-sky-bot` on the cluster proves this pattern works well — Gateway mode, K8s Secret for bot token, ConfigMap for settings.

### 15.4 LLM Routing

| Role | Model Tier | Examples | Use Case |
|------|-----------|----------|----------|
| **Triage / Classification** | Fast & cheap | GPT-4o-mini, Haiku | Task routing, priority scoring, notification generation |
| **Planning / Reasoning** | Capable | GPT-4o, Sonnet | Task decomposition, proactive opportunity evaluation |
| **Deep Work** | Best available | GPT-5, Opus | Research synthesis, complex analysis |

The orchestrator uses the cheap tier. Worker agents use capable/deep tiers depending on the task. Cost is tracked per-task for budget enforcement.

---

## 16. Project Structure

```
blueprints/life-agent/
├── LifeAgent.slnx
├── README.md
├── docs/
│   └── design.md                    ← This document
│
├── LifeAgent.Core/                  ← Domain model, events, interfaces
│   ├── Models/
│   │   ├── LifeTask.cs
│   │   ├── Events.cs
│   │   ├── UserProfile.cs
│   │   └── TaskResult.cs
│   ├── Memory/
│   │   ├── IUserProfileStore.cs
│   │   ├── IEventStore.cs
│   │   └── ITaskStore.cs
│   └── Workers/
│       └── IWorkerAgent.cs
│
├── LifeAgent.Orchestrator/          ← Core orchestration logic
│   ├── LifeAgentOrchestrator.cs     ← Stateless event processor
│   ├── TaskClassifier.cs            ← LLM-assisted task routing
│   ├── TaskPlanner.cs               ← LLM-assisted decomposition
│   └── ProactivityEngine.cs         ← Proactive opportunity scanner
│
├── LifeAgent.Workers/               ← Worker agent implementations
│   ├── ResearchWorker.cs            ← Wraps existing ResearchAgent
│   ├── ReminderWorker.cs
│   ├── MonitorWorker.cs
│   ├── ScheduleWorker.cs
│   ├── EmailTriageWorker.cs
│   ├── SummaryWorker.cs
│   └── FinanceAdvisorWorker.cs
│
├── LifeAgent.Persistence/           ← Event store, state, profiles
│   ├── PostgresEventStore.cs
│   ├── PostgresTaskStore.cs
│   ├── PostgresUserProfileStore.cs
│   └── StateProjection.cs
│
├── LifeAgent.Channels/              ← I/O adapters
│   ├── PushNotificationChannel.cs
│   ├── EmailChannel.cs
│   ├── DiscordChannel.cs
│   ├── CliChannel.cs
│   └── INotificationChannel.cs
│
├── LifeAgent.App/                   ← Host + API
│   ├── Program.cs                   ← ASP.NET Core host with BackgroundService
│   ├── Api/
│   │   ├── TasksController.cs       ← Submit/query/cancel tasks
│   │   ├── FeedbackController.cs    ← User feedback endpoints
│   │   └── SettingsController.cs    ← Proactivity, trust, preferences
│   ├── appsettings.json
│   └── Dockerfile
│
└── LifeAgent.Tests/
    ├── OrchestratorTests.cs
    ├── ProactivityEngineTests.cs
    ├── EventStoreTests.cs
    └── WorkerTests/
```

---

## 17. Implementation Dependencies

This section maps **what depends on what** — both internal components and external prerequisites. Use it to determine build order and identify blockers before starting each phase.

### 17.1 Component Dependency Graph

```
LAYER 0 ─ Domain Model (no dependencies — build first)
  ├── LifeTask, TaskResult, enums (TaskOrigin, TaskPriority, TaskStatus, TrustLevel)
  ├── Events (all LifeEvent records: TaskCreated, TaskDelegated, …)
  ├── UserProfile, ProactivitySettings
  ├── IWorkerAgent interface
  ├── IEventStore / ITaskStore interfaces
  └── INotificationChannel interface

LAYER 1 ─ Persistence + Channels (depends on Layer 0)
  ├── SQLite Event Store (append-only table, WAL mode) ──┐
  ├── SQLite UserProfile Store                            │
  ├── State Projection ◄──────────────────────────────────┘ (replays events → LifeAgentState)
  ├── CLI Channel (stdin/stdout)
  └── Discord Channel ← Discord.Net Gateway + Discord Bot Token (external)

LAYER 2 ─ Orchestrator (depends on Layers 0–1)
  ├── Task Classifier ← LLM call (OpenAI key, external)
  ├── Task Planner ← LLM call
  ├── Priority Queue (in-process Channel<T>)
  ├── Worker Dispatch (needs IWorkerAgent implementations from Layer 3)
  ├── Circuit Breakers (Polly: retry, timeout, rate-limit)
  └── Orchestrator Core Loop (ties the above together)

LAYER 3 ─ Worker Agents (depend on Layers 0–2)
  ├── ResearchWorker ← ResearchAgent extracted as library reference
  ├── ReminderWorker ← Scheduling infra (Cronos) + INotificationChannel
  ├── MonitorWorker ← Web search/HTTP tools + INotificationChannel + scheduling
  ├── SummaryWorker ← Event Store (reads history) + INotificationChannel + Calendar (optional)
  ├── ScheduleWorker ← Google Calendar API + OAuth2 tokens (external)
  ├── EmailTriageWorker ← Gmail API + OAuth2 tokens (external)
  └── FinanceAdvisorWorker ← Bank API (external, TBD)

LAYER 4 ─ Proactivity Engine (depends on Layers 0–3)
  ├── Scanner framework (IScannerPlugin interface + runner)
  ├── DeadlineScanner ← Event Store (queries active tasks with deadlines)
  ├── BriefingScanner ← Calendar integration + Email + Event Store
  ├── PriceScanner ← MonitorWorker's persisted watch data
  ├── FollowUpScanner ← Email integration (Gmail)
  ├── RoutineScanner ← UserProfile (learned behavioral patterns)
  └── Notification Fatigue Prevention ← UserProfile + feedback event history

LAYER 5 ─ Trust & Adaptation (depends on Layers 0–4)
  ├── Trust escalation/demotion logic ← UserFeedbackReceived events from Event Store
  ├── Preference learning ← Discord reactions + explicit feedback
  └── O-Mem-style active profiling ← full interaction history

LAYER 6 ─ Host + Deployment (wraps Layers 0–5)
  ├── ASP.NET Core host + BackgroundService (long-running process lifecycle)
  ├── Health checks (/healthz endpoint)
  ├── OpenTelemetry (structured logging + traces)
  ├── Dockerfile (multi-stage .NET 10 build)
  └── K8s manifests (Deployment, PVC, Secret, ConfigMap in `life-agent` namespace)
```

### 17.2 External Dependencies

Prerequisites that must be set up **outside the codebase** before the corresponding component works.

| Dependency | Needed By | Phase | Setup Steps | Status |
|---|---|---|---|---|
| **Discord Bot registration** | Discord Channel | 1 | Create application at discord.com/developers → add Bot → copy token → create K8s Secret | Not started |
| **Discord server + invite** | Discord Channel | 1 | Invite bot to a Discord server (or reuse an existing one) with DM permissions | Not started |
| **OpenAI API key** | Task Classifier, Task Planner, all Workers | 1 | Already have key; store in K8s Secret | Available |
| **ResearchAgent as library** | ResearchWorker | 1 | Extract `ResearchOrchestrator` into a referenceable project; `LifeAgent.Workers` adds `<ProjectReference>` to `ResearchAgent.App` (or new `ResearchAgent.Lib`) | Needs refactor |
| **AKS namespace + manifests** | Deployment | 1 | `kubectl create namespace life-agent` + Deployment/Service/PVC/Secret/ConfigMap YAML | Not started |
| **ACR image push** | Deployment | 1 | `docker build` + `az acr login` + `docker push ribacr123.azurecr.io/life-agent:<sha>` | Not started |
| **Google Cloud project** | ScheduleWorker, EmailTriageWorker | 3 | Create project in console.cloud.google.com → enable Calendar + Gmail APIs | Not started |
| **Google OAuth2 credentials** | ScheduleWorker, EmailTriageWorker | 3 | Create OAuth client ID → run auth flow → store refresh token in K8s Secret | Not started |
| **Bank API credentials** | FinanceAdvisorWorker | 4 | Provider-specific (Plaid, bank API, etc.) → read-only access | Not started |

### 17.3 Internal Cross-Component Dependencies

These are **within the codebase** — one component can't be built/tested until another exists.

| Component | Hard Dependencies (must exist) | Soft Dependencies (useful but not blocking) |
|---|---|---|
| **SQLite Event Store** | Domain Model (event types) | — |
| **State Projection** | Event Store, Domain Model | — |
| **CLI Channel** | INotificationChannel interface | — |
| **Discord Channel** | INotificationChannel interface, Discord Bot Token | — |
| **Task Classifier** | Domain Model (LifeTask, enums), LLM client | User Profile (for trust lookup) |
| **Task Planner** | Domain Model, LLM client | Event Store (prior context) |
| **Orchestrator Core Loop** | Event Store, State Projection, Task Classifier, Priority Queue | Workers (can stub with a no-op worker) |
| **Worker Dispatch** | IWorkerAgent interface, ≥1 worker implementation | — |
| **ResearchWorker** | IWorkerAgent, ResearchAgent-as-library | User Profile (for preferences) |
| **ReminderWorker** | IWorkerAgent, INotificationChannel, Cronos (scheduling) | — |
| **MonitorWorker** | IWorkerAgent, INotificationChannel, HTTP client, Event Store (persisted watches) | — |
| **SummaryWorker** | IWorkerAgent, Event Store, INotificationChannel | Calendar integration, Email integration |
| **ProactivityEngine** | Event Store, User Profile, ≥1 Scanner | All Workers (for richer scanning) |
| **DeadlineScanner** | Event Store (active tasks with deadlines) | — |
| **BriefingScanner** | Event Store | Google Calendar, Gmail |
| **PriceScanner** | MonitorWorker's persisted data | — |
| **Trust Logic** | Event Store (feedback events), User Profile | All Workers (to observe approval rates) |
| **Health Check** | ASP.NET host | Event Store (for readiness) |

### 17.4 ResearchAgent Library Extraction

The existing Research Agent is a console app (`ResearchAgent.App`). To use it as a worker, the orchestration logic needs to be callable as a library.

**Current structure:**
```
blueprints/research-agent/
├── ResearchAgent.App/          ← Console app (Program.cs + ResearchOrchestrator.cs)
├── ResearchAgent.Core/         ← Models + Memory (already a library)
└── ResearchAgent.Plugins/      ← Tool plugins (already a library)
```

**Required change**: Move `ResearchOrchestrator.cs` from `ResearchAgent.App` into a new `ResearchAgent.Orchestration` project (or into `ResearchAgent.Core`), so that `LifeAgent.Workers` can reference it without pulling in `Program.cs` / console hosting.

**Dependency after extraction:**
```
LifeAgent.Workers
  └── ProjectReference: ResearchAgent.Orchestration (or ResearchAgent.Core)
        ├── ResearchAgent.Core (Models, Memory)
        └── ResearchAgent.Plugins (Web search, extraction tools)
```

Alternatively, keep `ResearchOrchestrator` in `ResearchAgent.App` and add a `<ProjectReference>` directly — the orchestrator class itself has no dependency on `Program.cs`. This is simpler but couples the projects more tightly.

### 17.5 Phase 1 Critical Path

The build-order for Phase 1, accounting for all dependencies:

```
                    ┌─────────────────────┐
                    │  1. Domain Model     │  (no deps)
                    │  LifeTask, Events,   │
                    │  Interfaces          │
                    └────────┬────────────┘
                             │
              ┌──────────────┼──────────────┐
              ▼              ▼              ▼
    ┌──────────────┐ ┌──────────────┐ ┌──────────────┐
    │ 2a. SQLite   │ │ 2b. CLI      │ │ 2c. Discord  │  (depend on interfaces)
    │ Event Store  │ │ Channel      │ │ Channel      │
    └──────┬───────┘ └──────────────┘ └──────┬───────┘
           │                                  │
           │         ┌──────────────┐         │
           │         │ 2d. Research │         │
           │         │ Agent lib    │         │
           │         │ extraction   │         │
           │         └──────┬───────┘         │
           ▼                │                 │
    ┌──────────────┐        │                 │
    │ 3. State     │        │                 │
    │ Projection   │        │                 │
    └──────┬───────┘        │                 │
           │                │                 │
           ▼                ▼                 │
    ┌───────────────────────────────┐         │
    │ 4. Orchestrator Core Loop     │         │
    │  + Task Classifier (LLM)     │         │
    │  + Priority Queue            │         │
    │  + Circuit Breakers (Polly)  │         │
    └──────────────┬────────────────┘         │
                   │                          │
         ┌─────────┴─────────┐                │
         ▼                   ▼                ▼
  ┌──────────────┐   ┌──────────────────────────────┐
  │ 5a. Research │   │ 5b. ReminderWorker            │
  │ Worker       │   │  (Cronos + Discord Channel)   │
  └──────────────┘   └──────────────────────────────┘
                              │
                              ▼
                   ┌───────────────────┐
                   │ 6. ASP.NET Host   │
                   │  + BackgroundSvc  │
                   │  + /healthz       │
                   │  + OTel           │
                   └────────┬──────────┘
                            │
                            ▼
                   ┌───────────────────┐
                   │ 7. Dockerfile +   │
                   │ K8s Manifests +   │
                   │ ACR Push + Deploy │
                   └───────────────────┘
```

Steps 2a–2d can be built in parallel. Steps 5a–5b can be built in parallel. Steps 1–3 are strictly sequential.

### 17.6 Phase 2–4 Dependency Additions

Each phase layers on new components with their own dependencies:

**Phase 2** (Proactivity + Monitoring):
```
Scheduling infra (Cronos + PeriodicTimer)     ← built-in .NET, no new external deps
  └── SummaryWorker ← Event Store + Discord Channel
  └── MonitorWorker ← HTTP tools + Event Store + Discord Channel
ProactivityEngine ← Event Store + User Profile
  ├── DeadlineScanner ← Event Store
  └── BriefingScanner ← Event Store (+ Calendar/Email if available)
User Profile persistence ← SQLite
Quiet hours + rate limiting ← User Profile + ProactivitySettings
```

**Phase 3** (Interaction + Trust + Google APIs):
```
Google Cloud project + OAuth2 ← external setup (blocking for ScheduleWorker/EmailTriageWorker)
  ├── ScheduleWorker ← Google.Apis.Calendar.v3, Google.Apis.Auth
  └── EmailTriageWorker ← Google.Apis.Gmail.v1, Google.Apis.Auth
Human-as-tool pattern ← Discord Channel (buttons/reactions), Orchestrator
Feedback collection ← Discord Channel + Event Store
Trust logic ← Event Store (feedback history) + User Profile
```

**Phase 4** (Intelligence + Polish):
```
O-Mem user model ← all prior event/feedback history
Tiered LLM routing ← Orchestrator + config (model selection per role)
Cost tracking ← per-task LLM cost recorded in events
FinanceAdvisorWorker ← Bank API (external, TBD)
Web dashboard ← ASP.NET host (new Razor/Blazor project or SPA)
PostgreSQL migration ← schema translation (event store is append-only, straightforward)
```

**Phase 5** (Audio Lifelogging — see §8.4):
```
Omi pendant + Flutter app ← external hardware (~$24 dev kit)
BLE audio receiver ← Flutter app (or custom .NET BLE client)
  └── Silero VAD ← on-device pre-filtering (reduces API costs ~60%)
Deepgram nova-3 streaming ← WebSocket client + API key (external)
  └── Real-time transcript stream ← Deepgram WebSocket API
ECAPA-TDNN speaker embeddings ← SpeechBrain model (open source)
  └── Speaker gallery ← SQLite table (enrollment embeddings)
  └── Gallery matching ← cosine similarity on ECAPA-TDNN vectors
AudioLifelogAgent ← Orchestrator + Deepgram + speaker pipeline
  └── LLM structuring ← transcript → summary + entities + action items
  └── Conversational memory store ← SQLite + vector embeddings
  └── Auto-task creation ← spoken action items → LifeTask events
Privacy controls ← User Profile (pause, exclude contacts/locations)
```

### 17.7 NuGet Package Dependencies

| Package | Version | Project | Phase | Purpose |
|---|---|---|---|---|
| Microsoft.Agents.AI.OpenAI | 1.0.0-rc3 | Workers, Orchestrator | 1 | LLM integration (ChatClientAgent) |
| Microsoft.Agents.AI.Workflows | 1.0.0-rc3 | Workers, Orchestrator | 1 | Agent orchestration (InProcessExecution) |
| Microsoft.EntityFrameworkCore.Sqlite | 10.0.3 | Persistence | 1 | SQLite event store + WAL mode |
| Microsoft.Extensions.Hosting | 10.0.3 | App | 1 | BackgroundService, IHostedService lifecycle |
| Discord.Net | 3.19.0 | Channels | 1 | Discord Gateway bot (DMs, slash commands) |
| Polly.Core | 8.6.6 | Core, Orchestrator | 1 | Retry, circuit breaker, timeout resilience |
| Cronos | 0.11.1 | Orchestrator | 1 | Cron expression parsing for scheduled triggers |
| Microsoft.Extensions.Logging.Console | 10.0.3 | App | 1 | Console logging (dev mode) |
| Microsoft.Extensions.Configuration.* | 10.0.3 | App | 1 | appsettings.json, env vars, user secrets |
| OpenTelemetry | 1.12.0 | App | 1 | Distributed tracing |
| OpenTelemetry.Exporter.Console | 1.12.0 | App | 1 | Dev-mode trace output |
| Google.Apis.Calendar.v3 | 1.73.0 | Workers | 3 | Google Calendar read/write |
| Google.Apis.Gmail.v1 | 1.73.0 | Workers | 3 | Gmail scanning and draft creation |
| Google.Apis.Auth | 1.73.0 | Workers | 3 | OAuth2 for Google APIs |
| OpenTelemetry.Exporter.OpenTelemetryProtocol | 1.15.0 | App | 2+ | OTLP export for production observability |

All packages verified available on NuGet as of 2026-03-08.

---

## 18. Phased Rollout

### Phase 1: Foundation (2-3 weeks)

**Goal**: Orchestrator + event store + 2 workers, CLI + Discord

Build order follows §17.5 critical path:

- [ ] Core domain model (LifeTask, Events, UserProfile) — **Layer 0, no deps**
- [ ] Event store (SQLite WAL-mode, append-only table) — **Layer 1, needs domain model**
- [ ] State projection (replay events → LifeAgentState) — **Layer 1, needs event store**
- [ ] CLI channel — **Layer 1, needs INotificationChannel**
- [ ] Discord channel via Discord.Net Gateway — **Layer 1, needs bot token (external)**
- [ ] ResearchAgent library extraction — **Layer 1, refactor existing codebase**
- [ ] Task classifier (LLM-assisted routing) — **Layer 2, needs domain model + LLM**
- [ ] Orchestrator core loop + priority queue + circuit breakers — **Layer 2, needs event store + projection**
- [ ] ResearchWorker (wraps extracted ResearchAgent) — **Layer 3, needs orchestrator + library**
- [ ] ReminderWorker (Cronos + Discord DM delivery) — **Layer 3, needs orchestrator + Discord channel**
- [ ] ASP.NET Core host + BackgroundService + /healthz — **Layer 6, wraps everything**
- [ ] Structured logging + OpenTelemetry — **Layer 6**
- [ ] Dockerfile + AKS manifests (namespace, Deployment, PVC, Secret, ConfigMap) — **Layer 6**
- [ ] Push image to `ribacr123.azurecr.io/life-agent:<sha>` — **Layer 6**

**External blockers**: Discord bot registration, OpenAI key in K8s Secret.

**Deliverable**: Submit tasks via CLI or Discord, get research results and reminders back. Running on AKS.

### Phase 2: Proactivity + Monitoring (2 weeks)

**Goal**: The agent starts doing things without being asked

- [ ] Cron-based scheduling (`IHostedService` + `PeriodicTimer`) — needs Cronos
- [ ] MonitorWorker (price tracking, web change detection) — needs HTTP tools + event store
- [ ] SummaryWorker → morning briefing via Discord DM — needs event store + Discord channel
- [ ] ProactivityEngine + scanner framework — needs event store + user profile
- [ ] DeadlineScanner + BriefingScanner — needs event store
- [ ] Basic user profile + preference learning from Discord reactions — needs event store + Discord channel
- [ ] Quiet hours + notification rate limiting — needs user profile

**No new external blockers.** All Phase 2 components build on Phase 1 infrastructure.

**Deliverable**: Agent sends morning briefings via Discord, monitors prices, alerts on deadlines.

### Phase 3: Interaction + Trust + More Workers (2-3 weeks)

**Goal**: Two-way approval flow; trust escalation; broader capabilities

- [ ] Human-as-tool pattern (Discord buttons/reactions with timeout + fallback) — needs Discord channel
- [ ] Feedback collection via Discord reactions/threads — needs Discord channel + event store
- [ ] Trust escalation/demotion logic per domain — needs feedback events + user profile
- [ ] ScheduleWorker (Google Calendar API) — **needs Google Cloud project + OAuth2 (external blocker)**
- [ ] EmailTriageWorker (Gmail API) — **needs Google Cloud project + OAuth2 (same blocker)**
- [ ] Google OAuth2 token management (refresh tokens in K8s Secret) — needs Google credentials
- [ ] Email digest channel for weekly reports — needs EmailTriageWorker

**External blocker**: Google Cloud project + OAuth2 credentials must be set up before starting ScheduleWorker/EmailTriageWorker. Trust/feedback work has no external blockers and can proceed in parallel.

**Deliverable**: User can approve/reject via Discord; agent adapts to feedback; Google Calendar + Gmail support.

### Phase 4: Intelligence + Polish (3-4 weeks)

**Goal**: Smarter proactivity; production hardening

- [ ] O-Mem-style user model (active profiling, behavioral pattern detection) — needs full event history
- [ ] Tiered LLM routing (cheap triage, expensive reasoning) — needs orchestrator config refactor
- [ ] Cost tracking dashboard (per-task, per-day, per-worker) — needs event store cost data
- [ ] FinanceAdvisorWorker (read-only) — **needs bank API (external, TBD)**
- [ ] Web dashboard (task overview, settings, history, audit trail) — needs ASP.NET host + new UI project
- [ ] SQLite → PostgreSQL migration path — needs schema translation (straightforward for event store)
- [ ] Comprehensive test suite (event replay tests, proactivity simulation) — needs stable APIs
- [ ] AKS production hardening (HPA, resource limits, backup CronJob) — needs stable deployment

**External blocker**: Bank API selection and credentials for FinanceAdvisorWorker. Everything else builds on existing infrastructure.

**Deliverable**: Full life augmentation agent with 6+ workers, adaptive proactivity, cost controls.

### Phase 5: Audio Lifelogging (Timeline TBD)

**Goal**: Always-on conversation capture, transcription, speaker attribution, and knowledge integration

> Research basis: `knowledge-base/audio-lifelogging-research.md` — 17 academic papers, extensive product/community analysis.

- [ ] Acquire Omi pendant dev kit + set up Flutter app (or custom BLE receiver)
- [ ] Deepgram nova-3 streaming integration (WebSocket audio → real-time transcript)
- [ ] Silero VAD integration (on-device pre-filtering to reduce API costs)
- [ ] ECAPA-TDNN speaker embedding model (SpeechBrain, on-device or server-side)
- [ ] Speaker enrollment flow (record sample → generate embedding → store in gallery)
- [ ] Speaker diarization + gallery matching pipeline
- [ ] LLM structuring pipeline (conversation segmentation → summary + entities + action items)
- [ ] Conversational memory store (append-only SQLite table + vector embeddings)
- [ ] Query API ("What did I discuss with Alex on Tuesday?")
- [ ] Auto-create LifeTasks from spoken action items
- [ ] Privacy controls (pause recording, exclude locations/contacts, retention policy)
- [ ] Daily conversation digest in morning briefing (SummaryAgent integration)

**External blockers**: Omi pendant hardware (~$24), Deepgram API key, speaker enrollment UX.

**Cost estimate**: ~$1/day for Deepgram API (heavy talker); $200 free credit covers ~4 months.

**Key risks**:
- iOS background audio mode is unreliable — pendant+BLE architecture mitigates this
- Noisy environments degrade transcription — VAD + close-proximity pendant mic helps
- Speaker diarization accuracy in ambient audio — ECAPA-TDNN (0.8% EER) is state-of-the-art but tested in controlled conditions
- Privacy/legal: recording laws vary by jurisdiction (one-party vs two-party consent)

**Deliverable**: Searchable, speaker-attributed transcript of all conversations. Auto-generated action items. "What did I say about X?" queries answered from conversational memory.

---

## 19. Open Questions

Decisions resolved:
- ~~Hosting~~: **AKS** (`jk-aks-2`, westus2, Standard_D2s_v5×1 node)
- ~~Database~~: **SQLite WAL mode** on Azure Disk PV (`default` StorageClass)
- ~~Channels~~: **CLI + Discord** (Discord.Net Gateway, following `discord-sky-bot` pattern)
- ~~Phase 1-2 scope~~: **Research, Reminders, Monitoring, Morning Briefing**
- ~~Secrets~~: **Kubernetes Secrets** (same pattern as `discord-sky-secrets`)
- ~~Calendar/Email~~: **Google Calendar API + Gmail API** (OAuth2, refresh tokens in K8s Secret)
- ~~Container Registry~~: **`ribacr123.azurecr.io`** (existing ACR)
- ~~Privacy~~: **No restrictions for now** — store what's useful, optimize later
- ~~SQLite on AKS~~: **Azure Disk PV** (`default` SC, `WaitForFirstConsumer`, 1Gi). Single replica, no horizontal scaling. Acceptable — matches existing single-replica workloads on the cluster. Upgrade path: migrate to the existing Postgres instance or a new one.
- ~~Discord setup~~: **Gateway mode** (no public endpoint needed). The existing `discord-sky-bot` uses this pattern successfully with health checks at `/healthz`.

### AKS Cluster Context (from inspection)

```
Cluster:    jk-aks-2 (westus2, K8s 1.25.15, Free tier)
Node:       Standard_D2s_v5 × 1 (2 vCPU, 8 GiB RAM)
Usage:      182m CPU (9%), 5680Mi RAM (106% — swap/overcommit)
Registry:   ribacr123.azurecr.io (Basic SKU)
Ingress:    nginx (ingress-basic namespace) + cert-manager
Existing:   vaultwarden, discord-sky-bot, rib-backend, sendie, postgres
Storage:    default (Azure Disk CSI), azurefile (Azure File CSI)
Secrets:    Native K8s Secrets (Key Vault CSI not enabled)
```

**Resource considerations**: The node is at 106% memory utilization. The life agent should start with modest limits (100m CPU / 256Mi request, 250m CPU / 512Mi limit — matching `discord-sky-bot`). If memory pressure becomes an issue, either scale the node pool or reduce replicas of idle workloads (several `0/0` deployments exist).

Remaining questions:

1. **LLM provider flexibility**: Start with OpenAI only (matching `discord-sky-bot` which uses `gpt-5.4`). Add Anthropic/local model support as a later enhancement. Tiered routing still possible within OpenAI (`gpt-4o-mini` for triage, `gpt-5.4` for reasoning).

2. **Testing strategy**: Event-sourced architecture makes replay testing natural. Should we build a test harness that replays event logs and asserts state projections? How to simulate time for proactivity tests?

3. **Backup & disaster recovery**: Daily CronJob copying SQLite file to Azure Blob? How long to retain event history? Pruning strategy for old events?

4. **Audio lifelogging feasibility**: Phase 5 depends on Omi pendant hardware working reliably with iOS. Key unknowns: BLE audio streaming reliability, Deepgram latency from AKS westus2, speaker diarization accuracy in real ambient conditions. Should we prototype the Deepgram streaming pipeline (without hardware) first to validate the backend architecture?

5. **Conversational memory scale**: At ~4 hours of transcribed speech per day, that's roughly 40K words/day or ~15M words/year. What's the right chunking strategy for vector embeddings? Per-sentence, per-utterance, per-conversation? How does retrieval quality degrade over years of data?

---

*This is a living document. It will be updated as questions are resolved and implementation progresses.*
