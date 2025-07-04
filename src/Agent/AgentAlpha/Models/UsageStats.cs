namespace AgentAlpha.Models;

/// <summary>
/// Simple usage counter (input / output / total tokens).
/// </summary>
public readonly record struct UsageStats(int InputTokens, int OutputTokens, int TotalTokens);
