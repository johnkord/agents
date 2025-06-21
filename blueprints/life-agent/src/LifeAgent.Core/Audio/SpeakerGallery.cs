namespace LifeAgent.Core.Audio;

/// <summary>
/// An enrolled speaker in the voice gallery. Each entry holds a name
/// and one or more ECAPA-TDNN embeddings (192-dim float vectors) recorded
/// during enrollment. Multiple embeddings improve robustness across
/// different acoustic conditions.
/// </summary>
public sealed class SpeakerProfile
{
    public required string Name { get; init; }
    public List<SpeakerEmbedding> Embeddings { get; init; } = [];
    public DateTimeOffset EnrolledAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; }

    /// <summary>
    /// Total number of utterances attributed to this speaker across all sessions.
    /// Used for confidence calibration and "most frequent contacts" reports.
    /// </summary>
    public long TotalUtterances { get; set; }
}

/// <summary>
/// A single embedding vector for a speaker, captured from a specific sample.
/// Multiple embeddings per speaker improves recognition across different
/// environments, mic distances, and emotional states.
/// </summary>
public sealed class SpeakerEmbedding
{
    /// <summary>ECAPA-TDNN 192-dimensional embedding vector.</summary>
    public required float[] Vector { get; init; }
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;
    public TimeSpan SampleDuration { get; init; }

    /// <summary>Optional note about conditions (e.g., "quiet room", "outdoors").</summary>
    public string? Note { get; init; }
}

/// <summary>
/// Result of matching an utterance embedding against the speaker gallery.
/// </summary>
public sealed record SpeakerMatch(
    string SpeakerName,
    float CosineSimilarity,
    bool IsConfident)
{
    /// <summary>
    /// Default cosine similarity threshold for confident identification.
    /// Based on ECAPA-TDNN benchmarks (0.8% EER at threshold ~0.25).
    /// We use a conservative threshold for real-world ambient conditions.
    /// </summary>
    public const float DefaultThreshold = 0.35f;
}
