using Microsoft.AspNetCore.Mvc;
using Common.Interfaces.Session;
using Common.Models.Session;

namespace SessionService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SessionsController : ControllerBase
{
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<SessionsController> _logger;

    public SessionsController(ISessionManager sessionManager, ILogger<SessionsController> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllSessions()
    {
        try
        {
            var sessions = await _sessionManager.ListSessionsAsync();
            
            var result = sessions.Select(s => new 
            {
                sessionId = s.SessionId,
                name = s.Name,
                createdAt = s.CreatedAt,
                lastUpdatedAt = s.LastUpdatedAt,
                status = s.Status.ToString(),
                activityCount = s.GetActivityLog().Count,
                taskTitle = s.TaskTitle,
                taskStatus = s.TaskStatus.ToString(),
                currentStep = s.CurrentStep,
                totalSteps = s.TotalSteps,
                completedSteps = s.CompletedSteps,
                progressPercentage = s.ProgressPercentage,
                taskStartedAt = s.TaskStartedAt,
                taskCompletedAt = s.TaskCompletedAt,
                taskCategory = s.TaskCategory,
                taskPriority = s.TaskPriority,
                taskTags = s.GetTaskTagsList(),
                estimatedDuration = s.EstimatedDuration,
                actualDuration = s.ActualDuration,
                progressSummary = s.GetTaskProgressSummary()
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve sessions");
            return StatusCode(500, new { error = "Failed to retrieve sessions" });
        }
    }

    [HttpGet("{sessionId}")]
    public async Task<IActionResult> GetSession(string sessionId)
    {
        try
        {
            var session = await _sessionManager.GetSessionAsync(sessionId);
            if (session == null)
            {
                return NotFound();
            }

            var activities = session.GetActivityLog();
            
            var result = new 
            {
                sessionId = session.SessionId,
                name = session.Name,
                createdAt = session.CreatedAt,
                lastUpdatedAt = session.LastUpdatedAt,
                status = session.Status.ToString(),
                conversationState = session.ConversationState,
                configurationSnapshot = session.ConfigurationSnapshot,
                metadata = session.Metadata,
                currentPlan = session.CurrentPlan,
                taskTitle = session.TaskTitle,
                taskStatus = session.TaskStatus.ToString(),
                currentStep = session.CurrentStep,
                totalSteps = session.TotalSteps,
                completedSteps = session.CompletedSteps,
                progressPercentage = session.ProgressPercentage,
                taskStartedAt = session.TaskStartedAt,
                taskCompletedAt = session.TaskCompletedAt,
                taskCategory = session.TaskCategory,
                taskPriority = session.TaskPriority,
                taskTags = session.GetTaskTagsList(),
                estimatedDuration = session.EstimatedDuration,
                actualDuration = session.ActualDuration,
                progressSummary = session.GetTaskProgressSummary(),
                activities = activities.Select(a => new 
                {
                    activityId = a.ActivityId,
                    timestamp = a.Timestamp,
                    activityType = a.ActivityType,
                    description = a.Description,
                    success = a.Success,
                    durationMs = a.DurationMs,
                    errorMessage = a.ErrorMessage,
                    data = a.Data
                }).OrderBy(a => a.timestamp)
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to retrieve session" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateSession([FromBody] CreateSessionRequest request)
    {
        try
        {
            var session = await _sessionManager.CreateSessionAsync(request.Name ?? string.Empty);
            
            var result = new 
            {
                sessionId = session.SessionId,
                name = session.Name,
                createdAt = session.CreatedAt,
                lastUpdatedAt = session.LastUpdatedAt,
                status = session.Status.ToString()
            };

            return CreatedAtAction(nameof(GetSession), new { sessionId = session.SessionId }, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create session");
            return StatusCode(500, new { error = "Failed to create session" });
        }
    }

    [HttpPut("{sessionId}")]
    public async Task<IActionResult> UpdateSession(string sessionId, [FromBody] UpdateSessionRequest request)
    {
        try
        {
            var session = await _sessionManager.GetSessionAsync(sessionId);
            if (session == null)
            {
                return NotFound();
            }

            // Update fields
            if (!string.IsNullOrEmpty(request.Name))
                session.Name = request.Name;
            
            if (request.ConversationMessages != null)
                session.SetConversationMessages(request.ConversationMessages);
            
            if (!string.IsNullOrEmpty(request.ConfigurationSnapshot))
                session.ConfigurationSnapshot = request.ConfigurationSnapshot;
            
            if (!string.IsNullOrEmpty(request.Metadata))
                session.Metadata = request.Metadata;
            
            if (request.Status.HasValue)
                session.Status = request.Status.Value;
            
            if (request.CurrentPlan != null)
                session.SetCurrentPlan(request.CurrentPlan);
            
            if (request.Activities != null)
                session.SetActivityLog(request.Activities);
                
            // Update new task-related fields
            if (!string.IsNullOrEmpty(request.TaskTitle))
                session.TaskTitle = request.TaskTitle;
                
            if (request.TaskStatus.HasValue)
                session.TaskStatus = request.TaskStatus.Value;
                
            if (request.CurrentStep.HasValue)
                session.CurrentStep = request.CurrentStep.Value;
                
            if (request.TotalSteps.HasValue)
                session.TotalSteps = request.TotalSteps.Value;
                
            if (request.CompletedSteps.HasValue)
                session.CompletedSteps = request.CompletedSteps.Value;
                
            if (request.ProgressPercentage.HasValue)
                session.ProgressPercentage = request.ProgressPercentage.Value;
                
            if (request.TaskStartedAt.HasValue)
                session.TaskStartedAt = request.TaskStartedAt;
                
            if (request.TaskCompletedAt.HasValue)
                session.TaskCompletedAt = request.TaskCompletedAt;
                
            if (!string.IsNullOrEmpty(request.TaskCategory))
                session.TaskCategory = request.TaskCategory;
                
            if (request.TaskPriority.HasValue)
                session.TaskPriority = request.TaskPriority.Value;
                
            if (request.TaskTags != null)
                session.SetTaskTags(request.TaskTags);
                
            if (request.EstimatedDuration.HasValue)
                session.EstimatedDuration = request.EstimatedDuration.Value;
                
            if (request.ActualDuration.HasValue)
                session.ActualDuration = request.ActualDuration.Value;

            session.LastUpdatedAt = DateTime.UtcNow;
            await _sessionManager.SaveSessionAsync(session);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to update session" });
        }
    }

    [HttpDelete("{sessionId}")]
    public async Task<IActionResult> DeleteSession(string sessionId)
    {
        try
        {
            var deleted = await _sessionManager.DeleteSessionAsync(sessionId);
            if (!deleted)
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to delete session" });
        }
    }

    [HttpPost("{sessionId}/archive")]
    public async Task<IActionResult> ArchiveSession(string sessionId)
    {
        try
        {
            var archived = await _sessionManager.ArchiveSessionAsync(sessionId);
            if (!archived)
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to archive session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to archive session" });
        }
    }

    [HttpGet("by-name/{name}")]
    public async Task<IActionResult> GetSessionByName(string name)
    {
        try
        {
            var session = await _sessionManager.GetSessionByNameAsync(name);
            if (session == null)
            {
                return NotFound();
            }

            var result = new 
            {
                sessionId = session.SessionId,
                name = session.Name,
                createdAt = session.CreatedAt,
                lastUpdatedAt = session.LastUpdatedAt,
                status = session.Status.ToString(),
                conversationState = session.ConversationState,
                configurationSnapshot = session.ConfigurationSnapshot,
                metadata = session.Metadata,
                currentPlan = session.CurrentPlan
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve session by name {Name}", name);
            return StatusCode(500, new { error = "Failed to retrieve session by name" });
        }
    }
    
    /// <summary>
    /// Get sessions filtered by task status
    /// </summary>
    [HttpGet("by-task-status/{taskStatus}")]
    public async Task<IActionResult> GetSessionsByTaskStatus(TaskExecutionStatus taskStatus)
    {
        try
        {
            var sessions = await _sessionManager.GetSessionsByTaskStatusAsync(taskStatus);
            
            var result = sessions.Select(s => new 
            {
                sessionId = s.SessionId,
                name = s.Name,
                taskTitle = s.TaskTitle,
                taskStatus = s.TaskStatus.ToString(),
                progressPercentage = s.ProgressPercentage,
                currentStep = s.CurrentStep,
                totalSteps = s.TotalSteps,
                taskStartedAt = s.TaskStartedAt,
                taskCompletedAt = s.TaskCompletedAt,
                lastUpdatedAt = s.LastUpdatedAt
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve sessions by task status {TaskStatus}", taskStatus);
            return StatusCode(500, new { error = "Failed to retrieve sessions by task status" });
        }
    }
    
    /// <summary>
    /// Get active tasks (in progress)
    /// </summary>
    [HttpGet("active")]
    public async Task<IActionResult> GetActiveTasks()
    {
        try
        {
            var sessions = await _sessionManager.GetActiveTasksAsync();
            
            var result = sessions.Select(s => new 
            {
                sessionId = s.SessionId,
                name = s.Name,
                taskTitle = s.TaskTitle,
                progressPercentage = s.ProgressPercentage,
                currentStep = s.CurrentStep,
                totalSteps = s.TotalSteps,
                taskCategory = s.TaskCategory,
                taskPriority = s.TaskPriority,
                taskStartedAt = s.TaskStartedAt,
                lastUpdatedAt = s.LastUpdatedAt,
                progressSummary = s.GetTaskProgressSummary()
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve active tasks");
            return StatusCode(500, new { error = "Failed to retrieve active tasks" });
        }
    }
    
    /// <summary>
    /// Get completed tasks
    /// </summary>
    [HttpGet("completed")]
    public async Task<IActionResult> GetCompletedTasks()
    {
        try
        {
            var sessions = await _sessionManager.GetCompletedTasksAsync();
            
            var result = sessions.Select(s => new 
            {
                sessionId = s.SessionId,
                name = s.Name,
                taskTitle = s.TaskTitle,
                completedSteps = s.CompletedSteps,
                totalSteps = s.TotalSteps,
                taskCategory = s.TaskCategory,
                taskPriority = s.TaskPriority,
                taskStartedAt = s.TaskStartedAt,
                taskCompletedAt = s.TaskCompletedAt,
                actualDuration = s.ActualDuration,
                lastUpdatedAt = s.LastUpdatedAt
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve completed tasks");
            return StatusCode(500, new { error = "Failed to retrieve completed tasks" });
        }
    }
    
    /// <summary>
    /// Get sessions by progress range (0.0 to 1.0)
    /// </summary>
    [HttpGet("by-progress")]
    public async Task<IActionResult> GetSessionsByProgress([FromQuery] double minProgress = 0.0, [FromQuery] double maxProgress = 1.0)
    {
        try
        {
            var sessions = await _sessionManager.GetSessionsByProgressRangeAsync(minProgress, maxProgress);
            
            var result = sessions.Select(s => new 
            {
                sessionId = s.SessionId,
                name = s.Name,
                taskTitle = s.TaskTitle,
                progressPercentage = s.ProgressPercentage,
                currentStep = s.CurrentStep,
                totalSteps = s.TotalSteps,
                taskStatus = s.TaskStatus.ToString(),
                lastUpdatedAt = s.LastUpdatedAt,
                progressSummary = s.GetTaskProgressSummary()
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve sessions by progress range");
            return StatusCode(500, new { error = "Failed to retrieve sessions by progress range" });
        }
    }
    
    /// <summary>
    /// Get sessions by category
    /// </summary>
    [HttpGet("by-category/{category}")]
    public async Task<IActionResult> GetSessionsByCategory(string category)
    {
        try
        {
            var sessions = await _sessionManager.GetSessionsByCategoryAsync(category);
            
            var result = sessions.Select(s => new 
            {
                sessionId = s.SessionId,
                name = s.Name,
                taskTitle = s.TaskTitle,
                taskCategory = s.TaskCategory,
                taskPriority = s.TaskPriority,
                progressPercentage = s.ProgressPercentage,
                taskStatus = s.TaskStatus.ToString(),
                lastUpdatedAt = s.LastUpdatedAt
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve sessions by category {Category}", category);
            return StatusCode(500, new { error = "Failed to retrieve sessions by category" });
        }
    }
    
    /// <summary>
    /// Get sessions by tags (comma-separated list)
    /// </summary>
    [HttpGet("by-tags")]
    public async Task<IActionResult> GetSessionsByTags([FromQuery] string tags)
    {
        try
        {
            if (string.IsNullOrEmpty(tags))
                return BadRequest(new { error = "Tags parameter is required" });
                
            var tagArray = tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                              .Select(t => t.Trim())
                              .ToArray();
                              
            var sessions = await _sessionManager.GetSessionsByTagsAsync(tagArray);
            
            var result = sessions.Select(s => new 
            {
                sessionId = s.SessionId,
                name = s.Name,
                taskTitle = s.TaskTitle,
                taskTags = s.GetTaskTagsList(),
                taskCategory = s.TaskCategory,
                progressPercentage = s.ProgressPercentage,
                taskStatus = s.TaskStatus.ToString(),
                lastUpdatedAt = s.LastUpdatedAt
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve sessions by tags {Tags}", tags);
            return StatusCode(500, new { error = "Failed to retrieve sessions by tags" });
        }
    }
    
    /// <summary>
    /// Get sessions by priority level
    /// </summary>
    [HttpGet("by-priority/{priority}")]
    public async Task<IActionResult> GetSessionsByPriority(int priority)
    {
        try
        {
            if (priority < 1 || priority > 5)
                return BadRequest(new { error = "Priority must be between 1 and 5" });
                
            var sessions = await _sessionManager.GetSessionsByPriorityAsync(priority);
            
            var result = sessions.Select(s => new 
            {
                sessionId = s.SessionId,
                name = s.Name,
                taskTitle = s.TaskTitle,
                taskPriority = s.TaskPriority,
                taskCategory = s.TaskCategory,
                progressPercentage = s.ProgressPercentage,
                taskStatus = s.TaskStatus.ToString(),
                lastUpdatedAt = s.LastUpdatedAt
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve sessions by priority {Priority}", priority);
            return StatusCode(500, new { error = "Failed to retrieve sessions by priority" });
        }
    }
}

public class CreateSessionRequest
{
    public string? Name { get; set; }
}

public class UpdateSessionRequest
{
    public string? Name { get; set; }
    public List<object>? ConversationMessages { get; set; }
    public string? ConfigurationSnapshot { get; set; }
    public string? Metadata { get; set; }
    public SessionStatus? Status { get; set; }
    public TaskPlan? CurrentPlan { get; set; }
    public List<SessionActivity>? Activities { get; set; }
    public string? TaskTitle { get; set; }
    public TaskExecutionStatus? TaskStatus { get; set; }
    public int? CurrentStep { get; set; }
    public int? TotalSteps { get; set; }
    public int? CompletedSteps { get; set; }
    public double? ProgressPercentage { get; set; }
    public DateTime? TaskStartedAt { get; set; }
    public DateTime? TaskCompletedAt { get; set; }
    public string? TaskCategory { get; set; }
    public int? TaskPriority { get; set; }
    public List<string>? TaskTags { get; set; }
    public int? EstimatedDuration { get; set; }
    public int? ActualDuration { get; set; }
}