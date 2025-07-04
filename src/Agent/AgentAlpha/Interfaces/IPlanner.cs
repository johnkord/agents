namespace AgentAlpha.Interfaces;

/// <summary>
/// Abstraction over any planning strategy (single-shot or prompt-chained).
/// </summary>
public interface IPlanner
{
    /// <summary>
    /// Create a first-pass execution plan.
    /// </summary>
    Task<string> CreatePlanAsync(
        string task,
        IList<string>? availableTools = null,
        string? sessionId = null);

    /// <summary>
    /// Refine an existing plan based on evaluator feedback.
    /// </summary>
    Task<string> RefinePlanAsync(
        string existingPlan,
        string feedback,
        string? sessionId = null);
}
