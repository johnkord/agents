using AgentAlpha.Models;
using Common.Models.Session;

namespace AgentAlpha.Interfaces;

/// <summary>
/// Orchestrates the overall task execution flow
/// </summary>
public interface ITaskExecutor
{
    /// <summary>
    /// Execute a task from start to finish using markdown-based task management
    /// </summary>
    Task ExecuteAsync(string task);
    
    /// <summary>
    /// Execute a task with full request parameters using markdown-based task management
    /// </summary>
    Task ExecuteAsync(TaskExecutionRequest request);
}