using System.Collections.Generic;
using System.Text.Json.Serialization;
using OpenAIIntegration.Model;   // ← for ToolDefinition

namespace OpenAIIntegration.Model;

public sealed class ResponsesCreateRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "gpt-4o";

    /* union: string | array<any> */
    [JsonPropertyName("input")]
    public object Input { get; set; } = "";

    // --- new fields ---
    [JsonPropertyName("background")]
    public bool? Background { get; set; }

    [JsonPropertyName("include")]
    public string[]? Include { get; set; }

    [JsonPropertyName("instructions")]
    public string? Instructions { get; set; }

    [JsonPropertyName("max_output_tokens")]
    public int? MaxOutputTokens { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }

    [JsonPropertyName("parallel_tool_calls")]
    public bool? ParallelToolCalls { get; set; }

    [JsonPropertyName("previous_response_id")]
    public string? PreviousResponseId { get; set; }

    [JsonPropertyName("prompt")]
    public object? Prompt { get; set; }

    [JsonPropertyName("reasoning")]
    public object? Reasoning { get; set; }

    [JsonPropertyName("service_tier")]
    public string? ServiceTier { get; set; }

    [JsonPropertyName("store")]
    public bool? Store { get; set; }

    [JsonPropertyName("stream")]
    public bool? Stream { get; set; }

    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    [JsonPropertyName("text")]
    public object? Text { get; set; }

    [JsonPropertyName("tool_choice")]
    public object? ToolChoice { get; set; }   // replaces string? version

    [JsonPropertyName("tools")]
    public ToolDefinition[]? Tools { get; set; }

    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }

    [JsonPropertyName("truncation")]
    public object? Truncation { get; set; }

    [JsonPropertyName("user")]
    public string? User { get; set; }
}
