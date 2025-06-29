using Xunit;
using Microsoft.Extensions.Logging;
using OpenAIIntegration;
using OpenAIIntegration.Model;
using Common.Models.Session;
using Common.Interfaces.Session;
using System.Text.Json;
using System.Linq;
using System.Threading.Tasks;

namespace Common.Tests;

/// <summary>
/// Tests for enhanced logging functionality in SessionAwareOpenAIService
/// </summary>
public class SessionAwareOpenAIServiceEnhancedLoggingTests
{
    [Fact]
    public async Task CreateResponseAsync_WithEnhancedLogging_LogsFullRequestData()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var mockOpenAIService = new MockOpenAIResponsesService();
        var mockActivityLogger = new MockSessionActivityLogger();
        
        var service = new SessionAwareOpenAIService(
            mockOpenAIService,
            loggerFactory.CreateLogger<SessionAwareOpenAIService>())
        {
            EnableEnhancedLogging = true
        };
        
        service.SetActivityLogger(mockActivityLogger);
        
        var request = new ResponsesCreateRequest
        {
            Model = "gpt-4o",
            Input = new object[]
            {
                new { role = "system", content = "You are a helpful assistant." },
                new { role = "user", content = "Hello, can you help me?" }
            },
            Tools = new ToolDefinition[]
            {
                new() { Name = "test_tool", Description = "A test tool", Parameters = new { type = "object" } }
            },
            ToolChoice = "auto",
            Temperature = 0.7,
            Instructions = "Be helpful and concise"
        };
        
        // Act
        await service.CreateResponseAsync(request);
        
        // Assert
        var requestActivities = mockActivityLogger.GetActivitiesByType(ActivityTypes.OpenAIRequest);
        Assert.Single(requestActivities);
        
        var requestActivity = requestActivities.First();
        Assert.Contains("SessionAwareOpenAIService", requestActivity.Description);
        
        // Parse the logged data
        var requestData = JsonSerializer.Deserialize<JsonElement>(requestActivity.Data);
        
        // Verify basic data is logged
        Assert.Equal("gpt-4o", requestData.GetProperty("Model").GetString());
        Assert.Equal(2, requestData.GetProperty("InputCount").GetInt32());
        Assert.Equal(1, requestData.GetProperty("ToolCount").GetInt32());
        
        // Verify enhanced data is logged
        Assert.True(requestData.TryGetProperty("FullRequest", out var fullRequest));
        
        // Check full request contains messages
        Assert.True(fullRequest.TryGetProperty("Messages", out var messages));
        Assert.Equal(2, messages.GetArrayLength());
        
        // Check full request contains tools with details
        Assert.True(fullRequest.TryGetProperty("Tools", out var tools));
        Assert.Equal(1, tools.GetArrayLength());
        var tool = tools[0];
        Assert.Equal("test_tool", tool.GetProperty("Name").GetString());
        Assert.Equal("A test tool", tool.GetProperty("Description").GetString());
        Assert.True(tool.TryGetProperty("Parameters", out _));
        
        // Check other request details
        Assert.Equal("auto", fullRequest.GetProperty("ToolChoice").GetString());
        Assert.Equal(0.7, fullRequest.GetProperty("Temperature").GetDouble());
        Assert.Equal("Be helpful and concise", fullRequest.GetProperty("Instructions").GetString());
    }
    
    [Fact]
    public async Task CreateResponseAsync_WithEnhancedLogging_LogsFullResponseData()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var mockOpenAIService = new MockOpenAIResponsesServiceWithStructuredResponse();
        var mockActivityLogger = new MockSessionActivityLogger();
        
        var service = new SessionAwareOpenAIService(
            mockOpenAIService,
            loggerFactory.CreateLogger<SessionAwareOpenAIService>())
        {
            EnableEnhancedLogging = true
        };
        
        service.SetActivityLogger(mockActivityLogger);
        
        var request = new ResponsesCreateRequest
        {
            Model = "gpt-4o",
            Input = new[] { new { role = "user", content = "Hello" } },
            Tools = new ToolDefinition[0]
        };
        
        // Act
        await service.CreateResponseAsync(request);
        
        // Assert
        var responseActivities = mockActivityLogger.GetActivitiesByType(ActivityTypes.OpenAIResponse);
        Assert.Single(responseActivities);
        
        var responseActivity = responseActivities.First();
        var responseData = JsonSerializer.Deserialize<JsonElement>(responseActivity.Data);
        
        // Verify enhanced response data is logged
        Assert.True(responseData.TryGetProperty("FullResponse", out var fullResponse));
        
        // Check output details
        Assert.True(fullResponse.TryGetProperty("Output", out var output));
        Assert.True(output.GetArrayLength() > 0);
        
        // Check usage information
        Assert.True(fullResponse.TryGetProperty("Usage", out var usage));
        Assert.Equal(10, usage.GetProperty("InputTokens").GetInt32());
        Assert.Equal(20, usage.GetProperty("OutputTokens").GetInt32());
        Assert.Equal(30, usage.GetProperty("TotalTokens").GetInt32());
        
        // Check response metadata
        Assert.True(fullResponse.TryGetProperty("Id", out _));
        Assert.True(fullResponse.TryGetProperty("Status", out _));
    }
    
    [Fact]
    public async Task CreateResponseAsync_WithBasicLogging_LogsOnlyBasicData()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var mockOpenAIService = new MockOpenAIResponsesService();
        var mockActivityLogger = new MockSessionActivityLogger();
        
        var service = new SessionAwareOpenAIService(
            mockOpenAIService,
            loggerFactory.CreateLogger<SessionAwareOpenAIService>())
        {
            EnableEnhancedLogging = false
        };
        
        service.SetActivityLogger(mockActivityLogger);
        
        var request = new ResponsesCreateRequest
        {
            Model = "gpt-4o",
            Input = new[] { new { role = "user", content = "Hello" } },
            Tools = new ToolDefinition[]
            {
                new() { Name = "test_tool", Description = "A test tool" }
            }
        };
        
        // Act
        await service.CreateResponseAsync(request);
        
        // Assert
        var requestActivities = mockActivityLogger.GetActivitiesByType(ActivityTypes.OpenAIRequest);
        Assert.Single(requestActivities);
        
        var requestActivity = requestActivities.First();
        var requestData = JsonSerializer.Deserialize<JsonElement>(requestActivity.Data);
        
        // Verify basic data is logged
        Assert.Equal("gpt-4o", requestData.GetProperty("Model").GetString());
        Assert.Equal(1, requestData.GetProperty("InputCount").GetInt32());
        Assert.Equal(1, requestData.GetProperty("ToolCount").GetInt32());
        
        // Verify enhanced data is NOT logged
        Assert.False(requestData.TryGetProperty("FullRequest", out _));
        
        // Check response logging too
        var responseActivities = mockActivityLogger.GetActivitiesByType(ActivityTypes.OpenAIResponse);
        Assert.Single(responseActivities);
        
        var responseActivity = responseActivities.First();
        var responseData = JsonSerializer.Deserialize<JsonElement>(responseActivity.Data);
        
        // Verify basic response data is logged but not full response
        Assert.True(responseData.TryGetProperty("Model", out _));
        Assert.True(responseData.TryGetProperty("Usage", out _));
        Assert.False(responseData.TryGetProperty("FullResponse", out _));
    }
    
    // -----------------------------------------------------------------
    // Minimal stub – captures activities so the tests can assert on them
    // -----------------------------------------------------------------
    private class MockSessionActivityLogger : ISessionActivityLogger
    {
        private readonly List<SessionActivity> _activities = new();
        private AgentSession? _session;

        public void SetCurrentSession(AgentSession s) => _session = s;
        public AgentSession? GetCurrentSession()      => _session;

        public string StartActivity(string t, string d, object? data = null)
        {
            var act = SessionActivity.Create(t, d, data);
            _activities.Add(act);
            return act.ActivityId;
        }

        public Task LogActivityAsync(string t, string d, object? data = null)
        {
            _activities.Add(SessionActivity.Create(t, d, data));
            return Task.CompletedTask;
        }

        public Task LogTimedActivityAsync(string t, string d, long ms, object? data = null)
        {
            var act = SessionActivity.Create(t, d, data);
            act.Complete(ms);
            _activities.Add(act);
            return Task.CompletedTask;
        }

        public Task LogFailedActivityAsync(string t, string d, string err, object? data = null)
        {
            var act = SessionActivity.Create(t, d, data);
            act.Fail(err);
            _activities.Add(act);
            return Task.CompletedTask;
        }

        public Task CompleteActivityAsync(string id, object? add = null) => Task.CompletedTask;
        public Task FailActivityAsync(string id, string err, object? add = null) => Task.CompletedTask;
        public Task<List<SessionActivity>> GetSessionActivitiesAsync() =>
            Task.FromResult(_activities.ToList());

        // Helper used by the tests
        public IEnumerable<SessionActivity> GetActivitiesByType(string type) =>
            _activities.Where(a => a.ActivityType == type);
    }
}

/// <summary>
/// Mock OpenAI service with structured response for testing
/// </summary>
public class MockOpenAIResponsesServiceWithStructuredResponse : IOpenAIResponsesService
{
    public Task<ResponsesCreateResponse> CreateResponseAsync(
        ResponsesCreateRequest request, 
        CancellationToken cancellationToken = default)
    {
        var response = new ResponsesCreateResponse
        {
            Id = "test-response-id",
            Status = "completed",
            Output = new ResponseOutputItem[]
            {
                new OutputMessage
                {
                    Role = "assistant",
                    Content = JsonDocument.Parse("\"Hello! How can I help you today?\"").RootElement
                }
            },
            Usage = new ResponseUsage
            {
                InputTokens = 10,
                OutputTokens = 20,
                TotalTokens = 30
            }
        };
        
        return Task.FromResult(response);
    }
}

/// <summary>
/// Basic mock OpenAI service for testing
/// </summary>
public class MockOpenAIResponsesService : IOpenAIResponsesService
{
    public Task<ResponsesCreateResponse> CreateResponseAsync(
        ResponsesCreateRequest request, 
        CancellationToken cancellationToken = default)
    {
        var response = new ResponsesCreateResponse
        {
            Output = new ResponseOutputItem[]
            {
                new OutputMessage
                {
                    Role = "assistant",
                    Content = JsonDocument.Parse("\"Mock response\"").RootElement
                }
            },
            Usage = new ResponseUsage
            {
                InputTokens = 50,
                OutputTokens = 100,
                TotalTokens = 150
            }
        };
        
        return Task.FromResult(response);
    }
}