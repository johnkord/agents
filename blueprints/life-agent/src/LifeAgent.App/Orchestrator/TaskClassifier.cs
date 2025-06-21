using System.Text.Json.Serialization;
using LifeAgent.Core;
using LifeAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace LifeAgent.App.Orchestrator;

/// <summary>
/// LLM-based task classifier. Analyzes natural language task descriptions
/// to determine domain, priority, required worker, and whether decomposition is needed.
/// Uses ModelTier.Fast for cheap/fast classification (~100 tokens out).
/// </summary>
public sealed class TaskClassifier
{
    private readonly IChatCompletionService _llm;
    private readonly ILogger<TaskClassifier> _logger;

    private const string ClassificationSystemPrompt = """
        You are a task classifier for a personal life management agent.
        Given a task title and optional description, classify it into exactly one JSON object.

        Available worker types:
        - "reminder" — time-based notifications, follow-ups, deadline tracking
        - "research" — web research, information gathering, summarization
        - "audio-lifelog" — conversation recall, daily audio digest, speaker enrollment
        - "scheduling" — calendar management, meeting coordination
        - "email" — email drafting, summarization, triage
        - "monitoring" — price tracking, status checks, recurring observation
        - "general" — anything that doesn't fit the above

        Priority levels (choose based on urgency and impact):
        - "Critical" — immediate action required, significant consequences if delayed
        - "High" — time-sensitive, should be handled within hours
        - "Medium" — normal importance, within a day
        - "Low" — nice-to-have, no time pressure

        Trust assessment:
        - "FullAuto" — safe to execute without user confirmation (low risk, reversible)
        - "NotifyAndAct" — execute and notify user of result
        - "AskAndAct" — ask user before executing (high impact, irreversible, or ambiguous)
        - "NeverAuto" — always require explicit approval (financial, legal, personal decisions)

        If the task is complex and should be broken into sub-tasks, set needsDecomposition to true
        and provide suggestedSubTasks as an array of short task titles.

        Respond with ONLY a JSON object matching this schema:
        {
          "workerType": "string",
          "domain": "string",
          "priority": "Critical|High|Medium|Low",
          "requiredTrust": "FullAuto|NotifyAndAct|AskAndAct|NeverAuto",
          "needsDecomposition": false,
          "suggestedSubTasks": [],
          "reasoning": "one sentence explaining classification"
        }
        """;

    public TaskClassifier(IChatCompletionService llm, ILogger<TaskClassifier> logger)
    {
        _llm = llm;
        _logger = logger;
    }

    public async Task<TaskClassification> ClassifyAsync(LifeTask task, CancellationToken ct)
    {
        var userMessage = $"Task: {task.Title}";
        if (!string.IsNullOrWhiteSpace(task.Description))
            userMessage += $"\nDescription: {task.Description}";
        if (task.Deadline.HasValue)
            userMessage += $"\nDeadline: {task.Deadline.Value:yyyy-MM-dd HH:mm}";
        if (task.Tags.Count > 0)
            userMessage += $"\nTags: {string.Join(", ", task.Tags)}";

        var result = await _llm.CompleteJsonAsync<TaskClassificationResponse>(
            ClassificationSystemPrompt, userMessage, ModelTier.Fast, ct);

        if (result is null)
        {
            _logger.LogWarning("Task classification failed for {TaskId}, using fallback", task.Id);
            return FallbackClassification(task);
        }

        _logger.LogDebug("Classified {TaskId} → {Worker} ({Priority}, trust={Trust}): {Reason}",
            task.Id, result.WorkerType, result.Priority, result.RequiredTrust, result.Reasoning);

        return new TaskClassification
        {
            WorkerType = result.WorkerType ?? "general",
            Domain = result.Domain ?? result.WorkerType ?? "general",
            Priority = ParsePriority(result.Priority),
            RequiredTrust = ParseTrust(result.RequiredTrust),
            NeedsDecomposition = result.NeedsDecomposition,
            SuggestedSubTasks = result.SuggestedSubTasks ?? [],
            Reasoning = result.Reasoning ?? "",
        };
    }

    /// <summary>
    /// Rule-based fallback when LLM classification fails. Uses keyword matching
    /// to provide a reasonable classification without an API call.
    /// </summary>
    private static TaskClassification FallbackClassification(LifeTask task)
    {
        var text = $"{task.Title} {task.Description}".ToLowerInvariant();

        var workerType = text switch
        {
            _ when text.Contains("remind") || text.Contains("follow up") || text.Contains("deadline") => "reminder",
            _ when text.Contains("research") || text.Contains("look up") || text.Contains("find out") => "research",
            _ when text.Contains("conversation") || text.Contains("what did") || text.Contains("audio") => "audio-lifelog",
            _ when text.Contains("schedule") || text.Contains("meeting") || text.Contains("calendar") => "scheduling",
            _ when text.Contains("email") || text.Contains("mail") || text.Contains("send to") => "email",
            _ when text.Contains("track") || text.Contains("monitor") || text.Contains("watch") => "monitoring",
            _ => "general",
        };

        return new TaskClassification
        {
            WorkerType = workerType,
            Domain = workerType,
            Priority = task.Priority,
            RequiredTrust = task.RequiredTrust,
            NeedsDecomposition = false,
            SuggestedSubTasks = [],
            Reasoning = "Fallback classification (LLM unavailable)",
        };
    }

    private static TaskPriority ParsePriority(string? s) => s?.ToLowerInvariant() switch
    {
        "critical" => TaskPriority.Critical,
        "high" => TaskPriority.High,
        "medium" => TaskPriority.Medium,
        "low" => TaskPriority.Low,
        _ => TaskPriority.Medium,
    };

    private static TrustLevel ParseTrust(string? s) => s?.ToLowerInvariant() switch
    {
        "fullauto" => TrustLevel.FullAuto,
        "notifyandact" => TrustLevel.NotifyAndAct,
        "askandact" => TrustLevel.AskAndAct,
        "neverauto" => TrustLevel.NeverAuto,
        _ => TrustLevel.AskAndAct, // Default to requiring approval (P9: false-positive asymmetry)
    };
}

/// <summary>Structured classification result.</summary>
public sealed class TaskClassification
{
    public required string WorkerType { get; init; }
    public required string Domain { get; init; }
    public required TaskPriority Priority { get; init; }
    public required TrustLevel RequiredTrust { get; init; }
    public bool NeedsDecomposition { get; init; }
    public IReadOnlyList<string> SuggestedSubTasks { get; init; } = [];
    public string Reasoning { get; init; } = "";
}

/// <summary>Raw JSON shape from LLM response.</summary>
internal sealed class TaskClassificationResponse
{
    [JsonPropertyName("workerType")]
    public string? WorkerType { get; set; }

    [JsonPropertyName("domain")]
    public string? Domain { get; set; }

    [JsonPropertyName("priority")]
    public string? Priority { get; set; }

    [JsonPropertyName("requiredTrust")]
    public string? RequiredTrust { get; set; }

    [JsonPropertyName("needsDecomposition")]
    public bool NeedsDecomposition { get; set; }

    [JsonPropertyName("suggestedSubTasks")]
    public List<string>? SuggestedSubTasks { get; set; }

    [JsonPropertyName("reasoning")]
    public string? Reasoning { get; set; }
}
