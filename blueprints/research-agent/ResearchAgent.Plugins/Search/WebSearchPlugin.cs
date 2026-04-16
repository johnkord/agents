using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ResearchAgent.Core.Memory;
using ResearchAgent.Core.Models;

namespace ResearchAgent.Plugins.Search;

/// <summary>
/// Web search plugin for the research agent. Delegates actual HTTP traffic to an
/// <see cref="IWebSearchProvider"/> so the same plugin contract can run against a
/// simulated corpus (<see cref="SimulatedWebSearchProvider"/>) or a real API
/// (<see cref="TavilyWebSearchProvider"/>). Wiring is decided by ResearchOrchestrator
/// based on configuration (<c>Research:Search:Provider = Tavily | Simulated</c>).
/// </summary>
public sealed class WebSearchPlugin
{
    private readonly IWebSearchProvider _provider;
    private readonly ResearchMemory _memory;
    private readonly ILogger _logger;
    private readonly SearchStatistics? _stats;

    public WebSearchPlugin(IWebSearchProvider provider, ResearchMemory memory, ILoggerFactory loggerFactory, SearchStatistics? stats = null)
    {
        _provider = provider;
        _memory = memory;
        _logger = loggerFactory.CreateLogger<WebSearchPlugin>();
        _stats = stats;
    }

    [Description("Search the web for information on a given query. Returns a list of relevant results with titles, URLs, and snippets.")]
    public async Task<string> SearchWebAsync(
        [Description("The search query to execute")] string query,
        [Description("Maximum number of results to return")] int maxResults = 10)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("[TOOL] SearchWebAsync ({Provider}) — query=\"{Query}\", maxResults={MaxResults}",
            _provider.Name, query, maxResults);

        var results = await _provider.SearchWebAsync(query, maxResults, CancellationToken.None);

        foreach (var result in results)
            _memory.RegisterSource(result);

        var output = FormatResults(query, results, kind: "results");

        sw.Stop();
        _stats?.RecordCall(results.Count, sw.ElapsedMilliseconds);
        _logger.LogInformation("[TOOL] SearchWebAsync done — provider={Provider}, {ResultCount} results, {OutputChars} chars, {ElapsedMs}ms",
            _provider.Name, results.Count, output.Length, sw.ElapsedMilliseconds);

        return output;
    }

    [Description("Search for academic papers on a topic. Returns papers with titles, authors, and abstracts.")]
    public async Task<string> SearchAcademicPapersAsync(
        [Description("The academic search query")] string query,
        [Description("Maximum number of papers to return")] int maxResults = 5)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("[TOOL] SearchAcademicPapersAsync ({Provider}) — query=\"{Query}\", maxResults={MaxResults}",
            _provider.Name, query, maxResults);

        var papers = await _provider.SearchAcademicAsync(query, maxResults, CancellationToken.None);

        foreach (var p in papers)
            _memory.RegisterSource(p);

        var output = FormatResults(query, papers, kind: "academic papers");

        sw.Stop();
        _stats?.RecordCall(papers.Count, sw.ElapsedMilliseconds);
        _logger.LogInformation("[TOOL] SearchAcademicPapersAsync done — provider={Provider}, {PaperCount} papers, {OutputChars} chars, {ElapsedMs}ms",
            _provider.Name, papers.Count, output.Length, sw.ElapsedMilliseconds);

        return output;
    }

    private static string FormatResults(string query, IReadOnlyList<SourceRecord> results, string kind)
    {
        if (results.Count == 0)
            return $"No {kind} returned for: \"{query}\". Try a different query or relax filters.";

        // Surface the real SourceRecord.Id alongside each result so downstream tools
        // (RecordFinding, RecordClaimVerification) can be called with IDs that actually
        // resolve. Without this, the LLM has no option but to invent slugs — and every
        // finding becomes an orphan w.r.t. the evidence-sufficiency gate. The 'id:' label
        // is deliberate: it lines up with the '"sourceId":' parameter name models expect.
        var formatted = results.Select((r, i) =>
            $"[{i + 1}] (id: {r.Id}) {r.Title}\n    URL: {r.Url}\n    {r.Snippet}");
        return $"Found {results.Count} {kind} for: \"{query}\". " +
               $"When citing a source, pass its 'id' value (not the [1]/[2] index) to RecordFinding.\n\n" +
               string.Join("\n\n", formatted);
    }
}
