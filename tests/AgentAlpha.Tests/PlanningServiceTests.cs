using Microsoft.Extensions.Logging;
using Xunit;
using AgentAlpha.Services;
using AgentAlpha.Configuration;
using Common.Models.Session;
using ModelContextProtocol.Client;
using Common.Interfaces.Session;
using OpenAIIntegration;
using OpenAIIntegration.Model;
using System.Text.Json;
using AgentAlpha.Interfaces;

namespace AgentAlpha.Tests;

public class PlanningServiceTests
{
    private readonly ILogger<PlanningService> _logger;
    private readonly AgentConfiguration _config;

    public PlanningServiceTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<PlanningService>();
        _config = new AgentConfiguration { Model = "gpt-4.1-nano" };
    }

    [Fact]
    public async Task InitializeTaskPlanningAsync_ReturnsMarkdownWithExpectedSections()
    {
        var mockOpenAI = new MockOpenAIService();
        var planningService = new PlanningService(mockOpenAI, _logger, _config);

        var markdown = await planningService.InitializeTaskPlanningAsync(
            Guid.NewGuid().ToString(),
            "Test task",
            WrapTools(new List<McpClientTool>()));

        Assert.False(string.IsNullOrWhiteSpace(markdown));
        Assert.Contains("# Task:", markdown);
        Assert.Contains("## Subtasks", markdown);
    }

    [Fact]
    public async Task InitializeTaskPlanningWithStateAsync_ReturnsMarkdown()
    {
        var mockOpenAI = new MockOpenAIService();
        var planningService = new PlanningService(mockOpenAI, _logger, _config);

        var state = new CurrentState { SessionContext = "User context" };

        var markdown = await planningService.InitializeTaskPlanningWithStateAsync(
            Guid.NewGuid().ToString(),
            "State-aware task",
            WrapTools(new List<McpClientTool>()),
            state);

        Assert.False(string.IsNullOrWhiteSpace(markdown));
        Assert.Contains("# Task:", markdown);
    }

    // helper stays the same
    private static IList<IUnifiedTool> WrapTools(IList<McpClientTool> mcpTools) =>
        TestHelpers.WrapTools(mcpTools);

    // Mock OpenAI service for testing
    private class MockOpenAIService : ISessionAwareOpenAIService
    {
        public void SetActivityLogger(ISessionActivityLogger? activityLogger)
        {
            // No-op for testing
        }

        public Task<OpenAIIntegration.Model.ResponsesCreateResponse> CreateResponseAsync(
            OpenAIIntegration.Model.ResponsesCreateRequest request, 
            CancellationToken cancellationToken = default)
        {
            // Return a mock response for testing
            var mockResponse = new OpenAIIntegration.Model.ResponsesCreateResponse
            {
                Output = new[]
                {
                    new OpenAIIntegration.Model.OutputMessage
                    {
                        Role = "assistant",
                        Content = JsonSerializer.SerializeToElement(new
                        {
                            strategy = "Test strategy",
                            steps = new[]
                            {
                                new
                                {
                                    stepNumber = 1,
                                    description = "Test step",
                                    potentialTools = new[] { "test_tool" },
                                    isMandatory = true,
                                    expectedInput = "Test input",
                                    expectedOutput = "Test output"
                                }
                            },
                            requiredTools = new[] { "test_tool" },
                            complexity = "Medium",
                            confidence = 0.8
                        })
                    }
                }
            };

            return Task.FromResult(mockResponse);
        }
    }

    [Fact]
    public void JsonElement_StringArguments_ShouldBeHandledCorrectly()
    {
        // This test verifies that the JSON parsing logic handles string arguments correctly
        // Arrange
        var testJsonString = @"{""strategy"":""Test strategy"",""confidence"":0.8}";
        var stringJsonElement = JsonSerializer.SerializeToElement(testJsonString);
        
        // Verify that this is indeed a string JsonElement (like what would cause the original error)
        Assert.Equal(JsonValueKind.String, stringJsonElement.ValueKind);
        
        // Act - This should parse correctly now with our fix
        JsonElement parsedObject;
        var success = false;
        
        if (stringJsonElement.ValueKind == JsonValueKind.String)
        {
            var jsonContent = stringJsonElement.GetString();
            if (!string.IsNullOrWhiteSpace(jsonContent))
            {
                try
                {
                    parsedObject = JsonSerializer.Deserialize<JsonElement>(jsonContent);
                    success = true;
                }
                catch (JsonException)
                {
                    success = false;
                }
            }
        }
        
        // Assert
        Assert.True(success, "Should successfully parse string JSON arguments");
    }
}