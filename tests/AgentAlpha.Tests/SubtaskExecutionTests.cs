using Microsoft.Extensions.Logging;
using Xunit;
using AgentAlpha.Services;
using Common.Models.Session;
using Common.Services.Session;
using SessionService.Services;
using System.Text.Json;

namespace AgentAlpha.Tests;

/// <summary>
/// Tests for sequential subtask execution and task state management
/// </summary>
public class SubtaskExecutionTests
{
    [Fact]
    public void TaskState_FromTaskPlan_CreatesCorrectStructure()
    {
        // Arrange
        var taskPlan = new TaskPlan
        {
            Task = "Test multi-step task",
            Strategy = "Execute steps sequentially",
            Steps = new List<PlanStep>
            {
                new PlanStep
                {
                    StepNumber = 1,
                    Description = "First step - setup",
                    IsMandatory = true,
                    PotentialTools = new List<string> { "file_read", "file_write" }
                },
                new PlanStep
                {
                    StepNumber = 2,
                    Description = "Second step - process",
                    IsMandatory = true,
                    PotentialTools = new List<string> { "text_process", "calculate" }
                },
                new PlanStep
                {
                    StepNumber = 3,
                    Description = "Third step - finalize",
                    IsMandatory = false,
                    PotentialTools = new List<string> { "file_write" }
                }
            }
        };

        // Act
        var taskState = TaskState.FromTaskPlan(taskPlan);

        // Assert
        Assert.Equal(taskPlan.Task, taskState.Task);
        Assert.Equal(taskPlan.Strategy, taskState.Strategy);
        Assert.Equal(3, taskState.Subtasks.Count);
        Assert.Equal(TaskCompletionStatus.InProgress, taskState.Status);
        
        // Check subtasks
        var firstSubtask = taskState.Subtasks.First(s => s.StepNumber == 1);
        Assert.Equal("First step - setup", firstSubtask.Description);
        Assert.True(firstSubtask.IsMandatory);
        Assert.Equal(SubtaskStatus.Pending, firstSubtask.Status);
        Assert.Contains("file_read", firstSubtask.PotentialTools);
        Assert.Contains("file_write", firstSubtask.PotentialTools);
    }

    [Fact]
    public void TaskState_GetCurrentSubtask_ReturnsFirstPendingSubtask()
    {
        // Arrange
        var taskState = new TaskState
        {
            Task = "Test task",
            Subtasks = new List<SubtaskState>
            {
                new SubtaskState { StepNumber = 1, Description = "Step 1", Status = SubtaskStatus.Completed },
                new SubtaskState { StepNumber = 2, Description = "Step 2", Status = SubtaskStatus.Pending },
                new SubtaskState { StepNumber = 3, Description = "Step 3", Status = SubtaskStatus.Pending }
            }
        };

        // Act
        var currentSubtask = taskState.GetCurrentSubtask();

        // Assert
        Assert.NotNull(currentSubtask);
        Assert.Equal(2, currentSubtask.StepNumber);
        Assert.Equal("Step 2", currentSubtask.Description);
    }

    [Fact]
    public void TaskState_CompleteSubtask_UpdatesStatusAndContext()
    {
        // Arrange
        var taskState = new TaskState
        {
            Task = "Test task",
            Subtasks = new List<SubtaskState>
            {
                new SubtaskState { StepNumber = 1, Description = "Step 1", Status = SubtaskStatus.InProgress },
                new SubtaskState { StepNumber = 2, Description = "Step 2", Status = SubtaskStatus.Pending }
            }
        };

        var context = new Dictionary<string, object>
        {
            ["result"] = "Step 1 completed successfully",
            ["data"] = "important_data.txt"
        };

        // Act
        taskState.CompleteSubtask(1, "Step 1 finished", "Created file successfully", context);

        // Assert
        var completedSubtask = taskState.Subtasks.First(s => s.StepNumber == 1);
        Assert.Equal(SubtaskStatus.Completed, completedSubtask.Status);
        Assert.Equal("Step 1 finished", completedSubtask.CompletionSummary);
        Assert.Equal("Created file successfully", completedSubtask.CompletionEvidence);
        Assert.NotNull(completedSubtask.CompletedAt);
        
        // Check accumulated context
        Assert.Contains("Step1_result", taskState.AccumulatedContext.Keys);
        Assert.Contains("Step1_data", taskState.AccumulatedContext.Keys);
        Assert.Equal("Step 1 completed successfully", taskState.AccumulatedContext["Step1_result"]);
        Assert.Equal("important_data.txt", taskState.AccumulatedContext["Step1_data"]);
    }

    [Fact]
    public void TaskState_ToMarkdown_GeneratesCorrectFormat()
    {
        // Arrange
        var taskState = new TaskState
        {
            Task = "Create a report",
            Strategy = "Gather data, analyze, and format",
            Status = TaskCompletionStatus.InProgress,
            Subtasks = new List<SubtaskState>
            {
                new SubtaskState 
                { 
                    StepNumber = 1, 
                    Description = "Gather data", 
                    Status = SubtaskStatus.Completed,
                    CompletionSummary = "Data collected from API",
                    CompletedAt = new DateTime(2025, 1, 1, 10, 0, 0)
                },
                new SubtaskState 
                { 
                    StepNumber = 2, 
                    Description = "Analyze data", 
                    Status = SubtaskStatus.InProgress 
                },
                new SubtaskState 
                { 
                    StepNumber = 3, 
                    Description = "Format report", 
                    Status = SubtaskStatus.Pending 
                }
            },
            AccumulatedContext = new Dictionary<string, object>
            {
                ["Step1_dataSource"] = "external_api",
                ["Step1_recordCount"] = 100
            }
        };

        // Act
        var markdown = taskState.ToMarkdown();

        // Assert
        Assert.Contains("# Task: Create a report", markdown);
        Assert.Contains("**Strategy:** Gather data, analyze, and format", markdown);
        Assert.Contains("**Status:** InProgress", markdown);
        Assert.Contains("**Progress:** 1/3 subtasks completed", markdown);
        Assert.Contains("- [x] **Step 1:** Gather data", markdown);
        Assert.Contains("- [ ] **Step 2:** Analyze data", markdown);
        Assert.Contains("- [ ] **Step 3:** Format report", markdown);
        Assert.Contains("*Completed:* Data collected from API", markdown);
        Assert.Contains("*Status:* Currently in progress", markdown);
        Assert.Contains("## Context from Completed Subtasks", markdown);
        Assert.Contains("**Step1_dataSource:** external_api", markdown);
        Assert.Contains("**Step1_recordCount:** 100", markdown);
    }

    [Fact]
    public async Task TaskStateManager_CreateAndSaveTaskState_PersistsCorrectly()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var sessionManager = new SessionManager(loggerFactory.CreateLogger<SessionManager>());
        var taskStateManager = new TaskStateManager(sessionManager, loggerFactory.CreateLogger<TaskStateManager>());
        
        var session = await sessionManager.CreateSessionAsync("Test Subtask Session");
        var taskPlan = new TaskPlan
        {
            Task = "Test task with subtasks",
            Strategy = "Execute sequentially",
            Steps = new List<PlanStep>
            {
                new PlanStep { StepNumber = 1, Description = "First step" },
                new PlanStep { StepNumber = 2, Description = "Second step" }
            }
        };

        // Act
        var taskState = taskStateManager.CreateTaskState(taskPlan);
        await taskStateManager.SaveTaskStateAsync(session.SessionId, taskState);
        var retrievedTaskState = await taskStateManager.GetTaskStateAsync(session.SessionId);

        // Assert
        Assert.NotNull(retrievedTaskState);
        Assert.Equal(taskPlan.Task, retrievedTaskState.Task);
        Assert.Equal(taskPlan.Strategy, retrievedTaskState.Strategy);
        Assert.Equal(2, retrievedTaskState.Subtasks.Count);
        Assert.Equal(TaskCompletionStatus.InProgress, retrievedTaskState.Status);
    }

    [Fact]
    public async Task TaskStateManager_CompleteSubtask_UpdatesStateCorrectly()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var sessionManager = new SessionManager(loggerFactory.CreateLogger<SessionManager>());
        var taskStateManager = new TaskStateManager(sessionManager, loggerFactory.CreateLogger<TaskStateManager>());
        
        var session = await sessionManager.CreateSessionAsync("Test Subtask Completion");
        var taskPlan = new TaskPlan
        {
            Task = "Test task completion",
            Steps = new List<PlanStep>
            {
                new PlanStep { StepNumber = 1, Description = "Setup task" },
                new PlanStep { StepNumber = 2, Description = "Execute task" }
            }
        };

        var taskState = taskStateManager.CreateTaskState(taskPlan);
        await taskStateManager.SaveTaskStateAsync(session.SessionId, taskState);

        // Act
        var context = new Dictionary<string, object> { ["output"] = "setup_complete.txt" };
        var updatedTaskState = await taskStateManager.CompleteSubtaskAsync(
            session.SessionId, 1, "Setup completed successfully", "File created", context);

        // Assert
        var completedSubtask = updatedTaskState.Subtasks.First(s => s.StepNumber == 1);
        Assert.Equal(SubtaskStatus.Completed, completedSubtask.Status);
        Assert.Equal("Setup completed successfully", completedSubtask.CompletionSummary);
        Assert.Equal("File created", completedSubtask.CompletionEvidence);
        Assert.Contains("Step1_output", updatedTaskState.AccumulatedContext.Keys);
    }

    [Fact]
    public async Task TaskStateManager_GetCurrentSubtask_ReturnsCorrectSubtask()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var sessionManager = new SessionManager(loggerFactory.CreateLogger<SessionManager>());
        var taskStateManager = new TaskStateManager(sessionManager, loggerFactory.CreateLogger<TaskStateManager>());
        
        var session = await sessionManager.CreateSessionAsync("Test Current Subtask");
        var taskPlan = new TaskPlan
        {
            Task = "Multi-step task",
            Steps = new List<PlanStep>
            {
                new PlanStep { StepNumber = 1, Description = "First step" },
                new PlanStep { StepNumber = 2, Description = "Second step" },
                new PlanStep { StepNumber = 3, Description = "Third step" }
            }
        };

        var taskState = taskStateManager.CreateTaskState(taskPlan);
        await taskStateManager.SaveTaskStateAsync(session.SessionId, taskState);

        // Act & Assert
        // Initially, should return first subtask
        var currentSubtask = await taskStateManager.GetCurrentSubtaskAsync(session.SessionId);
        Assert.NotNull(currentSubtask);
        Assert.Equal(1, currentSubtask.StepNumber);

        // Complete first subtask
        await taskStateManager.CompleteSubtaskAsync(session.SessionId, 1, "First step done");
        
        // Now should return second subtask
        currentSubtask = await taskStateManager.GetCurrentSubtaskAsync(session.SessionId);
        Assert.NotNull(currentSubtask);
        Assert.Equal(2, currentSubtask.StepNumber);

        // Complete second subtask
        await taskStateManager.CompleteSubtaskAsync(session.SessionId, 2, "Second step done");
        
        // Now should return third subtask
        currentSubtask = await taskStateManager.GetCurrentSubtaskAsync(session.SessionId);
        Assert.NotNull(currentSubtask);
        Assert.Equal(3, currentSubtask.StepNumber);

        // Complete third subtask
        await taskStateManager.CompleteSubtaskAsync(session.SessionId, 3, "Third step done");
        
        // Now should return null (all done)
        currentSubtask = await taskStateManager.GetCurrentSubtaskAsync(session.SessionId);
        Assert.Null(currentSubtask);
    }

    [Fact]
    public void SubtaskState_CanStart_ChecksPrerequisites()
    {
        // Arrange
        var allSubtasks = new List<SubtaskState>
        {
            new SubtaskState { StepNumber = 1, Description = "Step 1", Status = SubtaskStatus.Completed },
            new SubtaskState { StepNumber = 2, Description = "Step 2", Status = SubtaskStatus.Completed },
            new SubtaskState { StepNumber = 3, Description = "Step 3", Status = SubtaskStatus.Pending, Prerequisites = new List<int> { 1, 2 } },
            new SubtaskState { StepNumber = 4, Description = "Step 4", Status = SubtaskStatus.Pending, Prerequisites = new List<int> { 1, 3 } }
        };

        // Act & Assert
        var step3 = allSubtasks.First(s => s.StepNumber == 3);
        var step4 = allSubtasks.First(s => s.StepNumber == 4);

        Assert.True(step3.CanStart(allSubtasks)); // Prerequisites 1,2 are completed
        Assert.False(step4.CanStart(allSubtasks)); // Prerequisite 3 is not completed
    }

    [Fact]
    public async Task TaskStateManager_GetAccumulatedContext_ReturnsCorrectContext()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var sessionManager = new SessionManager(loggerFactory.CreateLogger<SessionManager>());
        var taskStateManager = new TaskStateManager(sessionManager, loggerFactory.CreateLogger<TaskStateManager>());
        
        var session = await sessionManager.CreateSessionAsync("Test Context Accumulation");
        var taskPlan = new TaskPlan
        {
            Task = "Context test task",
            Steps = new List<PlanStep>
            {
                new PlanStep { StepNumber = 1, Description = "Generate data" },
                new PlanStep { StepNumber = 2, Description = "Process data" }
            }
        };

        var taskState = taskStateManager.CreateTaskState(taskPlan);
        await taskStateManager.SaveTaskStateAsync(session.SessionId, taskState);

        // Act
        var context1 = new Dictionary<string, object> { ["filename"] = "data.csv", ["rows"] = 100 };
        await taskStateManager.CompleteSubtaskAsync(session.SessionId, 1, "Data generated", "Created CSV file", context1);

        var context2 = new Dictionary<string, object> { ["processedFile"] = "processed_data.json", ["summary"] = "Data processed successfully" };
        await taskStateManager.CompleteSubtaskAsync(session.SessionId, 2, "Data processed", "Created JSON file", context2);

        var accumulatedContext = await taskStateManager.GetAccumulatedContextAsync(session.SessionId);

        // Assert
        Assert.Equal(4, accumulatedContext.Count);
        Assert.Contains("Step1_filename", accumulatedContext.Keys);
        Assert.Contains("Step1_rows", accumulatedContext.Keys);
        Assert.Contains("Step2_processedFile", accumulatedContext.Keys);
        Assert.Contains("Step2_summary", accumulatedContext.Keys);
        
        // Compare values with type-safe approach
        Assert.Equal("data.csv", accumulatedContext["Step1_filename"]?.ToString());
        
        // For numeric values, convert to expected type
        var rowsValue = accumulatedContext["Step1_rows"];
        if (rowsValue is int intValue)
        {
            Assert.Equal(100, intValue);
        }
        else if (rowsValue is JsonElement jsonElement)
        {
            if (jsonElement.TryGetInt32(out int jsonInt))
            {
                Assert.Equal(100, jsonInt);
            }
            else
            {
                Assert.Equal(100, Convert.ToInt32(rowsValue));
            }
        }
        else
        {
            Assert.Equal(100, Convert.ToInt32(rowsValue));
        }
        
        Assert.Equal("processed_data.json", accumulatedContext["Step2_processedFile"]?.ToString());
        Assert.Equal("Data processed successfully", accumulatedContext["Step2_summary"]?.ToString());
    }
}