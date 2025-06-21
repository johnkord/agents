using System.Text.Json.Serialization;

namespace ResearchAgent.Core.Models;

/// <summary>
/// JSON-serializable session export for automated analysis and improvement.
/// Each research session is saved to disk as a structured JSON file containing
/// the full trajectory: query, agent interactions, findings, sources, and metrics.
///
/// Designed for ingestion by analysis pipelines — track agent quality across
/// sessions, identify failure patterns, measure performance, and drive improvement.
/// </summary>
public sealed class SessionExport
{
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    [JsonPropertyName("query")]
    public required string Query { get; init; }

    [JsonPropertyName("provider")]
    public required string Provider { get; init; }

    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("startedAt")]
    public required DateTimeOffset StartedAt { get; init; }

    [JsonPropertyName("completedAt")]
    public required DateTimeOffset CompletedAt { get; init; }

    [JsonPropertyName("durationMs")]
    public long DurationMs => (long)(CompletedAt - StartedAt).TotalMilliseconds;

    [JsonPropertyName("report")]
    public string? Report { get; init; }

    [JsonPropertyName("agentInteractions")]
    public required IReadOnlyList<AgentInteraction> AgentInteractions { get; init; }

    [JsonPropertyName("findings")]
    public required IReadOnlyList<ResearchFinding> Findings { get; init; }

    [JsonPropertyName("sources")]
    public required IReadOnlyList<SourceRecord> Sources { get; init; }

    [JsonPropertyName("contextLog")]
    public required IReadOnlyList<string> ContextLog { get; init; }

    [JsonPropertyName("metrics")]
    public required SessionMetrics Metrics { get; init; }
}

/// <summary>
/// A single agent interaction in the research pipeline.
/// Captures which agent responded, what it said, and when — the trajectory unit
/// that analysis pipelines use to evaluate agent behavior.
/// </summary>
public sealed class AgentInteraction
{
    [JsonPropertyName("agent")]
    public required string Agent { get; init; }

    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

    [JsonPropertyName("charCount")]
    public int CharCount => Text?.Length ?? 0;
}

/// <summary>
/// Aggregate metrics for the research session.
/// Useful for comparing quality across runs without parsing the full trajectory.
/// </summary>
public sealed class SessionMetrics
{
    [JsonPropertyName("findingCount")]
    public int FindingCount { get; init; }

    [JsonPropertyName("sourceCount")]
    public int SourceCount { get; init; }

    [JsonPropertyName("agentInteractionCount")]
    public int AgentInteractionCount { get; init; }

    [JsonPropertyName("reportCharCount")]
    public int ReportCharCount { get; init; }

    [JsonPropertyName("averageFindingConfidence")]
    public double AverageFindingConfidence { get; init; }

    [JsonPropertyName("contextLogEntryCount")]
    public int ContextLogEntryCount { get; init; }

    // ── Research iteration metrics (Gap 2: iterative loop) ──

    [JsonPropertyName("iterationCount")]
    public int IterationCount { get; init; }

    [JsonPropertyName("reflectionCount")]
    public int ReflectionCount { get; init; }

    // ── Verification metrics (Gap 1 & Gap 7: verifier + auto-eval) ──

    [JsonPropertyName("verificationChecklistItems")]
    public int VerificationChecklistItems { get; init; }

    [JsonPropertyName("verificationItemsPassed")]
    public int VerificationItemsPassed { get; init; }

    [JsonPropertyName("verificationPassRate")]
    public double VerificationPassRate { get; init; }

    [JsonPropertyName("verificationFailedItems")]
    public IReadOnlyList<string>? VerificationFailedItems { get; init; }
}
