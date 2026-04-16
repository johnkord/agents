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

    /// <summary>
    /// Execution status: <c>"ok"</c> (non-empty response), <c>"empty"</c>
    /// (agent completed but emitted nothing — usually a misconfigured model or a silent LLM-call failure),
    /// or <c>"failed"</c> (exception thrown; see <see cref="ErrorText"/>).
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = "ok";

    /// <summary>
    /// Exception message when <see cref="Status"/> is <c>"failed"</c>. Null otherwise.
    /// Populated even when the orchestrator rethrows, so post-mortem session-log analysis
    /// can pinpoint which sub-agent failed and why without re-running.
    /// </summary>
    [JsonPropertyName("errorText")]
    public string? ErrorText { get; init; }
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

    /// <summary>
    /// Count of agent interactions whose <c>Status != "ok"</c> (either <c>"empty"</c> or
    /// <c>"failed"</c>). A non-zero value on a completed session is a strong signal of
    /// a misconfigured LLM (bad model name, expired key, quota), not a gap in sources.
    /// </summary>
    [JsonPropertyName("nonOkAgentInteractionCount")]
    public int NonOkAgentInteractionCount { get; init; }

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

    // ── Web-search metrics (P0.4: provider visibility for auto-eval) ──

    /// <summary>Name of the search backend used (<c>Tavily</c>, <c>Simulated</c>, …).</summary>
    [JsonPropertyName("searchProvider")]
    public string SearchProvider { get; init; } = "Simulated";

    /// <summary>Number of web-search tool invocations during the session.</summary>
    [JsonPropertyName("webSearchCallCount")]
    public int WebSearchCallCount { get; init; }

    /// <summary>Sum of results returned across all web-search calls.</summary>
    [JsonPropertyName("webSearchResultCount")]
    public int WebSearchResultCount { get; init; }

    /// <summary>Aggregate latency (ms) against the search backend.</summary>
    [JsonPropertyName("webSearchTotalLatencyMs")]
    public long WebSearchTotalLatencyMs { get; init; }

    // ── Evidence-sufficiency gate (P2.4) ──

    /// <summary>Gate mode in effect (<c>Off</c>/<c>Warn</c>/<c>Enforce</c>).</summary>
    [JsonPropertyName("evidenceGateMode")]
    public string? EvidenceGateMode { get; init; }

    /// <summary>Gate decision (<c>Pass</c>/<c>Refuse</c>). Null when gate was Off.</summary>
    [JsonPropertyName("evidenceGateDecision")]
    public string? EvidenceGateDecision { get; init; }

    /// <summary>True when synthesis was replaced with a diagnostic report.</summary>
    [JsonPropertyName("synthesisRefused")]
    public bool SynthesisRefused { get; init; }

    /// <summary>Human-readable reasons the gate cited, if any.</summary>
    [JsonPropertyName("evidenceGateReasons")]
    public IReadOnlyList<string>? EvidenceGateReasons { get; init; }

    /// <summary>Sub-question IDs that failed evidence thresholds, if any.</summary>
    [JsonPropertyName("evidenceGateFailingSubQuestions")]
    public IReadOnlyList<string>? EvidenceGateFailingSubQuestions { get; init; }

    /// <summary>Ratio of sources detected as simulated/placeholder (0..1).</summary>
    [JsonPropertyName("simulatedSourceRatio")]
    public double SimulatedSourceRatio { get; init; }

    // ── Context-management feature fingerprint (A/B sweep support) ──

    /// <summary>
    /// Compaction mode in effect for this session: <c>Off</c> / <c>ToolResultOnly</c> /
    /// <c>Pipeline</c>. Populated from <c>AI:Compaction:Mode</c> (after legacy promotion).
    /// Lets a sweep script pair session logs with the mode that produced them without
    /// depending on filename conventions.
    /// </summary>
    [JsonPropertyName("compactionMode")]
    public string? CompactionMode { get; init; }

    /// <summary>Human-readable pipeline shape, e.g. <c>Pipeline[ToolResult(&gt;8192) → Summarization(&gt;20480,≥8)]</c>.</summary>
    [JsonPropertyName("compactionShape")]
    public string? CompactionShape { get; init; }

    /// <summary>True when <c>AI:ResearchContextProvider:Enabled</c> was on for this session.</summary>
    [JsonPropertyName("researchContextProviderEnabled")]
    public bool ResearchContextProviderEnabled { get; init; }
}
