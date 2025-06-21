namespace Forge.Core;

/// <summary>
/// Immutable record of a single step in the agent loop.
/// </summary>
public sealed record StepRecord
{
    public required int StepNumber { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public string? Thought { get; init; }
    public IReadOnlyList<ToolCallRecord> ToolCalls { get; init; } = [];
    public int PromptTokens { get; init; }
    public int CompletionTokens { get; init; }
    public double DurationMs { get; init; }
}

public sealed record ToolCallRecord
{
    public required string ToolName { get; init; }
    public required string Arguments { get; init; }
    public required string ResultSummary { get; init; }
    public int ResultLength { get; init; }
    public bool IsError { get; init; }
    public double DurationMs { get; init; }
}

/// <summary>
/// Result of a complete agent session.
/// </summary>
public sealed record AgentResult
{
    public required bool Success { get; init; }
    public required string Output { get; init; }
    public required IReadOnlyList<StepRecord> Steps { get; init; }
    public int TotalPromptTokens { get; init; }
    public int TotalCompletionTokens { get; init; }
    public double TotalDurationMs { get; init; }
    public string? SessionLogPath { get; init; }
    public string? FailureReason { get; init; }
}
