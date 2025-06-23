using System;
using System.IO;                              // NEW
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
        var reqJson = JsonSerializer.Serialize(request, _json);                 // ← capture
        var content = new StringContent(reqJson, Encoding.UTF8, "application/json");

        using var httpResponse = await _http.PostAsync(
            "https://api.openai.com/v1/responses",
            content,
            cancellationToken);

        var respJson = await httpResponse.Content.ReadAsStringAsync(cancellationToken); // ← capture

        LogOpenAiInteraction(reqJson, respJson);                                // NEW

        if (!httpResponse.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"OpenAI API error: {httpResponse.StatusCode} – {respJson}");

        return JsonSerializer.Deserialize<ResponsesCreateResponse>(respJson, _json)
               ?? throw new InvalidOperationException("Invalid response payload");
    }

    /* ---------- helper -------------------------------------------------- */
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
