using Microsoft.Extensions.Logging;
using System.Diagnostics;
using AgentAlpha.Interfaces;
using AgentAlpha.Models;
using Common.Interfaces.Session;
using Common.Models.Session;

namespace AgentAlpha.Services;

/// <summary>
/// Implementation of session activity logging for audit trails
/// </summary>
public class SessionActivityLogger : ISessionActivityLogger
{
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<SessionActivityLogger> _logger;
    private readonly Dictionary<string, (SessionActivity Activity, Stopwatch Timer)> _activeActivities;
    private AgentSession? _currentSession;

    public SessionActivityLogger(ISessionManager sessionManager, ILogger<SessionActivityLogger> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
        _activeActivities = new Dictionary<string, (SessionActivity, Stopwatch)>();
    }

    public void SetCurrentSession(AgentSession session)
    {
        _currentSession = session;
        _logger.LogDebug("Set current session for activity logging: {SessionId}", session.SessionId);
    }

    public AgentSession? GetCurrentSession()
    {
        return _currentSession;
    }

    public async Task LogActivityAsync(string activityType, string description, object? data = null)
    {
        if (_currentSession == null)
        {
            _logger.LogWarning("Cannot log activity - no current session set");
            return;
        }

        var activity = SessionActivity.Create(activityType, description, data);
        await SaveActivityAsync(activity);
        
        _logger.LogDebug("Logged activity: {Type} - {Description}", activityType, description);
    }

    public async Task LogTimedActivityAsync(string activityType, string description, long durationMs, object? data = null)
    {
        if (_currentSession == null)
        {
            _logger.LogWarning("Cannot log timed activity - no current session set");
            return;
        }

        var activity = SessionActivity.Create(activityType, description, data);
        activity.Complete(durationMs);
        await SaveActivityAsync(activity);
        
        _logger.LogDebug("Logged timed activity: {Type} - {Description} ({Duration}ms)", activityType, description, durationMs);
    }

    public async Task LogFailedActivityAsync(string activityType, string description, string errorMessage, object? data = null)
    {
        if (_currentSession == null)
        {
            _logger.LogWarning("Cannot log failed activity - no current session set");
            return;
        }

        var activity = SessionActivity.Create(activityType, description, data);
        activity.Fail(errorMessage);
        await SaveActivityAsync(activity);
        
        _logger.LogDebug("Logged failed activity: {Type} - {Description}: {Error}", activityType, description, errorMessage);
    }

    public string StartActivity(string activityType, string description, object? data = null)
    {
        var activity = SessionActivity.Create(activityType, description, data);
        var timer = Stopwatch.StartNew();
        
        _activeActivities[activity.ActivityId] = (activity, timer);
        
        _logger.LogDebug("Started activity: {Type} - {Description} (ID: {ActivityId})", 
            activityType, description, activity.ActivityId);
        
        return activity.ActivityId;
    }

    public async Task CompleteActivityAsync(string activityId, object? additionalData = null)
    {
        if (!_activeActivities.TryGetValue(activityId, out var activityInfo))
        {
            _logger.LogWarning("Cannot complete activity - activity ID not found: {ActivityId}", activityId);
            return;
        }

        var (activity, timer) = activityInfo;
        timer.Stop();
        
        // Merge additional data if provided
        if (additionalData != null)
        {
            var existingData = string.IsNullOrEmpty(activity.Data) ? null : activity.GetData<object>();
            var mergedData = new { existing = existingData, additional = additionalData };
            activity.Data = System.Text.Json.JsonSerializer.Serialize(mergedData);
        }
        
        activity.Complete(timer.ElapsedMilliseconds);
        await SaveActivityAsync(activity);
        
        _activeActivities.Remove(activityId);
        
        _logger.LogDebug("Completed activity: {Type} - {Description} ({Duration}ms)", 
            activity.ActivityType, activity.Description, timer.ElapsedMilliseconds);
    }

    public async Task FailActivityAsync(string activityId, string errorMessage, object? additionalData = null)
    {
        if (!_activeActivities.TryGetValue(activityId, out var activityInfo))
        {
            _logger.LogWarning("Cannot fail activity - activity ID not found: {ActivityId}", activityId);
            return;
        }

        var (activity, timer) = activityInfo;
        timer.Stop();
        
        // Merge additional data if provided
        if (additionalData != null)
        {
            var existingData = string.IsNullOrEmpty(activity.Data) ? null : activity.GetData<object>();
            var mergedData = new { existing = existingData, additional = additionalData, error = errorMessage };
            activity.Data = System.Text.Json.JsonSerializer.Serialize(mergedData);
        }
        
        activity.Fail(errorMessage, timer.ElapsedMilliseconds);
        await SaveActivityAsync(activity);
        
        _activeActivities.Remove(activityId);
        
        _logger.LogDebug("Failed activity: {Type} - {Description} ({Duration}ms): {Error}", 
            activity.ActivityType, activity.Description, timer.ElapsedMilliseconds, errorMessage);
    }

    public async Task<List<SessionActivity>> GetSessionActivitiesAsync()
    {
        if (_currentSession == null)
        {
            _logger.LogWarning("Cannot get activities - no current session set");
            return new List<SessionActivity>();
        }

        // Use the session manager to get activities from the database
        return await _sessionManager.GetSessionActivitiesAsync(_currentSession.SessionId);
    }

    private async Task SaveActivityAsync(SessionActivity activity)
    {
        if (_currentSession == null)
            return;

        try
        {
            // Set the session ID on the activity
            activity.SessionId = _currentSession.SessionId;
            
            // Save the activity using the session manager
            await _sessionManager.AddSessionActivityAsync(_currentSession.SessionId, activity);
            
            _logger.LogDebug("Saved activity to session {SessionId}: {ActivityType} - {Description}", 
                _currentSession.SessionId, activity.ActivityType, activity.Description);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save activity to session: {ActivityType} - {Description}", 
                activity.ActivityType, activity.Description);
        }
    }
}