using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ResearchAgent.Core.Memory;
using ResearchAgent.Core.Models;

namespace ResearchAgent.Plugins.Verification;

/// <summary>
/// Verification plugin — implements checklist-based report verification.
///
/// Inspired by:
/// - FINDER (2025): Checklist methodology with 419 specific items across 100 tasks
/// - DeepVerifier (2025–2026): Rubric-guided verification pipeline
/// - The Asymmetry Thesis: Verification is fundamentally easier than generation,
///   so a lightweight verification step provides outsized returns
///
/// The Verifier agent uses these tools to generate a checklist of verifiable claims
/// from the report, check each against accumulated findings, and produce a
/// verification summary.
/// </summary>
public sealed class VerificationPlugin
{
    private readonly ResearchMemory _memory;
    private readonly ILogger _logger;

    public VerificationPlugin(ResearchMemory memory, ILoggerFactory loggerFactory)
    {
        _memory = memory;
        _logger = loggerFactory.CreateLogger<VerificationPlugin>();
    }

    [Description("Get all accumulated research findings and sources for verification. "
        + "Use this to access the evidence base before verifying claims in the report.")]
    public string GetVerificationContext()
    {
        _logger.LogInformation("[TOOL] GetVerificationContext called");

        var findings = _memory.GetAllFindings();
        var sources = _memory.GetAllSources();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"## Evidence Base for Verification");
        sb.AppendLine($"**{findings.Count} findings** from **{sources.Count} sources**");
        sb.AppendLine();

        sb.AppendLine("### Findings (ordered by confidence):");
        foreach (var f in findings)
        {
            sb.AppendLine($"- [{f.Confidence:P0}] [SubQ: {f.SubQuestionId}] {f.Content} (Source: {f.SourceId})");
        }

        sb.AppendLine();
        sb.AppendLine("### Sources:");
        foreach (var s in sources.Where(s => s.HasBeenRead))
        {
            sb.AppendLine($"- [{s.Id}] {s.Title} ({s.Url}) — reliability: {s.ReliabilityScore:F1}");
        }

        var output = sb.ToString();
        _logger.LogInformation("[TOOL] GetVerificationContext done — {OutputChars} chars", output.Length);
        return output;
    }

    [Description("Record the verification result for a specific claim from the report. "
        + "Verdict should be one of: SUPPORTED (backed by findings), UNSUPPORTED (no evidence found), "
        + "CONTRADICTED (evidence contradicts the claim), or UNVERIFIABLE (cannot be checked against findings). "
        + "Provide the specific evidence or reason for the verdict.")]
    public string RecordClaimVerification(
        [Description("The specific claim from the report being verified")] string claim,
        [Description("Verdict: SUPPORTED, UNSUPPORTED, CONTRADICTED, or UNVERIFIABLE")] string verdict,
        [Description("The evidence or reasoning supporting this verdict")] string evidence,
        [Description("If failed: the DEFT failure category (Factual, Reasoning, Completeness, Coherence, Attribution)")] string failureCategory = "")
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("[TOOL] RecordClaimVerification — verdict={Verdict}, claimLen={ClaimLen}",
            verdict, claim.Length);

        var parsedVerdict = verdict.ToUpperInvariant() switch
        {
            "SUPPORTED" => VerificationVerdict.Supported,
            "UNSUPPORTED" => VerificationVerdict.Unsupported,
            "CONTRADICTED" => VerificationVerdict.Contradicted,
            _ => VerificationVerdict.Unverifiable
        };

        // Store in memory context log for tracking
        _memory.LogVerificationItem(claim, parsedVerdict, evidence, failureCategory);

        sw.Stop();
        _logger.LogInformation("[TOOL] RecordClaimVerification done — {Verdict}, {ElapsedMs}ms",
            parsedVerdict, sw.ElapsedMilliseconds);

        return $"Claim verification recorded: {parsedVerdict}. Evidence: {evidence}";
    }

    [Description("Get the accumulated verification results as a structured summary. "
        + "Call this after verifying all claims to produce the final verification report.")]
    public string GetVerificationSummary()
    {
        _logger.LogInformation("[TOOL] GetVerificationSummary called");

        var result = _memory.GetVerificationResult();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("## Verification Summary");
        sb.AppendLine();
        sb.AppendLine($"**Total claims checked: {result.TotalItems}**");
        sb.AppendLine($"- ✅ Supported: {result.Items.Count(i => i.Verdict == VerificationVerdict.Supported)}");
        sb.AppendLine($"- ❌ Unsupported: {result.Items.Count(i => i.Verdict == VerificationVerdict.Unsupported)}");
        sb.AppendLine($"- ⚠️ Contradicted: {result.Items.Count(i => i.Verdict == VerificationVerdict.Contradicted)}");
        sb.AppendLine($"- ❓ Unverifiable: {result.Items.Count(i => i.Verdict == VerificationVerdict.Unverifiable)}");
        sb.AppendLine($"- **Pass rate: {result.PassRate:P0}**");
        sb.AppendLine();

        var failed = result.Items.Where(i => i.Verdict != VerificationVerdict.Supported).ToList();
        if (failed.Count > 0)
        {
            sb.AppendLine("### Issues Found:");
            foreach (var item in failed)
            {
                sb.AppendLine($"- [{item.Verdict}] {item.Claim}");
                sb.AppendLine($"  Evidence: {item.Evidence}");
                if (!string.IsNullOrEmpty(item.FailureCategory))
                    sb.AppendLine($"  Category: {item.FailureCategory}");
            }
        }

        // Group by failure category
        var categories = failed
            .Where(i => !string.IsNullOrEmpty(i.FailureCategory))
            .GroupBy(i => i.FailureCategory)
            .ToList();
        if (categories.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Failure Breakdown by DEFT Category:");
            foreach (var cat in categories.OrderByDescending(g => g.Count()))
            {
                sb.AppendLine($"- {cat.Key}: {cat.Count()} issues");
            }
        }

        var output = sb.ToString();
        _logger.LogInformation("[TOOL] GetVerificationSummary done — passRate={PassRate:P0}, total={Total}, passed={Passed}",
            result.PassRate, result.TotalItems, result.PassedItems);
        return output;
    }
}
