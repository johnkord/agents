namespace AgentAlpha.Interfaces;
using AgentAlpha.Models;
using System.Threading;

/// <summary>
/// Routes tasks to appropriate execution strategies based on task analysis
/// </summary>
public interface ITaskRouter
{
    /// <summary>
    /// Analyzes a task and determines the best execution route with confidence score
    /// </summary>
    /// <param name="request">The task execution request to route</param>
    /// <param name="ct">Cancellation token for async operations</param>
    /// <returns>A tuple containing the selected route and confidence score (0.0-1.0)</returns>
    Task<(TaskRoute route, double confidence)> RouteAsync(TaskExecutionRequest request,
                                                          CancellationToken ct = default);
}
