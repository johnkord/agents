using System;
using System.Text.Json;
using AgentAlpha.Models;
using Xunit;

namespace AgentAlpha.Tests;

/// <summary>
/// Test to validate that the fixed web_search_preview tool serializes to the correct OpenAI format
/// </summary>
public class WebSearchSerializationFixTests
{
    [Fact]
    public void WebSearchTool_SerializesToCorrectOpenAIFormat()
    {
        // Arrange
        var webSearchTool = new WebSearchTool
        {
            Type = "web_search_preview",
            SearchContextSize = "medium",
            UserLocation = new WebSearchUserLocation
            {
                Type = "approximate",
                Country = "US",
                City = "New York",
                Region = "NY"
            }
        };

        // Act
        var toolDefinition = webSearchTool.ToToolDefinition();
        var json = JsonSerializer.Serialize(toolDefinition, new JsonSerializerOptions { WriteIndented = true });

        // Assert - The serialized output should match OpenAI's specification exactly
        var expectedJson = @"{
  ""type"": ""web_search_preview"",
  ""user_location"": {
    ""type"": ""approximate"",
    ""country"": ""US"",
    ""city"": ""New York"",
    ""region"": ""NY""
  },
  ""search_context_size"": ""medium""
}";

        // Normalize whitespace for comparison
        var normalizedActual = json.Replace("\r\n", "\n").Replace(" ", "").Replace("\n", "");
        var normalizedExpected = expectedJson.Replace("\r\n", "\n").Replace(" ", "").Replace("\n", "");

        Assert.Equal(normalizedExpected, normalizedActual);
        
        // Also verify the structure properties
        Assert.Equal("web_search_preview", toolDefinition.Type);
        Assert.True(string.IsNullOrEmpty(toolDefinition.Name));
        Assert.Null(toolDefinition.Description);
        Assert.NotNull(toolDefinition.UserLocation);
        Assert.Equal("medium", toolDefinition.SearchContextSize);
        Assert.Null(toolDefinition.Parameters);
    }

    [Fact]
    public void WebSearchTool_MinimalConfiguration_SerializesCorrectly()
    {
        // Arrange - Test minimal configuration (just type and default search context size)
        var webSearchTool = new WebSearchTool();

        // Act
        var toolDefinition = webSearchTool.ToToolDefinition();
        var json = JsonSerializer.Serialize(toolDefinition, new JsonSerializerOptions { WriteIndented = true });

        // Assert - Should only contain type and search_context_size (default "medium")
        var expectedJson = @"{
  ""type"": ""web_search_preview"",
  ""search_context_size"": ""medium""
}";

        var normalizedActual = json.Replace("\r\n", "\n").Replace(" ", "").Replace("\n", "");
        var normalizedExpected = expectedJson.Replace("\r\n", "\n").Replace(" ", "").Replace("\n", "");

        Assert.Equal(normalizedExpected, normalizedActual);
    }
}