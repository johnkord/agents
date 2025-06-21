namespace LifeAgent.Core.Audio;

/// <summary>
/// A single transcribed utterance from the audio pipeline.
/// Represents one speaker turn, VAD-segmented by Deepgram.
/// </summary>
public sealed class TranscriptSegment
{
    public required string Id { get; init; }
    public required string ConversationId { get; init; }
    public required string Transcript { get; init; }
    public string? SpeakerLabel { get; set; }
    public string? SpeakerName { get; set; }
    public float Confidence { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// ECAPA-TDNN embedding for this utterance's speaker (192-dim float vector).
    /// Used for speaker identification against the gallery.
    /// </summary>
    public float[]? SpeakerEmbedding { get; set; }
}

/// <summary>
/// A conversation is a group of temporally-clustered transcript segments
/// involving one or more speakers. Conversations are delimited by silence gaps
/// exceeding a configurable threshold (default: 2 minutes).
/// </summary>
public sealed class Conversation
{
    public required string Id { get; init; }
    public List<TranscriptSegment> Segments { get; init; } = [];
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? EndedAt { get; set; }
    public TimeSpan? TotalDuration => EndedAt.HasValue ? EndedAt.Value - StartedAt : null;

    /// <summary>Distinct identified speakers in this conversation.</summary>
    public HashSet<string> Participants { get; } = [];

    /// <summary>LLM-generated summary. Null until the conversation is finalized and processed.</summary>
    public string? Summary { get; set; }

    /// <summary>Extracted action items (spoken commitments, "I need to...", etc.).</summary>
    public List<string> ActionItems { get; set; } = [];

    /// <summary>Named entities extracted (people, places, dates, organizations).</summary>
    public List<string> Entities { get; set; } = [];

    /// <summary>LLM-classified topic tags.</summary>
    public List<string> Topics { get; set; } = [];

    /// <summary>
    /// Returns the full transcript with speaker labels, one line per segment.
    /// </summary>
    public string ToAttributedTranscript()
    {
        return string.Join('\n', Segments.Select(s =>
        {
            var speaker = s.SpeakerName ?? s.SpeakerLabel ?? "Unknown";
            return $"[{s.StartedAt:HH:mm:ss}] {speaker}: {s.Transcript}";
        }));
    }
}
