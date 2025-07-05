namespace AgentAlpha.Interfaces;
using AgentAlpha.Models;

/// <summary>
/// Executes simple tasks using optimized fast-path strategies (direct tool calls or single LLM shot)
/// </summary>
public interface IFastPathExecutor
{
    /// <summary>
    /// Execute a task using fast-path optimization
    /// </summary>
    /// <param name="request">The task execution request</param>
    /// <exception cref="ArgumentNullException">Thrown when request is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when task is not suitable for fast-path execution</exception>
    Task ExecuteAsync(TaskExecutionRequest request);
}
