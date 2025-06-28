using Xunit;
using AgentAlpha.Models;
using AgentAlpha.Services;
using AgentAlpha.Configuration;
using AgentAlpha.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using OpenAIIntegration;
using ModelContextProtocol.Client;

namespace AgentAlpha.Tests;

/// <summary>
/// Tests for the web search tool functionality
/// </summary>
public class WebSearchToolTests
{
    [Fact]
    public void WebSearchTool_ToToolDefinition_CreatesCorrectDefinition()
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

        // Assert
        Assert.Equal("web_search_preview", toolDefinition.Type);
        Assert.Equal("web_search_preview", toolDefinition.Name);
        Assert.Equal("Search the web for current information and real-time data", toolDefinition.Description);
        Assert.NotNull(toolDefinition.Parameters);
    }

    [Fact]
    public void WebSearchTool_ToToolDefinition_WithoutLocation_CreatesMinimalDefinition()
    {
        // Arrange
        var webSearchTool = new WebSearchTool();

        // Act
        var toolDefinition = webSearchTool.ToToolDefinition();

        // Assert
        Assert.Equal("web_search_preview", toolDefinition.Type);
        Assert.Equal("web_search_preview", toolDefinition.Name);
        Assert.Equal("Search the web for current information and real-time data", toolDefinition.Description);
    }

    [Theory]
    [InlineData("What's the latest news today?", true)]
    [InlineData("Search for current stock prices", true)]
    [InlineData("Find recent developments in AI", true)]
    [InlineData("What's happening online right now?", true)]
    [InlineData("Browse the web for information", true)]
    [InlineData("Google the latest trends", true)]
    [InlineData("which models are available through openai?", true)]  // Original problematic case
    [InlineData("what pricing options are available?", true)]
    [InlineData("list all supported versions", true)]
    [InlineData("which apis are active?", true)]
    [InlineData("what features does the service offer?", true)]
    [InlineData("Calculate 2 + 2", false)]
    [InlineData("Read the file contents", false)]
    [InlineData("List directory contents", false)]
    [InlineData("", false)]
    public void ToolSelector_ShouldIncludeWebSearch_CorrectlyIdentifiesWebSearchTasks(string task, bool expected)
    {
        // Arrange
        var mockOpenAI = new Mock<ISessionAwareOpenAIService>();
        var mockToolManager = new Mock<IToolManager>();
        var mockLogger = new Mock<ILogger<ToolSelector>>();
        var agentConfig = new AgentConfiguration();
        var toolSelectionConfig = new ToolSelectionConfig();

        var toolSelector = new ToolSelector(
            mockOpenAI.Object,
            mockToolManager.Object,
            mockLogger.Object,
            agentConfig,
            toolSelectionConfig);

        // Act
        var result = toolSelector.ShouldIncludeWebSearch(task);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task ToolSelector_SelectToolsForTaskAsync_IncludesWebSearchForRelevantTasks()
    {
        // Arrange
        var mockOpenAI = new Mock<ISessionAwareOpenAIService>();
        var mockToolManager = new Mock<IToolManager>();
        var mockLogger = new Mock<ILogger<ToolSelector>>();
        var agentConfig = new AgentConfiguration
        {
            WebSearch = new WebSearchTool()
        };
        var toolSelectionConfig = new ToolSelectionConfig
        {
            UseLLMSelection = false // Use heuristics only for predictable testing
        };

        var toolSelector = new ToolSelector(
            mockOpenAI.Object,
            mockToolManager.Object,
            mockLogger.Object,
            agentConfig,
            toolSelectionConfig);

        var availableTools = TestHelpers.WrapTools(new List<McpClientTool>());

        // Act
        var result = await toolSelector.SelectToolsForTaskAsync("What's the latest news today?", availableTools);

        // Assert
        Assert.Contains(result, t => t.Type == "web_search_preview" && t.Name == "web_search_preview");
    }

    [Fact]
    public async Task ToolSelector_SelectToolsForTaskAsync_DoesNotIncludeWebSearchForNonWebTasks()
    {
        // Arrange
        var mockOpenAI = new Mock<ISessionAwareOpenAIService>();
        var mockToolManager = new Mock<IToolManager>();
        var mockLogger = new Mock<ILogger<ToolSelector>>();
        var agentConfig = new AgentConfiguration
        {
            WebSearch = new WebSearchTool()
        };
        var toolSelectionConfig = new ToolSelectionConfig
        {
            UseLLMSelection = false // Use heuristics only for predictable testing
        };

        var toolSelector = new ToolSelector(
            mockOpenAI.Object,
            mockToolManager.Object,
            mockLogger.Object,
            agentConfig,
            toolSelectionConfig);

        var availableTools = TestHelpers.WrapTools(new List<McpClientTool>());

        // Act
        var result = await toolSelector.SelectToolsForTaskAsync("Calculate 2 + 2", availableTools);

        // Assert
        Assert.DoesNotContain(result, t => t.Type == "web_search_preview");
    }

    [Fact]
    public async Task ToolSelector_SelectToolsForTaskAsync_IncludesWebSearchWithLLMSelection()
    {
        // Arrange - Test the specific case from the issue with LLM selection enabled
        var mockOpenAI = new Mock<ISessionAwareOpenAIService>();
        var mockToolManager = new Mock<IToolManager>();
        var mockLogger = new Mock<ILogger<ToolSelector>>();
        var agentConfig = new AgentConfiguration
        {
            WebSearch = new WebSearchTool()
        };
        var toolSelectionConfig = new ToolSelectionConfig
        {
            UseLLMSelection = true // Enable LLM selection to test the fix
        };

        var toolSelector = new ToolSelector(
            mockOpenAI.Object,
            mockToolManager.Object,
            mockLogger.Object,
            agentConfig,
            toolSelectionConfig);

        var availableTools = TestHelpers.WrapTools(new List<McpClientTool>());

        // Act - Use the exact task from the issue
        var result = await toolSelector.SelectToolsForTaskAsync("which models are available through openai?", availableTools);

        // Assert - Web search should be included regardless of LLM selection
        Assert.Contains(result, t => t.Type == "web_search_preview" && t.Name == "web_search_preview");
    }

    [Fact]
    public async Task ToolSelector_SelectToolsForTaskAsync_IncludesWebSearchWithHeuristicSelection()
    {
        // Arrange - Test with heuristic selection to ensure it still works
        var mockOpenAI = new Mock<ISessionAwareOpenAIService>();
        var mockToolManager = new Mock<IToolManager>();
        var mockLogger = new Mock<ILogger<ToolSelector>>();
        var agentConfig = new AgentConfiguration
        {
            WebSearch = new WebSearchTool()
        };
        var toolSelectionConfig = new ToolSelectionConfig
        {
            UseLLMSelection = false // Use heuristics
        };

        var toolSelector = new ToolSelector(
            mockOpenAI.Object,
            mockToolManager.Object,
            mockLogger.Object,
            agentConfig,
            toolSelectionConfig);

        var availableTools = TestHelpers.WrapTools(new List<McpClientTool>());

        // Act - Use the exact task from the issue
        var result = await toolSelector.SelectToolsForTaskAsync("which models are available through openai?", availableTools);

        // Assert - Web search should be included with heuristic selection too
        Assert.Contains(result, t => t.Type == "web_search_preview" && t.Name == "web_search_preview");
    }

    [Fact]
    public async Task ToolSelector_SelectToolsForTaskAsync_IncludesWebSearchWithDefaultConfig()
    {
        // Arrange - Test with default configuration to ensure it works out of the box
        var mockOpenAI = new Mock<ISessionAwareOpenAIService>();
        var mockToolManager = new Mock<IToolManager>();
        var mockLogger = new Mock<ILogger<ToolSelector>>();
        var agentConfig = new AgentConfiguration(); // Default config includes WebSearch
        var toolSelectionConfig = new ToolSelectionConfig(); // Default config

        var toolSelector = new ToolSelector(
            mockOpenAI.Object,
            mockToolManager.Object,
            mockLogger.Object,
            agentConfig,
            toolSelectionConfig);

        var availableTools = TestHelpers.WrapTools(new List<McpClientTool>());

        // Act - Use the exact task from the issue
        var result = await toolSelector.SelectToolsForTaskAsync("which models are available through openai?", availableTools);

        // Assert - Web search should be included with default configuration
        Assert.Contains(result, t => t.Type == "web_search_preview" && t.Name == "web_search_preview");
    }
}