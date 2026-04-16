using ResearchAgent.Core.Models;

namespace ResearchAgent.Plugins.Search;

/// <summary>
/// Backend for <see cref="WebSearchPlugin"/> search calls. Swapping implementations
/// lets the same plugin contract run against a simulated corpus (tests, offline demos)
/// or a real web-search API (Tavily, Brave, …) without changing agent orchestration.
/// </summary>
public interface IWebSearchProvider
{
    /// <summary>A short, human-readable provider name for logs (e.g. "Simulated", "Tavily").</summary>
    string Name { get; }

    /// <summary>Run a general-web search. Returns freshly-minted <see cref="SourceRecord"/>s.</summary>
    Task<IReadOnlyList<SourceRecord>> SearchWebAsync(string query, int maxResults, CancellationToken ct);

    /// <summary>
    /// Run an academic-paper search. Default implementation falls back to <see cref="SearchWebAsync"/>
    /// with a query prefix — providers with native academic endpoints (Semantic Scholar, arXiv)
    /// should override.
    /// </summary>
    Task<IReadOnlyList<SourceRecord>> SearchAcademicAsync(string query, int maxResults, CancellationToken ct)
        => SearchWebAsync($"academic paper OR arxiv OR peer-reviewed: {query}", maxResults, ct);
}
