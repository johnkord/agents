namespace AgentAlpha.Interfaces;

/// <summary>
/// Abstraction over any planning strategy (single-shot or prompt-chained).
/// </summary>
public interface IPlanner
{
    /// <summary>
    /// Create an execution plan for the given task.
    /// </summary>
    Task<string> CreatePlanAsync(
        string task,
        IList<string>? availableTools = null,
        string? sessionId = null);
}
