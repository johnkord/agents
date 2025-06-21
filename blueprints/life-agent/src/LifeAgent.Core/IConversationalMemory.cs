using LifeAgent.Core.Audio;

namespace LifeAgent.Core;

/// <summary>
/// Interface for the conversational memory store — the queryable archive
/// of speaker-attributed transcripts from audio lifelogging.
/// Supports both metadata queries and semantic (vector) search.
/// </summary>
public interface IConversationalMemory
{
    // ── Write operations ───────────────────────────────────────────

    Task StoreSegmentAsync(TranscriptSegment segment, CancellationToken ct = default);
    Task StoreConversationAsync(Conversation conversation, CancellationToken ct = default);

    // ── Speaker gallery ────────────────────────────────────────────

    Task<IReadOnlyList<SpeakerProfile>> GetAllSpeakersAsync(CancellationToken ct = default);
    Task EnrollSpeakerAsync(SpeakerProfile profile, CancellationToken ct = default);
    Task<SpeakerProfile?> GetSpeakerAsync(string name, CancellationToken ct = default);
    Task UpdateSpeakerLastSeenAsync(string name, DateTimeOffset lastSeen, CancellationToken ct = default);

    // ── Query operations ───────────────────────────────────────────

    /// <summary>Search transcripts by text content (full-text search).</summary>
    Task<IReadOnlyList<TranscriptSegment>> SearchTranscriptsAsync(
        string query,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string? speakerName = null,
        int maxResults = 20,
        CancellationToken ct = default);

    /// <summary>Get all conversations within a date range.</summary>
    Task<IReadOnlyList<Conversation>> GetConversationsAsync(
        DateTimeOffset from, DateTimeOffset to,
        CancellationToken ct = default);

    /// <summary>Get a single conversation by ID with all segments.</summary>
    Task<Conversation?> GetConversationAsync(string conversationId, CancellationToken ct = default);

    /// <summary>Get today's conversation statistics for the daily digest.</summary>
    Task<DailyConversationStats> GetDailyStatsAsync(
        DateOnly date, CancellationToken ct = default);
}

/// <summary>
/// Aggregated statistics for a day's conversations, used in the daily digest.
/// </summary>
public sealed record DailyConversationStats(
    int ConversationCount,
    TimeSpan TotalDuration,
    IReadOnlyList<string> TopSpeakers,
    IReadOnlyList<string> TopTopics,
    int UnresolvedActionItems);
