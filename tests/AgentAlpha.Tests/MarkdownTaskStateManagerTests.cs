using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Common.Models.Session;
using Common.Services.Session;
using Common.Interfaces.Session;
using OpenAIIntegration;
using OpenAIIntegration.Model;
using SessionService.Services;
using System.Text.Json;

namespace AgentAlpha.Tests;

/// <summary>
/// Tests for markdown-based task state management
/// </summary>
public class MarkdownTaskStateManagerTests
{
    [Fact]
    public async Task InitializeTaskMarkdownAsync_CreatesValidMarkdown()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var sessionManager = new SessionManager(loggerFactory.CreateLogger<SessionManager>());
        var mockOpenAiService = new Mock<ISessionAwareOpenAIService>();
        var markdownManager = new MarkdownTaskStateManager(
            sessionManager,
            mockOpenAiService.Object,
            loggerFactory.CreateLogger<MarkdownTaskStateManager>());

        var session = await sessionManager.CreateSessionAsync("Test Markdown Session");
        var taskDescription = "Create a comprehensive project report";

        // Mock OpenAI response
        var mockResponse = new ResponsesCreateResponse
        {
            Output = new[]
            {
                new OutputMessage
                {
                    Type = "message",
                    Content = JsonDocument.Parse("\"# Task: Create a comprehensive project report\\n\\n**Strategy:** Gather data, analyze, and format\\n\\n**Status:** In Progress\\n\\n## Subtasks\\n\\n- [ ] Gather project data\\n- [ ] Analyze performance metrics\\n- [ ] Create summary document\\n\\n## Progress Notes\\n\\n*Task initialized*\\n\\n## Context\\n\\n[Initial setup completed]\"").RootElement
                }
            }
        };

        mockOpenAiService.Setup(x => x.CreateResponseAsync(It.IsAny<ResponsesCreateRequest>(), default))
                        .ReturnsAsync(mockResponse);

        // Act
        var markdown = await markdownManager.InitializeTaskMarkdownAsync(session.SessionId, taskDescription);

        // Assert
        Assert.NotNull(markdown);
        Assert.Contains("# Task:", markdown);
        Assert.Contains("Create a comprehensive project report", markdown);
        Assert.Contains("**Strategy:**", markdown);
        Assert.Contains("## Subtasks", markdown);
        Assert.Contains("- [ ]", markdown); // Should have unchecked items
        
        // Verify it was saved to session
        var updatedSession = await sessionManager.GetSessionAsync(session.SessionId);
        Assert.NotNull(updatedSession);
        Assert.NotEmpty(updatedSession.TaskStateMarkdown);
        Assert.Equal(markdown, updatedSession.TaskStateMarkdown);
    }

    [Fact]
    public async Task InitializeTaskMarkdownAsync_FallsBackOnOpenAIFailure()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var sessionManager = new SessionManager(loggerFactory.CreateLogger<SessionManager>());
        var mockOpenAiService = new Mock<ISessionAwareOpenAIService>();
        var markdownManager = new MarkdownTaskStateManager(
            sessionManager,
            mockOpenAiService.Object,
            loggerFactory.CreateLogger<MarkdownTaskStateManager>());

        var session = await sessionManager.CreateSessionAsync("Test Fallback Session");
        var taskDescription = "Simple test task";

        // Mock OpenAI failure - empty response
        var mockResponse = new ResponsesCreateResponse
        {
            Output = Array.Empty<ResponseOutputItem>()
        };

        mockOpenAiService.Setup(x => x.CreateResponseAsync(It.IsAny<ResponsesCreateRequest>(), default))
                        .ReturnsAsync(mockResponse);

        // Act
        var markdown = await markdownManager.InitializeTaskMarkdownAsync(session.SessionId, taskDescription);

        // Assert
        Assert.NotNull(markdown);
        Assert.Contains("# Task: Simple test task", markdown);
        Assert.Contains("**Strategy:**", markdown);
        Assert.Contains("## Subtasks", markdown);
        Assert.Contains("- [ ] Analyze the task requirements", markdown);
        
        // Verify fallback template was used and saved
        var updatedSession = await sessionManager.GetSessionAsync(session.SessionId);
        Assert.NotNull(updatedSession);
        Assert.NotEmpty(updatedSession.TaskStateMarkdown);
    }

    [Fact]
    public async Task GetCurrentSubtaskFromMarkdownAsync_ReturnsFirstUncheckedTask()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var sessionManager = new SessionManager(loggerFactory.CreateLogger<SessionManager>());
        var mockOpenAiService = new Mock<ISessionAwareOpenAIService>();
        var markdownManager = new MarkdownTaskStateManager(
            sessionManager,
            mockOpenAiService.Object,
            loggerFactory.CreateLogger<MarkdownTaskStateManager>());

        var session = await sessionManager.CreateSessionAsync("Test Current Subtask Session");
        
        // Set up markdown with mixed completed/uncompleted subtasks
        var testMarkdown = """
            # Task: Test Task
            
            **Strategy:** Execute sequentially
            
            ## Subtasks
            
            - [x] First task - completed
            - [ ] Second task - pending
            - [ ] Third task - also pending
            
            ## Context
            
            Some progress made.
            """;
            
        session.TaskStateMarkdown = testMarkdown;
        await sessionManager.SaveSessionAsync(session);

        // Act
        var currentSubtask = await markdownManager.GetCurrentSubtaskFromMarkdownAsync(session.SessionId);

        // Assert
        Assert.NotNull(currentSubtask);
        Assert.Equal("Second task - pending", currentSubtask.Description);
        Assert.False(currentSubtask.IsCompleted);
        Assert.Equal(2, currentSubtask.Priority); // Should be the second item in order
    }

    [Fact]
    public async Task GetCurrentSubtaskFromMarkdownAsync_ReturnsNullWhenAllComplete()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var sessionManager = new SessionManager(loggerFactory.CreateLogger<SessionManager>());
        var mockOpenAiService = new Mock<ISessionAwareOpenAIService>();
        var markdownManager = new MarkdownTaskStateManager(
            sessionManager,
            mockOpenAiService.Object,
            loggerFactory.CreateLogger<MarkdownTaskStateManager>());

        var session = await sessionManager.CreateSessionAsync("Test All Complete Session");
        
        // Set up markdown with all completed subtasks
        var testMarkdown = """
            # Task: Test Task
            
            **Strategy:** Execute sequentially
            
            ## Subtasks
            
            - [x] First task - completed
            - [x] Second task - completed
            - [x] Third task - completed
            
            ## Context
            
            All tasks completed.
            """;
            
        session.TaskStateMarkdown = testMarkdown;
        await sessionManager.SaveSessionAsync(session);

        // Act
        var currentSubtask = await markdownManager.GetCurrentSubtaskFromMarkdownAsync(session.SessionId);

        // Assert
        Assert.Null(currentSubtask);
    }

    [Fact]
    public async Task UpdateTaskMarkdownAsync_UpdatesMarkdownCorrectly()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var sessionManager = new SessionManager(loggerFactory.CreateLogger<SessionManager>());
        var mockOpenAiService = new Mock<ISessionAwareOpenAIService>();
        var markdownManager = new MarkdownTaskStateManager(
            sessionManager,
            mockOpenAiService.Object,
            loggerFactory.CreateLogger<MarkdownTaskStateManager>());

        var session = await sessionManager.CreateSessionAsync("Test Update Session");
        
        // Set up initial markdown
        var initialMarkdown = """
            # Task: Test Task
            
            **Strategy:** Execute sequentially
            
            ## Subtasks
            
            - [ ] Data gathering
            - [ ] Analysis
            
            ## Context
            
            Initial setup.
            """;
            
        session.TaskStateMarkdown = initialMarkdown;
        await sessionManager.SaveSessionAsync(session);

        // Mock OpenAI response with updated markdown
        var updatedMarkdownContent = """
            # Task: Test Task
            
            **Strategy:** Execute sequentially
            
            ## Subtasks
            
            - [x] Data gathering - completed successfully
            - [ ] Analysis
            
            ## Progress Notes
            
            *Updated with action results*
            
            ## Context
            
            Data gathering completed with positive results.
            """;

        var mockResponse = new ResponsesCreateResponse
        {
            Output = new[]
            {
                new OutputMessage
                {
                    Type = "message",
                    Content = JsonDocument.Parse(JsonSerializer.Serialize(updatedMarkdownContent)).RootElement
                }
            }
        };

        mockOpenAiService.Setup(x => x.CreateResponseAsync(It.IsAny<ResponsesCreateRequest>(), default))
                        .ReturnsAsync(mockResponse);

        // Act
        var updatedMarkdown = await markdownManager.UpdateTaskMarkdownAsync(
            session.SessionId, 
            "Completed data gathering", 
            "Successfully collected all required data");

        // Assert
        Assert.NotNull(updatedMarkdown);
        Assert.Contains("- [x] Data gathering", updatedMarkdown);
        Assert.Contains("completed successfully", updatedMarkdown);
        
        // Verify it was saved to session
        var updatedSession = await sessionManager.GetSessionAsync(session.SessionId);
        Assert.NotNull(updatedSession);
        Assert.Equal(updatedMarkdown, updatedSession.TaskStateMarkdown);
    }

    [Fact]
    public async Task GetTaskMarkdownAsync_ReturnsEmptyForNonExistentSession()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var sessionManager = new SessionManager(loggerFactory.CreateLogger<SessionManager>());
        var mockOpenAiService = new Mock<ISessionAwareOpenAIService>();
        var markdownManager = new MarkdownTaskStateManager(
            sessionManager,
            mockOpenAiService.Object,
            loggerFactory.CreateLogger<MarkdownTaskStateManager>());

        // Act
        var markdown = await markdownManager.GetTaskMarkdownAsync("non-existent-session-id");

        // Assert
        Assert.Equal(string.Empty, markdown);
    }

    [Fact]
    public async Task CompleteSubtaskInMarkdownAsync_ThrowsForNonExistentSession()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var sessionManager = new SessionManager(loggerFactory.CreateLogger<SessionManager>());
        var mockOpenAiService = new Mock<ISessionAwareOpenAIService>();
        var markdownManager = new MarkdownTaskStateManager(
            sessionManager,
            mockOpenAiService.Object,
            loggerFactory.CreateLogger<MarkdownTaskStateManager>());

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await markdownManager.CompleteSubtaskInMarkdownAsync(
                "non-existent-session-id", 
                "Some task", 
                "Some result"));
    }
}