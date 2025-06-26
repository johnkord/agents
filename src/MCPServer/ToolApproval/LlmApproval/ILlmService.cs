using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MCPServer.ToolApproval.LlmApproval;

/// <summary>
/// Interface for LLM services that can evaluate tool calls for approval decisions
/// </summary>
public interface ILlmService
{
    /// <summary>
    /// Evaluate a tool call and provide an approval decision
    /// </summary>
    /// <param name="toolName">Name of the tool being called</param>
    /// <param name="arguments">Arguments passed to the tool</param>
    /// <param name="context">Additional context for the evaluation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>LLM approval decision with confidence and reasoning</returns>
    Task<LlmApprovalDecision> EvaluateToolCallAsync(
        string toolName, 
        IReadOnlyDictionary<string, object?> arguments,
        LlmApprovalContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Name of the LLM service provider
    /// </summary>
    string ServiceName { get; }

    /// <summary>
    /// Model name or identifier being used
    /// </summary>
    string ModelName { get; }
}

/// <summary>
/// Result of an LLM approval evaluation
/// </summary>
/// <param name="Result">The approval decision</param>
/// <param name="Confidence">Confidence score (0.0 to 1.0)</param>
/// <param name="Reasoning">Human-readable explanation of the decision</param>
/// <param name="RiskCategory">Assessed risk category</param>
/// <param name="Concerns">Specific concerns identified</param>
/// <param name="ProcessingTime">Time taken to make the decision</param>
public record LlmApprovalDecision(
    ApprovalResult Result,
    double Confidence,
    string Reasoning,
    RiskCategory RiskCategory,
    IReadOnlyList<string> Concerns,
    TimeSpan ProcessingTime);

/// <summary>
/// Context information for LLM approval evaluation
/// </summary>
/// <param name="UserId">User making the request</param>
/// <param name="SessionId">Session identifier</param>
/// <param name="Timestamp">When the request was made</param>
/// <param name="Environment">Environment context (dev, staging, prod)</param>
/// <param name="AdditionalContext">Additional context information</param>
public record LlmApprovalContext(
    string? UserId = null,
    string? SessionId = null,
    DateTimeOffset? Timestamp = null,
    string? Environment = null,
    IReadOnlyDictionary<string, object?>? AdditionalContext = null);

/// <summary>
/// LLM approval decision result
/// </summary>
public enum ApprovalResult
{
    /// <summary>
    /// Automatically approve the tool call
    /// </summary>
    Approve,
    
    /// <summary>
    /// Require human approval before proceeding
    /// </summary>
    RequireHuman,
    
    /// <summary>
    /// Deny the tool call
    /// </summary>
    Deny
}

/// <summary>
/// Risk category assessment for tool calls
/// </summary>
public enum RiskCategory
{
    /// <summary>
    /// Safe operations (read-only, calculations, etc.)
    /// </summary>
    Safe,
    
    /// <summary>
    /// Moderate risk operations (file operations with specific patterns)
    /// </summary>
    Moderate,
    
    /// <summary>
    /// High risk operations (system operations, network calls)
    /// </summary>
    High,
    
    /// <summary>
    /// Critical risk operations (destructive operations, security changes)
    /// </summary>
    Critical
}