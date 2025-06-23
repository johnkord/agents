using OpenAIIntegration.Model;

namespace AgentAlpha.Models;

/// <summary>
/// Response from a conversation iteration
/// </summary>
public record ConversationResponse(
    string AssistantText,
    IReadOnlyList<ToolCall> ToolCalls,
    bool HasToolCalls
);

/// <summary>
/// Represents a tool call with name and arguments
/// </summary>
public record ToolCall(
    string Name,
    Dictionary<string, object?> Arguments
);