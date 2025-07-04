namespace AgentAlpha.Models;

/// <summary>
/// Result returned by IPlanEvaluator.
/// </summary>
public readonly record struct EvaluationResult(double Score, string Feedback);
