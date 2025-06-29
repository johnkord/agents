using AgentAlpha.Models;
using Xunit;

namespace AgentAlpha.Tests;

public class ArgumentParsingTests
{
    [Fact]
    public void TaskExecutionRequest_HasExpectedProperties()
    {
        // This test verifies that TaskExecutionRequest has all the properties
        // we need for command-line argument parsing
        
        // Arrange & Act
        var request = new TaskExecutionRequest();
        
        // Assert - Verify all properties exist and have expected default values
        Assert.Equal(string.Empty, request.Task);
        Assert.Null(request.Model);
        Assert.Null(request.Temperature);
        Assert.Null(request.MaxIterations);
        Assert.Null(request.SystemPrompt);
        Assert.Equal(TaskPriority.Normal, request.Priority);
        Assert.Null(request.Timeout);
        Assert.False(request.VerboseLogging);
    }

    [Theory]
    [InlineData("gpt-4.1")]
    [InlineData("gpt-4.1-nano")]
    [InlineData("gpt-4-turbo")]
    public void TaskExecutionRequest_SupportsModelParameter(string modelValue)
    {
        // Test that the model can be set via different parameter formats
        var request = new TaskExecutionRequest { Model = modelValue };
        Assert.Equal(modelValue, request.Model);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(0.7)]
    [InlineData(1.0)]
    public void TaskExecutionRequest_SupportsTemperatureParameter(double temperature)
    {
        // Test temperature parameter validation
        var request = new TaskExecutionRequest { Temperature = temperature };
        Assert.Equal(temperature, request.Temperature);
    }

    [Fact]
    public void TaskExecutionRequest_SupportsAllCommandLineParameters()
    {
        // Verify that all the parameters we want to support via command line
        // have corresponding properties in TaskExecutionRequest
        var requestType = typeof(TaskExecutionRequest);
        
        // Check for key properties that should be settable via command line
        Assert.NotNull(requestType.GetProperty("Model"));
        Assert.NotNull(requestType.GetProperty("Temperature"));
        Assert.NotNull(requestType.GetProperty("MaxIterations"));
        Assert.NotNull(requestType.GetProperty("Priority"));
        Assert.NotNull(requestType.GetProperty("Timeout"));
        Assert.NotNull(requestType.GetProperty("VerboseLogging"));
        Assert.NotNull(requestType.GetProperty("SystemPrompt"));
    }

    [Fact]
    public void TaskPriority_CanBeParsedFromString()
    {
        // Test that TaskPriority enum can be parsed from strings (for command line args)
        Assert.True(Enum.TryParse<TaskPriority>("Low", true, out var low));
        Assert.Equal(TaskPriority.Low, low);
        
        Assert.True(Enum.TryParse<TaskPriority>("Normal", true, out var normal));
        Assert.Equal(TaskPriority.Normal, normal);
        
        Assert.True(Enum.TryParse<TaskPriority>("High", true, out var high));
        Assert.Equal(TaskPriority.High, high);
    }

    [Fact]
    public void TaskExecutionRequest_FromTask_CreatesValidRequest()
    {
        // Test the static factory method
        const string testTask = "Test task";
        var request = TaskExecutionRequest.FromTask(testTask);
        
        Assert.Equal(testTask, request.Task);
        Assert.Null(request.Model);
        Assert.Equal(TaskPriority.Normal, request.Priority);
    }

    [Fact]
    public void TaskExecutionRequest_FromTaskAndModel_CreatesValidRequest()
    {
        // Test the static factory method with model
        const string testTask = "Test task";
        const string testModel = "gpt-4.1";
        
        var request = TaskExecutionRequest.FromTaskAndModel(testTask, testModel);
        
        Assert.Equal(testTask, request.Task);
        Assert.Equal(testModel, request.Model);
        Assert.Equal(TaskPriority.Normal, request.Priority);
    }
}