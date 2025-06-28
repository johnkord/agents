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
using System.Reflection;
using AgentAlpha.Interfaces;
using OpenAIIntegration;

namespace AgentAlpha.Tests
{
    /// <summary>
    /// Tests to reproduce and verify fixes for the ToolSelector bugs reported in issue #117
    /// </summary>
    public class ToolSelectorBugFixTests
    {
        [Fact]
        public void ExtractTextFromContent_WithProblemStatementResponse_ShouldParseCorrectly()
        {
            // This simulates the actual response structure from the problem statement
            var contentJson = @"[
                {
                    ""type"": ""output_text"",
                    ""annotations"": [],
                    ""logprobs"": [],
                    ""text"": ""[\""openai_list_vector_stores\"", \""openai_query_vector_store\"", \""openai_create_vector_store\""]""
                }
            ]";

            var contentElement = JsonSerializer.Deserialize<JsonElement>(contentJson);
            var extractedText = ExtractTextFromContentHelper(contentElement);
            
            Assert.Equal("[\"openai_list_vector_stores\", \"openai_query_vector_store\", \"openai_create_vector_store\"]", extractedText);
            
            // This should not throw an exception - this is the core of the bug
            var toolNames = JsonSerializer.Deserialize<string[]>(extractedText);
            Assert.NotNull(toolNames);
            Assert.Equal(3, toolNames.Length);
            Assert.Equal("openai_list_vector_stores", toolNames[0]);
            Assert.Equal("openai_query_vector_store", toolNames[1]);
            Assert.Equal("openai_create_vector_store", toolNames[2]);
        }

        [Fact]
        public void ExtractTextFromContent_WithInvalidJsonResponse_ShouldHandleGracefully()
        {
            // Test edge case where OpenAI returns malformed JSON in the text field
            var contentJson = @"[
                {
                    ""type"": ""output_text"",
                    ""annotations"": [],
                    ""text"": ""This is not valid JSON for tool selection""
                }
            ]";

            var contentElement = JsonSerializer.Deserialize<JsonElement>(contentJson);
            var extractedText = ExtractTextFromContentHelper(contentElement);
            
            Assert.Equal("This is not valid JSON for tool selection", extractedText);
            
            // This should throw a JsonException - we want to test our error handling
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<string[]>(extractedText));
        }

        [Fact]
        public void ExtractTextFromContent_WithEmptyResponse_ShouldReturnEmptyString()
        {
            // Test edge case where content is empty or has no output_text
            var contentJson = @"[]";

            var contentElement = JsonSerializer.Deserialize<JsonElement>(contentJson);
            var extractedText = ExtractTextFromContentHelper(contentElement);
            
            Assert.Equal("", extractedText);
        }

        [Fact]
        public void ExtractTextFromContent_WithMissingTextProperty_ShouldReturnEmptyString()
        {
            // Test edge case where output_text item has no text property
            var contentJson = @"[
                {
                    ""type"": ""output_text"",
                    ""annotations"": []
                }
            ]";

            var contentElement = JsonSerializer.Deserialize<JsonElement>(contentJson);
            var extractedText = ExtractTextFromContentHelper(contentElement);
            
            Assert.Equal("", extractedText);
        }

        [Theory]
        [InlineData("which models are available through openai", true)] // From problem statement
        [InlineData("what math operations can I do", true)] // Contains "what" - currently triggers web search
        [InlineData("list openai pricing options", true)]
        [InlineData("calculate 2 + 2", false)]
        [InlineData("find current openai api status", true)]
        [InlineData("add two numbers", false)]
        public void ShouldIncludeWebSearch_WithVariousTasks_ShouldReturnCorrectResult(string task, bool expectedResult)
        {
            // Test the web search detection logic which is mentioned in the problem
            var result = ShouldIncludeWebSearchHelper(task);
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public void BuiltInTools_ShouldBeIncludedInToolSelection()
        {
            // This test ensures that built-in OpenAI tools like web_search_preview 
            // are included in the tool selection process
            
            // Test the helper methods that were added to support built-in tools
            var toolSelector = CreateTestToolSelector();
            
            var builtInDescriptions = GetBuiltInToolDescriptionsHelper(
                "which models are available through openai", new List<ToolDefinition>());
            
            // Should include web search for this type of query
            Assert.Contains("- web_search: Search the web for current information and real-time data", builtInDescriptions);
            
            // Test getting tool definition for web_search
            var webSearchDef = GetBuiltInToolDefinitionHelper("web_search_preview");
            Assert.NotNull(webSearchDef);
            Assert.Equal("web_search_preview", webSearchDef.Name);
        }

        [Fact]
        public void JsonParsingError_ShouldProvideDetailedErrorInfo()
        {
            // This test validates that JSON parsing errors are handled properly
            // and provide detailed error information for debugging
            
            var contentJson = @"[
                {
                    ""type"": ""output_text"",
                    ""annotations"": [],
                    ""text"": ""This is invalid JSON that should cause parsing to fail""
                }
            ]";

            var contentElement = JsonSerializer.Deserialize<JsonElement>(contentJson);
            var extractedText = ExtractTextFromContentHelper(contentElement);
            
            // This should throw a JsonException when trying to parse as string array
            var exception = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<string[]>(extractedText));
            Assert.NotNull(exception);
            
            // The error should be descriptive about what went wrong
            Assert.Contains("invalid", exception.Message);
        }

        /// <summary>
        /// Helper method to test the private ExtractTextFromContent method via reflection
        /// </summary>
        private static string ExtractTextFromContentHelper(JsonElement? content)
        {
            var toolSelectorType = typeof(ToolSelector);
            var method = toolSelectorType.GetMethod("ExtractTextFromContent", 
                BindingFlags.NonPublic | BindingFlags.Static);
            
            Assert.NotNull(method);
            
            var result = method.Invoke(null, new object?[] { content });
            return result?.ToString() ?? "";
        }

        /// <summary>
        /// Helper method to test the private ShouldIncludeWebSearch method via reflection
        /// </summary>
        private static bool ShouldIncludeWebSearchHelper(string task)
        {
            // Create a minimal ToolSelector instance for testing
            var mockOpenAI = new MockSessionAwareOpenAIServiceForBugFix();
            var mockToolManager = new MockToolManagerForBugFix();
            var mockLogger = new MockLoggerForBugFix<ToolSelector>();
            var agentConfig = new AgentConfiguration();
            
            var toolSelector = new ToolSelector(mockOpenAI, mockToolManager, mockLogger, agentConfig);
            
            return toolSelector.ShouldIncludeWebSearch(task);
        }

        /// <summary>
        /// Helper method to create a ToolSelector for testing
        /// </summary>
        private static ToolSelector CreateTestToolSelector()
        {
            var mockOpenAI = new MockSessionAwareOpenAIServiceForBugFix();
            var mockToolManager = new MockToolManagerForBugFix();
            var mockLogger = new MockLoggerForBugFix<ToolSelector>();
            var agentConfig = new AgentConfiguration();
            
            return new ToolSelector(mockOpenAI, mockToolManager, mockLogger, agentConfig);
        }

        /// <summary>
        /// Helper method to test the private GetBuiltInOpenAIToolDescriptions method via reflection
        /// </summary>
        private static List<string> GetBuiltInToolDescriptionsHelper(string task, List<ToolDefinition> alreadySelected)
        {
            var toolSelector = CreateTestToolSelector();
            var toolSelectorType = typeof(ToolSelector);
            var method = toolSelectorType.GetMethod("GetBuiltInOpenAIToolDescriptions", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            Assert.NotNull(method);
            
            var result = method.Invoke(toolSelector, new object[] { task, alreadySelected });
            return (List<string>)result!;
        }

        /// <summary>
        /// Helper method to test the private GetBuiltInOpenAIToolDefinition method via reflection
        /// </summary>
        private static ToolDefinition? GetBuiltInToolDefinitionHelper(string toolName)
        {
            var toolSelector = CreateTestToolSelector();
            var toolSelectorType = typeof(ToolSelector);
            var method = toolSelectorType.GetMethod("GetBuiltInOpenAIToolDefinition", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            Assert.NotNull(method);
            
            var result = method.Invoke(toolSelector, new object[] { toolName });
            return (ToolDefinition?)result;
        }
    }

    // Mock classes for testing
    public class MockSessionAwareOpenAIServiceForBugFix : OpenAIIntegration.ISessionAwareOpenAIService
    {
        public void SetActivityLogger(Common.Interfaces.Session.ISessionActivityLogger? activityLogger) { }
        public Task<ResponsesCreateResponse> CreateResponseAsync(ResponsesCreateRequest request, System.Threading.CancellationToken cancellationToken = default) => 
            Task.FromResult(new ResponsesCreateResponse());
    }

    public class MockToolManagerForBugFix : AgentAlpha.Interfaces.IToolManager
    {
        public Task<IList<McpClientTool>> DiscoverToolsAsync(AgentAlpha.Interfaces.IConnectionManager connection) => 
            Task.FromResult<IList<McpClientTool>>(new List<McpClientTool>());
        
        public IList<McpClientTool> ApplyFilters(IList<McpClientTool> tools, ToolFilterConfig filter) => tools;
        
        public ToolDefinition CreateOpenAiToolDefinition(McpClientTool mcpTool) => 
            new ToolDefinition { Name = mcpTool.Name, Description = mcpTool.Description };
        
        public Task<string> ExecuteToolAsync(AgentAlpha.Interfaces.IConnectionManager connection, string toolName, Dictionary<string, object?> arguments) => 
            Task.FromResult("mock result");

        // New unified methods
        public Task<IList<IUnifiedTool>> DiscoverAllToolsAsync(AgentAlpha.Interfaces.IConnectionManager connection) =>
            Task.FromResult<IList<IUnifiedTool>>(new List<IUnifiedTool>());

        public IList<IUnifiedTool> ApplyFiltersToAllTools(IList<IUnifiedTool> tools, ToolFilterConfig filter) => tools;

        public Task<string> ExecuteUnifiedToolAsync(IUnifiedTool tool, AgentAlpha.Interfaces.IConnectionManager connection, Dictionary<string, object?> arguments) =>
            Task.FromResult("mock unified result");

        public ToolDefinition[] ConvertToToolDefinitions(IList<IUnifiedTool> tools) =>
            tools.Select(t => t.ToToolDefinition()).ToArray();
    }

    public class MockLoggerForBugFix<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}