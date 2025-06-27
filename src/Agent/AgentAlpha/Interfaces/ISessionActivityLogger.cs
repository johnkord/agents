using Common.Models.Session;

namespace AgentAlpha.Interfaces;

/// <summary>
/// Interface for logging session activities for audit trail purposes
/// </summary>
public interface ISessionActivityLogger
{
    /// <summary>
    /// Log an activity for the current session
    /// </summary>
    Task LogActivityAsync(string activityType, string description, object? data = null);
    
    /// <summary>
    /// Log an activity with timing information
    /// </summary>
    Task LogTimedActivityAsync(string activityType, string description, long durationMs, object? data = null);
    
    /// <summary>
    /// Log a failed activity
    /// </summary>
    Task LogFailedActivityAsync(string activityType, string description, string errorMessage, object? data = null);
    
    /// <summary>
    /// Start timing an activity (returns an activity ID for completion)
    /// </summary>
    string StartActivity(string activityType, string description, object? data = null);
    
    /// <summary>
    /// Complete a timed activity
    /// </summary>
    Task CompleteActivityAsync(string activityId, object? additionalData = null);
    
    /// <summary>
    /// Fail a timed activity
    /// </summary>
    Task FailActivityAsync(string activityId, string errorMessage, object? additionalData = null);
    
    /// <summary>
    /// Set the current session for logging
    /// </summary>
    void SetCurrentSession(AgentSession session);
    
    /// <summary>
    /// Get all activities for the current session
    /// </summary>
    Task<List<SessionActivity>> GetSessionActivitiesAsync();
}