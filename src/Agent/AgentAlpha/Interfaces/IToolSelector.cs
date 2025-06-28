using ModelContextProtocol.Client;
using OpenAIIntegration.Model;
using AgentAlpha.Models;

namespace AgentAlpha.Interfaces;

/// <summary>
/// Service for intelligently selecting relevant tools for a given task/context to reduce OpenAI token costs
/// </summary>
public interface IToolSelector
{
    /// <summary>
    /// Select relevant tools for the initial task (includes both MCP tools and built-in OpenAI tools)
    /// </summary>
    /// <param name="task">The user's task description</param>
    /// <param name="availableTools">All available tools from MCP server</param>
    /// <param name="maxTools">Maximum number of tools to select (optional)</param>
    /// <returns>Selected tools that are likely relevant to the task</returns>
    Task<ToolDefinition[]> SelectToolsForTaskAsync(string task, IList<McpClientTool> availableTools, int? maxTools = null);
    
    /// <summary>
    /// Select additional tools for a conversation iteration based on current context
    /// </summary>
    /// <param name="conversationContext">Current conversation messages</param>
    /// <param name="availableTools">All available tools from MCP server</param>
    /// <param name="currentlySelectedTools">Tools already included in this conversation</param>
    /// <param name="maxAdditionalTools">Maximum additional tools to add</param>
    /// <returns>Additional tools to include for this iteration</returns>
    Task<ToolDefinition[]> SelectAdditionalToolsAsync(
        IEnumerable<object> conversationContext, 
        IList<McpClientTool> availableTools, 
        ToolDefinition[] currentlySelectedTools,
        int maxAdditionalTools = 3);
    
    /// <summary>
    /// Get essential tools that should always be included (e.g., task completion)
    /// </summary>
    /// <param name="availableTools">All available tools</param>
    /// <returns>Essential tools that should always be available</returns>
    Task<ToolDefinition[]> GetEssentialToolsAsync(IList<McpClientTool> availableTools);
    
    /// <summary>
    /// Determine if web search should be included based on task analysis
    /// </summary>
    /// <param name="task">The user's task description</param>
    /// <returns>True if web search tool should be included</returns>
    bool ShouldIncludeWebSearch(string task);
    
    /// <summary>
    /// Set the session activity logger for automatic OpenAI request logging
    /// </summary>
    /// <param name="activityLogger">The activity logger to use for this session</param>
    void SetActivityLogger(ISessionActivityLogger? activityLogger);
}