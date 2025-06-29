using Xunit;
using Microsoft.Extensions.Logging;
using SessionService.Services;
using Common.Models.Session;
using Common.Services.Session;

namespace Common.Tests;

public class SessionEnhancementTests
{
    private SessionManager CreateSessionManager()
    {
        var logger = new LoggerFactory().CreateLogger<SessionManager>();
        // Use a temporary file instead of :memory: to ensure the database is properly initialized
        var tempPath = Path.GetTempFileName();
        return new SessionManager(logger, tempPath);
    }

    private TaskStateManager CreateTaskStateManager(SessionManager sessionManager)
    {
        var logger = new LoggerFactory().CreateLogger<TaskStateManager>();
        return new TaskStateManager(sessionManager, logger);
    }

    [Fact]
    public async Task AgentSession_CanStoreTaskProgressInformation()
    {
        // Arrange
        var sessionManager = CreateSessionManager();
        var session = await sessionManager.CreateSessionAsync("Test Task Progress");

        // Act - Update task information
        var taskPlan = new TaskPlan
        {
            Task = "Test task with multiple steps",
            Strategy = "Step by step approach",
            Steps = new List<PlanStep>
            {
                new() { StepNumber = 1, Description = "First step" },
                new() { StepNumber = 2, Description = "Second step" },
                new() { StepNumber = 3, Description = "Third step" }
            },
            Complexity = TaskComplexity.Medium
        };

        session.UpdateTaskInfo(taskPlan);
        await sessionManager.SaveSessionAsync(session);

        // Act - Retrieve and verify
        var retrievedSession = await sessionManager.GetSessionAsync(session.SessionId);

        // Assert
        Assert.NotNull(retrievedSession);
        Assert.Equal("Test task with multiple steps", retrievedSession.TaskTitle);
        Assert.Equal(TaskExecutionStatus.InProgress, retrievedSession.TaskStatus);
        Assert.Equal(3, retrievedSession.TotalSteps);
        Assert.Equal(0, retrievedSession.CompletedSteps);
        Assert.Equal(0.0, retrievedSession.ProgressPercentage);
        Assert.Equal(1, retrievedSession.CurrentStep);
        Assert.NotNull(retrievedSession.TaskStartedAt);
        Assert.Equal("Medium", retrievedSession.TaskCategory);
        Assert.Equal(2, retrievedSession.TaskPriority);
    }

    [Fact]
    public async Task AgentSession_CanUpdateSubtaskProgress()
    {
        // Arrange
        var sessionManager = CreateSessionManager();
        var session = await sessionManager.CreateSessionAsync("Test Subtask Progress");

        var taskPlan = new TaskPlan
        {
            Task = "Test task",
            Steps = new List<PlanStep>
            {
                new() { StepNumber = 1, Description = "Step 1" },
                new() { StepNumber = 2, Description = "Step 2" }
            }
        };

        session.UpdateTaskInfo(taskPlan);

        // Act - Complete first subtask
        session.UpdateSubtaskProgress(1, false);
        await sessionManager.SaveSessionAsync(session);

        // Assert - Check progress after first step
        var retrievedSession = await sessionManager.GetSessionAsync(session.SessionId);
        Assert.NotNull(retrievedSession);
        Assert.Equal(1, retrievedSession.CompletedSteps);
        Assert.Equal(2, retrievedSession.CurrentStep);
        Assert.Equal(0.5, retrievedSession.ProgressPercentage);
        Assert.Equal(TaskExecutionStatus.InProgress, retrievedSession.TaskStatus);

        // Act - Complete second subtask (final)
        retrievedSession.UpdateSubtaskProgress(2, true);
        await sessionManager.SaveSessionAsync(retrievedSession);

        // Assert - Check completion
        var finalSession = await sessionManager.GetSessionAsync(session.SessionId);
        Assert.NotNull(finalSession);
        Assert.Equal(2, finalSession.CompletedSteps);
        Assert.Equal(2, finalSession.CurrentStep);
        Assert.Equal(1.0, finalSession.ProgressPercentage);
        Assert.Equal(TaskExecutionStatus.Completed, finalSession.TaskStatus);
        Assert.NotNull(finalSession.TaskCompletedAt);
        Assert.NotNull(finalSession.ActualDuration);
    }

    [Fact]
    public async Task SessionManager_CanQueryByTaskStatus()
    {
        // Arrange
        var sessionManager = CreateSessionManager();
        
        var session1 = await sessionManager.CreateSessionAsync("In Progress Task");
        session1.TaskStatus = TaskExecutionStatus.InProgress;
        session1.TaskTitle = "Ongoing work";
        await sessionManager.SaveSessionAsync(session1);

        var session2 = await sessionManager.CreateSessionAsync("Completed Task");
        session2.TaskStatus = TaskExecutionStatus.Completed;
        session2.TaskTitle = "Finished work";
        await sessionManager.SaveSessionAsync(session2);

        // Act
        var inProgressSessions = await sessionManager.GetSessionsByTaskStatusAsync(TaskExecutionStatus.InProgress);
        var completedSessions = await sessionManager.GetSessionsByTaskStatusAsync(TaskExecutionStatus.Completed);

        // Assert
        Assert.Single(inProgressSessions);
        Assert.Equal("Ongoing work", inProgressSessions[0].TaskTitle);
        
        Assert.Single(completedSessions);
        Assert.Equal("Finished work", completedSessions[0].TaskTitle);
    }

    [Fact]
    public async Task SessionManager_CanQueryByProgressRange()
    {
        // Arrange
        var sessionManager = CreateSessionManager();
        
        var session1 = await sessionManager.CreateSessionAsync("Low Progress");
        session1.ProgressPercentage = 0.2;
        await sessionManager.SaveSessionAsync(session1);

        var session2 = await sessionManager.CreateSessionAsync("High Progress");
        session2.ProgressPercentage = 0.8;
        await sessionManager.SaveSessionAsync(session2);

        // Act
        var lowProgressSessions = await sessionManager.GetSessionsByProgressRangeAsync(0.0, 0.5);
        var highProgressSessions = await sessionManager.GetSessionsByProgressRangeAsync(0.5, 1.0);

        // Assert
        Assert.Single(lowProgressSessions);
        Assert.Equal("Low Progress", lowProgressSessions[0].Name);
        
        Assert.Single(highProgressSessions);
        Assert.Equal("High Progress", highProgressSessions[0].Name);
    }

    [Fact]
    public async Task SessionManager_CanQueryByTags()
    {
        // Arrange
        var sessionManager = CreateSessionManager();
        
        var session1 = await sessionManager.CreateSessionAsync("Development Task");
        session1.TaskTags = "development, coding, backend";
        await sessionManager.SaveSessionAsync(session1);

        var session2 = await sessionManager.CreateSessionAsync("Testing Task");
        session2.TaskTags = "testing, qa, automation";
        await sessionManager.SaveSessionAsync(session2);

        // Act
        var devSessions = await sessionManager.GetSessionsByTagsAsync("development");
        var testSessions = await sessionManager.GetSessionsByTagsAsync("testing");
        var codingSessions = await sessionManager.GetSessionsByTagsAsync("coding");

        // Assert
        Assert.Single(devSessions);
        Assert.Equal("Development Task", devSessions[0].Name);
        
        Assert.Single(testSessions);
        Assert.Equal("Testing Task", testSessions[0].Name);
        
        Assert.Single(codingSessions);
        Assert.Equal("Development Task", codingSessions[0].Name);
    }

    [Fact]
    public async Task TaskStateManager_InitializeTaskStateUpdatesSession()
    {
        // Arrange
        var sessionManager = CreateSessionManager();
        var taskStateManager = CreateTaskStateManager(sessionManager);
        
        var session = await sessionManager.CreateSessionAsync("Task State Test");
        
        var taskPlan = new TaskPlan
        {
            Task = "Complex analysis task",
            Strategy = "Multi-step data processing",
            Steps = new List<PlanStep>
            {
                new() { StepNumber = 1, Description = "Collect data" },
                new() { StepNumber = 2, Description = "Process data" },
                new() { StepNumber = 3, Description = "Generate report" }
            },
            Complexity = TaskComplexity.Complex,
            AdditionalContext = new Dictionary<string, object>
            {
                ["priority"] = 4,
                ["tags"] = "analysis, reporting, data"
            }
        };

        // Act
        var taskState = await taskStateManager.InitializeTaskStateAsync(session.SessionId, taskPlan);

        // Assert
        Assert.NotNull(taskState);
        Assert.Equal("Complex analysis task", taskState.Task);
        Assert.Equal(3, taskState.Subtasks.Count);

        // Verify session was updated
        var updatedSession = await sessionManager.GetSessionAsync(session.SessionId);
        Assert.NotNull(updatedSession);
        Assert.Equal("Complex analysis task", updatedSession.TaskTitle);
        Assert.Equal(3, updatedSession.TotalSteps);
        Assert.Equal(TaskExecutionStatus.InProgress, updatedSession.TaskStatus);
        Assert.Equal("Complex", updatedSession.TaskCategory);
        Assert.Equal(4, updatedSession.TaskPriority);
        Assert.Contains("analysis", updatedSession.TaskTags);
    }

    [Fact]
    public void AgentSession_TaskTagsHandling()
    {
        // Arrange
        var session = new AgentSession();
        var tags = new[] { "development", "backend", "api" };

        // Act
        session.SetTaskTags(tags);

        // Assert
        Assert.Equal("development, backend, api", session.TaskTags);
        
        var retrievedTags = session.GetTaskTagsList();
        Assert.Equal(3, retrievedTags.Count);
        Assert.Contains("development", retrievedTags);
        Assert.Contains("backend", retrievedTags);
        Assert.Contains("api", retrievedTags);
    }

    [Fact]
    public void AgentSession_TaskProgressSummary()
    {
        // Arrange
        var session = new AgentSession
        {
            TaskStatus = TaskExecutionStatus.InProgress,
            CompletedSteps = 2,
            TotalSteps = 5,
            ProgressPercentage = 0.4
        };

        // Act
        var summary = session.GetTaskProgressSummary();

        // Assert
        Assert.Equal("In Progress: 2/5 steps completed (40.0 %)", summary);
    }
}