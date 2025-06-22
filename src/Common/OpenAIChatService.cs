using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OpenAIIntegration;

public interface IOpenAIChatService
{
    Task<(string content, ToolCall[]? toolCalls)> CreateChatCompletionAsync(
        object[] messages,
        object[] tools,
        CancellationToken cancellationToken = default);
}

/// <summary>Minimal HTTP implementation for OpenAI chat completions.</summary>
public sealed class OpenAIChatService : IOpenAIChatService, IDisposable
{
    private readonly HttpClient _http = new();
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public OpenAIChatService(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("OpenAI API key is required", nameof(apiKey));

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<(string content, ToolCall[]? toolCalls)> CreateChatCompletionAsync(
        object[] messages,
        object[] tools,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            model   = "gpt-3.5-turbo",
            messages,
            tools,
            tool_choice = "auto"
        };

        var response = await _http.PostAsync(
            "https://api.openai.com/v1/chat/completions",
            new StringContent(JsonSerializer.Serialize(payload, _json), Encoding.UTF8, "application/json"),
            cancellationToken);

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"OpenAI API error: {response.StatusCode} - {json}");

        var root  = JsonDocument.Parse(json).RootElement;
        var msg   = root.GetProperty("choices")[0].GetProperty("message");

        string content = msg.TryGetProperty("content", out var cProp) ? cProp.GetString() ?? "" : "";
        ToolCall[]? toolCalls = null;
        if (msg.TryGetProperty("tool_calls", out var tcProp))
            toolCalls = JsonSerializer.Deserialize<ToolCall[]>(tcProp.GetRawText(), _json);

        return (content, toolCalls);
    }

    public void Dispose() => _http.Dispose();
}

/* Shared DTOs so that other projects can reference them */
public sealed class ToolCall
{
    public string id { get; set; } = "";
    public string type { get; set; } = "";
    public FunctionCall function { get; set; } = new();
}

public sealed class FunctionCall
{
    public string name { get; set; } = "";
    public string arguments { get; set; } = "";
}
