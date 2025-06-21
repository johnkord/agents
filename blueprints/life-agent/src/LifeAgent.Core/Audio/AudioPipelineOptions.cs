namespace LifeAgent.Core.Audio;

/// <summary>
/// Configuration for the audio lifelogging pipeline.
/// Loaded from appsettings.json "Audio" section via Options pattern.
/// </summary>
public sealed class AudioPipelineOptions
{
    public const string SectionName = "Audio";

    // ── Deepgram settings ─────────────────────────────────────────
    public string DeepgramApiKey { get; set; } = string.Empty;
    public string DeepgramModel { get; set; } = "nova-3";
    public string Language { get; set; } = "en";
    public bool EnableDiarization { get; set; } = true;
    public bool EnablePunctuation { get; set; } = true;
    public bool EnableSmartFormat { get; set; } = true;
    public string Encoding { get; set; } = "linear16";
    public int SampleRate { get; set; } = 16_000;
    public int Channels { get; set; } = 1;

    // ── VAD (Voice Activity Detection) ────────────────────────────
    /// <summary>
    /// Minimum speech duration (ms) to send to Deepgram.
    /// Filters out coughs, clicks, and other non-speech sounds.
    /// </summary>
    public int VadMinSpeechDurationMs { get; set; } = 250;

    /// <summary>
    /// Silence duration (ms) that triggers end-of-utterance.
    /// </summary>
    public int VadSilenceThresholdMs { get; set; } = 500;

    // ── Conversation segmentation ─────────────────────────────────
    /// <summary>
    /// Silence gap (seconds) between utterances that starts a new conversation.
    /// Default: 120 seconds (2 minutes of silence = new conversation).
    /// </summary>
    public int ConversationGapSeconds { get; set; } = 120;

    /// <summary>
    /// Minimum conversation duration (seconds) before it's summarized.
    /// Short exchanges (greetings, etc.) are stored but not LLM-processed.
    /// </summary>
    public int MinConversationDurationForSummarySeconds { get; set; } = 60;

    // ── Speaker identification ────────────────────────────────────
    /// <summary>
    /// Cosine similarity threshold for speaker identification.
    /// Higher = more conservative (fewer false positives, more "Unknown" labels).
    /// </summary>
    public float SpeakerIdThreshold { get; set; } = SpeakerMatch.DefaultThreshold;

    /// <summary>
    /// Minimum enrollment sample duration (seconds) for reliable embeddings.
    /// Research shows 10s is the sweet spot for ECAPA-TDNN.
    /// </summary>
    public int MinEnrollmentSampleSeconds { get; set; } = 10;

    // ── BLE / Omi pendant ─────────────────────────────────────────
    /// <summary>
    /// WebSocket endpoint that the Omi Flutter app streams audio to.
    /// The Life Agent backend listens on this endpoint.
    /// </summary>
    public int WebSocketPort { get; set; } = 8091;

    // ── LLM structuring ───────────────────────────────────────────
    /// <summary>Model used for transcript structuring (cheap/fast model preferred).</summary>
    public string StructuringModel { get; set; } = "gpt-4o-mini";

    // ── Privacy ───────────────────────────────────────────────────
    /// <summary>If true, raw audio bytes are never persisted — only transcripts.</summary>
    public bool DiscardAudioAfterTranscription { get; set; } = true;
}
