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

    /// <summary>
    /// When a single tool result exceeds this many characters, the full raw output
    /// is written to disk and only a preview + pointer is injected into the
    /// conversation. Lets the agent retrieve specific slices via read_file on
    /// the spill path instead of paying the full token cost on every turn.
    /// Set to 0 to disable spilling.
    /// </summary>
    public int ToolResultSpillThresholdChars { get; init; } = 20_000;

    /// <summary>
    /// Directory (relative to WorkspacePath, unless absolute) where large tool results are spilled.
    /// Sessions older than <see cref="ToolResultSpillGcDays"/> are deleted at session start.
    /// </summary>
    public string ToolResultSpillRoot { get; init; } = ".forge/tool-results";

    /// <summary>Age threshold for garbage collecting stale spill directories. Default 7 days.</summary>
    public int ToolResultSpillGcDays { get; init; } = 7;

    /// <summary>
    /// Number of consecutive read_file stub-returns allowed on the same path before the next
    /// re-read is escalated from stub to a hard BLOCKED message (P1.4). Reset on any edit to
    /// the file. Set to a large value to disable the block (stub-only mode).
    /// Default 2 — the agent gets two "SUPPRESSED" warnings, then the third re-read is blocked.
    /// </summary>
    public int FileReadStubThresholdBeforeBlock { get; init; } = 2;

    // ── P2.1: Mid-session structured memory ────────────────────────────────

    /// <summary>
    /// Enable periodic extraction of a structured session-memory snapshot
    /// (<c>.forge/session-memory/&lt;sessionId&gt;/summary.md</c>) during long tasks.
    /// Requires a session-memory extractor to be wired at the call site (see
    /// <see cref="AgentLoop.RunAsync"/>). Disabled by default while the feature
    /// collects real-world data.
    /// </summary>
    public bool SessionMemoryEnabled { get; init; } = false;

    /// <summary>Cumulative prompt+completion tokens required before the FIRST extraction fires. Default 15 000.</summary>
    public int SessionMemoryMinInitTokens { get; init; } = 15_000;

    /// <summary>Steps between subsequent extractions, counted from the last success. Default 5.</summary>
    public int SessionMemoryStepsBetweenUpdates { get; init; } = 5;

    /// <summary>
    /// Directory (relative to WorkspacePath, unless absolute) where the session memory
    /// is persisted. A per-session subdirectory is created underneath.
    /// </summary>
    public string SessionMemoryRoot { get; init; } = ".forge/session-memory";

    /// <summary>Persist raw LLM extraction responses (including the stripped &lt;analysis&gt; block) for debugging.</summary>
    public bool SessionMemoryPersistRawResponses { get; init; } = false;
}
