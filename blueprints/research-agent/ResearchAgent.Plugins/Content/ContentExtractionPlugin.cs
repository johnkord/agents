using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ResearchAgent.Core.Memory;

namespace ResearchAgent.Plugins.Content;

/// <summary>
/// Content extraction plugin — implements StateLM's readChunk pattern.
/// Fetches and extracts content from URLs, then the agent distills findings
/// and the raw content can be pruned from context.
///
/// In production, replace with real HTTP fetching + content extraction
/// (e.g., using HtmlAgilityPack, Playwright, or a headless browser).
/// </summary>
public sealed class ContentExtractionPlugin
{
    private readonly ResearchMemory _memory;
    private readonly ILogger _logger;

    public ContentExtractionPlugin(ResearchMemory memory, ILoggerFactory loggerFactory)
    {
        _memory = memory;
        _logger = loggerFactory.CreateLogger<ContentExtractionPlugin>();
    }

    [Description("Fetch and extract the main text content from a URL. Returns the cleaned text suitable for analysis.")]
    public async Task<string> FetchContentAsync(
        [Description("The URL to fetch content from")] string url)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("[TOOL] FetchContentAsync — url=\"{Url}\"", url);

        // TODO: Replace with real HTTP fetch + HTML-to-text extraction
        await Task.Delay(200); // Simulate network latency

        // Mark the source as read in memory
        var sources = _memory.GetAllSources();
        var source = sources.FirstOrDefault(s => s.Url == url);
        if (source is not null)
        {
            source.HasBeenRead = true;
            _logger.LogDebug("[TOOL] FetchContentAsync — source {SourceId} marked as read", source.Id);
        }
        else
        {
            _logger.LogWarning("[TOOL] FetchContentAsync — no registered source matches url=\"{Url}\"", url);
        }

        // Simulated content extraction
        var content = $"""
            # Content from: {url}

            [Simulated article content]

            This is a placeholder for the actual content that would be extracted from the URL.
            In a production implementation, this would:
            1. Make an HTTP GET request to the URL
            2. Parse the HTML response
            3. Extract the main article content (removing nav, ads, sidebars)
            4. Convert to clean, readable text
            5. Optionally extract metadata (author, date, etc.)

            The extracted content would then be analyzed by the research agent,
            key findings distilled into notes (Pensieve pattern), and the raw
            content pruned from the agent's context to stay within token budget.

            [End of simulated content — approximately 150 words]
            """;

        sw.Stop();
        _logger.LogInformation("[TOOL] FetchContentAsync done — {ContentChars} chars, sourceMatched={SourceMatched}, {ElapsedMs}ms",
            content.Length, source is not null, sw.ElapsedMilliseconds);

        return content;
    }

    [Description("Extract and parse content from a PDF document URL. Returns the text content of the PDF.")]
    public async Task<string> FetchPdfContentAsync(
        [Description("The URL of the PDF to extract content from")] string pdfUrl)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("[TOOL] FetchPdfContentAsync — pdfUrl=\"{PdfUrl}\"", pdfUrl);

        // TODO: Replace with real PDF extraction
        await Task.Delay(300);

        var source = _memory.GetAllSources().FirstOrDefault(s => s.Url == pdfUrl);
        if (source is not null)
        {
            source.HasBeenRead = true;
            _logger.LogDebug("[TOOL] FetchPdfContentAsync — source {SourceId} marked as read", source.Id);
        }
        else
        {
            _logger.LogWarning("[TOOL] FetchPdfContentAsync — no registered source matches pdfUrl=\"{PdfUrl}\"", pdfUrl);
        }

        var content = $"""
            # PDF Content from: {pdfUrl}

            [Simulated PDF extraction]

            This placeholder represents content extracted from a PDF document.
            In production, this would use a PDF parsing library to extract:
            - Full text content with section headings
            - Tables and figures (as text descriptions)
            - References and citations
            - Metadata (title, authors, abstract for academic papers)

            [End of simulated PDF content]
            """;

        sw.Stop();
        _logger.LogInformation("[TOOL] FetchPdfContentAsync done — {ContentChars} chars, sourceMatched={SourceMatched}, {ElapsedMs}ms",
            content.Length, source is not null, sw.ElapsedMilliseconds);

        return content;
    }
}
