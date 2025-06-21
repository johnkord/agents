using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ResearchAgent.Core.Memory;
using ResearchAgent.Core.Models;

namespace ResearchAgent.Plugins.Search;

/// <summary>
/// Web search plugin for the research agent.
/// Provides the "information acquisition" tools from the StateLM spellbook.
///
/// In production, replace the simulated results with calls to:
/// - Bing Search API, Google Custom Search, Tavily, Brave Search, etc.
/// </summary>
public sealed class WebSearchPlugin
{
    private readonly ResearchMemory _memory;
    private readonly ILogger _logger;

    public WebSearchPlugin(ResearchMemory memory, ILoggerFactory loggerFactory)
    {
        _memory = memory;
        _logger = loggerFactory.CreateLogger<WebSearchPlugin>();
    }

    [Description("Search the web for information on a given query. Returns a list of relevant results with titles, URLs, and snippets.")]
    public async Task<string> SearchWebAsync(
        [Description("The search query to execute")] string query,
        [Description("Maximum number of results to return")] int maxResults = 10)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("[TOOL] SearchWebAsync — query=\"{Query}\", maxResults={MaxResults}", query, maxResults);

        // TODO: Replace with actual search API integration
        await Task.Delay(100); // Simulate API latency

        var results = GenerateSimulatedResults(query, maxResults);

        foreach (var result in results)
        {
            _memory.RegisterSource(result);
        }

        var formatted = results.Select((r, i) =>
            $"[{i + 1}] {r.Title}\n    URL: {r.Url}\n    {r.Snippet}");

        var output = $"Found {results.Count} results for: \"{query}\"\n\n{string.Join("\n\n", formatted)}";

        sw.Stop();
        _logger.LogInformation("[TOOL] SearchWebAsync done — {ResultCount} results, {OutputChars} chars, {ElapsedMs}ms",
            results.Count, output.Length, sw.ElapsedMilliseconds);

        return output;
    }

    [Description("Search for academic papers on a topic. Returns papers with titles, authors, and abstracts.")]
    public async Task<string> SearchAcademicPapersAsync(
        [Description("The academic search query")] string query,
        [Description("Maximum number of papers to return")] int maxResults = 5)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("[TOOL] SearchAcademicPapersAsync — query=\"{Query}\", maxResults={MaxResults}",
            query, maxResults);

        await Task.Delay(100);

        var papers = Enumerable.Range(1, Math.Min(maxResults, 5)).Select(i => new SourceRecord
        {
            Title = $"[Simulated Paper {i}] Research on: {query}",
            Url = $"https://arxiv.org/abs/simulated.{i:D4}",
            Snippet = $"This paper investigates {query} and presents findings relevant to the topic.",
            Type = SourceType.AcademicPaper,
            ReliabilityScore = 0.85
        }).ToList();

        foreach (var paper in papers)
        {
            _memory.RegisterSource(paper);
        }

        var formatted = papers.Select((p, i) =>
            $"[{i + 1}] {p.Title}\n    URL: {p.Url}\n    Abstract: {p.Snippet}");

        var output = $"Found {papers.Count} academic papers for: \"{query}\"\n\n{string.Join("\n\n", formatted)}";

        sw.Stop();
        _logger.LogInformation("[TOOL] SearchAcademicPapersAsync done — {PaperCount} papers, {OutputChars} chars, {ElapsedMs}ms",
            papers.Count, output.Length, sw.ElapsedMilliseconds);

        return output;
    }

    private static List<SourceRecord> GenerateSimulatedResults(string query, int maxResults)
    {
        return Enumerable.Range(1, Math.Min(maxResults, 10)).Select(i => new SourceRecord
        {
            Title = $"[Simulated Result {i}] {query}",
            Url = $"https://example.com/result-{i}",
            Snippet = $"This result contains information about {query}. It provides relevant details and analysis.",
            Type = i % 3 == 0 ? SourceType.AcademicPaper : SourceType.WebPage,
            ReliabilityScore = 0.5 + (0.05 * (maxResults - i))
        }).ToList();
    }
}
