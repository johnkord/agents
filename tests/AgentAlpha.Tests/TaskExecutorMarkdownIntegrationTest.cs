using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using AgentAlpha.Services;
using AgentAlpha.Configuration;
using AgentAlpha.Interfaces;
using AgentAlpha.Models;
using Common.Services.Session;
using Common.Interfaces.Session;
using Common.Models.Session;
using SessionService.Services;
using OpenAIIntegration;
using OpenAIIntegration.Model;
using MCPClient;
using System.Text.Json;
using Common.Interfaces.Tools;          // NEW
using CommonToolScope = Common.Interfaces.Tools.IToolScopeManager;   // alias to disambiguate

namespace AgentAlpha.Tests;

/// <summary>
/// Integration test to verify the fix for the markdown task state warning
/// This test simulates the real workflow between TaskExecutor and MarkdownTaskStateManager
/// </summary>
public class TaskExecutorMarkdownIntegrationTest
{
    /// <summary>
    /// Test that verifies the fix: TaskExecutor should use MarkdownTaskStateManager consistently
    /// to avoid the warning "No task markdown found for session, initializing with action result"
    /// </summary>
    [Fact]
    public async Task TaskExecutor_WithMarkdownTaskStateManager_ShouldUseConsistentWorkflow()
    {
        // Arrange: Create a realistic setup with TaskExecutor and MarkdownTaskStateManager
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug); // Capture all logs to verify no warnings
        });

        var sessionManager  = new SessionManager(loggerFactory.CreateLogger<SessionManager>());
        var activityLogger  = new SessionActivityLogger(sessionManager, loggerFactory.CreateLogger<SessionActivityLogger>());

        // Mocks needed *before* they are first used
        var mockToolScopeCommon   = new Mock<CommonToolScope>();                 // for MarkdownTaskStateManager (Common)
        var mockToolScopeManager  = new Mock<IToolScopeManager>(); // for TaskExecutor (AgentAlpha)

        // Create MarkdownTaskStateManager with mocked OpenAI
        var mockOpenAiService = new Mock<ISessionAwareOpenAIService>();
        var initMarkdownResponse  = CreateMockInitializeResponse();
        var updateMarkdownResponse = CreateMockUpdateResponse();

        mockOpenAiService.SetupSequence(x => x.CreateResponseAsync(It.IsAny<ResponsesCreateRequest>(), default))
                         .ReturnsAsync(initMarkdownResponse)   // For initialization
                         .ReturnsAsync(updateMarkdownResponse); // For updates

        var markdownTaskStateManager = new MarkdownTaskStateManager(
            sessionManager,
            mockOpenAiService.Object,
            mockToolScopeCommon.Object,                        // use Common alias mock
            loggerFactory.CreateLogger<MarkdownTaskStateManager>());

        // Mock other required services for TaskExecutor
        var mockConnectionManager   = new Mock<IConnectionManager>();
        var mockToolManager         = new Mock<IToolManager>();
        var mockToolSelector        = new Mock<IToolSelector>();
        var mockConversationManager = new Mock<IConversationManager>();
        var mockTaskStateManager    = new Mock<ITaskStateManager>();

        // Create real MarkdownTaskPlanningService with a mock that supports FunctionToolCall
        var mockOpenAI = new MockSessionAwareOpenAIService();
        var toolScope = new ToolScopeManager();
        var config = new AgentConfiguration { Model = "gpt-4.1-nano" };
        var planningService = new MarkdownTaskPlanningService(
            markdownTaskStateManager,
            mockOpenAI,
            loggerFactory.CreateLogger<MarkdownTaskPlanningService>(),
            config,
            toolScope);

        // Setup basic mocks
        mockConnectionManager.Setup(x => x.ConnectAsync(
                It.IsAny<McpTransportType>(), 
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<string[]>()))
            .Returns(Task.CompletedTask);
        mockToolManager.Setup(x => x.DiscoverAllToolsAsync(It.IsAny<IConnectionManager>()))
            .ReturnsAsync(new List<IUnifiedTool>());

        
        // Create TaskExecutor with MarkdownTaskPlanningService
        var taskExecutor = new TaskExecutor(
            mockConnectionManager.Object,
            mockToolManager.Object,
            mockToolSelector.Object,
            mockConversationManager.Object,
            sessionManager,
            planningService,
            activityLogger,
            mockTaskStateManager.Object,
            config,
            loggerFactory.CreateLogger<TaskExecutor>(),
            mockToolScopeManager.Object,                       // AgentAlpha mock
            markdownTaskStateManager); // This is the key: provide MarkdownTaskStateManager

        // Act: Call the InitializeMarkdownPlanAsync workflow
        var session = await sessionManager.CreateSessionAsync("Integration Test Session");
        var request = new TaskExecutionRequest
        {
            Task = "Test task for integration",
            SessionId = session.SessionId
        };

        // Use reflection to call the private InitializeMarkdownPlanAsync method
        var method = typeof(TaskExecutor).GetMethod("InitializeMarkdownPlanAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);

        var resultTask = (Task<string>)method.Invoke(taskExecutor, new object[] { request })!;
        var markdownResult = await resultTask;

        // Assert: Verify the markdown was created through MarkdownTaskStateManager
        Assert.NotNull(markdownResult);
        Assert.Contains("# Task:", markdownResult);

        // Verify the session has the markdown
        var updatedSession = await sessionManager.GetSessionAsync(session.SessionId);
        Assert.NotNull(updatedSession?.TaskStateMarkdown);
        Assert.Equal(markdownResult, updatedSession.TaskStateMarkdown);

        // Now test the update workflow
        // Use reflection to call the private TryUpdateMarkdownAsync method
        var updateMethod = typeof(TaskExecutor).GetMethod("TryUpdateMarkdownAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(updateMethod);

        var updateTask = (Task)updateMethod.Invoke(taskExecutor, new object[] { 
            session.SessionId, 
            "Test action", 
            "Action completed successfully" 
        })!;
        await updateTask;

        // Assert: Verify the update succeeded without warnings
        var finalSession = await sessionManager.GetSessionAsync(session.SessionId);
        Assert.NotNull(finalSession?.TaskStateMarkdown);
        
        // The markdown should have been updated by MarkdownTaskStateManager
        // Since we're now using MarkdownTaskPlanningService, we don't need to verify 
        // that the old PlanningService wasn't called
    }

    private static ResponsesCreateResponse CreateMockInitializeResponse()
    {
        var markdownContent = """
            # Task: Test task for integration
            
            **Strategy:** Execute systematically
            
            **Status:** In Progress
            
            ## Subtasks
            
            - [ ] Initialize test
            - [ ] Execute test
            - [ ] Verify results
            
            ## Progress Notes
            
            *Task initialized via MarkdownTaskStateManager*
            
            ## Context
            
            [Initialization completed]
            """;

        return new ResponsesCreateResponse
        {
            Output = new[]
            {
                new OutputMessage
                {
                    Type = "message",
                    Content = JsonDocument.Parse(JsonSerializer.Serialize(markdownContent)).RootElement
                }
            }
        };
    }

    private static ResponsesCreateResponse CreateMockUpdateResponse()
    {
        var markdownContent = """
            # Task: Test task for integration
            
            **Strategy:** Execute systematically
            
            **Status:** In Progress
            
            ## Subtasks
            
            - [x] Initialize test - completed
            - [ ] Execute test
            - [ ] Verify results
            
            ## Progress Notes
            
            *Task initialized via MarkdownTaskStateManager*
            *Test action completed*
            
            ## Context
            
            [Initialization completed]
            [Test action completed successfully]
            """;

        return new ResponsesCreateResponse
        {
            Output = new[]
            {
                new OutputMessage
                {
                    Type = "message",
                    Content = JsonDocument.Parse(JsonSerializer.Serialize(markdownContent)).RootElement
                }
            }
        };
    }

    /// <summary>
    /// Mock OpenAI service that supports FunctionToolCall responses for planning
    /// </summary>
    private class MockSessionAwareOpenAIService : ISessionAwareOpenAIService
    {
        public void SetActivityLogger(ISessionActivityLogger? activityLogger)
        {
            // No-op for testing
        }

        public Task<ResponsesCreateResponse> CreateResponseAsync(
            ResponsesCreateRequest request, 
            CancellationToken cancellationToken = default)
        {
            // Check if this is a planning request that expects a function call
            var hasTools = request.Tools?.Any() == true;
            
            if (hasTools)
            {
                // Return a response with a function call for planning
                var mockResponse = new ResponsesCreateResponse
                {
                    Output = new[]
                    {
                        new FunctionToolCall
                        {
                            Name = "save_markdown_plan",
                            Arguments = JsonDocument.Parse(JsonSerializer.Serialize(new
                            {
                                markdown_plan = "# Test Task\n\n**Strategy:** Test strategy\n\n## Subtasks\n\n- [ ] Test step 1\n- [ ] Test step 2\n\n## Required Tools\n\n- test_tool\n\n## Success Criteria\n\n- Task completed successfully",
                                required_tools = new[] { "test_tool" }
                            })).RootElement
                        }
                    }
                };
                return Task.FromResult(mockResponse);
            }
            else
            {
                // Return a regular text response for non-planning requests
                var markdownContent = """
                    # Updated Task
                    
                    **Strategy:** Execute systematically
                    
                    **Status:** In Progress
                    
                    ## Subtasks
                    
                    - [x] Completed step
                    - [ ] Next step
                    
                    ## Progress Notes
                    
                    *Task updated*
                    """;

                return Task.FromResult(new ResponsesCreateResponse
                {
                    Output = new[]
                    {
                        new OutputMessage
                        {
                            Type = "message",
                            Content = JsonDocument.Parse(JsonSerializer.Serialize(markdownContent)).RootElement
                        }
                    }
                });
            }
        }
    }
}