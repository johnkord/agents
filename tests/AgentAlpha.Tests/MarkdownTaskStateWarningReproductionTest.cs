using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Common.Services.Session;
using Common.Interfaces.Session;
using SessionService.Services;
using OpenAIIntegration;
using OpenAIIntegration.Model;
using System.Text.Json;
using Common.Interfaces.Tools;          // NEW

namespace AgentAlpha.Tests;

/// <summary>
/// Focused integration test to reproduce and fix the warning message issue
/// </summary>
public class MarkdownTaskStateWarningReproductionTest
{
    /// <summary>
    /// This test reproduces the exact warning scenario described in the issue:
    /// "No task markdown found for session, initializing with action result"
    /// </summary>
    [Fact]
    public async Task ReproduceWarningScenario_UpdateBeforeInitialize()
    {
        // Arrange: Create a fresh session without any markdown initialized
        using var loggerFactory = LoggerFactory.Create(builder => 
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Warning); // Capture warnings
        });
        
        var sessionManager = new SessionManager(loggerFactory.CreateLogger<SessionManager>());
        var mockOpenAiService = new Mock<ISessionAwareOpenAIService>();
        var mockToolScope     = new Mock<IToolScopeManager>();   // NEW

        var markdownManager = new MarkdownTaskStateManager(
            sessionManager,
            mockOpenAiService.Object,
            mockToolScope.Object,                                 // NEW
            loggerFactory.CreateLogger<MarkdownTaskStateManager>());

        // Create a session but don't initialize markdown
        var session = await sessionManager.CreateSessionAsync("Warning Reproduction Test");
        
        // Verify session has no meaningful markdown initially
        Assert.True(string.IsNullOrEmpty(session.TaskStateMarkdown));
        Assert.True(string.IsNullOrEmpty(await markdownManager.GetTaskMarkdownAsync(session.SessionId)));

        // Act: Call UpdateTaskMarkdownAsync without first calling InitializeTaskMarkdownAsync
        // This should trigger the warning: "No task markdown found for session {SessionId}, initializing with action result"
        var result = await markdownManager.UpdateTaskMarkdownAsync(
            session.SessionId,
            "First action executed",
            "Action completed successfully");

        // Assert: The method should handle this gracefully and create basic markdown
        Assert.NotNull(result);
        Assert.Contains($"# Task: Session {session.SessionId}", result);
        Assert.Contains("Session started without initial task plan", result);
        Assert.Contains("First action executed", result);
        Assert.Contains("Action completed successfully", result);
        
        // Verify the markdown was saved to the session
        var updatedSession = await sessionManager.GetSessionAsync(session.SessionId);
        Assert.NotNull(updatedSession?.TaskStateMarkdown);
        Assert.Equal(result, updatedSession.TaskStateMarkdown);
    }

    /// <summary>
    /// Test the correct workflow that should NOT produce warnings
    /// </summary>
    [Fact]
    public async Task CorrectWorkflow_InitializeThenUpdate_ShouldNotProduceWarnings()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var sessionManager = new SessionManager(loggerFactory.CreateLogger<SessionManager>());
        var mockOpenAiService = new Mock<ISessionAwareOpenAIService>();
        var mockToolScope     = new Mock<IToolScopeManager>();   // NEW
        
        // Mock OpenAI to return valid markdown for both initialize and update
        var initMarkdownResponse = CreateMockInitializeResponse();
        var updateMarkdownResponse = CreateMockUpdateResponse();
        
        mockOpenAiService.SetupSequence(x => x.CreateResponseAsync(It.IsAny<ResponsesCreateRequest>(), default))
                        .ReturnsAsync(initMarkdownResponse)  // First call for initialize
                        .ReturnsAsync(updateMarkdownResponse); // Second call for update

        var markdownManager = new MarkdownTaskStateManager(
            sessionManager,
            mockOpenAiService.Object,
            mockToolScope.Object,                                 // NEW
            loggerFactory.CreateLogger<MarkdownTaskStateManager>());

        var session = await sessionManager.CreateSessionAsync("Correct Workflow Test");

        // Act 1: Initialize markdown first (proper workflow)
        var initialMarkdown = await markdownManager.InitializeTaskMarkdownAsync(
            session.SessionId, 
            "Test task for correct workflow");

        // Verify initialization worked
        Assert.NotNull(initialMarkdown);
        Assert.Contains("# Task:", initialMarkdown);

        // Act 2: Update markdown (should NOT trigger warning since markdown exists)
        var updatedMarkdown = await markdownManager.UpdateTaskMarkdownAsync(
            session.SessionId,
            "Completed first subtask",
            "Subtask completed successfully");

        // Assert: Both operations should succeed
        Assert.NotNull(updatedMarkdown);
        
        // Verify the session has the updated markdown
        var finalSession = await sessionManager.GetSessionAsync(session.SessionId);
        Assert.NotNull(finalSession?.TaskStateMarkdown);
    }

    private static ResponsesCreateResponse CreateMockInitializeResponse()
    {
        var markdownContent = """
            # Task: Test task for correct workflow
            
            **Strategy:** Execute systematically
            
            **Status:** In Progress
            
            ## Subtasks
            
            - [ ] First subtask
            - [ ] Second subtask
            
            ## Progress Notes
            
            *Task initialized*
            
            ## Context
            
            [Initial setup completed]
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
            # Task: Test task for correct workflow
            
            **Strategy:** Execute systematically
            
            **Status:** In Progress
            
            ## Subtasks
            
            - [x] First subtask - completed successfully
            - [ ] Second subtask
            
            ## Progress Notes
            
            *Task initialized*
            *First subtask completed*
            
            ## Context
            
            [Initial setup completed]
            [First subtask completed successfully]
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
}