using Xunit;
using AgentAlpha.Models;
using AgentAlpha.Services;
using Microsoft.Extensions.Logging;
using System.IO;

namespace AgentAlpha.Tests;

public class PlanPersistenceTests : IDisposable
{
    private readonly SessionManager _sessionManager;
    private readonly ILogger<SessionManager> _logger;
    private readonly string _testDbPath;

    public PlanPersistenceTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<SessionManager>();
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_plan_persistence_{Guid.NewGuid()}.db");
        _sessionManager = new SessionManager(_logger, _testDbPath);
    }

    public void Dispose()
    {
        // Clean up test database
        if (File.Exists(_testDbPath))
        {
            File.Delete(_testDbPath);
        }
    }

    [Fact]
    public async Task Session_ShouldPersistAndRestorePlan()
    {
        // Arrange
        var session = await _sessionManager.CreateSessionAsync("Test Session with Plan");
        var plan = new TaskPlan
        {
            Task = "Test task",
            Strategy = "Test strategy",
            Steps = new List<PlanStep>
            {
                new PlanStep { StepNumber = 1, Description = "First step" },
                new PlanStep { StepNumber = 2, Description = "Second step" }
            },
            RequiredTools = new List<string> { "tool1", "tool2" },
            Complexity = TaskComplexity.Medium,
            Confidence = 0.85
        };

        // Act - Save plan to session
        session.SetCurrentPlan(plan);
        await _sessionManager.SaveSessionAsync(session);

        // Retrieve session and plan
        var retrievedSession = await _sessionManager.GetSessionAsync(session.SessionId);
        var retrievedPlan = retrievedSession?.GetCurrentPlan();

        // Assert
        Assert.NotNull(retrievedSession);
        Assert.NotNull(retrievedPlan);
        Assert.Equal(plan.Task, retrievedPlan.Task);
        Assert.Equal(plan.Strategy, retrievedPlan.Strategy);
        Assert.Equal(plan.Steps.Count, retrievedPlan.Steps.Count);
        Assert.Equal(plan.RequiredTools.Count, retrievedPlan.RequiredTools.Count);
        Assert.Equal(plan.Complexity, retrievedPlan.Complexity);
        Assert.Equal(plan.Confidence, retrievedPlan.Confidence);
    }

    [Fact]
    public async Task Session_ShouldHandleNullPlan()
    {
        // Arrange
        var session = await _sessionManager.CreateSessionAsync("Test Session without Plan");

        // Act - Save session without plan
        await _sessionManager.SaveSessionAsync(session);

        // Retrieve session
        var retrievedSession = await _sessionManager.GetSessionAsync(session.SessionId);
        var retrievedPlan = retrievedSession?.GetCurrentPlan();

        // Assert
        Assert.NotNull(retrievedSession);
        Assert.Null(retrievedPlan);
        Assert.Equal(string.Empty, retrievedSession.CurrentPlan);
    }

    [Fact]
    public async Task Session_ShouldUpdatePlan()
    {
        // Arrange
        var session = await _sessionManager.CreateSessionAsync("Test Session for Plan Update");
        var originalPlan = new TaskPlan
        {
            Task = "Original task",
            Strategy = "Original strategy",
            Steps = new List<PlanStep> { new PlanStep { StepNumber = 1, Description = "Original step" } },
            RequiredTools = new List<string> { "original_tool" },
            Complexity = TaskComplexity.Simple,
            Confidence = 0.7
        };

        // Act - Save original plan
        session.SetCurrentPlan(originalPlan);
        await _sessionManager.SaveSessionAsync(session);

        // Update plan
        var updatedPlan = new TaskPlan
        {
            Task = "Updated task",
            Strategy = "Updated strategy",
            Steps = new List<PlanStep>
            {
                new PlanStep { StepNumber = 1, Description = "Updated step 1" },
                new PlanStep { StepNumber = 2, Description = "Updated step 2" }
            },
            RequiredTools = new List<string> { "updated_tool1", "updated_tool2" },
            Complexity = TaskComplexity.Complex,
            Confidence = 0.9
        };

        session.SetCurrentPlan(updatedPlan);
        await _sessionManager.SaveSessionAsync(session);

        // Retrieve and verify updated plan
        var retrievedSession = await _sessionManager.GetSessionAsync(session.SessionId);
        var retrievedPlan = retrievedSession?.GetCurrentPlan();

        // Assert
        Assert.NotNull(retrievedSession);
        Assert.NotNull(retrievedPlan);
        Assert.Equal(updatedPlan.Task, retrievedPlan.Task);
        Assert.Equal(updatedPlan.Strategy, retrievedPlan.Strategy);
        Assert.Equal(2, retrievedPlan.Steps.Count);
        Assert.Equal("Updated step 1", retrievedPlan.Steps[0].Description);
        Assert.Equal("Updated step 2", retrievedPlan.Steps[1].Description);
        Assert.Equal(2, retrievedPlan.RequiredTools.Count);
        Assert.Contains("updated_tool1", retrievedPlan.RequiredTools);
        Assert.Contains("updated_tool2", retrievedPlan.RequiredTools);
        Assert.Equal(TaskComplexity.Complex, retrievedPlan.Complexity);
        Assert.Equal(0.9, retrievedPlan.Confidence);
    }

    [Fact]
    public async Task Session_ShouldClearPlan()
    {
        // Arrange
        var session = await _sessionManager.CreateSessionAsync("Test Session for Plan Clearing");
        var plan = new TaskPlan
        {
            Task = "Task to be cleared",
            Strategy = "Strategy to be cleared",
            Steps = new List<PlanStep> { new PlanStep { StepNumber = 1, Description = "Step to be cleared" } },
            RequiredTools = new List<string> { "tool_to_be_cleared" },
            Complexity = TaskComplexity.Medium,
            Confidence = 0.8
        };

        // Act - Save plan then clear it
        session.SetCurrentPlan(plan);
        await _sessionManager.SaveSessionAsync(session);

        session.SetCurrentPlan(null);
        await _sessionManager.SaveSessionAsync(session);

        // Retrieve session
        var retrievedSession = await _sessionManager.GetSessionAsync(session.SessionId);
        var retrievedPlan = retrievedSession?.GetCurrentPlan();

        // Assert
        Assert.NotNull(retrievedSession);
        Assert.Null(retrievedPlan);
        Assert.Equal(string.Empty, retrievedSession.CurrentPlan);
    }
}