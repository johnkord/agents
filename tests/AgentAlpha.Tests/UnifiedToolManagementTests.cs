using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using AgentAlpha.Services;
using AgentAlpha.Models;
using AgentAlpha.Configuration;
using AgentAlpha.Interfaces;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OpenAIIntegration.Model;
using MCPClient;

namespace AgentAlpha.Tests;

/// <summary>
/// Tests for the unified tool management system that handles both MCP and built-in OpenAI tools
/// </summary>
public class UnifiedToolManagementTests
{
    private class TestLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    private class MockConnectionManager : IConnectionManager
    {
        public bool IsConnected => true;
        public Task ConnectAsync(McpTransportType transport, string serverName, string? serverUrl = null, string? command = null, string[]? args = null) => Task.CompletedTask;
        public Task<IList<McpClientTool>> ListToolsAsync() => Task.FromResult<IList<McpClientTool>>(new List<McpClientTool>());
        public Task<CallToolResult> CallToolAsync(string toolName, Dictionary<string, object?> arguments) => throw new NotImplementedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public void BuiltInToolRegistry_RegistersWebSearchTool_WhenConfigured()
    {
        // Arrange
        var logger = new TestLogger<BuiltInToolRegistry>();
        var config = new AgentConfiguration
        {
            WebSearch = new WebSearchTool
            {
                UserLocation = new WebSearchUserLocation { Country = "US", City = "New York" },
                SearchContextSize = "medium"
            }
        };

        // Act
        var registry = new BuiltInToolRegistry(logger, config);

        // Assert
        Assert.Equal(1, registry.Count);
        Assert.True(registry.IsBuiltInTool("web_search_preview"));
        
        var webSearchTool = registry.GetBuiltInTool("web_search_preview");
        Assert.NotNull(webSearchTool);
        Assert.Equal("web_search_preview", webSearchTool.Name);
        Assert.Equal(ToolType.BuiltInOpenAI, webSearchTool.Type);
        Assert.Contains("Search the web", webSearchTool.Description);
    }

    [Fact]
    public void BuiltInToolRegistry_ReturnsEmptyList_WhenNoToolsConfigured()
    {
        // Arrange
        var logger = new TestLogger<BuiltInToolRegistry>();
        var config = new AgentConfiguration
        {
            WebSearch = null! // No web search configured
        };

        // Act
        var registry = new BuiltInToolRegistry(logger, config);

        // Assert
        Assert.Equal(0, registry.Count);
        Assert.False(registry.IsBuiltInTool("web_search_preview"));
        Assert.Null(registry.GetBuiltInTool("web_search_preview"));
    }

    [Fact]
    public async Task ToolManager_DiscoverAllToolsAsync_CombinesMcpAndBuiltInTools()
    {
        // Arrange
        var logger = new TestLogger<ToolManager>();
        var builderLogger = new TestLogger<BuiltInToolRegistry>();
        var connection = new MockConnectionManager();
        
        var config = new AgentConfiguration
        {
            WebSearch = new WebSearchTool(),
            ToolFilter = new ToolFilterConfig()
        };
        
        var registry = new BuiltInToolRegistry(builderLogger, config);
        var toolManager = new ToolManager(logger, config, registry);

        // Act
        var allTools = await toolManager.DiscoverAllToolsAsync(connection);

        // Assert
        Assert.Single(allTools); // Should have web search tool (no MCP tools in mock)
        
        var webSearchTool = allTools.FirstOrDefault(t => t.Name == "web_search_preview");
        Assert.NotNull(webSearchTool);
        Assert.Equal(ToolType.BuiltInOpenAI, webSearchTool.Type);
        Assert.True(webSearchTool.CanExecute());
    }

    [Fact]
    public async Task ToolManager_ApplyFiltersToAllTools_FiltersUnifiedTools()
    {
        // Arrange
        var logger = new TestLogger<ToolManager>();
        var builderLogger = new TestLogger<BuiltInToolRegistry>();
        var connection = new MockConnectionManager();
        
        var config = new AgentConfiguration
        {
            WebSearch = new WebSearchTool(),
            ToolFilter = new ToolFilterConfig()
        };
        
        // Configure filter to exclude web search
        config.ToolFilter.Blacklist.Add("web_search_preview");
        
        var registry = new BuiltInToolRegistry(builderLogger, config);
        var toolManager = new ToolManager(logger, config, registry);

        // Act
        var allTools = await toolManager.DiscoverAllToolsAsync(connection);
        var filteredTools = toolManager.ApplyFiltersToAllTools(allTools, config.ToolFilter);

        // Assert
        Assert.Single(allTools); // Original tools
        Assert.Empty(filteredTools); // Filtered out web search
    }

    [Fact]
    public void ToolManager_ConvertToToolDefinitions_ConvertsUnifiedTools()
    {
        // Arrange
        var logger = new TestLogger<ToolManager>();
        var builderLogger = new TestLogger<BuiltInToolRegistry>();
        
        var config = new AgentConfiguration
        {
            WebSearch = new WebSearchTool(),
            ToolFilter = new ToolFilterConfig()
        };
        
        var registry = new BuiltInToolRegistry(builderLogger, config);
        var toolManager = new ToolManager(logger, config, registry);

        var unifiedTools = registry.GetAvailableBuiltInTools();

        // Act
        var toolDefinitions = toolManager.ConvertToToolDefinitions(unifiedTools);

        // Assert
        Assert.Single(toolDefinitions);
        
        var webSearchDef = toolDefinitions.First();
        Assert.Equal("web_search_preview", webSearchDef.Type); // Built-in OpenAI tool type
        Assert.Equal("web_search_preview", webSearchDef.Name);
    }

    [Fact]
    public async Task ToolManager_ExecuteUnifiedToolAsync_HandlesBuiltInTools()
    {
        // Arrange
        var logger = new TestLogger<ToolManager>();
        var builderLogger = new TestLogger<BuiltInToolRegistry>();
        var connection = new MockConnectionManager();
        
        var config = new AgentConfiguration
        {
            WebSearch = new WebSearchTool(),
            ToolFilter = new ToolFilterConfig()
        };
        
        var registry = new BuiltInToolRegistry(builderLogger, config);
        var toolManager = new ToolManager(logger, config, registry);
        
        var webSearchTool = registry.GetBuiltInTool("web_search_preview");
        Assert.NotNull(webSearchTool);

        // Act
        var result = await toolManager.ExecuteUnifiedToolAsync(webSearchTool, connection, new Dictionary<string, object?>());

        // Assert
        Assert.Contains("Built-in OpenAI tool execution is handled by the OpenAI API", result);
    }

    [Fact]
    public void WebSearchBuiltInTool_HasCorrectMetadata()
    {
        // Arrange
        var webSearchConfig = new WebSearchTool
        {
            UserLocation = new WebSearchUserLocation { Country = "US" },
            SearchContextSize = "high"
        };

        // Act
        var webSearchTool = new WebSearchBuiltInTool(webSearchConfig);
        var metadata = webSearchTool.GetMetadata();

        // Assert
        Assert.Equal("web_search_preview", webSearchTool.Name);
        Assert.Equal(ToolType.BuiltInOpenAI, webSearchTool.Type);
        Assert.Contains("Search the web", webSearchTool.Description);
        
        Assert.True(metadata.ContainsKey("webSearchConfig"));
        Assert.True(metadata.ContainsKey("supportedModels"));
        Assert.True(metadata.ContainsKey("requiresLocation"));
        Assert.Equal(webSearchConfig, metadata["webSearchConfig"]);
        Assert.True((bool)metadata["requiresLocation"]!);
    }

    [Fact]
    public void UnifiedTools_EqualityComparison_WorksCorrectly()
    {
        // Arrange
        var webSearchConfig = new WebSearchTool();
        var webSearchTool1 = new WebSearchBuiltInTool(webSearchConfig);
        var webSearchTool2 = new WebSearchBuiltInTool(webSearchConfig);

        // Act & Assert
        Assert.Equal(webSearchTool1, webSearchTool2);
        Assert.Equal(webSearchTool1.GetHashCode(), webSearchTool2.GetHashCode());
    }
}