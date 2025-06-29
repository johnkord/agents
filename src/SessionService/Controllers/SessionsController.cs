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

    // --------------------------------------------------------------------
    // GET /api/sessions
    // --------------------------------------------------------------------
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
                result.Add(new
                {
                    sessionId         = s.SessionId,
                    name              = s.Name,
                    createdAt         = s.CreatedAt,
                    lastUpdatedAt     = s.LastUpdatedAt,
                    status            = s.Status.ToString(),
                    activityCount,
                    taskStateMarkdown = s.TaskStateMarkdown
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

    // --------------------------------------------------------------------
    // GET /api/sessions/{sessionId}
    // --------------------------------------------------------------------
    [HttpGet("{sessionId}")]
    public async Task<IActionResult> GetSession(string sessionId)
    {
        try
        {
            var session = await _sessionManager.GetSessionAsync(sessionId);
            if (session == null)
                return NotFound();

            var activities = await _sessionManager.GetSessionActivitiesAsync(sessionId);
            var dto = new SessionDetailsDto(session, activities, session.TaskStateMarkdown); // record exists elsewhere
            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to retrieve session" });
        }
    }

    // --------------------------------------------------------------------
    // GET /api/sessions/{sessionId}/activities  (needed by client)
    // --------------------------------------------------------------------
    [HttpGet("{sessionId}/activities")]
    public async Task<IActionResult> GetSessionActivities(string sessionId)
    {
        try
        {
            var activities = await _sessionManager.GetSessionActivitiesAsync(sessionId);
            return Ok(activities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get activities for session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to get session activities" });
        }
    }

    // --------------------------------------------------------------------
    // POST /api/sessions
    // --------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> CreateSession([FromBody] CreateSessionRequest request)
    {
        try
        {
            var session = await _sessionManager.CreateSessionAsync(request?.Name ?? string.Empty);

            var result = new
            {
                sessionId     = session.SessionId,
                name          = session.Name,
                createdAt     = session.CreatedAt,
                lastUpdatedAt = session.LastUpdatedAt,
                status        = session.Status.ToString()
            };

            return CreatedAtAction(nameof(GetSession), new { sessionId = session.SessionId }, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create session");
            return StatusCode(500, new { error = "Failed to create session" });
        }
    }

    // --------------------------------------------------------------------
    // PUT /api/sessions/{sessionId}
    // --------------------------------------------------------------------
    [HttpPut("{sessionId}")]
    public async Task<IActionResult> UpdateSession(string sessionId, [FromBody] UpdateSessionRequest request)
    {
        try
        {
            var session = await _sessionManager.GetSessionAsync(sessionId);
            if (session == null)
                return NotFound();

            // apply updates
            if (!string.IsNullOrWhiteSpace(request.Name))
                session.Name = request.Name;

            if (request.ConversationMessages != null)
                session.SetConversationMessages(request.ConversationMessages);

            if (!string.IsNullOrWhiteSpace(request.ConfigurationSnapshot))
                session.ConfigurationSnapshot = request.ConfigurationSnapshot;

            if (!string.IsNullOrWhiteSpace(request.Metadata))
                session.Metadata = request.Metadata;

            if (request.Status.HasValue)
                session.Status = request.Status.Value;

            if (!string.IsNullOrWhiteSpace(request.TaskStateMarkdown))
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

    // --------------------------------------------------------------------
    // POST /api/sessions/{sessionId}/activities
    // --------------------------------------------------------------------
    [HttpPost("{sessionId}/activities")]
    public async Task<IActionResult> AddSessionActivity(string sessionId, [FromBody] SessionActivity activity)
    {
        try
        {
            var session = await _sessionManager.GetSessionAsync(sessionId);
            if (session == null)
                return NotFound();

            activity.SessionId = sessionId;
            if (string.IsNullOrWhiteSpace(activity.ActivityId))
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

    // --------------------------------------------------------------------
    // DELETE /api/sessions/{sessionId}
    // --------------------------------------------------------------------
    [HttpDelete("{sessionId}")]
    public async Task<IActionResult> DeleteSession(string sessionId)
    {
        try
        {
            var deleted = await _sessionManager.DeleteSessionAsync(sessionId);
            return deleted ? NoContent() : NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to delete session" });
        }
    }

    // --------------------------------------------------------------------
    // POST /api/sessions/{sessionId}/archive
    // --------------------------------------------------------------------
    [HttpPost("{sessionId}/archive")]
    public async Task<IActionResult> ArchiveSession(string sessionId)
    {
        try
        {
            var archived = await _sessionManager.ArchiveSessionAsync(sessionId);
            return archived ? NoContent() : NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to archive session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to archive session" });
        }
    }

    // --------------------------------------------------------------------
    // GET /api/sessions/by-name/{name}
    // --------------------------------------------------------------------
    [HttpGet("by-name/{name}")]
    public async Task<IActionResult> GetSessionByName(string name)
    {
        try
        {
            var session = await _sessionManager.GetSessionByNameAsync(name);
            if (session == null)
                return NotFound();

            var result = new
            {
                sessionId             = session.SessionId,
                name                  = session.Name,
                createdAt             = session.CreatedAt,
                lastUpdatedAt         = session.LastUpdatedAt,
                status                = session.Status.ToString(),
                conversationState     = session.ConversationState,
                configurationSnapshot = session.ConfigurationSnapshot,
                metadata              = session.Metadata,
                taskStateMarkdown     = session.TaskStateMarkdown
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

// ------------------------------------------------------------------------
// DTOs
// ------------------------------------------------------------------------
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
    public string? TaskStateMarkdown { get; set; }
}

// ------------------------------------------------------------------------
// Added DTO to resolve missing type error
// ------------------------------------------------------------------------
public record SessionDetailsDto
{
    public string SessionId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime LastUpdatedAt { get; init; }
    public string Status { get; init; } = string.Empty;
    public IEnumerable<SessionActivity> Activities { get; init; } = new List<SessionActivity>();
    public string? TaskStateMarkdown { get; init; }

    // --- fix: use AgentSession instead of missing Session type ---------------
    public SessionDetailsDto(AgentSession session,
                             IEnumerable<SessionActivity> activities,
                             string? taskStateMarkdown)
    {
        SessionId         = session.SessionId;
        Name              = session.Name;
        CreatedAt         = session.CreatedAt;
        LastUpdatedAt     = session.LastUpdatedAt;
        Status            = session.Status.ToString();
        Activities        = activities;
        TaskStateMarkdown = taskStateMarkdown;
    }
}