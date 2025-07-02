using Common.Interfaces.Session;
using Common.Models.Session;
using Common.Services.Session;
using Microsoft.Extensions.Logging;
using OpenAIIntegration;
using OpenAIIntegration.Model;
using Moq;
using System.Text.Json;
using Xunit;

namespace AgentAlpha.Tests;

public class SessionSummaryServiceTests
{
    private readonly Mock<ISessionManager> _mockSessionManager;
    private readonly Mock<ISessionAwareOpenAIService> _mockOpenAIService;
    private readonly Mock<ILogger<SessionSummaryService>> _mockLogger;
    private readonly SessionSummaryService _summaryService;
    private readonly string _testSessionId = "test-session-123";

    public SessionSummaryServiceTests()
    {
        _mockSessionManager = new Mock<ISessionManager>();
        _mockOpenAIService = new Mock<ISessionAwareOpenAIService>();
        _mockLogger = new Mock<ILogger<SessionSummaryService>>();
        
        _summaryService = new SessionSummaryService(
            _mockSessionManager.Object,
            _mockOpenAIService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GenerateSessionSummaryAsync_WithValidSession_ReturnsSummary()
    {
        // Arrange
        var session = CreateTestSession();
        var activities = CreateTestActivities();
        var mockResponse = CreateMockOpenAIResponse();

        _mockSessionManager.Setup(m => m.GetSessionAsync(_testSessionId))
            .ReturnsAsync(session);
        _mockSessionManager.Setup(m => m.GetSessionActivitiesAsync(_testSessionId))
            .ReturnsAsync(activities);
        _mockOpenAIService.Setup(m => m.CreateResponseAsync(It.IsAny<ResponsesCreateRequest>(), default))
            .ReturnsAsync(mockResponse);

        // Act
        var summary = await _summaryService.GenerateSessionSummaryAsync(_testSessionId);

        // Assert
        Assert.NotNull(summary);
        Assert.Equal(_testSessionId, summary.SessionId);
        Assert.Equal("Test Session", summary.SessionName);
        Assert.NotEmpty(summary.MarkdownSummary);
        Assert.True(summary.Statistics.TotalActivities > 0);
        Assert.Equal(2, summary.Statistics.ToolCalls);
        Assert.Equal(1, summary.Statistics.SuccessfulToolCalls);
    }

    [Fact]
    public async Task GenerateSessionSummaryAsync_WithNonExistentSession_ThrowsArgumentException()
    {
        // Arrange
        _mockSessionManager.Setup(m => m.GetSessionAsync(_testSessionId))
            .ReturnsAsync((AgentSession?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _summaryService.GenerateSessionSummaryAsync(_testSessionId));
    }

    [Fact]
    public async Task GenerateFinalSummaryAsync_WithUserQuestions_IncludesQuestionResponses()
    {
        // Arrange
        var session = CreateTestSession();
        var activities = CreateTestActivities();
        var mockResponse = CreateMockOpenAIResponse();
        var enhancedMockResponse = CreateMockOpenAIResponse("Enhanced summary with user questions");
        var userQuestions = "Did the task complete successfully? What errors occurred?";

        _mockSessionManager.Setup(m => m.GetSessionAsync(_testSessionId))
            .ReturnsAsync(session);
        _mockSessionManager.Setup(m => m.GetSessionActivitiesAsync(_testSessionId))
            .ReturnsAsync(activities);
        _mockOpenAIService.SetupSequence(m => m.CreateResponseAsync(It.IsAny<ResponsesCreateRequest>(), default))
            .ReturnsAsync(mockResponse)  // First call for basic summary
            .ReturnsAsync(enhancedMockResponse);  // Second call for enhancement

        // Act
        var summary = await _summaryService.GenerateFinalSummaryAsync(_testSessionId, userQuestions);

        // Assert
        Assert.NotNull(summary);
        Assert.Contains("Enhanced summary", summary.MarkdownSummary);
        
        // Verify that CreateResponseAsync was called twice (summary + enhancement)
        _mockOpenAIService.Verify(m => m.CreateResponseAsync(It.IsAny<ResponsesCreateRequest>(), default), 
            Times.Exactly(2));
    }

    [Fact]
    public async Task GeneratePartialSummaryAsync_WithTimeRange_FiltersActivities()
    {
        // Arrange
        var session = CreateTestSession();
        var activities = CreateTestActivitiesWithTimestamps();
        var mockResponse = CreateMockOpenAIResponse();
        
        var fromTime = DateTime.UtcNow.AddHours(-2);
        var toTime = DateTime.UtcNow.AddHours(-1);

        _mockSessionManager.Setup(m => m.GetSessionAsync(_testSessionId))
            .ReturnsAsync(session);
        _mockSessionManager.Setup(m => m.GetSessionActivitiesAsync(_testSessionId))
            .ReturnsAsync(activities);
        _mockOpenAIService.Setup(m => m.CreateResponseAsync(It.IsAny<ResponsesCreateRequest>(), default))
            .ReturnsAsync(mockResponse);

        // Act
        var summary = await _summaryService.GeneratePartialSummaryAsync(_testSessionId, fromTime, toTime);

        // Assert
        Assert.NotNull(summary);
        Assert.Null(summary.SessionEndTime); // Partial summaries don't have end time
    }

    [Fact]
    public void SummaryOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new SummaryOptions();

        // Assert
        Assert.True(options.IncludeDetailedLogs);
        Assert.False(options.IncludeRawData);
        Assert.Equal(500, options.MaxActivityDescriptionLength);
        Assert.Single(options.FocusAreas);
        Assert.Equal(SummaryFocus.TaskCompletion, options.FocusAreas[0]);
        Assert.True(options.IncludePerformanceAnalysis);
    }

    [Fact]
    public void SessionStatistics_ToolCallSuccessRate_CalculatesCorrectly()
    {
        // Arrange
        var stats = new SessionStatistics
        {
            ToolCalls = 10,
            SuccessfulToolCalls = 8
        };

        // Act & Assert
        Assert.Equal(80.0, stats.ToolCallSuccessRate);
    }

    [Fact]
    public void SessionStatistics_ToolCallSuccessRate_WithZeroToolCalls_ReturnsZero()
    {
        // Arrange
        var stats = new SessionStatistics
        {
            ToolCalls = 0,
            SuccessfulToolCalls = 0
        };

        // Act & Assert
        Assert.Equal(0.0, stats.ToolCallSuccessRate);
    }

    [Fact]
    public void TaskCompletionAssessment_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var assessment = new TaskCompletionAssessment();

        // Assert
        Assert.False(assessment.TaskCompleted);
        Assert.Equal(0, assessment.CompletionPercentage);
        Assert.Empty(assessment.Evidence);
        Assert.Empty(assessment.CompletedSubtasks);
        Assert.Empty(assessment.FailedSubtasks);
    }

    [Fact]
    public void SessionSummary_Duration_CalculatesCorrectly()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddHours(-2);
        var endTime = DateTime.UtcNow.AddHours(-1);
        
        var summary = new SessionSummary
        {
            SessionStartTime = startTime,
            SessionEndTime = endTime
        };

        // Act & Assert
        Assert.NotNull(summary.Duration);
        Assert.True(Math.Abs((summary.Duration.Value - TimeSpan.FromHours(1)).TotalMilliseconds) < 100);
    }

    [Fact]
    public void SessionSummary_Duration_WithNullEndTime_ReturnsNull()
    {
        // Arrange
        var summary = new SessionSummary
        {
            SessionStartTime = DateTime.UtcNow.AddHours(-2),
            SessionEndTime = null
        };

        // Act & Assert
        Assert.Null(summary.Duration);
    }

    private AgentSession CreateTestSession()
    {
        return new AgentSession
        {
            SessionId = _testSessionId,
            Name = "Test Session",
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            Status = SessionStatus.Completed,
            TaskStateMarkdown = """
                # Task: Complete test operation
                
                ## Subtasks
                - [x] Initialize system - Completed successfully
                - [x] Process data - Completed with warnings  
                - [ ] Cleanup resources - Failed due to permissions
                
                ## Progress Notes
                Task mostly completed with minor issues.
                """
        };
    }

    private List<SessionActivity> CreateTestActivities()
    {
        var activities = new List<SessionActivity>
        {
            SessionActivity.Create(ActivityTypes.SessionStart, "Session started"),
            SessionActivity.Create(ActivityTypes.ToolCall, "Called github_get_pull_request", 
                new { ToolName = "github_get_pull_request", Success = true }),
            SessionActivity.Create(ActivityTypes.ToolResult, "Tool result received", 
                new { ToolName = "github_get_pull_request", Success = true }),
            SessionActivity.Create(ActivityTypes.ToolCall, "Called file_editor", 
                new { ToolName = "file_editor", Success = false }),
            SessionActivity.Create(ActivityTypes.Error, "File permission error", 
                new { ErrorType = "PermissionDenied" }),
            SessionActivity.Create(ActivityTypes.TaskComplete, "Task completed with warnings"),
            SessionActivity.Create(ActivityTypes.SessionEnd, "Session ended")
        };

        // Set the Success property on the failed tool call
        activities[3].Success = false;
        activities[3].ErrorMessage = "File permission error";
        
        // Set the Success property on the error activity
        activities[4].Success = false;

        return activities;
    }

    private List<SessionActivity> CreateTestActivitiesWithTimestamps()
    {
        var activities = new List<SessionActivity>();
        var baseTime = DateTime.UtcNow.AddHours(-3);

        for (int i = 0; i < 5; i++)
        {
            var activity = SessionActivity.Create(ActivityTypes.Info, $"Activity {i}");
            activity.Timestamp = baseTime.AddMinutes(i * 30);
            activities.Add(activity);
        }

        return activities;
    }

    private ResponsesCreateResponse CreateMockOpenAIResponse(string content = "# Test Summary\n\nThis is a test summary.")
    {
        var outputMessage = new OutputMessage
        {
            Role = "assistant",
            Content = JsonDocument.Parse(JsonSerializer.Serialize(content)).RootElement
        };
        
        var response = new ResponsesCreateResponse
        {
            Id = "test-response-id",
            Output = new ResponseOutputItem[] { outputMessage }
        };
        return response;
    }
}