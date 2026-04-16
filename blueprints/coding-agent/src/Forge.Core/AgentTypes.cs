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

    /// <summary>
    /// P2.5: Context-management classification applied to this tool result. One of:
    /// <c>"spilled"</c> (large output diverted to disk by <see cref="ObservationPipeline"/>),
    /// <c>"truncated"</c> (size-gated without spill),
    /// <c>"stubbed"</c> (re-read suppressed by <see cref="VerificationState.TryGetReReadStub"/>),
    /// <c>"blocked"</c> (re-read hard-blocked after N consecutive stubs — P1.4).
    /// Null when none apply. Structured so <see cref="SessionAnalyzer"/> can count
    /// without fragile string matching against <see cref="ResultSummary"/>.
    /// </summary>
    public string? ResultTag { get; init; }

    /// <summary>
    /// When <see cref="ResultTag"/> is <c>"spilled"</c>, the on-disk path where the
    /// full raw output was persisted. Null otherwise.
    /// </summary>
    public string? SpillPath { get; init; }
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
