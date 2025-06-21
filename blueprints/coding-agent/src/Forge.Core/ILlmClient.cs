using Microsoft.Extensions.AI;

namespace Forge.Core;

/// <summary>
/// Result of a single LLM call — either text output, tool calls, or both.
/// This is our abstraction over provider-specific response types.
/// </summary>
public sealed class LlmResponse
{
    public string? Text { get; init; }
    public IReadOnlyList<LlmToolCall> ToolCalls { get; init; } = [];
    public int PromptTokens { get; init; }
    public int CompletionTokens { get; init; }
}

/// <summary>
/// A tool call requested by the LLM.
/// </summary>
public sealed record LlmToolCall
{
    public required string CallId { get; init; }
    public required string FunctionName { get; init; }
    public required string ArgumentsJson { get; init; }
}

/// <summary>
/// Result of executing a tool, to be sent back to the LLM.
/// </summary>
public sealed record LlmToolResult
{
    public required string CallId { get; init; }
    public required string Output { get; init; }
}

/// <summary>
/// Thin abstraction over LLM providers. Designed for agentic tool-calling loops.
/// Implementations manage their own conversation state (message history, response chaining, etc.)
///
/// Why not use IChatClient? Two reasons:
/// 1. IChatClient wraps /v1/chat/completions which doesn't support reasoning_effort + tools together.
/// 2. We want full control over the agent loop without framework opinions.
///
/// Each implementation owns its conversation state. Call AddSystemMessage/AddUserMessage to set up,
/// then loop: GetStreamingResponseAsync → execute tool calls → AddToolResults → repeat.
/// </summary>
public interface ILlmClient : IAsyncDisposable
{
    /// <summary>Add a system instruction to the conversation.</summary>
    void AddSystemMessage(string content);

    /// <summary>Add a user message to the conversation.</summary>
    void AddUserMessage(string content);

    /// <summary>Add tool execution results back into the conversation.</summary>
    void AddToolResults(IEnumerable<LlmToolResult> results);

    /// <summary>Update the tool set available to the LLM. Call before each step
    /// to reflect tools activated via find_tools.</summary>
    void UpdateTools(IReadOnlyList<AITool> tools);

    /// <summary>Override the reasoning effort for subsequent calls.
    /// Used by progressive deepening to escalate on consecutive failures.</summary>
    void SetReasoningEffort(ReasoningEffort? effort);

    /// <summary>
    /// Stream the next LLM response. Text tokens are forwarded via onToken callback.
    /// Returns the complete response with tool calls (if any) and token usage.
    /// </summary>
    Task<LlmResponse> GetStreamingResponseAsync(
        OnTokenReceived? onToken = null,
        CancellationToken cancellationToken = default);
}
