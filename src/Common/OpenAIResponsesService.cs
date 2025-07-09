using System;
using System.IO;                              // NEW
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;                 // NEW
using OpenAIIntegration.Model;               // NEW

namespace OpenAIIntegration;

public interface IOpenAIResponsesService
{
    /* Sends any request object to the /responses endpoint and
       returns the created response ID. */
    Task<ResponsesCreateResponse> CreateResponseAsync(   // NEW
        ResponsesCreateRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Minimal HTTP implementation for OpenAI “Responses” API calls.</summary>
public sealed class OpenAIResponsesService : IOpenAIResponsesService, IDisposable
{
    private const int DefaultMaxRetries = 100;        // NEW
    private readonly HttpClient _http = new();
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public OpenAIResponsesService(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("OpenAI API key is required", nameof(apiKey));

        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);

        // NEW – required for the preview “Responses” API
        _http.DefaultRequestHeaders.Add("OpenAI-Beta", "responses=2024-05-17");
    }

    public async Task<ResponsesCreateResponse> CreateResponseAsync(
        ResponsesCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var reqJson = JsonSerializer.Serialize(request, _json);

        for (var attempt = 1; attempt <= DefaultMaxRetries; attempt++)   // NEW retry loop
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var content = new StringContent(reqJson, Encoding.UTF8, "application/json");

            using var httpResponse = await _http.PostAsync(
                "https://api.openai.com/v1/responses",
                content,
                cancellationToken);

            var respJson = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            LogOpenAiInteraction(reqJson, respJson);

            if (httpResponse.IsSuccessStatusCode)
            {
                return JsonSerializer.Deserialize<ResponsesCreateResponse>(respJson, _json)
                       ?? throw new InvalidOperationException("Invalid response payload");
            }

            // Retry on rate-limit errors (HTTP 429)
            if (httpResponse.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                && attempt < DefaultMaxRetries)
            {
                var delay = GetRetryDelay(httpResponse, respJson, attempt);
                await Task.Delay(delay, cancellationToken);
                continue;   // try again
            }

            throw new InvalidOperationException(
                $"OpenAI API error: {httpResponse.StatusCode} – {respJson}");
        }

        throw new InvalidOperationException("Exceeded maximum retry attempts");
    }

    /* ---------- helper -------------------------------------------------- */
    private static TimeSpan GetRetryDelay(HttpResponseMessage response, string bodyJson, int attempt)
    {
        // 1) Honor Retry-After header
        if (response.Headers.RetryAfter?.Delta is { } delta)
            return delta;

        // 2) Parse “…try again in Xs” from body, if present
        try
        {
            var m = Regex.Match(bodyJson, @"try again in\s+([0-9.]+)s", RegexOptions.IgnoreCase);
            if (m.Success && double.TryParse(m.Groups[1].Value, out var secs))
                return TimeSpan.FromSeconds(secs);
        }
        catch { /* ignore parsing issues */ }

        // 3) Exponential back-off (capped at 30 s)
        var backoff = Math.Min(Math.Pow(2, attempt - 1), 30);
        return TimeSpan.FromSeconds(backoff);
    }

    /* ---------- logging -------------------------------------------------- */
    private static void LogOpenAiInteraction(string requestJson, string responseJson)
    {
        try
        {
            Directory.CreateDirectory("logs");
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff");
            var path  = Path.Combine("logs", $"openai_{stamp}.json");
            File.WriteAllText(
                path,
                JsonSerializer.Serialize(new
                {
                    timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff"),
                    request   = JsonSerializer.Deserialize<object>(requestJson),
                    response  = JsonSerializer.Deserialize<object>(responseJson)
                }));
        }
        catch { /* swallow – logging must never break main flow */ }
    }

    /* ------------------------------------------------------------------ */
    public void Dispose() => _http.Dispose();
}
