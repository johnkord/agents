using Microsoft.Extensions.Logging;
using Xunit;
using AgentAlpha.Services;
using AgentAlpha.Models;
using AgentAlpha.Configuration;
using ModelContextProtocol.Client;
using OpenAIIntegration;
using Common.Models.Session;
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

        var availableTools = new List<McpClientTool>(); // no concrete instances needed

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

    [Fact]
    public void CurrentState_Creation_SetsDefaultValues()
    {
        // Arrange & Act
        var state = new CurrentState();

        // Assert
        Assert.Null(state.SessionContext);
        Assert.Empty(state.PreviousResults);
        Assert.Empty(state.AvailableResources);
        Assert.Null(state.UserPreferences);
        Assert.Null(state.Environment);
        Assert.True(state.CapturedAt <= DateTime.UtcNow);
        Assert.Empty(state.AdditionalContext);
    }

    [Fact]
    public void ExecutionResult_Creation_SetsDefaultValues()
    {
        // Arrange & Act
        var result = new ExecutionResult
        {
            Task = "Test task",
            Success = true,
            Summary = "Test summary"
        };

        // Assert
        Assert.Equal("Test task", result.Task);
        Assert.True(result.Success);
        Assert.Equal("Test summary", result.Summary);
        Assert.Empty(result.ToolsUsed);
        Assert.True(result.CompletedAt <= DateTime.UtcNow);
        Assert.Null(result.Insights);
    }

    [Fact]
    public void UserPreferences_Creation_SetsDefaultValues()
    {
        // Arrange & Act
        var preferences = new UserPreferences();

        // Assert
        Assert.Null(preferences.PreferredApproach);
        Assert.Empty(preferences.PreferredTools);
        Assert.Empty(preferences.AvoidedTools);
        Assert.Null(preferences.MaxExecutionTime);
        Assert.Equal(0.5, preferences.RiskTolerance);
    }

    [Fact]
    public async Task CreatePlanWithStateAnalysisAsync_WithCompleteState_CreatesEnhancedPlan()
    {
        // Arrange
        var mockOpenAI = new MockOpenAIService();
        var planningService = new PlanningService(mockOpenAI, _logger, _config);
        
        var currentState = new CurrentState
        {
            SessionContext = "User working on data analysis project",
            PreviousResults = new List<ExecutionResult>
            {
                new ExecutionResult
                {
                    Task = "Load data file",
                    Success = true,
                    Summary = "Successfully loaded CSV file with 1000 rows",
                    ToolsUsed = new List<string> { "file_reader" },
                    Insights = "Data quality is good"
                }
            },
            UserPreferences = new UserPreferences
            {
                PreferredApproach = "thorough",
                RiskTolerance = 0.3,
                PreferredTools = new List<string> { "pandas", "numpy" }
            },
            Environment = new EnvironmentCapabilities
            {
                ComputeResources = "8GB RAM, 4 CPU cores",
                NetworkStatus = "Connected",
                SecurityConstraints = new List<string> { "no_external_apis" }
            },
            AvailableResources = new Dictionary<string, string>
            {
                ["DataFile"] = "customer_data.csv",
                ["OutputFormat"] = "JSON"
            }
        };

        var availableTools = new List<McpClientTool>(); // no concrete instances needed

        // Act
        var plan = await planningService.CreatePlanWithStateAnalysisAsync(
            "Analyze customer data and generate insights", 
            availableTools, 
            currentState
        );

        // Assert
        Assert.NotNull(plan);
        Assert.Equal("Analyze customer data and generate insights", plan.Task);
        Assert.NotEmpty(plan.Strategy);
        Assert.NotEmpty(plan.Steps);
        Assert.NotNull(plan.AdditionalContext);
        Assert.True(plan.AdditionalContext.ContainsKey("StateAnalysisTimestamp"));
        Assert.True(plan.AdditionalContext.ContainsKey("UserRiskTolerance"));
        Assert.Equal(0.3, plan.AdditionalContext["UserRiskTolerance"]);
    }

    [Fact]
    public async Task CreatePlanAsync_CallsStateAnalysisVersion()
    {
        // Arrange
        var mockOpenAI = new MockOpenAIService();
        var planningService = new PlanningService(mockOpenAI, _logger, _config);
        
        var availableTools = new List<McpClientTool>(); // no concrete instances needed

        // Act
        var plan = await planningService.CreatePlanAsync("Test task", availableTools, "Test context");

        // Assert
        Assert.NotNull(plan);
        Assert.Equal("Test task", plan.Task);
        Assert.NotEmpty(plan.Strategy);
        // The plan should have additional context since it now goes through state analysis
        Assert.NotNull(plan.AdditionalContext);
    }

    [Fact]
    public async Task RefinePlanWithStateAsync_WithStateContext_CreatesEnhancedRefinedPlan()
    {
        // Arrange
        var mockOpenAI = new MockOpenAIService();
        var planningService = new PlanningService(mockOpenAI, _logger, _config);
        
        var existingPlan = new TaskPlan
        {
            Task = "Analyze data",
            Strategy = "Simple analysis",
            Steps = new List<PlanStep>
            {
                new PlanStep
                {
                    StepNumber = 1,
                    Description = "Load data",
                    PotentialTools = new List<string> { "file_reader" }
                }
            },
            Complexity = TaskComplexity.Simple,
            Confidence = 0.6
        };

        var currentState = new CurrentState
        {
            SessionContext = "User needs more detailed analysis",
            PreviousResults = new List<ExecutionResult>
            {
                new ExecutionResult
                {
                    Task = "Previous analysis",
                    Success = false,
                    Summary = "Analysis was too shallow",
                    Insights = "Need more statistical analysis"
                }
            },
            UserPreferences = new UserPreferences
            {
                PreferredApproach = "thorough",
                RiskTolerance = 0.8
            }
        };

        var availableTools = new List<McpClientTool>(); // no concrete instances needed

        // Act
        var refinedPlan = await planningService.RefinePlanWithStateAsync(
            existingPlan,
            "Need more thorough statistical analysis with charts",
            availableTools,
            currentState
        );

        // Assert
        Assert.NotNull(refinedPlan);
        Assert.Equal("Analyze data", refinedPlan.Task);
        Assert.NotEmpty(refinedPlan.Strategy);
        Assert.NotNull(refinedPlan.AdditionalContext);
        Assert.True(refinedPlan.AdditionalContext.ContainsKey("RefinementFeedback"));
        Assert.True(refinedPlan.AdditionalContext.ContainsKey("RefinedAt"));
        Assert.True(refinedPlan.AdditionalContext.ContainsKey("OriginalComplexity"));
        Assert.True(refinedPlan.AdditionalContext.ContainsKey("OriginalConfidence"));
        Assert.Equal("Need more thorough statistical analysis with charts", refinedPlan.AdditionalContext["RefinementFeedback"]);
        Assert.Equal("Simple", refinedPlan.AdditionalContext["OriginalComplexity"]);
        Assert.Equal(0.6, refinedPlan.AdditionalContext["OriginalConfidence"]);
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