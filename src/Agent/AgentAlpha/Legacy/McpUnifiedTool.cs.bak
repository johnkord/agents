using ModelContextProtocol.Client;
using OpenAIIntegration.Model;
using AgentAlpha.Interfaces;

namespace AgentAlpha.Models;

/// <summary>
/// Unified tool wrapper for MCP (Model Context Protocol) tools
/// This allows MCP tools to be used in the unified tool management system
/// </summary>
public class McpUnifiedTool : IUnifiedTool
{
    private readonly McpClientTool _mcpTool;
    private readonly IToolManager _toolManager;

    public McpUnifiedTool(McpClientTool mcpTool, IToolManager toolManager)
    {
        _mcpTool = mcpTool ?? throw new ArgumentNullException(nameof(mcpTool));
        _toolManager = toolManager ?? throw new ArgumentNullException(nameof(toolManager));
    }

    /// <summary>
    /// The underlying MCP tool
    /// </summary>
    public McpClientTool McpTool => _mcpTool;

    public string Name => _mcpTool.Name;

    public string Description => _mcpTool.Description ?? "No description available";

    public ToolType Type => ToolType.MCP;

    public ToolDefinition ToToolDefinition()
    {
        return _toolManager.CreateOpenAiToolDefinition(_mcpTool);
    }

    public bool CanExecute()
    {
        // MCP tools can always be executed if we have a connection
        return true;
    }

    public Dictionary<string, object?> GetMetadata()
    {
        return new Dictionary<string, object?>
        {
            { "mcpTool", _mcpTool },
            { "inputSchema", _mcpTool.ProtocolTool.InputSchema },
            { "requiresConnection", true },
            { "executionType", "mcp" }
        };
    }

    public override bool Equals(object? obj)
    {
        return obj is McpUnifiedTool other && Name == other.Name && Type == other.Type;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Type);
    }

    public override string ToString()
    {
        return $"MCP Tool: {Name} - {Description}";
    }
}