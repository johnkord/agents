using Microsoft.Extensions.Logging;
using Common.Interfaces.Session;

namespace Common.Services.Session;

/// <summary>
/// Implementation of task state management using markdown-based task management
/// </summary>
public class TaskStateManager : ITaskStateManager
{
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<TaskStateManager> _logger;
    private readonly IMarkdownTaskStateManager _markdownTaskStateManager;
    
    public TaskStateManager(ISessionManager sessionManager, ILogger<TaskStateManager> logger, IMarkdownTaskStateManager markdownTaskStateManager)
    {
        _sessionManager = sessionManager;
        _logger = logger;
        _markdownTaskStateManager = markdownTaskStateManager ?? throw new ArgumentNullException(nameof(markdownTaskStateManager));
    }
    
    public async Task<SubtaskInfo?> GetCurrentSubtaskAsync(string sessionId)
    {
        _logger.LogDebug("Getting current subtask for session {SessionId}", sessionId);
        
        try
        {
            return await _markdownTaskStateManager.GetCurrentSubtaskFromMarkdownAsync(sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get current subtask for session {SessionId}", sessionId);
            return null;
        }
    }
    
    public async Task CompleteSubtaskAsync(string sessionId, string subtaskDescription, string completionSummary, string? evidence = null, Dictionary<string, object>? context = null)
    {
        _logger.LogInformation("Completing subtask for session {SessionId}: {SubtaskDescription}", sessionId, subtaskDescription);
        
        try
        {
            var completionResult = completionSummary;
            if (!string.IsNullOrEmpty(evidence))
            {
                completionResult += $"\n\nEvidence: {evidence}";
            }
            
            if (context != null && context.Count > 0)
            {
                var contextInfo = string.Join(", ", context.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
                completionResult += $"\n\nContext: {contextInfo}";
            }
            
            await _markdownTaskStateManager.CompleteSubtaskInMarkdownAsync(sessionId, subtaskDescription, completionResult);
            _logger.LogInformation("Successfully completed subtask for session {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete subtask for session {SessionId}", sessionId);
            throw;
        }
    }
    
    public async Task UpdatePlanIterativelyAsync(string sessionId, string executionFeedback, string? currentContext = null)
    {
        _logger.LogInformation("Updating plan iteratively for session {SessionId}", sessionId);
        
        try
        {
            await _markdownTaskStateManager.UpdatePlanIterativelyAsync(sessionId, executionFeedback, currentContext);
            _logger.LogInformation("Successfully updated plan for session {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update plan iteratively for session {SessionId}", sessionId);
            throw;
        }
    }
    
    public async Task InitializeTaskStateAsync(string sessionId, string taskDescription)
    {
        _logger.LogInformation("Initializing task state for session {SessionId}: {TaskDescription}", sessionId, taskDescription);
        
        try
        {
            await _markdownTaskStateManager.InitializeTaskMarkdownAsync(sessionId, taskDescription);
            _logger.LogInformation("Successfully initialized task state for session {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize task state for session {SessionId}", sessionId);
            throw;
        }
    }
    
    public async Task<string> GetTaskMarkdownAsync(string sessionId)
    {
        _logger.LogDebug("Getting task markdown for session {SessionId}", sessionId);
        
        try
        {
            return await _markdownTaskStateManager.GetTaskMarkdownAsync(sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get task markdown for session {SessionId}", sessionId);
            throw;
        }
    }
}