namespace Forge.Core;

using Microsoft.Extensions.AI;

/// <summary>
/// Configuration for an agent session.
/// </summary>
public sealed class AgentOptions
{
    public required string Model { get; init; }
    public int MaxSteps { get; init; } = 30;
    public int MaxTotalTokens { get; init; } = 500_000;
    public float Temperature { get; init; } = 0f;
    /// <summary>Base reasoning effort. May be escalated to High by progressive deepening on consecutive failures.</summary>
    public ReasoningEffort? ReasoningEffort { get; init; } = null;
    public int ObservationMaxLines { get; init; } = 200;
    public int ObservationMaxChars { get; init; } = 10_000;
    public bool DryRun { get; init; } = false;
    public string WorkspacePath { get; init; } = Directory.GetCurrentDirectory();
    public string SessionsPath { get; init; } = "sessions";
    /// <summary>Path to the lessons file for cross-session learning. Null to disable.</summary>
    public string? LessonsPath { get; init; } = null;
    /// <summary>Tool mode restriction for subagent processes (explore, verify, execute). Null = no restriction.</summary>
    public string? ToolMode { get; init; } = null;
}
