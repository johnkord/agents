using Common.Models.Session;

namespace Common.Interfaces.Session;

/// <summary>
/// Interface for managing task state as markdown documents with LLM-driven planning
/// </summary>
public interface IMarkdownTaskStateManager
{
    /// <summary>
    /// Initialize a task state markdown document from a task description
    /// </summary>
    Task<string> InitializeTaskMarkdownAsync(string sessionId, string taskDescription);
    
    /// <summary>
    /// Initialize a task state markdown document from a complete TaskPlan
    /// </summary>
    Task<string> InitializeTaskMarkdownFromPlanAsync(string sessionId, TaskPlan taskPlan);
    
    /// <summary>
    /// Update the task markdown document based on action results using LLM
    /// </summary>
    Task<string> UpdateTaskMarkdownAsync(string sessionId, string actionDescription, string actionResult, string? observations = null);
    
    /// <summary>
    /// Get the current task state markdown document
    /// </summary>
    Task<string> GetTaskMarkdownAsync(string sessionId);
    
    /// <summary>
    /// Parse the markdown document to extract the current subtask to work on
    /// </summary>
    Task<SubtaskInfo?> GetCurrentSubtaskFromMarkdownAsync(string sessionId);
    
    /// <summary>
    /// Mark a subtask as completed in the markdown document
    /// </summary>
    Task<string> CompleteSubtaskInMarkdownAsync(string sessionId, string subtaskDescription, string completionResult);
    
    /// <summary>
    /// Add a new subtask to the markdown document (LLM-driven planning)
    /// </summary>
    Task<string> AddSubtaskToMarkdownAsync(string sessionId, string reason, string? context = null);
    
    /// <summary>
    /// Update the plan iteratively based on execution progress and feedback
    /// </summary>
    Task<string> UpdatePlanIterativelyAsync(string sessionId, string executionFeedback, string? currentContext = null);
}

/// <summary>
/// Information about a subtask extracted from markdown
/// </summary>
public class SubtaskInfo
{
    /// <summary>
    /// Description of the subtask
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether this subtask is completed
    /// </summary>
    public bool IsCompleted { get; set; }
    
    /// <summary>
    /// Priority or order of the subtask
    /// </summary>
    public int Priority { get; set; }
    
    /// <summary>
    /// Any additional context or notes
    /// </summary>
    public string? Notes { get; set; }
}