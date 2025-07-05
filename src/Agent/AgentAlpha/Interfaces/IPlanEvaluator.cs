using AgentAlpha.Models;

namespace AgentAlpha.Interfaces;

/// <summary>
/// Evaluates an execution plan and returns a numeric score with feedback.
/// </summary>
public interface IPlanEvaluator
{
    /// <summary>
    /// Evaluate the quality of an execution plan for a given task
    /// </summary>
    /// <param name="plan">The execution plan to evaluate</param>
    /// <param name="task">The original task description</param>
    /// <returns>Evaluation result with score (0.0-1.0) and feedback</returns>
    /// <exception cref="ArgumentException">Thrown when plan or task is null or empty</exception>
    Task<EvaluationResult> EvaluateAsync(string plan, string task);
}
