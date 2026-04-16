using Microsoft.Extensions.Logging.Abstractions;
using ResearchAgent.App;
using ResearchAgent.Core.Models;

namespace ResearchAgent.Tests;

/// <summary>
/// Unit tests for <see cref="EvidenceSufficiencyEvaluator"/>. The evaluator is a
/// pure function so these tests hit it directly without the orchestrator.
///
/// Coverage corresponds to the P2.4 acceptance criteria in
/// <c>research/context-management-implementation-plan.md</c>.
/// </summary>
public sealed class EvidenceSufficiencyEvaluatorTests
{
    private static EvidenceSufficiencyEvaluator NewEvaluator(
        EvidenceGateMode mode = EvidenceGateMode.Enforce,
        int minFindingsPerQuestion = 2,
        int minDistinctDomainsPerQuestion = 2,
        double minMeanConfidence = 0.5,
        double maxSimulatedSourceRatio = 0.0)
    {
        var options = new EvidenceGateOptions
        {
            Mode = mode,
            MinFindingsPerQuestion = minFindingsPerQuestion,
            MinDistinctDomainsPerQuestion = minDistinctDomainsPerQuestion,
            MinMeanConfidence = minMeanConfidence,
            MaxSimulatedSourceRatio = maxSimulatedSourceRatio,
        };
        return new EvidenceSufficiencyEvaluator(options, NullLogger<EvidenceSufficiencyEvaluator>.Instance);
    }

    private static SourceRecord Source(string id, string url, double score = 0.7, string? title = null)
        => new()
        {
            Id = id,
            Title = title ?? $"Source {id}",
            Url = url,
            ReliabilityScore = score,
        };

    private static ResearchFinding Finding(string sourceId, string sq, double confidence = 0.7)
        => new()
        {
            Content = $"A finding for {sq}",
            SourceId = sourceId,
            SubQuestionId = sq,
            Confidence = confidence,
        };

    private static SubQuestionProgress Progress(string id) => new() { SubQuestionId = id };

    [Fact]
    public void Refuses_when_all_sources_are_simulated()
    {
        // Arrange: two sub-questions, four findings, all sources are example.com — i.e. the
        // pre-P0.4 smoke-test scenario. Gate should reject and supply a diagnostic.
        var sources = new[]
        {
            Source("s1", "https://example.com/result-1"),
            Source("s2", "https://example.com/result-2"),
            Source("s3", "https://example.com/result-3"),
            Source("s4", "https://arxiv.org/abs/simulated.0001"),
        };
        var findings = new[]
        {
            Finding("s1", "SQ1"), Finding("s2", "SQ1"),
            Finding("s3", "SQ2"), Finding("s4", "SQ2"),
        };
        var progress = new[] { Progress("SQ1"), Progress("SQ2") };

        var verdict = NewEvaluator().Evaluate(findings, sources, progress);

        Assert.Equal(EvidenceDecision.Refuse, verdict.Decision);
        Assert.True(verdict.SimulatedSourceRatio > 0);
        Assert.Contains(verdict.Reasons, r => r.Contains("Simulated-source ratio", StringComparison.OrdinalIgnoreCase));
        Assert.True(verdict.ShouldRefuseSynthesis, "Mode=Enforce + Refuse should block synthesis");

        var diagnostic = verdict.RenderDiagnostic("test query");
        Assert.Contains("Insufficient Evidence", diagnostic);
        Assert.Contains("test query", diagnostic);
    }

    [Fact]
    public void Passes_when_real_sources_and_coverage_meet_thresholds()
    {
        var sources = new[]
        {
            Source("s1", "https://arxiv.org/abs/2024.12345"),
            Source("s2", "https://learn.microsoft.com/agent-framework/overview"),
            Source("s3", "https://github.com/microsoft/agent-framework"),
            Source("s4", "https://nature.com/articles/s12345"),
        };
        var findings = new[]
        {
            Finding("s1", "SQ1"), Finding("s2", "SQ1"),
            Finding("s3", "SQ2"), Finding("s4", "SQ2"),
        };
        var progress = new[] { Progress("SQ1"), Progress("SQ2") };

        var verdict = NewEvaluator().Evaluate(findings, sources, progress);

        Assert.Equal(EvidenceDecision.Pass, verdict.Decision);
        Assert.Empty(verdict.Reasons);
        Assert.Empty(verdict.FailingSubQuestions);
        Assert.False(verdict.ShouldRefuseSynthesis);
        Assert.False(verdict.ShouldAnnotateSynthesis);
    }

    [Fact]
    public void Refuses_when_sub_question_has_too_few_findings()
    {
        // SQ1 is well-covered; SQ2 has only one finding — should be flagged.
        var sources = new[]
        {
            Source("s1", "https://arxiv.org/abs/a"),
            Source("s2", "https://acm.org/doi/b"),
            Source("s3", "https://nature.com/c"),
        };
        var findings = new[]
        {
            Finding("s1", "SQ1"), Finding("s2", "SQ1"),
            Finding("s3", "SQ2"),
        };
        var progress = new[] { Progress("SQ1"), Progress("SQ2") };

        var verdict = NewEvaluator().Evaluate(findings, sources, progress);

        Assert.Equal(EvidenceDecision.Refuse, verdict.Decision);
        Assert.Contains("SQ2", verdict.FailingSubQuestions);
        Assert.DoesNotContain("SQ1", verdict.FailingSubQuestions);
    }

    [Fact]
    public void Refuses_when_sub_question_only_has_single_domain()
    {
        // Two findings for SQ1, both from arxiv.org — domain-diversity requirement fails.
        var sources = new[]
        {
            Source("s1", "https://arxiv.org/abs/a"),
            Source("s2", "https://arxiv.org/abs/b"),
        };
        var findings = new[] { Finding("s1", "SQ1"), Finding("s2", "SQ1") };
        var progress = new[] { Progress("SQ1") };

        var verdict = NewEvaluator().Evaluate(findings, sources, progress);

        Assert.Equal(EvidenceDecision.Refuse, verdict.Decision);
        Assert.Contains(verdict.Reasons, r => r.Contains("distinct domain"));
    }

    [Fact]
    public void Refuses_when_mean_confidence_below_threshold()
    {
        var sources = new[]
        {
            Source("s1", "https://arxiv.org/abs/a"),
            Source("s2", "https://acm.org/doi/b"),
        };
        var findings = new[]
        {
            Finding("s1", "SQ1", confidence: 0.2),
            Finding("s2", "SQ1", confidence: 0.3),
        };
        var progress = new[] { Progress("SQ1") };

        var verdict = NewEvaluator().Evaluate(findings, sources, progress);

        Assert.Equal(EvidenceDecision.Refuse, verdict.Decision);
        Assert.Contains(verdict.Reasons, r => r.Contains("mean confidence"));
    }

    [Fact]
    public void Warn_mode_flags_but_does_not_block_synthesis()
    {
        var sources = new[] { Source("s1", "https://example.com/x") };
        var findings = new[] { Finding("s1", "SQ1") };
        var progress = new[] { Progress("SQ1") };

        var verdict = NewEvaluator(mode: EvidenceGateMode.Warn).Evaluate(findings, sources, progress);

        Assert.Equal(EvidenceDecision.Refuse, verdict.Decision);
        Assert.False(verdict.ShouldRefuseSynthesis);
        Assert.True(verdict.ShouldAnnotateSynthesis);
    }

    [Fact]
    public void Treats_www_prefixed_and_bare_hosts_as_same_domain()
    {
        // A thin but real "gotcha": www.nytimes.com and nytimes.com should count as one domain.
        var sources = new[]
        {
            Source("s1", "https://www.nytimes.com/a"),
            Source("s2", "https://nytimes.com/b"),
        };
        var findings = new[] { Finding("s1", "SQ1"), Finding("s2", "SQ1") };
        var progress = new[] { Progress("SQ1") };

        var verdict = NewEvaluator().Evaluate(findings, sources, progress);

        // Only one distinct domain ⇒ refuse.
        Assert.Equal(EvidenceDecision.Refuse, verdict.Decision);
        Assert.Contains(verdict.Reasons, r => r.Contains("distinct domain"));
    }

    [Theory]
    [InlineData("https://example.com/foo", true)]
    [InlineData("https://example.org/foo", true)]
    [InlineData("https://www.example.net/foo", true)]
    [InlineData("http://localhost:8080/foo", true)]
    [InlineData("http://127.0.0.1/foo", true)]
    [InlineData("https://arxiv.org/abs/simulated.0001", true)]
    [InlineData("https://arxiv.org/abs/2024.12345", false)]
    [InlineData("https://learn.microsoft.com/agent-framework", false)]
    [InlineData("https://github.com/microsoft/agent-framework", false)]
    public void IsSimulatedSource_matches_expected_heuristics(string url, bool expected)
    {
        var record = new SourceRecord { Title = "t", Url = url };
        Assert.Equal(expected, EvidenceSufficiencyEvaluator.IsSimulatedSource(record));
    }

    [Fact]
    public void Flags_title_marker_even_when_url_looks_real()
    {
        // Defensive: the simulated provider tags titles "[Simulated …]"; someone could
        // plug it behind an arxiv-looking URL template by accident.
        var record = new SourceRecord
        {
            Title = "[Simulated Paper 1] Research on foo",
            Url = "https://arxiv.org/abs/2024.99999",
        };
        Assert.True(EvidenceSufficiencyEvaluator.IsSimulatedSource(record));
    }

    [Fact]
    public void Passes_if_MaxSimulatedSourceRatio_loosened()
    {
        // User knowingly accepts a single placeholder source mixed into a real-search session.
        var sources = new[]
        {
            Source("s1", "https://arxiv.org/abs/a"),
            Source("s2", "https://acm.org/doi/b"),
            Source("s3", "https://nature.com/c"),
            Source("s4", "https://example.com/x"), // 1/4 simulated
        };
        var findings = new[]
        {
            Finding("s1", "SQ1"), Finding("s2", "SQ1"),
            Finding("s3", "SQ2"), Finding("s4", "SQ2"),
        };
        var progress = new[] { Progress("SQ1"), Progress("SQ2") };

        var strict = NewEvaluator(maxSimulatedSourceRatio: 0.0).Evaluate(findings, sources, progress);
        var lenient = NewEvaluator(maxSimulatedSourceRatio: 0.5).Evaluate(findings, sources, progress);

        Assert.Equal(EvidenceDecision.Refuse, strict.Decision);
        Assert.Equal(EvidenceDecision.Pass, lenient.Decision);
    }
}
