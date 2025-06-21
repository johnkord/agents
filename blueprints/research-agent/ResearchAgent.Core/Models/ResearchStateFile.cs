using System.Text.Json.Serialization;

namespace ResearchAgent.Core.Models;

/// <summary>
/// Serializable research state file — the durable artifact of a research session.
///
/// Design follows the "stateless-reducer with external state" pattern:
///   - The agent itself is stateless (a pure function)
///   - Research state is serialized to disk as a well-defined JSON file
///   - A prior state file can be loaded as input to a follow-up session
///   - f(query, priorState?) → (report, newState)
///
/// Inspired by:
///   - Step-DeepResearch: file system as external persistent memory
///   - 12-Factor Agents: "Memory is an input to the reducer, not internal hidden state"
///   - Pensieve/StateLM: persistent notes vs. ephemeral content separation
/// </summary>
public sealed class ResearchStateFile
{
    /// <summary>
    /// Schema version for forward compatibility. Loaders should reject unknown versions.
    /// </summary>
    [JsonPropertyName("version")]
    public int Version { get; init; } = 1;

    [JsonPropertyName("metadata")]
    public required StateFileMetadata Metadata { get; init; }

    /// <summary>
    /// The research plan (sub-questions) produced by the Planner.
    /// </summary>
    [JsonPropertyName("plan")]
    public required PlanSnapshot Plan { get; init; }

    /// <summary>
    /// Distilled research findings — the durable knowledge from the session.
    /// </summary>
    [JsonPropertyName("findings")]
    public required IReadOnlyList<ResearchFinding> Findings { get; init; }

    /// <summary>
    /// Sources consulted during research.
    /// </summary>
    [JsonPropertyName("sources")]
    public required IReadOnlyList<SourceRecord> Sources { get; init; }

    /// <summary>
    /// Analyst reflections — gap observations and methodological notes.
    /// </summary>
    [JsonPropertyName("reflections")]
    public required IReadOnlyList<ReflectionEntry> Reflections { get; init; }

    /// <summary>
    /// Quality metrics from the session.
    /// </summary>
    [JsonPropertyName("quality")]
    public required QualitySnapshot Quality { get; init; }
}

public sealed class StateFileMetadata
{
    [JsonPropertyName("sessionId")]
    public required string SessionId { get; init; }

    [JsonPropertyName("query")]
    public required string Query { get; init; }

    [JsonPropertyName("createdAt")]
    public required DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("completedAt")]
    public required DateTimeOffset CompletedAt { get; init; }

    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("provider")]
    public required string Provider { get; init; }

    /// <summary>
    /// Session ID of the prior state file that was loaded (if any).
    /// Creates a linked chain of research sessions.
    /// </summary>
    [JsonPropertyName("parentSessionId")]
    public string? ParentSessionId { get; init; }
}

public sealed class PlanSnapshot
{
    /// <summary>
    /// The raw plan text produced by the Planner agent.
    /// </summary>
    [JsonPropertyName("rawPlan")]
    public required string RawPlan { get; init; }

    /// <summary>
    /// Sub-question IDs extracted from progress tracking.
    /// </summary>
    [JsonPropertyName("subQuestionIds")]
    public required IReadOnlyList<string> SubQuestionIds { get; init; }

    /// <summary>
    /// Which sub-questions were marked complete by the research loop.
    /// </summary>
    [JsonPropertyName("completedQuestionIds")]
    public required IReadOnlyList<string> CompletedQuestionIds { get; init; }
}

public sealed class QualitySnapshot
{
    [JsonPropertyName("findingCount")]
    public int FindingCount { get; init; }

    [JsonPropertyName("sourceCount")]
    public int SourceCount { get; init; }

    [JsonPropertyName("averageFindingConfidence")]
    public double AverageFindingConfidence { get; init; }

    [JsonPropertyName("iterationCount")]
    public int IterationCount { get; init; }

    [JsonPropertyName("reflectionCount")]
    public int ReflectionCount { get; init; }

    [JsonPropertyName("verificationPassRate")]
    public double? VerificationPassRate { get; init; }

    [JsonPropertyName("failedClaims")]
    public IReadOnlyList<string>? FailedClaims { get; init; }
}
