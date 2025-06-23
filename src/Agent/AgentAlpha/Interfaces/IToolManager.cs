using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using AgentAlpha.Configuration;
using OpenAIIntegration.Model;

namespace AgentAlpha.Interfaces;

/// <summary>
/// Handles tool discovery, validation, and schema management
/// </summary>
public interface IToolManager
{
    /// <summary>
    /// Discover all available tools from the connection
    /// </summary>
    Task<IList<McpClientTool>> DiscoverToolsAsync(IConnectionManager connection);
    
    /// <summary>
    /// Apply filtering configuration to the list of tools
    /// </summary>
    IList<McpClientTool> ApplyFilters(IList<McpClientTool> tools, ToolFilterConfig filter);
    
    /// <summary>
    /// Create OpenAI-compatible tool definition from MCP tool
    /// </summary>
    OpenAIIntegration.Model.ToolDefinition CreateOpenAiToolDefinition(McpClientTool mcpTool);
    
    /// <summary>
    /// Execute a tool and return the result summary
    /// </summary>
    Task<string> ExecuteToolAsync(IConnectionManager connection, string toolName, Dictionary<string, object?> arguments);
}