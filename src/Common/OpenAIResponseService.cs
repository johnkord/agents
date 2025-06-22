using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenAIIntegration.Model;               // NEW

namespace OpenAIIntegration;

public interface IOpenAIResponseService
{
    /* Sends any request object to the /responses endpoint and
       returns the created response ID. */
    Task<ResponseCreateResponse> CreateResponseAsync(   // NEW
        ResponseCreateRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Minimal HTTP implementation for OpenAI “Responses” API calls.</summary>
public sealed class OpenAIResponseService : IOpenAIResponseService, IDisposable
{
    private readonly HttpClient _http = new();
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public OpenAIResponseService(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("OpenAI API key is required", nameof(apiKey));

        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<ResponseCreateResponse> CreateResponseAsync( // UPDATED
        ResponseCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var httpResponse = await _http.PostAsync(
            "https://api.openai.com/v1/responses",
            new StringContent(JsonSerializer.Serialize(request, _json),
                              Encoding.UTF8,
                              "application/json"),
            cancellationToken);

        var json = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!httpResponse.IsSuccessStatusCode)
            throw new InvalidOperationException($"OpenAI API error: {httpResponse.StatusCode} - {json}");

        return JsonSerializer.Deserialize<ResponseCreateResponse>(json, _json)
               ?? throw new InvalidOperationException("Invalid response payload");
    }

    public void Dispose() => _http.Dispose();
}
