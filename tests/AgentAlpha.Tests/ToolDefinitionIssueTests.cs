using Xunit;
using Microsoft.Extensions.Logging;
using AgentAlpha.Services;
using AgentAlpha.Configuration;
using AgentAlpha.Models;
using OpenAIIntegration;
using System.Text.Json;

namespace AgentAlpha.Tests;

/// <summary>
/// Tests to validate the tool definition issues are resolved:
/// 1. There should be 2 MCP tools enabled: run_command and complete_task
/// 2. There should be web_search_preview tool enabled (built-in OpenAI tool)
/// 3. Handle empty tool names correctly for built-in tools
/// </summary>
public class ToolDefinitionIssueTests
{
    [Fact]
    public void WebSearchTool_ToToolDefinition_HasCorrectFormat()
    {
        // Arrange
        var webSearchTool = new WebSearchTool
        {
            Type = "web_search_preview",
            SearchContextSize = "medium"
        };

        // Act
        var toolDefinition = webSearchTool.ToToolDefinition();

        // Assert
        Assert.Equal("web_search_preview", toolDefinition.Type);
        Assert.True(string.IsNullOrEmpty(toolDefinition.Name), "Built-in tools should have empty names");
        Assert.Null(toolDefinition.Description);
        Assert.Null(toolDefinition.Parameters);
        Assert.Equal("medium", toolDefinition.SearchContextSize);
    }

    [Fact]
    public void WebSearchTool_ToToolDefinition_WithUserLocation_SerializesCorrectly()
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

        // Assert
        Assert.Equal("web_search_preview", toolDefinition.Type);
        Assert.True(string.IsNullOrEmpty(toolDefinition.Name));
        Assert.NotNull(toolDefinition.UserLocation);
        Assert.Equal("medium", toolDefinition.SearchContextSize);
        
        // Verify JSON structure
        Assert.Contains("\"type\": \"web_search_preview\"", json);
        Assert.Contains("\"search_context_size\": \"medium\"", json);
        Assert.Contains("\"user_location\"", json);
        Assert.DoesNotContain("\"name\"", json); // Name should not be serialized for built-in tools
        Assert.DoesNotContain("\"description\"", json); // Description should not be serialized for built-in tools
    }

    [Fact]
    public void ToolDefinition_EmptyNameForBuiltInTools_IsCorrectBehavior()
    {
        // This test validates that empty names for built-in tools is the correct behavior
        // according to OpenAI specification for tools like web_search_preview

        // Arrange
        var webSearchTool = new WebSearchTool();
        var toolDefinition = webSearchTool.ToToolDefinition();

        // Act & Assert
        Assert.True(string.IsNullOrEmpty(toolDefinition.Name), 
            "Built-in OpenAI tools like web_search_preview should have empty names");
        Assert.Equal("web_search_preview", toolDefinition.Type);
        Assert.True(toolDefinition.IsBuiltInTool, "Should be identified as built-in tool");
    }

    [Fact]
    public void ToolDefinition_FunctionTools_ShouldHaveNames()
    {
        // Arrange & Act
        var functionTool = new OpenAIIntegration.Model.ToolDefinition
        {
            Type = "function",
            Name = "test_function",
            Description = "A test function"
        };

        // Assert
        Assert.False(string.IsNullOrEmpty(functionTool.Name), "Function tools should have names");
        Assert.Equal("function", functionTool.Type);
        Assert.False(functionTool.IsBuiltInTool, "Should NOT be identified as built-in tool");
    }

    [Fact]
    public void ToolLogging_HandlesEmptyNamesCorrectly()
    {
        // Test the logging format for mixed tool types
        var tools = new[]
        {
            new OpenAIIntegration.Model.ToolDefinition { Type = "function", Name = "complete_task" },
            new OpenAIIntegration.Model.ToolDefinition { Type = "function", Name = "run_command" },
            new OpenAIIntegration.Model.ToolDefinition { Type = "web_search_preview", Name = "" }
        };

        // Simulate the logging format used in ConversationManager
        var toolNames = tools.Select(t => 
            string.IsNullOrEmpty(t.Name) ? $"[{t.Type}]" : t.Name).ToArray();

        // Assert
        Assert.Equal(3, toolNames.Length);
        Assert.Contains("complete_task", toolNames);
        Assert.Contains("run_command", toolNames);
        Assert.Contains("[web_search_preview]", toolNames);
        Assert.DoesNotContain("", toolNames); // No empty strings
    }
}

/// <summary>
/// Mock implementation of ISessionAwareOpenAIService for testing
/// </summary>
internal class MockSessionAwareOpenAIService : ISessionAwareOpenAIService
{
    public void SetActivityLogger(Common.Interfaces.Session.ISessionActivityLogger? activityLogger) { }
    public Task<OpenAIIntegration.Model.ResponsesCreateResponse> CreateResponseAsync(OpenAIIntegration.Model.ResponsesCreateRequest request) => throw new NotImplementedException();
    public Task<OpenAIIntegration.Model.ResponsesCreateResponse> CreateResponseAsync(OpenAIIntegration.Model.ResponsesCreateRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
}