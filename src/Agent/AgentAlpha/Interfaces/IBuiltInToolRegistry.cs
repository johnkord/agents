using Common.Interfaces.Tools;

namespace AgentAlpha.Interfaces;

/// <summary>
/// Registry of available built-in OpenAI tools and other non-MCP tools
/// </summary>
public interface IBuiltInToolRegistry
{
    /// <summary>
    /// Get all available built-in tools based on current configuration
    /// </summary>
    IList<IUnifiedTool> GetAvailableBuiltInTools();
    
    /// <summary>
    /// Get a specific built-in tool by name
    /// </summary>
    IUnifiedTool? GetBuiltInTool(string toolName);
    
    /// <summary>
    /// Register a new built-in tool
    /// </summary>
    void RegisterBuiltInTool(IUnifiedTool tool);
    
    /// <summary>
    /// Check if a tool name corresponds to a built-in tool
    /// </summary>
    bool IsBuiltInTool(string toolName);
    
    /// <summary>
    /// Get the count of registered built-in tools
    /// </summary>
    int Count { get; }
}