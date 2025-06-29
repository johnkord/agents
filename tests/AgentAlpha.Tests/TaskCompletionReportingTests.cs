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
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

        // Arrange
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
        var completionInfo = new
        {
            TaskCompletion = new
            {
                Status = "COMPLETED",
                Summary = "Successfully analyzed data and generated report",
                Results = new
                {
                    FilesProcessed = 5,
                    ReportGenerated = true,
                    OutputFile = "analysis_report.pdf"
                }
            }
        };
        
        await activityLogger.LogActivityAsync(
            ActivityTypes.TaskCompletionEvaluation,
            "Task completion report - " + completionInfo.TaskCompletion.Summary,
            completionInfo);

        // Get activities from database
        var activities = await sessionManager.GetSessionActivitiesAsync(session.SessionId);
        
        // Assert
        Assert.NotEmpty(activities);
        
        // Find task completion activity
        var completionActivity = activities.FirstOrDefault(a => 
            a.ActivityType == ActivityTypes.TaskCompletionEvaluation);
        
        Assert.NotNull(completionActivity);
        Assert.Contains("Successfully analyzed data", completionActivity.Description);
        Assert.Contains("COMPLETED", completionActivity.Data);
        Assert.Contains("FilesProcessed", completionActivity.Data);
        Assert.True(completionActivity.Success);
        
        // Verify the completion data structure
        var completionData = completionActivity.Data;
        Assert.NotNull(completionData);
        Assert.NotEmpty(completionData);
        
        // Parse the JSON data
        var completionJsonRaw = completionActivity.Data;
        var completionJson    = JsonSerializer.Deserialize<JsonElement>(completionJsonRaw);
        
        var tc = completionJson.GetProperty("TaskCompletion");
        Assert.Equal("COMPLETED", tc.GetProperty("Status").GetString());
        Assert.Equal("Successfully analyzed data and generated report", tc.GetProperty("Summary").GetString());
        var results = tc.GetProperty("Results");
        Assert.Equal(5, results.GetProperty("FilesProcessed").GetInt32());
        Assert.True(results.GetProperty("ReportGenerated").GetBoolean());
        Assert.Equal("analysis_report.pdf", results.GetProperty("OutputFile").GetString());
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