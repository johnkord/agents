using Common.Models.Session;

namespace Common.Interfaces.Session;

/// <summary>
/// Interface for managing task state persistence and operations
/// </summary>
public interface ITaskStateManager
{
    /// <summary>
    /// Create a new task state from a task plan
    /// </summary>
    TaskState CreateTaskState(TaskPlan taskPlan);
    
    /// <summary>
    /// Get the current task state for a session
    /// </summary>
    Task<TaskState?> GetTaskStateAsync(string sessionId);
    
    /// <summary>
    /// Save task state to a session
    /// </summary>
    Task SaveTaskStateAsync(string sessionId, TaskState taskState);
    
    /// <summary>
    /// Complete a subtask and update the task state
    /// </summary>
    Task<TaskState> CompleteSubtaskAsync(string sessionId, int stepNumber, string completionSummary, string? evidence = null, Dictionary<string, object>? context = null);
    
    /// <summary>
    /// Start working on a subtask
    /// </summary>
    Task<TaskState> StartSubtaskAsync(string sessionId, int stepNumber);
    
    /// <summary>
    /// Update subtask notes without completing it
    /// </summary>
    Task<TaskState> UpdateSubtaskNotesAsync(string sessionId, int stepNumber, string? notes = null, string? progressUpdate = null);
    
    /// <summary>
    /// Get the current subtask that should be executed
    /// </summary>
    Task<SubtaskState?> GetCurrentSubtaskAsync(string sessionId);
    
    /// <summary>
    /// Get the context accumulated from completed subtasks
    /// </summary>
    Task<Dictionary<string, object>> GetAccumulatedContextAsync(string sessionId);
    
    /// <summary>
    /// Initialize task state from a task plan and create the initial markdown representation
    /// </summary>
    Task<TaskState> InitializeTaskStateAsync(string sessionId, TaskPlan taskPlan);
}