using Xunit;
using Microsoft.Extensions.Logging;
using AgentAlpha.Services;
using AgentAlpha.Models;
using AgentAlpha.Configuration;
using SessionService.Services;
using Common.Models.Session;
using System.Text.Json;

namespace AgentAlpha.Tests;

public class TaskCompletionReportingTests
{
    [Fact]
    public void TaskCompletionTool_EnhancedParameters_ReturnsStructuredReport()
    {
        // Arrange
        var summary = "Successfully analyzed data and generated insights";
        var reasoning = "All required data analysis steps were completed successfully";
        var evidence = "Generated 3 charts, processed 1000 records, created final report";
        var deliverables = "analysis-report.pdf, data-charts.png, summary.txt";
        var keyActions = "Loaded data, cleaned records, analyzed patterns, generated visualizations";

        // Act - Use reflection to call the static method since we don't have direct access
        var result = TaskCompletionTool.CompleteTask(
            summary: summary,
            reasoning: reasoning, 
            evidence: evidence,
            deliverables: deliverables,
            keyActions: keyActions);

        // Assert
        Assert.Contains("TASK COMPLETED:", result);
        Assert.Contains(summary, result);
        Assert.Contains("COMPLETION REPORT:", result);
        
        // Verify the JSON structure is present
        Assert.Contains("\"Status\": \"COMPLETED\"", result);
        Assert.Contains("\"Summary\":", result);
        Assert.Contains("\"Reasoning\":", result);
        Assert.Contains("\"Evidence\":", result);
        Assert.Contains("\"Deliverables\":", result);
        Assert.Contains("\"KeyActions\":", result);
        Assert.Contains("\"CompletedAt\":", result);
    }

    [Fact]
    public void TaskCompletionTool_MinimalParameters_ReturnsBasicReport()
    {
        // Act
        var result = TaskCompletionTool.CompleteTask(summary: "Basic task completed");

        // Assert
        Assert.Contains("TASK COMPLETED: Basic task completed", result);
        Assert.Contains("COMPLETION REPORT:", result);
        Assert.Contains("\"Status\": \"COMPLETED\"", result);
        Assert.Contains("\"Summary\": \"Basic task completed\"", result);
    }

    [Fact]
    public void TaskCompletionTool_NoParameters_ReturnsDefaultReport()
    {
        // Act
        var result = TaskCompletionTool.CompleteTask();

        // Assert
        Assert.Contains("TASK COMPLETED: Successfully completed", result);
        Assert.Contains("COMPLETION REPORT:", result);
        Assert.Contains("\"Status\": \"COMPLETED\"", result);
        Assert.Contains("\"Summary\": \"Task completed\"", result);
        Assert.Contains("\"Reasoning\": \"Task completion criteria have been met\"", result);
    }

    [Fact]
    public async Task TaskCompletionReporting_GeneratesComprehensiveActivityLog()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var sessionManager = new SessionManager(loggerFactory.CreateLogger<SessionManager>());
        var activityLogger = new SessionActivityLogger(sessionManager, loggerFactory.CreateLogger<SessionActivityLogger>());
        
        var session = await sessionManager.CreateSessionAsync("Task Completion Report Test");
        activityLogger.SetCurrentSession(session);

        // Simulate various activities that would occur during task execution
        await activityLogger.LogActivityAsync(ActivityTypes.SessionStart, "Session started for task completion test");
        await activityLogger.LogActivityAsync(ActivityTypes.TaskPlanning, "Created execution plan", 
            new { Strategy = "Test strategy", Complexity = "Simple" });
        await activityLogger.LogActivityAsync(ActivityTypes.ToolSelection, "Selected tools for execution",
            new { SelectedTools = new[] { "tool1", "tool2", "complete_task" } });
        
        // Simulate tool calls
        await activityLogger.LogActivityAsync(ActivityTypes.ToolCall, "Executing tool: test_tool", 
            new { ToolName = "test_tool", Arguments = new { param1 = "value1" } });
        await activityLogger.LogActivityAsync(ActivityTypes.ToolResult, "Tool result: test_tool", 
            new { ToolName = "test_tool", Success = true, ResultLength = 100 });
        
        // Simulate OpenAI interactions
        await activityLogger.LogActivityAsync(ActivityTypes.OpenAIRequest, "Sending request to OpenAI", 
            new { Model = "gpt-4", MessageCount = 5 });
        await activityLogger.LogActivityAsync(ActivityTypes.OpenAIResponse, "Received OpenAI response", 
            new { Model = "gpt-4", TokensUsed = 500 });

        // Simulate task completion
        await activityLogger.LogActivityAsync(ActivityTypes.TaskCompletionEvaluation, 
            "Task completion report - Successfully analyzed data",
            new 
            {
                TaskCompletion = new
                {
                    Status = "COMPLETED",
                    Summary = "Successfully analyzed data",
                    ProvidedReasoning = "All analysis steps completed successfully",
                    ProvidedEvidence = "Generated reports and charts"
                },
                ExecutionEvidence = new
                {
                    ToolsUsed = new[] { "test_tool" },
                    OpenAIInteractions = 2,
                    ErrorsEncountered = new object[0]
                },
                TaskCompletionQuality = new
                {
                    HasDetailedReasoning = true,
                    HasExplicitEvidence = true,
                    RecommendationScore = 0.95
                }
            });

        // Act
        var activities = await activityLogger.GetSessionActivitiesAsync();

        // Assert
        Assert.NotEmpty(activities);
        
        // Verify we have different types of activities
        Assert.Contains(activities, a => a.ActivityType == ActivityTypes.SessionStart);
        Assert.Contains(activities, a => a.ActivityType == ActivityTypes.TaskPlanning);
        Assert.Contains(activities, a => a.ActivityType == ActivityTypes.ToolSelection);
        Assert.Contains(activities, a => a.ActivityType == ActivityTypes.ToolCall);
        Assert.Contains(activities, a => a.ActivityType == ActivityTypes.ToolResult);
        Assert.Contains(activities, a => a.ActivityType == ActivityTypes.OpenAIRequest);
        Assert.Contains(activities, a => a.ActivityType == ActivityTypes.OpenAIResponse);
        Assert.Contains(activities, a => a.ActivityType == ActivityTypes.TaskCompletionEvaluation);

        // Verify the task completion evaluation activity has comprehensive data
        var completionActivity = activities.FirstOrDefault(a => a.ActivityType == ActivityTypes.TaskCompletionEvaluation);
        Assert.NotNull(completionActivity);
        Assert.Contains("Task completion report", completionActivity.Description);
        Assert.True(completionActivity.Success);
        
        // Verify the completion data structure
        var completionData = completionActivity.Data;
        Assert.NotNull(completionData);
        Assert.NotEmpty(completionData);
        
        // Parse the JSON data
        var completionJson = JsonSerializer.Deserialize<JsonElement>(completionData);
        Assert.True(completionJson.TryGetProperty("TaskCompletion", out var taskCompletion));
        Assert.True(taskCompletion.TryGetProperty("Status", out var status));
        Assert.Equal("COMPLETED", status.GetString());
        
        Assert.True(completionJson.TryGetProperty("ExecutionEvidence", out var evidence));
        Assert.True(completionJson.TryGetProperty("TaskCompletionQuality", out var quality));
        Assert.True(quality.TryGetProperty("RecommendationScore", out var score));
        Assert.Equal(0.95, score.GetDouble());
    }

    [Fact]
    public void TaskCompletionEvaluation_ActivityType_ExistsInActivityTypes()
    {
        // This test ensures that the TaskCompletionEvaluation activity type is properly defined
        // in the ActivityTypes class and can be used consistently
        
        // Arrange & Act
        var activityType = ActivityTypes.TaskCompletionEvaluation;
        
        // Assert
        Assert.Equal("Task_Completion_Evaluation", activityType);
        Assert.NotNull(activityType);
        Assert.NotEmpty(activityType);
    }

    [Fact]
    public async Task TaskCompletionReporting_HandlesEmptySessionGracefully()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var sessionManager = new SessionManager(loggerFactory.CreateLogger<SessionManager>());
        var activityLogger = new SessionActivityLogger(sessionManager, loggerFactory.CreateLogger<SessionActivityLogger>());
        
        var session = await sessionManager.CreateSessionAsync("Empty Session Test");
        activityLogger.SetCurrentSession(session);

        // Act - simulate task completion with no prior activities
        await activityLogger.LogActivityAsync(ActivityTypes.TaskCompletionEvaluation, 
            "Task completion report - minimal execution",
            new 
            {
                TaskCompletion = new
                {
                    Status = "COMPLETED",
                    Summary = "Minimal task completed",
                    ProvidedReasoning = "",
                    ProvidedEvidence = ""
                },
                ExecutionEvidence = new
                {
                    ToolsUsed = new object[0],
                    OpenAIInteractions = 0,
                    ErrorsEncountered = new object[0]
                },
                TaskCompletionQuality = new
                {
                    HasDetailedReasoning = false,
                    HasExplicitEvidence = false,
                    RecommendationScore = 0.4
                }
            });

        // Assert
        var activities = await activityLogger.GetSessionActivitiesAsync();
        Assert.Single(activities);
        
        var completionActivity = activities.First();
        Assert.Equal(ActivityTypes.TaskCompletionEvaluation, completionActivity.ActivityType);
        Assert.True(completionActivity.Success);
        
        // Verify the completion data can be deserialized correctly
        var completionData = completionActivity.Data;
        Assert.NotNull(completionData);
        Assert.NotEmpty(completionData);
    }
}