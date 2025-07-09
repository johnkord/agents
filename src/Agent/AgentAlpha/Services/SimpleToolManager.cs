using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using AgentAlpha.Configuration;
using AgentAlpha.Interfaces;
using OpenAIIntegration.Model;
using OpenAIIntegration;
using System.Text.Json;
using Common.Interfaces.Session;
using Common.Models.Session;

namespace AgentAlpha.Services;

/// <summary>
/// Simplified tool management that consolidates tool discovery, filtering, and basic selection
/// </summary>
public class SimpleToolManager
{
    private readonly ILogger<SimpleToolManager> _logger;
    private readonly AgentConfiguration _config;
    private readonly ISessionAwareOpenAIService _openAi;
    private ISessionActivityLogger? _activityLogger;

    public SimpleToolManager(
        ILogger<SimpleToolManager> logger,
        AgentConfiguration config,
        ISessionAwareOpenAIService openAi)
    {
        _logger = logger;
        _config = config;
        _openAi = openAi;
    }

    public void SetActivityLogger(ISessionActivityLogger? activityLogger)
    {
        _activityLogger = activityLogger;
        _openAi.SetActivityLogger(activityLogger);
    }

    public async Task<IList<McpClientTool>> DiscoverToolsAsync(IConnectionManager connection)
    {
        if (!connection.IsConnected)
        {
            _logger.LogWarning("Connection is not active, cannot discover tools");
            return new List<McpClientTool>();
        }

        try
        {
            var tools = await connection.ListToolsAsync();
            _logger.LogDebug("Discovered {Count} tools from MCP server", tools.Count);
            return tools;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover tools from MCP server");
            return new List<McpClientTool>();
        }
    }

    public IList<McpClientTool> ApplyFilters(IList<McpClientTool> tools, ToolFilterConfig filter)
    {
        var filteredTools = tools.ToList();

        // Apply whitelist if specified
        if (filter.Whitelist.Count > 0)
        {
            filteredTools = filteredTools
                .Where(tool => filter.Whitelist.Contains(tool.Name))
                .ToList();
        }

        // Apply blacklist
        if (filter.Blacklist.Count > 0)
        {
            filteredTools = filteredTools
                .Where(tool => !filter.Blacklist.Contains(tool.Name))
                .ToList();
        }

        _logger.LogDebug("Filtered tools from {Original} to {Filtered}",
            tools.Count, filteredTools.Count);

        return filteredTools;
    }

    public OpenAIIntegration.Model.ToolDefinition CreateOpenAiToolDefinition(McpClientTool mcpTool)
    {
        return new OpenAIIntegration.Model.ToolDefinition
        {
            Type = "function",
            Name = mcpTool.Name,
            Description = mcpTool.Description,
            Parameters = JsonSerializer.Deserialize<object>(
                mcpTool.ProtocolTool.InputSchema.GetRawText())
        };
    }

    public async Task<string> ExecuteToolAsync(IConnectionManager connection, string toolName, Dictionary<string, object?> arguments)
    {
        string? activityId = null;
        
        // Log tool call start
        if (_activityLogger != null)
        {
            var toolCallData = new 
            {
                ToolName = toolName,
                Arguments = arguments,
                FullInput = new
                {
                    ToolName = toolName,
                    ArgumentCount = arguments?.Count ?? 0,
                    ArgumentKeys = arguments?.Keys.ToArray() ?? Array.Empty<string>(),
                    ArgumentValues = arguments?.ToDictionary(
                        kvp => kvp.Key, 
                        kvp => kvp.Value?.ToString() ?? "null") ?? new Dictionary<string, string>()
                }
            };
            
            activityId = _activityLogger.StartActivity(
                ActivityTypes.ToolCall, 
                $"Executing tool: {toolName}", 
                toolCallData);
        }

        try
        {
            var result = await connection.CallToolAsync(toolName, arguments ?? new Dictionary<string, object?>());
            
            if (result.IsError)
            {
                var errorText = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "Unknown error";
                _logger.LogWarning("Tool {ToolName} returned error: {Error}", toolName, errorText);
                
                // Log tool result with error
                if (_activityLogger != null)
                {
                    var errorResultData = new
                    {
                        ToolName = toolName,
                        Success = false,
                        IsError = true,
                        ErrorMessage = errorText,
                        FullOutput = new
                        {
                            ResultText = $"Error executing {toolName}: {errorText}",
                            ContentBlocks = result.Content.Select(c => new 
                            { 
                                Type = c.GetType().Name, 
                                Content = c is TextContentBlock tc ? tc.Text : c.ToString() 
                            }).ToArray(),
                            IsError = true,
                            Metadata = "{\"execution_status\": \"error\"}"
                        }
                    };
                    
                    if (activityId != null)
                    {
                        await _activityLogger.FailActivityAsync(activityId, errorText, errorResultData);
                    }
                    else
                    {
                        await _activityLogger.LogFailedActivityAsync(
                            ActivityTypes.ToolResult, 
                            $"Tool result with error: {toolName}", 
                            errorText, 
                            errorResultData);
                    }
                }
                
                return $"Error executing {toolName}: {errorText}";
            }

            var textBlocks = result.Content.OfType<TextContentBlock>().ToList();
            var output = textBlocks.Count == 0 
                ? $"Tool {toolName} completed successfully (no text output)"
                : string.Join("\n", textBlocks.Select(tb => tb.Text));
            
            // Log successful tool result
            if (_activityLogger != null)
            {
                var successResultData = new
                {
                    ToolName = toolName,
                    Success = true,
                    ResultLength = output.Length,
                    HasContent = textBlocks.Count > 0,
                    FullOutput = new
                    {
                        ResultText = output,
                        ContentBlocks = result.Content.Select(c => new 
                        { 
                            Type = c.GetType().Name, 
                            Content = c is TextContentBlock tc ? tc.Text : c.ToString() 
                        }).ToArray(),
                        IsError = false,
                        Metadata = "{\"execution_status\": \"success\"}"
                    }
                };
                
                if (activityId != null)
                {
                    await _activityLogger.CompleteActivityAsync(activityId, successResultData);
                }
                
                // Always log a separate ToolResult activity for comprehensive audit trail
                await _activityLogger.LogActivityAsync(
                    ActivityTypes.ToolResult, 
                    $"Tool result: {toolName}", 
                    successResultData);
            }
            
            _logger.LogDebug("Tool {ToolName} executed successfully, output length: {Length}", toolName, output.Length);
            return output;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute tool {ToolName}", toolName);
            
            // Log tool execution exception
            if (_activityLogger != null)
            {
                var exceptionResultData = new
                {
                    ToolName = toolName,
                    Success = false,
                    IsError = true,
                    ErrorMessage = ex.Message,
                    ExceptionType = ex.GetType().Name,
                    FullOutput = new
                    {
                        ResultText = $"Error executing {toolName}: {ex.Message}",
                        ContentBlocks = new object[0],
                        IsError = true,
                        Metadata = $"{{\"execution_status\": \"exception\", \"exception_type\": \"{ex.GetType().Name}\"}}"
                    }
                };
                
                if (activityId != null)
                {
                    await _activityLogger.FailActivityAsync(activityId, ex.Message, exceptionResultData);
                }
                else
                {
                    await _activityLogger.LogFailedActivityAsync(
                        ActivityTypes.ToolResult, 
                        $"Tool execution failed: {toolName}", 
                        ex.Message, 
                        exceptionResultData);
                }
            }
            
            return $"Error executing {toolName}: {ex.Message}";
        }
    }

    /// <summary>
    /// Returns up to <paramref name="maxTools"/> tool definitions without heuristics.
    /// The LLM decides which ones to use each iteration.
    /// </summary>
    public Task<List<ToolDefinition>> SelectToolsForPlanAsync(
        string plan,
        IList<McpClientTool> availableTools)
    {
        var defs = availableTools
            .Select(CreateOpenAiToolDefinition)
            .ToList();

        // Ensure complete_task is present
        if (defs.All(d => d.Name != "complete_task"))
        {
            var ct = availableTools.FirstOrDefault(t => t.Name == "complete_task");
            if (ct != null)
                defs.Add(CreateOpenAiToolDefinition(ct));
        }

        defs.Add(_config.WebSearch.ToToolDefinition());

        // TODO: Make a ResponsesCreateRequest to OpenAI to get the selected tools
        // This is a placeholder for the actual selection logic
        // For now, we just return all available tools as definitions
        // In a real implementation, you would call OpenAI's API to select tools based on the plan
        // and possibly filter them based on some criteria.

        _logger.LogInformation("Selected {Count} tools for plan: {Plan}",
            defs.Count, plan);
        return Task.FromResult(defs);
    }

    // Simplified tool management methods - these throw NotImplementedException as they're not needed
    // in the simplified architecture but kept for potential interface compatibility
    public Task<IList<object>> DiscoverAllToolsAsync(IConnectionManager connection)
    {
        throw new NotImplementedException("Simplified architecture removes unified tool abstraction");
    }

    public IList<object> ApplyFiltersToAllTools(IList<object> tools, ToolFilterConfig filter)
    {
        throw new NotImplementedException("Simplified architecture removes unified tool abstraction");
    }

    public Task<string> ExecuteUnifiedToolAsync(object tool, IConnectionManager connection, Dictionary<string, object?> arguments)
    {
        throw new NotImplementedException("Simplified architecture removes unified tool abstraction");
    }

    public ToolDefinition[] ConvertToToolDefinitions(IList<object> tools)
    {
        throw new NotImplementedException("Simplified architecture removes unified tool abstraction");
    }
}