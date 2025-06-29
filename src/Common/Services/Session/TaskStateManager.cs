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
    private readonly IMarkdownTaskStateManager? _markdownTaskStateManager;
    
    public TaskStateManager(ISessionManager sessionManager, ILogger<TaskStateManager> logger, IMarkdownTaskStateManager? markdownTaskStateManager = null)
    {
        _sessionManager = sessionManager;
        _logger = logger;
        _markdownTaskStateManager = markdownTaskStateManager;
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
            session.LastUpdatedAt = DateTime.UtcNow;
            
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
        
        // Complete the subtask
        taskState.CompleteSubtask(stepNumber, completionSummary, evidence, context);
        
        // Re-summarize the markdown document with current task state and new observations
        await RegenerateTaskMarkdownAsync(sessionId, taskState, completionSummary);
        
        // Also update the markdown-based state if available
        if (_markdownTaskStateManager != null)
        {
            try
            {
                var subtaskDescription = taskState.Subtasks.FirstOrDefault(s => s.StepNumber == stepNumber)?.Description ?? $"Step {stepNumber}";
                await _markdownTaskStateManager.CompleteSubtaskInMarkdownAsync(sessionId, subtaskDescription, completionSummary);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update markdown task state for completed subtask {StepNumber} in session {SessionId}", stepNumber, sessionId);
            }
        }
        
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
        
        // Regenerate markdown to reflect the subtask being started
        await RegenerateTaskMarkdownAsync(sessionId, taskState, $"Started working on Step {stepNumber}");
        
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
            
            // Regenerate markdown to reflect the updated notes
            var updateDescription = !string.IsNullOrEmpty(progressUpdate) 
                ? $"Progress update for Step {stepNumber}: {progressUpdate}"
                : $"Updated notes for Step {stepNumber}";
            await RegenerateTaskMarkdownAsync(sessionId, taskState, updateDescription);
            
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
    /// Initialize task state from a task plan and create the initial markdown representation
    /// </summary>
    public async Task<TaskState> InitializeTaskStateAsync(string sessionId, TaskPlan taskPlan)
    {
        try
        {
            var taskState = TaskState.FromTaskPlan(taskPlan);
            
            // Use markdown task state manager if available
            if (_markdownTaskStateManager != null)
            {
                try
                {
                    await _markdownTaskStateManager.InitializeTaskMarkdownFromPlanAsync(sessionId, taskPlan);
                    _logger.LogInformation("Initialized markdown-based task state for session {SessionId}", sessionId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to initialize markdown task state, falling back to traditional state for session {SessionId}", sessionId);
                    // Fall back to generating markdown from TaskState
                    await RegenerateTaskMarkdownAsync(sessionId, taskState);
                }
            }
            else
            {
                // Generate initial markdown and store it
                await RegenerateTaskMarkdownAsync(sessionId, taskState);
            }
            
            // Save the task state in metadata for backward compatibility
            await SaveTaskStateAsync(sessionId, taskState);
            
            _logger.LogInformation("Initialized task state for session {SessionId}: {Task} with {StepCount} steps", 
                sessionId, taskPlan.Task, taskPlan.Steps.Count);
            
            return taskState;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize task state for session {SessionId}", sessionId);
            throw;
        }
    }
    
    /// <summary>
    /// Regenerate and update the task markdown document with current state and new observations
    /// </summary>
    private async Task RegenerateTaskMarkdownAsync(string sessionId, TaskState taskState, string? latestObservation = null)
    {
        try
        {
            var session = await _sessionManager.GetSessionAsync(sessionId);
            if (session == null)
            {
                _logger.LogError("Cannot regenerate task markdown - session {SessionId} not found", sessionId);
                return;
            }
            
            // Generate the updated markdown representation
            var markdown = taskState.ToMarkdown();
            
            // Add latest observation/completion summary to the markdown
            if (!string.IsNullOrEmpty(latestObservation))
            {
                markdown += "\n## Latest Update\n\n";
                markdown += $"- {latestObservation}\n";
                markdown += $"- *Updated at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}*\n";
            }
            
            // Store the regenerated markdown in the session's TaskStateMarkdown field
            // This consolidates plan and state management into a single markdown document
            session.TaskStateMarkdown = markdown;
            session.LastUpdatedAt = DateTime.UtcNow;
            
            await _sessionManager.SaveSessionAsync(session);
            
            _logger.LogDebug("Regenerated task markdown for session {SessionId} with {CompletedCount}/{TotalCount} subtasks completed",
                sessionId, taskState.GetCompletedCount(), taskState.Subtasks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to regenerate task markdown for session {SessionId}", sessionId);
        }
    }
}