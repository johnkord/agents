namespace LifeAgent.Core.Events;

// ── Audio lifelogging events (Phase 5) ─────────────────────────────
//
// These events are emitted by the audio pipeline as it processes
// streaming audio from the Omi BLE pendant → Deepgram → structuring pipeline.
// They flow through the same event store as all other LifeEvents.

/// <summary>
/// Emitted when a speech segment has been transcribed by Deepgram.
/// One per utterance (VAD-segmented). The raw audio is discarded after transcription.
/// </summary>
public record AudioSegmentTranscribed(
    string SegmentId,
    string Transcript,
    string? SpeakerLabel,
    TimeSpan Duration,
    float Confidence,
    DateTimeOffset UtteranceStart) : LifeEvent;

/// <summary>
/// Emitted when a speaker has been identified by matching ECAPA-TDNN embeddings
/// against the enrolled speaker gallery. May arrive slightly after the transcript
/// if diarization is async.
/// </summary>
public record SpeakerIdentified(
    string SegmentId,
    string SpeakerName,
    float Confidence,
    float[] Embedding) : LifeEvent;

/// <summary>
/// Emitted when a multi-utterance conversation segment has been structured by the LLM.
/// Contains the distilled summary, extracted action items, entities, and topics.
/// </summary>
public record ConversationSummarized(
    string ConversationId,
    string Summary,
    List<string> ActionItems,
    List<string> Entities,
    List<string> Topics,
    List<ConversationParticipant> Participants,
    TimeSpan TotalDuration) : LifeEvent;

/// <summary>
/// Emitted when a spoken commitment is detected ("I need to...", "Remind me to...").
/// The AudioLifelogAgent creates a LifeTask from this.
/// </summary>
public record SpokenCommitmentDetected(
    string SegmentId,
    string CommitmentText,
    string? SpeakerName,
    DateTimeOffset? ParsedDeadline,
    float Confidence) : LifeEvent;

/// <summary>
/// Emitted when a new speaker is enrolled in the gallery via a voice sample.
/// </summary>
public record SpeakerEnrolled(
    string SpeakerName,
    float[] Embedding,
    TimeSpan SampleDuration) : LifeEvent;

/// <summary>
/// Emitted when recording is paused/resumed (user-initiated or location-based).
/// </summary>
public record AudioRecordingStateChanged(
    bool IsRecording,
    string Reason) : LifeEvent;

/// <summary>
/// A participant in a summarized conversation.
/// </summary>
public sealed record ConversationParticipant(
    string Name,
    bool Identified,
    int UtteranceCount);
