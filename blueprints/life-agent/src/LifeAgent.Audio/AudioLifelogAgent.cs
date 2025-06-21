using LifeAgent.Core;
using LifeAgent.Core.Audio;
using LifeAgent.Core.Events;
using LifeAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace LifeAgent.Audio;

/// <summary>
/// Worker agent for audio lifelogging queries and operations.
/// Handles tasks like:
/// - "What did Alex say about the project deadline?"
/// - "Show me today's conversation summary"
/// - "Enroll a new speaker"
/// - Creating LifeTasks from spoken commitments
///
/// This worker is the interface between the orchestrator and the audio pipeline.
/// It reads from the conversational memory store and the event store.
/// </summary>
public sealed class AudioLifelogAgent : IWorkerAgent
{
    private readonly IConversationalMemory _memory;
    private readonly IEventStore _eventStore;
    private readonly ILogger<AudioLifelogAgent> _logger;

    public string WorkerType => "audio-lifelog";
    public string Description =>
        "Searches and queries the speaker-attributed conversational memory " +
        "from the audio lifelogging pipeline. Handles conversation recall, " +
        "daily digests, and commitment tracking.";
    public IReadOnlyList<string> SupportedDomains { get; } = ["audio-lifelog", "conversation-recall", "memory"];

    public AudioLifelogAgent(
        IConversationalMemory memory,
        IEventStore eventStore,
        ILogger<AudioLifelogAgent> logger)
    {
        _memory = memory;
        _eventStore = eventStore;
        _logger = logger;
    }

    public async Task<TaskResult> ExecuteAsync(
        LifeTask task, UserProfile userProfile, CancellationToken ct)
    {
        _logger.LogInformation("[AUDIO-WORKER] Executing task: {Title}", task.Title);

        // Route to the appropriate handler based on task tags and description
        if (task.Tags.Contains("conversation-recall"))
            return await HandleConversationRecallAsync(task, ct);

        if (task.Tags.Contains("daily-digest"))
            return await HandleDailyDigestAsync(task, ct);

        if (task.Tags.Contains("speaker-enrollment"))
            return await HandleSpeakerEnrollmentAsync(task, ct);

        // Default: treat as a conversational memory search
        return await HandleConversationRecallAsync(task, ct);
    }

    /// <summary>
    /// Handles "What did X say about Y?" style queries by searching
    /// the conversational memory store.
    /// </summary>
    private async Task<TaskResult> HandleConversationRecallAsync(LifeTask task, CancellationToken ct)
    {
        var query = task.Description ?? task.Title;

        _logger.LogInformation("[AUDIO-WORKER] Searching conversational memory: \"{Query}\"", query);

        // Search transcripts
        var segments = await _memory.SearchTranscriptsAsync(
            query: query,
            maxResults: 10,
            ct: ct);

        if (segments.Count == 0)
        {
            return new TaskResult
            {
                Success = true,
                Summary = $"No matching conversations found for: \"{query}\"",
                CompletedAt = DateTimeOffset.UtcNow,
            };
        }

        // Group by conversation and build a readable result
        var grouped = segments.GroupBy(s => s.ConversationId);
        var results = new List<string>();

        foreach (var group in grouped)
        {
            var conversation = await _memory.GetConversationAsync(group.Key, ct);
            var header = conversation?.Summary is not null
                ? $"**{conversation.StartedAt:MMM d, HH:mm}** — {conversation.Summary}"
                : $"**{group.First().StartedAt:MMM d, HH:mm}** — conversation";

            var excerpts = group.Select(s =>
            {
                var speaker = s.SpeakerName ?? s.SpeakerLabel ?? "Unknown";
                return $"  [{s.StartedAt:HH:mm:ss}] {speaker}: {s.Transcript}";
            });

            results.Add($"{header}\n{string.Join('\n', excerpts)}");
        }

        var output = string.Join("\n\n---\n\n", results);

        return new TaskResult
        {
            Success = true,
            Summary = $"Found {segments.Count} matching segments across {grouped.Count()} conversations",
            DetailedOutput = output,
            CompletedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>
    /// Generates the daily conversation digest for the evening briefing.
    /// </summary>
    private async Task<TaskResult> HandleDailyDigestAsync(LifeTask task, CancellationToken ct)
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var stats = await _memory.GetDailyStatsAsync(date, ct);

        if (stats.ConversationCount == 0)
        {
            return new TaskResult
            {
                Success = true,
                Summary = "No conversations recorded today.",
                CompletedAt = DateTimeOffset.UtcNow,
            };
        }

        var digest = $"""
            ## Conversation Digest — {date:MMMM d, yyyy}

            **{stats.ConversationCount} conversations** ({stats.TotalDuration:h\h\ mm\m} total)

            **Top speakers**: {string.Join(", ", stats.TopSpeakers)}
            **Topics**: {string.Join(", ", stats.TopTopics)}
            """;

        if (stats.UnresolvedActionItems > 0)
            digest += $"\n\n⚠️ **{stats.UnresolvedActionItems} unresolved action items** from today's conversations.";

        // Include individual conversation summaries
        var from = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var conversations = await _memory.GetConversationsAsync(from, from.AddDays(1), ct);

        foreach (var conv in conversations.Where(c => c.Summary is not null))
        {
            digest += $"\n\n### {conv.StartedAt:HH:mm} ({conv.TotalDuration:m\\m\\ ss\\s}) — {string.Join(", ", conv.Participants)}";
            digest += $"\n{conv.Summary}";
            if (conv.ActionItems.Count > 0)
                digest += $"\n- Action items: {string.Join("; ", conv.ActionItems)}";
        }

        return new TaskResult
        {
            Success = true,
            Summary = $"Daily digest: {stats.ConversationCount} conversations, " +
                      $"{stats.TotalDuration:h\\h\\ mm\\m} total, " +
                      $"top topics: {string.Join(", ", stats.TopTopics.Take(3))}",
            DetailedOutput = digest,
            CompletedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>
    /// Placeholder for speaker enrollment flow.
    /// Full implementation requires ECAPA-TDNN inference (Python sidecar or ONNX).
    /// </summary>
    private Task<TaskResult> HandleSpeakerEnrollmentAsync(LifeTask task, CancellationToken ct)
    {
        _logger.LogWarning("[AUDIO-WORKER] Speaker enrollment not yet implemented (requires ECAPA-TDNN sidecar)");

        return Task.FromResult(new TaskResult
        {
            Success = false,
            Summary = "Speaker enrollment requires the ECAPA-TDNN embedding service, which is not yet available. " +
                      "For now, speakers are identified by Deepgram's built-in diarization labels (Speaker 0, 1, 2...).",
            CompletedAt = DateTimeOffset.UtcNow,
        });
    }
}
