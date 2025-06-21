using System.Threading.Channels;
using LifeAgent.Audio.Deepgram;
using LifeAgent.Audio.Diarization;
using LifeAgent.Core;
using LifeAgent.Core.Audio;
using LifeAgent.Core.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LifeAgent.Audio.Pipeline;

/// <summary>
/// Top-level BackgroundService that orchestrates the full audio lifelogging pipeline:
///
///   Omi Pendant → BLE → Phone → [OmiWebSocketListener]
///     → [DeepgramStreamingClient] (ASR)
///       → [ConversationSegmenter] (temporal grouping)
///         → [SpeakerIdentificationService] (gallery matching)
///           → [TranscriptStructuringService] (LLM: summary, entities, commitments)
///             → [IConversationalMemory] (SQLite persistence)
///               → [IEventStore] (LifeEvent emission)
///
/// Lifecycle: Starts with the host, runs continuously, gracefully shuts down on stop.
/// </summary>
public sealed class AudioPipelineService : BackgroundService
{
    private readonly AudioPipelineOptions _options;
    private readonly ILogger<AudioPipelineService> _logger;
    private readonly DeepgramStreamingClient _deepgram;
    private readonly OmiWebSocketListener _wsListener;
    private readonly ConversationSegmenter _segmenter;
    private readonly SpeakerIdentificationService _speakerId;
    private readonly TranscriptStructuringService _structuring;
    private readonly IConversationalMemory _memory;
    private readonly IEventStore _eventStore;
    private readonly ChannelWriter<LifeEvent>? _orchestratorChannel;

    private long _totalSegments;
    private long _totalConversations;

    public AudioPipelineService(
        IOptions<AudioPipelineOptions> options,
        ILogger<AudioPipelineService> logger,
        ILoggerFactory loggerFactory,
        SpeakerIdentificationService speakerId,
        TranscriptStructuringService structuring,
        IConversationalMemory memory,
        IEventStore eventStore,
        Channel<LifeEvent>? orchestratorChannel = null)
    {
        _options = options.Value;
        _logger = logger;
        _speakerId = speakerId;
        _structuring = structuring;
        _memory = memory;
        _eventStore = eventStore;
        _orchestratorChannel = orchestratorChannel?.Writer;

        // Create the Deepgram client with our transcript handler
        _deepgram = new DeepgramStreamingClient(
            options,
            loggerFactory.CreateLogger<DeepgramStreamingClient>(),
            OnTranscriptReceivedAsync);

        // Create the conversation segmenter
        _segmenter = new ConversationSegmenter(
            options,
            loggerFactory.CreateLogger<ConversationSegmenter>());

        // Wire up conversation finalization
        _segmenter.OnConversationFinalized += OnConversationFinalizedAsync;

        // Create the WebSocket listener for the Omi pendant
        _wsListener = new OmiWebSocketListener(
            options,
            loggerFactory.CreateLogger<OmiWebSocketListener>(),
            audioData => _deepgram.SendAudioAsync(audioData));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrEmpty(_options.DeepgramApiKey))
        {
            _logger.LogWarning("[AUDIO-PIPELINE] Deepgram API key not configured — audio pipeline disabled. " +
                "Set Audio:DeepgramApiKey in appsettings.json or AUDIO__DEEPGRAMAPIKEY env var.");
            return;
        }

        _logger.LogInformation("[AUDIO-PIPELINE] Starting audio lifelogging pipeline...");

        try
        {
            // Load the speaker gallery from persistent storage
            var speakers = await _memory.GetAllSpeakersAsync(stoppingToken);
            _speakerId.LoadGallery(speakers);

            // Connect to Deepgram streaming API
            await _deepgram.ConnectAsync(stoppingToken);

            // Start listening for Omi pendant connections
            await _wsListener.StartAsync(stoppingToken);

            // Emit recording state change event
            await _eventStore.AppendAsync(
                new AudioRecordingStateChanged(IsRecording: true, Reason: "Pipeline started"),
                stoppingToken);

            _logger.LogInformation("[AUDIO-PIPELINE] Pipeline running. Waiting for audio from Omi pendant on ws://0.0.0.0:{Port}/audio/",
                _options.WebSocketPort);

            // Periodic conversation timeout check — finalizes conversations after silence gap
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await _segmenter.CheckTimeoutAsync();

                if (_totalSegments > 0 && _totalSegments % 100 == 0)
                {
                    _logger.LogInformation(
                        "[AUDIO-PIPELINE] Stats: {Segments} segments transcribed, {Conversations} conversations finalized",
                        _totalSegments, _totalConversations);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("[AUDIO-PIPELINE] Shutting down...");
        }
        finally
        {
            // Flush any in-progress conversation
            await _segmenter.FlushAsync();

            // Disconnect from Deepgram
            await _deepgram.DisposeAsync();
            await _wsListener.DisposeAsync();

            await _eventStore.AppendAsync(
                new AudioRecordingStateChanged(IsRecording: false, Reason: "Pipeline stopped"),
                CancellationToken.None);

            _logger.LogInformation("[AUDIO-PIPELINE] Pipeline stopped. Total: {Segments} segments, {Conversations} conversations",
                _totalSegments, _totalConversations);
        }
    }

    /// <summary>
    /// Called for each VAD-segmented utterance transcribed by Deepgram.
    /// </summary>
    private async Task OnTranscriptReceivedAsync(AudioSegmentTranscribed evt)
    {
        Interlocked.Increment(ref _totalSegments);

        // 1. Emit the raw transcript event
        await _eventStore.AppendAsync(evt);

        // 2. Attempt speaker identification (if we have an embedding)
        // NOTE: In the full pipeline, we'd extract ECAPA-TDNN embeddings here.
        // For MVP, we use Deepgram's built-in speaker labels and match post-hoc.
        string? identifiedSpeaker = null;
        // TODO: When ECAPA-TDNN sidecar is available, extract embedding and identify:
        // var embedding = await _ecapaTdnn.ExtractEmbeddingAsync(audioSegment);
        // var match = _speakerId.Identify(embedding);
        // if (match is not null) identifiedSpeaker = match.SpeakerName;

        if (identifiedSpeaker is not null)
        {
            await _eventStore.AppendAsync(new SpeakerIdentified(
                evt.SegmentId, identifiedSpeaker, 0.9f, []));

            await _memory.UpdateSpeakerLastSeenAsync(identifiedSpeaker, evt.Timestamp);
        }

        // 3. Feed into conversation segmenter
        await _segmenter.ProcessSegmentAsync(evt);

        // 4. Persist the segment
        var segment = new Core.Audio.TranscriptSegment
        {
            Id = evt.SegmentId,
            ConversationId = "pending", // Will be updated when conversation finalizes
            Transcript = evt.Transcript,
            SpeakerLabel = evt.SpeakerLabel,
            SpeakerName = identifiedSpeaker,
            Confidence = evt.Confidence,
            StartedAt = evt.UtteranceStart,
            Duration = evt.Duration,
        };

        await _memory.StoreSegmentAsync(segment);
    }

    /// <summary>
    /// Called when a conversation is finalized (silence gap exceeded or explicit flush).
    /// Runs LLM structuring to extract summary, action items, entities, and commitments.
    /// </summary>
    private async Task OnConversationFinalizedAsync(Conversation conversation)
    {
        Interlocked.Increment(ref _totalConversations);

        _logger.LogInformation(
            "[AUDIO-PIPELINE] Conversation finalized: {Id} ({Segments} segments, {Duration}, participants: {Participants})",
            conversation.Id,
            conversation.Segments.Count,
            conversation.TotalDuration,
            string.Join(", ", conversation.Participants));

        // Run LLM structuring
        var result = await _structuring.StructureAsync(conversation);

        // Emit structured events
        if (result.SummaryEvent is not null)
            await _eventStore.AppendAsync(result.SummaryEvent);

        foreach (var commitment in result.Commitments)
        {
            await _eventStore.AppendAsync(commitment);

            // Bridge to orchestrator — spoken commitments trigger task creation
            if (_orchestratorChannel is not null)
            {
                await _orchestratorChannel.WriteAsync(commitment);
            }
        }

        // Persist the full conversation
        await _memory.StoreConversationAsync(conversation);

        _logger.LogInformation(
            "[AUDIO-PIPELINE] Conversation {Id} structured: {Topics} topics, {Actions} actions, {Commitments} commitments",
            conversation.Id,
            result.SummaryEvent?.Topics.Count ?? 0,
            result.SummaryEvent?.ActionItems.Count ?? 0,
            result.Commitments.Count);
    }
}
