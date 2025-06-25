using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using AgentAlpha.Configuration;
using AgentAlpha.Interfaces;
using AgentAlpha.Models;
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

    public ToolManager(ILogger<ToolManager> logger, ISessionActivityLogger? activityLogger = null)
    {
        _logger = logger;
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
            toolCallActivityId = _activityLogger.StartActivity(
                ActivityTypes.ToolCall,
                $"Executing MCP tool: {toolName}",
                new { ToolName = toolName, Arguments = arguments });
        }
        
        try
        {
            var result = await connection.CallToolAsync(toolName, arguments);
            var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "<no text>";
            
            if (_activityLogger != null && toolCallActivityId != null)
            {
                await _activityLogger.CompleteActivityAsync(toolCallActivityId, new { ResultLength = text.Length });
                
                // Log the result as a separate activity
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
                await _activityLogger.LogFailedActivityAsync(
                    ActivityTypes.ToolResult,
                    $"MCP tool failed: {toolName}",
                    ex.Message,
                    new { ToolName = toolName, Arguments = arguments });
            }
            
            _logger.LogError(ex, "Failed to execute tool {ToolName}", toolName);
            return $"Error executing {toolName}: {ex.Message}";
        }
    }
}