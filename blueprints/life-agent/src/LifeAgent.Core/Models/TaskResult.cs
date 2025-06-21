namespace LifeAgent.Core.Models;

/// <summary>
/// Result of a completed task execution by a worker agent.
/// </summary>
public sealed class TaskResult
{
    public required bool Success { get; init; }
    public required string Summary { get; init; }
    public string? DetailedOutput { get; init; }
    public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.UtcNow;
    public decimal LlmCostUsd { get; init; }
}
