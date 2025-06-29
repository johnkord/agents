using Microsoft.Extensions.Logging;
using System.Text.Json;
using Common.Models.Session;
using Common.Interfaces.Session;

namespace Common.Services.Session;

/// <summary>
/// Implementation of task state management
/// </summary>
public class TaskStateManager : ITaskStateManager
{
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<TaskStateManager> _logger;
    
    public TaskStateManager(ISessionManager sessionManager, ILogger<TaskStateManager> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }
    
    public TaskState CreateTaskState(TaskPlan taskPlan)
    {
        _logger.LogDebug("Creating task state from task plan: {Task}", taskPlan.Task);
        return TaskState.FromTaskPlan(taskPlan);
    }
    
    public async Task<TaskState?> GetTaskStateAsync(string sessionId)
    {
        try
        {
            var session = await _sessionManager.GetSessionAsync(sessionId);
            if (session == null)
            {
                _logger.LogWarning("Session {SessionId} not found", sessionId);
                return null;
            }
            
            // Try to get task state from session metadata
            var taskStateJson = session.Metadata;
            if (string.IsNullOrEmpty(taskStateJson))
            {
                _logger.LogDebug("No task state found in session {SessionId}", sessionId);
                return null;
            }
            
            var taskState = JsonSerializer.Deserialize<TaskState>(taskStateJson);
            _logger.LogDebug("Retrieved task state for session {SessionId}: {SubtaskCount} subtasks", 
                sessionId, taskState?.Subtasks.Count ?? 0);
            
            return taskState;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get task state for session {SessionId}", sessionId);
            return null;
        }
    }
    
    public async Task SaveTaskStateAsync(string sessionId, TaskState taskState)
    {
        try
        {
            var session = await _sessionManager.GetSessionAsync(sessionId);
            if (session == null)
            {
                _logger.LogError("Cannot save task state - session {SessionId} not found", sessionId);
                return;
            }
            
            // Serialize task state to JSON and store in session metadata
            var taskStateJson = JsonSerializer.Serialize(taskState, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            session.Metadata = taskStateJson;
            
            // Update task progress information
            UpdateSessionTaskProgress(session, taskState);
            
            await _sessionManager.SaveSessionAsync(session);
            
            _logger.LogDebug("Saved task state for session {SessionId}: {CompletedCount}/{TotalCount} subtasks completed",
                sessionId, taskState.GetCompletedCount(), taskState.Subtasks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save task state for session {SessionId}", sessionId);
            throw;
        }
    }
    
    public async Task<TaskState> CompleteSubtaskAsync(string sessionId, int stepNumber, string completionSummary, string? evidence = null, Dictionary<string, object>? context = null)
    {
        var taskState = await GetTaskStateAsync(sessionId);
        if (taskState == null)
        {
            throw new InvalidOperationException($"No task state found for session {sessionId}");
        }
        
        _logger.LogInformation("Completing subtask {StepNumber} for session {SessionId}: {Summary}", 
            stepNumber, sessionId, completionSummary);
        
        taskState.CompleteSubtask(stepNumber, completionSummary, evidence, context);
        await SaveTaskStateAsync(sessionId, taskState);
        
        return taskState;
    }
    
    public async Task<TaskState> StartSubtaskAsync(string sessionId, int stepNumber)
    {
        var taskState = await GetTaskStateAsync(sessionId);
        if (taskState == null)
        {
            throw new InvalidOperationException($"No task state found for session {sessionId}");
        }
        
        _logger.LogInformation("Starting subtask {StepNumber} for session {SessionId}", stepNumber, sessionId);
        
        taskState.StartSubtask(stepNumber);
        await SaveTaskStateAsync(sessionId, taskState);
        
        return taskState;
    }
    
    public async Task<TaskState> UpdateSubtaskNotesAsync(string sessionId, int stepNumber, string? notes = null, string? progressUpdate = null)
    {
        var taskState = await GetTaskStateAsync(sessionId);
        if (taskState == null)
        {
            throw new InvalidOperationException($"No task state found for session {sessionId}");
        }
        
        var subtask = taskState.Subtasks.FirstOrDefault(s => s.StepNumber == stepNumber);
        if (subtask != null)
        {
            if (!string.IsNullOrEmpty(notes))
            {
                subtask.Notes = notes;
            }
            
            if (!string.IsNullOrEmpty(progressUpdate))
            {
                subtask.Notes += (string.IsNullOrEmpty(subtask.Notes) ? "" : "\n") + $"Progress: {progressUpdate}";
            }
            
            taskState.LastUpdatedAt = DateTime.UtcNow;
            await SaveTaskStateAsync(sessionId, taskState);
            
            _logger.LogDebug("Updated notes for subtask {StepNumber} in session {SessionId}", stepNumber, sessionId);
        }
        
        return taskState;
    }
    
    public async Task<SubtaskState?> GetCurrentSubtaskAsync(string sessionId)
    {
        var taskState = await GetTaskStateAsync(sessionId);
        if (taskState == null)
        {
            return null;
        }
        
        var currentSubtask = taskState.GetCurrentSubtask();
        
        if (currentSubtask != null)
        {
            _logger.LogDebug("Current subtask for session {SessionId}: Step {StepNumber} - {Description}", 
                sessionId, currentSubtask.StepNumber, currentSubtask.Description);
        }
        else
        {
            _logger.LogDebug("No current subtask found for session {SessionId} (all may be completed)", sessionId);
        }
        
        return currentSubtask;
    }
    
    public async Task<Dictionary<string, object>> GetAccumulatedContextAsync(string sessionId)
    {
        var taskState = await GetTaskStateAsync(sessionId);
        return taskState?.AccumulatedContext ?? new Dictionary<string, object>();
    }
    
    /// <summary>
    /// Update session task progress fields based on task state
    /// </summary>
    private void UpdateSessionTaskProgress(AgentSession session, TaskState taskState)
    {
        // Update basic task information
        session.TaskTitle = taskState.Task;
        session.TotalSteps = taskState.Subtasks.Count;
        session.CompletedSteps = taskState.GetCompletedCount();
        
        // Update task status
        session.TaskStatus = taskState.Status switch
        {
            TaskCompletionStatus.InProgress => TaskExecutionStatus.InProgress,
            TaskCompletionStatus.Completed => TaskExecutionStatus.Completed,
            TaskCompletionStatus.Failed => TaskExecutionStatus.Failed,
            TaskCompletionStatus.Cancelled => TaskExecutionStatus.Cancelled,
            _ => TaskExecutionStatus.InProgress
        };
        
        // Update current step
        var currentSubtask = taskState.GetCurrentSubtask();
        session.CurrentStep = currentSubtask?.StepNumber ?? 0;
        
        // If no current subtask but there are completed subtasks, we're done
        if (currentSubtask == null && taskState.GetCompletedCount() > 0)
        {
            session.CurrentStep = session.TotalSteps;
        }
        
        // Update progress percentage
        if (session.TotalSteps > 0)
        {
            session.ProgressPercentage = Math.Round((double)session.CompletedSteps / session.TotalSteps, 3);
        }
        
        // Set task start time if not already set and we have subtasks
        if (!session.TaskStartedAt.HasValue && session.TotalSteps > 0)
        {
            session.TaskStartedAt = taskState.CreatedAt;
            session.TaskStatus = TaskExecutionStatus.InProgress;
        }
        
        // Set task completion time if completed
        if (session.TaskStatus == TaskExecutionStatus.Completed && !session.TaskCompletedAt.HasValue)
        {
            session.TaskCompletedAt = DateTime.UtcNow;
            session.ProgressPercentage = 1.0;
            
            // Calculate actual duration
            if (session.TaskStartedAt.HasValue)
            {
                session.ActualDuration = (int)(DateTime.UtcNow - session.TaskStartedAt.Value).TotalMinutes;
            }
        }
        
        // Update task category based on subtask count
        if (string.IsNullOrEmpty(session.TaskCategory))
        {
            session.TaskCategory = session.TotalSteps switch
            {
                <= 3 => "Simple",
                <= 7 => "Medium", 
                <= 15 => "Complex",
                _ => "VeryComplex"
            };
        }
        
        session.LastUpdatedAt = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Initialize task state from a task plan and update session accordingly
    /// </summary>
    public async Task<TaskState> InitializeTaskStateAsync(string sessionId, TaskPlan taskPlan)
    {
        var taskState = CreateTaskState(taskPlan);
        
        var session = await _sessionManager.GetSessionAsync(sessionId);
        if (session != null)
        {
            // Update session with task information
            session.UpdateTaskInfo(taskPlan);
            UpdateSessionTaskProgress(session, taskState);
            
            // Store the task plan in CurrentPlan
            session.SetCurrentPlan(taskPlan);
            
            await _sessionManager.SaveSessionAsync(session);
        }
        
        await SaveTaskStateAsync(sessionId, taskState);
        
        _logger.LogInformation("Initialized task state for session {SessionId}: {TaskTitle} with {StepCount} steps", 
            sessionId, taskPlan.Task, taskPlan.Steps.Count);
        
        return taskState;
    }
}