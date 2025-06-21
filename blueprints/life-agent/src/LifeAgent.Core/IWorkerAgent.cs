using LifeAgent.Core.Models;

namespace LifeAgent.Core;

/// <summary>
/// Interface for all worker agents. Each worker has narrow scope,
/// specific tools, and a clear success/failure definition.
/// Follows design.md §8.1.
/// </summary>
public interface IWorkerAgent
{
    string WorkerType { get; }
    string Description { get; }
    IReadOnlyList<string> SupportedDomains { get; }

    Task<TaskResult> ExecuteAsync(
        LifeTask task,
        UserProfile userProfile,
        CancellationToken ct);
}
