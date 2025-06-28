using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenAIIntegration.Model;
using Common.Models.Session;
using AgentAlpha.Interfaces;

namespace OpenAIIntegration;

/// <summary>
/// Session-aware decorator for OpenAI service that automatically logs all requests to the activity log
/// </summary>
public interface ISessionAwareOpenAIService : IOpenAIResponsesService
{
    /// <summary>
    /// Set the activity logger for the current session context
    /// </summary>
    void SetActivityLogger(ISessionActivityLogger? activityLogger);
}

/// <summary>
/// Decorator implementation that adds session activity logging to OpenAI requests
/// </summary>
public class SessionAwareOpenAIService : ISessionAwareOpenAIService, IDisposable
{
    private readonly IOpenAIResponsesService _innerService;
    private readonly ILogger<SessionAwareOpenAIService> _logger;
    private ISessionActivityLogger? _activityLogger;

    public SessionAwareOpenAIService(IOpenAIResponsesService innerService, ILogger<SessionAwareOpenAIService> logger)
    {
        _innerService = innerService ?? throw new ArgumentNullException(nameof(innerService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void SetActivityLogger(ISessionActivityLogger? activityLogger)
    {
        _activityLogger = activityLogger;
        _logger.LogDebug("Activity logger {Status} for session-aware OpenAI service", 
            activityLogger != null ? "set" : "cleared");
    }

    public async Task<ResponsesCreateResponse> CreateResponseAsync(
        ResponsesCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        string? requestActivityId = null;
        
        // Start activity logging if available
        if (_activityLogger != null)
        {
            var requestData = new 
            { 
                Model = request.Model,
                InputCount = request.Input?.Length ?? 0,
                ToolCount = request.Tools?.Length ?? 0,
                ToolNames = request.Tools?.Select(t => t.Name).ToArray(),
                ToolChoice = request.ToolChoice,
                Source = "SessionAwareOpenAIService"
            };
            
            requestActivityId = _activityLogger.StartActivity(
                ActivityTypes.OpenAIRequest,
                "OpenAI API request via SessionAwareOpenAIService",
                requestData);
                
            _logger.LogDebug("Started activity logging for OpenAI request: {ActivityId}", requestActivityId);
        }

        try
        {
            // Call the inner service
            var response = await _innerService.CreateResponseAsync(request, cancellationToken);
            
            // Complete activity logging on success
            if (_activityLogger != null && requestActivityId != null)
            {
                await _activityLogger.CompleteActivityAsync(requestActivityId, new 
                { 
                    ResponseReceived = true,
                    OutputItemCount = response.Output?.Count() ?? 0,
                    HasToolCalls = response.Output?.Any(o => o is FunctionToolCall || o is McpToolCall) ?? false
                });
                
                // Log the response as a separate activity
                var responseData = new
                {
                    Model = request.Model,
                    OutputItemCount = response.Output?.Count() ?? 0,
                    HasToolCalls = response.Output?.Any(o => o is FunctionToolCall || o is McpToolCall) ?? false,
                    Usage = response.Usage != null ? new
                    {
                        response.Usage.InputTokens,
                        response.Usage.OutputTokens,
                        response.Usage.TotalTokens
                    } : null,
                    Source = "SessionAwareOpenAIService"
                };
                
                await _activityLogger.LogActivityAsync(
                    ActivityTypes.OpenAIResponse,
                    "OpenAI API response via SessionAwareOpenAIService",
                    responseData);
                    
                _logger.LogDebug("Completed activity logging for OpenAI response");
            }
            
            return response;
        }
        catch (Exception ex)
        {
            // Fail activity logging on error
            if (_activityLogger != null && requestActivityId != null)
            {
                await _activityLogger.FailActivityAsync(requestActivityId, ex.Message);
                _logger.LogDebug("Failed activity logging for OpenAI request: {Error}", ex.Message);
            }
            
            throw;
        }
    }

    public void Dispose()
    {
        if (_innerService is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}