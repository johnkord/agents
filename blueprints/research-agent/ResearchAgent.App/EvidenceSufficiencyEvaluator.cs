using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ResearchAgent.Core.Models;

namespace ResearchAgent.App;

/// <summary>
/// Evidence-sufficiency gate. Runs between the Analyst loop and the Synthesizer to
/// decide whether the research produced enough real evidence to justify a synthesis
/// pass. When evidence is thin or obviously synthetic, we'd rather emit a short
/// diagnostic than have the Synthesizer author confident-sounding prose over
/// placeholder data — which is exactly the failure mode P0.4's smoke test exposed.
///
/// Design notes
/// ────────────
/// - Pure function of the current memory snapshot; no LLM calls, no mutation.
/// - Thresholds are configurable (see <see cref="EvidenceGateOptions"/>); the
///   mode (<c>Enforce</c>/<c>Warn</c>/<c>Off</c>) decides what the orchestrator
///   does with a failing verdict.
/// - Reasons are surfaced as human-readable strings for inclusion in the session
///   log and (on refuse) the diagnostic report.
///
/// References: P2.4 in <c>research/context-management-implementation-plan.md</c>.
/// </summary>
public sealed class EvidenceSufficiencyEvaluator
{
    private readonly EvidenceGateOptions _options;
    private readonly ILogger<EvidenceSufficiencyEvaluator> _logger;

    public EvidenceSufficiencyEvaluator(EvidenceGateOptions options, ILogger<EvidenceSufficiencyEvaluator> logger)
    {
        _options = options;
        _logger = logger;
    }

    public EvidenceVerdict Evaluate(
        IReadOnlyList<ResearchFinding> findings,
        IReadOnlyList<SourceRecord> sources,
        IReadOnlyList<SubQuestionProgress> progress)
    {
        var reasons = new List<string>();
        var failingQuestions = new List<string>();

        // Index sources by id for domain lookups.
        var sourceById = sources.ToDictionary(s => s.Id, s => s);

        // ── Session-wide checks ────────────────────────────────────────────
        // Simulated-source ratio: the most decisive signal. If > threshold of
        // sources look synthetic (example.com / simulated.* / localhost), the
        // Synthesizer cannot produce anything honest regardless of confidence.
        var simulatedCount = sources.Count(IsSimulatedSource);
        var simulatedRatio = sources.Count > 0 ? (double)simulatedCount / sources.Count : 0.0;
        if (simulatedRatio > _options.MaxSimulatedSourceRatio)
        {
            reasons.Add(
                $"Simulated-source ratio {simulatedRatio:P0} exceeds max {_options.MaxSimulatedSourceRatio:P0} " +
                $"({simulatedCount}/{sources.Count} sources look synthetic). " +
                "Set Research:Search:Provider=Tavily and configure Research:Tavily:ApiKey to fetch real sources.");
        }

        if (sources.Count == 0)
        {
            reasons.Add("No sources discovered during research.");
        }

        if (findings.Count == 0)
        {
            reasons.Add("No findings recorded during research.");
        }

        // ── Per-sub-question checks ────────────────────────────────────────
        // `progress` is authoritative for the set of sub-questions the Researcher
        // actually worked. Using this instead of findings.GroupBy avoids the
        // degenerate "0 findings" sub-questions dropping out silently.
        var subQuestionDetails = new List<SubQuestionEvidence>();
        foreach (var p in progress)
        {
            var qFindings = findings.Where(f => f.SubQuestionId == p.SubQuestionId).ToList();
            var qSources = qFindings
                .Select(f => sourceById.TryGetValue(f.SourceId, out var s) ? s : null)
                .Where(s => s is not null)
                .Cast<SourceRecord>()
                .ToList();

            var distinctDomains = qSources
                .Select(s => TryGetDomain(s.Url))
                .Where(d => d is not null)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var meanConf = qFindings.Count > 0 ? qFindings.Average(f => f.Confidence) : 0.0;

            var issues = new List<string>();
            if (qFindings.Count < _options.MinFindingsPerQuestion)
                issues.Add($"only {qFindings.Count} finding(s) (min {_options.MinFindingsPerQuestion})");
            if (distinctDomains < _options.MinDistinctDomainsPerQuestion)
                issues.Add($"only {distinctDomains} distinct domain(s) (min {_options.MinDistinctDomainsPerQuestion})");
            if (qFindings.Count > 0 && meanConf < _options.MinMeanConfidence)
                issues.Add($"mean confidence {meanConf:F2} < min {_options.MinMeanConfidence:F2}");

            subQuestionDetails.Add(new SubQuestionEvidence
            {
                SubQuestionId = p.SubQuestionId,
                FindingCount = qFindings.Count,
                DistinctDomainCount = distinctDomains,
                MeanConfidence = meanConf,
                Issues = issues,
            });

            if (issues.Count > 0)
            {
                failingQuestions.Add(p.SubQuestionId);
                reasons.Add($"Sub-question {p.SubQuestionId}: {string.Join("; ", issues)}.");
            }
        }

        // If progress is empty but we have findings anyway (shouldn't happen in
        // the current pipeline, but defensive) — treat as a missing-structure warning.
        if (progress.Count == 0 && findings.Count > 0)
        {
            reasons.Add("Findings exist but no sub-question progress was recorded — cannot evaluate coverage.");
        }

        var decision = reasons.Count == 0 ? EvidenceDecision.Pass : EvidenceDecision.Refuse;
        var verdict = new EvidenceVerdict
        {
            Decision = decision,
            Mode = _options.Mode,
            SimulatedSourceCount = simulatedCount,
            SimulatedSourceRatio = simulatedRatio,
            TotalSources = sources.Count,
            TotalFindings = findings.Count,
            FailingSubQuestions = failingQuestions,
            Reasons = reasons,
            PerSubQuestion = subQuestionDetails,
        };

        _logger.LogInformation(
            "Evidence gate: decision={Decision}, mode={Mode}, findings={Findings}, sources={Sources}, simulatedRatio={Simulated:P0}, failingQuestions={FailingCount}",
            verdict.Decision, verdict.Mode, findings.Count, sources.Count, simulatedRatio, failingQuestions.Count);

        return verdict;
    }

    /// <summary>Heuristics for detecting non-real sources leaked from the simulated provider.</summary>
    internal static bool IsSimulatedSource(SourceRecord s)
    {
        if (string.IsNullOrWhiteSpace(s.Url)) return true;
        var lower = s.Url.ToLowerInvariant();
        return lower.Contains("example.com")
            || lower.Contains("example.org")
            || lower.Contains("example.net")
            || lower.Contains("/simulated.")
            || lower.Contains("simulated-")
            || lower.Contains("localhost")
            || lower.StartsWith("http://127.")
            || s.Title?.StartsWith("[Simulated", StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>Extract the registered domain ("news.example.co.uk" → "example.co.uk" is out of scope — use host).</summary>
    internal static string? TryGetDomain(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        // Use Host, lowercased. Strip leading "www." so a site isn't counted twice.
        var host = uri.Host.ToLowerInvariant();
        return host.StartsWith("www.") ? host[4..] : host;
    }
}

/// <summary>Configurable thresholds for the evidence gate. All defaults are conservative.</summary>
public sealed class EvidenceGateOptions
{
    public EvidenceGateMode Mode { get; init; } = EvidenceGateMode.Warn;
    public int MinFindingsPerQuestion { get; init; } = 2;
    public int MinDistinctDomainsPerQuestion { get; init; } = 2;
    public double MinMeanConfidence { get; init; } = 0.5;
    /// <summary>0.0 → any simulated source fails the gate. 1.0 → never fails on this criterion.</summary>
    public double MaxSimulatedSourceRatio { get; init; } = 0.0;

    public static EvidenceGateOptions FromConfig(IConfiguration config)
    {
        var section = config.GetSection("Research:EvidenceGate");
        var modeStr = section["Mode"] ?? "Warn";
        var mode = Enum.TryParse<EvidenceGateMode>(modeStr, ignoreCase: true, out var m) ? m : EvidenceGateMode.Warn;
        return new EvidenceGateOptions
        {
            Mode = mode,
            MinFindingsPerQuestion = section.GetValue("MinFindingsPerQuestion", 2),
            MinDistinctDomainsPerQuestion = section.GetValue("MinDistinctDomainsPerQuestion", 2),
            MinMeanConfidence = section.GetValue("MinMeanConfidence", 0.5),
            MaxSimulatedSourceRatio = section.GetValue("MaxSimulatedSourceRatio", 0.0),
        };
    }
}

public enum EvidenceGateMode
{
    /// <summary>Gate runs but has no effect — logged only. Useful for baseline measurement.</summary>
    Off,
    /// <summary>Gate runs; failures log a warning and annotate the Synthesizer input but do NOT block synthesis.</summary>
    Warn,
    /// <summary>Gate runs; failures replace the Synthesizer output with a diagnostic report.</summary>
    Enforce,
}

public enum EvidenceDecision { Pass, Refuse }

public sealed class EvidenceVerdict
{
    public required EvidenceDecision Decision { get; init; }
    public required EvidenceGateMode Mode { get; init; }
    public required int SimulatedSourceCount { get; init; }
    public required double SimulatedSourceRatio { get; init; }
    public required int TotalSources { get; init; }
    public required int TotalFindings { get; init; }
    public required IReadOnlyList<string> FailingSubQuestions { get; init; }
    public required IReadOnlyList<string> Reasons { get; init; }
    public required IReadOnlyList<SubQuestionEvidence> PerSubQuestion { get; init; }

    /// <summary>True when the orchestrator should replace the Synthesizer pass with a diagnostic report.</summary>
    public bool ShouldRefuseSynthesis => Decision == EvidenceDecision.Refuse && Mode == EvidenceGateMode.Enforce;

    /// <summary>True when the Synthesizer should still run but with a warning notice prepended.</summary>
    public bool ShouldAnnotateSynthesis => Decision == EvidenceDecision.Refuse && Mode == EvidenceGateMode.Warn;

    /// <summary>
    /// Renders a compact, human-readable diagnostic report used when the gate refuses.
    /// Also suitable for inclusion in the session export and for Warn-mode annotation.
    /// </summary>
    public string RenderDiagnostic(string query)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Research Diagnostic — Insufficient Evidence");
        sb.AppendLine();
        sb.AppendLine($"**Query**: {query}");
        sb.AppendLine();
        sb.AppendLine("The evidence-sufficiency gate (P2.4) rejected this research session before synthesis. ");
        sb.AppendLine("Publishing a report from the gathered evidence would risk confident-sounding prose over thin or synthetic data.");
        sb.AppendLine();
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine($"- Total findings: **{TotalFindings}**");
        sb.AppendLine($"- Total sources: **{TotalSources}** (simulated: {SimulatedSourceCount}, {SimulatedSourceRatio:P0})");
        sb.AppendLine($"- Sub-questions failing evidence thresholds: **{FailingSubQuestions.Count}**");
        sb.AppendLine();
        sb.AppendLine("## Reasons");
        sb.AppendLine();
        foreach (var r in Reasons) sb.AppendLine($"- {r}");
        sb.AppendLine();
        if (PerSubQuestion.Count > 0)
        {
            sb.AppendLine("## Per-Sub-Question Coverage");
            sb.AppendLine();
            sb.AppendLine("| Sub-question | Findings | Distinct domains | Mean confidence | Status |");
            sb.AppendLine("|---|---:|---:|---:|---|");
            foreach (var q in PerSubQuestion)
            {
                var status = q.Issues.Count == 0 ? "✓" : "✗ " + string.Join("; ", q.Issues);
                sb.AppendLine($"| {q.SubQuestionId} | {q.FindingCount} | {q.DistinctDomainCount} | {q.MeanConfidence:F2} | {status} |");
            }
            sb.AppendLine();
        }
        sb.AppendLine("## Recommended Actions");
        sb.AppendLine();
        if (SimulatedSourceRatio > 0)
        {
            sb.AppendLine("- Configure a real search provider: set `Research:Search:Provider=Tavily` and `Research:Tavily:ApiKey` (user secrets).");
        }
        sb.AppendLine("- Increase `Research:MaxIterations` to give the Researcher more chances to fill gaps.");
        sb.AppendLine("- Broaden the query or split it into narrower sub-queries.");
        sb.AppendLine("- To suppress this gate for a legitimately low-evidence topic, set `Research:EvidenceGate:Mode=Off` or `Warn`.");
        return sb.ToString();
    }
}

public sealed class SubQuestionEvidence
{
    public required string SubQuestionId { get; init; }
    public required int FindingCount { get; init; }
    public required int DistinctDomainCount { get; init; }
    public required double MeanConfidence { get; init; }
    public required IReadOnlyList<string> Issues { get; init; }
}
