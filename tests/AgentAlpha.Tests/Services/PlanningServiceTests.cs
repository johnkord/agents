using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using AgentAlpha.Configuration;
using AgentAlpha.Services;
using OpenAIIntegration;
using OpenAIIntegration.Model;
using System.Text.Json;

namespace AgentAlpha.Tests.Services;

public class PlanningServiceTests
{
    private readonly Mock<ISessionAwareOpenAIService> _mockOpenAiService;
    private readonly Mock<ILogger<PlanningService>> _mockLogger;
    private readonly AgentConfiguration _config;
    private readonly PlanningService _planningService;

    public PlanningServiceTests()
    {
        _mockOpenAiService = new Mock<ISessionAwareOpenAIService>();
        _mockLogger = new Mock<ILogger<PlanningService>>();
        _config = new AgentConfiguration
        {
            Model = "gpt-4.1",
            PlanningModel = "o1-preview" // Test with a reasoning model
        };
        _planningService = new PlanningService(_config, _mockOpenAiService.Object, _mockLogger.Object);
    }

    private static JsonElement CreateJsonElement(string content)
    {
        // Properly escape the content for JSON
        var json = System.Text.Json.JsonSerializer.Serialize(content);
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    [Fact]
    public async Task CreatePlanAsync_UsesPlanningModel_WhenConfigured()
    {
        // Arrange
        var task = "Create a data analysis report";
        var expectedModel = "o1-preview";
        
        _mockOpenAiService.Setup(x => x.CreateResponseAsync(
            It.Is<ResponsesCreateRequest>(req => req.Model == expectedModel),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResponsesCreateResponse
            {
                Output = new[] { new OutputMessage { Content = CreateJsonElement("# Test Plan\n\nThis is a test plan.") } }
            });

        // Act
        var result = await _planningService.CreatePlanAsync(task);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("# Test Plan", result);
        _mockOpenAiService.Verify(x => x.CreateResponseAsync(
            It.Is<ResponsesCreateRequest>(req => req.Model == expectedModel),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePlanAsync_FallsBackToMainModel_WhenPlanningModelNotSet()
    {
        // Arrange
        var configWithoutPlanningModel = new AgentConfiguration
        {
            Model = "gpt-4.1",
            PlanningModel = null
        };
        var service = new PlanningService(configWithoutPlanningModel, _mockOpenAiService.Object, _mockLogger.Object);
        var task = "Create a data analysis report";
        var expectedModel = "gpt-4.1";
        
        _mockOpenAiService.Setup(x => x.CreateResponseAsync(
            It.Is<ResponsesCreateRequest>(req => req.Model == expectedModel),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResponsesCreateResponse
            {
                Output = new[] { new OutputMessage { Content = CreateJsonElement("# Test Plan\n\nThis is a test plan.") } }
            });

        // Act
        var result = await service.CreatePlanAsync(task);

        // Assert
        Assert.NotNull(result);
        _mockOpenAiService.Verify(x => x.CreateResponseAsync(
            It.Is<ResponsesCreateRequest>(req => req.Model == expectedModel),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePlanAsync_CreatesFallbackPlan_OnException()
    {
        // Arrange
        var task = "Create a data analysis report";
        
        _mockOpenAiService.Setup(x => x.CreateResponseAsync(
            It.IsAny<ResponsesCreateRequest>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("API Error"));

        // Act
        var result = await _planningService.CreatePlanAsync(task);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Execution Plan:", result);
        Assert.Contains("Analysis Phase", result);
        Assert.Contains("fallback plan", result.ToLower());
    }

    [Fact]
    public async Task RefinePlanAsync_UsesPlanningModel_WhenRefiningPlan()
    {
        // Arrange
        var existingPlan = "# Original Plan\n\n1. Step 1\n2. Step 2";
        var feedback = "Add more detail to step 1";
        var expectedModel = "o1-preview";
        
        _mockOpenAiService.Setup(x => x.CreateResponseAsync(
            It.Is<ResponsesCreateRequest>(req => req.Model == expectedModel),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResponsesCreateResponse
            {
                Output = new[] { new OutputMessage { Content = CreateJsonElement("# Refined Plan\n\n1. Detailed Step 1\n2. Step 2") } }
            });

        // Act
        var result = await _planningService.RefinePlanAsync(existingPlan, feedback);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("# Refined Plan", result);
        _mockOpenAiService.Verify(x => x.CreateResponseAsync(
            It.Is<ResponsesCreateRequest>(req => req.Model == expectedModel),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePlanAsync_IncludesAvailableTools_InPrompt()
    {
        // Arrange
        var task = "Create a data analysis report";
        var availableTools = new List<string> { "pandas", "matplotlib", "jupyter" };
        
        _mockOpenAiService.Setup(x => x.CreateResponseAsync(
            It.IsAny<ResponsesCreateRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResponsesCreateResponse
            {
                Output = new[] { new OutputMessage { Content = CreateJsonElement("# Test Plan with Tools") } }
            });

        // Act
        var result = await _planningService.CreatePlanAsync(task, availableTools);

        // Assert
        Assert.NotNull(result);
        _mockOpenAiService.Verify(x => x.CreateResponseAsync(
            It.IsAny<ResponsesCreateRequest>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}