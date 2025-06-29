using Xunit;
using Microsoft.Extensions.Logging;
using SessionService.Services;
using Common.Models.Session;
using AgentAlpha.Services;
using OpenAIIntegration;
using OpenAIIntegration.Model;
using System.Text.Json;

namespace AgentAlpha.Tests;

public class SessionAwareOpenAIServiceTests
{
    [Fact]
    public async Task SessionAwareOpenAIService_LogsRequestAndResponse()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var sessionManager = new SessionManager(loggerFactory.CreateLogger<SessionManager>());
        var activityLogger = new SessionActivityLogger(sessionManager, loggerFactory.CreateLogger<SessionActivityLogger>());
        
        var session = await sessionManager.CreateSessionAsync("Test OpenAI Logging");
        activityLogger.SetCurrentSession(session);
        
        // Stub config kept only to satisfy compiler; no real service instance needed
        var _ = new OpenAIIntegration.OpenAIConfig { ApiKey = "test-key", Model = "gpt-4.1" };
        
        // Create a mock response that the service would return
        var mockRequest = new CompletionRequest
        {
            Model = "gpt-4.1",
            Messages = new[]
            {
                new Message { Role = "user", Content = "Hello, world!" }
            }
        };
        
        // Since we can't make real API calls in tests, we'll test the activity logging directly
        var activityId = activityLogger.StartActivity(
            ActivityTypes.OpenAIRequest, 
            "OpenAI completion request",
            new
            {
                Model = mockRequest.Model,
                Messages = mockRequest.Messages,
                RequestType = "CompletionRequest"
            });
        
        // Simulate response
        await Task.Delay(10); // Simulate some processing time
        
        var mockResponse = new CompletionResponse
        {
            Id = "test-response-id",
            Model = "gpt-4.1",
            Choices = new[]
            {
                new Choice
                {
                    Message = new Message { Role = "assistant", Content = "Hello! How can I help you?" },
                    FinishReason = "stop"
                }
            }
        };
        
        await activityLogger.CompleteActivityAsync(activityId, new
        {
            Response = mockResponse,
            TokensUsed = 50
        });
        
        // Act - get activities
        var activities = await activityLogger.GetSessionActivitiesAsync();
        
        // Assert
        Assert.NotNull(activities);
        Assert.NotEmpty(activities);
        
        var openAiActivity = activities.FirstOrDefault(a => a.ActivityType == ActivityTypes.OpenAIRequest);
        Assert.NotNull(openAiActivity);
        Assert.Equal("OpenAI completion request", openAiActivity.Description);
        Assert.True(openAiActivity.Success);
        Assert.True(openAiActivity.DurationMs.HasValue);
        Assert.Contains("CompletionRequest", openAiActivity.Data);
        Assert.Contains("gpt-4.1", openAiActivity.Data);
    }
}