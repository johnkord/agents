using OpenAIIntegration.Model;

namespace Common.Interfaces.Tools;

/// <summary>
/// Types of tools supported by the unified tool management system
/// </summary>
public enum ToolType
{
    /// <summary>
    /// Tool from MCP (Model Context Protocol) server
    /// </summary>
    MCP,
    
    /// <summary>
    /// Built-in OpenAI tool (e.g., web_search_preview)
    /// </summary>
    BuiltInOpenAI,
    
    /// <summary>
    /// Custom agent-specific tool
    /// </summary>
    Custom
}

/// <summary>
/// Unified interface for all tool types (MCP tools, built-in OpenAI tools, etc.)
/// This abstraction allows the framework to handle different tool types uniformly
/// </summary>
public interface IUnifiedTool
{
    /// <summary>
    /// Unique tool identifier
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Human-readable tool description
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// Tool type (MCP, BuiltIn, etc.)
    /// </summary>
    ToolType Type { get; }
    
    /// <summary>
    /// Convert to OpenAI ToolDefinition for API requests
    /// </summary>
    ToolDefinition ToToolDefinition();
    
    /// <summary>
    /// Check if this tool can be executed in the current context
    /// </summary>
    bool CanExecute();
    
    /// <summary>
    /// Get tool-specific metadata (e.g., required capabilities, configuration)
    /// </summary>
    Dictionary<string, object?> GetMetadata();
}