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
            var result = new List<object>();
            foreach (var s in sessions)
            {
                var activityCount = (await _sessionManager.GetSessionActivitiesAsync(s.SessionId)).Count;
                result.Add(new {
                    sessionId = s.SessionId,
                    name = s.Name,
                    createdAt = s.CreatedAt,
                    lastUpdatedAt = s.LastUpdatedAt,
                    status = s.Status.ToString(),
                    activityCount
                });
            }
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
            var activities = await _sessionManager.GetSessionActivitiesAsync(sessionId);
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
            if (!string.IsNullOrEmpty(request.TaskStateMarkdown))
                session.TaskStateMarkdown = request.TaskStateMarkdown;
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

    // New endpoint: Add activity to a session
    [HttpPost("{sessionId}/activities")]
    public async Task<IActionResult> AddSessionActivity(string sessionId, [FromBody] SessionActivity activity)
    {
        try
        {
            var session = await _sessionManager.GetSessionAsync(sessionId);
            if (session == null)
                return NotFound();
            // Ensure the activity is associated with the correct session
            activity.SessionId = sessionId;
            if (string.IsNullOrEmpty(activity.ActivityId))
                activity.ActivityId = Guid.NewGuid().ToString();
            if (activity.Timestamp == default)
                activity.Timestamp = DateTime.UtcNow;
            await _sessionManager.AddSessionActivityAsync(sessionId, activity);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add activity to session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to add activity" });
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
                metadata = session.Metadata
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve session by name {Name}", name);
            return StatusCode(500, new { error = "Failed to retrieve session by name" });
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
    public List<SessionActivity>? Activities { get; set; }

    public string? TaskStateMarkdown { get; set; }
}