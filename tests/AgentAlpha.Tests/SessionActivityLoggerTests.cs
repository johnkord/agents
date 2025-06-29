using Xunit;
using Microsoft.Extensions.Logging;
using AgentAlpha.Services;
using AgentAlpha.Models;
using AgentAlpha.Configuration;
using SessionService.Services;
using Common.Models.Session;
using System.Text.Json;

namespace AgentAlpha.Tests;

public class SessionActivityLoggerTests
{
    [Fact]
    public async Task SessionActivityLogger_LogsActivitiesCorrectly()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var sessionManager = new SessionManager(loggerFactory.CreateLogger<SessionManager>());
        var activityLogger = new SessionActivityLogger(sessionManager, loggerFactory.CreateLogger<SessionActivityLogger>());
        
        var session = await sessionManager.CreateSessionAsync("Test Activity Logging");
        activityLogger.SetCurrentSession(session);
        
        // Act
        await activityLogger.LogActivityAsync(ActivityTypes.SessionStart, "Test session started");
        await activityLogger.LogActivityAsync(ActivityTypes.TaskPlanning, "Planning test task");
        
        var timedActivityId = activityLogger.StartActivity(ActivityTypes.OpenAIRequest, "Test OpenAI call");
        await Task.Delay(10); // Simulate some work
        await activityLogger.CompleteActivityAsync(timedActivityId);
        
        // Act - get activities
        var activities = await activityLogger.GetSessionActivitiesAsync();
        
        // Assert
        Assert.Equal(3, activities.Count);
        
        var sessionStartActivity = activities[0];
        Assert.Equal(ActivityTypes.SessionStart, sessionStartActivity.ActivityType);
        Assert.Equal("Test session started", sessionStartActivity.Description);
        Assert.True(sessionStartActivity.Success);
        
        var planningActivity = activities[1];
        Assert.Equal(ActivityTypes.TaskPlanning, planningActivity.ActivityType);
        Assert.Equal("Planning test task", planningActivity.Description);
        Assert.True(planningActivity.Success);
        
        var openAiActivity = activities[2];
        Assert.Equal(ActivityTypes.OpenAIRequest, openAiActivity.ActivityType);
        Assert.Equal("Test OpenAI call", openAiActivity.Description);
        Assert.True(openAiActivity.Success);
        Assert.True(openAiActivity.DurationMs.HasValue);
        Assert.True(openAiActivity.DurationMs.Value >= 0);
    }
    
    [Fact]
    public async Task SessionActivityLogger_LogsFailedActivitiesCorrectly()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var sessionManager = new SessionManager(loggerFactory.CreateLogger<SessionManager>());
        var activityLogger = new SessionActivityLogger(sessionManager, loggerFactory.CreateLogger<SessionActivityLogger>());
        
        var session = await sessionManager.CreateSessionAsync("Test Failed Activity Logging");
        activityLogger.SetCurrentSession(session);
        
        // Act
        await activityLogger.LogFailedActivityAsync(ActivityTypes.Error, "Test error", "Test error message");
        
        var timedActivityId = activityLogger.StartActivity(ActivityTypes.ToolCall, "Test tool call");
        await activityLogger.FailActivityAsync(timedActivityId, "Tool execution failed");
        
        // Act - get activities
        var activities = await activityLogger.GetSessionActivitiesAsync();
        
        // Assert
        Assert.Equal(2, activities.Count);
        
        var errorActivity = activities[0];
        Assert.Equal(ActivityTypes.Error, errorActivity.ActivityType);
        Assert.Equal("Test error", errorActivity.Description);
        Assert.False(errorActivity.Success);
        Assert.Equal("Test error message", errorActivity.ErrorMessage);
        
        var failedToolActivity = activities[1];
        Assert.Equal(ActivityTypes.ToolCall, failedToolActivity.ActivityType);
        Assert.Equal("Test tool call", failedToolActivity.Description);
        Assert.False(failedToolActivity.Success);
        Assert.Equal("Tool execution failed", failedToolActivity.ErrorMessage);
        Assert.True(failedToolActivity.DurationMs.HasValue);
    }
    
    [Fact]
    public async Task SessionActivityLogger_PersistsActivitiesInSession()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var sessionManager = new SessionManager(loggerFactory.CreateLogger<SessionManager>());
        var activityLogger = new SessionActivityLogger(sessionManager, loggerFactory.CreateLogger<SessionActivityLogger>());
        
        var session = await sessionManager.CreateSessionAsync("Test Activity Persistence");
        activityLogger.SetCurrentSession(session);
        
        // Act - log some activities
        await activityLogger.LogActivityAsync(ActivityTypes.SessionStart, "Session started");
        await activityLogger.LogActivityAsync(ActivityTypes.TaskPlanning, "Planning task");
        
        // Retrieve activities from the new activity log table
        var activities = await sessionManager.GetSessionActivitiesAsync(session.SessionId);
        Assert.Equal(2, activities.Count);
        
        var sessionStartActivity = activities[0];
        Assert.Equal(ActivityTypes.SessionStart, sessionStartActivity.ActivityType);
        Assert.Equal("Session started", sessionStartActivity.Description);
        
        var planningActivity = activities[1];
        Assert.Equal(ActivityTypes.TaskPlanning, planningActivity.ActivityType);
        Assert.Equal("Planning task", planningActivity.Description);
    }
    
    [Fact]
    public async Task SessionActivityLogger_LogsDetailedDataCorrectly()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var sessionManager = new SessionManager(loggerFactory.CreateLogger<SessionManager>());
        var activityLogger = new SessionActivityLogger(sessionManager, loggerFactory.CreateLogger<SessionActivityLogger>());
        
        var session = await sessionManager.CreateSessionAsync("Test Enhanced Activity Logging");
        activityLogger.SetCurrentSession(session);
        
        // Act - log activities with detailed data
        var toolCallData = new 
        {
            ToolName = "test_tool",
            Arguments = new { param1 = "value1", param2 = 42 },
            FullInput = new
            {
                ToolName = "test_tool",
                ArgumentCount = 2,
                ArgumentKeys = new[] { "param1", "param2" },
                ArgumentValues = new Dictionary<string, string> { { "param1", "value1" }, { "param2", "42" } }
            }
        };
        
        await activityLogger.LogActivityAsync(ActivityTypes.ToolCall, "Executing enhanced tool", toolCallData);
        
        var toolResultData = new
        {
            ToolName = "test_tool",
            Success = true,
            ResultLength = 150,
            HasContent = true,
            FullOutput = new
            {
                ResultText = "This is a test tool result with comprehensive data",
                ContentBlocks = new[] { new { Type = "TextContentBlock", Content = "Test content" } },
                IsError = false,
                Metadata = "{\"key\": \"value\"}"
            }
        };
        
        await activityLogger.LogActivityAsync(ActivityTypes.ToolResult, "Tool result with details", toolResultData);
        
        // Fetch activities from the new activity log table
        var activities = await sessionManager.GetSessionActivitiesAsync(session.SessionId);
        Assert.Equal(2, activities.Count);
        
        var toolCallActivity = activities[0];
        Assert.Equal(ActivityTypes.ToolCall, toolCallActivity.ActivityType);
        Assert.Equal("Executing enhanced tool", toolCallActivity.Description);
        Assert.Contains("test_tool", toolCallActivity.Data);
        Assert.Contains("FullInput", toolCallActivity.Data);
        Assert.Contains("ArgumentKeys", toolCallActivity.Data);
        
        var toolResultActivity = activities[1];
        Assert.Equal(ActivityTypes.ToolResult, toolResultActivity.ActivityType);
        Assert.Equal("Tool result with details", toolResultActivity.Description);
        Assert.Contains("FullOutput", toolResultActivity.Data);
        Assert.Contains("ResultText", toolResultActivity.Data);
        Assert.Contains("ContentBlocks", toolResultActivity.Data);
    }
    
    [Fact]
    public void SessionActivity_CreateWithDetailedData_HandlesLargeData()
    {
        // Arrange
        var largeData = new
        {
            LargeText = new string('X', 100000), // 100KB of data
            AdditionalInfo = "This should be truncated"
        };
        
        // Act
        var activity = SessionActivity.CreateWithDetailedData("Test", "Large data test", largeData, 1000);
        
        // Assert
        Assert.NotNull(activity.Data);
        Assert.True(activity.Data.Length <= 1200); // Should be truncated with some overhead for metadata
        Assert.Contains("truncated", activity.Data);
        Assert.Contains("originalSize", activity.Data);
    }
    
    [Fact]
    public void SessionActivity_TruncateString_WorksCorrectly()
    {
        // Arrange
        var longString = new string('A', 10000);
        var shortString = "Short text";
        
        // Act & Assert
        var truncatedLong = SessionActivity.TruncateString(longString, 100);
        Assert.True(truncatedLong.Length <= 100);
        Assert.Contains("[TRUNCATED]", truncatedLong);
        
        var truncatedShort = SessionActivity.TruncateString(shortString, 100);
        Assert.Equal(shortString, truncatedShort);
        
        var truncatedNull = SessionActivity.TruncateString(null, 100);
        Assert.Equal(string.Empty, truncatedNull);
    }
    
    [Fact]
    public void ActivityLoggingConfig_HasCorrectDefaults()
    {
        // Arrange & Act
        var config = new ActivityLoggingConfig();
        
        // Assert
        Assert.True(config.VerboseOpenAI);
        Assert.True(config.VerboseTools);
        Assert.Equal(50000, config.MaxDataSize);
        Assert.Equal(5000, config.MaxStringSize);
        Assert.Equal(50, config.MaxMessagesInLog);
    }
    
    [Fact]
    public void EnhancedLogging_DemonstratesComprehensiveDataCapture()
    {
        // This test demonstrates the enhanced logging capabilities
        // showing the difference between basic and enhanced activity logging
        
        // Arrange - Create sample data like what would be used in real operations
        var basicToolData = new { ToolName = "test_tool", Success = true };
        
        var enhancedToolData = new {
            ToolName = "github_get_pull_request_files",
            Arguments = new { owner = "microsoft", repo = "mssql-python", pullNumber = 104 },
            FullInput = new {
                ToolName = "github_get_pull_request_files",
                ArgumentCount = 3,
                ArgumentKeys = new[] { "owner", "repo", "pullNumber" },
                ArgumentValues = new Dictionary<string, string> {
                    { "owner", "microsoft" },
                    { "repo", "mssql-python" },
                    { "pullNumber", "104" }
                }
            }
        };
        
        var enhancedResultData = new {
            ToolName = "github_get_pull_request_files",
            Success = true,
            ResultLength = 337,
            HasContent = true,
            FullOutput = new {
                ResultText = "Files changed in PR: src/main.py, tests/test_main.py",
                ContentBlocks = new[] { 
                    new { Type = "TextContentBlock", Content = "File list retrieved successfully" } 
                },
                IsError = false,
                Metadata = "{\"files_count\": 2}"
            }
        };
        
        // Act - Create activities with different levels of detail
        var basicActivity = SessionActivity.Create(ActivityTypes.ToolCall, "Basic tool call", basicToolData);
        var enhancedCallActivity = SessionActivity.Create(ActivityTypes.ToolCall, "Enhanced tool call", enhancedToolData);
        var enhancedResultActivity = SessionActivity.Create(ActivityTypes.ToolResult, "Enhanced tool result", enhancedResultData);
        
        // Assert - Verify enhanced logging captures more comprehensive information
        Assert.Contains("ToolName", basicActivity.Data);
        Assert.DoesNotContain("FullInput", basicActivity.Data);
        
        Assert.Contains("ToolName", enhancedCallActivity.Data);
        Assert.Contains("FullInput", enhancedCallActivity.Data);
        Assert.Contains("ArgumentKeys", enhancedCallActivity.Data);
        Assert.Contains("ArgumentValues", enhancedCallActivity.Data);
        
        Assert.Contains("FullOutput", enhancedResultActivity.Data);
        Assert.Contains("ResultText", enhancedResultActivity.Data);
        Assert.Contains("ContentBlocks", enhancedResultActivity.Data);
        Assert.Contains("Metadata", enhancedResultActivity.Data);
        
        // Demonstrate the enhanced data structure provides comprehensive audit trail
        var callData = JsonSerializer.Deserialize<Dictionary<string, object>>(enhancedCallActivity.Data);
        Assert.NotNull(callData);
        
        var resultData = JsonSerializer.Deserialize<Dictionary<string, object>>(enhancedResultActivity.Data);
        Assert.NotNull(resultData);
        
        // The enhanced logging now provides the detailed data requested in the issue
        // allowing users to see "virtually every step in the operation and the data 
        // that was gathered/sent in each step"
    }
}