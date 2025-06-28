using System.Text.Json.Serialization;
using OpenAIIntegration.Model;

namespace AgentAlpha.Models;

/// <summary>
/// Represents the OpenAI builtin web search tool configuration
/// </summary>
public class WebSearchTool
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "web_search_preview";

    [JsonPropertyName("user_location")]
    public WebSearchUserLocation? UserLocation { get; set; }

    [JsonPropertyName("search_context_size")]
    public string? SearchContextSize { get; set; } = "medium"; // default to OpenAI-recommended size

    /// <summary>
    /// Converts this web search tool to a ToolDefinition for use in OpenAI API requests
    /// According to OpenAI docs, built-in tools like web_search_preview should only have
    /// type and configuration parameters at the root level, not name/description/parameters like function tools
    /// </summary>
    public ToolDefinition ToToolDefinition()
    {
        var toolDef = new ToolDefinition
        {
            Type = Type,
            // For built-in tools, set the configuration properties directly
            UserLocation = UserLocation,
            SearchContextSize = SearchContextSize
            // Name and Description are left null/empty so they won't be serialized due to JsonIgnore conditions
        };

        return toolDef;
    }
}

/// <summary>
/// User location configuration for web search results
/// </summary>
public class WebSearchUserLocation
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "approximate";

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("region")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Region { get; set; }

    [JsonPropertyName("timezone")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Timezone { get; set; }
}