using System.Text.Json;
using LifeAgent.Core;
using LifeAgent.Core.Audio;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace LifeAgent.Audio.Pipeline;

/// <summary>
/// SQLite-backed implementation of <see cref="IConversationalMemory"/>.
/// Stores speaker-attributed transcripts, conversations, and the speaker gallery.
///
/// Schema design:
/// - segments: individual utterances with full-text search via FTS5
/// - conversations: finalized conversation metadata + JSON summaries
/// - speakers: enrolled speaker gallery with serialized embeddings
///
/// Uses raw ADO.NET (Microsoft.Data.Sqlite) instead of EF Core for:
/// - FTS5 support (EF Core doesn't model virtual tables well)
/// - Float array (embedding) storage via BLOB
/// - Maximum control over WAL mode and connection pooling
/// </summary>
public sealed class SqliteConversationalMemory : IConversationalMemory, IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly ILogger<SqliteConversationalMemory> _logger;

    public SqliteConversationalMemory(string dbPath, ILogger<SqliteConversationalMemory> logger)
    {
        _connectionString = $"Data Source={dbPath};Mode=ReadWriteCreate;Cache=Shared";
        _logger = logger;
    }

    /// <summary>
    /// Initialize the database schema. Must be called once on startup.
    /// Creates tables, FTS5 index, and enables WAL mode.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        // WAL mode for concurrent reads + single writer
        await ExecuteAsync(conn, "PRAGMA journal_mode=WAL;", ct);
        await ExecuteAsync(conn, "PRAGMA synchronous=NORMAL;", ct);

        // Core tables
        await ExecuteAsync(conn, """
            CREATE TABLE IF NOT EXISTS segments (
                id TEXT PRIMARY KEY,
                conversation_id TEXT NOT NULL,
                transcript TEXT NOT NULL,
                speaker_label TEXT,
                speaker_name TEXT,
                confidence REAL NOT NULL,
                started_at TEXT NOT NULL,
                duration_ms INTEGER NOT NULL,
                speaker_embedding BLOB,
                created_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
            """, ct);

        await ExecuteAsync(conn, """
            CREATE TABLE IF NOT EXISTS conversations (
                id TEXT PRIMARY KEY,
                started_at TEXT NOT NULL,
                ended_at TEXT,
                duration_ms INTEGER,
                participants_json TEXT,
                summary TEXT,
                action_items_json TEXT,
                entities_json TEXT,
                topics_json TEXT,
                created_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
            """, ct);

        await ExecuteAsync(conn, """
            CREATE TABLE IF NOT EXISTS speakers (
                name TEXT PRIMARY KEY,
                embeddings_json TEXT NOT NULL,
                enrolled_at TEXT NOT NULL,
                last_seen_at TEXT,
                total_utterances INTEGER NOT NULL DEFAULT 0
            );
            """, ct);

        // FTS5 virtual table for full-text search over transcripts
        await ExecuteAsync(conn, """
            CREATE VIRTUAL TABLE IF NOT EXISTS segments_fts USING fts5(
                transcript,
                content='segments',
                content_rowid='rowid'
            );
            """, ct);

        // Triggers to keep FTS index in sync
        await ExecuteAsync(conn, """
            CREATE TRIGGER IF NOT EXISTS segments_ai AFTER INSERT ON segments BEGIN
                INSERT INTO segments_fts(rowid, transcript)
                VALUES (new.rowid, new.transcript);
            END;
            """, ct);

        // Indexes
        await ExecuteAsync(conn, "CREATE INDEX IF NOT EXISTS idx_segments_conversation ON segments(conversation_id);", ct);
        await ExecuteAsync(conn, "CREATE INDEX IF NOT EXISTS idx_segments_started ON segments(started_at);", ct);
        await ExecuteAsync(conn, "CREATE INDEX IF NOT EXISTS idx_segments_speaker ON segments(speaker_name);", ct);
        await ExecuteAsync(conn, "CREATE INDEX IF NOT EXISTS idx_conversations_started ON conversations(started_at);", ct);

        _logger.LogInformation("[MEMORY] Conversational memory initialized at {Path}", _connectionString);
    }

    // ── Write operations ───────────────────────────────────────────

    public async Task StoreSegmentAsync(TranscriptSegment segment, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO segments
                (id, conversation_id, transcript, speaker_label, speaker_name, confidence, started_at, duration_ms, speaker_embedding)
            VALUES
                (@id, @convId, @transcript, @label, @name, @confidence, @startedAt, @durationMs, @embedding)
            """;
        cmd.Parameters.AddWithValue("@id", segment.Id);
        cmd.Parameters.AddWithValue("@convId", segment.ConversationId);
        cmd.Parameters.AddWithValue("@transcript", segment.Transcript);
        cmd.Parameters.AddWithValue("@label", (object?)segment.SpeakerLabel ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@name", (object?)segment.SpeakerName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@confidence", segment.Confidence);
        cmd.Parameters.AddWithValue("@startedAt", segment.StartedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@durationMs", (long)segment.Duration.TotalMilliseconds);
        cmd.Parameters.AddWithValue("@embedding",
            segment.SpeakerEmbedding is not null
                ? FloatsToBlob(segment.SpeakerEmbedding)
                : DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task StoreConversationAsync(Conversation conversation, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO conversations
                (id, started_at, ended_at, duration_ms, participants_json, summary, action_items_json, entities_json, topics_json)
            VALUES
                (@id, @startedAt, @endedAt, @durationMs, @participants, @summary, @actionItems, @entities, @topics)
            """;
        cmd.Parameters.AddWithValue("@id", conversation.Id);
        cmd.Parameters.AddWithValue("@startedAt", conversation.StartedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@endedAt", conversation.EndedAt?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@durationMs", (long)(conversation.TotalDuration?.TotalMilliseconds ?? 0));
        cmd.Parameters.AddWithValue("@participants", JsonSerializer.Serialize(conversation.Participants));
        cmd.Parameters.AddWithValue("@summary", (object?)conversation.Summary ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@actionItems", JsonSerializer.Serialize(conversation.ActionItems));
        cmd.Parameters.AddWithValue("@entities", JsonSerializer.Serialize(conversation.Entities));
        cmd.Parameters.AddWithValue("@topics", JsonSerializer.Serialize(conversation.Topics));

        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ── Speaker gallery ────────────────────────────────────────────

    public async Task<IReadOnlyList<SpeakerProfile>> GetAllSpeakersAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name, embeddings_json, enrolled_at, last_seen_at, total_utterances FROM speakers";

        var profiles = new List<SpeakerProfile>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            profiles.Add(new SpeakerProfile
            {
                Name = reader.GetString(0),
                Embeddings = JsonSerializer.Deserialize<List<SpeakerEmbedding>>(reader.GetString(1)) ?? [],
                EnrolledAt = DateTimeOffset.Parse(reader.GetString(2)),
                LastSeenAt = reader.IsDBNull(3) ? default : DateTimeOffset.Parse(reader.GetString(3)),
                TotalUtterances = reader.GetInt64(4),
            });
        }

        return profiles;
    }

    public async Task EnrollSpeakerAsync(SpeakerProfile profile, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO speakers (name, embeddings_json, enrolled_at, last_seen_at, total_utterances)
            VALUES (@name, @embeddings, @enrolledAt, @lastSeen, @utterances)
            """;
        cmd.Parameters.AddWithValue("@name", profile.Name);
        cmd.Parameters.AddWithValue("@embeddings", JsonSerializer.Serialize(profile.Embeddings));
        cmd.Parameters.AddWithValue("@enrolledAt", profile.EnrolledAt.ToString("O"));
        cmd.Parameters.AddWithValue("@lastSeen", profile.LastSeenAt == default ? DBNull.Value : profile.LastSeenAt.ToString("O"));
        cmd.Parameters.AddWithValue("@utterances", profile.TotalUtterances);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<SpeakerProfile?> GetSpeakerAsync(string name, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name, embeddings_json, enrolled_at, last_seen_at, total_utterances FROM speakers WHERE name = @name";
        cmd.Parameters.AddWithValue("@name", name);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new SpeakerProfile
        {
            Name = reader.GetString(0),
            Embeddings = JsonSerializer.Deserialize<List<SpeakerEmbedding>>(reader.GetString(1)) ?? [],
            EnrolledAt = DateTimeOffset.Parse(reader.GetString(2)),
            LastSeenAt = reader.IsDBNull(3) ? default : DateTimeOffset.Parse(reader.GetString(3)),
            TotalUtterances = reader.GetInt64(4),
        };
    }

    public async Task UpdateSpeakerLastSeenAsync(string name, DateTimeOffset lastSeen, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE speakers SET last_seen_at = @lastSeen, total_utterances = total_utterances + 1
            WHERE name = @name
            """;
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@lastSeen", lastSeen.ToString("O"));

        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ── Query operations ───────────────────────────────────────────

    public async Task<IReadOnlyList<TranscriptSegment>> SearchTranscriptsAsync(
        string query, DateTimeOffset? from, DateTimeOffset? to,
        string? speakerName, int maxResults, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        // Use FTS5 for text search, with optional metadata filters
        await using var cmd = conn.CreateCommand();
        var whereClauses = new List<string>();
        var usesFts = !string.IsNullOrWhiteSpace(query);

        if (usesFts)
        {
            cmd.CommandText = """
                SELECT s.id, s.conversation_id, s.transcript, s.speaker_label, s.speaker_name,
                       s.confidence, s.started_at, s.duration_ms
                FROM segments s
                INNER JOIN segments_fts fts ON s.rowid = fts.rowid
                WHERE segments_fts MATCH @query
                """;
            cmd.Parameters.AddWithValue("@query", query);
        }
        else
        {
            cmd.CommandText = """
                SELECT s.id, s.conversation_id, s.transcript, s.speaker_label, s.speaker_name,
                       s.confidence, s.started_at, s.duration_ms
                FROM segments s
                WHERE 1=1
                """;
        }

        if (from.HasValue)
        {
            cmd.CommandText += " AND s.started_at >= @from";
            cmd.Parameters.AddWithValue("@from", from.Value.ToString("O"));
        }

        if (to.HasValue)
        {
            cmd.CommandText += " AND s.started_at <= @to";
            cmd.Parameters.AddWithValue("@to", to.Value.ToString("O"));
        }

        if (speakerName is not null)
        {
            cmd.CommandText += " AND s.speaker_name = @speaker";
            cmd.Parameters.AddWithValue("@speaker", speakerName);
        }

        cmd.CommandText += " ORDER BY s.started_at DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("@limit", maxResults);

        var results = new List<TranscriptSegment>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new TranscriptSegment
            {
                Id = reader.GetString(0),
                ConversationId = reader.GetString(1),
                Transcript = reader.GetString(2),
                SpeakerLabel = reader.IsDBNull(3) ? null : reader.GetString(3),
                SpeakerName = reader.IsDBNull(4) ? null : reader.GetString(4),
                Confidence = reader.GetFloat(5),
                StartedAt = DateTimeOffset.Parse(reader.GetString(6)),
                Duration = TimeSpan.FromMilliseconds(reader.GetInt64(7)),
            });
        }

        return results;
    }

    public async Task<IReadOnlyList<Conversation>> GetConversationsAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, started_at, ended_at, duration_ms, participants_json, summary, action_items_json, entities_json, topics_json
            FROM conversations
            WHERE started_at >= @from AND started_at <= @to
            ORDER BY started_at DESC
            """;
        cmd.Parameters.AddWithValue("@from", from.ToString("O"));
        cmd.Parameters.AddWithValue("@to", to.ToString("O"));

        var results = new List<Conversation>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(ReadConversation(reader));
        }

        return results;
    }

    public async Task<Conversation?> GetConversationAsync(string conversationId, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        // Get conversation metadata
        await using var convCmd = conn.CreateCommand();
        convCmd.CommandText = """
            SELECT id, started_at, ended_at, duration_ms, participants_json, summary, action_items_json, entities_json, topics_json
            FROM conversations WHERE id = @id
            """;
        convCmd.Parameters.AddWithValue("@id", conversationId);

        await using var convReader = await convCmd.ExecuteReaderAsync(ct);
        if (!await convReader.ReadAsync(ct)) return null;

        var conversation = ReadConversation(convReader);

        // Load segments
        await using var segCmd = conn.CreateCommand();
        segCmd.CommandText = """
            SELECT id, conversation_id, transcript, speaker_label, speaker_name, confidence, started_at, duration_ms
            FROM segments WHERE conversation_id = @convId ORDER BY started_at
            """;
        segCmd.Parameters.AddWithValue("@convId", conversationId);

        await using var segReader = await segCmd.ExecuteReaderAsync(ct);
        while (await segReader.ReadAsync(ct))
        {
            conversation.Segments.Add(new TranscriptSegment
            {
                Id = segReader.GetString(0),
                ConversationId = segReader.GetString(1),
                Transcript = segReader.GetString(2),
                SpeakerLabel = segReader.IsDBNull(3) ? null : segReader.GetString(3),
                SpeakerName = segReader.IsDBNull(4) ? null : segReader.GetString(4),
                Confidence = segReader.GetFloat(5),
                StartedAt = DateTimeOffset.Parse(segReader.GetString(6)),
                Duration = TimeSpan.FromMilliseconds(segReader.GetInt64(7)),
            });
        }

        return conversation;
    }

    public async Task<DailyConversationStats> GetDailyStatsAsync(DateOnly date, CancellationToken ct = default)
    {
        var from = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var to = from.AddDays(1);

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        // Conversation count and total duration
        await using var statsCmd = conn.CreateCommand();
        statsCmd.CommandText = """
            SELECT COUNT(*), COALESCE(SUM(duration_ms), 0)
            FROM conversations WHERE started_at >= @from AND started_at < @to
            """;
        statsCmd.Parameters.AddWithValue("@from", from.ToString("O"));
        statsCmd.Parameters.AddWithValue("@to", to.ToString("O"));

        await using var statsReader = await statsCmd.ExecuteReaderAsync(ct);
        await statsReader.ReadAsync(ct);
        var count = statsReader.GetInt32(0);
        var durationMs = statsReader.GetInt64(1);

        // Top speakers
        await using var speakerCmd = conn.CreateCommand();
        speakerCmd.CommandText = """
            SELECT speaker_name, COUNT(*) as cnt
            FROM segments
            WHERE started_at >= @from AND started_at < @to AND speaker_name IS NOT NULL
            GROUP BY speaker_name ORDER BY cnt DESC LIMIT 5
            """;
        speakerCmd.Parameters.AddWithValue("@from", from.ToString("O"));
        speakerCmd.Parameters.AddWithValue("@to", to.ToString("O"));

        var speakers = new List<string>();
        await using var spkReader = await speakerCmd.ExecuteReaderAsync(ct);
        while (await spkReader.ReadAsync(ct))
            speakers.Add(spkReader.GetString(0));

        // Top topics from conversations
        await using var topicCmd = conn.CreateCommand();
        topicCmd.CommandText = """
            SELECT topics_json FROM conversations
            WHERE started_at >= @from AND started_at < @to AND topics_json IS NOT NULL
            """;
        topicCmd.Parameters.AddWithValue("@from", from.ToString("O"));
        topicCmd.Parameters.AddWithValue("@to", to.ToString("O"));

        var allTopics = new List<string>();
        await using var topicReader = await topicCmd.ExecuteReaderAsync(ct);
        while (await topicReader.ReadAsync(ct))
        {
            var topics = JsonSerializer.Deserialize<List<string>>(topicReader.GetString(0));
            if (topics is not null) allTopics.AddRange(topics);
        }

        var topTopics = allTopics
            .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => g.Key)
            .ToList();

        return new DailyConversationStats(
            count,
            TimeSpan.FromMilliseconds(durationMs),
            speakers,
            topTopics,
            UnresolvedActionItems: 0 // TODO: cross-reference with task store
        );
    }

    public async ValueTask DisposeAsync()
    {
        // SqliteConnection pooling handles cleanup; nothing extra needed
        await Task.CompletedTask;
    }

    // ── Helpers ────────────────────────────────────────────────────

    private static Conversation ReadConversation(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        StartedAt = DateTimeOffset.Parse(reader.GetString(1)),
        EndedAt = reader.IsDBNull(2) ? null : DateTimeOffset.Parse(reader.GetString(2)),
        Summary = reader.IsDBNull(5) ? null : reader.GetString(5),
        ActionItems = reader.IsDBNull(6) ? [] : JsonSerializer.Deserialize<List<string>>(reader.GetString(6)) ?? [],
        Entities = reader.IsDBNull(7) ? [] : JsonSerializer.Deserialize<List<string>>(reader.GetString(7)) ?? [],
        Topics = reader.IsDBNull(8) ? [] : JsonSerializer.Deserialize<List<string>>(reader.GetString(8)) ?? [],
    };

    private static byte[] FloatsToBlob(float[] floats)
    {
        var bytes = new byte[floats.Length * sizeof(float)];
        Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static async Task ExecuteAsync(SqliteConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
