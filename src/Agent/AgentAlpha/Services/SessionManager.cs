using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using AgentAlpha.Interfaces;
using AgentAlpha.Models;
using System;
using System.IO;

namespace AgentAlpha.Services;

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
        
        // Use env var, explicit parameter, or fallback to shared data directory
        var dbPath = Environment.GetEnvironmentVariable("AGENT_SESSION_DB_PATH") 
                    ?? databasePath 
                    ?? "./data/agent_sessions.db";   // Shared location, not app-specific
                    
        // Ensure directory exists
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
                    
        _connectionString = $"Data Source={dbPath}";
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
                ActivityLog      TEXT NOT NULL DEFAULT ''
            );
            
            CREATE INDEX IF NOT EXISTS idx_sessions_created_at ON AgentSessions(CreatedAt);
            CREATE INDEX IF NOT EXISTS idx_sessions_status ON AgentSessions(Status);
            
            -- Add CurrentPlan column to existing tables if it doesn't exist
            PRAGMA table_info(AgentSessions);
            """;
        cmd.ExecuteNonQuery();
        
        // Check if CurrentPlan column exists and add it if it doesn't
        cmd.CommandText = "PRAGMA table_info(AgentSessions)";
        using var reader = cmd.ExecuteReader();
        bool hasCurrentPlanColumn = false;
        bool hasActivityLogColumn = false;
        while (reader.Read())
        {
            var columnName = reader.GetString(1);
            if (columnName == "CurrentPlan")
                hasCurrentPlanColumn = true;
            if (columnName == "ActivityLog")
                hasActivityLogColumn = true;
        }
        reader.Close();
        
        if (!hasCurrentPlanColumn)
        {
            cmd.CommandText = "ALTER TABLE AgentSessions ADD COLUMN CurrentPlan TEXT NOT NULL DEFAULT ''";
            cmd.ExecuteNonQuery();
        }
        
        if (!hasActivityLogColumn)
        {
            cmd.CommandText = "ALTER TABLE AgentSessions ADD COLUMN ActivityLog TEXT NOT NULL DEFAULT ''";
            cmd.ExecuteNonQuery();
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
                   ConversationState, ConfigurationSnapshot, Metadata, Status, CurrentPlan, ActivityLog
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
                CreatedAt = DateTime.Parse(reader.GetString(2)),
                LastUpdatedAt = DateTime.Parse(reader.GetString(3)),
                ConversationState = reader.GetString(4),
                ConfigurationSnapshot = reader.GetString(5),
                Metadata = reader.GetString(6),
                Status = (SessionStatus)reader.GetInt32(7),
                CurrentPlan = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                ActivityLog = reader.IsDBNull(9) ? string.Empty : reader.GetString(9)
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
                   ConversationState, ConfigurationSnapshot, Metadata, Status, CurrentPlan, ActivityLog
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
                CreatedAt = DateTime.Parse(reader.GetString(2)),
                LastUpdatedAt = DateTime.Parse(reader.GetString(3)),
                ConversationState = reader.GetString(4),
                ConfigurationSnapshot = reader.GetString(5),
                Metadata = reader.GetString(6),
                Status = (SessionStatus)reader.GetInt32(7),
                CurrentPlan = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                ActivityLog = reader.IsDBNull(9) ? string.Empty : reader.GetString(9)
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
            (SessionId, Name, CreatedAt, LastUpdatedAt, ConversationState, ConfigurationSnapshot, Metadata, Status, CurrentPlan, ActivityLog)
            VALUES ($sessionId, $name, $createdAt, $lastUpdatedAt, $conversationState, $configSnapshot, $metadata, $status, $currentPlan, $activityLog)
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
                   ConversationState, ConfigurationSnapshot, Metadata, Status, CurrentPlan, ActivityLog
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
                CreatedAt = DateTime.Parse(reader.GetString(2)),
                LastUpdatedAt = DateTime.Parse(reader.GetString(3)),
                ConversationState = reader.GetString(4),
                ConfigurationSnapshot = reader.GetString(5),
                Metadata = reader.GetString(6),
                Status = (SessionStatus)reader.GetInt32(7),
                CurrentPlan = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                ActivityLog = reader.IsDBNull(9) ? string.Empty : reader.GetString(9)
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