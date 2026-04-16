using System.Collections.Concurrent;
using ResearchAgent.Core.Models;

namespace ResearchAgent.Core.Memory;

/// <summary>
/// Implements the Pensieve memory pattern from StateLM:
/// - Persistent notes survive context pruning
/// - Raw content is ephemeral — read, distill, discard
/// - Budget-aware: tracks token usage and triggers pruning
///
/// Extended with:
/// - Reflective Memory Repository (Reflection-Driven Control, AAAI 2026):
///   stores past reflections so agents avoid repeating mistakes
/// - Sub-question progress tracking (CoRefine-inspired adaptive compute):
///   tracks per-question effort to allocate compute where most needed
/// </summary>
public sealed class ResearchMemory
{
    private readonly ConcurrentDictionary<string, ResearchFinding> _findings = new();
    private readonly ConcurrentDictionary<string, SourceRecord> _sources = new();
    private readonly ConcurrentDictionary<string, string> _workingNotes = new();
    private readonly ConcurrentDictionary<string, SubQuestionProgress> _subQuestionProgress = new();
    private readonly List<ReflectionEntry> _reflections = [];
    private readonly List<string> _contextLog = [];
    private readonly object _logLock = new();
    private readonly object _reflectionLock = new();
    private volatile string? _researchQuestion;
    private volatile string? _plannerOutput;

    /// <summary>
    /// The top-level research question for the current session. Set once at session start
    /// by the orchestrator; read by any <c>AIContextProvider</c> that injects state
    /// into sub-agent instructions. Null until the orchestrator populates it.
    /// </summary>
    public string? ResearchQuestion => _researchQuestion;

    /// <summary>
    /// The Planner's full output for the current session. Set once after Phase 3 by the
    /// orchestrator; read by any <c>AIContextProvider</c> that wants to surface the plan
    /// to downstream agents without re-embedding it in user-message payloads.
    /// </summary>
    public string? PlannerOutput => _plannerOutput;

    /// <summary>Records the current session's research question. Idempotent; last write wins.</summary>
    public void SetResearchQuestion(string question)
    {
        _researchQuestion = question;
        LogEvent($"Research question set: {Truncate(question, 120)}");
    }

    /// <summary>Records the Planner's output for downstream context-provider consumption.</summary>
    public void SetPlannerOutput(string plannerOutput)
    {
        _plannerOutput = plannerOutput;
        LogEvent($"Planner output recorded ({plannerOutput.Length} chars)");
    }

    /// <summary>
    /// Load findings and sources from a prior research state file.
    /// Implements the "stateless-reducer with external state" pattern:
    /// prior knowledge is an explicit input, not hidden internal state.
    /// </summary>
    public void LoadPriorState(ResearchStateFile priorState)
    {
        foreach (var finding in priorState.Findings)
        {
            _findings[finding.Id] = finding;
        }

        foreach (var source in priorState.Sources)
        {
            _sources[source.Id] = source;
        }

        lock (_reflectionLock)
        {
            foreach (var reflection in priorState.Reflections)
            {
                _reflections.Add(reflection);
            }
        }

        LogEvent($"Loaded prior state: {priorState.Findings.Count} findings, {priorState.Sources.Count} sources, {priorState.Reflections.Count} reflections from session {priorState.Metadata.SessionId}");
    }

    /// <summary>
    /// Store a distilled finding (the "note" operation in Pensieve).
    /// This persists even when raw content is pruned from context.
    /// </summary>
    public void StoreFinding(ResearchFinding finding)
    {
        _findings[finding.Id] = finding;
        LogEvent($"Stored finding {finding.Id}: {Truncate(finding.Content, 80)}");
    }

    /// <summary>
    /// Store or update a working note for a sub-question.
    /// Analogous to StateLM's updateNote tool.
    /// </summary>
    public void UpdateNote(string subQuestionId, string note)
    {
        _workingNotes[subQuestionId] = note;
        LogEvent($"Updated note for sub-question {subQuestionId}");
    }

    /// <summary>
    /// Read accumulated notes (StateLM's readNote tool).
    /// </summary>
    public string ReadNote(string subQuestionId)
    {
        return _workingNotes.TryGetValue(subQuestionId, out var note) ? note : string.Empty;
    }

    /// <summary>
    /// Register a discovered source.
    /// </summary>
    public void RegisterSource(SourceRecord source)
    {
        _sources[source.Id] = source;
        LogEvent($"Registered source {source.Id}: {source.Title}");
    }

    /// <summary>
    /// Get all findings for a given sub-question.
    /// </summary>
    public IReadOnlyList<ResearchFinding> GetFindingsForQuestion(string subQuestionId)
    {
        return _findings.Values
            .Where(f => f.SubQuestionId == subQuestionId)
            .OrderByDescending(f => f.Confidence)
            .ToList();
    }

    /// <summary>
    /// Get all findings across all sub-questions, ordered by confidence.
    /// Used during synthesis to build the final report.
    /// </summary>
    public IReadOnlyList<ResearchFinding> GetAllFindings()
    {
        return _findings.Values
            .OrderByDescending(f => f.Confidence)
            .ToList();
    }

    /// <summary>
    /// Get all registered sources.
    /// </summary>
    public IReadOnlyList<SourceRecord> GetAllSources()
    {
        return _sources.Values.ToList();
    }

    /// <summary>
    /// Build a context summary suitable for injection into an agent's prompt.
    /// This implements the "compact state" concept from Pensieve —
    /// only distilled notes, not raw content.
    /// Now includes reflections for the Reflective Memory Repository pattern.
    /// </summary>
    public string BuildContextSummary()
    {
        var sections = new List<string>();

        if (_workingNotes.Count > 0)
        {
            sections.Add("## Working Notes");
            foreach (var (questionId, note) in _workingNotes)
            {
                sections.Add($"### {questionId}\n{note}");
            }
        }

        if (_findings.Count > 0)
        {
            sections.Add($"## Key Findings ({_findings.Count} total)");
            foreach (var finding in _findings.Values.OrderByDescending(f => f.Confidence).Take(20))
            {
                sections.Add($"- [{finding.Confidence:P0}] {finding.Content} (Source: {finding.SourceId})");
            }
        }

        if (_sources.Count > 0)
        {
            sections.Add($"## Sources ({_sources.Count} total)");
            foreach (var source in _sources.Values.OrderByDescending(s => s.ReliabilityScore).Take(15))
            {
                var readStatus = source.HasBeenRead ? "✓" : "○";
                sections.Add($"- [{readStatus}] {source.Title} ({source.Url}) — reliability: {source.ReliabilityScore:F1}");
            }
        }

        lock (_reflectionLock)
        {
            if (_reflections.Count > 0)
            {
                sections.Add($"## Reflections ({_reflections.Count} total)");
                foreach (var reflection in _reflections.TakeLast(10))
                {
                    sections.Add($"- [{reflection.SubQuestionId}] {reflection.OriginalAction} → {reflection.Reflection} ({reflection.Outcome})");
                }
            }
        }

        if (!_subQuestionProgress.IsEmpty)
        {
            sections.Add("## Sub-Question Progress");
            foreach (var (id, progress) in _subQuestionProgress)
            {
                var status = progress.MarkedComplete ? "✓ COMPLETE" : $"searches={progress.SearchAttempts}, findings={progress.FindingsRecorded}";
                sections.Add($"- {id}: {status} (avg confidence: {progress.AverageConfidence:P0})");
                if (progress.KnowledgeGaps.Count > 0)
                    sections.Add($"  Gaps: {string.Join("; ", progress.KnowledgeGaps)}");
            }
        }

        return string.Join("\n\n", sections);
    }

    // ── Reflection Memory (Reflection-Driven Control, AAAI 2026) ──

    /// <summary>
    /// Store a reflection about what went wrong and what to try instead.
    /// </summary>
    public void StoreReflection(ReflectionEntry entry)
    {
        lock (_reflectionLock)
        {
            _reflections.Add(entry);
        }
        LogEvent($"Stored reflection for {entry.SubQuestionId}: {Truncate(entry.Reflection, 80)}");
    }

    /// <summary>
    /// Get all reflections, optionally filtered by sub-question.
    /// </summary>
    public IReadOnlyList<ReflectionEntry> GetReflections(string? subQuestionId = null)
    {
        lock (_reflectionLock)
        {
            return subQuestionId is null
                ? [.. _reflections]
                : _reflections.Where(r => r.SubQuestionId == subQuestionId).ToList();
        }
    }

    // ── Sub-Question Progress Tracking (CoRefine-inspired) ──

    /// <summary>
    /// Get or create progress tracking for a sub-question.
    /// </summary>
    public SubQuestionProgress GetOrCreateProgress(string subQuestionId)
    {
        return _subQuestionProgress.GetOrAdd(subQuestionId, id => new SubQuestionProgress
        {
            SubQuestionId = id
        });
    }

    /// <summary>
    /// Record a search attempt for a sub-question.
    /// </summary>
    public void RecordSearchAttempt(string subQuestionId, string? failedQuery = null)
    {
        var progress = GetOrCreateProgress(subQuestionId);
        progress.SearchAttempts++;
        if (failedQuery is not null)
            progress.FailedQueries.Add(failedQuery);
    }

    /// <summary>
    /// Update progress after a finding is recorded.
    /// </summary>
    public void UpdateProgressForFinding(string subQuestionId, double confidence)
    {
        var progress = GetOrCreateProgress(subQuestionId);
        progress.FindingsRecorded++;
        // Running average
        progress.AverageConfidence = progress.FindingsRecorded == 1
            ? confidence
            : (progress.AverageConfidence * (progress.FindingsRecorded - 1) + confidence) / progress.FindingsRecorded;
    }

    /// <summary>
    /// Record a knowledge gap identified by the Analyst.
    /// </summary>
    public void RecordKnowledgeGap(string subQuestionId, string gap)
    {
        var progress = GetOrCreateProgress(subQuestionId);
        progress.KnowledgeGaps.Add(gap);
    }

    /// <summary>
    /// Get sub-questions that need more research.
    /// A sub-question is under-researched if:
    ///   - It has fewer than 2 findings (insufficient breadth), OR
    ///   - Its average confidence is below 0.5 (low quality), OR
    ///   - It has more unresolved knowledge gaps than findings recorded
    /// The gap condition uses a ratio (gaps > findings) rather than gaps > 0,
    /// so that sub-questions with gaps BUT sufficient findings aren't re-flagged
    /// indefinitely across iterations.
    /// </summary>
    public IReadOnlyList<SubQuestionProgress> GetUnderResearchedQuestions()
    {
        return _subQuestionProgress.Values
            .Where(p => !p.MarkedComplete &&
                (p.FindingsRecorded < 2 ||
                 p.AverageConfidence < 0.5 ||
                 p.KnowledgeGaps.Count > p.FindingsRecorded))
            .OrderBy(p => p.AverageConfidence)
            .ToList();
    }

    /// <summary>
    /// Clear all recorded knowledge gaps across sub-questions.
    /// Called between iteration cycles so only freshly-identified gaps drive re-research.
    /// </summary>
    public void ClearKnowledgeGaps()
    {
        foreach (var progress in _subQuestionProgress.Values)
        {
            progress.KnowledgeGaps.Clear();
        }
    }

    /// <summary>
    /// Get all sub-question progress entries.
    /// </summary>
    public IReadOnlyList<SubQuestionProgress> GetAllProgress()
    {
        return _subQuestionProgress.Values.ToList();
    }

    // ── Verification (FINDER checklist + DeepVerifier rubric) ──

    private readonly List<VerificationItem> _verificationItems = [];
    private readonly object _verificationLock = new();

    /// <summary>
    /// Log a single verification item (claim check result).
    /// </summary>
    public void LogVerificationItem(string claim, VerificationVerdict verdict, string evidence, string failureCategory)
    {
        var item = new VerificationItem
        {
            Claim = claim,
            Verdict = verdict,
            Evidence = evidence,
            FailureCategory = string.IsNullOrWhiteSpace(failureCategory) ? null : failureCategory
        };

        lock (_verificationLock)
        {
            _verificationItems.Add(item);
        }

        LogEvent($"Verification: {verdict} — {Truncate(claim, 60)}");
    }

    /// <summary>
    /// Get the aggregated verification result.
    /// </summary>
    public VerificationResult GetVerificationResult()
    {
        lock (_verificationLock)
        {
            var items = _verificationItems.ToList();
            return new VerificationResult
            {
                TotalItems = items.Count,
                PassedItems = items.Count(i => i.Verdict == VerificationVerdict.Supported),
                FailedItems = items.Count(i => i.Verdict != VerificationVerdict.Supported),
                Items = items
            };
        }
    }

    /// <summary>
    /// Estimate the token count of the current memory state.
    /// Rough approximation: ~4 chars per token.
    /// </summary>
    public int EstimateTokenCount()
    {
        var totalChars = _workingNotes.Values.Sum(n => n.Length)
            + _findings.Values.Sum(f => f.Content.Length)
            + _sources.Values.Sum(s => (s.Title?.Length ?? 0) + (s.Snippet?.Length ?? 0));

        lock (_reflectionLock)
        {
            totalChars += _reflections.Sum(r => r.Reflection.Length + r.OriginalAction.Length);
        }

        return totalChars / 4;
    }

    public IReadOnlyList<string> GetContextLog()
    {
        lock (_logLock)
        {
            return [.. _contextLog];
        }
    }

    private void LogEvent(string message)
    {
        lock (_logLock)
        {
            _contextLog.Add($"[{DateTimeOffset.UtcNow:HH:mm:ss}] {message}");
        }
    }

    private static string Truncate(string text, int maxLength)
    {
        return text.Length <= maxLength ? text : string.Concat(text.AsSpan(0, maxLength), "...");
    }
}
