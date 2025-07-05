using AgentAlpha.Services;
using AgentAlpha.Models;
using Microsoft.Extensions.Logging;
using Moq;
using OpenAIIntegration;
using OpenAIIntegration.Model;
using Xunit;

namespace AgentAlpha.Tests.Services;

public class TaskRouterTests
{
    private readonly Mock<ISessionAwareOpenAIService> _mockOpenAiService;
    private readonly Mock<ILogger<TaskRouter>> _mockLogger;
    private readonly TaskRouter _taskRouter;

    public TaskRouterTests()
    {
        _mockOpenAiService = new Mock<ISessionAwareOpenAIService>();
        _mockLogger = new Mock<ILogger<TaskRouter>>();
        _taskRouter = new TaskRouter(_mockOpenAiService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task RouteAsync_WithNullRequest_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _taskRouter.RouteAsync(null));
    }

    [Fact]
    public async Task RouteAsync_WithEmptyTask_ShouldThrowArgumentException()
    {
        // Arrange
        var request = new TaskExecutionRequest { Task = "" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _taskRouter.RouteAsync(request));
    }

    [Theory]
    [InlineData("what time is it?", "SIMPLE_TOOL", 0.9, TaskRoute.FastPath)]
    [InlineData("list files in current directory", "SIMPLE_TOOL", 0.85, TaskRoute.FastPath)]
    [InlineData("explain quantum computing", "SIMPLE_QUERY", 0.95, TaskRoute.FastPath)]
    [InlineData("what is the capital of France?", "SIMPLE_QUERY", 0.9, TaskRoute.FastPath)]
    [InlineData("analyze the codebase and create a report", "COMPLEX", 0.8, TaskRoute.ReactLoop)]
    public async Task RouteAsync_WithVariousTasks_ShouldRouteCorrectly(
        string task, string classification, double confidence, TaskRoute expectedRoute)
    {
        // Arrange
        var request = new TaskExecutionRequest { Task = task, SessionId = "test-session" };
        var llmResponse = $@"{{
            ""classification"": ""{classification}"",
            ""confidence"": {confidence},
            ""reasoning"": ""Test reasoning""
        }}";

        var mockResponse = new ResponsesCreateResponse
        {
            Output = new[] { new OutputMessage { Content = llmResponse } }
        };

        _mockOpenAiService
            .Setup(x => x.CreateResponseAsync(
                It.IsAny<ResponsesCreateRequest>(),
                It.IsAny<string>()))
            .ReturnsAsync(mockResponse);

        // Act
        var (route, returnedConfidence) = await _taskRouter.RouteAsync(request);

        // Assert
        Assert.Equal(expectedRoute, route);
        Assert.Equal(confidence, returnedConfidence, 2);
    }

    [Fact]
    public async Task RouteAsync_WithLowConfidence_ShouldDefaultToReactLoop()
    {
        // Arrange
        var request = new TaskExecutionRequest { Task = "ambiguous task", SessionId = "test-session" };
        var llmResponse = @"{
            ""classification"": ""SIMPLE_TOOL"",
            ""confidence"": 0.5,
            ""reasoning"": ""Not sure about this""
        }";

        var mockResponse = new ResponsesCreateResponse
        {
            Output = new[] { new OutputMessage { Content = llmResponse } }
        };

        _mockOpenAiService
            .Setup(x => x.CreateResponseAsync(
                It.IsAny<ResponsesCreateRequest>(),
                It.IsAny<string>()))
            .ReturnsAsync(mockResponse);

        // Act
        var (route, confidence) = await _taskRouter.RouteAsync(request);

        // Assert
        Assert.Equal(TaskRoute.ReactLoop, route);
        Assert.Equal(0.5, confidence);
        
        // Verify logging
        _mockLogger.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Low confidence")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RouteAsync_WithInvalidJsonResponse_ShouldDefaultToReactLoop()
    {
        // Arrange
        var request = new TaskExecutionRequest { Task = "test task", SessionId = "test-session" };
        
        var mockResponse = new ResponsesCreateResponse
        {
            Output = new[] { new OutputMessage { Content = "This is not valid JSON" } }
        };

        _mockOpenAiService
            .Setup(x => x.CreateResponseAsync(
                It.IsAny<ResponsesCreateRequest>(),
                It.IsAny<string>()))
            .ReturnsAsync(mockResponse);

        // Act
        var (route, confidence) = await _taskRouter.RouteAsync(request);

        // Assert
        Assert.Equal(TaskRoute.ReactLoop, route);
        Assert.Equal(0.3, confidence); // Default confidence for parse errors
        
        // Verify error logging
        _mockLogger.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Failed to parse")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RouteAsync_WithEmptyLLMResponse_ShouldDefaultToReactLoop()
    {
        // Arrange
        var request = new TaskExecutionRequest { Task = "test task", SessionId = "test-session" };

        var mockResponse = new ResponsesCreateResponse
        {
            Output = new[] { new OutputMessage { Content = string.Empty } }
        };

        _mockOpenAiService
            .Setup(x => x.CreateResponseAsync(
                It.IsAny<ResponsesCreateRequest>(),
                It.IsAny<string>()))
            .ReturnsAsync(mockResponse);

        // Act
        var (route, confidence) = await _taskRouter.RouteAsync(request);

        // Assert
        Assert.Equal(TaskRoute.ReactLoop, route);
        Assert.Equal(0.5, confidence);
    }

    [Fact]
    public async Task RouteAsync_WithOpenAIException_ShouldDefaultToReactLoop()
    {
        // Arrange
        var request = new TaskExecutionRequest { Task = "test task", SessionId = "test-session" };

        _mockOpenAiService
            .Setup(x => x.CreateResponseAsync(
                It.IsAny<ResponsesCreateRequest>(),
                It.IsAny<string>()))
            .ThrowsAsync(new Exception("OpenAI API error"));

        // Act
        var (route, confidence) = await _taskRouter.RouteAsync(request);

        // Assert
        Assert.Equal(TaskRoute.ReactLoop, route);
        Assert.Equal(0.0, confidence); // Zero confidence on exception
    }

    [Theory]
    [InlineData("UNKNOWN_TYPE", TaskRoute.ReactLoop)]
    [InlineData("", TaskRoute.ReactLoop)]
    [InlineData(null, TaskRoute.ReactLoop)]
    public async Task RouteAsync_WithUnknownClassification_ShouldDefaultToReactLoop(
        string classification, TaskRoute expectedRoute)
    {
        // Arrange
        var request = new TaskExecutionRequest { Task = "test task", SessionId = "test-session" };
        var llmResponse = $@"{{
            ""classification"": ""{classification}"",
            ""confidence"": 0.9,
            ""reasoning"": ""Test reasoning""
        }}";

        var mockResponse = new ResponsesCreateResponse
        {
            Output = new[] { new OutputMessage { Content = llmResponse } }
        };

        _mockOpenAiService
            .Setup(x => x.CreateResponseAsync(
                It.IsAny<ResponsesCreateRequest>(),
                It.IsAny<string>()))
            .ReturnsAsync(mockResponse);

        // Act
        var (route, _) = await _taskRouter.RouteAsync(request);

        // Assert
        Assert.Equal(expectedRoute, route);
    }
}
