using Xunit;
using Microsoft.Extensions.Logging;
using AgentAlpha.Services;
using AgentAlpha.Models;

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
        
        // Retrieve the session again and check activities are persisted
        var retrievedSession = await sessionManager.GetSessionAsync(session.SessionId);
        
        // Assert
        Assert.NotNull(retrievedSession);
        var activities = retrievedSession.GetActivityLog();
        Assert.Equal(2, activities.Count);
        
        var sessionStartActivity = activities[0];
        Assert.Equal(ActivityTypes.SessionStart, sessionStartActivity.ActivityType);
        Assert.Equal("Session started", sessionStartActivity.Description);
        
        var planningActivity = activities[1];
        Assert.Equal(ActivityTypes.TaskPlanning, planningActivity.ActivityType);
        Assert.Equal("Planning task", planningActivity.Description);
    }
}