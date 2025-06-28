using Xunit;
using Microsoft.Extensions.Logging;
using AgentAlpha.Services;
using AgentAlpha.Configuration;
using OpenAIIntegration;
using SessionService.Services;
using Common.Models.Session;
using ModelContextProtocol.Client;
using AgentAlpha.Models;

namespace AgentAlpha.Tests;

/// <summary>
/// Integration tests to verify OpenAI request logging works across all services
/// </summary>
public class OpenAIRequestLoggingIntegrationTests
{
    [Fact]
    public async Task ToolSelector_LogsOpenAIRequests_WhenActivityLoggerIsSet()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var sessionManager = new SessionManager(loggerFactory.CreateLogger<SessionManager>());
        var activityLogger = new SessionActivityLogger(sessionManager, loggerFactory.CreateLogger<SessionActivityLogger>());
        
        var session = await sessionManager.CreateSessionAsync("Test ToolSelector Logging");
        activityLogger.SetCurrentSession(session);
        
        var mockOpenAIService = new SessionAwareOpenAIService(
            new MockOpenAIResponsesService(),
            loggerFactory.CreateLogger<SessionAwareOpenAIService>());
        
        var config = new AgentConfiguration
        {
            Model = "gpt-4o",
            MaxConversationMessages = 50,
            OpenAiApiKey = "test-key"
        };
        
        var toolSelector = new ToolSelector(
            mockOpenAIService,
            null!, // ToolManager not needed for this test
            loggerFactory.CreateLogger<ToolSelector>(),
            config);
        
        // Set the activity logger
        toolSelector.SetActivityLogger(activityLogger);
        
        // Act
        try
        {
            await toolSelector.SelectToolsForTaskAsync("Test task", new List<McpClientTool>(), 5);
        }
        catch
        {
            // We expect this to fail due to null ToolManager, but OpenAI logging should still work
        }
        
        // Assert
        var activities = await activityLogger.GetSessionActivitiesAsync();
        
        var openAIRequestActivities = activities.Where(a => a.ActivityType == ActivityTypes.OpenAIRequest).ToList();
        var openAIResponseActivities = activities.Where(a => a.ActivityType == ActivityTypes.OpenAIResponse).ToList();
        
        // Should have at least one OpenAI request/response pair
        Assert.NotEmpty(openAIRequestActivities);
        Assert.NotEmpty(openAIResponseActivities);
        
        // Verify the request activity has expected data
        var requestActivity = openAIRequestActivities.First();
        Assert.Contains("Source", requestActivity.Data);
        Assert.Contains("SessionAwareOpenAIService", requestActivity.Data);
        Assert.Contains("gpt-4o", requestActivity.Data);
    }
    
    [Fact]
    public async Task PlanningService_LogsOpenAIRequests_WhenActivityLoggerIsSet()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var sessionManager = new SessionManager(loggerFactory.CreateLogger<SessionManager>());
        var activityLogger = new SessionActivityLogger(sessionManager, loggerFactory.CreateLogger<SessionActivityLogger>());
        
        var session = await sessionManager.CreateSessionAsync("Test PlanningService Logging");
        activityLogger.SetCurrentSession(session);
        
        var mockOpenAIService = new SessionAwareOpenAIService(
            new MockOpenAIResponsesService(),
            loggerFactory.CreateLogger<SessionAwareOpenAIService>());
        
        var config = new AgentConfiguration
        {
            Model = "gpt-4o",
            MaxConversationMessages = 50,
            OpenAiApiKey = "test-key"
        };
        
        var planningService = new PlanningService(
            mockOpenAIService,
            loggerFactory.CreateLogger<PlanningService>(),
            config);
        
        // Set the activity logger
        planningService.SetActivityLogger(activityLogger);
        
        // Act
        var plan = await planningService.CreatePlanAsync("Test planning task", new List<McpClientTool>());
        
        // Assert
        Assert.NotNull(plan);
        
        var activities = await activityLogger.GetSessionActivitiesAsync();
        
        var openAIRequestActivities = activities.Where(a => a.ActivityType == ActivityTypes.OpenAIRequest).ToList();
        var openAIResponseActivities = activities.Where(a => a.ActivityType == ActivityTypes.OpenAIResponse).ToList();
        
        // Should have at least one OpenAI request/response pair
        Assert.NotEmpty(openAIRequestActivities);
        Assert.NotEmpty(openAIResponseActivities);
        
        // Verify the request activity has expected data
        var requestActivity = openAIRequestActivities.First();
        Assert.Contains("Source", requestActivity.Data);
        Assert.Contains("SessionAwareOpenAIService", requestActivity.Data);
        Assert.Contains("gpt-4o", requestActivity.Data);
    }
    
    [Fact]
    public async Task ConversationManager_ContinuesToWork_WithExistingLogging()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var sessionManager = new SessionManager(loggerFactory.CreateLogger<SessionManager>());
        var activityLogger = new SessionActivityLogger(sessionManager, loggerFactory.CreateLogger<SessionActivityLogger>());
        
        var session = await sessionManager.CreateSessionAsync("Test ConversationManager Logging");
        activityLogger.SetCurrentSession(session);
        
        // ConversationManager still uses the base service and handles its own logging
        var mockOpenAIService = new MockOpenAIResponsesService();
        
        var config = new AgentConfiguration
        {
            Model = "gpt-4o",
            MaxConversationMessages = 50,
            OpenAiApiKey = "test-key",
            ActivityLogging = new ActivityLoggingConfig
            {
                VerboseOpenAI = true,
                VerboseTools = true
            }
        };
        
        var conversationManager = new ConversationManager(
            mockOpenAIService,
            loggerFactory.CreateLogger<ConversationManager>(),
            config,
            activityLogger);
        
        conversationManager.InitializeConversation("Test system prompt", "Test user task");
        
        // Act
        var response = await conversationManager.ProcessIterationAsync(new OpenAIIntegration.Model.ToolDefinition[0]);
        
        // Assert
        Assert.NotNull(response);
        
        var activities = await activityLogger.GetSessionActivitiesAsync();
        
        var openAIRequestActivities = activities.Where(a => a.ActivityType == ActivityTypes.OpenAIRequest).ToList();
        var openAIResponseActivities = activities.Where(a => a.ActivityType == ActivityTypes.OpenAIResponse).ToList();
        
        // ConversationManager should still log its OpenAI requests as before
        Assert.NotEmpty(openAIRequestActivities);
        Assert.NotEmpty(openAIResponseActivities);
        
        // ConversationManager logging should include more detailed information
        var requestActivity = openAIRequestActivities.First();
        Assert.Contains("gpt-4o", requestActivity.Data);
        // ConversationManager includes FullRequest in verbose mode
        Assert.Contains("FullRequest", requestActivity.Data);
    }
}

/// <summary>
/// Enhanced mock that returns structured responses for PlanningService
/// </summary>
public class MockOpenAIResponsesServiceForPlanning : MockOpenAIResponsesService
{
    public MockOpenAIResponsesServiceForPlanning() : base(false) { }
    
    public new Task<OpenAIIntegration.Model.ResponsesCreateResponse> CreateResponseAsync(
        OpenAIIntegration.Model.ResponsesCreateRequest request, 
        CancellationToken cancellationToken = default)
    {
        // Return a mock planning response that PlanningService can parse
        var mockPlanResponse = new
        {
            goal = "Test planning task",
            approach = "Break down the task systematically",
            steps = new[]
            {
                new { stepNumber = 1, description = "Analyze the task requirements", toolsNeeded = new[] { "analysis_tool" } },
                new { stepNumber = 2, description = "Execute the task", toolsNeeded = new[] { "execution_tool" } },
                new { stepNumber = 3, description = "Verify completion", toolsNeeded = new[] { "verification_tool" } }
            }
        };
        
        var response = new OpenAIIntegration.Model.ResponsesCreateResponse
        {
            Output = new OpenAIIntegration.Model.ResponseOutputItem[]
            {
                new OpenAIIntegration.Model.OutputMessage
                {
                    Role = "assistant",
                    Content = System.Text.Json.JsonSerializer.Serialize(mockPlanResponse)
                }
            },
            Usage = new OpenAIIntegration.Model.ResponseUsage
            {
                InputTokens = 50,
                OutputTokens = 100,
                TotalTokens = 150
            }
        };
        
        return Task.FromResult(response);
    }
}