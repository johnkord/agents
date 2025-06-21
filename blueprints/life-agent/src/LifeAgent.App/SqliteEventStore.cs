using System.Runtime.CompilerServices;
using System.Text.Json;
using LifeAgent.Core;
using LifeAgent.Core.Events;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace LifeAgent.App;

/// <summary>
/// SQLite-backed event store following the ESAA append-only pattern.
/// All state mutations flow through here as immutable events.
/// Uses WAL mode for concurrent reads + single writer.
/// </summary>
public sealed class SqliteEventStore : IEventStore, IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly ILogger<SqliteEventStore> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public SqliteEventStore(string dbPath, ILogger<SqliteEventStore> logger)
    {
        _connectionString = $"Data Source={dbPath};Mode=ReadWriteCreate;Cache=Shared";
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await ExecuteAsync(conn, "PRAGMA journal_mode=WAL;", ct);
        await ExecuteAsync(conn, "PRAGMA synchronous=NORMAL;", ct);

        await ExecuteAsync(conn, """
            CREATE TABLE IF NOT EXISTS events (
                sequence INTEGER PRIMARY KEY AUTOINCREMENT,
                event_id TEXT NOT NULL UNIQUE,
                event_type TEXT NOT NULL,
                timestamp TEXT NOT NULL,
                correlation_id TEXT,
                payload_json TEXT NOT NULL,
                created_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
            """, ct);

        await ExecuteAsync(conn, "CREATE INDEX IF NOT EXISTS idx_events_type ON events(event_type);", ct);
        await ExecuteAsync(conn, "CREATE INDEX IF NOT EXISTS idx_events_timestamp ON events(timestamp);", ct);
        await ExecuteAsync(conn, "CREATE INDEX IF NOT EXISTS idx_events_correlation ON events(correlation_id);", ct);

        _logger.LogInformation("[EVENT-STORE] Initialized at {Path}", _connectionString);
    }

    public async Task<long> AppendAsync(LifeEvent evt, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO events (event_id, event_type, timestamp, correlation_id, payload_json)
            VALUES (@eventId, @eventType, @timestamp, @correlationId, @payload);
            SELECT last_insert_rowid();
            """;

        var typeName = evt.GetType().Name;
        cmd.Parameters.AddWithValue("@eventId", evt.EventId);
        cmd.Parameters.AddWithValue("@eventType", typeName);
        cmd.Parameters.AddWithValue("@timestamp", evt.Timestamp.ToString("O"));
        cmd.Parameters.AddWithValue("@correlationId", (object?)evt.CorrelationId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@payload", JsonSerializer.Serialize<object>(evt, JsonOptions));

        var sequence = (long)(await cmd.ExecuteScalarAsync(ct))!;

        _logger.LogDebug("[EVENT-STORE] Appended {Type} (seq={Seq}, id={Id})", typeName, sequence, evt.EventId);
        return sequence;
    }

    public async Task<long> AppendBatchAsync(IReadOnlyList<LifeEvent> events, CancellationToken ct = default)
    {
        if (events.Count == 0) return 0;

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var txn = await conn.BeginTransactionAsync(ct);
        long lastSequence = 0;

        foreach (var evt in events)
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = (SqliteTransaction)txn;
            cmd.CommandText = """
                INSERT INTO events (event_id, event_type, timestamp, correlation_id, payload_json)
                VALUES (@eventId, @eventType, @timestamp, @correlationId, @payload);
                SELECT last_insert_rowid();
                """;

            cmd.Parameters.AddWithValue("@eventId", evt.EventId);
            cmd.Parameters.AddWithValue("@eventType", evt.GetType().Name);
            cmd.Parameters.AddWithValue("@timestamp", evt.Timestamp.ToString("O"));
            cmd.Parameters.AddWithValue("@correlationId", (object?)evt.CorrelationId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@payload", JsonSerializer.Serialize<object>(evt, JsonOptions));

            lastSequence = (long)(await cmd.ExecuteScalarAsync(ct))!;
        }

        await txn.CommitAsync(ct);

        _logger.LogDebug("[EVENT-STORE] Appended batch of {Count} events (last seq={Seq})",
            events.Count, lastSequence);
        return lastSequence;
    }

    public async IAsyncEnumerable<(long Sequence, LifeEvent Event)> ReadFromAsync(
        long fromSequence, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT sequence, event_type, payload_json FROM events
            WHERE sequence >= @from ORDER BY sequence
            """;
        cmd.Parameters.AddWithValue("@from", fromSequence);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var sequence = reader.GetInt64(0);
            var typeName = reader.GetString(1);
            var json = reader.GetString(2);

            var evt = DeserializeEvent(typeName, json);
            if (evt is not null)
                yield return (sequence, evt);
        }
    }

    public async IAsyncEnumerable<LifeEvent> ReadByTypeAsync<T>(
        DateTimeOffset from, DateTimeOffset to,
        [EnumeratorCancellation] CancellationToken ct = default)
        where T : LifeEvent
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT payload_json FROM events
            WHERE event_type = @type AND timestamp >= @from AND timestamp <= @to
            ORDER BY sequence
            """;
        cmd.Parameters.AddWithValue("@type", typeof(T).Name);
        cmd.Parameters.AddWithValue("@from", from.ToString("O"));
        cmd.Parameters.AddWithValue("@to", to.ToString("O"));

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var json = reader.GetString(0);
            var evt = JsonSerializer.Deserialize<T>(json, JsonOptions);
            if (evt is not null)
                yield return evt;
        }
    }

    public async Task<long> GetLatestSequenceAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(sequence), 0) FROM events";
        return (long)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async ValueTask DisposeAsync()
    {
        await Task.CompletedTask;
    }

    // ── Helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Deserialize an event from its type name and JSON payload.
    /// Maps type names back to their concrete LifeEvent subtypes.
    /// </summary>
    private static LifeEvent? DeserializeEvent(string typeName, string json)
    {
        return typeName switch
        {
            nameof(TaskCreated) => JsonSerializer.Deserialize<TaskCreated>(json, JsonOptions),
            nameof(TaskDelegated) => JsonSerializer.Deserialize<TaskDelegated>(json, JsonOptions),
            nameof(TaskCompleted) => JsonSerializer.Deserialize<TaskCompleted>(json, JsonOptions),
            nameof(TaskFailed) => JsonSerializer.Deserialize<TaskFailed>(json, JsonOptions),
            nameof(TaskCancelled) => JsonSerializer.Deserialize<TaskCancelled>(json, JsonOptions),
            nameof(HumanApprovalRequested) => JsonSerializer.Deserialize<HumanApprovalRequested>(json, JsonOptions),
            nameof(HumanApprovalReceived) => JsonSerializer.Deserialize<HumanApprovalReceived>(json, JsonOptions),
            nameof(HumanApprovalTimedOut) => JsonSerializer.Deserialize<HumanApprovalTimedOut>(json, JsonOptions),
            nameof(UserFeedbackReceived) => JsonSerializer.Deserialize<UserFeedbackReceived>(json, JsonOptions),
            nameof(AudioSegmentTranscribed) => JsonSerializer.Deserialize<AudioSegmentTranscribed>(json, JsonOptions),
            nameof(SpeakerIdentified) => JsonSerializer.Deserialize<SpeakerIdentified>(json, JsonOptions),
            nameof(ConversationSummarized) => JsonSerializer.Deserialize<ConversationSummarized>(json, JsonOptions),
            nameof(SpokenCommitmentDetected) => JsonSerializer.Deserialize<SpokenCommitmentDetected>(json, JsonOptions),
            nameof(SpeakerEnrolled) => JsonSerializer.Deserialize<SpeakerEnrolled>(json, JsonOptions),
            nameof(AudioRecordingStateChanged) => JsonSerializer.Deserialize<AudioRecordingStateChanged>(json, JsonOptions),
            nameof(ScheduledTriggerFired) => JsonSerializer.Deserialize<ScheduledTriggerFired>(json, JsonOptions),
            nameof(WebhookReceived) => JsonSerializer.Deserialize<WebhookReceived>(json, JsonOptions),
            nameof(BudgetThresholdReached) => JsonSerializer.Deserialize<BudgetThresholdReached>(json, JsonOptions),
            nameof(ProactiveOpportunityDetected) => JsonSerializer.Deserialize<ProactiveOpportunityDetected>(json, JsonOptions),
            nameof(ProactiveSuggestionSent) => JsonSerializer.Deserialize<ProactiveSuggestionSent>(json, JsonOptions),
            nameof(ProactiveSuggestionDismissed) => JsonSerializer.Deserialize<ProactiveSuggestionDismissed>(json, JsonOptions),
            _ => null,
        };
    }

    private static async Task ExecuteAsync(SqliteConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
