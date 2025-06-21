using LifeAgent.Core.Audio;
using LifeAgent.Core.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LifeAgent.Audio.Pipeline;

/// <summary>
/// Groups individual transcript segments into conversations based on temporal proximity.
/// A conversation is a cluster of utterances separated by gaps shorter than
/// <see cref="AudioPipelineOptions.ConversationGapSeconds"/>.
///
/// When a gap exceeding the threshold is detected, the current conversation is finalized
/// and emitted for downstream processing (LLM structuring → memory store).
/// </summary>
public sealed class ConversationSegmenter
{
    private readonly AudioPipelineOptions _options;
    private readonly ILogger<ConversationSegmenter> _logger;

    private Conversation? _current;
    private DateTimeOffset _lastUtteranceTime = DateTimeOffset.MinValue;

    /// <summary>
    /// Raised when a conversation is finalized (gap exceeded or explicit flush).
    /// Subscribers receive the complete conversation with all segments.
    /// </summary>
    public event Func<Conversation, Task>? OnConversationFinalized;

    public ConversationSegmenter(
        IOptions<AudioPipelineOptions> options,
        ILogger<ConversationSegmenter> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Process an incoming transcript segment. May trigger conversation finalization
    /// if the gap since the last utterance exceeds the threshold.
    /// </summary>
    public async Task ProcessSegmentAsync(AudioSegmentTranscribed evt)
    {
        var gapThreshold = TimeSpan.FromSeconds(_options.ConversationGapSeconds);
        var gap = evt.UtteranceStart - _lastUtteranceTime;

        // If the gap exceeds threshold, finalize the current conversation and start a new one
        if (_current is not null && gap > gapThreshold)
        {
            await FinalizeCurrentAsync();
        }

        // Start a new conversation if needed
        if (_current is null)
        {
            _current = new Conversation
            {
                Id = Guid.NewGuid().ToString("N"),
                StartedAt = evt.UtteranceStart,
            };

            _logger.LogDebug("[SEGMENTER] New conversation started: {Id}", _current.Id);
        }

        // Add the segment to the current conversation
        var segment = new TranscriptSegment
        {
            Id = evt.SegmentId,
            ConversationId = _current.Id,
            Transcript = evt.Transcript,
            SpeakerLabel = evt.SpeakerLabel,
            Confidence = evt.Confidence,
            StartedAt = evt.UtteranceStart,
            Duration = evt.Duration,
        };

        _current.Segments.Add(segment);
        _lastUtteranceTime = evt.UtteranceStart + evt.Duration;

        // Track participants
        if (evt.SpeakerLabel is not null)
            _current.Participants.Add(evt.SpeakerLabel);
    }

    /// <summary>
    /// Update speaker attribution for a segment after speaker identification completes.
    /// </summary>
    public void AttributeSpeaker(string segmentId, string speakerName)
    {
        if (_current is null) return;

        var segment = _current.Segments.FirstOrDefault(s => s.Id == segmentId);
        if (segment is not null)
        {
            segment.SpeakerName = speakerName;
            _current.Participants.Add(speakerName);
        }
    }

    /// <summary>
    /// Force-finalize the current conversation (e.g., on recording pause or shutdown).
    /// </summary>
    public async Task FlushAsync()
    {
        if (_current is not null)
            await FinalizeCurrentAsync();
    }

    /// <summary>
    /// Check if the current conversation should be finalized based on elapsed silence.
    /// Called periodically by the pipeline background service.
    /// </summary>
    public async Task CheckTimeoutAsync()
    {
        if (_current is null) return;

        var gap = DateTimeOffset.UtcNow - _lastUtteranceTime;
        if (gap > TimeSpan.FromSeconds(_options.ConversationGapSeconds))
        {
            await FinalizeCurrentAsync();
        }
    }

    private async Task FinalizeCurrentAsync()
    {
        if (_current is null) return;

        _current.EndedAt = _lastUtteranceTime;

        _logger.LogInformation(
            "[SEGMENTER] Conversation finalized: {Id} ({SegmentCount} segments, {Duration}, {Participants} participants)",
            _current.Id,
            _current.Segments.Count,
            _current.TotalDuration,
            _current.Participants.Count);

        if (OnConversationFinalized is not null)
            await OnConversationFinalized(_current);

        _current = null;
    }
}
