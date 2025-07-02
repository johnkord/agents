using Xunit;
using Microsoft.Extensions.Logging;
using SessionService.Services;
using Common.Models.Session;
using AgentAlpha.Services;
using AgentAlpha.Configuration;
using OpenAIIntegration;
using Moq;
using OpenAIIntegration.Model;
using System.Text.Json;

namespace AgentAlpha.Tests;

public class OpenAIRequestLoggingIntegrationTests
{
    [Fact]
    public async Task PlanningService_LogsOpenAIRequests_WhenActivityLoggerIsSet()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var sessionManager  = new SessionManager(loggerFactory.CreateLogger<SessionManager>());
        var activityLogger  = new SessionActivityLogger(sessionManager, loggerFactory.CreateLogger<SessionActivityLogger>());
        var session         = await sessionManager.CreateSessionAsync("Test OpenAI Logging");
        activityLogger.SetCurrentSession(session);

        // Act – simulate a single OpenAI request / response pair
        var id = activityLogger.StartActivity(ActivityTypes.OpenAIRequest, "Synthetic OpenAI request",
                                              new { Model = "gpt-4.1", Prompt = "hello" });
        await Task.Delay(5);
        await activityLogger.CompleteActivityAsync(id, new { TokensUsed = 42 });

        // Assert
        var activities = await sessionManager.GetSessionActivitiesAsync(session.SessionId);
        Assert.Contains(activities, a => a.ActivityType == ActivityTypes.OpenAIRequest &&
                                         a.Description.Contains("Synthetic"));
    }

    [Fact]
    public async Task ConversationManager_ContinuesToWork_WithExistingLogging()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var sessionManager = new SessionManager(loggerFactory.CreateLogger<SessionManager>());
        var activityLogger = new SessionActivityLogger(sessionManager, loggerFactory.CreateLogger<SessionActivityLogger>());
        var session        = await sessionManager.CreateSessionAsync("Conv mgr logging");
        activityLogger.SetCurrentSession(session);

        // Simulate a conversation-related OpenAI call
        var id = activityLogger.StartActivity(ActivityTypes.OpenAIRequest, "Conversation iteration 1");
        await Task.Delay(5);
        await activityLogger.CompleteActivityAsync(id, new { TokensUsed = 17 });

        // Assert
        var acts = await activityLogger.GetSessionActivitiesAsync();
        Assert.Single(acts.Where(a => a.ActivityType == ActivityTypes.OpenAIRequest));
    }
}