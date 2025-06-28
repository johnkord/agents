namespace Common.Models.Session;

/// <summary>
/// Represents the current state of the environment and context for task planning
/// </summary>
public class CurrentState
{
    /// <summary>
    /// Current session information and conversation history summary
    /// </summary>
    public string? SessionContext { get; set; }
    
    /// <summary>
    /// Previous task execution results and outcomes
    /// </summary>
    public List<ExecutionResult> PreviousResults { get; set; } = new();
    
    /// <summary>
    /// Available resources and their current status
    /// </summary>
    public Dictionary<string, string> AvailableResources { get; set; } = new();
    
    /// <summary>
    /// User preferences and constraints
    /// </summary>
    public UserPreferences? UserPreferences { get; set; }
    
    /// <summary>
    /// Current environment capabilities and limitations
    /// </summary>
    public EnvironmentCapabilities? Environment { get; set; }
    
    /// <summary>
    /// Timestamp when this state snapshot was captured
    /// </summary>
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Any additional contextual information
    /// </summary>
    public Dictionary<string, object> AdditionalContext { get; set; } = new();
}

/// <summary>
/// Represents the result of a previous task execution
/// </summary>
public class ExecutionResult
{
    /// <summary>
    /// The task that was executed
    /// </summary>
    public string Task { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether the execution was successful
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Summary of what was accomplished
    /// </summary>
    public string Summary { get; set; } = string.Empty;
    
    /// <summary>
    /// Tools that were used in this execution
    /// </summary>
    public List<string> ToolsUsed { get; set; } = new();
    
    /// <summary>
    /// When this execution completed
    /// </summary>
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Any lessons learned or insights from this execution
    /// </summary>
    public string? Insights { get; set; }
}

/// <summary>
/// User preferences that should influence planning
/// </summary>
public class UserPreferences
{
    /// <summary>
    /// Preferred approach to task execution (e.g., "thorough", "fast", "interactive")
    /// </summary>
    public string? PreferredApproach { get; set; }
    
    /// <summary>
    /// Tools the user prefers to use or avoid
    /// </summary>
    public List<string> PreferredTools { get; set; } = new();
    
    /// <summary>
    /// Tools the user wants to avoid
    /// </summary>
    public List<string> AvoidedTools { get; set; } = new();
    
    /// <summary>
    /// Maximum time user wants to spend on the task
    /// </summary>
    public TimeSpan? MaxExecutionTime { get; set; }
    
    /// <summary>
    /// User's risk tolerance level (0.0 = very conservative, 1.0 = high risk)
    /// </summary>
    public double RiskTolerance { get; set; } = 0.5;
}

/// <summary>
/// Current environment capabilities and constraints
/// </summary>
public class EnvironmentCapabilities
{
    /// <summary>
    /// Available computational resources
    /// </summary>
    public string? ComputeResources { get; set; }
    
    /// <summary>
    /// Network connectivity status and limitations
    /// </summary>
    public string? NetworkStatus { get; set; }
    
    /// <summary>
    /// File system access and storage information
    /// </summary>
    public string? StorageInfo { get; set; }
    
    /// <summary>
    /// Security constraints or permissions
    /// </summary>
    public List<string> SecurityConstraints { get; set; } = new();
    
    /// <summary>
    /// Current system load or performance metrics
    /// </summary>
    public Dictionary<string, double> PerformanceMetrics { get; set; } = new();
}