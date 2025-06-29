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
    /// Get sessions by task status
    /// </summary>
    Task<IReadOnlyList<AgentSession>> GetSessionsByTaskStatusAsync(TaskExecutionStatus taskStatus);
    
    /// <summary>
    /// Get sessions by category
    /// </summary>
    Task<IReadOnlyList<AgentSession>> GetSessionsByCategoryAsync(string category);
    
    /// <summary>
    /// Get sessions by priority level
    /// </summary>
    Task<IReadOnlyList<AgentSession>> GetSessionsByPriorityAsync(int priority);
    
    /// <summary>
    /// Get sessions with progress in a specified range
    /// </summary>
    Task<IReadOnlyList<AgentSession>> GetSessionsByProgressRangeAsync(double minProgress, double maxProgress);
    
    /// <summary>
    /// Get active tasks (in progress)
    /// </summary>
    Task<IReadOnlyList<AgentSession>> GetActiveTasksAsync();
    
    /// <summary>
    /// Get completed tasks
    /// </summary>
    Task<IReadOnlyList<AgentSession>> GetCompletedTasksAsync();
    
    /// <summary>
    /// Get sessions by tags (contains any of the specified tags)
    /// </summary>
    Task<IReadOnlyList<AgentSession>> GetSessionsByTagsAsync(params string[] tags);
}