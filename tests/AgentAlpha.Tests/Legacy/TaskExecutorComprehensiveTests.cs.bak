using Microsoft.Extensions.Logging;
using Xunit;
using AgentAlpha.Models;

namespace AgentAlpha.Tests;

/// <summary>
/// Comprehensive tests for TaskExecutor to improve code coverage
/// Focus on properties, models, and basic functionality without complex mocking
/// </summary>
public class TaskExecutorComprehensiveTests
{
    [Fact]
    public void TaskExecutionRequest_WithAllProperties_ShouldSetCorrectly()
    {
        // Arrange & Act
        var request = new TaskExecutionRequest
        {
            Task = "Comprehensive test task",
            Model = "gpt-4.1",
            Temperature = 0.8,
            MaxIterations = 10,
            SystemPrompt = "You are a helpful assistant",
            Priority = TaskPriority.High,
            Timeout = TimeSpan.FromMinutes(30),
            VerboseLogging = true,
            SessionId = "test-session-123",
            SessionName = "Test Session"
        };

        // Assert
        Assert.Equal("Comprehensive test task", request.Task);
        Assert.Equal("gpt-4.1", request.Model);
        Assert.Equal(0.8, request.Temperature);
        Assert.Equal(10, request.MaxIterations);
        Assert.Equal("You are a helpful assistant", request.SystemPrompt);
        Assert.Equal(TaskPriority.High, request.Priority);
        Assert.Equal(TimeSpan.FromMinutes(30), request.Timeout);
        Assert.True(request.VerboseLogging);
        Assert.Equal("test-session-123", request.SessionId);
        Assert.Equal("Test Session", request.SessionName);
    }

    [Fact]
    public void TaskExecutionRequest_FromTask_ShouldCreateBasicRequest()
    {
        // Arrange
        var taskDescription = "Simple task";

        // Act
        var request = TaskExecutionRequest.FromTask(taskDescription);

        // Assert
        Assert.Equal(taskDescription, request.Task);
        Assert.Equal(TaskPriority.Normal, request.Priority);
        Assert.Null(request.Model);
        Assert.Null(request.Temperature);
        Assert.Null(request.MaxIterations);
        Assert.Null(request.SystemPrompt);
        Assert.Null(request.Timeout);
        Assert.False(request.VerboseLogging);
    }

    [Theory]
    [InlineData(TaskPriority.Low)]
    [InlineData(TaskPriority.Normal)]
    [InlineData(TaskPriority.High)]
    public void TaskExecutionRequest_WithDifferentPriorities_ShouldAcceptAll(TaskPriority priority)
    {
        // Arrange & Act
        var request = new TaskExecutionRequest
        {
            Task = "Priority test",
            Priority = priority
        };

        // Assert
        Assert.Equal(priority, request.Priority);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(null)]
    public void TaskExecutionRequest_WithDifferentTemperatures_ShouldAcceptAll(double? temperature)
    {
        // Arrange & Act
        var request = new TaskExecutionRequest
        {
            Task = "Temperature test",
            Temperature = temperature
        };

        // Assert
        Assert.Equal(temperature, request.Temperature);
    }

    [Theory]
    [InlineData("gpt-4.1")]
    [InlineData("gpt-4-turbo")]
    [InlineData("gpt-4.1-nano")]
    [InlineData(null)]
    public void TaskExecutionRequest_WithDifferentModels_ShouldAcceptAll(string? model)
    {
        // Arrange & Act
        var request = new TaskExecutionRequest
        {
            Task = "Model test",
            Model = model
        };

        // Assert
        Assert.Equal(model, request.Model);
    }

    [Fact]
    public void TaskExecutionRequest_WithLongTask_ShouldHandleCorrectly()
    {
        // Arrange
        var longTask = string.Join(" ", Enumerable.Repeat("Long task description", 100));

        // Act
        var request = new TaskExecutionRequest
        {
            Task = longTask
        };

        // Assert
        Assert.Equal(longTask, request.Task);
    }

    [Fact]
    public void TaskExecutionRequest_WithSpecialCharacters_ShouldHandleCorrectly()
    {
        // Arrange
        var taskWithSpecialChars = "Task with special chars: @#$%^&*()_+{}|:<>?[]\\;'\",./ and unicode: 你好世界";

        // Act
        var request = new TaskExecutionRequest
        {
            Task = taskWithSpecialChars
        };

        // Assert
        Assert.Equal(taskWithSpecialChars, request.Task);
    }

    [Fact]
    public void TaskExecutionRequest_WithMaxIterations_ShouldRespectLimits()
    {
        // Arrange & Act
        var request = new TaskExecutionRequest
        {
            Task = "Iteration test",
            MaxIterations = 1
        };

        // Assert
        Assert.Equal(1, request.MaxIterations);
    }

    [Fact]
    public void TaskExecutionRequest_WithTimeout_ShouldStoreCorrectly()
    {
        // Arrange
        var timeout = TimeSpan.FromHours(2);

        // Act
        var request = new TaskExecutionRequest
        {
            Task = "Timeout test",
            Timeout = timeout
        };

        // Assert
        Assert.Equal(timeout, request.Timeout);
    }

    [Fact]
    public void TaskExecutionRequest_WithEmptyTask_ShouldAllowEmpty()
    {
        // Arrange & Act
        var request = new TaskExecutionRequest
        {
            Task = ""
        };

        // Assert
        Assert.Equal("", request.Task);
    }

    [Fact]
    public void TaskExecutionRequest_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var request = new TaskExecutionRequest();

        // Assert
        Assert.Equal(string.Empty, request.Task);
        Assert.Equal(TaskPriority.Normal, request.Priority);
        Assert.Null(request.Model);
        Assert.Null(request.Temperature);
        Assert.Null(request.MaxIterations);
        Assert.Null(request.SystemPrompt);
        Assert.Null(request.Timeout);
        Assert.False(request.VerboseLogging);
        Assert.Null(request.SessionId);
        Assert.Null(request.SessionName);
        Assert.Null(request.ToolFilter);
    }

    [Fact]
    public void TaskExecutionRequest_WithVerboseLogging_ShouldToggleCorrectly()
    {
        // Arrange & Act
        var request = new TaskExecutionRequest
        {
            Task = "Verbose test",
            VerboseLogging = true
        };

        // Assert
        Assert.True(request.VerboseLogging);
        
        // Test toggle
        request.VerboseLogging = false;
        Assert.False(request.VerboseLogging);
    }

    [Fact]
    public void TaskPriority_Enum_ShouldHaveExpectedValues()
    {
        // Assert: Verify enum values exist
        Assert.True(Enum.IsDefined(typeof(TaskPriority), TaskPriority.Low));
        Assert.True(Enum.IsDefined(typeof(TaskPriority), TaskPriority.Normal));
        Assert.True(Enum.IsDefined(typeof(TaskPriority), TaskPriority.High));
        
        // Verify enum order/values
        Assert.Equal(0, (int)TaskPriority.Low);
        Assert.Equal(1, (int)TaskPriority.Normal);
        Assert.Equal(2, (int)TaskPriority.High);
    }

    [Fact]
    public void TaskExecutionRequest_WithSessionIdAndName_ShouldStoreBoth()
    {
        // Arrange & Act
        var request = new TaskExecutionRequest
        {
            Task = "Session test",
            SessionId = "session-123",
            SessionName = "Test Session Name"
        };

        // Assert
        Assert.Equal("session-123", request.SessionId);
        Assert.Equal("Test Session Name", request.SessionName);
    }
}