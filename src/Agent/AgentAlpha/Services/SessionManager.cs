using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using AgentAlpha.Interfaces;
using AgentAlpha.Models;

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
        var dbPath = databasePath ?? "agent_sessions.db";
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
                Status           INTEGER NOT NULL DEFAULT 0
            );
            
            CREATE INDEX IF NOT EXISTS idx_sessions_created_at ON AgentSessions(CreatedAt);
            CREATE INDEX IF NOT EXISTS idx_sessions_status ON AgentSessions(Status);
            """;
        cmd.ExecuteNonQuery();
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
                   ConversationState, ConfigurationSnapshot, Metadata, Status
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
                Status = (SessionStatus)reader.GetInt32(7)
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
            (SessionId, Name, CreatedAt, LastUpdatedAt, ConversationState, ConfigurationSnapshot, Metadata, Status)
            VALUES ($sessionId, $name, $createdAt, $lastUpdatedAt, $conversationState, $configSnapshot, $metadata, $status)
            """;
            
        cmd.Parameters.AddWithValue("$sessionId", session.SessionId);
        cmd.Parameters.AddWithValue("$name", session.Name);
        cmd.Parameters.AddWithValue("$createdAt", session.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$lastUpdatedAt", session.LastUpdatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$conversationState", session.ConversationState);
        cmd.Parameters.AddWithValue("$configSnapshot", session.ConfigurationSnapshot);
        cmd.Parameters.AddWithValue("$metadata", session.Metadata);
        cmd.Parameters.AddWithValue("$status", (int)session.Status);

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
                   ConversationState, ConfigurationSnapshot, Metadata, Status
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
                Status = (SessionStatus)reader.GetInt32(7)
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