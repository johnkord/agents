using Microsoft.Extensions.Logging;
using Xunit;
using AgentAlpha.Services;
using AgentAlpha.Models;
using AgentAlpha.Configuration;
using ModelContextProtocol.Client;
using OpenAIIntegration;
using System.Text.Json;

namespace AgentAlpha.Tests;

public class PlanningServiceTests
{
    private readonly ILogger<PlanningService> _logger;
    private readonly AgentConfiguration _config;

    public PlanningServiceTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<PlanningService>();
        _config = new AgentConfiguration { Model = "gpt-3.5-turbo" };
    }

    [Fact]
    public void TaskPlan_Creation_SetsDefaultValues()
    {
        // Arrange & Act
        var plan = new TaskPlan
        {
            Task = "Test task",
            Strategy = "Test strategy"
        };

        // Assert
        Assert.Equal("Test task", plan.Task);
        Assert.Equal("Test strategy", plan.Strategy);
        Assert.Empty(plan.Steps);
        Assert.Empty(plan.RequiredTools);
        Assert.Equal(TaskComplexity.Medium, plan.Complexity);
        Assert.Equal(0.5, plan.Confidence);
        Assert.True(plan.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void PlanStep_Creation_SetsCorrectDefaults()
    {
        // Arrange & Act
        var step = new PlanStep
        {
            StepNumber = 1,
            Description = "Test step"
        };

        // Assert
        Assert.Equal(1, step.StepNumber);
        Assert.Equal("Test step", step.Description);
        Assert.Empty(step.PotentialTools);
        Assert.True(step.IsMandatory);
        Assert.Null(step.ExpectedInput);
        Assert.Null(step.ExpectedOutput);
    }

    [Fact]
    public async Task ValidatePlanAsync_WithEmptyPlan_ReturnsInvalidResult()
    {
        // Arrange
        var mockOpenAI = new MockOpenAIService();
        var planningService = new PlanningService(mockOpenAI, _logger, _config);
        
        var plan = new TaskPlan
        {
            Task = "Test task",
            Strategy = "", // Empty strategy
            Steps = new List<PlanStep>() // No steps
        };

        var availableTools = new List<McpClientTool>();

        // Act
        var result = await planningService.ValidatePlanAsync(plan, availableTools);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Contains("no execution steps"));
        Assert.Contains(result.Issues, issue => issue.Contains("lacks a clear strategy"));
    }

    [Fact]
    public void TaskComplexity_EnumValues_AreCorrect()
    {
        // Arrange & Act & Assert
        Assert.Equal(0, (int)TaskComplexity.Simple);
        Assert.Equal(1, (int)TaskComplexity.Medium);
        Assert.Equal(2, (int)TaskComplexity.Complex);
        Assert.Equal(3, (int)TaskComplexity.VeryComplex);
    }

    [Fact]
    public void PlanValidationResult_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var result = new AgentAlpha.Interfaces.PlanValidationResult();

        // Assert
        Assert.False(result.IsValid);
        Assert.Empty(result.Issues);
        Assert.Empty(result.MissingTools);
        Assert.Equal(1.0, result.Confidence);
        Assert.Empty(result.Suggestions);
    }

    // Mock OpenAI service for testing
    private class MockOpenAIService : IOpenAIResponsesService
    {
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
}