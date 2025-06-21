using System.Net;
using System.Net.WebSockets;
using LifeAgent.Core.Audio;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LifeAgent.Audio.Pipeline;

/// <summary>
/// WebSocket server that listens for audio streams from the Omi pendant's
/// companion app. The Omi Flutter app connects via WebSocket and streams
/// raw PCM audio frames.
///
/// Protocol: Binary WebSocket frames containing PCM 16-bit, 16kHz, mono audio.
/// One connection per active pendant session.
/// </summary>
public sealed class OmiWebSocketListener : IAsyncDisposable
{
    private readonly AudioPipelineOptions _options;
    private readonly ILogger<OmiWebSocketListener> _logger;
    private readonly Func<ReadOnlyMemory<byte>, Task> _onAudioReceived;
    private HttpListener? _httpListener;
    private CancellationTokenSource? _cts;

    public OmiWebSocketListener(
        IOptions<AudioPipelineOptions> options,
        ILogger<OmiWebSocketListener> logger,
        Func<ReadOnlyMemory<byte>, Task> onAudioReceived)
    {
        _options = options.Value;
        _logger = logger;
        _onAudioReceived = onAudioReceived;
    }

    /// <summary>
    /// Start listening for WebSocket connections on the configured port.
    /// </summary>
    public Task StartAsync(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        _httpListener = new HttpListener();
        _httpListener.Prefixes.Add($"http://+:{_options.WebSocketPort}/audio/");
        _httpListener.Start();

        _logger.LogInformation("[OMI-WS] Listening for Omi pendant connections on ws://0.0.0.0:{Port}/audio/",
            _options.WebSocketPort);

        // Accept connections in the background
        _ = AcceptConnectionsAsync(_cts.Token);

        return Task.CompletedTask;
    }

    private async Task AcceptConnectionsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _httpListener?.IsListening == true)
        {
            try
            {
                var context = await _httpListener.GetContextAsync();

                if (!context.Request.IsWebSocketRequest)
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                    continue;
                }

                var wsContext = await context.AcceptWebSocketAsync(null);
                _logger.LogInformation("[OMI-WS] Pendant connected from {Remote}",
                    context.Request.RemoteEndPoint);

                _ = HandleConnectionAsync(wsContext.WebSocket, ct);
            }
            catch (ObjectDisposedException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OMI-WS] Error accepting connection");
            }
        }
    }

    private async Task HandleConnectionAsync(WebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[4096]; // ~128ms of audio at 16kHz 16-bit mono

        try
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(buffer, ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("[OMI-WS] Pendant disconnected (clean close)");
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, ct);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Binary && result.Count > 0)
                {
                    await _onAudioReceived(new ReadOnlyMemory<byte>(buffer, 0, result.Count));
                }
            }
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        {
            _logger.LogWarning("[OMI-WS] Pendant disconnected unexpectedly");
        }
        catch (OperationCanceledException)
        {
            // Shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OMI-WS] Error handling pendant connection");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _httpListener?.Stop();
        _httpListener?.Close();
        _cts?.Dispose();
        await Task.CompletedTask;
    }
}
