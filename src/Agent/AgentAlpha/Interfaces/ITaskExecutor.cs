using AgentAlpha.Models;

namespace AgentAlpha.Interfaces;

/// <summary>
/// Orchestrates the overall task execution flow
/// </summary>
public interface ITaskExecutor
{
    /// <summary>
    /// Execute a task from start to finish
    /// </summary>
    Task ExecuteAsync(string task);
    
    /// <summary>
    /// Execute a task with full request parameters
    /// </summary>
    Task ExecuteAsync(TaskExecutionRequest request);
    
    /// <summary>
    /// Create a plan for a task without executing it
    /// </summary>
    Task<TaskPlan> CreatePlanAsync(string task);
}