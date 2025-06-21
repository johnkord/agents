using LifeAgent.Core;
using LifeAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace LifeAgent.App.Workers;

/// <summary>
/// Worker agent for time-based reminders, follow-ups, and deadline notifications.
/// Handles tasks classified as "reminder" domain.
///
/// Execution is simple — the value is in the orchestrator routing the task here
/// at the right time and the notification channel delivering it effectively.
/// </summary>
public sealed class ReminderWorker : IWorkerAgent
{
    private readonly IChatCompletionService _llm;
    private readonly ILogger<ReminderWorker> _logger;

    public string WorkerType => "reminder";
    public string Description => "Time-based reminders, follow-ups, and deadline tracking";
    public IReadOnlyList<string> SupportedDomains { get; } = ["reminder", "follow-up", "deadline"];

    public ReminderWorker(IChatCompletionService llm, ILogger<ReminderWorker> logger)
    {
        _llm = llm;
        _logger = logger;
    }

    public async Task<TaskResult> ExecuteAsync(LifeTask task, UserProfile userProfile, CancellationToken ct)
    {
        _logger.LogInformation("Reminder worker executing: {Title}", task.Title);

        // For simple reminders, craft a user-friendly notification message
        var message = await _llm.CompleteAsync(
            systemPrompt: """
                You are a helpful personal assistant crafting a reminder notification.
                Keep it concise (1-2 sentences), friendly, and actionable.
                Include the original context and any deadline information.
                Do not use emojis. Do not be overly enthusiastic.
                """,
            userMessage: $"Reminder task: {task.Title}" +
                (task.Description is not null ? $"\nContext: {task.Description}" : "") +
                (task.Deadline.HasValue ? $"\nDeadline: {task.Deadline.Value:f}" : ""),
            tier: ModelTier.Fast,
            temperature: 0.4f,
            maxTokens: 150,
            ct: ct);

        return new TaskResult
        {
            Success = true,
            Summary = message.Trim(),
            DetailedOutput = $"Reminder delivered: {task.Title}",
            CompletedAt = DateTimeOffset.UtcNow,
            LlmCostUsd = 0.0001m, // ~100 tokens on gpt-4o-mini
        };
    }
}
