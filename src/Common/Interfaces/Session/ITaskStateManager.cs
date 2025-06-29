namespace Common.Interfaces.Session;

/// <summary>
/// Interface for managing task state persistence and operations using markdown-based task management
/// </summary>
public interface ITaskStateManager
{
    /// <summary>
    /// Get the current subtask that should be executed from markdown
    /// </summary>
    Task<SubtaskInfo?> GetCurrentSubtaskAsync(string sessionId);
    
    /// <summary>
    /// Complete a subtask and update the markdown state
    /// </summary>
    Task CompleteSubtaskAsync(string sessionId, string subtaskDescription, string completionSummary, string? evidence = null, Dictionary<string, object>? context = null);
    
    /// <summary>
    /// Update the task plan iteratively based on execution progress and feedback
    /// </summary>
    Task UpdatePlanIterativelyAsync(string sessionId, string executionFeedback, string? currentContext = null);
    
    /// <summary>
    /// Initialize task state from a task description using markdown-based planning
    /// </summary>
    Task InitializeTaskStateAsync(string sessionId, string taskDescription);
    
    /// <summary>
    /// Get the current task state markdown document
    /// </summary>
    Task<string> GetTaskMarkdownAsync(string sessionId);
}