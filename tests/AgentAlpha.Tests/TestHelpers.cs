using AgentAlpha.Configuration;
using AgentAlpha.Interfaces;
using AgentAlpha.Models;
using ModelContextProtocol.Client;
using OpenAIIntegration.Model;

namespace AgentAlpha.Tests;

/// <summary>
/// Helper methods for tests
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// Convert a list of McpClientTool to IUnifiedTool for tests
    /// </summary>
    public static IList<IUnifiedTool> WrapTools(IList<McpClientTool> mcpTools)
    {
        var mockToolManager = new MockToolManager();
        return mcpTools.Select(tool => new McpUnifiedTool(tool, mockToolManager) as IUnifiedTool).ToList();
    }

    /// <summary>
    /// Mock implementation of IToolManager for tests
    /// </summary>
    private class MockToolManager : IToolManager
    {
        public Task<IList<McpClientTool>> DiscoverToolsAsync(IConnectionManager connection) => throw new NotImplementedException();
        public IList<McpClientTool> ApplyFilters(IList<McpClientTool> tools, ToolFilterConfig filter) => throw new NotImplementedException();
        public ToolDefinition CreateOpenAiToolDefinition(McpClientTool mcpTool) => new ToolDefinition { Name = mcpTool.Name, Description = mcpTool.Description };
        public Task<string> ExecuteToolAsync(IConnectionManager connection, string toolName, Dictionary<string, object?> arguments) => throw new NotImplementedException();
        public Task<IList<IUnifiedTool>> DiscoverAllToolsAsync(IConnectionManager connection) => throw new NotImplementedException();
        public IList<IUnifiedTool> ApplyFiltersToAllTools(IList<IUnifiedTool> tools, ToolFilterConfig filter) => throw new NotImplementedException();
        public Task<string> ExecuteUnifiedToolAsync(IUnifiedTool tool, IConnectionManager connection, Dictionary<string, object?> arguments) => throw new NotImplementedException();
        public ToolDefinition[] ConvertToToolDefinitions(IList<IUnifiedTool> tools) => throw new NotImplementedException();
    }
}