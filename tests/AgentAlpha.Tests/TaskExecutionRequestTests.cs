using AgentAlpha.Models;
using Xunit;

namespace AgentAlpha.Tests;

public class TaskExecutionRequestTests
{
    [Fact]
    public void TaskExecutionRequest_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var request = new TaskExecutionRequest();

        // Assert
        Assert.Equal(string.Empty, request.Task);
        Assert.Null(request.Model);
        Assert.Null(request.Temperature);
        Assert.Null(request.MaxIterations);
        Assert.Null(request.SystemPrompt);
        Assert.Equal(TaskPriority.Normal, request.Priority);
        Assert.Null(request.Timeout);
        Assert.Null(request.ToolFilter);
        Assert.False(request.VerboseLogging);
    }

    [Fact]
    public void FromTask_CreatesRequestWithTask()
    {
        // Arrange
        const string task = "Calculate 2 + 2";

        // Act
        var request = TaskExecutionRequest.FromTask(task);

        // Assert
        Assert.Equal(task, request.Task);
        Assert.Null(request.Model);
        Assert.Equal(TaskPriority.Normal, request.Priority);
    }

    [Fact]
    public void FromTaskAndModel_CreatesRequestWithTaskAndModel()
    {
        // Arrange
        const string task = "Write a story";
        const string model = "gpt-4.1";

        // Act
        var request = TaskExecutionRequest.FromTaskAndModel(task, model);

        // Assert
        Assert.Equal(task, request.Task);
        Assert.Equal(model, request.Model);
        Assert.Equal(TaskPriority.Normal, request.Priority);
    }

    [Theory]
    [InlineData(TaskPriority.Low)]
    [InlineData(TaskPriority.Normal)]
    [InlineData(TaskPriority.High)]
    public void Priority_CanBeSetToAllValidValues(TaskPriority priority)
    {
        // Arrange
        var request = new TaskExecutionRequest();

        // Act
        request.Priority = priority;

        // Assert
        Assert.Equal(priority, request.Priority);
    }

    [Fact]
    public void AllProperties_CanBeSetAndRetrieved()
    {
        // Arrange
        var request = new TaskExecutionRequest
        {
            Task = "Test task",
            Model = "gpt-4.1",
            Temperature = 0.7,
            MaxIterations = 5,
            SystemPrompt = "Custom prompt",
            Priority = TaskPriority.High,
            Timeout = TimeSpan.FromMinutes(10),
            VerboseLogging = true
        };

        // Assert
        Assert.Equal("Test task", request.Task);
        Assert.Equal("gpt-4.1", request.Model);
        Assert.Equal(0.7, request.Temperature);
        Assert.Equal(5, request.MaxIterations);
        Assert.Equal("Custom prompt", request.SystemPrompt);
        Assert.Equal(TaskPriority.High, request.Priority);
        Assert.Equal(TimeSpan.FromMinutes(10), request.Timeout);
        Assert.True(request.VerboseLogging);
    }
}