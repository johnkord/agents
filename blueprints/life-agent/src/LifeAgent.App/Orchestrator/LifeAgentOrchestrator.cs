using LifeAgent.Core;
using LifeAgent.Core.Events;
using LifeAgent.Core.Memory;
using LifeAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace LifeAgent.App.Orchestrator;

/// <summary>
/// The brain of the Life Agent. A stateless function that takes current state + event
/// and produces actions and new events: f(state, event) → (actions[], events[]).
///
/// Design principles enforced:
///   P1 — Stateless: reads from LifeAgentState, never mutates it directly
///   P4 — Human-as-Tool: routes through approval when trust demands it
///   P6 — Cost accounting: refuses work when budget exhausted
///   P7 — Circuit breakers: disables workers below 80% success rate
///   P8 — Separation of intention/execution: emits AgentAction, doesn't execute
///   P9 — False-positive asymmetry: defaults to asking when uncertain
///   P10 — The 80% rule: imperfect fast > perfect never
/// </summary>
public sealed class LifeAgentOrchestrator
{
    private readonly TaskClassifier _classifier;
    private readonly IReadOnlyDictionary<string, IWorkerAgent> _workers;
    private readonly ILogger<LifeAgentOrchestrator> _logger;

    private const float WorkerSuccessThreshold = 0.8f;
    private const int MaxConsecutiveDismissalsBeforeThrottle = 5;

    public LifeAgentOrchestrator(
        TaskClassifier classifier,
        IEnumerable<IWorkerAgent> workers,
        ILogger<LifeAgentOrchestrator> logger)
    {
        _classifier = classifier;
        _workers = workers.ToDictionary(w => w.WorkerType, w => w);
        _logger = logger;
    }

    /// <summary>
    /// Core orchestration function. Given current state and an incoming event,
    /// returns a set of actions to execute and new events to persist.
    /// </summary>
    public async Task<OrchestratorDecision> ProcessAsync(
        LifeAgentState state, LifeEvent evt, CancellationToken ct)
    {
        var actions = new List<AgentAction>();
        var events = new List<LifeEvent>();

        switch (evt)
        {
            case TaskCreated e:
                await HandleTaskCreated(state, e, actions, events, ct);
                break;

            case TaskCompleted e:
                HandleTaskCompleted(state, e, actions, events);
                break;

            case TaskFailed e:
                HandleTaskFailed(state, e, actions, events);
                break;

            case HumanApprovalReceived e:
                HandleApprovalReceived(state, e, actions, events);
                break;

            case HumanApprovalTimedOut e:
                HandleApprovalTimeout(state, e, actions, events);
                break;

            case ScheduledTriggerFired e:
                HandleScheduledTrigger(state, e, actions, events);
                break;

            case SpokenCommitmentDetected e:
                HandleSpokenCommitment(state, e, actions, events);
                break;

            case ProactiveOpportunityDetected e:
                HandleProactiveOpportunity(state, e, actions, events);
                break;

            case UserFeedbackReceived e:
                HandleUserFeedback(state, e, actions, events);
                break;

            default:
                _logger.LogDebug("Event {Type} handled by state projection only", evt.GetType().Name);
                break;
        }

        return new OrchestratorDecision(actions, events);
    }

    // ── Event handlers ─────────────────────────────────────────

    private async Task HandleTaskCreated(
        LifeAgentState state, TaskCreated evt,
        List<AgentAction> actions, List<LifeEvent> events,
        CancellationToken ct)
    {
        var task = evt.Task;

        // Budget gate (P6)
        if (state.Budget.IsExhausted)
        {
            _logger.LogWarning("Budget exhausted, cancelling task {TaskId}", task.Id);
            actions.Add(new CancelTask(task.Id, "Daily budget exhausted"));
            events.Add(new TaskCancelled(task.Id, "Daily budget exhausted"));
            actions.Add(new NotifyUser(
                $"Task '{task.Title}' cancelled — daily budget of ${state.Budget.LimitUsd:F2} exhausted. " +
                $"Spent: ${state.Budget.SpentUsd:F2}.",
                NotificationUrgency.High, task.Id));
            return;
        }

        // Classify the task (LLM call for user-originated, skip for pre-classified)
        TaskClassification classification;
        if (task.AssignedWorker is not null)
        {
            // Pre-routed (e.g., from scheduler or spoken commitment)
            classification = new TaskClassification
            {
                WorkerType = task.AssignedWorker,
                Domain = task.AssignedWorker,
                Priority = task.Priority,
                RequiredTrust = task.RequiredTrust,
                NeedsDecomposition = false,
                SuggestedSubTasks = [],
                Reasoning = "Pre-routed task",
            };
        }
        else
        {
            classification = await _classifier.ClassifyAsync(task, ct);
        }

        // Apply classification to task
        task.Priority = classification.Priority;

        // Decomposition check
        if (classification.NeedsDecomposition && classification.SuggestedSubTasks.Count > 0)
        {
            var subTasks = classification.SuggestedSubTasks.Select((title, i) => new LifeTask
            {
                Id = $"{task.Id}-sub{i}",
                Title = title,
                Origin = TaskOrigin.Continuation,
                Priority = classification.Priority,
                RequiredTrust = classification.RequiredTrust,
                ParentTaskId = task.Id,
            }).ToList();

            actions.Add(new CreateSubTasks(task.Id, subTasks));
            foreach (var sub in subTasks)
                events.Add(new TaskCreated(sub));

            _logger.LogInformation("Decomposed {TaskId} into {Count} sub-tasks", task.Id, subTasks.Count);
            return;
        }

        // Worker availability check (P7: circuit breaker)
        if (!_workers.ContainsKey(classification.WorkerType))
        {
            _logger.LogWarning("No worker registered for type {WorkerType}, routing to general",
                classification.WorkerType);
            classification = new TaskClassification
            {
                WorkerType = "general",
                Domain = classification.Domain,
                Priority = classification.Priority,
                RequiredTrust = classification.RequiredTrust,
                NeedsDecomposition = classification.NeedsDecomposition,
                SuggestedSubTasks = classification.SuggestedSubTasks,
                Reasoning = classification.Reasoning,
            };

            if (!_workers.ContainsKey("general"))
            {
                actions.Add(new NotifyUser(
                    $"Cannot process task '{task.Title}' — no suitable worker available.",
                    NotificationUrgency.Medium, task.Id));
                events.Add(new TaskFailed(task.Id, $"No worker for type '{classification.WorkerType}'", 0));
                return;
            }
        }

        // Worker health check (80% success rate rule)
        var successRate = state.GetWorkerSuccessRate(classification.WorkerType);
        if (successRate < WorkerSuccessThreshold)
        {
            _logger.LogWarning("Worker {Worker} below success threshold ({Rate:P0} < {Threshold:P0})",
                classification.WorkerType, successRate, WorkerSuccessThreshold);
            actions.Add(new NotifyUser(
                $"Worker '{classification.WorkerType}' has been unreliable ({successRate:P0} success rate). " +
                $"Task '{task.Title}' requires your approval.",
                NotificationUrgency.High, task.Id));
            events.Add(new HumanApprovalRequested(
                task.Id,
                $"Worker '{classification.WorkerType}' has a {successRate:P0} success rate. Proceed anyway?",
                TimeSpan.FromMinutes(30),
                "cancel"));
            return;
        }

        // Trust-based routing (P4: Human-as-Tool)
        var userTrust = state.UserProfile.DomainTrust.GetValueOrDefault(
            classification.Domain, TrustLevel.AskAndAct);
        var effectiveTrust = (TrustLevel)Math.Max((int)userTrust, (int)classification.RequiredTrust);

        switch (effectiveTrust)
        {
            case TrustLevel.FullAuto:
                // Execute immediately
                actions.Add(new DelegateToWorker(task.Id, classification.WorkerType));
                events.Add(new TaskDelegated(task.Id, classification.WorkerType));
                break;

            case TrustLevel.NotifyAndAct:
                // Execute and notify
                actions.Add(new DelegateToWorker(task.Id, classification.WorkerType));
                events.Add(new TaskDelegated(task.Id, classification.WorkerType));
                actions.Add(new NotifyUser(
                    $"Working on: {task.Title} (via {classification.WorkerType})",
                    NotificationUrgency.Low, task.Id));
                break;

            case TrustLevel.AskAndAct:
                // Ask before executing
                events.Add(new HumanApprovalRequested(
                    task.Id,
                    $"Shall I proceed with '{task.Title}'? " +
                    $"Worker: {classification.WorkerType}. " +
                    $"Reasoning: {classification.Reasoning}",
                    TimeSpan.FromHours(4),
                    "cancel"));
                actions.Add(new RequestHumanApproval(
                    task.Id,
                    $"Shall I proceed with '{task.Title}'? (Worker: {classification.WorkerType})",
                    TimeSpan.FromHours(4),
                    "cancel"));
                break;

            case TrustLevel.NeverAuto:
                actions.Add(new NotifyUser(
                    $"Task '{task.Title}' is in a domain ({classification.Domain}) that requires " +
                    "manual handling. I've logged it but won't take action.",
                    NotificationUrgency.Medium, task.Id));
                events.Add(new TaskCancelled(task.Id, "NeverAuto trust level"));
                break;
        }
    }

    private void HandleTaskCompleted(
        LifeAgentState state, TaskCompleted evt,
        List<AgentAction> actions, List<LifeEvent> events)
    {
        var task = state.GetTask(evt.TaskId);
        if (task is null) return;

        // Notify user of completion
        var costInfo = evt.Result.LlmCostUsd > 0
            ? $" (cost: ${evt.Result.LlmCostUsd:F4})"
            : "";

        actions.Add(new NotifyUser(
            $"Completed: {task.Title}\n{evt.Result.Summary}{costInfo}",
            NotificationUrgency.Low, evt.TaskId));

        _logger.LogInformation("Task {TaskId} completed by {Worker}: {Summary}",
            evt.TaskId, task.AssignedWorker, evt.Result.Summary);
    }

    private void HandleTaskFailed(
        LifeAgentState state, TaskFailed evt,
        List<AgentAction> actions, List<LifeEvent> events)
    {
        var task = state.GetTask(evt.TaskId);
        if (task is null) return;

        if (evt.RetryCount < task.MaxRetries)
        {
            // Exponential backoff: 30s, 90s, 270s
            var delay = TimeSpan.FromSeconds(30 * Math.Pow(3, evt.RetryCount));
            actions.Add(new RetryTask(evt.TaskId, delay));
            _logger.LogWarning("Task {TaskId} failed (attempt {Attempt}/{Max}), retrying in {Delay}s",
                evt.TaskId, evt.RetryCount + 1, task.MaxRetries, delay.TotalSeconds);
        }
        else
        {
            actions.Add(new NotifyUser(
                $"Task '{task.Title}' failed after {task.MaxRetries} attempts: {evt.Reason}",
                NotificationUrgency.High, evt.TaskId));
            _logger.LogError("Task {TaskId} permanently failed: {Reason}", evt.TaskId, evt.Reason);
        }
    }

    private void HandleApprovalReceived(
        LifeAgentState state, HumanApprovalReceived evt,
        List<AgentAction> actions, List<LifeEvent> events)
    {
        var task = state.GetTask(evt.TaskId);
        if (task is null) return;

        if (evt.Approved)
        {
            var workerType = task.AssignedWorker ?? "general";
            actions.Add(new DelegateToWorker(evt.TaskId, workerType));
            events.Add(new TaskDelegated(evt.TaskId, workerType));
            _logger.LogInformation("Task {TaskId} approved by user, delegating to {Worker}",
                evt.TaskId, workerType);
        }
        else
        {
            events.Add(new TaskCancelled(evt.TaskId, evt.UserComment ?? "Rejected by user"));
            actions.Add(new NotifyUser(
                $"Task '{task.Title}' cancelled.",
                NotificationUrgency.Low, evt.TaskId));
        }
    }

    private void HandleApprovalTimeout(
        LifeAgentState state, HumanApprovalTimedOut evt,
        List<AgentAction> actions, List<LifeEvent> events)
    {
        switch (evt.FallbackAction.ToLowerInvariant())
        {
            case "cancel":
                events.Add(new TaskCancelled(evt.TaskId, "Approval timed out"));
                break;
            case "proceed":
                var task = state.GetTask(evt.TaskId);
                if (task?.AssignedWorker is not null)
                {
                    actions.Add(new DelegateToWorker(evt.TaskId, task.AssignedWorker));
                    events.Add(new TaskDelegated(evt.TaskId, task.AssignedWorker));
                }
                break;
            default:
                events.Add(new TaskCancelled(evt.TaskId, $"Approval timed out (fallback: {evt.FallbackAction})"));
                break;
        }
    }

    private void HandleScheduledTrigger(
        LifeAgentState state, ScheduledTriggerFired evt,
        List<AgentAction> actions, List<LifeEvent> events)
    {
        // Map trigger IDs to properly populated, pre-routed tasks
        var (title, description, worker, priority) = evt.TriggerId switch
        {
            "morning-briefing" => (
                "Daily briefing",
                "Compile today's priorities, approaching deadlines, yesterday's progress, and conversation highlights",
                "daily-briefing",
                TaskPriority.Medium),
            "deadline-check" => (
                "Deadline proximity check",
                "Scan for tasks with approaching deadlines and alert if any are at risk",
                (string?)null, // Handled by ProactivityScanner, not a worker task
                TaskPriority.Low),
            "proactivity-scan" => (
                "Proactive opportunity scan",
                "Scan for proactive opportunities across all domains",
                (string?)null,
                TaskPriority.Low),
            _ => (
                $"Scheduled: {evt.TriggerId}",
                (string?)null,
                (string?)null,
                TaskPriority.Medium),
        };

        // deadline-check and proactivity-scan are handled by ProactivityScanner directly
        if (worker is null)
        {
            _logger.LogDebug("Scheduled trigger {TriggerId} handled by background scanner, no task created",
                evt.TriggerId);
            return;
        }

        var task = new LifeTask
        {
            Id = $"sched-{evt.TriggerId}-{DateTimeOffset.UtcNow:yyyyMMddHHmm}",
            Title = title,
            Description = description,
            Origin = TaskOrigin.Scheduled,
            Priority = priority,
            RequiredTrust = TrustLevel.FullAuto, // Scheduled = pre-approved
            AssignedWorker = worker, // Pre-routed, skip classification
            Tags = ["scheduled", evt.TriggerId],
        };

        events.Add(new TaskCreated(task));
    }

    /// <summary>
    /// Handle spoken commitments detected by the audio pipeline.
    /// The state projection already created the task — we just need to notify the user
    /// so they're aware the agent picked something up.
    /// </summary>
    private void HandleSpokenCommitment(
        LifeAgentState state, SpokenCommitmentDetected evt,
        List<AgentAction> actions, List<LifeEvent> events)
    {
        if (!state.UserProfile.AudioLifelog.AutoCreateTasksFromCommitments)
            return;

        var taskId = $"spoken-{evt.SegmentId}";
        var task = state.GetTask(taskId);
        if (task is null) return; // State projection didn't create it

        var deadlineInfo = evt.ParsedDeadline.HasValue
            ? $" (deadline: {evt.ParsedDeadline.Value:MMM dd})"
            : "";

        actions.Add(new NotifyUser(
            $"Heard commitment: \"{evt.CommitmentText}\"{deadlineInfo} — task created",
            NotificationUrgency.Low, taskId));

        _logger.LogInformation("Spoken commitment → task {TaskId}: {Text}",
            taskId, evt.CommitmentText);
    }

    /// <summary>
    /// Handle proactive opportunities detected by the ProactivityScanner.
    /// Notify the user about the opportunity.
    /// </summary>
    private void HandleProactiveOpportunity(
        LifeAgentState state, ProactiveOpportunityDetected evt,
        List<AgentAction> actions, List<LifeEvent> events)
    {
        var urgency = evt.Confidence switch
        {
            >= 0.9f => NotificationUrgency.High,
            >= 0.7f => NotificationUrgency.Medium,
            _ => NotificationUrgency.Low,
        };

        actions.Add(new NotifyUser(evt.Description, urgency));
    }

    private void HandleUserFeedback(
        LifeAgentState state, UserFeedbackReceived evt,
        List<AgentAction> actions, List<LifeEvent> events)
    {
        // State projection handles trust adjustments.
        // Orchestrator handles notification throttling.
        if (state.ConsecutiveDismissals >= MaxConsecutiveDismissalsBeforeThrottle)
        {
            actions.Add(new ThrottleProactivity(
                TimeSpan.FromHours(1),
                $"{MaxConsecutiveDismissalsBeforeThrottle} consecutive dismissals"));
            actions.Add(new NotifyUser(
                "I've noticed you've been dismissing my suggestions. " +
                "I'll reduce notifications for the next hour.",
                NotificationUrgency.Low));
        }
    }
}

/// <summary>
/// Immutable result of a single orchestration cycle.
/// </summary>
public sealed record OrchestratorDecision(
    IReadOnlyList<AgentAction> Actions,
    IReadOnlyList<LifeEvent> Events);
