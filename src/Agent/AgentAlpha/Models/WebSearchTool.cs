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
    /// </summary>
    public ToolDefinition ToToolDefinition()
    {
        var toolDef = new ToolDefinition
        {
            Type = Type,
            Name = "web_search_preview",
            Description = "Search the web for current information and real-time data"
        };

        // Build the tool parameters based on configuration
        var parameters = new Dictionary<string, object>();
        
        if (UserLocation != null)
        {
            parameters["user_location"] = UserLocation;
        }
        
        if (!string.IsNullOrEmpty(SearchContextSize))
        {
            parameters["search_context_size"] = SearchContextSize;
        }

        if (parameters.Count > 0)
        {
            toolDef.Parameters = parameters;
        }

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
    public string? Region { get; set; }

    [JsonPropertyName("timezone")]
    public string? Timezone { get; set; }
}