using System.Text.Json;

namespace Common.Models.Session;

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
    /// Activity log for the session (JSON serialized list of SessionActivity)
    /// </summary>
    [Obsolete("Use ISessionManager.GetSessionActivitiesAsync() instead")]
    public string ActivityLog { get; set; } = string.Empty;
    
    /// <summary>
    /// Markdown document containing current task state and summarized context, 
    /// managed by the LLM for dynamic planning
    /// </summary>
    public string TaskStateMarkdown { get; set; } = string.Empty;
    
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
    /// Add a new activity to the session log
    /// </summary>
    [Obsolete("Use ISessionManager.AddSessionActivityAsync() instead")]
    public void AddActivity(SessionActivity activity)
    {
        // This method is deprecated - activities are now stored in a separate table
        // Left empty to avoid breaking existing code during migration
    }
    
    /// <summary>
    /// Get all activities from the session log
    /// </summary>
    [Obsolete("Use ISessionManager.GetSessionActivitiesAsync() instead")]
    public List<SessionActivity> GetActivityLog()
    {
        // This method is deprecated - activities are now stored in a separate table
        // Return empty list to avoid breaking existing code during migration
        return new List<SessionActivity>();
    }
    
    /// <summary>
    /// Set the entire activity log (replaces existing)
    /// </summary>
    [Obsolete("Use ISessionManager.AddSessionActivityAsync() instead")]
    public void SetActivityLog(List<SessionActivity> activities)
    {
        // This method is deprecated - activities are now stored in a separate table
        // Left empty to avoid breaking existing code during migration
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