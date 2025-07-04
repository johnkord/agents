using AgentAlpha.Models;

namespace AgentAlpha.Interfaces;

/// <summary>
/// Evaluates an execution plan and returns a numeric score with feedback.
/// </summary>
public interface IPlanEvaluator
{
    Task<EvaluationResult> EvaluateAsync(string plan, string task);
}
