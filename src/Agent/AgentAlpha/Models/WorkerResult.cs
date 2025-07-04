using System.Collections.Generic;

namespace AgentAlpha.Models;

/// <summary>
/// Result emitted by a worker sub-conversation.
/// </summary>
public sealed record WorkerResult(
    string Summary,
    IReadOnlyList<ToolCall> ToolOutputs,
    UsageStats TokensUsed);
