namespace AgentAlpha.Interfaces;
using AgentAlpha.Models;
public interface IFastPathExecutor
{
    Task ExecuteAsync(TaskExecutionRequest request);
}
