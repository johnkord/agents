using Microsoft.Extensions.Logging;
using AgentAlpha.Configuration;
using AgentAlpha.Interfaces;
using AgentAlpha.Models;

namespace AgentAlpha.Services;

/// <summary>
/// Registry implementation for built-in OpenAI tools and other non-MCP tools
/// </summary>
public class BuiltInToolRegistry : IBuiltInToolRegistry
{
    private readonly Dictionary<string, IUnifiedTool> _tools;
    private readonly ILogger<BuiltInToolRegistry> _logger;
    private readonly AgentConfiguration _config;

    public BuiltInToolRegistry(ILogger<BuiltInToolRegistry> logger, AgentConfiguration config)
    {
        _tools = new Dictionary<string, IUnifiedTool>(StringComparer.OrdinalIgnoreCase);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        
        InitializeBuiltInTools();
    }

    public IList<IUnifiedTool> GetAvailableBuiltInTools()
    {
        return _tools.Values.Where(tool => tool.CanExecute()).ToList();
    }

    public IUnifiedTool? GetBuiltInTool(string toolName)
    {
        _tools.TryGetValue(toolName, out var tool);
        return tool;
    }

    public void RegisterBuiltInTool(IUnifiedTool tool)
    {
        if (tool == null) throw new ArgumentNullException(nameof(tool));
        
        _tools[tool.Name] = tool;
        _logger.LogDebug("Registered built-in tool: {ToolName} ({ToolType})", tool.Name, tool.Type);
    }

    public bool IsBuiltInTool(string toolName)
    {
        return _tools.ContainsKey(toolName);
    }

    public int Count => _tools.Count;

    /// <summary>
    /// Initialize built-in tools based on configuration
    /// </summary>
    private void InitializeBuiltInTools()
    {
        _logger.LogDebug("Initializing built-in tools registry");

        // Register web search tool if configured
        if (_config.WebSearch != null)
        {
            try
            {
                var webSearchTool = new WebSearchBuiltInTool(_config.WebSearch);
                RegisterBuiltInTool(webSearchTool);
                _logger.LogInformation("Registered web search built-in tool");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register web search tool");
            }
        }

        // Future: Add other built-in tools here
        // - Code interpreter tools
        // - File search tools
        // - Custom agent tools
        
        _logger.LogInformation("Built-in tools registry initialized with {Count} tools", _tools.Count);
    }
}