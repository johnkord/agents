using OpenAIIntegration.Model;
using AgentAlpha.Models;
using Common.Models.Session;

namespace AgentAlpha.Interfaces;

/// <summary>
/// Simplified interface for managing OpenAI conversations and message flow
/// </summary>
public interface IConversationManager
{
    /// <summary>
    /// Initialize a new conversation with system prompt and user task
    /// </summary>
    void InitializeConversation(string systemPrompt, string userTask);
    
    /// <summary>
    /// Initialize conversation from an existing session
    /// </summary>
    void InitializeFromSession(AgentSession session, string newUserTask);
    
    /// <summary>
    /// Get current conversation messages for session persistence
    /// </summary>
    IEnumerable<object> GetCurrentMessages();
    
    /// <summary>
    /// Process one iteration of the conversation
    /// </summary>
    Task<ConversationResponse> ProcessIterationAsync(ToolDefinition[] availableTools);
    
    /// <summary>
    /// Add tool execution results to the conversation
    /// </summary>
    void AddToolResults(IEnumerable<string> toolSummaries);
    
    /// <summary>
    /// Check if the task appears to be completed based on assistant response
    /// </summary>
    bool IsTaskComplete(string assistantResponse);
}