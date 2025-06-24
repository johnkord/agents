namespace AgentAlpha.Models;

/// <summary>
/// Statistics about a conversation for monitoring and optimization
/// </summary>
public record ConversationStatistics(
    int TotalMessages,
    int SystemMessages, 
    int UserMessages,
    int AssistantMessages,
    int EstimatedTokens
);