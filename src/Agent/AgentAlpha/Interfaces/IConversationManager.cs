using OpenAIIntegration.Model;
using AgentAlpha.Models;

namespace AgentAlpha.Interfaces;

/// <summary>
/// Manages OpenAI conversations and message flow
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
    Task<ConversationResponse> ProcessIterationAsync(OpenAIIntegration.Model.ToolDefinition[] availableTools);
    
    /// <summary>
    /// Add an assistant message to the conversation
    /// </summary>
    void AddAssistantMessage(string content);
    
    /// <summary>
    /// Add tool execution results to the conversation
    /// </summary>
    void AddToolResults(IEnumerable<string> toolSummaries);
    
    /// <summary>
    /// Check if the task appears to be completed based on assistant response
    /// </summary>
    bool IsTaskComplete(string assistantResponse);
    
    /// <summary>
    /// Get conversation statistics for monitoring
    /// </summary>
    ConversationStatistics GetConversationStatistics();
}