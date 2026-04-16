using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ResearchAgent.Core.Models;

namespace ResearchAgent.Plugins.Search;

/// <summary>
/// Real-web-search provider backed by <see href="https://tavily.com">Tavily</see>.
/// Tavily is an LLM-oriented search API that returns clean text extracts rather than
/// raw HTML, which fits the research-agent's observation budget well.
///
/// POST https://api.tavily.com/search
///   { "query": "...", "search_depth": "basic"|"advanced", "max_results": N,
///     "include_answer": false, "include_raw_content": false }
/// Header: Authorization: Bearer &lt;key&gt;  (older docs used an "api_key" body field;
/// both are currently accepted — we send the header form for hygiene.)
///
/// Reliability score mapping: Tavily returns a 0..1 "score" per result; we pass it
/// through directly so downstream evidence-sufficiency logic (P2.4) can weight it.
/// </summary>
public sealed class TavilyWebSearchProvider : IWebSearchProvider
{
    private const string TavilyEndpoint = "https://api.tavily.com/search";

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly ILogger<TavilyWebSearchProvider> _logger;
    private readonly string _searchDepth;

    public string Name => "Tavily";

    public TavilyWebSearchProvider(HttpClient httpClient, string apiKey, ILogger<TavilyWebSearchProvider> logger, string searchDepth = "basic")
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("Tavily API key is required", nameof(apiKey));
        _http = httpClient;
        _apiKey = apiKey;
        _logger = logger;
        _searchDepth = string.Equals(searchDepth, "advanced", StringComparison.OrdinalIgnoreCase) ? "advanced" : "basic";
    }

    public async Task<IReadOnlyList<SourceRecord>> SearchWebAsync(string query, int maxResults, CancellationToken ct)
    {
        return await QueryAsync(query, maxResults, academic: false, ct);
    }

    public async Task<IReadOnlyList<SourceRecord>> SearchAcademicAsync(string query, int maxResults, CancellationToken ct)
    {
        // Bias toward academic domains — Tavily honors include_domains.
        return await QueryAsync(query, maxResults, academic: true, ct);
    }

    private async Task<IReadOnlyList<SourceRecord>> QueryAsync(string query, int maxResults, bool academic, CancellationToken ct)
    {
        var body = new TavilyRequest
        {
            Query = query,
            SearchDepth = _searchDepth,
            MaxResults = Math.Clamp(maxResults, 1, 20),
            IncludeAnswer = false,
            IncludeRawContent = false,
            IncludeDomains = academic
                ? new[] { "arxiv.org", "scholar.google.com", "semanticscholar.org", "pubmed.ncbi.nlm.nih.gov", "acm.org", "ieee.org", "nature.com", "science.org" }
                : null,
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, TavilyEndpoint)
        {
            Content = JsonContent.Create(body, options: SerializerOptions),
        };
        req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_apiKey}");

        HttpResponseMessage resp;
        try
        {
            resp = await _http.SendAsync(req, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tavily request failed for query=\"{Query}\"", query);
            return Array.Empty<SourceRecord>();
        }

        if (!resp.IsSuccessStatusCode)
        {
            var errBody = await SafeReadAsync(resp, ct);
            _logger.LogWarning("Tavily HTTP {Status} for query=\"{Query}\": {Body}",
                (int)resp.StatusCode, query, Truncate(errBody, 500));
            return Array.Empty<SourceRecord>();
        }

        TavilyResponse? payload;
        try
        {
            payload = await resp.Content.ReadFromJsonAsync<TavilyResponse>(SerializerOptions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tavily response JSON parse failed");
            return Array.Empty<SourceRecord>();
        }

        if (payload?.Results is null || payload.Results.Count == 0)
            return Array.Empty<SourceRecord>();

        return payload.Results.Select(r => new SourceRecord
        {
            Title = string.IsNullOrWhiteSpace(r.Title) ? r.Url : r.Title,
            Url = r.Url,
            Snippet = r.Content,
            Type = InferSourceType(r.Url, academic),
            // Tavily score is already 0..1 and directly comparable to our ReliabilityScore.
            // Fall back to a neutral 0.5 when score is missing (null or out-of-range).
            ReliabilityScore = r.Score is >= 0 and <= 1 ? r.Score.Value : 0.5,
        }).ToList();
    }

    private static SourceType InferSourceType(string url, bool academic)
    {
        if (string.IsNullOrWhiteSpace(url)) return SourceType.WebPage;
        var lower = url.ToLowerInvariant();
        if (academic) return SourceType.AcademicPaper;
        if (lower.Contains("arxiv.org") || lower.Contains("scholar.google") || lower.Contains("semanticscholar"))
            return SourceType.AcademicPaper;
        if (lower.Contains("/docs/") || lower.Contains("learn.microsoft") || lower.Contains("developer.mozilla"))
            return SourceType.Documentation;
        if (lower.Contains("news.") || lower.Contains("/news/"))
            return SourceType.NewsArticle;
        return SourceType.WebPage;
    }

    private static async Task<string> SafeReadAsync(HttpResponseMessage r, CancellationToken ct)
    {
        try { return await r.Content.ReadAsStringAsync(ct); } catch { return "(no body)"; }
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "…";

    // ── JSON DTOs ──────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed class TavilyRequest
    {
        [JsonPropertyName("query")] public string Query { get; set; } = "";
        [JsonPropertyName("search_depth")] public string SearchDepth { get; set; } = "basic";
        [JsonPropertyName("max_results")] public int MaxResults { get; set; }
        [JsonPropertyName("include_answer")] public bool IncludeAnswer { get; set; }
        [JsonPropertyName("include_raw_content")] public bool IncludeRawContent { get; set; }
        [JsonPropertyName("include_domains")] public string[]? IncludeDomains { get; set; }
    }

    private sealed class TavilyResponse
    {
        [JsonPropertyName("results")] public List<TavilyResult>? Results { get; set; }
    }

    private sealed class TavilyResult
    {
        [JsonPropertyName("title")] public string Title { get; set; } = "";
        [JsonPropertyName("url")] public string Url { get; set; } = "";
        [JsonPropertyName("content")] public string Content { get; set; } = "";
        [JsonPropertyName("score")] public double? Score { get; set; }
    }
}
