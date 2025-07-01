using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using AgentAlpha.Configuration;
using OpenAIIntegration.Model;
using Common.Interfaces.Tools;

namespace AgentAlpha.Interfaces;

/// <summary>
/// Handles tool discovery, validation, and schema management for both MCP and built-in tools
/// </summary>
public interface IToolManager
{
    // Existing MCP-specific methods (maintained for backward compatibility)
    
    /// <summary>
    /// Discover all available MCP tools from the connection
    /// </summary>
    Task<IList<McpClientTool>> DiscoverToolsAsync(IConnectionManager connection);
    
    /// <summary>
    /// Apply filtering configuration to the list of MCP tools
    /// </summary>
    IList<McpClientTool> ApplyFilters(IList<McpClientTool> tools, ToolFilterConfig filter);
    
    /// <summary>
    /// Create OpenAI-compatible tool definition from MCP tool
    /// </summary>
    OpenAIIntegration.Model.ToolDefinition CreateOpenAiToolDefinition(McpClientTool mcpTool);
    
    /// <summary>
    /// Execute an MCP tool and return the result summary
    /// </summary>
    Task<string> ExecuteToolAsync(IConnectionManager connection, string toolName, Dictionary<string, object?> arguments);
    
    // New unified methods for handling all tool types
    
    /// <summary>
    /// Discover all available tools (both MCP and built-in) from all sources
    /// </summary>
    Task<IList<IUnifiedTool>> DiscoverAllToolsAsync(IConnectionManager connection);
    
    /// <summary>
    /// Apply filtering configuration to all types of tools
    /// </summary>
    IList<IUnifiedTool> ApplyFiltersToAllTools(IList<IUnifiedTool> tools, ToolFilterConfig filter);
    
    /// <summary>
    /// Execute any type of unified tool and return the result summary
    /// </summary>
    Task<string> ExecuteUnifiedToolAsync(IUnifiedTool tool, IConnectionManager connection, Dictionary<string, object?> arguments);
    
    /// <summary>
    /// Convert a list of unified tools to OpenAI tool definitions
    /// </summary>
    ToolDefinition[] ConvertToToolDefinitions(IList<IUnifiedTool> tools);
}