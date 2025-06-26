using Xunit;

namespace MCPServer.Tests;

public class TaskCompletionToolTests
{
    [Fact]
    public void CompleteTask_WithoutSummary_ShouldReturnDefaultMessage()
    {
        // Act
        var result = TaskCompletionTool.CompleteTask();

        // Assert
        Assert.Equal("TASK COMPLETED", result);
    }

    [Fact]
    public void CompleteTask_WithNullSummary_ShouldReturnDefaultMessage()
    {
        // Act
        var result = TaskCompletionTool.CompleteTask(null);

        // Assert
        Assert.Equal("TASK COMPLETED", result);
    }

    [Fact]
    public void CompleteTask_WithEmptySummary_ShouldReturnDefaultMessage()
    {
        // Act
        var result = TaskCompletionTool.CompleteTask("");

        // Assert
        Assert.Equal("TASK COMPLETED", result);
    }

    [Fact]
    public void CompleteTask_WithWhitespaceSummary_ShouldReturnDefaultMessage()
    {
        // Act
        var result = TaskCompletionTool.CompleteTask("   ");

        // Assert
        Assert.Equal("TASK COMPLETED", result);
    }

    [Fact]
    public void CompleteTask_WithValidSummary_ShouldReturnMessageWithSummary()
    {
        // Arrange
        var summary = "Successfully completed the task";

        // Act
        var result = TaskCompletionTool.CompleteTask(summary);

        // Assert
        Assert.Equal($"TASK COMPLETED: {summary}", result);
    }

    [Theory]
    [InlineData("Task finished successfully")]
    [InlineData("Analysis complete")]
    [InlineData("Data processing done")]
    [InlineData("Report generated")]
    public void CompleteTask_WithVariousSummaries_ShouldReturnCorrectFormat(string summary)
    {
        // Act
        var result = TaskCompletionTool.CompleteTask(summary);

        // Assert
        Assert.Equal($"TASK COMPLETED: {summary}", result);
        Assert.StartsWith("TASK COMPLETED:", result);
    }

    [Fact]
    public void CompleteTask_WithSpecialCharacters_ShouldHandleCorrectly()
    {
        // Arrange
        var summary = "Task with special chars: @#$%^&*()";

        // Act
        var result = TaskCompletionTool.CompleteTask(summary);

        // Assert
        Assert.Equal($"TASK COMPLETED: {summary}", result);
        Assert.Contains(summary, result);
    }

    [Fact]
    public void CompleteTask_WithLongSummary_ShouldHandleCorrectly()
    {
        // Arrange
        var summary = new string('A', 1000); // Very long summary

        // Act
        var result = TaskCompletionTool.CompleteTask(summary);

        // Assert
        Assert.Equal($"TASK COMPLETED: {summary}", result);
        Assert.Contains(summary, result);
    }
}