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
    /// Simple tool selection for a task - uses basic heuristics and includes web search when appropriate
    /// </summary>
    public Task<ToolDefinition[]> SelectToolsForTaskAsync(string task, IList<McpClientTool> availableTools, int maxTools = 10)
    {
        var selectedTools = new List<ToolDefinition>();
        var taskLower = task.ToLowerInvariant();

        // Add essential MCP tools first - these should always be available
        var essentialToolNames = new[] { "complete_task", "run_command" };
        var alreadySelected = new HashSet<string>();
        
        foreach (var toolName in essentialToolNames)
        {
            var essentialTool = availableTools.FirstOrDefault(t => t.Name == toolName);
            if (essentialTool != null)
            {
                selectedTools.Add(CreateOpenAiToolDefinition(essentialTool));
                alreadySelected.Add(essentialTool.Name);
                _logger.LogDebug("Added essential tool: {ToolName}", toolName);
            }
            else
            {
                _logger.LogWarning("Essential tool not found: {ToolName}", toolName);
            }
        }

        // Simple keyword-based selection for remaining tools
        var keywordMappings = new Dictionary<string[], string[]>
        {
            [new[] { "file", "read", "write", "directory", "folder" }] = 
                new[] { "read_file", "write_file", "list_directory", "file_info" },
            [new[] { "text", "search", "replace", "word", "count" }] = 
                new[] { "search_in_file", "replace_in_file", "word_count" },
            [new[] { "time", "date", "current", "now" }] = 
                new[] { "current_time" },
            [new[] { "system", "environment", "variable" }] = 
                new[] { "get_env_var", "system_info" }
        };

        foreach (var (keywords, toolNames) in keywordMappings)
        {
            if (selectedTools.Count >= maxTools) break;

            if (keywords.Any(keyword => taskLower.Contains(keyword)))
            {
                foreach (var toolName in toolNames)
                {
                    if (selectedTools.Count >= maxTools) break;

                    var tool = availableTools.FirstOrDefault(t => 
                        string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase) &&
                        !alreadySelected.Contains(t.Name));

                    if (tool != null)
                    {
                        selectedTools.Add(CreateOpenAiToolDefinition(tool));
                        alreadySelected.Add(tool.Name);
                    }
                }
            }
        }

        // Add web search if it looks like the task might need current information
        if (ShouldIncludeWebSearch(task) && selectedTools.Count < maxTools && _config.WebSearch != null)
        {
            var webSearchTool = _config.WebSearch.ToToolDefinition();
            selectedTools.Add(webSearchTool);
            _logger.LogDebug("Added web search tool for task");
        }

        // Fill remaining slots with commonly useful tools
        if (selectedTools.Count < maxTools)
        {
            var generalTools = new[] { "file_info", "list_directory", "current_time" };
            foreach (var toolName in generalTools)
            {
                if (selectedTools.Count >= maxTools) break;

                var tool = availableTools.FirstOrDefault(t => 
                    string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase) &&
                    !alreadySelected.Contains(t.Name));

                if (tool != null)
                {
                    selectedTools.Add(CreateOpenAiToolDefinition(tool));
                }
            }
        }

        _logger.LogInformation("Selected {Count} tools for task: {Tools}", 
            selectedTools.Count, string.Join(", ", selectedTools.Select(t => 
                string.IsNullOrEmpty(t.Name) ? $"[{t.Type}]" : t.Name)));

        return Task.FromResult(selectedTools.ToArray());
    }

    private bool ShouldIncludeWebSearch(string task)
    {
        var taskLower = task.ToLowerInvariant();
        var webSearchKeywords = new[]
        {
            "web", "search", "internet", "online", "news", "current", "latest", "recent", 
            "today", "real-time", "live", "browse", "website", "url", "google", "find",
            "what's happening", "breaking", "update", "trending", "available", "which", 
            "what", "list", "models", "options", "versions", "supported", "offerings", 
            "plans", "pricing", "features", "capabilities", "services", "apis", "endpoints", 
            "status", "working", "active"
        };

        return webSearchKeywords.Any(keyword => taskLower.Contains(keyword));
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