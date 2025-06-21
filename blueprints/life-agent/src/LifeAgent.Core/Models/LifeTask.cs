namespace LifeAgent.Core.Models;

/// <summary>
/// Fundamental unit of work in the Life Agent system.
/// Every user request, proactive suggestion, scheduled trigger, and webhook
/// becomes a LifeTask that flows through the orchestrator → worker pipeline.
/// </summary>
public sealed class LifeTask
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public TaskOrigin Origin { get; init; }
    public TaskPriority Priority { get; set; }
    public LifeTaskStatus Status { get; set; }
    public string? AssignedWorker { get; set; }
    public TrustLevel RequiredTrust { get; init; }
    public DateTimeOffset? Deadline { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; init; } = 3;
    public string? ParentTaskId { get; init; }
    public TaskResult? Result { get; set; }

    /// <summary>
    /// Optional tags for categorization and search (e.g., "audio-lifelog", "spoken-commitment").
    /// </summary>
    public HashSet<string> Tags { get; init; } = [];
}

public enum TaskOrigin { User, Proactive, Scheduled, Webhook, Continuation, AudioLifelog }
public enum TaskPriority { Critical, High, Medium, Low }
public enum LifeTaskStatus { Queued, Delegated, WaitingOnHuman, Completed, Failed, Cancelled }
public enum TrustLevel { FullAuto, NotifyAndAct, AskAndAct, NeverAuto }
