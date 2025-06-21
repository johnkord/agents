using LifeAgent.Core.Audio;
using LifeAgent.Core.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Deepgram;
using Deepgram.Clients.Interfaces.v2;
using Deepgram.Models.Authenticate.v1;
using Deepgram.Models.Listen.v2.WebSocket;

namespace LifeAgent.Audio.Deepgram;

/// <summary>
/// Streams audio from the Omi pendant (via WebSocket relay) to Deepgram's
/// nova-3 real-time API. Emits <see cref="AudioSegmentTranscribed"/> events
/// for each VAD-segmented utterance.
///
/// Architecture: Omi Pendant → BLE → iPhone App → WebSocket → this service → Deepgram WSS
///
/// Uses the Deepgram .NET SDK v5 for WebSocket-based streaming transcription.
/// Audio is never stored — only the resulting transcript text is persisted.
/// </summary>
public sealed class DeepgramStreamingClient : IAsyncDisposable
{
    private readonly AudioPipelineOptions _options;
    private readonly ILogger<DeepgramStreamingClient> _logger;
    private readonly Func<AudioSegmentTranscribed, Task> _onTranscript;
    private IListenWebSocketClient? _wsClient;
    private CancellationTokenSource? _cts;

    public bool IsConnected => _wsClient?.IsConnected() ?? false;

    public DeepgramStreamingClient(
        IOptions<AudioPipelineOptions> options,
        ILogger<DeepgramStreamingClient> logger,
        Func<AudioSegmentTranscribed, Task> onTranscript)
    {
        _options = options.Value;
        _logger = logger;
        _onTranscript = onTranscript;
    }

    /// <summary>
    /// Opens a persistent WebSocket connection to Deepgram's streaming API.
    /// The connection stays open for the lifetime of the audio session.
    /// </summary>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (IsConnected)
        {
            _logger.LogWarning("Already connected to Deepgram — ignoring duplicate ConnectAsync call");
            return;
        }

        _logger.LogInformation("Connecting to Deepgram streaming API (model={Model}, diarization={Diarize})",
            _options.DeepgramModel, _options.EnableDiarization);

        var clientOptions = new DeepgramWsClientOptions(_options.DeepgramApiKey);

        _wsClient = ClientFactory.CreateListenWebSocketClient(
            _options.DeepgramApiKey, clientOptions);

        // Subscribe to transcript results (v5 per-type event pattern)
        await _wsClient.Subscribe(new EventHandler<ResultResponse>(async (sender, e) =>
        {
            try
            {
                if (e?.Channel?.Alternatives is not { Count: > 0 } alternatives)
                    return;

                var best = alternatives[0];
                if (string.IsNullOrWhiteSpace(best.Transcript))
                    return;

                var segment = new AudioSegmentTranscribed(
                    SegmentId: Guid.NewGuid().ToString("N"),
                    Transcript: best.Transcript.Trim(),
                    SpeakerLabel: best.Words?.FirstOrDefault()?.Speaker?.ToString(),
                    Duration: TimeSpan.FromSeconds((double)(e.Duration ?? 0m)),
                    Confidence: (float)(best.Confidence ?? 0),
                    UtteranceStart: DateTimeOffset.UtcNow - TimeSpan.FromSeconds((double)(e.Duration ?? 0m)));

                _logger.LogDebug("[DEEPGRAM] Transcript: speaker={Speaker} conf={Conf:F2} \"{Text}\"",
                    segment.SpeakerLabel, segment.Confidence, Truncate(segment.Transcript, 80));

                await _onTranscript(segment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DEEPGRAM] Error processing transcript result");
            }
        }));

        // Subscribe to errors
        await _wsClient.Subscribe(new EventHandler<ErrorResponse>((sender, e) =>
        {
            _logger.LogError("[DEEPGRAM] Error: {Error}", e?.ToString() ?? "unknown");
        }));

        // Subscribe to unhandled responses
        await _wsClient.Subscribe(new EventHandler<UnhandledResponse>((sender, e) =>
        {
            _logger.LogWarning("[DEEPGRAM] Unhandled response: {Type}", e?.Type);
        }));

        var liveOptions = new LiveSchema
        {
            Model = _options.DeepgramModel,
            Language = _options.Language,
            Punctuate = _options.EnablePunctuation,
            SmartFormat = _options.EnableSmartFormat,
            Diarize = _options.EnableDiarization,
            Encoding = _options.Encoding,
            SampleRate = _options.SampleRate,
            Channels = _options.Channels,
            InterimResults = false,
            UtteranceEnd = _options.VadSilenceThresholdMs.ToString(),
            VadEvents = true,
        };

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var connected = await _wsClient.Connect(liveOptions, _cts, null, null);

        if (!connected)
        {
            _logger.LogError("Failed to connect to Deepgram streaming API");
            return;
        }

        _logger.LogInformation("Connected to Deepgram streaming API");
    }

    /// <summary>
    /// Sends raw audio bytes (PCM 16-bit, 16kHz, mono) to Deepgram for transcription.
    /// Called continuously as audio frames arrive from the Omi pendant via BLE → phone → WebSocket.
    /// </summary>
    public Task SendAudioAsync(ReadOnlyMemory<byte> audioData, CancellationToken ct = default)
    {
        if (_wsClient is null || !IsConnected)
        {
            _logger.LogWarning("Cannot send audio — not connected to Deepgram");
            return Task.CompletedTask;
        }

        var data = audioData.ToArray();
        _wsClient.Send(data, data.Length);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Signals end of audio stream. Deepgram will flush any buffered audio
    /// and deliver any remaining transcript results before the connection closes.
    /// </summary>
    public async Task FinishAsync()
    {
        if (_wsClient is not null && IsConnected)
        {
            _logger.LogInformation("Sending Finalize to Deepgram");
            await _wsClient.SendFinalize();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_wsClient is not null)
        {
            try
            {
                await FinishAsync();
                await _wsClient.Stop(_cts ?? new CancellationTokenSource(), true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during Deepgram client disposal");
            }
            _wsClient = null;
        }

        _cts?.Dispose();
        _cts = null;
    }

    private static string Truncate(string s, int maxLength)
        => s.Length <= maxLength ? s : string.Concat(s.AsSpan(0, maxLength - 3), "...");
}
