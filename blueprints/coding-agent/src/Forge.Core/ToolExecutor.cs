using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Serilog;

namespace Forge.Core;

/// <summary>
/// Executes LLM-requested tool calls with guardrails, duplicate detection,
/// observation processing, and result packaging for the agent loop.
/// </summary>
public sealed class ToolExecutor
{
    private readonly AgentOptions _options;
    private readonly Guardrails _guardrails;
    private readonly ILogger _logger;
    private readonly HashSet<string> _recentFailedCalls = new(StringComparer.Ordinal);
    private readonly Queue<string> _failedCallOrder = new();
    private const int MaxRecentFailedCalls = 200;

    public ToolExecutor(AgentOptions options, Guardrails guardrails, ILogger logger)
    {
        _options = options;
        _guardrails = guardrails;
        _logger = logger;
    }

    public async Task<ToolExecutionResult> ExecuteAsync(
        IReadOnlyList<LlmToolCall> toolCalls,
        IReadOnlyList<AITool> activeTools,
        OnTokenReceived? onToken = null,
        CancellationToken cancellationToken = default)
    {
        var toolCallRecords = new List<ToolCallRecord>();
        var toolResults = new List<LlmToolResult>();
        var activeFunctions = activeTools.OfType<AIFunction>().ToList();
        bool hadExecutionError = false;
        bool hadCancellation = false;

        foreach (var toolCall in toolCalls)
        {
            var toolSw = Stopwatch.StartNew();
            onToken?.Invoke($"\n  ⚡ {toolCall.FunctionName}");

            var callSignature = $"{toolCall.FunctionName}:{toolCall.ArgumentsJson}";
            if (_recentFailedCalls.Contains(callSignature))
            {
                var dupeMsg = $"You already called {toolCall.FunctionName} with the same arguments and it failed. Try a different tool or different arguments.";
                _logger.Warning("Duplicate tool call detected: {Tool}", toolCall.FunctionName);
                toolResults.Add(new LlmToolResult { CallId = toolCall.CallId, Output = dupeMsg });
                toolCallRecords.Add(new ToolCallRecord
                {
                    ToolName = toolCall.FunctionName,
                    Arguments = toolCall.ArgumentsJson,
                    ResultSummary = dupeMsg,
                    ResultLength = dupeMsg.Length,
                    IsError = true,
                    DurationMs = 0,
                });
                onToken?.Invoke(" 🔁 duplicate, skipped\n");
                hadExecutionError = true;
                continue;
            }

            var (allowed, reason) = _guardrails.CheckToolCall(toolCall.FunctionName, toolCall.ArgumentsJson);
            if (!allowed)
            {
                var blockedMsg = $"BLOCKED: {reason}";
                _logger.Warning("Tool call blocked: {Tool} — {Reason}", toolCall.FunctionName, reason);
                toolResults.Add(new LlmToolResult { CallId = toolCall.CallId, Output = blockedMsg });
                toolCallRecords.Add(new ToolCallRecord
                {
                    ToolName = toolCall.FunctionName,
                    Arguments = toolCall.ArgumentsJson,
                    ResultSummary = blockedMsg,
                    ResultLength = reason?.Length ?? 0,
                    IsError = true,
                    DurationMs = toolSw.Elapsed.TotalMilliseconds,
                });
                onToken?.Invoke(" ❌ blocked\n");
                TrackFailedCall(callSignature);
                hadExecutionError = true;
                continue;
            }

            var tool = activeFunctions.FirstOrDefault(t => t.Name == toolCall.FunctionName);
            if (tool is null)
            {
                var msg = $"Tool '{toolCall.FunctionName}' not found. "
                    + $"Use find_tools('{toolCall.FunctionName.Replace("_", " ")}') to search for available tools, "
                    + $"or use one of: {string.Join(", ", activeFunctions.Select(t => t.Name))}";
                toolResults.Add(new LlmToolResult { CallId = toolCall.CallId, Output = msg });
                toolCallRecords.Add(new ToolCallRecord
                {
                    ToolName = toolCall.FunctionName,
                    Arguments = toolCall.ArgumentsJson,
                    ResultSummary = msg,
                    ResultLength = msg.Length,
                    IsError = true,
                    DurationMs = toolSw.Elapsed.TotalMilliseconds,
                });
                onToken?.Invoke(" ❓ not found\n");
                TrackFailedCall(callSignature);
                hadExecutionError = true;
                continue;
            }

            try
            {
                var args = JsonSerializer.Deserialize<Dictionary<string, object?>>(toolCall.ArgumentsJson);
                var aiArgs = args is not null ? new AIFunctionArguments(args) : null;
                var result = await tool.InvokeAsync(aiArgs, cancellationToken);
                var resultText = result?.ToString() ?? "(no output)";

                var processedResult = ObservationPipeline.Process(toolCall.FunctionName, resultText, _options);
                var summary = Truncate(processedResult, 500);

                toolResults.Add(new LlmToolResult { CallId = toolCall.CallId, Output = processedResult });
                toolCallRecords.Add(new ToolCallRecord
                {
                    ToolName = toolCall.FunctionName,
                    Arguments = toolCall.ArgumentsJson,
                    ResultSummary = summary,
                    ResultLength = resultText.Length,
                    DurationMs = toolSw.Elapsed.TotalMilliseconds,
                });

                onToken?.Invoke($" ✓ ({toolSw.Elapsed.TotalMilliseconds:F0}ms)\n");
                _logger.Debug("Tool result ({Tool}): {Summary}", toolCall.FunctionName, summary);
                _recentFailedCalls.Remove(callSignature);
            }
            catch (OperationCanceledException)
            {
                var cancelMsg = $"Tool '{toolCall.FunctionName}' was cancelled (session interrupted).";
                _logger.Information("Tool call cancelled: {Tool}", toolCall.FunctionName);
                toolResults.Add(new LlmToolResult { CallId = toolCall.CallId, Output = cancelMsg });
                toolCallRecords.Add(new ToolCallRecord
                {
                    ToolName = toolCall.FunctionName,
                    Arguments = toolCall.ArgumentsJson,
                    ResultSummary = cancelMsg,
                    ResultLength = cancelMsg.Length,
                    IsError = true,
                    DurationMs = toolSw.Elapsed.TotalMilliseconds,
                });
                onToken?.Invoke(" ⏹ cancelled\n");
                hadCancellation = true;
                hadExecutionError = true;
                // Don't process remaining tool calls — session is ending
                break;
            }
            catch (NotImplementedException)
            {
                var notImplMsg = $"Tool '{toolCall.FunctionName}' exists but is not yet implemented. Use find_tools to search for an alternative, or use the core tools.";
                _logger.Warning("Unimplemented tool called: {Tool}", toolCall.FunctionName);
                toolResults.Add(new LlmToolResult { CallId = toolCall.CallId, Output = notImplMsg });
                toolCallRecords.Add(new ToolCallRecord
                {
                    ToolName = toolCall.FunctionName,
                    Arguments = toolCall.ArgumentsJson,
                    ResultSummary = notImplMsg,
                    ResultLength = notImplMsg.Length,
                    IsError = true,
                    DurationMs = toolSw.Elapsed.TotalMilliseconds,
                });
                onToken?.Invoke(" 🚧 not implemented\n");
                hadExecutionError = true;
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error executing {toolCall.FunctionName}: {ex.Message}";
                _logger.Error(ex, "Tool execution failed: {Tool}", toolCall.FunctionName);
                toolResults.Add(new LlmToolResult { CallId = toolCall.CallId, Output = errorMsg });
                toolCallRecords.Add(new ToolCallRecord
                {
                    ToolName = toolCall.FunctionName,
                    Arguments = toolCall.ArgumentsJson,
                    ResultSummary = errorMsg,
                    ResultLength = errorMsg.Length,
                    IsError = true,
                    DurationMs = toolSw.Elapsed.TotalMilliseconds,
                });
                onToken?.Invoke(" ❌ error\n");
                TrackFailedCall(callSignature);
                hadExecutionError = true;
            }
        }

        return new ToolExecutionResult
        {
            ToolResults = toolResults,
            ToolCallRecords = toolCallRecords,
            HadExecutionErrors = hadExecutionError,
            WasCancelled = hadCancellation,
        };
    }

    private void TrackFailedCall(string callSignature)
    {
        if (_recentFailedCalls.Add(callSignature))
        {
            _failedCallOrder.Enqueue(callSignature);
            // Evict oldest entries instead of clearing all — preserves recent tracking
            while (_failedCallOrder.Count > MaxRecentFailedCalls)
                _recentFailedCalls.Remove(_failedCallOrder.Dequeue());
        }
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength] + "...";
}

public sealed record ToolExecutionResult
{
    public required IReadOnlyList<LlmToolResult> ToolResults { get; init; }
    public required IReadOnlyList<ToolCallRecord> ToolCallRecords { get; init; }
    public bool HadExecutionErrors { get; init; }
    /// <summary>True if any tool call was cancelled (e.g., by Ctrl+C). Used to suppress
    /// progressive deepening and failure nudges — cancelled operations are not real failures.</summary>
    public bool WasCancelled { get; init; }
}
