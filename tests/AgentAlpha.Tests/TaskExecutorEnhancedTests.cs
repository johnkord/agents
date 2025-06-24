using AgentAlpha.Interfaces;
using AgentAlpha.Models;
using Xunit;
using System.Reflection;

namespace AgentAlpha.Tests;

public class TaskExecutorEnhancedTests
{
    [Fact]
    public void ITaskExecutor_HasBothExecuteAsyncMethods()
    {
        // Arrange & Act & Assert
        var interfaceType = typeof(ITaskExecutor);
        var methods = interfaceType.GetMethods();
        
        // Verify string overload exists
        Assert.Contains(methods, m => m.Name == "ExecuteAsync" && 
            m.GetParameters().Length == 1 && 
            m.GetParameters()[0].ParameterType == typeof(string));
            
        // Verify TaskExecutionRequest overload exists
        Assert.Contains(methods, m => m.Name == "ExecuteAsync" && 
            m.GetParameters().Length == 1 && 
            m.GetParameters()[0].ParameterType == typeof(TaskExecutionRequest));
    }

    [Fact]
    public void TaskExecutionRequest_SupportsAllExpectedParameters()
    {
        // Arrange & Act
        var request = new TaskExecutionRequest
        {
            Task = "Test task",
            Model = "gpt-3.5-turbo",
            Temperature = 0.5,
            MaxIterations = 3,
            SystemPrompt = "Custom prompt",
            Priority = TaskPriority.High,
            Timeout = TimeSpan.FromMinutes(5),
            VerboseLogging = true
        };

        // Assert - Verify all parameters are properly stored
        Assert.Equal("Test task", request.Task);
        Assert.Equal("gpt-3.5-turbo", request.Model);
        Assert.Equal(0.5, request.Temperature);
        Assert.Equal(3, request.MaxIterations);
        Assert.Equal("Custom prompt", request.SystemPrompt);
        Assert.Equal(TaskPriority.High, request.Priority);
        Assert.Equal(TimeSpan.FromMinutes(5), request.Timeout);
        Assert.True(request.VerboseLogging);
    }

    [Theory]
    [InlineData("gpt-4o")]
    [InlineData("gpt-3.5-turbo")]
    [InlineData("gpt-4-turbo")]
    public void TaskExecutionRequest_AcceptsValidModelNames(string model)
    {
        // Arrange & Act
        var request = TaskExecutionRequest.FromTaskAndModel("Test", model);

        // Assert
        Assert.Equal(model, request.Model);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void TaskExecutionRequest_AcceptsValidTemperatures(double temperature)
    {
        // Arrange & Act
        var request = new TaskExecutionRequest { Temperature = temperature };

        // Assert
        Assert.Equal(temperature, request.Temperature);
    }

    [Fact]
    public void TaskPriority_HasExpectedValues()
    {
        // Verify the enum has the expected values
        Assert.True(Enum.IsDefined(typeof(TaskPriority), TaskPriority.Low));
        Assert.True(Enum.IsDefined(typeof(TaskPriority), TaskPriority.Normal));
        Assert.True(Enum.IsDefined(typeof(TaskPriority), TaskPriority.High));
        
        // Verify default is Normal
        var request = new TaskExecutionRequest();
        Assert.Equal(TaskPriority.Normal, request.Priority);
    }

    [Fact]
    public void TaskExecutionRequest_DefaultTimeoutIsNull()
    {
        // Arrange & Act
        var request = new TaskExecutionRequest();

        // Assert
        Assert.Null(request.Timeout);
    }

    [Fact]
    public void TaskExecutionRequest_CanSetTimeout()
    {
        // Arrange
        var timeout = TimeSpan.FromMinutes(10);
        var request = new TaskExecutionRequest();

        // Act
        request.Timeout = timeout;

        // Assert
        Assert.Equal(timeout, request.Timeout);
    }
}