using System;
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
        var content = new StringContent(
            JsonSerializer.Serialize(request, _json),
            Encoding.UTF8,
            "application/json");

        using var httpResponse = await _http.PostAsync(
            "https://api.openai.com/v1/responses",
            content,
            cancellationToken);

        var json = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"OpenAI API error: {httpResponse.StatusCode} – {json}");

        return JsonSerializer.Deserialize<ResponsesCreateResponse>(json, _json)
               ?? throw new InvalidOperationException("Invalid response payload");
    }

    /* ------------------------------------------------------------------ */
    public void Dispose() => _http.Dispose();
}
