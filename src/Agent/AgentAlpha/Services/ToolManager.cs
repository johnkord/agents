using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using AgentAlpha.Configuration;
using AgentAlpha.Interfaces;
using AgentAlpha.Models;
using Common.Models.Session;
using OpenAIIntegration.Model;
using System.Text.Json;   // new

namespace AgentAlpha.Services;

/// <summary>
/// Implementation of tool discovery, validation, and schema management
/// </summary>
public class ToolManager : IToolManager
{
    private readonly ILogger<ToolManager> _logger;
    private readonly ISessionActivityLogger? _activityLogger;
    private readonly AgentConfiguration _config;

    public ToolManager(ILogger<ToolManager> logger, AgentConfiguration config, ISessionActivityLogger? activityLogger = null)
    {
        _logger = logger;
        _config = config;
        _activityLogger = activityLogger;
    }

    public async Task<IList<McpClientTool>> DiscoverToolsAsync(IConnectionManager connection)
    {
        try
        {
            var tools = await connection.ListToolsAsync();
            _logger.LogInformation("Discovered {Count} tools from MCP server", tools.Count);
            return tools;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover tools");
            throw;
        }
    }

    public IList<McpClientTool> ApplyFilters(IList<McpClientTool> tools, ToolFilterConfig filter)
    {
        var filteredTools = tools.Where(t => filter.ShouldIncludeTool(t.Name)).ToList();
        
        _logger.LogInformation("Applied filters: {Total} tools -> {Filtered} tools", tools.Count, filteredTools.Count);
        
        if (filteredTools.Count != tools.Count)
        {
            var excluded = tools.Where(t => !filter.ShouldIncludeTool(t.Name)).Select(t => t.Name);
            _logger.LogDebug("Excluded tools: {ExcludedTools}", string.Join(", ", excluded));
        }
        
        return filteredTools;
    }

    public OpenAIIntegration.Model.ToolDefinition CreateOpenAiToolDefinition(McpClientTool mcpTool)
    {
        // Use the JSON schema provided by the server instead of guessing
        return new OpenAIIntegration.Model.ToolDefinition
        {
            Type = "function",
            Name = mcpTool.Name,
            Description = mcpTool.Description,
            // The server already advertises the expected input schema; forward it to OpenAI
            Parameters = JsonSerializer.Deserialize<object>(
                mcpTool.ProtocolTool.InputSchema.GetRawText())
        };
    }

    public async Task<string> ExecuteToolAsync(IConnectionManager connection, string toolName, Dictionary<string, object?> arguments)
    {
        string? toolCallActivityId = null;
        if (_activityLogger != null)
        {
            if (_config.ActivityLogging.VerboseTools)
            {
                // Create detailed tool call data for comprehensive logging
                var toolCallData = new 
                { 
                    ToolName = toolName, 
                    Arguments = arguments,
                    // Include full input details for comprehensive audit trail
                    FullInput = new
                    {
                        ToolName = toolName,
                        ArgumentCount = arguments?.Count ?? 0,
                        ArgumentKeys = arguments?.Keys.ToArray(),
                        ArgumentValues = arguments?.ToDictionary(
                            kvp => kvp.Key, 
                            kvp => kvp.Value?.ToString() ?? "null"
                        )
                    }
                };
                
                toolCallActivityId = _activityLogger.StartActivity(
                    ActivityTypes.ToolCall,
                    $"Executing MCP tool: {toolName}",
                    toolCallData);
            }
            else
            {
                // Basic tool call logging
                toolCallActivityId = _activityLogger.StartActivity(
                    ActivityTypes.ToolCall,
                    $"Executing MCP tool: {toolName}",
                    new { ToolName = toolName, ArgumentCount = arguments?.Count ?? 0 });
            }
        }
        
        try
        {
            var result = await connection.CallToolAsync(toolName, arguments ?? new Dictionary<string, object?>());
            var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "<no text>";
            
            if (_activityLogger != null && toolCallActivityId != null)
            {
                await _activityLogger.CompleteActivityAsync(toolCallActivityId, new { ResultLength = text.Length });
                
                if (_config.ActivityLogging.VerboseTools)
                {
                    // Log the result as a separate activity with comprehensive data
                    var resultData = new 
                    { 
                        ToolName = toolName,
                        Success = true,
                        ResultLength = text.Length,
                        HasContent = !string.IsNullOrEmpty(text) && text != "<no text>",
                        // Include full output details for comprehensive audit trail
                        FullOutput = new
                        {
                            ResultText = SessionActivity.TruncateString(text, _config.ActivityLogging.MaxStringSize * 2), // Allow more space for tool results
                            ContentBlocks = result.Content.Select(block => new
                            {
                                Type = block.GetType().Name,
                                Content = block switch
                                {
                                    TextContentBlock textBlock => SessionActivity.TruncateString(textBlock.Text, _config.ActivityLogging.MaxStringSize),
                                    _ => SessionActivity.TruncateString(block.ToString(), 1000)
                                }
                            }).ToArray(),
                            IsError = result.IsError,
                            Metadata = result.Meta != null ? SessionActivity.TruncateString(JsonSerializer.Serialize(result.Meta), 2000) : null
                        }
                    };
                    
                    await _activityLogger.LogActivityAsync(
                        ActivityTypes.ToolResult,
                        $"MCP tool result: {toolName}",
                        resultData);
                }
                else
                {
                    // Basic tool result logging
                    await _activityLogger.LogActivityAsync(
                        ActivityTypes.ToolResult,
                        $"MCP tool result: {toolName}",
                        new 
                        { 
                            ToolName = toolName,
                            Success = true,
                            ResultLength = text.Length,
                            HasContent = !string.IsNullOrEmpty(text) && text != "<no text>"
                        });
                }
            }
            
            _logger.LogDebug("Tool {ToolName} executed successfully", toolName);
            return text;
        }
        catch (Exception ex)
        {
            if (_activityLogger != null && toolCallActivityId != null)
            {
                await _activityLogger.FailActivityAsync(toolCallActivityId, ex.Message);
            }
            
            if (_activityLogger != null)
            {
                // Enhanced error logging with detailed failure information
                var errorData = new 
                { 
                    ToolName = toolName, 
                    Arguments = arguments,
                    ErrorType = ex.GetType().Name,
                    ErrorMessage = ex.Message,
                    StackTrace = SessionActivity.TruncateString(ex.StackTrace, 2000),
                    // Include context for debugging
                    FailureContext = new
                    {
                        ToolName = toolName,
                        ArgumentCount = arguments?.Count ?? 0,
                        ArgumentKeys = arguments?.Keys.ToArray(),
                        Timestamp = DateTime.UtcNow
                    }
                };
                
                await _activityLogger.LogFailedActivityAsync(
                    ActivityTypes.ToolResult,
                    $"MCP tool failed: {toolName}",
                    ex.Message,
                    errorData);
            }
            
            _logger.LogError(ex, "Failed to execute tool {ToolName}", toolName);
            return $"Error executing {toolName}: {ex.Message}";
        }
    }
}