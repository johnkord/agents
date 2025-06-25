using System.Text.Json;

namespace AgentAlpha.Models;

/// <summary>
/// Represents a persistent agent session that can be saved and restored
/// </summary>
public class AgentSession
{
    /// <summary>
    /// Unique identifier for the session
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
    
    /// <summary>
    /// Human-readable name or description for the session
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// When the session was created
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// When the session was last updated
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }
    
    /// <summary>
    /// Serialized conversation messages
    /// </summary>
    public string ConversationState { get; set; } = string.Empty;
    
    /// <summary>
    /// Configuration used for this session
    /// </summary>
    public string ConfigurationSnapshot { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional metadata for the session
    /// </summary>
    public string Metadata { get; set; } = string.Empty;
    
    /// <summary>
    /// Status of the session
    /// </summary>
    public SessionStatus Status { get; set; } = SessionStatus.Active;
    
    /// <summary>
    /// Serialized current task plan for the session
    /// </summary>
    public string CurrentPlan { get; set; } = string.Empty;
    
    /// <summary>
    /// Serialized activity log for session audit trail
    /// </summary>
    public string ActivityLog { get; set; } = string.Empty;
    
    /// <summary>
    /// Create a new session with a generated ID
    /// </summary>
    public static AgentSession CreateNew(string name = "")
    {
        var now = DateTime.UtcNow;
        return new AgentSession
        {
            SessionId = Guid.NewGuid().ToString(),
            Name = string.IsNullOrEmpty(name) ? $"Session {now:yyyy-MM-dd HH:mm}" : name,
            CreatedAt = now,
            LastUpdatedAt = now,
            Status = SessionStatus.Active
        };
    }
    
    /// <summary>
    /// Deserialize conversation messages from stored state
    /// </summary>
    public List<object> GetConversationMessages()
    {
        if (string.IsNullOrEmpty(ConversationState))
            return new List<object>();
            
        try
        {
            return JsonSerializer.Deserialize<List<object>>(ConversationState) ?? new List<object>();
        }
        catch
        {
            return new List<object>();
        }
    }
    
    /// <summary>
    /// Serialize conversation messages to storable state
    /// </summary>
    public void SetConversationMessages(IEnumerable<object> messages)
    {
        try
        {
            ConversationState = JsonSerializer.Serialize(messages);
            LastUpdatedAt = DateTime.UtcNow;
        }
        catch
        {
            ConversationState = string.Empty;
        }
    }
    
    /// <summary>
    /// Get the current task plan for this session
    /// </summary>
    public TaskPlan? GetCurrentPlan()
    {
        if (string.IsNullOrEmpty(CurrentPlan))
            return null;
            
        try
        {
            return JsonSerializer.Deserialize<TaskPlan>(CurrentPlan);
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// Set the current task plan for this session
    /// </summary>
    public void SetCurrentPlan(TaskPlan? plan)
    {
        try
        {
            CurrentPlan = plan != null ? JsonSerializer.Serialize(plan) : string.Empty;
            LastUpdatedAt = DateTime.UtcNow;
        }
        catch
        {
            CurrentPlan = string.Empty;
        }
    }
    
    /// <summary>
    /// Get the activity log for this session
    /// </summary>
    public List<SessionActivity> GetActivityLog()
    {
        if (string.IsNullOrEmpty(ActivityLog))
            return new List<SessionActivity>();
            
        try
        {
            return JsonSerializer.Deserialize<List<SessionActivity>>(ActivityLog) ?? new List<SessionActivity>();
        }
        catch
        {
            return new List<SessionActivity>();
        }
    }
    
    /// <summary>
    /// Add an activity to the session log
    /// </summary>
    public void AddActivity(SessionActivity activity)
    {
        var activities = GetActivityLog();
        activities.Add(activity);
        SetActivityLog(activities);
    }
    
    /// <summary>
    /// Set the complete activity log for this session
    /// </summary>
    public void SetActivityLog(List<SessionActivity> activities)
    {
        try
        {
            ActivityLog = JsonSerializer.Serialize(activities);
            LastUpdatedAt = DateTime.UtcNow;
        }
        catch
        {
            ActivityLog = string.Empty;
        }
    }
}

/// <summary>
/// Status of an agent session
/// </summary>
public enum SessionStatus
{
    Active,
    Completed,
    Archived,
    Error
}