using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAIIntegration.Model;
using Common.Models.Session;
using Common.Interfaces.Session;

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
    
    /// <summary>
    /// Enable enhanced logging that includes full prompts and tool definitions
    /// </summary>
    public bool EnableEnhancedLogging { get; set; } = true;
    
    /// <summary>
    /// Maximum size of data to log before truncation (in characters)
    /// </summary>
    public int MaxDataSize { get; set; } = 50000;
    
    /// <summary>
    /// Maximum number of messages to include in OpenAI request logging
    /// </summary>
    public int MaxMessagesInLog { get; set; } = 50;

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
            if (EnableEnhancedLogging)
            {
                // Enhanced logging with full prompts and tool definitions
                var requestData = new 
                { 
                    Model = request.Model,
                    InputCount = GetInputCount(request.Input),
                    ToolCount = request.Tools?.Length ?? 0,
                    ToolNames = request.Tools?.Select(t => t.Name).ToArray(),
                    ToolChoice = request.ToolChoice,
                    Source = "SessionAwareOpenAIService",
                    // Include full request details for comprehensive audit trail
                    FullRequest = new
                    {
                        Model = request.Model,
                        Messages = GetInputMessages(request.Input),
                        Tools = request.Tools?.Select(t => new { 
                            t.Name, 
                            t.Description, 
                            ParameterCount = t.Parameters?.ToString()?.Length ?? 0,
                            Parameters = t.Parameters 
                        }).ToArray(),
                        ToolChoice = request.ToolChoice,
                        Instructions = request.Instructions,
                        Temperature = request.Temperature,
                        MaxOutputTokens = request.MaxOutputTokens,
                        Metadata = request.Metadata
                    }
                };
                
                requestActivityId = _activityLogger.StartActivity(
                    ActivityTypes.OpenAIRequest,
                    "OpenAI API request via SessionAwareOpenAIService",
                    requestData);
            }
            else
            {
                // Basic logging without full request details
                var requestData = new 
                { 
                    Model = request.Model,
                    InputCount = GetInputCount(request.Input),
                    ToolCount = request.Tools?.Length ?? 0,
                    ToolNames = request.Tools?.Select(t => t.Name).ToArray(),
                    ToolChoice = request.ToolChoice,
                    Source = "SessionAwareOpenAIService"
                };
                
                requestActivityId = _activityLogger.StartActivity(
                    ActivityTypes.OpenAIRequest,
                    "OpenAI API request via SessionAwareOpenAIService",
                    requestData);
            }
                
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
                
                if (EnableEnhancedLogging)
                {
                    // Enhanced response logging with full tool calls and text responses
                    var responseData = new
                    {
                        Model = request.Model,
                        OutputItemCount = response.Output?.Count() ?? 0,
                        HasToolCalls = response.Output?.Any(o => o is FunctionToolCall || o is McpToolCall) ?? false,
                        Source = "SessionAwareOpenAIService",
                        // Include detailed response data for comprehensive audit trail
                        FullResponse = new
                        {
                            Output = response.Output?.Select(output => output switch
                            {
                                OutputMessage msg => (object)new { 
                                    Type = "message",
                                    msg.Role, 
                                    Content = SessionActivity.TruncateString(msg.Content?.ToString(), 5000) 
                                },
                                FunctionToolCall ftc => (object)new { 
                                    Type = "function_call",
                                    ftc.Id,
                                    ftc.Name, 
                                    Arguments = SessionActivity.TruncateString(ftc.Arguments?.ToString(), 5000),
                                    ftc.Status
                                },
                                McpToolCall mtc => (object)new { 
                                    Type = "mcp_call",
                                    mtc.Id,
                                    mtc.Name, 
                                    Arguments = SessionActivity.TruncateString(mtc.Arguments, 5000),
                                    mtc.ServerLabel,
                                    mtc.Error,
                                    Output = SessionActivity.TruncateString(mtc.Output, 5000)
                                },
                                FileSearchToolCall fsc => (object)new {
                                    Type = "file_search_call",
                                    fsc.Id,
                                    fsc.Queries,
                                    fsc.Status
                                },
                                WebSearchToolCall wsc => (object)new {
                                    Type = "web_search_call", 
                                    wsc.Id,
                                    wsc.Queries,
                                    wsc.Status
                                },
                                _ => (object)new { 
                                    Type = "unknown",
                                    Raw = SessionActivity.TruncateString(output.ToString(), 5000) 
                                }
                            }).ToArray(),
                            Usage = response.Usage != null ? new
                            {
                                response.Usage.InputTokens,
                                response.Usage.OutputTokens,
                                response.Usage.TotalTokens
                            } : null,
                            Status = response.Status,
                            Id = response.Id
                        }
                    };
                    
                    await _activityLogger.LogActivityAsync(
                        ActivityTypes.OpenAIResponse,
                        "OpenAI API response via SessionAwareOpenAIService",
                        responseData);
                }
                else
                {
                    // Basic response logging
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
                }
                    
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

    /// <summary>
    /// Safely determine how many input items were provided in the request.
    /// </summary>
    private static int GetInputCount(object? input) =>
        input switch
        {
            null => 0,
            IEnumerable<object> enumerable => enumerable.Count(),
            _ => 1 // treat single primitive/object as one item
        };
    
    /// <summary>
    /// Extract messages from the Input property for logging
    /// </summary>
    private object GetInputMessages(object? input)
    {
        if (input == null) 
            return new object[0];
            
        try
        {
            // Handle different input types
            if (input is IEnumerable<object> enumerable)
            {
                var messages = enumerable.Take(MaxMessagesInLog);
                
                // Try to extract meaningful information from messages
                return messages.Select(msg => 
                {
                    try
                    {
                        // If it's already a structured message object, truncate content
                        var msgStr = msg.ToString();
                        if (msgStr != null && msgStr.Contains("role") && msgStr.Contains("content"))
                        {
                            // Parse as JSON and truncate content if needed
                            var jsonElement = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(msg));
                            if (jsonElement.TryGetProperty("content", out var content))
                            {
                                var truncatedContent = SessionActivity.TruncateString(content.ToString(), 5000);
                                return new { 
                                    role = jsonElement.TryGetProperty("role", out var role) ? role.ToString() : "unknown",
                                    content = truncatedContent
                                };
                            }
                        }
                        
                        return new { 
                            role = "unknown", 
                            content = SessionActivity.TruncateString(msgStr, 5000) 
                        };
                    }
                    catch
                    {
                        return new { 
                            role = "unknown", 
                            content = SessionActivity.TruncateString(msg?.ToString(), 5000) 
                        };
                    }
                }).ToArray();
            }
            
            // Single input item
            return new[] { new { 
                role = "user", 
                content = SessionActivity.TruncateString(input.ToString(), 5000) 
            }};
        }
        catch
        {
            // Fallback for any parsing issues
            return new[] { new { 
                role = "unknown", 
                content = SessionActivity.TruncateString(input?.ToString(), 5000) 
            }};
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