using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Forge.Core;

/// <summary>
/// Append-only JSONL event log for a single agent session.
/// Writes one JSON object per line. Designed for post-hoc analysis and replay.
/// </summary>
public sealed class EventLog : IAsyncDisposable
{
    private const int MaxSanitizedFileNameLength = 50;

    private readonly StreamWriter _writer;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public string FilePath { get; }

    private EventLog(StreamWriter writer, string filePath)
    {
        _writer = writer;
        FilePath = filePath;
    }

    public static async Task<EventLog> CreateAsync(string sessionsDir, string taskSlug, string? model = null, string? workspace = null)
    {
        Directory.CreateDirectory(sessionsDir);
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        var safeName = SanitizeFileName(taskSlug);
        var path = Path.Combine(sessionsDir, $"{timestamp}-{safeName}.jsonl");
        var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        StreamWriter writer;
        try
        {
            writer = new StreamWriter(stream) { AutoFlush = true };
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }

        var log = new EventLog(writer, path);
        await log.WriteEventAsync("session_start", new
        {
            task = taskSlug,
            model,
            workspace,
            startedAt = DateTimeOffset.UtcNow,
        });
        return log;
    }

    public async Task RecordStepAsync(StepRecord step)
    {
        await WriteEventAsync("step", step);
    }

    public async Task RecordSessionEndAsync(AgentResult result)
    {
        await WriteEventAsync("session_end", new
        {
            success = result.Success,
            totalSteps = result.Steps.Count,
            totalPromptTokens = result.TotalPromptTokens,
            totalCompletionTokens = result.TotalCompletionTokens,
            totalDurationMs = result.TotalDurationMs,
            failureReason = result.FailureReason,
        });
    }

    public async Task RecordHandoffAsync(SessionHandoff handoff)
    {
        await WriteEventAsync("session_handoff", handoff);
    }

    private async Task WriteEventAsync(string eventType, object payload)
    {
        var envelope = new
        {
            @event = eventType,
            ts = DateTimeOffset.UtcNow,
            data = payload,
        };
        var json = JsonSerializer.Serialize(envelope, _jsonOptions);
        await _writer.WriteLineAsync(json);
    }

    internal static string SanitizeFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "unnamed";

        var normalized = input.Normalize(NormalizationForm.FormKD);
        var builder = new StringBuilder(normalized.Length);
        var previousWasHyphen = false;

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark)
                continue;

            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
                previousWasHyphen = false;
                continue;
            }

            if (builder.Length > 0 && !previousWasHyphen)
            {
                builder.Append('-');
                previousWasHyphen = true;
            }
        }

        var sanitized = builder.ToString().Trim('-');
        if (sanitized.Length == 0)
            return "unnamed";
        if (sanitized.Length > MaxSanitizedFileNameLength)
            sanitized = sanitized[..MaxSanitizedFileNameLength].Trim('-');

        return sanitized.Length == 0 ? "unnamed" : sanitized;
    }

    public async ValueTask DisposeAsync()
    {
        await _writer.DisposeAsync();
    }
}
