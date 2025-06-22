using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OpenAIIntegration.Model;

public sealed class ResponseCreateRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "gpt-4o";

    /* Content items that make up the prompt.  
       Keep this generic for now – callers can shape as needed. */
    [JsonPropertyName("items")]
    public object[] Items { get; set; } = [];

    [JsonPropertyName("modalities")]
    public string[]? Modalities { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }

    // NEW ­– fields required by SimpleAgentAlpha
    [JsonPropertyName("messages")]
    public object[]? Messages { get; set; }

    [JsonPropertyName("tools")]
    public object[]? Tools { get; set; }

    [JsonPropertyName("tool_choice")]
    public string? ToolChoice { get; set; }
}
