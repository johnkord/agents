namespace ResearchAgent.Core.Models;

/// <summary>
/// Progress events streamed during research execution.
/// Designed for real-time display (stderr in CLI, event stream in service).
///
/// Inspired by ResearStudio's event-driven protocol: stream actions (what the agent
/// is doing), not findings (which may be revised). Findings are in the final report.
/// </summary>
public sealed class ResearchProgressEvent
{
    public required ResearchProgressKind Kind { get; init; }
    public required string Message { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Optional structured data (e.g., finding count, iteration number).
    /// </summary>
    public int? NumericValue { get; init; }

    public override string ToString() => Kind switch
    {
        ResearchProgressKind.PhaseChange => $"► {Message}",
        ResearchProgressKind.Iteration => $"↻ {Message}",
        ResearchProgressKind.FindingDiscovered => $"  ● {Message}",
        ResearchProgressKind.GapIdentified => $"  △ {Message}",
        ResearchProgressKind.SessionInfo => $"  {Message}",
        _ => Message
    };
}

public enum ResearchProgressKind
{
    /// <summary>Pipeline phase transition (Planning → Researching → Analyzing → etc.).</summary>
    PhaseChange,

    /// <summary>Research↔Analyst iteration cycle starting.</summary>
    Iteration,

    /// <summary>A new finding was recorded.</summary>
    FindingDiscovered,

    /// <summary>Analyst identified a knowledge gap.</summary>
    GapIdentified,

    /// <summary>Informational (session ID, config, timing).</summary>
    SessionInfo,
}
