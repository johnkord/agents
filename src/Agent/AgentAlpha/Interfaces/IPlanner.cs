namespace AgentAlpha.Interfaces;

/// <summary>
/// Abstraction over any planning strategy (single-shot or prompt-chained).
/// </summary>
public interface IPlanner
{
    /// <summary>
    /// Create a first-pass execution plan.
    /// </summary>
    /// <param name="task">The task description to plan for</param>
    /// <param name="availableTools">List of available tool names (optional)</param>
    /// <param name="sessionId">Session ID for tracking (optional)</param>
    /// <returns>A structured execution plan as a string</returns>
    /// <exception cref="ArgumentException">Thrown when task is null or empty</exception>
    Task<string> CreatePlanAsync(
        string task,
        IList<string>? availableTools = null,
        string? sessionId = null);

    /// <summary>
    /// Refine an existing plan based on evaluator feedback.
    /// </summary>
    /// <param name="existingPlan">The current plan to refine</param>
    /// <param name="feedback">Feedback from the plan evaluator</param>
    /// <param name="sessionId">Session ID for tracking (optional)</param>
    /// <returns>An improved execution plan</returns>
    /// <exception cref="ArgumentException">Thrown when existingPlan or feedback is null or empty</exception>
    Task<string> RefinePlanAsync(
        string existingPlan,
        string feedback,
        string? sessionId = null);
}
