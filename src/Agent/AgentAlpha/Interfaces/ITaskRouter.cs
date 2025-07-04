namespace AgentAlpha.Interfaces;
using AgentAlpha.Models;
using System.Threading;
public interface ITaskRouter
{
    Task<(TaskRoute route, double confidence)> RouteAsync(TaskExecutionRequest request,
                                                          CancellationToken ct = default);
}
