using System.Text.Json.Serialization;

namespace OpenAIIntegration.Model;

public sealed class ToolDefinition
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /* Keep the parameters bag untyped so callers can pass an
       arbitrary JSON-serialisable object graph. */
    [JsonPropertyName("parameters")]
    public object? Parameters { get; set; }

    [JsonPropertyName("strict")]
    public bool? Strict { get; set; }
}
