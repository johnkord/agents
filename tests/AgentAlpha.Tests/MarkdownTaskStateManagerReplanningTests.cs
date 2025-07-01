using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Common.Models.Session;
using Common.Services.Session;
using Common.Interfaces.Session;
using Common.Interfaces.Tools;
using OpenAIIntegration;
using OpenAIIntegration.Model;
using SessionService.Services;
using System.Text.Json;
using System.Linq;

namespace AgentAlpha.Tests;

/// <summary>
/// Tests for the merged re-planning functionality in MarkdownTaskStateManager
/// Validates that re-planning occurs each time the task state is updated
/// </summary>
public class MarkdownTaskStateManagerReplanningTests
{
    private const string InitialMarkdown = """
        # Task: Test Project
        
        **Strategy:** Complete project step by step
        
        ## Subtasks
        
        - [ ] Research requirements
        - [ ] Design solution
        - [ ] Implement features
        - [ ] Test implementation
        
        ## Progress Notes
        
        *Task initialized*
        
        ## Context
        
        Initial setup completed.
        """;

    private const string UpdatedMarkdownWithReplanning = """
        # Task: Test Project
        
        **Strategy:** Complete project step by step, adjusted based on research findings
        
        ## Subtasks
        
        - [x] Research requirements - Completed successfully
        - [ ] Design solution based on research findings
        - [ ] Create detailed implementation plan
        - [ ] Implement core features
        - [ ] Implement additional features discovered during research
        - [ ] Test implementation
        - [ ] Document findings
        
        ## Progress Notes
        
        *Updated: Research phase revealed additional requirements*
        
        ## Context
        
        Research completed. Found need for additional security features and user interface improvements.
        """;

    [Fact]
    public async Task UpdateTaskMarkdownAsync_PerformsReplanningBasedOnProgress()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var sessionManager = new SessionManager(loggerFactory.CreateLogger<SessionManager>());
        var mockOpenAiService = new Mock<ISessionAwareOpenAIService>();
        var mockToolScope = new Mock<IToolScopeManager>();
        var markdownManager = new MarkdownTaskStateManager(
            sessionManager,
            mockOpenAiService.Object,
            mockToolScope.Object,
            loggerFactory.CreateLogger<MarkdownTaskStateManager>());

        var session = await sessionManager.CreateSessionAsync("Test Re-planning Session");
        
        // Set up initial markdown
        var sessionToUpdate = await sessionManager.GetSessionAsync(session.SessionId);
        Assert.NotNull(sessionToUpdate);
        sessionToUpdate.TaskStateMarkdown = InitialMarkdown;
        await sessionManager.SaveSessionAsync(sessionToUpdate);

        // Mock OpenAI response with re-planned markdown using function call
        var mockFunctionCallResponse = new ResponsesCreateResponse
        {
            Output = new ResponseOutputItem[]
            {
                new FunctionToolCall
                {
                    Name = "save_markdown_plan",
                    Arguments = JsonSerializer.SerializeToElement(new
                    {
                        markdown_plan = UpdatedMarkdownWithReplanning,
                        required_tools = new[] { "research_tool", "design_tool", "security_analyzer" }
                    })
                }
            }
        };

        mockOpenAiService.Setup(x => x.CreateResponseAsync(It.IsAny<ResponsesCreateRequest>(), default))
                        .ReturnsAsync(mockFunctionCallResponse);

        // Act - Update with action result that should trigger re-planning
        var updatedMarkdown = await markdownManager.UpdateTaskMarkdownAsync(
            session.SessionId, 
            "Completed research phase", 
            "Found additional security and UI requirements that need to be addressed");

        // Assert
        Assert.NotNull(updatedMarkdown);
        Assert.Contains("Research requirements - Completed successfully", updatedMarkdown);
        Assert.Contains("additional security features", updatedMarkdown);
        Assert.Contains("adjusted based on research findings", updatedMarkdown);
        
        // Verify that the re-planned markdown was saved to session
        var finalSession = await sessionManager.GetSessionAsync(session.SessionId);
        Assert.NotNull(finalSession);
        Assert.Equal(updatedMarkdown, finalSession.TaskStateMarkdown);

        // Verify that tool scope was updated with new required tools
        mockToolScope.Verify(x => x.SetRequiredTools(session.SessionId, 
            It.Is<string[]>(tools => tools.Contains("research_tool") && 
                                   tools.Contains("design_tool") && 
                                   tools.Contains("security_analyzer"))), 
            Times.Once);

        // Verify that the OpenAI service was called (re-planning occurred)
        mockOpenAiService.Verify(x => x.CreateResponseAsync(It.IsAny<ResponsesCreateRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task CompleteSubtaskInMarkdownAsync_PerformsReplanningOnCompletion()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var sessionManager = new SessionManager(loggerFactory.CreateLogger<SessionManager>());
        var mockOpenAiService = new Mock<ISessionAwareOpenAIService>();
        var mockToolScope = new Mock<IToolScopeManager>();
        var markdownManager = new MarkdownTaskStateManager(
            sessionManager,
            mockOpenAiService.Object,
            mockToolScope.Object,
            loggerFactory.CreateLogger<MarkdownTaskStateManager>());

        var session = await sessionManager.CreateSessionAsync("Test Subtask Re-planning Session");
        
        // Set up initial markdown
        var sessionToUpdate = await sessionManager.GetSessionAsync(session.SessionId);
        Assert.NotNull(sessionToUpdate);
        sessionToUpdate.TaskStateMarkdown = InitialMarkdown;
        await sessionManager.SaveSessionAsync(sessionToUpdate);

        // Mock OpenAI response with re-planned markdown after subtask completion
        var replanAfterSubtaskCompletion = """
            # Task: Test Project
            
            **Strategy:** Complete project step by step, now focusing on design phase
            
            ## Subtasks
            
            - [x] Research requirements - Completed with comprehensive findings
            - [ ] Create detailed technical design
            - [ ] Validate design with stakeholders
            - [ ] Implement core features
            - [ ] Test implementation
            
            ## Progress Notes
            
            *Updated: Research completed successfully, moving to design phase*
            
            ## Context
            
            Research phase completed with excellent results. Ready to proceed with design.
            """;

        var mockFunctionCallResponse = new ResponsesCreateResponse
        {
            Output = new ResponseOutputItem[]
            {
                new FunctionToolCall
                {
                    Name = "save_markdown_plan",
                    Arguments = JsonSerializer.SerializeToElement(new
                    {
                        markdown_plan = replanAfterSubtaskCompletion,
                        required_tools = new[] { "design_tool", "validation_tool" }
                    })
                }
            }
        };

        mockOpenAiService.Setup(x => x.CreateResponseAsync(It.IsAny<ResponsesCreateRequest>(), default))
                        .ReturnsAsync(mockFunctionCallResponse);

        // Act - Complete subtask which should trigger re-planning
        var updatedMarkdown = await markdownManager.CompleteSubtaskInMarkdownAsync(
            session.SessionId, 
            "Research requirements", 
            "Completed comprehensive research with detailed findings");

        // Assert
        Assert.NotNull(updatedMarkdown);
        Assert.Contains("Research requirements - Completed with comprehensive findings", updatedMarkdown);
        Assert.Contains("now focusing on design phase", updatedMarkdown);
        Assert.Contains("Create detailed technical design", updatedMarkdown);
        
        // Verify that tool scope was updated with new required tools for the next phase
        mockToolScope.Verify(x => x.SetRequiredTools(session.SessionId, 
            It.Is<string[]>(tools => tools.Contains("design_tool") && 
                                   tools.Contains("validation_tool"))), 
            Times.Once);

        // Verify that the OpenAI service was called (re-planning occurred)
        mockOpenAiService.Verify(x => x.CreateResponseAsync(It.IsAny<ResponsesCreateRequest>(), default), Times.Once);
    }

    [Fact]
    public async Task UpdatePlanIterativelyAsync_UsesStructuredToolResponse()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var sessionManager = new SessionManager(loggerFactory.CreateLogger<SessionManager>());
        var mockOpenAiService = new Mock<ISessionAwareOpenAIService>();
        var mockToolScope = new Mock<IToolScopeManager>();
        var markdownManager = new MarkdownTaskStateManager(
            sessionManager,
            mockOpenAiService.Object,
            mockToolScope.Object,
            loggerFactory.CreateLogger<MarkdownTaskStateManager>());

        var session = await sessionManager.CreateSessionAsync("Test Iterative Planning Session");
        
        // Set up initial markdown
        var sessionToUpdate = await sessionManager.GetSessionAsync(session.SessionId);
        Assert.NotNull(sessionToUpdate);
        sessionToUpdate.TaskStateMarkdown = InitialMarkdown;
        await sessionManager.SaveSessionAsync(sessionToUpdate);

        // Mock structured response for iterative planning
        var iterativelyUpdatedMarkdown = """
            # Task: Test Project
            
            **Strategy:** Adjusted strategy based on execution feedback
            
            ## Subtasks
            
            - [x] Research requirements
            - [x] Initial design
            - [ ] Refine design based on feedback
            - [ ] Implement with new approach
            - [ ] Extended testing phase
            
            ## Progress Notes
            
            *Updated based on execution feedback*
            
            ## Context
            
            Execution feedback revealed need for design refinements.
            """;

        var mockFunctionCallResponse = new ResponsesCreateResponse
        {
            Output = new ResponseOutputItem[]
            {
                new FunctionToolCall
                {
                    Name = "save_markdown_plan",
                    Arguments = JsonSerializer.SerializeToElement(new
                    {
                        markdown_plan = iterativelyUpdatedMarkdown,
                        required_tools = new[] { "design_refiner", "advanced_tester" }
                    })
                }
            }
        };

        mockOpenAiService.Setup(x => x.CreateResponseAsync(It.IsAny<ResponsesCreateRequest>(), default))
                        .ReturnsAsync(mockFunctionCallResponse);

        // Act - Update plan iteratively with execution feedback
        var updatedMarkdown = await markdownManager.UpdatePlanIterativelyAsync(
            session.SessionId, 
            "Initial design needs refinement based on technical constraints",
            "Current implementation approach may not scale properly");

        // Assert
        Assert.NotNull(updatedMarkdown);
        Assert.Contains("Adjusted strategy based on execution feedback", updatedMarkdown);
        Assert.Contains("Refine design based on feedback", updatedMarkdown);
        Assert.Contains("Extended testing phase", updatedMarkdown);
        
        // Verify that tool scope was updated with tools for the refined approach
        mockToolScope.Verify(x => x.SetRequiredTools(session.SessionId, 
            It.Is<string[]>(tools => tools.Contains("design_refiner") && 
                                   tools.Contains("advanced_tester"))), 
            Times.Once);
    }

    [Fact]
    public async Task ReplanningFallsBackGracefullyWhenFunctionCallFails()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var sessionManager = new SessionManager(loggerFactory.CreateLogger<SessionManager>());
        var mockOpenAiService = new Mock<ISessionAwareOpenAIService>();
        var mockToolScope = new Mock<IToolScopeManager>();
        var markdownManager = new MarkdownTaskStateManager(
            sessionManager,
            mockOpenAiService.Object,
            mockToolScope.Object,
            loggerFactory.CreateLogger<MarkdownTaskStateManager>());

        var session = await sessionManager.CreateSessionAsync("Test Fallback Session");
        
        // Set up initial markdown
        var sessionToUpdate = await sessionManager.GetSessionAsync(session.SessionId);
        Assert.NotNull(sessionToUpdate);
        sessionToUpdate.TaskStateMarkdown = InitialMarkdown;
        await sessionManager.SaveSessionAsync(sessionToUpdate);

        // First call returns no function call (with a message content), 
        // which will be extracted and returned as fallback
        var emptyFunctionCallResponse = new ResponsesCreateResponse
        {
            Output = new ResponseOutputItem[]
            {
                new OutputMessage
                {
                    Type = "message",
                    Content = JsonDocument.Parse("\"Fallback update applied - no structured response available\"").RootElement
                }
            }
        };

        mockOpenAiService.Setup(x => x.CreateResponseAsync(It.IsAny<ResponsesCreateRequest>(), default))
                        .ReturnsAsync(emptyFunctionCallResponse);

        // Act - Update should fall back gracefully when function call fails
        var updatedMarkdown = await markdownManager.UpdateTaskMarkdownAsync(
            session.SessionId, 
            "Research completed", 
            "Basic research results available");

        // Assert
        Assert.NotNull(updatedMarkdown);
        // The fallback should extract the content from the OutputMessage
        Assert.Contains("Fallback update applied", updatedMarkdown);
        
        // Verify that OpenAI service was called once (no second fallback call in this scenario)
        mockOpenAiService.Verify(x => x.CreateResponseAsync(It.IsAny<ResponsesCreateRequest>(), default), 
            Times.Once);
    }
}