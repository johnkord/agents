namespace ResearchAgent.Core.Models;

/// <summary>
/// Represents the current state of a research session.
/// Tracks the session lifecycle — query is set at start,
/// phases advance through the pipeline, and FinalReport is
/// populated by the Synthesizer agent at the end.
/// </summary>
public sealed class ResearchState
{
    public string SessionId { get; init; } = Guid.NewGuid().ToString("N")[..12];
    public string Query { get; init; } = string.Empty;
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The final synthesized report, populated at the end of the workflow.
    /// </summary>
    public string? FinalReport { get; set; }

    public ResearchPhase CurrentPhase { get; set; } = ResearchPhase.Planning;

    /// <summary>
    /// How many Researcher↔Analyst iteration cycles have been completed.
    /// </summary>
    public int IterationCount { get; set; }
}

public enum ResearchPhase
{
    Planning,
    Searching,
    Reading,
    Analyzing,
    Synthesizing,
    Verifying,
    Complete
}

/// <summary>
/// A distilled research finding — the core unit of the Pensieve note system.
/// Raw content is read, key facts extracted into a Finding, then raw content pruned.
/// </summary>
public sealed class ResearchFinding
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public required string Content { get; init; }
    public required string SourceId { get; init; }
    public required string SubQuestionId { get; init; }
    public double Confidence { get; set; } = 0.5;
    public DateTimeOffset DiscoveredAt { get; init; } = DateTimeOffset.UtcNow;
    public List<string> Tags { get; init; } = [];
}

/// <summary>
/// Tracks a source discovered during research with quality metadata.
/// </summary>
public sealed class SourceRecord
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public required string Title { get; init; }
    public required string Url { get; init; }
    public string? Snippet { get; init; }
    public SourceType Type { get; set; } = SourceType.WebPage;
    public double ReliabilityScore { get; set; } = 0.5;
    public bool HasBeenRead { get; set; }
    public DateTimeOffset DiscoveredAt { get; init; } = DateTimeOffset.UtcNow;
}

public enum SourceType
{
    WebPage,
    AcademicPaper,
    Documentation,
    NewsArticle,
    ForumPost,
    Book,
    Unknown
}

/// <summary>
/// A reflection entry recording what went wrong during research and what was learned.
/// Inspired by Reflection-Driven Control (AAAI 2026): reflective memory repository
/// stores past reflections so agents avoid repeating mistakes.
/// </summary>
public sealed class ReflectionEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public required string SubQuestionId { get; init; }
    public required string OriginalAction { get; init; }
    public required string Reflection { get; init; }
    public string? RevisedAction { get; init; }
    public ReflectionOutcome Outcome { get; set; } = ReflectionOutcome.Pending;
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

public enum ReflectionOutcome
{
    Pending,
    RevisedApproachWorked,
    RevisedApproachFailed,
    Abandoned
}

/// <summary>
/// Tracks per-sub-question research progress for adaptive compute allocation.
/// Inspired by CoRefine (2026): spend more effort on hard sub-questions,
/// move on quickly from well-answered ones.
/// </summary>
public sealed class SubQuestionProgress
{
    public required string SubQuestionId { get; init; }
    public int SearchAttempts { get; set; }
    public int FindingsRecorded { get; set; }
    public double AverageConfidence { get; set; }
    public bool MarkedComplete { get; set; }
    public List<string> FailedQueries { get; init; } = [];
    public List<string> KnowledgeGaps { get; init; } = [];
}

/// <summary>
/// Result of the Verifier agent's checklist-based report verification.
/// Inspired by FINDER (2025) checklist methodology and DeepVerifier's
/// rubric-guided verification pipeline.
/// </summary>
public sealed class VerificationResult
{
    public int TotalItems { get; set; }
    public int PassedItems { get; set; }
    public int FailedItems { get; set; }
    public double PassRate => TotalItems > 0 ? (double)PassedItems / TotalItems : 0;
    public List<VerificationItem> Items { get; init; } = [];
}

/// <summary>
/// A single item in the verification checklist.
/// </summary>
public sealed class VerificationItem
{
    public required string Claim { get; init; }
    public required VerificationVerdict Verdict { get; init; }
    public string? Evidence { get; init; }
    public string? FailureCategory { get; init; }
}

public enum VerificationVerdict
{
    Supported,
    Unsupported,
    Contradicted,
    Unverifiable
}
