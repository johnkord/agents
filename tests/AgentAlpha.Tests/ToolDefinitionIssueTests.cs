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
    public void SimpleToolManager_ShouldIncludeWebSearch_DetectsKeywords()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<SimpleToolManager>();
        var config = new AgentConfiguration { WebSearch = new WebSearchTool() };
        var mockOpenAI = new MockSessionAwareOpenAIService();
        var toolManager = new SimpleToolManager(logger, config, mockOpenAI);

        // Test cases that should trigger web search
        var webSearchTasks = new[]
        {
            "What models are available through openai?",
            "Find current stock prices",
            "Search for recent AI developments", 
            "What's the latest news?",
            "Browse the web for information",
            "What are the supported features?",
            "List available options",
            "Which APIs are active?"
        };

        foreach (var task in webSearchTasks)
        {
            // Use reflection to access private method for testing
            var method = typeof(SimpleToolManager).GetMethod("ShouldIncludeWebSearch", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);

            // Act
            var result = (bool)method.Invoke(toolManager, new object[] { task })!;

            // Assert
            Assert.True(result, $"Task '{task}' should include web search tool");
        }
    }

    [Fact]
    public void SimpleToolManager_ShouldNotIncludeWebSearch_ForBasicTasks()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<SimpleToolManager>();
        var config = new AgentConfiguration { WebSearch = new WebSearchTool() };
        var mockOpenAI = new MockSessionAwareOpenAIService();
        var toolManager = new SimpleToolManager(logger, config, mockOpenAI);

        // Test cases that should NOT trigger web search
        var basicTasks = new[]
        {
            "Read this file",
            "Write some text",
            "Calculate the sum",
            "Process this data",
            "Create a simple script"
        };

        foreach (var task in basicTasks)
        {
            // Use reflection to access private method for testing
            var method = typeof(SimpleToolManager).GetMethod("ShouldIncludeWebSearch", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);

            // Act
            var result = (bool)method.Invoke(toolManager, new object[] { task })!;

            // Assert
            Assert.False(result, $"Task '{task}' should NOT include web search tool");
        }
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