using LifeAgent.Core.Events;
using LifeAgent.Core.Models;

namespace LifeAgent.Core.Memory;

/// <summary>
/// Materialized state projection built from the event log. On startup, the orchestrator
/// replays all events since the last snapshot to rebuild this state (ESAA pattern).
///
/// Thread safety: all mutation goes through <see cref="Apply"/> which should be called
/// from a single writer. Reads from <see cref="GetTask"/>, <see cref="GetQueuedTasks"/>, etc.
/// are safe for concurrent readers.
/// </summary>
public sealed class LifeAgentState
{
    private readonly Dictionary<string, LifeTask> _tasks = new();
    private readonly PriorityQueue<string, TaskPriorityKey> _taskQueue = new();
    private readonly Dictionary<string, DateTimeOffset> _pendingApprovals = new();
    private readonly List<string> _consecutiveDismissals = [];
    private long _lastAppliedSequence;

    public UserProfile UserProfile { get; set; } = new()
    {
        UserId = "default",
        DomainTrust = new()
        {
            ["research"] = TrustLevel.FullAuto,
            ["reminder"] = TrustLevel.NotifyAndAct,
            ["monitoring"] = TrustLevel.FullAuto,
            ["scheduling"] = TrustLevel.AskAndAct,
            ["email"] = TrustLevel.AskAndAct,
            ["finance"] = TrustLevel.NeverAuto,
            ["audio-lifelog"] = TrustLevel.FullAuto,
        },
    };

    public DailyBudget Budget { get; set; } = new();
    public long LastAppliedSequence => _lastAppliedSequence;
    public int ActiveTaskCount => _tasks.Count(t => t.Value.Status is LifeTaskStatus.Queued
        or LifeTaskStatus.Delegated or LifeTaskStatus.WaitingOnHuman);
    public int ConsecutiveDismissals => _consecutiveDismissals.Count;
    public DateTimeOffset LastSnapshotAt { get; set; }

    // ── Query methods ──────────────────────────────────────────

    public LifeTask? GetTask(string taskId) =>
        _tasks.TryGetValue(taskId, out var task) ? task : null;

    public IReadOnlyList<LifeTask> GetActiveTasks() =>
        _tasks.Values
            .Where(t => t.Status is LifeTaskStatus.Queued or LifeTaskStatus.Delegated or LifeTaskStatus.WaitingOnHuman)
            .OrderBy(t => t.Priority)
            .ThenBy(t => t.Deadline ?? DateTimeOffset.MaxValue)
            .ToList();

    public IReadOnlyList<LifeTask> GetQueuedTasks() =>
        _tasks.Values.Where(t => t.Status == LifeTaskStatus.Queued)
            .OrderBy(t => t.Priority)
            .ThenBy(t => t.Deadline ?? DateTimeOffset.MaxValue)
            .ToList();

    public IReadOnlyList<LifeTask> GetTasksByWorker(string workerType) =>
        _tasks.Values.Where(t => t.AssignedWorker == workerType && t.Status == LifeTaskStatus.Delegated).ToList();

    public IReadOnlyList<LifeTask> GetTasksWithDeadlineBefore(DateTimeOffset cutoff) =>
        _tasks.Values
            .Where(t => t.Deadline.HasValue && t.Deadline.Value <= cutoff
                && t.Status is LifeTaskStatus.Queued or LifeTaskStatus.Delegated)
            .OrderBy(t => t.Deadline)
            .ToList();

    public bool HasPendingApproval(string taskId) =>
        _pendingApprovals.ContainsKey(taskId);

    public IReadOnlyDictionary<string, DateTimeOffset> GetPendingApprovals() =>
        _pendingApprovals;

    /// <summary>
    /// Gets per-worker success rate over the last N completed tasks.
    /// Used for the 80% rule — auto-disable workers below 90% success.
    /// </summary>
    public float GetWorkerSuccessRate(string workerType, int windowSize = 10)
    {
        var workerTasks = _tasks.Values
            .Where(t => t.AssignedWorker == workerType
                && t.Status is LifeTaskStatus.Completed or LifeTaskStatus.Failed)
            .OrderByDescending(t => t.CompletedAt ?? t.CreatedAt)
            .Take(windowSize)
            .ToList();

        if (workerTasks.Count == 0) return 1.0f; // No history = trust
        return (float)workerTasks.Count(t => t.Status == LifeTaskStatus.Completed) / workerTasks.Count;
    }

    // ── Event application (state machine) ──────────────────────

    public void Apply(LifeEvent evt, long sequence)
    {
        switch (evt)
        {
            case TaskCreated e:
                _tasks[e.Task.Id] = e.Task;
                _taskQueue.Enqueue(e.Task.Id, new TaskPriorityKey(e.Task.Priority, e.Task.Deadline ?? DateTimeOffset.MaxValue));
                break;

            case TaskDelegated e:
                if (_tasks.TryGetValue(e.TaskId, out var delegatedTask))
                {
                    delegatedTask.Status = LifeTaskStatus.Delegated;
                    delegatedTask.AssignedWorker = e.WorkerType;
                }
                break;

            case TaskCompleted e:
                if (_tasks.TryGetValue(e.TaskId, out var completedTask))
                {
                    completedTask.Status = LifeTaskStatus.Completed;
                    completedTask.Result = e.Result;
                    completedTask.CompletedAt = e.Result.CompletedAt;
                    Budget.RecordCost(e.Result.LlmCostUsd);
                }
                break;

            case TaskFailed e:
                if (_tasks.TryGetValue(e.TaskId, out var failedTask))
                {
                    failedTask.RetryCount = e.RetryCount;
                    if (e.RetryCount >= failedTask.MaxRetries)
                        failedTask.Status = LifeTaskStatus.Failed;
                    else
                        failedTask.Status = LifeTaskStatus.Queued; // re-queue for retry
                    Budget.RecordFailure();
                }
                break;

            case TaskCancelled e:
                if (_tasks.TryGetValue(e.TaskId, out var cancelledTask))
                    cancelledTask.Status = LifeTaskStatus.Cancelled;
                break;

            case HumanApprovalRequested e:
                if (_tasks.TryGetValue(e.TaskId, out var awaitingTask))
                    awaitingTask.Status = LifeTaskStatus.WaitingOnHuman;
                _pendingApprovals[e.TaskId] = evt.Timestamp;
                break;

            case HumanApprovalReceived e:
                _pendingApprovals.Remove(e.TaskId);
                if (_tasks.TryGetValue(e.TaskId, out var approvedTask))
                {
                    if (e.Approved)
                        approvedTask.Status = LifeTaskStatus.Queued;
                    else
                        approvedTask.Status = LifeTaskStatus.Cancelled;
                }
                break;

            case HumanApprovalTimedOut e:
                _pendingApprovals.Remove(e.TaskId);
                break;

            case UserFeedbackReceived e:
                ApplyFeedback(e);
                break;

            case ProactiveSuggestionDismissed e:
                _consecutiveDismissals.Add(e.TaskId);
                break;

            case ProactiveSuggestionSent:
                // Reset dismissal streak on any accepted suggestion
                break;

            case BudgetThresholdReached:
                // Budget state is tracked via DailyBudget property
                break;

            case SpokenCommitmentDetected e:
                // Auto-create task from spoken commitment
                if (UserProfile.AudioLifelog.AutoCreateTasksFromCommitments)
                {
                    var commitmentTask = new LifeTask
                    {
                        Id = $"spoken-{e.SegmentId}",
                        Title = e.CommitmentText,
                        Description = $"Spoken commitment by {e.SpeakerName ?? "unknown"}" +
                            (e.ParsedDeadline.HasValue ? $" (deadline: {e.ParsedDeadline.Value:d})" : ""),
                        Origin = TaskOrigin.AudioLifelog,
                        Priority = e.ParsedDeadline.HasValue ? TaskPriority.Medium : TaskPriority.Low,
                        RequiredTrust = TrustLevel.NotifyAndAct,
                        Deadline = e.ParsedDeadline,
                        Tags = ["commitment", "audio-lifelog"],
                    };
                    _tasks[commitmentTask.Id] = commitmentTask;
                }
                break;
        }

        _lastAppliedSequence = sequence;

        // Daily budget reset
        if (Budget.NeedsReset())
            Budget = new DailyBudget { LimitUsd = Budget.LimitUsd };
    }

    private void ApplyFeedback(UserFeedbackReceived feedback)
    {
        UserProfile.LastInteraction = feedback.Timestamp;

        switch (feedback.Type)
        {
            case FeedbackType.Helpful:
                _consecutiveDismissals.Clear();
                break;
            case FeedbackType.TooFrequent:
                // Reduce proactivity level by 10%
                UserProfile.Proactivity.ProactivityLevel =
                    Math.Max(0f, UserProfile.Proactivity.ProactivityLevel - 0.1f);
                break;
            case FeedbackType.NotHelpful:
            case FeedbackType.WrongContent:
                _consecutiveDismissals.Add(feedback.TaskId);
                break;
        }

        // Trust demotion: 3 consecutive dismissals in same domain → demote
        if (_consecutiveDismissals.Count >= 3)
        {
            var task = GetTask(feedback.TaskId);
            if (task?.AssignedWorker is not null &&
                UserProfile.DomainTrust.TryGetValue(task.AssignedWorker, out var currentTrust))
            {
                var demoted = currentTrust switch
                {
                    TrustLevel.FullAuto => TrustLevel.NotifyAndAct,
                    TrustLevel.NotifyAndAct => TrustLevel.AskAndAct,
                    _ => currentTrust,
                };
                if (demoted != currentTrust)
                    UserProfile.DomainTrust[task.AssignedWorker] = demoted;
            }
            _consecutiveDismissals.Clear();
        }
    }

    /// <summary>
    /// Purge completed/cancelled/failed tasks older than the given age
    /// to prevent unbounded state growth. Active tasks are never purged.
    /// </summary>
    public int PurgeOldTasks(TimeSpan maxAge)
    {
        var cutoff = DateTimeOffset.UtcNow - maxAge;
        var toRemove = _tasks
            .Where(kv => kv.Value.Status is LifeTaskStatus.Completed or LifeTaskStatus.Failed or LifeTaskStatus.Cancelled
                && (kv.Value.CompletedAt ?? kv.Value.CreatedAt) < cutoff)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var id in toRemove)
            _tasks.Remove(id);

        return toRemove.Count;
    }
}

/// <summary>
/// Composite key for priority queue ordering.
/// Lower priority value (Critical=0) sorts first; ties broken by deadline proximity.
/// </summary>
internal readonly record struct TaskPriorityKey(TaskPriority Priority, DateTimeOffset Deadline)
    : IComparable<TaskPriorityKey>
{
    public int CompareTo(TaskPriorityKey other)
    {
        var priorityCompare = Priority.CompareTo(other.Priority);
        return priorityCompare != 0 ? priorityCompare : Deadline.CompareTo(other.Deadline);
    }
}
