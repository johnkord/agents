using LifeAgent.Core;
using LifeAgent.Core.Memory;
using LifeAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace LifeAgent.App.Workers;

/// <summary>
/// Compiles a personalized daily briefing from the agent's state:
///   - Active tasks and approaching deadlines
///   - Yesterday's completed work
///   - Budget usage
///   - Conversation highlights (if audio lifelogging is enabled)
///   - Pending approvals
///
/// Triggered by the scheduler's "morning-briefing" trigger.
/// Uses ModelTier.Standard for synthesis to produce a natural, readable briefing.
/// </summary>
public sealed class DailyBriefingWorker : IWorkerAgent
{
    private readonly IChatCompletionService _llm;
    private readonly LifeAgentState _state;
    private readonly IConversationalMemory? _conversationalMemory;
    private readonly ILogger<DailyBriefingWorker> _logger;

    public string WorkerType => "daily-briefing";
    public string Description => "Compiles personalized morning briefings from agent state";
    public IReadOnlyList<string> SupportedDomains { get; } = ["daily-briefing", "summary", "briefing"];

    public DailyBriefingWorker(
        IChatCompletionService llm,
        LifeAgentState state,
        ILogger<DailyBriefingWorker> logger,
        IConversationalMemory? conversationalMemory = null)
    {
        _llm = llm;
        _state = state;
        _conversationalMemory = conversationalMemory;
        _logger = logger;
    }

    public async Task<TaskResult> ExecuteAsync(LifeTask task, UserProfile userProfile, CancellationToken ct)
    {
        _logger.LogInformation("Compiling daily briefing...");

        var rawData = await GatherBriefingDataAsync(userProfile, ct);

        var briefing = await _llm.CompleteAsync(
            systemPrompt: """
                You are a personal assistant compiling a concise morning briefing.
                Organize the information clearly with short sections. Use this structure:
                
                TODAY'S PRIORITIES — bullet list of the most important items
                APPROACHING DEADLINES — anything due within 3 days
                YESTERDAY'S PROGRESS — what was completed
                OPEN ITEMS — tasks that need attention
                BUDGET — daily spend status (only if notable)
                CONVERSATIONS — interesting highlights from audio (only if data present)
                
                Skip any section that has no content. Be concise — aim for under 300 words.
                Do not use emojis. Use plain text formatting with dashes for bullets.
                Be direct and practical, not cheerful.
                """,
            userMessage: rawData,
            tier: ModelTier.Standard,
            temperature: 0.3f,
            maxTokens: 800,
            ct: ct);

        return new TaskResult
        {
            Success = true,
            Summary = briefing.Trim(),
            DetailedOutput = briefing.Trim(),
            CompletedAt = DateTimeOffset.UtcNow,
            LlmCostUsd = 0.003m,
        };
    }

    private async Task<string> GatherBriefingDataAsync(UserProfile userProfile, CancellationToken ct)
    {
        var parts = new List<string>();
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.DateTime);

        // Active tasks
        var activeTasks = _state.GetActiveTasks();
        if (activeTasks.Count > 0)
        {
            parts.Add("ACTIVE TASKS:");
            foreach (var t in activeTasks.Take(15))
            {
                var deadline = t.Deadline.HasValue ? $" [due: {t.Deadline.Value:MMM dd}]" : "";
                var worker = t.AssignedWorker is not null ? $" ({t.AssignedWorker})" : "";
                parts.Add($"  - [{t.Status}] {t.Title}{worker}{deadline}");
            }
        }

        // Approaching deadlines (next 3 days)
        var deadlineCutoff = now.AddDays(3);
        var upcoming = _state.GetTasksWithDeadlineBefore(deadlineCutoff);
        if (upcoming.Count > 0)
        {
            parts.Add("\nAPPROACHING DEADLINES:");
            foreach (var t in upcoming)
            {
                var daysLeft = (t.Deadline!.Value - now).TotalDays;
                var urgency = daysLeft < 1 ? "TODAY" : daysLeft < 2 ? "TOMORROW" : $"{daysLeft:F0} days";
                parts.Add($"  - {t.Title} — {urgency}");
            }
        }

        // Pending approvals
        var approvals = _state.GetPendingApprovals();
        if (approvals.Count > 0)
        {
            parts.Add($"\nPENDING APPROVALS: {approvals.Count} task(s) waiting for your response");
        }

        // Budget
        var budget = _state.Budget;
        parts.Add($"\nBUDGET: ${budget.SpentUsd:F2} / ${budget.LimitUsd:F2} spent today " +
            $"({budget.TasksExecuted} tasks executed, {budget.TasksFailed} failed)");

        // Conversation stats (if audio lifelogging available)
        if (_conversationalMemory is not null && userProfile.AudioLifelog.IncludeInDailyBriefing)
        {
            try
            {
                var yesterday = today.AddDays(-1);
                var stats = await _conversationalMemory.GetDailyStatsAsync(yesterday, ct);
                if (stats.ConversationCount > 0)
                {
                    parts.Add($"\nYESTERDAY'S CONVERSATIONS: {stats.ConversationCount} " +
                        $"({stats.TotalDuration.TotalMinutes:F0} min)");
                    if (stats.TopTopics.Count > 0)
                        parts.Add($"  Topics: {string.Join(", ", stats.TopTopics.Take(5))}");
                    if (stats.TopSpeakers.Count > 0)
                        parts.Add($"  Speakers: {string.Join(", ", stats.TopSpeakers.Take(5))}");
                    if (stats.UnresolvedActionItems > 0)
                        parts.Add($"  Unresolved action items: {stats.UnresolvedActionItems}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch conversation stats for briefing");
            }
        }

        return string.Join("\n", parts);
    }
}
