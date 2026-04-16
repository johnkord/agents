namespace Forge.Core.SessionMemory;

/// <summary>
/// A structured mid-session memory snapshot, modeled after Claude Code's
/// <c>summary.md</c> pattern but deliberately simpler (6 sections, not 12).
///
/// The snapshot is written to <c>.forge/session-memory/&lt;sessionId&gt;/summary.md</c>
/// (human-readable) and <c>summary.json</c> (machine-readable) at regular intervals
/// during a long session. A continuation session can hydrate from the snapshot to
/// carry intent across compaction boundaries.
///
/// Sections are intentionally prose-light and list-heavy so the extraction call
/// can be a small, fast auxiliary LLM invocation rather than a full reasoning pass.
/// </summary>
public sealed record SessionMemorySnapshot
{
    /// <summary>Restated task in the agent's own words, for drift detection across updates.</summary>
    public required string TaskDescription { get; init; }

    /// <summary>Where the agent is right now: phase, current focus, blockers.</summary>
    public required string CurrentState { get; init; }

    /// <summary>Repo-relative paths touched so far, with one-line purpose notes.</summary>
    public IReadOnlyList<string> FilesTouched { get; init; } = [];

    /// <summary>"Saw X → fixed by Y" entries. Carries forward across compactions.</summary>
    public IReadOnlyList<string> ErrorsAndFixes { get; init; } = [];

    /// <summary>Outstanding sub-tasks or follow-ups the agent has queued.</summary>
    public IReadOnlyList<string> Pending { get; init; } = [];

    /// <summary>Chronological one-liners — the structural spine for a continuation session.</summary>
    public IReadOnlyList<string> Worklog { get; init; } = [];

    /// <summary>
    /// Last step index covered by this summary. Next extraction only re-analyzes
    /// steps with <c>StepNumber &gt; LastExtractedStep</c>. Equivalent to Claude
    /// Code's <c>lastSummarizedMessageId</c>, adapted for Forge's step-indexed loop.
    /// </summary>
    public required int LastExtractedStep { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Cumulative extraction calls that produced this snapshot (1 = first).</summary>
    public required int ExtractionCount { get; init; }
}

/// <summary>
/// Inputs to a single extraction call.
/// </summary>
public sealed record SessionMemoryExtractionRequest
{
    public required string Task { get; init; }
    public required IReadOnlyList<StepRecord> Steps { get; init; }

    /// <summary>
    /// Last step already covered by <see cref="PreviousSummary"/>. Extractor should
    /// focus on steps with <c>StepNumber &gt; FromStepIndex</c> and merge-in.
    /// Null for the first extraction.
    /// </summary>
    public int? FromStepIndex { get; init; }

    /// <summary>Previous snapshot, for incremental merging. Null for first extraction.</summary>
    public SessionMemorySnapshot? PreviousSummary { get; init; }
}

public sealed record SessionMemoryExtractionResult
{
    public required SessionMemorySnapshot Snapshot { get; init; }
    public int PromptTokens { get; init; }
    public int CompletionTokens { get; init; }

    /// <summary>Raw LLM response including the stripped <c>&lt;analysis&gt;</c> block, for debug logging only.</summary>
    public string? RawResponse { get; init; }
}

/// <summary>
/// Auxiliary-LLM extractor. Injected into <see cref="SessionMemoryManager"/> so the
/// main agent loop stays decoupled from LLM-client construction and tests can swap
/// in a fake. A concrete implementation typically spawns a throwaway
/// <see cref="ILlmClient"/> with an empty tool list (NO_TOOLS constraint).
/// </summary>
public delegate Task<SessionMemoryExtractionResult> SessionMemoryExtractor(
    SessionMemoryExtractionRequest request,
    CancellationToken ct);
