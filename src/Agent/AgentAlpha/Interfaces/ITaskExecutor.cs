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
}