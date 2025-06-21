using LifeAgent.Core;
using LifeAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace LifeAgent.App.Workers;

/// <summary>
/// LLM-powered catch-all worker for tasks that don't map to a specialized domain.
/// Uses ModelTier.Standard for reasoning over freeform requests.
///
/// This worker handles:
///  - Advice questions ("Should I...")
///  - Information synthesis ("What are the pros/cons of...")
///  - Simple analysis ("Compare X and Y")
///  - Task clarification (when a task is too vague for a specialist)
///
/// It's the safety net — if no specialized worker matches, the task lands here.
/// </summary>
public sealed class GeneralWorker : IWorkerAgent
{
    private readonly IChatCompletionService _llm;
    private readonly ILogger<GeneralWorker> _logger;

    public string WorkerType => "general";
    public string Description => "General-purpose LLM assistant for tasks that don't fit a specialist";
    public IReadOnlyList<string> SupportedDomains { get; } = ["general", "advice", "analysis"];

    public GeneralWorker(IChatCompletionService llm, ILogger<GeneralWorker> logger)
    {
        _llm = llm;
        _logger = logger;
    }

    public async Task<TaskResult> ExecuteAsync(LifeTask task, UserProfile userProfile, CancellationToken ct)
    {
        _logger.LogInformation("General worker executing: {Title}", task.Title);

        var context = BuildContext(task, userProfile);

        var response = await _llm.CompleteAsync(
            systemPrompt: """
                You are a helpful personal assistant. The user has submitted a task for you to handle.
                Provide a clear, practical, concise response. Focus on what's actionable.
                
                If the task is ambiguous, do your best interpretation and note any assumptions.
                If the task requires real-time data you don't have (prices, weather, etc.), say so clearly.
                
                Keep your response under 500 words unless the task clearly demands more detail.
                Do not use emojis. Be direct and helpful.
                """,
            userMessage: context,
            tier: ModelTier.Standard,
            temperature: 0.5f,
            maxTokens: 1500,
            ct: ct);

        return new TaskResult
        {
            Success = true,
            Summary = TruncateSummary(response),
            DetailedOutput = response.Trim(),
            CompletedAt = DateTimeOffset.UtcNow,
            LlmCostUsd = 0.005m, // ~1500 tokens on gpt-4o
        };
    }

    private static string BuildContext(LifeTask task, UserProfile userProfile)
    {
        var parts = new List<string> { $"Task: {task.Title}" };

        if (!string.IsNullOrWhiteSpace(task.Description))
            parts.Add($"Description: {task.Description}");
        if (task.Deadline.HasValue)
            parts.Add($"Deadline: {task.Deadline.Value:f}");
        if (task.Tags.Count > 0)
            parts.Add($"Tags: {string.Join(", ", task.Tags)}");

        // Include relevant user preferences for personalization
        if (userProfile.Preferences.Count > 0)
        {
            var prefs = string.Join("; ", userProfile.Preferences.Take(5)
                .Select(kv => $"{kv.Key}: {kv.Value}"));
            parts.Add($"User preferences: {prefs}");
        }

        return string.Join("\n", parts);
    }

    /// <summary>
    /// Extract the first meaningful sentence(s) as the summary.
    /// The full response goes into DetailedOutput.
    /// </summary>
    private static string TruncateSummary(string response)
    {
        var trimmed = response.Trim();
        if (trimmed.Length <= 200) return trimmed;

        // Find first paragraph break or first 200 chars
        var paraBreak = trimmed.IndexOf("\n\n", StringComparison.Ordinal);
        if (paraBreak > 0 && paraBreak <= 300)
            return trimmed[..paraBreak].Trim();

        // Fall back to first sentence
        var sentenceEnd = trimmed.IndexOfAny(['.', '!', '?'], 50);
        if (sentenceEnd > 0 && sentenceEnd <= 250)
            return trimmed[..(sentenceEnd + 1)].Trim();

        return trimmed[..200].Trim() + "...";
    }
}
