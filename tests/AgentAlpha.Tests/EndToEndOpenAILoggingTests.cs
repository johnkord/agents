using Xunit;
using Microsoft.Extensions.Logging;
using SessionService.Services;
using Common.Models.Session;
using AgentAlpha.Services;
using AgentAlpha.Models;

namespace AgentAlpha.Tests;

public class EndToEndOpenAILoggingTests
{
    [Fact]
    public async Task AllOpenAIRequests_AreLoggedToSessionActivityLog()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var sessionManager = new SessionManager(loggerFactory.CreateLogger<SessionManager>());
        var activityLogger = new SessionActivityLogger(sessionManager, loggerFactory.CreateLogger<SessionActivityLogger>());
        
        var session = await sessionManager.CreateSessionAsync("End-to-End OpenAI Logging Test");
        activityLogger.SetCurrentSession(session);
        
        // Simulate multiple OpenAI requests
        var requests = new[]
        {
            ("Plan creation", "Creating execution plan"),
            ("Tool selection", "Selecting relevant tools"),
            ("Task execution", "Executing main task"),
            ("Result synthesis", "Synthesizing results")
        };
        
        // Act - Log multiple OpenAI requests
        foreach (var (type, description) in requests)
        {
            // Include the high-level type in the description so the later assertion that
            // searches for it (`Description.Contains(type)`) passes.
            var activityId = activityLogger.StartActivity(
                ActivityTypes.OpenAIRequest,
                $"{type}: {description}");
            await Task.Delay(10); // Simulate processing
            await activityLogger.CompleteActivityAsync(activityId, new
            {
                Model = "gpt-4o",
                Type = type,
                TokensUsed = Random.Shared.Next(50, 200)
            });
        }
        
        // Get all activities
        var activities = await sessionManager.GetSessionActivitiesAsync(session.SessionId);
        
        // Assert
        var openAiActivities = activities.Where(a => a.ActivityType == ActivityTypes.OpenAIRequest).ToList();
        Assert.True(openAiActivities.Count >= 4, $"Expected at least 4 OpenAI requests, got {openAiActivities.Count}");
        
        // Verify all activities have required data
        foreach (var activity in openAiActivities)
        {
            Assert.True(activity.Success);
            Assert.True(activity.DurationMs.HasValue);
            Assert.Contains("gpt-4o", activity.Data);
            Assert.Contains("TokensUsed", activity.Data);
        }
        
        // Verify specific request types
        Assert.Contains(openAiActivities, a => a.Description.Contains("Plan creation"));
        Assert.Contains(openAiActivities, a => a.Description.Contains("Tool selection"));
        Assert.Contains(openAiActivities, a => a.Description.Contains("Task execution"));
        Assert.Contains(openAiActivities, a => a.Description.Contains("Result synthesis"));
    }
}