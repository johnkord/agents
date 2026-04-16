using ResearchAgent.Core.Models;

namespace ResearchAgent.Plugins.Search;

/// <summary>
/// Simulated provider: returns a deterministic set of fake results for tests and
/// offline demos. Clearly marked — do not use for real research.
/// </summary>
public sealed class SimulatedWebSearchProvider : IWebSearchProvider
{
    public string Name => "Simulated";

    public Task<IReadOnlyList<SourceRecord>> SearchWebAsync(string query, int maxResults, CancellationToken ct)
    {
        IReadOnlyList<SourceRecord> results = Enumerable.Range(1, Math.Min(maxResults, 10)).Select(i => new SourceRecord
        {
            Title = $"[Simulated Result {i}] {query}",
            Url = $"https://example.com/result-{i}",
            Snippet = $"This result contains information about {query}. It provides relevant details and analysis.",
            Type = i % 3 == 0 ? SourceType.AcademicPaper : SourceType.WebPage,
            ReliabilityScore = 0.5 + (0.05 * (maxResults - i))
        }).ToList();
        return Task.FromResult(results);
    }

    public Task<IReadOnlyList<SourceRecord>> SearchAcademicAsync(string query, int maxResults, CancellationToken ct)
    {
        IReadOnlyList<SourceRecord> papers = Enumerable.Range(1, Math.Min(maxResults, 5)).Select(i => new SourceRecord
        {
            Title = $"[Simulated Paper {i}] Research on: {query}",
            Url = $"https://arxiv.org/abs/simulated.{i:D4}",
            Snippet = $"This paper investigates {query} and presents findings relevant to the topic.",
            Type = SourceType.AcademicPaper,
            ReliabilityScore = 0.85
        }).ToList();
        return Task.FromResult(papers);
    }
}
