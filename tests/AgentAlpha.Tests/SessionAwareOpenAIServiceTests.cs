using Xunit;
using Microsoft.Extensions.Logging;
using AgentAlpha.Services;
using OpenAIIntegration;
using OpenAIIntegration.Model;
using SessionService.Services;
using Common.Models.Session;
using System.Threading;
using System.Text.Json;

namespace AgentAlpha.Tests;

/// <summary>
/// Tests for SessionAwareOpenAIService to verify automatic activity logging
/// </summary>
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
        
        // Create a mock inner service
        var mockInnerService = new MockOpenAIResponsesService();
        var sessionAwareService = new SessionAwareOpenAIService(
            mockInnerService, 
            loggerFactory.CreateLogger<SessionAwareOpenAIService>());
        
        sessionAwareService.SetActivityLogger(activityLogger);
        
        var request = new ResponsesCreateRequest
        {
            Model = "gpt-4o",
            Input = new[] { new { role = "user", content = "Test message" } },
            Tools = null,
            ToolChoice = "auto"
        };
        
        // Act
        var response = await sessionAwareService.CreateResponseAsync(request);
        
        // Assert
        var activities = await activityLogger.GetSessionActivitiesAsync();
        
        // Should have 2 activities: request and response
        var requestActivity = activities.FirstOrDefault(a => a.ActivityType == ActivityTypes.OpenAIRequest);
        var responseActivity = activities.FirstOrDefault(a => a.ActivityType == ActivityTypes.OpenAIResponse);
        
        Assert.NotNull(requestActivity);
        Assert.NotNull(responseActivity);
        
        Assert.Equal("OpenAI API request via SessionAwareOpenAIService", requestActivity.Description);
        Assert.Equal("OpenAI API response via SessionAwareOpenAIService", responseActivity.Description);
        
        Assert.True(requestActivity.Success);
        Assert.True(responseActivity.Success);
        
        // Verify request data contains expected fields
        Assert.Contains("Model", requestActivity.Data);
        Assert.Contains("gpt-4o", requestActivity.Data);
        Assert.Contains("Source", requestActivity.Data);
        Assert.Contains("SessionAwareOpenAIService", requestActivity.Data);
        
        // Verify response data contains expected fields
        Assert.Contains("OutputItemCount", responseActivity.Data);
        Assert.Contains("Source", responseActivity.Data);
        Assert.Contains("SessionAwareOpenAIService", responseActivity.Data);
    }
    
    [Fact]
    public async Task SessionAwareOpenAIService_LogsErrorsCorrectly()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var sessionManager = new SessionManager(loggerFactory.CreateLogger<SessionManager>());
        var activityLogger = new SessionActivityLogger(sessionManager, loggerFactory.CreateLogger<SessionActivityLogger>());
        
        var session = await sessionManager.CreateSessionAsync("Test OpenAI Error Logging");
        activityLogger.SetCurrentSession(session);
        
        // Create a mock inner service that throws an exception
        var mockInnerService = new MockOpenAIResponsesService(shouldThrow: true);
        var sessionAwareService = new SessionAwareOpenAIService(
            mockInnerService, 
            loggerFactory.CreateLogger<SessionAwareOpenAIService>());
        
        sessionAwareService.SetActivityLogger(activityLogger);
        
        var request = new ResponsesCreateRequest
        {
            Model = "gpt-4o",
            Input = new[] { new { role = "user", content = "Test message" } },
            Tools = null,
            ToolChoice = "auto"
        };
        
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sessionAwareService.CreateResponseAsync(request));
        
        var activities = await activityLogger.GetSessionActivitiesAsync();
        
        // Should have 1 failed request activity
        var requestActivity = activities.FirstOrDefault(a => a.ActivityType == ActivityTypes.OpenAIRequest);
        
        Assert.NotNull(requestActivity);
        Assert.False(requestActivity.Success);
        Assert.Equal("Test error from mock service", requestActivity.ErrorMessage);
    }
    
    [Fact]
    public async Task SessionAwareOpenAIService_WorksWithoutActivityLogger()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        
        var mockInnerService = new MockOpenAIResponsesService();
        var sessionAwareService = new SessionAwareOpenAIService(
            mockInnerService, 
            loggerFactory.CreateLogger<SessionAwareOpenAIService>());
        
        // Don't set activity logger
        
        var request = new ResponsesCreateRequest
        {
            Model = "gpt-4o",
            Input = new[] { new { role = "user", content = "Test message" } },
            Tools = null,
            ToolChoice = "auto"
        };
        
        // Act - should not throw even without activity logger
        var response = await sessionAwareService.CreateResponseAsync(request);
        
        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.Output);
        Assert.Single(response.Output);
    }
}

/// <summary>
/// Mock implementation of IOpenAIResponsesService for testing
/// </summary>
public class MockOpenAIResponsesService : IOpenAIResponsesService
{
    private readonly bool _shouldThrow;
    
    public MockOpenAIResponsesService(bool shouldThrow = false)
    {
        _shouldThrow = shouldThrow;
    }
    
    public Task<ResponsesCreateResponse> CreateResponseAsync(
        ResponsesCreateRequest request, 
        CancellationToken cancellationToken = default)
    {
        if (_shouldThrow)
        {
            throw new InvalidOperationException("Test error from mock service");
        }
        
        var response = new ResponsesCreateResponse
        {
            Output = new ResponseOutputItem[]
            {
                new OutputMessage
                {
                    Role = "assistant",
                    Content = JsonDocument.Parse("\"Test response from mock service\"").RootElement
                }
            },
            Usage = new ResponseUsage
            {
                InputTokens = 10,
                OutputTokens = 5,
                TotalTokens = 15
            }
        };
        
        return Task.FromResult(response);
    }
}