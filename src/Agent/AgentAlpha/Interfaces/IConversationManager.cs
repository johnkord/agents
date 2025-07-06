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
    Task<ConversationResponse> ProcessIterationAsync(List<ToolDefinition> availableTools);

    /// <summary>
    /// Add tool execution results to the conversation
    /// </summary>
    void AddToolResults(IEnumerable<string> toolSummaries);
    
    /// <summary>
    /// Check if the task appears to be completed based on assistant response
    /// </summary>
    bool IsTaskComplete(string assistantResponse);
    
    /// <summary>
    /// Check if the task appears to be completed based on conversation response including tool calls
    /// </summary>
    bool IsTaskComplete(ConversationResponse response);

    /// <summary>
    /// Get the current markdown document that tracks the overall task state.
    /// </summary>
    string GetTaskMarkdown();

    /// <summary>
    /// Ask the LLM to update the markdown with the latest observations / actions.
    /// When taskCompleted == true the LLM must finalise the document with all
    /// evidence, reasoning and detailed results.
    /// </summary>
    Task UpdateMarkdownAsync(string iterationSummary, bool taskCompleted = false);

    /* P5 – Worker Sub-Conversations */
    Task<WorkerResult> SpawnWorkerAsync(string subTask,
                                        Common.Models.Session.AgentSession parentSession,
                                        CancellationToken ct = default);
}