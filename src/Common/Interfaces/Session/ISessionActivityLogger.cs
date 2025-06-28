using Common.Models.Session;

namespace Common.Interfaces.Session;

/// <summary>
/// Interface for logging session activities for audit trail purposes
/// </summary>
public interface ISessionActivityLogger
{
    Task LogActivityAsync(string activityType, string description, object? data = null);
    Task LogTimedActivityAsync(string activityType, string description, long durationMs, object? data = null);
    Task LogFailedActivityAsync(string activityType, string description, string errorMessage, object? data = null);

    string StartActivity(string activityType, string description, object? data = null);
    Task CompleteActivityAsync(string activityId, object? additionalData = null);
    Task FailActivityAsync(string activityId, string errorMessage, object? additionalData = null);

    void SetCurrentSession(AgentSession session);
    Task<List<SessionActivity>> GetSessionActivitiesAsync();
}
