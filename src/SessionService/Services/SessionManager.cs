using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Common.Interfaces.Session;
using Common.Models.Session;

namespace SessionService.Services;

/// <summary>
/// SQLite-based implementation of session management
/// </summary>
public class SessionManager : ISessionManager
{
    private readonly ILogger<SessionManager> _logger;
    private readonly string _connectionString;

    public SessionManager(ILogger<SessionManager> logger, string? databasePath = null)
    {
        _logger = logger;
        
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
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS AgentSessions (
                SessionId         TEXT PRIMARY KEY,
                Name             TEXT NOT NULL,
                CreatedAt        TEXT NOT NULL,
                LastUpdatedAt    TEXT NOT NULL,
                ConversationState TEXT NOT NULL DEFAULT '',
                ConfigurationSnapshot TEXT NOT NULL DEFAULT '',
                Metadata         TEXT NOT NULL DEFAULT '',
                Status           INTEGER NOT NULL DEFAULT 0,
                ActivityLog      TEXT NOT NULL DEFAULT '',
                TaskStateMarkdown TEXT NOT NULL DEFAULT ''
            );
            
            CREATE INDEX IF NOT EXISTS idx_sessions_created_at ON AgentSessions(CreatedAt);
            CREATE INDEX IF NOT EXISTS idx_sessions_status ON AgentSessions(Status);
            """;
        cmd.ExecuteNonQuery();
        
        // Check if new columns exist and add them if they don't
        cmd.CommandText = "PRAGMA table_info(AgentSessions)";
        using var reader = cmd.ExecuteReader();
        bool hasActivityLogColumn = false;
        bool hasTaskStateMarkdownColumn = false;
        while (reader.Read())
        {
            var columnName = reader.GetString(1);
            if (string.Equals(columnName, "ActivityLog", StringComparison.OrdinalIgnoreCase))
                hasActivityLogColumn = true;
            if (string.Equals(columnName, "TaskStateMarkdown", StringComparison.OrdinalIgnoreCase))
                hasTaskStateMarkdownColumn = true;
        }
        reader.Close();

        // helper local to add a column but swallow "duplicate column" race conditions
        void TryAddColumn(string sql)
        {
            try
            {
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 1 &&
                                             ex.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase))
            {
                // Another concurrent initializer already added the column – safe to ignore.
                _logger.LogDebug("Column already exists, skipping: {Sql}", sql);
            }
        }

        if (!hasActivityLogColumn)
        {
            TryAddColumn("ALTER TABLE AgentSessions ADD COLUMN ActivityLog TEXT NOT NULL DEFAULT ''");
        }

        if (!hasTaskStateMarkdownColumn)
        {
            TryAddColumn("ALTER TABLE AgentSessions ADD COLUMN TaskStateMarkdown TEXT NOT NULL DEFAULT ''");
        }
    }

    public async Task<AgentSession> CreateSessionAsync(string name = "")
    {
        var session = AgentSession.CreateNew(name);
        await SaveSessionAsync(session);
        
        _logger.LogInformation("Created new session: {SessionId} - {Name}", session.SessionId, session.Name);
        return session;
    }

    public async Task<AgentSession?> GetSessionAsync(string sessionId)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        
        cmd.CommandText = """
            SELECT SessionId, Name, CreatedAt, LastUpdatedAt, 
                   ConversationState, ConfigurationSnapshot, Metadata, Status, ActivityLog, TaskStateMarkdown
            FROM AgentSessions 
            WHERE SessionId = $sessionId
            """;
        cmd.Parameters.AddWithValue("$sessionId", sessionId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
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
                ActivityLog = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                TaskStateMarkdown = reader.IsDBNull(9) ? string.Empty : reader.GetString(9)
            };
        }

        return null;
    }

    public async Task<AgentSession?> GetSessionByNameAsync(string name)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        
        cmd.CommandText = """
            SELECT SessionId, Name, CreatedAt, LastUpdatedAt, 
                   ConversationState, ConfigurationSnapshot, Metadata, Status, ActivityLog, TaskStateMarkdown
            FROM AgentSessions 
            WHERE Name = $name
            ORDER BY LastUpdatedAt DESC
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("$name", name);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
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
                ActivityLog = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                TaskStateMarkdown = reader.IsDBNull(9) ? string.Empty : reader.GetString(9)
            };
        }

        return null;
    }

    public async Task SaveSessionAsync(AgentSession session)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        
        cmd.CommandText = """
            INSERT OR REPLACE INTO AgentSessions 
            (SessionId, Name, CreatedAt, LastUpdatedAt, ConversationState, ConfigurationSnapshot, Metadata, Status, ActivityLog, TaskStateMarkdown)
            VALUES ($sessionId, $name, $createdAt, $lastUpdatedAt, $conversationState, $configSnapshot, $metadata, $status, $activityLog, $taskStateMarkdown)
            """;
            
        cmd.Parameters.AddWithValue("$sessionId", session.SessionId);
        cmd.Parameters.AddWithValue("$name", session.Name);
        cmd.Parameters.AddWithValue("$createdAt", session.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$lastUpdatedAt", session.LastUpdatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$conversationState", session.ConversationState);
        cmd.Parameters.AddWithValue("$configSnapshot", session.ConfigurationSnapshot);
        cmd.Parameters.AddWithValue("$metadata", session.Metadata);
        cmd.Parameters.AddWithValue("$status", (int)session.Status);
        cmd.Parameters.AddWithValue("$activityLog", session.ActivityLog);
        cmd.Parameters.AddWithValue("$taskStateMarkdown", session.TaskStateMarkdown);

        await cmd.ExecuteNonQueryAsync();
        
        _logger.LogDebug("Saved session: {SessionId}", session.SessionId);
    }

    public async Task<IReadOnlyList<AgentSession>> ListSessionsAsync()
    {
        var sessions = new List<AgentSession>();
        
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        
        cmd.CommandText = """
            SELECT SessionId, Name, CreatedAt, LastUpdatedAt, 
                   ConversationState, ConfigurationSnapshot, Metadata, Status, ActivityLog, TaskStateMarkdown
            FROM AgentSessions 
            ORDER BY LastUpdatedAt DESC
            """;

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            sessions.Add(new AgentSession
            {
                SessionId = reader.GetString(0),
                Name = reader.GetString(1),
                CreatedAt = DateTime.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind),
                LastUpdatedAt = DateTime.Parse(reader.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind),
                ConversationState = reader.GetString(4),
                ConfigurationSnapshot = reader.GetString(5),
                Metadata = reader.GetString(6),
                ActivityLog = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                TaskStateMarkdown = reader.IsDBNull(9) ? string.Empty : reader.GetString(9)
            });
        }

        return sessions;
    }

    public async Task<bool> DeleteSessionAsync(string sessionId)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        
        cmd.CommandText = "DELETE FROM AgentSessions WHERE SessionId = $sessionId";
        cmd.Parameters.AddWithValue("$sessionId", sessionId);

        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        var deleted = rowsAffected > 0;
        
        if (deleted)
        {
            _logger.LogInformation("Deleted session: {SessionId}", sessionId);
        }
        
        return deleted;
    }

    public async Task<bool> ArchiveSessionAsync(string sessionId)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        
        cmd.CommandText = """
            UPDATE AgentSessions 
            SET Status = $status, LastUpdatedAt = $lastUpdatedAt
            WHERE SessionId = $sessionId
            """;
        cmd.Parameters.AddWithValue("$status", (int)SessionStatus.Archived);
        cmd.Parameters.AddWithValue("$lastUpdatedAt", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$sessionId", sessionId);

        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        var archived = rowsAffected > 0;
        
        if (archived)
        {
            _logger.LogInformation("Archived session: {SessionId}", sessionId);
        }
        
        return archived;
    }
}