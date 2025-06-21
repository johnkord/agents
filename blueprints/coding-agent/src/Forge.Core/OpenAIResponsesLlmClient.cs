using System.Text.Json;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Responses;
using Serilog;

namespace Forge.Core;

/// <summary>
/// ILlmClient implementation using OpenAI's /v1/responses endpoint with
/// client-managed context (no PreviousResponseId).
///
/// WHY: PreviousResponseId chains the full conversation server-side, meaning
/// every tool result from step 0 is included in step N's prompt — causing
/// monotonic token growth. A 20-step task can hit 500K+ tokens.
///
/// INSTEAD: We manage our own conversation state as a rolling window.
/// Each call to GetStreamingResponseAsync builds InputItems from scratch:
///   1. Developer message (system prompt)
///   2. User task message
///   3. Compressed summary of older turns (if any)
///   4. Last N turn-pairs in full (assistant response + tool results)
///   5. Any new inputs (tool results, nudge messages)
///
/// This implements the "sawtooth" context pattern from the design:
/// context grows for a few turns, then older turns get compressed into
/// a summary. Per-step token cost stays roughly constant.
///
/// Research basis:
/// - Pensieve/StateLM (2026): 1/4 active context, +5-12% accuracy
/// - Active Context Compression (2026): 22.7% token savings, 0% accuracy loss
/// - SWE-Pruner (2026): read operations are 76% of token cost
/// </summary>
public sealed class OpenAIResponsesLlmClient : ILlmClient
{
    private readonly ResponsesClient _client;
    private readonly string _model;
    private readonly AgentOptions _agentOptions;
    private readonly ILogger _logger;
    private readonly List<ResponseTool> _tools = [];
    private readonly HashSet<string> _knownToolNames = new(StringComparer.OrdinalIgnoreCase);

    // ── Conversation state (managed by us, not the server) ─────────────────
    private string? _systemMessage;
    private string? _userTask;
    private readonly List<ConversationTurn> _turns = [];
    private readonly List<ResponseItem> _pendingInputs = []; // tool results, nudges
    private ReasoningEffort? _reasoningEffortOverride; // set by progressive deepening

    /// <summary>Default number of recent turns to keep uncompressed.</summary>
    private const int DefaultKeepRecentTurns = 8;
    /// <summary>During editing phases (replace/create), keep more context for verification.</summary>
    private const int EditPhaseKeepRecentTurns = 12;
    /// <summary>During exploration (read/grep/list), compress faster.</summary>
    private const int ExplorePhaseKeepRecentTurns = 6;

    public OpenAIResponsesLlmClient(
        ResponsesClient client,
        string model,
        AgentOptions agentOptions,
        IReadOnlyList<AITool> tools,
        ILogger logger)
    {
        _client = client;
        _model = model;
        _agentOptions = agentOptions;
        _logger = logger;

        // Bridge MCP tools (AIFunction) to OpenAI FunctionTool format
        UpdateTools(tools);
    }

    public void UpdateTools(IReadOnlyList<AITool> tools)
    {
        foreach (var tool in tools.OfType<AIFunction>())
        {
            if (_knownToolNames.Contains(tool.Name)) continue; // already registered

            var parametersJson = tool.JsonSchema.ValueKind != JsonValueKind.Undefined
                ? BinaryData.FromString(tool.JsonSchema.ToString())
                : BinaryData.FromString("{}");

            _tools.Add(ResponseTool.CreateFunctionTool(
                functionName: tool.Name,
                functionParameters: parametersJson,
                strictModeEnabled: false,
                functionDescription: tool.Description));
            _knownToolNames.Add(tool.Name);
        }
    }

    public void AddSystemMessage(string content) => _systemMessage = content;

    public void SetReasoningEffort(ReasoningEffort? effort) => _reasoningEffortOverride = effort;
    public void AddUserMessage(string content)
    {
        if (_userTask is null)
            _userTask = content;
        else
            _pendingInputs.Add(ResponseItem.CreateUserMessageItem(content));
    }

    public void AddToolResults(IEnumerable<LlmToolResult> results)
    {
        foreach (var result in results)
            _pendingInputs.Add(ResponseItem.CreateFunctionCallOutputItem(result.CallId, result.Output));

        // Attach these tool results to the LAST turn (the one that requested them)
        // so that when that turn is compressed, its function calls and their results
        // are compressed together — preventing orphaned call_id references.
        if (_turns.Count > 0)
        {
            _turns[^1].ToolResultItems.AddRange(_pendingInputs);
            _pendingInputs.Clear();
        }
    }

    public async Task<LlmResponse> GetStreamingResponseAsync(
        OnTokenReceived? onToken = null,
        CancellationToken cancellationToken = default)
    {
        var options = BuildRequestOptions();

        // Stream the response
        var textParts = new List<string>();
        var toolCalls = new List<LlmToolCall>();
        var pendingFunctionCalls = new Dictionary<string, (string Name, string CallId)>();
        int promptTokens = 0, completionTokens = 0;
        var rawAssistantItems = new List<ResponseItem>();

        await foreach (var update in _client.CreateResponseStreamingAsync(options)
            .WithCancellation(cancellationToken))
        {
            if (update is StreamingResponseOutputTextDeltaUpdate textDelta)
            {
                textParts.Add(textDelta.Delta);
                onToken?.Invoke(textDelta.Delta);
            }
            else if (update is StreamingResponseFunctionCallArgumentsDoneUpdate funcDone)
            {
                var funcName = "unknown";
                var callId = funcDone.ItemId;
                if (pendingFunctionCalls.TryGetValue(funcDone.ItemId, out var pending))
                {
                    funcName = pending.Name;
                    callId = pending.CallId;
                }
                toolCalls.Add(new LlmToolCall
                {
                    CallId = callId,
                    FunctionName = funcName,
                    ArgumentsJson = funcDone.FunctionArguments?.ToString() ?? "{}",
                });
            }
            else if (update is StreamingResponseOutputItemAddedUpdate itemAdded
                && itemAdded.Item is FunctionCallResponseItem funcCall)
            {
                pendingFunctionCalls[funcCall.Id ?? ""] = (funcCall.FunctionName, funcCall.CallId);
            }
            else if (update is StreamingResponseCompletedUpdate completed)
            {
                var usage = completed.Response?.Usage;
                if (usage is not null)
                {
                    promptTokens = usage.InputTokenCount;
                    completionTokens = usage.OutputTokenCount;
                }
                // Capture output items for our conversation history
                if (completed.Response?.OutputItems is { } items)
                {
                    rawAssistantItems.AddRange(items);
                }
            }
        }

        // Record this turn in our conversation history
        var assistantText = textParts.Count > 0 ? string.Join("", textParts) : null;
        _turns.Add(new ConversationTurn
        {
            AssistantText = assistantText,
            ToolCalls = toolCalls.Select(tc => (tc.FunctionName, Truncate(tc.ArgumentsJson, 80))).ToList(),
            AssistantOutputItems = rawAssistantItems,
            // ToolResultItems starts empty — gets populated when AddToolResults() is called
            // with the results of executing THIS turn's tool calls.
        });

        // Any pending inputs that weren't tool results (e.g. nudge messages added
        // before this call) were already included in the request. Clear them.
        _pendingInputs.Clear();

        var keepRecent = GetAdaptiveKeepRecentTurns();
        _logger.Debug("Context: {TurnCount} turns ({Kept} full, {Compressed} compressed, window={Window}), {PromptTokens} prompt tokens",
            _turns.Count, Math.Min(_turns.Count, keepRecent),
            Math.Max(0, _turns.Count - keepRecent), keepRecent, promptTokens);

        return new LlmResponse
        {
            Text = assistantText,
            ToolCalls = toolCalls,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
        };
    }

    // ── Context assembly (the core of the sawtooth pattern) ────────────────

    private CreateResponseOptions BuildRequestOptions()
    {
        var options = new CreateResponseOptions
        {
            Model = _model,
            StreamingEnabled = true,
        };

        // Temperature / reasoning (override takes precedence for progressive deepening)
        var activeEffort = _reasoningEffortOverride ?? _agentOptions.ReasoningEffort;
        if (!activeEffort.HasValue)
        {
            options.Temperature = _agentOptions.Temperature;
        }
        if (activeEffort.HasValue)
        {
            var effortLevel = activeEffort.Value switch
            {
                Microsoft.Extensions.AI.ReasoningEffort.Low => ResponseReasoningEffortLevel.Low,
                Microsoft.Extensions.AI.ReasoningEffort.Medium => ResponseReasoningEffortLevel.Medium,
                Microsoft.Extensions.AI.ReasoningEffort.High => ResponseReasoningEffortLevel.High,
                _ => (ResponseReasoningEffortLevel?)null,
            };
            if (effortLevel.HasValue)
            {
                options.ReasoningOptions = new ResponseReasoningOptions
                {
                    ReasoningEffortLevel = effortLevel.Value,
                };
            }
        }

        // Tools
        foreach (var tool in _tools)
            options.Tools.Add(tool);

        // ── Build InputItems: the sawtooth context window ──────────────────

        // 1. System prompt
        if (_systemMessage is not null)
            options.InputItems.Add(ResponseItem.CreateDeveloperMessageItem(_systemMessage));

        // 2. User task
        if (_userTask is not null)
            options.InputItems.Add(ResponseItem.CreateUserMessageItem(_userTask));

        // 3. Compressed summary of older turns (if any)
        var keepRecent = GetAdaptiveKeepRecentTurns();
        if (_turns.Count > keepRecent)
        {
            var olderTurns = _turns.Take(_turns.Count - keepRecent);
            var recentFilePaths = GetRecentFilePaths(keepRecent);
            var summary = CompressTurns(olderTurns, recentFilePaths);
            options.InputItems.Add(ResponseItem.CreateDeveloperMessageItem(
                $"[Earlier steps compressed — {_turns.Count - keepRecent} turns]\n{summary}"));
        }

        // 4. Recent turns in full (assistant output items + tool result items)
        var recentStart = Math.Max(0, _turns.Count - keepRecent);
        for (int i = recentStart; i < _turns.Count; i++)
        {
            var turn = _turns[i];
            foreach (var item in turn.AssistantOutputItems)
                options.InputItems.Add(item);
            foreach (var item in turn.ToolResultItems)
                options.InputItems.Add(item);
        }

        // 5. Pending new inputs (tool results from current step, nudge messages)
        foreach (var item in _pendingInputs)
            options.InputItems.Add(item);

        return options;
    }

    /// <summary>
    /// Compress older turns into a concise summary.
    /// Uses relevance-weighted compression: turns involving files still active
    /// in recent turns get fuller previews; others get minimal summaries.
    ///
    /// Research basis:
    ///   - SWE-Pruner (2026): goal-driven pruning improves accuracy while cutting tokens
    ///   - Active Context Compression (2026): 22.7% savings with 0% accuracy loss
    /// </summary>
    private static string CompressTurns(IEnumerable<ConversationTurn> turns, HashSet<string> recentFilePaths)
    {
        var lines = new List<string>();
        foreach (var turn in turns)
        {
            // Check if this turn involves files still active in the recent window
            var isRelevant = turn.ToolCalls.Any(tc =>
                recentFilePaths.Any(fp => tc.ArgsPreview.Contains(fp, StringComparison.OrdinalIgnoreCase)));

            if (turn.AssistantText is { Length: > 0 } text)
            {
                // Sticky assumption breadcrumb: if the agent stated assumptions in this turn,
                // preserve them across compression so the agent remembers its chosen interpretation
                // even 15+ steps later. Without this, assumptions from step 1 get compressed
                // to 80 chars and lost by step 10.
                // Research basis: Ambig-SWE (ICLR 2026) — stated assumptions are the highest-value
                // planning artifact. AwN — explicit assumptions interrupt overconfidence pipeline.
                var extracted = AgentLoop.ContainsAssumptionReasoning(text)
                    ? AgentLoop.ExtractAssumptionText(text)
                    : null;
                if (extracted is not null)
                    lines.Add($"  [Stated assumptions: {extracted}]");

                // Sticky pivot breadcrumb: preserve WHY the agent changed approach.
                // Pivot reasons get compressed away by sawtooth but are the highest-value
                // signal for understanding the session narrative.
                // Research basis: AriadneMem (transition history), SAMULE meso-level.
                var pivotExtracted = AgentLoop.ContainsPivotReasoning(text)
                    ? AgentLoop.ExtractPivotReason(text)
                    : null;
                if (pivotExtracted is not null)
                    lines.Add($"  [Pivot: {pivotExtracted}]");

                var maxLen = isRelevant ? 200 : 80;
                var preview = text.Length > maxLen ? text[..maxLen] + "..." : text;
                lines.Add($"  Thought: {preview}");
            }
            foreach (var (name, argsPreview) in turn.ToolCalls)
            {
                lines.Add($"  → {name}({argsPreview})");
            }
            // Relevant turns get full result previews; others get tool-call-only summaries
            if (isRelevant)
            {
                foreach (var item in turn.ToolResultItems)
                {
                    var resultText = item switch
                    {
                        FunctionCallOutputResponseItem output => output.FunctionOutput,
                        _ => null,
                    };
                    if (resultText is { Length: > 0 })
                    {
                        var resultPreview = resultText.Length > 400 ? resultText[..400] + "..." : resultText;
                        lines.Add($"    Result: {resultPreview}");
                    }
                }
            }
            else
            {
                // Sticky file summaries: when compressing read_file/grep_search results,
                // leave a 1-line breadcrumb so the agent knows this content was seen.
                // Research basis: TraceMem (episodic → semantic distillation),
                // MemoryOS (mid-term tier preserves task-relevant facts).
                foreach (var (name, argsPreview) in turn.ToolCalls)
                {
                    if (name is "read_file")
                    {
                        lines.Add($"    [Read: {argsPreview} — content was loaded here]");
                    }
                    else if (name is "grep_search" or "file_search")
                    {
                        // Extract the match count from the corresponding result if available
                        var matchResult = turn.ToolResultItems
                            .OfType<FunctionCallOutputResponseItem>()
                            .Select(o => o.FunctionOutput)
                            .FirstOrDefault(r => r?.Contains("match", StringComparison.OrdinalIgnoreCase) == true
                                              || r?.Contains("file(s) found", StringComparison.OrdinalIgnoreCase) == true);
                        var brief = matchResult is { Length: > 0 }
                            ? matchResult.Length > 120 ? matchResult[..120] + "..." : matchResult
                            : "results available";
                        lines.Add($"    [Searched: {argsPreview} → {brief}]");
                    }
                }
            }
        }
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Determine KeepRecentTurns based on the current task phase.
    /// Editing phases (replace/create) need more context for verification loops.
    /// Exploration phases (read/grep/list) can compress faster.
    /// </summary>
    private int GetAdaptiveKeepRecentTurns()
    {
        if (_turns.Count < 3) return DefaultKeepRecentTurns;

        // Look at last 3 turns to detect the current phase
        var recentTools = _turns.TakeLast(3)
            .SelectMany(t => t.ToolCalls)
            .Select(tc => tc.Name)
            .ToList();

        var editCount = recentTools.Count(t =>
            t is "replace_string_in_file" or "create_file" or "run_tests" or "get_errors");

        if (editCount >= 2)
            return EditPhaseKeepRecentTurns; // editing — keep more context

        var exploreCount = recentTools.Count(t =>
            t is "read_file" or "grep_search" or "list_directory" or "file_search" or "semantic_search");

        if (exploreCount >= 2)
            return ExplorePhaseKeepRecentTurns; // exploring — compress faster

        return DefaultKeepRecentTurns;
    }

    /// <summary>
    /// Extract file paths mentioned in recent turns to guide relevance-weighted compression.
    /// </summary>
    private HashSet<string> GetRecentFilePaths(int keepRecent)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var recentStart = Math.Max(0, _turns.Count - keepRecent);
        for (int i = recentStart; i < _turns.Count; i++)
        {
            foreach (var (_, argsPreview) in _turns[i].ToolCalls)
            {
                // Extract file-like paths from args preview (contain / and an extension)
                var parts = argsPreview.Split('"', '(', ')', ',', ' ');
                foreach (var part in parts)
                {
                    if (part.Contains('/') && part.Contains('.'))
                    {
                        // Use just the filename for matching (more robust than full paths)
                        var fileName = part.Split('/')[^1];
                        if (fileName.Length > 2)
                            paths.Add(fileName);
                    }
                }
            }
        }
        return paths;
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength] + "...";

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ── Internal conversation state ────────────────────────────────────────

    private sealed class ConversationTurn
    {
        public string? AssistantText { get; init; }
        public List<(string Name, string ArgsPreview)> ToolCalls { get; init; } = [];
        public List<ResponseItem> AssistantOutputItems { get; init; } = [];
        public List<ResponseItem> ToolResultItems { get; init; } = [];
    }
}
