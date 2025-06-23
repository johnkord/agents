using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using AgentAlpha.Configuration;
using AgentAlpha.Interfaces;
using OpenAIIntegration.Model;
using System.Text.Json;   // new

namespace AgentAlpha.Services;

/// <summary>
/// Implementation of tool discovery, validation, and schema management
/// </summary>
public class ToolManager : IToolManager
{
    private readonly ILogger<ToolManager> _logger;

    public ToolManager(ILogger<ToolManager> logger)
    {
        _logger = logger;
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
        try
        {
            var result = await connection.CallToolAsync(toolName, arguments);
            var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "<no text>";
            
            _logger.LogDebug("Tool {ToolName} executed successfully", toolName);
            return text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute tool {ToolName}", toolName);
            return $"Error executing {toolName}: {ex.Message}";
        }
    }
}