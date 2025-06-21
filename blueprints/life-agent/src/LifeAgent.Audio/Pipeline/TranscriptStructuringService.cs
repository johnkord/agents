using LifeAgent.Core;
using LifeAgent.Core.Audio;
using LifeAgent.Core.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LifeAgent.Audio.Pipeline;

/// <summary>
/// Uses an LLM to structure finalized conversations into summaries, action items,
/// entities, and topic tags. This is the "intelligence" layer of the audio pipeline.
///
/// Receives finalized <see cref="Conversation"/> objects from the <see cref="ConversationSegmenter"/>
/// and emits <see cref="ConversationSummarized"/> and <see cref="SpokenCommitmentDetected"/> events.
///
/// Uses a cheap/fast model (gpt-4o-mini by default) since this runs on every conversation.
/// </summary>
public sealed class TranscriptStructuringService
{
    private readonly AudioPipelineOptions _options;
    private readonly ILogger<TranscriptStructuringService> _logger;
    private readonly IChatCompletionService _llm;

    public TranscriptStructuringService(
        IOptions<AudioPipelineOptions> options,
        ILogger<TranscriptStructuringService> logger,
        IChatCompletionService llm)
    {
        _options = options.Value;
        _logger = logger;
        _llm = llm;
    }

    /// <summary>
    /// Structure a finalized conversation: generate summary, extract action items,
    /// entities, and topics. Returns the structured events to be appended to the event store.
    /// </summary>
    public async Task<ConversationStructureResult> StructureAsync(
        Conversation conversation, CancellationToken ct = default)
    {
        var result = new ConversationStructureResult();

        // Skip very short conversations (greetings, passing exchanges)
        if (conversation.TotalDuration < TimeSpan.FromSeconds(_options.MinConversationDurationForSummarySeconds))
        {
            _logger.LogDebug("[STRUCTURING] Skipping short conversation {Id} ({Duration})",
                conversation.Id, conversation.TotalDuration);
            return result;
        }

        var transcript = conversation.ToAttributedTranscript();

        _logger.LogInformation("[STRUCTURING] Processing conversation {Id} ({Segments} segments, {Duration})",
            conversation.Id, conversation.Segments.Count, conversation.TotalDuration);

        // ── Step 1: Summary + topics ──────────────────────────────

        var summaryResponse = await _llm.CompleteAsync(
            SummarySystemPrompt,
            $"Conversation transcript:\n\n{transcript}",
            ModelTier.Fast, ct: ct);

        var parsed = ParseStructuredResponse(summaryResponse);
        conversation.Summary = parsed.Summary;
        conversation.Topics = parsed.Topics;
        conversation.Entities = parsed.Entities;

        result.SummaryEvent = new ConversationSummarized(
            ConversationId: conversation.Id,
            Summary: parsed.Summary,
            ActionItems: parsed.ActionItems,
            Entities: parsed.Entities,
            Topics: parsed.Topics,
            Participants: conversation.Participants.Select(p =>
                new ConversationParticipant(
                    p,
                    conversation.Segments.Any(s => s.SpeakerName == p),
                    conversation.Segments.Count(s => s.SpeakerName == p || s.SpeakerLabel == p)
                )).ToList(),
            TotalDuration: conversation.TotalDuration ?? TimeSpan.Zero);

        // ── Step 2: Commitment detection ──────────────────────────

        var commitmentResponse = await _llm.CompleteAsync(
            CommitmentSystemPrompt,
            $"Conversation transcript:\n\n{transcript}",
            ModelTier.Fast, ct: ct);

        result.Commitments = ParseCommitments(commitmentResponse, conversation);

        _logger.LogInformation(
            "[STRUCTURING] Structured conversation {Id}: {TopicCount} topics, {ActionCount} actions, {CommitmentCount} commitments",
            conversation.Id, parsed.Topics.Count, parsed.ActionItems.Count, result.Commitments.Count);

        return result;
    }

    private static ParsedStructure ParseStructuredResponse(string response)
    {
        // Parse the LLM's structured response. Expected format:
        // SUMMARY: ...
        // TOPICS: topic1, topic2, ...
        // ENTITIES: entity1, entity2, ...
        // ACTION_ITEMS: item1 | item2 | ...

        var result = new ParsedStructure();
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (line.StartsWith("SUMMARY:", StringComparison.OrdinalIgnoreCase))
                result.Summary = line["SUMMARY:".Length..].Trim();
            else if (line.StartsWith("TOPICS:", StringComparison.OrdinalIgnoreCase))
                result.Topics = [.. line["TOPICS:".Length..].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];
            else if (line.StartsWith("ENTITIES:", StringComparison.OrdinalIgnoreCase))
                result.Entities = [.. line["ENTITIES:".Length..].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];
            else if (line.StartsWith("ACTION_ITEMS:", StringComparison.OrdinalIgnoreCase))
                result.ActionItems = [.. line["ACTION_ITEMS:".Length..].Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];
        }

        // Fallback: if no structured format detected, use entire response as summary
        if (string.IsNullOrEmpty(result.Summary))
            result.Summary = response.Trim();

        return result;
    }

    private static List<SpokenCommitmentDetected> ParseCommitments(
        string response, Conversation conversation)
    {
        var commitments = new List<SpokenCommitmentDetected>();
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (!line.StartsWith("COMMITMENT:", StringComparison.OrdinalIgnoreCase))
                continue;

            var parts = line["COMMITMENT:".Length..].Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length < 1) continue;

            var commitmentText = parts[0];
            var speakerName = parts.Length > 1 ? parts[1] : null;
            DateTimeOffset? deadline = parts.Length > 2 && DateTimeOffset.TryParse(parts[2], out var d) ? d : null;
            var confidence = parts.Length > 3 && float.TryParse(parts[3], out var c) ? c : 0.7f;

            commitments.Add(new SpokenCommitmentDetected(
                SegmentId: conversation.Segments.LastOrDefault()?.Id ?? "unknown",
                CommitmentText: commitmentText,
                SpeakerName: speakerName,
                ParsedDeadline: deadline,
                Confidence: confidence));
        }

        return commitments;
    }

    private const string SummarySystemPrompt = """
        You are a conversation structuring assistant. Given a speaker-attributed transcript,
        extract the following in this EXACT format (one per line):

        SUMMARY: A 2-3 sentence summary of what was discussed.
        TOPICS: comma-separated list of discussion topics
        ENTITIES: comma-separated list of named entities (people, places, organizations, dates, products)
        ACTION_ITEMS: pipe-separated list of action items or decisions made (e.g., "Call dentist by Friday | Review budget proposal")

        If a field has no items, write NONE. Be concise and factual. Do not invent information not in the transcript.
        """;

    private const string CommitmentSystemPrompt = """
        You are a commitment detection assistant. Given a speaker-attributed transcript,
        identify any spoken commitments, promises, or "I need to..." statements.

        For each commitment found, output one line in this format:
        COMMITMENT: <what was committed> | <who said it> | <deadline if mentioned, ISO 8601> | <confidence 0.0-1.0>

        Examples:
        COMMITMENT: Call the dentist to reschedule | Jordan | 2026-03-10 | 0.9
        COMMITMENT: Send the Q2 budget proposal | Alex | | 0.7
        COMMITMENT: Pick up groceries on the way home | Jordan | 2026-03-08 | 0.85

        Only output lines starting with COMMITMENT:. If no commitments are found, output nothing.
        Be conservative — only flag clear commitments, not vague intentions.
        """;

    private sealed class ParsedStructure
    {
        public string Summary { get; set; } = string.Empty;
        public List<string> Topics { get; set; } = [];
        public List<string> Entities { get; set; } = [];
        public List<string> ActionItems { get; set; } = [];
    }
}

/// <summary>
/// Result of structuring a single conversation.
/// </summary>
public sealed class ConversationStructureResult
{
    public ConversationSummarized? SummaryEvent { get; set; }
    public List<SpokenCommitmentDetected> Commitments { get; set; } = [];
}
