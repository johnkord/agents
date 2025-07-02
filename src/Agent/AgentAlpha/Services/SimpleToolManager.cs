using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using AgentAlpha.Configuration;
using AgentAlpha.Interfaces;
using OpenAIIntegration.Model;
using OpenAIIntegration;
using System.Text.Json;
using Common.Interfaces.Session;

namespace AgentAlpha.Services;

/// <summary>
/// Simplified tool management that consolidates tool discovery, filtering, and basic selection
/// </summary>
public class SimpleToolManager : IToolManager
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
        try
        {
            var result = await connection.CallToolAsync(toolName, arguments);
            
            if (result.IsError)
            {
                var errorText = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "Unknown error";
                _logger.LogWarning("Tool {ToolName} returned error: {Error}", toolName, errorText);
                return $"Error executing {toolName}: {errorText}";
            }

            var textBlocks = result.Content.OfType<TextContentBlock>().ToList();
            if (textBlocks.Count == 0)
            {
                return $"Tool {toolName} completed successfully (no text output)";
            }

            var output = string.Join("\n", textBlocks.Select(tb => tb.Text));
            _logger.LogDebug("Tool {ToolName} executed successfully, output length: {Length}", toolName, output.Length);
            
            return output;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute tool {ToolName}", toolName);
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

        // Add essential tools first (e.g., task completion)
        var essentialTool = availableTools.FirstOrDefault(t => t.Name == "task_complete");
        if (essentialTool != null)
        {
            selectedTools.Add(CreateOpenAiToolDefinition(essentialTool));
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

        var alreadySelected = selectedTools.Select(t => t.Name).ToHashSet();

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
            selectedTools.Count, string.Join(", ", selectedTools.Select(t => t.Name)));

        return Task.FromResult(selectedTools.ToArray());
    }

    private bool ShouldIncludeWebSearch(string task)
    {
        var taskLower = task.ToLowerInvariant();
        var webSearchKeywords = new[]
        {
            "web", "search", "internet", "online", "news", "current", "latest", "recent", 
            "today", "real-time", "live", "browse", "website", "url", "google", "find"
        };

        return webSearchKeywords.Any(keyword => taskLower.Contains(keyword));
    }

    // Unified tool management methods (simplified implementations)
    public Task<IList<IUnifiedTool>> DiscoverAllToolsAsync(IConnectionManager connection)
    {
        // Simplified - just return MCP tools wrapped as unified tools
        throw new NotImplementedException("Simplified architecture removes unified tool abstraction");
    }

    public IList<IUnifiedTool> ApplyFiltersToAllTools(IList<IUnifiedTool> tools, ToolFilterConfig filter)
    {
        throw new NotImplementedException("Simplified architecture removes unified tool abstraction");
    }

    public Task<string> ExecuteUnifiedToolAsync(IUnifiedTool tool, IConnectionManager connection, Dictionary<string, object?> arguments)
    {
        throw new NotImplementedException("Simplified architecture removes unified tool abstraction");
    }

    public ToolDefinition[] ConvertToToolDefinitions(IList<IUnifiedTool> tools)
    {
        throw new NotImplementedException("Simplified architecture removes unified tool abstraction");
    }
}