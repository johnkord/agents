using LifeAgent.Core.Models;

namespace LifeAgent.Core;

/// <summary>
/// Actions emitted by the orchestrator. The runtime executes these —
/// the orchestrator itself never performs side effects directly (P8: separation of intention and execution).
/// </summary>
public abstract record AgentAction
{
    public string ActionId { get; init; } = Guid.NewGuid().ToString("N")[..12];
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Dispatch a task to a worker agent for execution.</summary>
public sealed record DelegateToWorker(string TaskId, string WorkerType) : AgentAction;

/// <summary>Send a notification to the user via the best available channel.</summary>
public sealed record NotifyUser(string Message, NotificationUrgency Urgency, string? TaskId = null) : AgentAction;

/// <summary>Request explicit user approval before proceeding (Human-as-Tool).</summary>
public sealed record RequestHumanApproval(
    string TaskId, string Question, TimeSpan Timeout, string FallbackAction) : AgentAction;

/// <summary>Retry a failed task after a delay.</summary>
public sealed record RetryTask(string TaskId, TimeSpan Delay) : AgentAction;

/// <summary>Create new sub-tasks from a decomposition.</summary>
public sealed record CreateSubTasks(string ParentTaskId, IReadOnlyList<LifeTask> SubTasks) : AgentAction;

/// <summary>Cancel a task.</summary>
public sealed record CancelTask(string TaskId, string Reason) : AgentAction;

/// <summary>Update the user's trust level for a domain.</summary>
public sealed record UpdateTrustLevel(string Domain, TrustLevel NewLevel) : AgentAction;

/// <summary>Pause proactive suggestions temporarily.</summary>
public sealed record ThrottleProactivity(TimeSpan Duration, string Reason) : AgentAction;
