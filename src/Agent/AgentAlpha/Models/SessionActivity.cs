using System.Text.Json;

namespace AgentAlpha.Models;

/// <summary>
/// Represents a single activity/event in an agent session for audit trail purposes
/// </summary>
public class SessionActivity
{
    /// <summary>
    /// Unique identifier for this activity
    /// </summary>
    public string ActivityId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Timestamp when the activity occurred
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Type of activity (e.g., "OpenAI_Request", "Tool_Call", "Planning", etc.)
    /// </summary>
    public string ActivityType { get; set; } = string.Empty;
    
    /// <summary>
    /// Human-readable description of the activity
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Additional data associated with the activity (JSON serialized)
    /// </summary>
    public string Data { get; set; } = string.Empty;
    
    /// <summary>
    /// Duration of the activity in milliseconds (optional)
    /// </summary>
    public long? DurationMs { get; set; }
    
    /// <summary>
    /// Whether the activity completed successfully
    /// </summary>
    public bool Success { get; set; } = true;
    
    /// <summary>
    /// Error message if the activity failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Create a new activity with the specified type and description
    /// </summary>
    public static SessionActivity Create(string activityType, string description, object? data = null)
    {
        var activity = new SessionActivity
        {
            ActivityType = activityType,
            Description = description
        };
        
        if (data != null)
        {
            try
            {
                activity.Data = JsonSerializer.Serialize(data);
            }
            catch
            {
                activity.Data = data.ToString() ?? string.Empty;
            }
        }
        
        return activity;
    }
    
    /// <summary>
    /// Mark the activity as completed with optional duration
    /// </summary>
    public void Complete(long? durationMs = null)
    {
        Success = true;
        DurationMs = durationMs;
    }
    
    /// <summary>
    /// Mark the activity as failed with error message
    /// </summary>
    public void Fail(string errorMessage, long? durationMs = null)
    {
        Success = false;
        ErrorMessage = errorMessage;
        DurationMs = durationMs;
    }
    
    /// <summary>
    /// Get the deserialized data as the specified type
    /// </summary>
    public T? GetData<T>() where T : class
    {
        if (string.IsNullOrEmpty(Data))
            return null;
            
        try
        {
            return JsonSerializer.Deserialize<T>(Data);
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// Create an activity with detailed data, applying size limits and truncation as needed
    /// </summary>
    public static SessionActivity CreateWithDetailedData(string activityType, string description, object? data = null, int maxDataSize = 50000)
    {
        var activity = new SessionActivity
        {
            ActivityType = activityType,
            Description = description
        };
        
        if (data != null)
        {
            try
            {
                var serializedData = JsonSerializer.Serialize(data);
                
                // If data is too large, truncate and add metadata about truncation
                if (serializedData.Length > maxDataSize)
                {
                    var truncatedData = new
                    {
                        truncated = true,
                        originalSize = serializedData.Length,
                        maxSize = maxDataSize,
                        data = serializedData.Substring(0, Math.Min(maxDataSize - 200, serializedData.Length)) + "... [TRUNCATED]"
                    };
                    activity.Data = JsonSerializer.Serialize(truncatedData);
                }
                else
                {
                    activity.Data = serializedData;
                }
            }
            catch
            {
                activity.Data = data.ToString() ?? string.Empty;
            }
        }
        
        return activity;
    }
    
    /// <summary>
    /// Safely truncate string data to prevent oversized activity logs
    /// </summary>
    public static string TruncateString(string? input, int maxLength = 5000)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;
            
        if (input.Length <= maxLength)
            return input;
            
        return input.Substring(0, maxLength - 20) + "... [TRUNCATED]";
    }
}

/// <summary>
/// Standard activity types for consistency
/// </summary>
public static class ActivityTypes
{
    public const string SessionStart = "Session_Start";
    public const string SessionEnd = "Session_End";
    public const string TaskPlanning = "Task_Planning";
    public const string ToolSelection = "Tool_Selection";
    public const string OpenAIRequest = "OpenAI_Request";
    public const string OpenAIResponse = "OpenAI_Response";
    public const string ToolCall = "Tool_Call";
    public const string ToolResult = "Tool_Result";
    public const string ConversationIteration = "Conversation_Iteration";
    public const string Error = "Error";
}