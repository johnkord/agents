using AgentAlpha.Configuration;

namespace AgentAlpha.Models;

/// <summary>
/// Represents a complete request for task execution with all parameters
/// </summary>
public class TaskExecutionRequest
{
    /// <summary>
    /// The task description to execute
    /// </summary>
    public string Task { get; set; } = string.Empty;
    
    /// <summary>
    /// OpenAI model to use for this task (e.g., "gpt-4.1", "gpt-4.1-nano")
    /// </summary>
    public string? Model { get; set; }
    
    /// <summary>
    /// Temperature for OpenAI responses (0.0 to 1.0)
    /// </summary>
    public double? Temperature { get; set; }
    
    /// <summary>
    /// Maximum number of conversation iterations for this task
    /// </summary>
    public int? MaxIterations { get; set; }
    
    /// <summary>
    /// Custom system prompt to override the default
    /// </summary>
    public string? SystemPrompt { get; set; }
    
    /// <summary>
    /// Priority level for task execution (High, Normal, Low)
    /// </summary>
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    
    /// <summary>
    /// Timeout for the entire task execution
    /// </summary>
    public TimeSpan? Timeout { get; set; }
    
    /// <summary>
    /// Tool filtering preferences for this specific task
    /// </summary>
    public ToolFilterConfig? ToolFilter { get; set; }
    
    /// <summary>
    /// Whether to enable verbose logging for this task
    /// </summary>
    public bool VerboseLogging { get; set; } = false;
    
    /// <summary>
    /// Session ID for persistent session support (optional)
    /// If specified, the agent will use/update the existing session
    /// </summary>
    public string? SessionId { get; set; }
    
    /// <summary>
    /// Session name for creating new sessions (optional)
    /// Used when SessionId is not specified but session persistence is desired
    /// </summary>
    public string? SessionName { get; set; }
    
    /// <summary>
    /// Create a basic request with just a task
    /// </summary>
    public static TaskExecutionRequest FromTask(string task)
    {
        return new TaskExecutionRequest { Task = task };
    }
    
    /// <summary>
    /// Create a request with task and model
    /// </summary>
    public static TaskExecutionRequest FromTaskAndModel(string task, string model)
    {
        return new TaskExecutionRequest 
        { 
            Task = task, 
            Model = model 
        };
    }
}

/// <summary>
/// Priority levels for task execution
/// </summary>
public enum TaskPriority
{
    Low,
    Normal,
    High
}