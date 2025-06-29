using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Common.Interfaces.Session;
using Common.Models.Session;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SessionService.Services;

/// <summary>
/// SQLite-based implementation of session management
/// </summary>
public class SessionManager : ISessionManager
{
    private readonly ILogger<SessionManager> _logger;
    private readonly string _connectionString;
    private readonly JsonSerializerOptions _jsonOptions;

    // Helper class to exclude ActivityLog from serialization
    private class LoggableAgentSession
    {
        public string SessionId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }
        public string ConversationState { get; set; } = string.Empty;
        public string ConfigurationSnapshot { get; set; } = string.Empty;
        public string Metadata { get; set; } = string.Empty;
        public SessionStatus Status { get; set; }
        public string TaskStateMarkdown { get; set; } = string.Empty;
        
        public static LoggableAgentSession FromAgentSession(AgentSession session) => new()
        {
            SessionId             = session.SessionId,
            Name                  = session.Name,
            CreatedAt             = session.CreatedAt,
            LastUpdatedAt         = session.LastUpdatedAt,
            ConversationState     = session.ConversationState,
            ConfigurationSnapshot = session.ConfigurationSnapshot,
            Metadata              = session.Metadata,
            Status                = session.Status,
            TaskStateMarkdown     = session.TaskStateMarkdown
        };
    }

    public SessionManager(ILogger<SessionManager> logger, string? databasePath = null)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions 
        { 
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        
        // Prefer explicitly provided path, then environment variable, then default
        var dbPath = databasePath                                   // 1) caller-supplied
                     ?? Environment.GetEnvironmentVariable("AGENT_SESSION_DB_PATH") // 2) env var
                     ?? "./data/agent_sessions.db";                 // 3) fallback

        // Ensure directory exists
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
                    
        // Explicitly configure connection for read-write-create access
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode       = SqliteOpenMode.ReadWriteCreate,
            Cache      = SqliteCacheMode.Shared
        };
        _connectionString = builder.ToString();

        EnsureDatabase();
    }

    private void EnsureDatabase()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS AgentSessions (
                SessionId         TEXT PRIMARY KEY,
                Name             TEXT NOT NULL,
                CreatedAt        TEXT NOT NULL,
                LastUpdatedAt    TEXT NOT NULL,
                ConversationState TEXT NOT NULL DEFAULT '',
                ConfigurationSnapshot TEXT NOT NULL DEFAULT '',
                Metadata         TEXT NOT NULL DEFAULT '',
                Status           INTEGER NOT NULL DEFAULT 0,
                TaskStateMarkdown TEXT NOT NULL DEFAULT ''
            );
            CREATE INDEX IF NOT EXISTS idx_sessions_created_at ON AgentSessions(CreatedAt);
            CREATE INDEX IF NOT EXISTS idx_sessions_status ON AgentSessions(Status);

            CREATE TABLE IF NOT EXISTS SessionActivities (
                ActivityId      TEXT PRIMARY KEY,
                SessionId       TEXT NOT NULL,
                Timestamp       TEXT NOT NULL,
                ActivityType    TEXT NOT NULL,
                Description     TEXT NOT NULL,
                Data            TEXT NOT NULL DEFAULT '',
                DurationMs      INTEGER,
                Success         INTEGER NOT NULL DEFAULT 1,
                ErrorMessage    TEXT,
                FOREIGN KEY(SessionId) REFERENCES AgentSessions(SessionId) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS idx_activities_session_id ON SessionActivities(SessionId);
            CREATE INDEX IF NOT EXISTS idx_activities_timestamp ON SessionActivities(Timestamp);
        ";
        cmd.ExecuteNonQuery();
    }

    private static AgentSession ReadAgentSession(SqliteDataReader reader)
    {
        return new AgentSession
        {
            SessionId = reader.GetString(0),
            Name = reader.GetString(1),
            CreatedAt = DateTime.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind),
            LastUpdatedAt = DateTime.Parse(reader.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind),
            ConversationState = reader.GetString(4),
            ConfigurationSnapshot = reader.GetString(5),
            Metadata = reader.GetString(6),
            Status = (SessionStatus)reader.GetInt32(7),
            TaskStateMarkdown = reader.IsDBNull(8) ? string.Empty : reader.GetString(8)
        };
    }

    public async Task<AgentSession> CreateSessionAsync(string name = "")
    {
        var now = DateTime.UtcNow; // single, precise timestamp
        var session = new AgentSession
        {
            SessionId      = Guid.NewGuid().ToString(),
            Name           = string.IsNullOrEmpty(name) ? $"Session {now:yyyyMMdd_HHmmss}" : name,
            CreatedAt      = now,
            LastUpdatedAt  = now,
            Status         = SessionStatus.Active
        };

        // Don't access ActivityLog directly - activities are stored separately
        
        await SaveSessionAsync(session);
        _logger?.LogInformation("Created new session {SessionId} with name {Name}", session.SessionId, session.Name);
        
        return session;
    }

    public async Task<AgentSession?> GetSessionAsync(string sessionId)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        
        cmd.CommandText = """
            SELECT SessionId, Name, CreatedAt, LastUpdatedAt, 
                   ConversationState, ConfigurationSnapshot, Metadata, Status, TaskStateMarkdown
            FROM AgentSessions 
            WHERE SessionId = $sessionId
            """;
        cmd.Parameters.AddWithValue("$sessionId", sessionId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var session = ReadAgentSession(reader);
            _logger.LogInformation("Fetched session by id: {SessionJson}",
                JsonSerializer.Serialize(LoggableAgentSession.FromAgentSession(session), _jsonOptions));
            return session;
        }

        _logger.LogWarning("Session not found: {SessionId}", sessionId);
        return null;
    }

    public async Task<AgentSession?> GetSessionByNameAsync(string name)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        
        cmd.CommandText = """
            SELECT SessionId, Name, CreatedAt, LastUpdatedAt, 
                   ConversationState, ConfigurationSnapshot, Metadata, Status, TaskStateMarkdown
            FROM AgentSessions 
            WHERE Name = $name
            ORDER BY LastUpdatedAt DESC
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("$name", name);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var session = ReadAgentSession(reader);
            _logger.LogInformation("Fetched session by name: {SessionJson}",
                JsonSerializer.Serialize(LoggableAgentSession.FromAgentSession(session), _jsonOptions));
            return session;
        }

        _logger.LogWarning("Session with name '{Name}' not found", name);
        return null;
    }

    public async Task SaveSessionAsync(AgentSession session)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();

        // Up-sert by SessionId
        cmd.CommandText = @"
            INSERT INTO AgentSessions (
                SessionId, Name, CreatedAt, LastUpdatedAt,
                ConversationState, ConfigurationSnapshot, Metadata, Status, TaskStateMarkdown
            )
            VALUES (
                $id, $name, $created, $updated,
                $conv, $config, $meta, $status, $markdown
            )
            ON CONFLICT(SessionId) DO UPDATE SET
                Name                  = excluded.Name,
                LastUpdatedAt         = excluded.LastUpdatedAt,
                ConversationState     = excluded.ConversationState,
                ConfigurationSnapshot = excluded.ConfigurationSnapshot,
                Metadata              = excluded.Metadata,
                Status                = excluded.Status,
                TaskStateMarkdown     = excluded.TaskStateMarkdown
            ";
        cmd.Parameters.AddWithValue("$id",       session.SessionId);
        cmd.Parameters.AddWithValue("$name",     session.Name);
        cmd.Parameters.AddWithValue("$created",  session.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$updated",  session.LastUpdatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$conv",     session.ConversationState);
        cmd.Parameters.AddWithValue("$config",   session.ConfigurationSnapshot);
        cmd.Parameters.AddWithValue("$meta",     session.Metadata);
        cmd.Parameters.AddWithValue("$status",   (int)session.Status);
        cmd.Parameters.AddWithValue("$markdown", session.TaskStateMarkdown);

        await cmd.ExecuteNonQueryAsync();
        _logger.LogDebug("Saved session {SessionId}", session.SessionId);
    }

    // --------------------------------------------------------------------
    // New methods — store activities in the SessionActivities table
    // --------------------------------------------------------------------
    public async Task AddSessionActivityAsync(string sessionId, SessionActivity activity)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            INSERT INTO SessionActivities (
                ActivityId, SessionId, Timestamp,
                ActivityType, Description, Data,
                DurationMs, Success, ErrorMessage
            )
            VALUES (
                $id, $sessionId, $timestamp,
                $type, $desc, $data,
                $duration, $success, $error
            )";
        cmd.Parameters.AddWithValue("$id",        activity.ActivityId);
        cmd.Parameters.AddWithValue("$sessionId", sessionId);
        cmd.Parameters.AddWithValue("$timestamp", activity.Timestamp.ToString("O"));
        cmd.Parameters.AddWithValue("$type",      activity.ActivityType);
        cmd.Parameters.AddWithValue("$desc",      activity.Description);
        cmd.Parameters.AddWithValue("$data",      activity.Data ?? string.Empty);
        cmd.Parameters.AddWithValue("$duration",  (object?)activity.DurationMs ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$success",   activity.Success ? 1 : 0);
        cmd.Parameters.AddWithValue("$error",     (object?)activity.ErrorMessage ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
        _logger.LogTrace("Added activity {ActivityId} to session {SessionId}", activity.ActivityId, sessionId);
    }

    public async Task<List<SessionActivity>> GetSessionActivitiesAsync(string sessionId)
    {
        var list = new List<SessionActivity>();

        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            SELECT ActivityId, Timestamp, ActivityType, Description, Data,
                   DurationMs, Success, ErrorMessage
            FROM SessionActivities
            WHERE SessionId = $sessionId
            ORDER BY Timestamp ASC
        ";
        cmd.Parameters.AddWithValue("$sessionId", sessionId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new SessionActivity
            {
                ActivityId   = reader.GetString(0),
                Timestamp    = DateTime.Parse(reader.GetString(1), null, System.Globalization.DateTimeStyles.RoundtripKind),
                ActivityType = reader.GetString(2),
                Description  = reader.GetString(3),
                Data         = reader.GetString(4),
                DurationMs   = reader.IsDBNull(5) ? null : reader.GetInt64(5),
                Success      = reader.GetInt32(6) == 1,
                ErrorMessage = reader.IsDBNull(7) ? null : reader.GetString(7),
                SessionId    = sessionId
            });
        }
        return list;
    }

    // --------------------------------------------------------------------
    // Utility queries needed by API controllers / client
    // --------------------------------------------------------------------
    public async Task<IReadOnlyList<AgentSession>> ListSessionsAsync()
    {
        var list = new List<AgentSession>();

        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT SessionId, Name, CreatedAt, LastUpdatedAt, 
                   ConversationState, ConfigurationSnapshot, Metadata, Status, TaskStateMarkdown
            FROM AgentSessions
            ORDER BY LastUpdatedAt DESC
        """;

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add(ReadAgentSession(reader));

        return list;
    }

    public async Task<bool> DeleteSessionAsync(string sessionId)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM AgentSessions WHERE SessionId = $id";
        cmd.Parameters.AddWithValue("$id", sessionId);

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<bool> ArchiveSessionAsync(string sessionId)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE AgentSessions
            SET Status = $status, LastUpdatedAt = $ts
            WHERE SessionId = $id
        ";
        cmd.Parameters.AddWithValue("$status", (int)SessionStatus.Archived);
        cmd.Parameters.AddWithValue("$ts",     DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$id",     sessionId);

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }
}