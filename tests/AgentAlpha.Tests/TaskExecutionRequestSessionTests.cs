using Xunit;
using AgentAlpha.Models;

namespace AgentAlpha.Tests;

public class TaskExecutionRequestSessionTests
{
    [Fact]
    public void TaskExecutionRequest_ShouldSupportSessionId()
    {
        // Arrange & Act
        var request = new TaskExecutionRequest
        {
            Task = "Test task",
            SessionId = "test-session-id"
        };

        // Assert
        Assert.Equal("test-session-id", request.SessionId);
        Assert.Equal("Test task", request.Task);
    }

    [Fact]
    public void TaskExecutionRequest_ShouldSupportSessionName()
    {
        // Arrange & Act
        var request = new TaskExecutionRequest
        {
            Task = "Test task",
            SessionName = "My Test Session"
        };

        // Assert
        Assert.Equal("My Test Session", request.SessionName);
        Assert.Equal("Test task", request.Task);
    }

    [Fact]
    public void TaskExecutionRequest_ShouldSupportBothSessionIdAndName()
    {
        // Arrange & Act
        var request = new TaskExecutionRequest
        {
            Task = "Test task",
            SessionId = "existing-session-id",
            SessionName = "Fallback Session Name"
        };

        // Assert
        Assert.Equal("existing-session-id", request.SessionId);
        Assert.Equal("Fallback Session Name", request.SessionName);
    }

    [Fact]
    public void FromTask_ShouldCreateBasicRequest()
    {
        // Act
        var request = TaskExecutionRequest.FromTask("Test task");

        // Assert
        Assert.Equal("Test task", request.Task);
        Assert.Null(request.SessionId);
        Assert.Null(request.SessionName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TaskExecutionRequest_ShouldHandleNullOrEmptySessionValues(string? sessionValue)
    {
        // Arrange & Act
        var request = new TaskExecutionRequest
        {
            Task = "Test task",
            SessionId = sessionValue,
            SessionName = sessionValue
        };

        // Assert
        Assert.Equal(sessionValue, request.SessionId);
        Assert.Equal(sessionValue, request.SessionName);
    }
}