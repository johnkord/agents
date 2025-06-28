using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;
using AgentAlpha.Services;
using AgentAlpha.Models;
using AgentAlpha.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using OpenAIIntegration.Model;
using AgentAlpha.Interfaces;
using OpenAIIntegration;

namespace AgentAlpha.Tests
{
    /// <summary>
    /// Demonstration test showing the specific issue from #117 is now fixed
    /// </summary>
    public class Issue117DemonstrationTests
    {
        [Fact]
        public void Issue117_JsonParsingFromProblemStatement_ShouldWorkNow()
        {
            // This is the exact response structure from the problem statement that was causing the error
            var problemStatementResponse = @"[
                {
                    ""type"": ""output_text"",
                    ""annotations"": [],
                    ""logprobs"": [],
                    ""text"": ""[\""openai_list_vector_stores\"", \""openai_query_vector_store\"", \""openai_create_vector_store\""]""
                }
            ]";

            var contentElement = JsonSerializer.Deserialize<JsonElement>(problemStatementResponse);
            
            // Extract text using the same method as ToolSelector
            var extractedText = ExtractTextFromContentViaReflection(contentElement);
            
            Assert.Equal("[\"openai_list_vector_stores\", \"openai_query_vector_store\", \"openai_create_vector_store\"]", extractedText);
            
            // This should NOT throw an exception anymore - this was the core bug
            var selectedTools = JsonSerializer.Deserialize<string[]>(extractedText);
            
            Assert.NotNull(selectedTools);
            Assert.Equal(3, selectedTools.Length);
            Assert.Equal("openai_list_vector_stores", selectedTools[0]);
            Assert.Equal("openai_query_vector_store", selectedTools[1]);
            Assert.Equal("openai_create_vector_store", selectedTools[2]);
        }

        [Fact]
        public void Issue117_BuiltInToolsIncluded_InLLMPrompt()
        {
            // This demonstrates that built-in tools are now included in the LLM prompt
            var task = "which models are available through openai"; // From problem statement
            var toolSelector = CreateTestToolSelector();
            
            // Test the method that gets built-in tool descriptions
            var builtInDescriptions = GetBuiltInToolDescriptionsViaReflection(toolSelector, task, new List<ToolDefinition>());
            
            // Should include web search for this type of query about current information
            Assert.Contains("- web_search: Search the web for current information and real-time data", builtInDescriptions);
            
            // Verify we can get the tool definition
            var webSearchDef = GetBuiltInToolDefinitionViaReflection(toolSelector, "web_search");
            Assert.NotNull(webSearchDef);
            Assert.Equal("web_search", webSearchDef.Name);
        }

        [Fact]
        public void Issue117_WebSearchDetection_WorksForProblemStatementTask()
        {
            // The specific task from the problem statement should trigger web search
            var task = "which models are available through openai?";
            var toolSelector = CreateTestToolSelector();
            
            var shouldIncludeWebSearch = toolSelector.ShouldIncludeWebSearch(task);
            
            // This should return true because "which", "available", and "models" are keywords that suggest need for current info
            Assert.True(shouldIncludeWebSearch);
        }

        private static ToolSelector CreateTestToolSelector()
        {
            var mockOpenAI = new MockSessionAwareOpenAIService();
            var mockToolManager = new MockToolManager();
            var mockLogger = new MockLogger<ToolSelector>();
            var agentConfig = new AgentConfiguration();
            
            return new ToolSelector(mockOpenAI, mockToolManager, mockLogger, agentConfig);
        }

        private static string ExtractTextFromContentViaReflection(JsonElement? content)
        {
            var toolSelectorType = typeof(ToolSelector);
            var method = toolSelectorType.GetMethod("ExtractTextFromContent", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            Assert.NotNull(method);
            
            var result = method.Invoke(null, new object?[] { content });
            return result?.ToString() ?? "";
        }

        private static List<string> GetBuiltInToolDescriptionsViaReflection(ToolSelector toolSelector, string task, List<ToolDefinition> alreadySelected)
        {
            var toolSelectorType = typeof(ToolSelector);
            var method = toolSelectorType.GetMethod("GetBuiltInOpenAIToolDescriptions", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            Assert.NotNull(method);
            
            var result = method.Invoke(toolSelector, new object[] { task, alreadySelected });
            return (List<string>)result!;
        }

        private static ToolDefinition? GetBuiltInToolDefinitionViaReflection(ToolSelector toolSelector, string toolName)
        {
            var toolSelectorType = typeof(ToolSelector);
            var method = toolSelectorType.GetMethod("GetBuiltInOpenAIToolDefinition", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            Assert.NotNull(method);
            
            var result = method.Invoke(toolSelector, new object[] { toolName });
            return (ToolDefinition?)result;
        }

        // Mock classes for testing
        public class MockSessionAwareOpenAIService : ISessionAwareOpenAIService
        {
            public void SetActivityLogger(Common.Interfaces.Session.ISessionActivityLogger? activityLogger) { }
            public Task<ResponsesCreateResponse> CreateResponseAsync(ResponsesCreateRequest request, System.Threading.CancellationToken cancellationToken = default) => 
                Task.FromResult(new ResponsesCreateResponse());
        }

        public class MockToolManager : IToolManager
        {
            public Task<IList<McpClientTool>> DiscoverToolsAsync(IConnectionManager connection) => 
                Task.FromResult<IList<McpClientTool>>(new List<McpClientTool>());
            
            public IList<McpClientTool> ApplyFilters(IList<McpClientTool> tools, ToolFilterConfig filter) => tools;
            
            public ToolDefinition CreateOpenAiToolDefinition(McpClientTool mcpTool) => 
                new ToolDefinition { Name = mcpTool.Name, Description = mcpTool.Description };
            
            public Task<string> ExecuteToolAsync(IConnectionManager connection, string toolName, Dictionary<string, object?> arguments) => 
                Task.FromResult("mock result");

            // New unified methods
            public Task<IList<IUnifiedTool>> DiscoverAllToolsAsync(IConnectionManager connection) =>
                Task.FromResult<IList<IUnifiedTool>>(new List<IUnifiedTool>());

            public IList<IUnifiedTool> ApplyFiltersToAllTools(IList<IUnifiedTool> tools, ToolFilterConfig filter) => tools;

            public Task<string> ExecuteUnifiedToolAsync(IUnifiedTool tool, IConnectionManager connection, Dictionary<string, object?> arguments) =>
                Task.FromResult("mock unified result");

            public ToolDefinition[] ConvertToToolDefinitions(IList<IUnifiedTool> tools) =>
                tools.Select(t => t.ToToolDefinition()).ToArray();
        }

        public class MockLogger<T> : ILogger<T>
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => false;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
        }
    }
}