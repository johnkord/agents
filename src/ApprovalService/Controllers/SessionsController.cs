using Microsoft.AspNetCore.Mvc;
using AgentAlpha.Interfaces;
using AgentAlpha.Services;
using Microsoft.Extensions.Logging;

namespace ApprovalService.Controllers;

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
                activityCount = s.GetActivityLog().Count
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
}