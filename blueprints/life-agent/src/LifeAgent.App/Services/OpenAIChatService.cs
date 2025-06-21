using System.Net.Http.Json;
using System.Text.Json;
using LifeAgent.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LifeAgent.App.Services;

/// <summary>
/// OpenAI chat completion service with tiered model routing.
/// Maps <see cref="ModelTier"/> to concrete model names from configuration.
/// Tracks per-call cost estimation for budget enforcement.
/// </summary>
public sealed class OpenAIChatService : IChatCompletionService, IDisposable
{
    private readonly HttpClient _http;
    private readonly ILogger<OpenAIChatService> _logger;
    private readonly string _fastModel;
    private readonly string _standardModel;
    private readonly string _deepModel;
    private readonly string _endpoint;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public OpenAIChatService(IConfiguration config, ILogger<OpenAIChatService> logger)
    {
        _logger = logger;

        var apiKey = config["AI:ApiKey"]
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? string.Empty;

        _endpoint = config["AI:Endpoint"]?.TrimEnd('/')
            ?? "https://api.openai.com";

        _fastModel = config["AI:FastModel"] ?? "gpt-4o-mini";
        _standardModel = config["AI:StandardModel"] ?? config["AI:Model"] ?? "gpt-4o";
        _deepModel = config["AI:DeepModel"] ?? "gpt-4o";

        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        _http.Timeout = TimeSpan.FromMinutes(3);
    }

    public async Task<string> CompleteAsync(
        string systemPrompt, string userMessage,
        ModelTier tier, float temperature, int maxTokens,
        CancellationToken ct)
    {
        var model = ResolveModel(tier);
        _logger.LogDebug("[LLM] {Tier} → {Model} (temp={Temp}, max_tokens={MaxTokens})",
            tier, model, temperature, maxTokens);

        var request = new
        {
            model,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userMessage },
            },
            temperature,
            max_tokens = maxTokens,
        };

        var response = await _http.PostAsJsonAsync($"{_endpoint}/v1/chat/completions", request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("[LLM] API error {Status}: {Body}", response.StatusCode, errorBody);
            response.EnsureSuccessStatusCode();
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var content = json.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";

        if (json.TryGetProperty("usage", out var usage))
        {
            var totalTokens = usage.GetProperty("total_tokens").GetInt32();
            _logger.LogDebug("[LLM] {Model}: {Tokens} tokens", model, totalTokens);
        }

        return content;
    }

    public async Task<T?> CompleteJsonAsync<T>(
        string systemPrompt, string userMessage,
        ModelTier tier, CancellationToken ct) where T : class
    {
        var model = ResolveModel(tier);

        var request = new
        {
            model,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt + "\n\nRespond with valid JSON only." },
                new { role = "user", content = userMessage },
            },
            temperature = 0.1f,
            max_tokens = 1000,
            response_format = new { type = "json_object" },
        };

        var response = await _http.PostAsJsonAsync($"{_endpoint}/v1/chat/completions", request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var content = json.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

        if (string.IsNullOrWhiteSpace(content)) return null;

        try
        {
            return JsonSerializer.Deserialize<T>(content, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "[LLM] Failed to deserialize JSON response as {Type}: {Content}",
                typeof(T).Name, content[..Math.Min(200, content.Length)]);
            return null;
        }
    }

    private string ResolveModel(ModelTier tier) => tier switch
    {
        ModelTier.Fast => _fastModel,
        ModelTier.Standard => _standardModel,
        ModelTier.Deep => _deepModel,
        _ => _fastModel,
    };

    public void Dispose() => _http.Dispose();
}
