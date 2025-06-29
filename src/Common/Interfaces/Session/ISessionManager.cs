using Common.Models.Session;

namespace Common.Interfaces.Session;

/// <summary>
/// Interface for managing persistent agent sessions
/// </summary>
public interface ISessionManager
{
    /// <summary>
    /// Create a new session
    /// </summary>
    Task<AgentSession> CreateSessionAsync(string name = "");
    
    /// <summary>
    /// Get a session by ID
    /// </summary>
    Task<AgentSession?> GetSessionAsync(string sessionId);
    
    /// <summary>
    /// Get a session by name (returns the most recent one if multiple exist)
    /// </summary>
    Task<AgentSession?> GetSessionByNameAsync(string name);
    
    /// <summary>
    /// Save or update a session
    /// </summary>
    Task SaveSessionAsync(AgentSession session);
    
    /// <summary>
    /// List all sessions
    /// </summary>
    Task<IReadOnlyList<AgentSession>> ListSessionsAsync();
    
    /// <summary>
    /// Delete a session
    /// </summary>
    Task<bool> DeleteSessionAsync(string sessionId);
    
    /// <summary>
    /// Archive a session (mark as completed/archived)
    /// </summary>
    Task<bool> ArchiveSessionAsync(string sessionId);

    /// <summary>
    /// Add an activity to a session
    /// </summary>
    Task AddSessionActivityAsync(string sessionId, SessionActivity activity);

    /// <summary>
    /// Get all activities for a session
    /// </summary>
    Task<List<SessionActivity>> GetSessionActivitiesAsync(string sessionId);
}