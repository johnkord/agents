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
                CurrentPlan      TEXT NOT NULL DEFAULT '',
                ActivityLog      TEXT NOT NULL DEFAULT '',
                TaskTitle        TEXT NOT NULL DEFAULT '',
                TaskStatus       INTEGER NOT NULL DEFAULT 0,
                CurrentStep      INTEGER NOT NULL DEFAULT 0,
                TotalSteps       INTEGER NOT NULL DEFAULT 0,
                CompletedSteps   INTEGER NOT NULL DEFAULT 0,
                ProgressPercentage REAL NOT NULL DEFAULT 0.0,
                TaskStartedAt    TEXT,
                TaskCompletedAt  TEXT,
                TaskCategory     TEXT NOT NULL DEFAULT '',
                TaskPriority     INTEGER NOT NULL DEFAULT 2,
                TaskTags         TEXT NOT NULL DEFAULT '',
                EstimatedDuration INTEGER,
                ActualDuration   INTEGER
            );
            
            CREATE INDEX IF NOT EXISTS idx_sessions_created_at ON AgentSessions(CreatedAt);
            CREATE INDEX IF NOT EXISTS idx_sessions_status ON AgentSessions(Status);
            CREATE INDEX IF NOT EXISTS idx_sessions_task_status ON AgentSessions(TaskStatus);
            CREATE INDEX IF NOT EXISTS idx_sessions_task_category ON AgentSessions(TaskCategory);
            CREATE INDEX IF NOT EXISTS idx_sessions_task_priority ON AgentSessions(TaskPriority);
            CREATE INDEX IF NOT EXISTS idx_sessions_progress ON AgentSessions(ProgressPercentage);
            """;
        cmd.ExecuteNonQuery();
        
        // Migrate existing schema to add new columns
        MigrateSchema(conn);
    }
    
    private void MigrateSchema(SqliteConnection conn)
    {
        // Get current column information
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA table_info(AgentSessions)";
        using var reader = cmd.ExecuteReader();
        
        var existingColumns = new HashSet<string>();
        while (reader.Read())
        {
            existingColumns.Add(reader.GetString(1)); // Column name is at index 1
        }
        reader.Close();
        
        // Define new columns with their default values in dependency order
        var newColumns = new List<(string name, string statement)>
        {
            ("CurrentPlan", "ALTER TABLE AgentSessions ADD COLUMN CurrentPlan TEXT NOT NULL DEFAULT ''"),
            ("ActivityLog", "ALTER TABLE AgentSessions ADD COLUMN ActivityLog TEXT NOT NULL DEFAULT ''"),
            ("TaskTitle", "ALTER TABLE AgentSessions ADD COLUMN TaskTitle TEXT NOT NULL DEFAULT ''"),
            ("TaskStatus", "ALTER TABLE AgentSessions ADD COLUMN TaskStatus INTEGER NOT NULL DEFAULT 0"),
            ("CurrentStep", "ALTER TABLE AgentSessions ADD COLUMN CurrentStep INTEGER NOT NULL DEFAULT 0"),
            ("TotalSteps", "ALTER TABLE AgentSessions ADD COLUMN TotalSteps INTEGER NOT NULL DEFAULT 0"),
            ("CompletedSteps", "ALTER TABLE AgentSessions ADD COLUMN CompletedSteps INTEGER NOT NULL DEFAULT 0"),
            ("ProgressPercentage", "ALTER TABLE AgentSessions ADD COLUMN ProgressPercentage REAL NOT NULL DEFAULT 0.0"),
            ("TaskStartedAt", "ALTER TABLE AgentSessions ADD COLUMN TaskStartedAt TEXT"),
            ("TaskCompletedAt", "ALTER TABLE AgentSessions ADD COLUMN TaskCompletedAt TEXT"),
            ("TaskCategory", "ALTER TABLE AgentSessions ADD COLUMN TaskCategory TEXT NOT NULL DEFAULT ''"),
            ("TaskPriority", "ALTER TABLE AgentSessions ADD COLUMN TaskPriority INTEGER NOT NULL DEFAULT 2"),
            ("TaskTags", "ALTER TABLE AgentSessions ADD COLUMN TaskTags TEXT NOT NULL DEFAULT ''"),
            ("EstimatedDuration", "ALTER TABLE AgentSessions ADD COLUMN EstimatedDuration INTEGER"),
            ("ActualDuration", "ALTER TABLE AgentSessions ADD COLUMN ActualDuration INTEGER")
        };
        
        // Add missing columns
        foreach (var (columnName, alterStatement) in newColumns)
        {
            if (!existingColumns.Contains(columnName))
            {
                try
                {
                    cmd.CommandText = alterStatement;
                    cmd.ExecuteNonQuery();
                    _logger.LogInformation("Added column {ColumnName} to AgentSessions table", columnName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to add column {ColumnName} to AgentSessions table", columnName);
                }
            }
        }
        
        // Add new indexes if they don't exist
        var indexStatements = new[]
        {
            "CREATE INDEX IF NOT EXISTS idx_sessions_task_status ON AgentSessions(TaskStatus)",
            "CREATE INDEX IF NOT EXISTS idx_sessions_task_category ON AgentSessions(TaskCategory)",
            "CREATE INDEX IF NOT EXISTS idx_sessions_task_priority ON AgentSessions(TaskPriority)",
            "CREATE INDEX IF NOT EXISTS idx_sessions_progress ON AgentSessions(ProgressPercentage)"
        };
        
        foreach (var indexStatement in indexStatements)
        {
            try
            {
                cmd.CommandText = indexStatement;
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create index: {IndexStatement}", indexStatement);
            }
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
                   ConversationState, ConfigurationSnapshot, Metadata, Status, CurrentPlan, ActivityLog,
                   TaskTitle, TaskStatus, CurrentStep, TotalSteps, CompletedSteps, ProgressPercentage,
                   TaskStartedAt, TaskCompletedAt, TaskCategory, TaskPriority, TaskTags, 
                   EstimatedDuration, ActualDuration
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
                CurrentPlan = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                ActivityLog = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                TaskTitle = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                TaskStatus = reader.IsDBNull(11) ? TaskExecutionStatus.NotStarted : (TaskExecutionStatus)reader.GetInt32(11),
                CurrentStep = reader.IsDBNull(12) ? 0 : reader.GetInt32(12),
                TotalSteps = reader.IsDBNull(13) ? 0 : reader.GetInt32(13),
                CompletedSteps = reader.IsDBNull(14) ? 0 : reader.GetInt32(14),
                ProgressPercentage = reader.IsDBNull(15) ? 0.0 : reader.GetDouble(15),
                TaskStartedAt = reader.IsDBNull(16) ? null : DateTime.Parse(reader.GetString(16), null, System.Globalization.DateTimeStyles.RoundtripKind),
                TaskCompletedAt = reader.IsDBNull(17) ? null : DateTime.Parse(reader.GetString(17), null, System.Globalization.DateTimeStyles.RoundtripKind),
                TaskCategory = reader.IsDBNull(18) ? string.Empty : reader.GetString(18),
                TaskPriority = reader.IsDBNull(19) ? 2 : reader.GetInt32(19),
                TaskTags = reader.IsDBNull(20) ? string.Empty : reader.GetString(20),
                EstimatedDuration = reader.IsDBNull(21) ? null : reader.GetInt32(21),
                ActualDuration = reader.IsDBNull(22) ? null : reader.GetInt32(22)
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
                   ConversationState, ConfigurationSnapshot, Metadata, Status, CurrentPlan, ActivityLog,
                   TaskTitle, TaskStatus, CurrentStep, TotalSteps, CompletedSteps, ProgressPercentage,
                   TaskStartedAt, TaskCompletedAt, TaskCategory, TaskPriority, TaskTags, 
                   EstimatedDuration, ActualDuration
            FROM AgentSessions 
            WHERE Name = $name
            ORDER BY LastUpdatedAt DESC
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("$name", name);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return CreateSessionFromReader(reader);
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
            (SessionId, Name, CreatedAt, LastUpdatedAt, ConversationState, ConfigurationSnapshot, Metadata, Status, CurrentPlan, ActivityLog,
             TaskTitle, TaskStatus, CurrentStep, TotalSteps, CompletedSteps, ProgressPercentage,
             TaskStartedAt, TaskCompletedAt, TaskCategory, TaskPriority, TaskTags, EstimatedDuration, ActualDuration)
            VALUES ($sessionId, $name, $createdAt, $lastUpdatedAt, $conversationState, $configSnapshot, $metadata, $status, $currentPlan, $activityLog,
                    $taskTitle, $taskStatus, $currentStep, $totalSteps, $completedSteps, $progressPercentage,
                    $taskStartedAt, $taskCompletedAt, $taskCategory, $taskPriority, $taskTags, $estimatedDuration, $actualDuration)
            """;
            
        cmd.Parameters.AddWithValue("$sessionId", session.SessionId);
        cmd.Parameters.AddWithValue("$name", session.Name);
        cmd.Parameters.AddWithValue("$createdAt", session.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$lastUpdatedAt", session.LastUpdatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$conversationState", session.ConversationState);
        cmd.Parameters.AddWithValue("$configSnapshot", session.ConfigurationSnapshot);
        cmd.Parameters.AddWithValue("$metadata", session.Metadata);
        cmd.Parameters.AddWithValue("$status", (int)session.Status);
        cmd.Parameters.AddWithValue("$currentPlan", session.CurrentPlan);
        cmd.Parameters.AddWithValue("$activityLog", session.ActivityLog);
        cmd.Parameters.AddWithValue("$taskTitle", session.TaskTitle);
        cmd.Parameters.AddWithValue("$taskStatus", (int)session.TaskStatus);
        cmd.Parameters.AddWithValue("$currentStep", session.CurrentStep);
        cmd.Parameters.AddWithValue("$totalSteps", session.TotalSteps);
        cmd.Parameters.AddWithValue("$completedSteps", session.CompletedSteps);
        cmd.Parameters.AddWithValue("$progressPercentage", session.ProgressPercentage);
        cmd.Parameters.AddWithValue("$taskStartedAt", (object?)session.TaskStartedAt?.ToString("O") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$taskCompletedAt", (object?)session.TaskCompletedAt?.ToString("O") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$taskCategory", session.TaskCategory);
        cmd.Parameters.AddWithValue("$taskPriority", session.TaskPriority);
        cmd.Parameters.AddWithValue("$taskTags", session.TaskTags);
        cmd.Parameters.AddWithValue("$estimatedDuration", (object?)session.EstimatedDuration ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$actualDuration", (object?)session.ActualDuration ?? DBNull.Value);

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
                   ConversationState, ConfigurationSnapshot, Metadata, Status, CurrentPlan, ActivityLog,
                   TaskTitle, TaskStatus, CurrentStep, TotalSteps, CompletedSteps, ProgressPercentage,
                   TaskStartedAt, TaskCompletedAt, TaskCategory, TaskPriority, TaskTags, 
                   EstimatedDuration, ActualDuration
            FROM AgentSessions 
            ORDER BY LastUpdatedAt DESC
            """;

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            sessions.Add(CreateSessionFromReader(reader));
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
    
    /// <summary>
    /// Get sessions by task status
    /// </summary>
    public async Task<IReadOnlyList<AgentSession>> GetSessionsByTaskStatusAsync(TaskExecutionStatus taskStatus)
    {
        var sessions = new List<AgentSession>();
        
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        
        cmd.CommandText = """
            SELECT SessionId, Name, CreatedAt, LastUpdatedAt, 
                   ConversationState, ConfigurationSnapshot, Metadata, Status, CurrentPlan, ActivityLog,
                   TaskTitle, TaskStatus, CurrentStep, TotalSteps, CompletedSteps, ProgressPercentage,
                   TaskStartedAt, TaskCompletedAt, TaskCategory, TaskPriority, TaskTags, 
                   EstimatedDuration, ActualDuration
            FROM AgentSessions 
            WHERE TaskStatus = $taskStatus
            ORDER BY LastUpdatedAt DESC
            """;
        cmd.Parameters.AddWithValue("$taskStatus", (int)taskStatus);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            sessions.Add(CreateSessionFromReader(reader));
        }

        return sessions;
    }
    
    /// <summary>
    /// Get sessions by category
    /// </summary>
    public async Task<IReadOnlyList<AgentSession>> GetSessionsByCategoryAsync(string category)
    {
        var sessions = new List<AgentSession>();
        
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        
        cmd.CommandText = """
            SELECT SessionId, Name, CreatedAt, LastUpdatedAt, 
                   ConversationState, ConfigurationSnapshot, Metadata, Status, CurrentPlan, ActivityLog,
                   TaskTitle, TaskStatus, CurrentStep, TotalSteps, CompletedSteps, ProgressPercentage,
                   TaskStartedAt, TaskCompletedAt, TaskCategory, TaskPriority, TaskTags, 
                   EstimatedDuration, ActualDuration
            FROM AgentSessions 
            WHERE TaskCategory = $category
            ORDER BY LastUpdatedAt DESC
            """;
        cmd.Parameters.AddWithValue("$category", category);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            sessions.Add(CreateSessionFromReader(reader));
        }

        return sessions;
    }
    
    /// <summary>
    /// Get sessions by priority level
    /// </summary>
    public async Task<IReadOnlyList<AgentSession>> GetSessionsByPriorityAsync(int priority)
    {
        var sessions = new List<AgentSession>();
        
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        
        cmd.CommandText = """
            SELECT SessionId, Name, CreatedAt, LastUpdatedAt, 
                   ConversationState, ConfigurationSnapshot, Metadata, Status, CurrentPlan, ActivityLog,
                   TaskTitle, TaskStatus, CurrentStep, TotalSteps, CompletedSteps, ProgressPercentage,
                   TaskStartedAt, TaskCompletedAt, TaskCategory, TaskPriority, TaskTags, 
                   EstimatedDuration, ActualDuration
            FROM AgentSessions 
            WHERE TaskPriority = $priority
            ORDER BY LastUpdatedAt DESC
            """;
        cmd.Parameters.AddWithValue("$priority", priority);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            sessions.Add(CreateSessionFromReader(reader));
        }

        return sessions;
    }
    
    /// <summary>
    /// Get sessions with progress in a specified range
    /// </summary>
    public async Task<IReadOnlyList<AgentSession>> GetSessionsByProgressRangeAsync(double minProgress, double maxProgress)
    {
        var sessions = new List<AgentSession>();
        
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        
        cmd.CommandText = """
            SELECT SessionId, Name, CreatedAt, LastUpdatedAt, 
                   ConversationState, ConfigurationSnapshot, Metadata, Status, CurrentPlan, ActivityLog,
                   TaskTitle, TaskStatus, CurrentStep, TotalSteps, CompletedSteps, ProgressPercentage,
                   TaskStartedAt, TaskCompletedAt, TaskCategory, TaskPriority, TaskTags, 
                   EstimatedDuration, ActualDuration
            FROM AgentSessions 
            WHERE ProgressPercentage >= $minProgress AND ProgressPercentage <= $maxProgress
            ORDER BY ProgressPercentage DESC
            """;
        cmd.Parameters.AddWithValue("$minProgress", minProgress);
        cmd.Parameters.AddWithValue("$maxProgress", maxProgress);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            sessions.Add(CreateSessionFromReader(reader));
        }

        return sessions;
    }
    
    /// <summary>
    /// Get active tasks (in progress)
    /// </summary>
    public async Task<IReadOnlyList<AgentSession>> GetActiveTasksAsync()
    {
        return await GetSessionsByTaskStatusAsync(TaskExecutionStatus.InProgress);
    }
    
    /// <summary>
    /// Get completed tasks
    /// </summary>
    public async Task<IReadOnlyList<AgentSession>> GetCompletedTasksAsync()
    {
        return await GetSessionsByTaskStatusAsync(TaskExecutionStatus.Completed);
    }
    
    /// <summary>
    /// Get sessions by tags (contains any of the specified tags)
    /// </summary>
    public async Task<IReadOnlyList<AgentSession>> GetSessionsByTagsAsync(params string[] tags)
    {
        if (tags == null || tags.Length == 0)
            return new List<AgentSession>();
            
        var sessions = new List<AgentSession>();
        
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        
        // Build WHERE clause for tag matching
        var tagConditions = tags.Select((tag, index) => $"TaskTags LIKE $tag{index}").ToArray();
        var whereClause = string.Join(" OR ", tagConditions);
        
        cmd.CommandText = $"""
            SELECT SessionId, Name, CreatedAt, LastUpdatedAt, 
                   ConversationState, ConfigurationSnapshot, Metadata, Status, CurrentPlan, ActivityLog,
                   TaskTitle, TaskStatus, CurrentStep, TotalSteps, CompletedSteps, ProgressPercentage,
                   TaskStartedAt, TaskCompletedAt, TaskCategory, TaskPriority, TaskTags, 
                   EstimatedDuration, ActualDuration
            FROM AgentSessions 
            WHERE {whereClause}
            ORDER BY LastUpdatedAt DESC
            """;
        
        for (int i = 0; i < tags.Length; i++)
        {
            cmd.Parameters.AddWithValue($"$tag{i}", $"%{tags[i]}%");
        }

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            sessions.Add(CreateSessionFromReader(reader));
        }

        return sessions;
    }
    
    /// <summary>
    /// Helper method to create AgentSession from database reader
    /// </summary>
    private static AgentSession CreateSessionFromReader(SqliteDataReader reader)
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
            CurrentPlan = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
            ActivityLog = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
            TaskTitle = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
            TaskStatus = reader.IsDBNull(11) ? TaskExecutionStatus.NotStarted : (TaskExecutionStatus)reader.GetInt32(11),
            CurrentStep = reader.IsDBNull(12) ? 0 : reader.GetInt32(12),
            TotalSteps = reader.IsDBNull(13) ? 0 : reader.GetInt32(13),
            CompletedSteps = reader.IsDBNull(14) ? 0 : reader.GetInt32(14),
            ProgressPercentage = reader.IsDBNull(15) ? 0.0 : reader.GetDouble(15),
            TaskStartedAt = reader.IsDBNull(16) ? null : DateTime.Parse(reader.GetString(16), null, System.Globalization.DateTimeStyles.RoundtripKind),
            TaskCompletedAt = reader.IsDBNull(17) ? null : DateTime.Parse(reader.GetString(17), null, System.Globalization.DateTimeStyles.RoundtripKind),
            TaskCategory = reader.IsDBNull(18) ? string.Empty : reader.GetString(18),
            TaskPriority = reader.IsDBNull(19) ? 2 : reader.GetInt32(19),
            TaskTags = reader.IsDBNull(20) ? string.Empty : reader.GetString(20),
            EstimatedDuration = reader.IsDBNull(21) ? null : reader.GetInt32(21),
            ActualDuration = reader.IsDBNull(22) ? null : reader.GetInt32(22)
        };
    }
}