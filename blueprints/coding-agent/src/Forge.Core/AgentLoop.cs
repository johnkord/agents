using System.Diagnostics;
using Microsoft.Extensions.AI;
using Serilog;

namespace Forge.Core;

/// <summary>
/// Callback invoked for each text token streamed from the LLM.
/// </summary>
public delegate void OnTokenReceived(string token);

/// <summary>
/// The core agent loop with research-informed features:
///   - Plan→Act→Verify prompting with grounded thinking and verification checklists
///   - Progressive deepening: escalate reasoning effort on consecutive failures
///   - LESSONS.md: cross-session learning via Reflexion-style verbal memory
///   - Failure taxonomy: categorize failures for targeted recovery nudges
///   - Observation pipeline and duplicate detection via ToolExecutor
///
/// Research basis (2026 review):
///   - DeepVerifier: structured verification gives 8-11% accuracy gain
///   - Kimi k1.5 / Art of Scaling: adaptive compute allocation outperforms uniform by 4-8x
///   - Reflexion (2023): verbal self-reflection achieves 91% HumanEval without weight updates
///   - DEFT/DeepVerifier: categorized failure taxonomy enables targeted recovery
/// </summary>
public sealed class AgentLoop
{
    private readonly AgentOptions _options;
    private readonly Guardrails _guardrails;
    private readonly ILogger _logger;

    public AgentLoop(AgentOptions options, ILogger logger)
    {
        _options = options;
        _guardrails = new Guardrails(options);
        _logger = logger;
    }

    public async Task<AgentResult> RunAsync(
        string task,
        ILlmClient llmClient,
        IReadOnlyList<AITool> allTools,
        OnTokenReceived? onToken = null,
        CancellationToken cancellationToken = default,
        string? continuationContext = null)
    {
        var sessionSw = Stopwatch.StartNew();
        await using var eventLog = await EventLog.CreateAsync(_options.SessionsPath, task, _options.Model, _options.WorkspacePath);

        // Set up dynamic tool registry
        var registry = new ToolRegistry();
        registry.RegisterAll(allTools);

        // Apply tool mode restriction for subagent processes
        if (_options.ToolMode is not null)
        {
            registry.ApplyMode(_options.ToolMode);
            _logger.Information("Tool mode: {Mode}", _options.ToolMode);
        }

        var (total, active, core) = registry.GetStats();
        _logger.Information("Tool registry: {Core} core / {Total} total ({Inactive} discoverable via find_tools)",
            core, total, total - core);

        // Create a git checkpoint before making changes (4B: rollback capability).
        // The sha is logged at creation time for manual rollback via `git stash apply <sha>`.
        CreateGitCheckpoint();

        // Load lessons from previous sessions (Reflexion-style cross-session learning)
        var lessons = LoadLessons();

        // Generate or load cached REPO.md (repository structural map)
        var repoMap = RepoMapGenerator.GenerateOrLoadCached(_options.WorkspacePath);
        if (repoMap is not null)
            _logger.Information("REPO.md loaded ({Length} chars)", repoMap.Length);

        // Auto-activate run_subagent for complex tasks (avoids find_tools discovery step)
        if (_options.ToolMode is null && TaskLooksComplex(task))
        {
            registry.Activate("run_subagent");
            _logger.Information("Auto-activated run_subagent (complex task detected)");
        }

        // Initialize conversation with lessons-aware system prompt + REPO.md
        // Pass available tool names so the prompt only references registered tools
        // Pass task so the debugging protocol is conditionally included
        llmClient.AddSystemMessage(SystemPrompt.Build(_options, lessons, repoMap, ToolRegistry.GetCoreToolNames(), task));

        // For resumed sessions, inject continuationContext as the user message
        // (contains handoff summary + discovery context). The original task name
        // is preserved for metadata (filename, handoff note, logs).
        if (continuationContext is not null)
        {
            llmClient.AddUserMessage(continuationContext);
        }
        else
        {
            llmClient.AddUserMessage(task);
        }

        var steps = new List<StepRecord>();
        int totalPromptTokens = 0;
        int totalCompletionTokens = 0;
        var toolExecutor = new ToolExecutor(_options, _guardrails, _logger);
        var verificationState = new VerificationState();
        var verificationTracker = new VerificationTracker();
        int consecutiveFailures = 0;
        const int MaxConsecutiveFailures = 3;
        const int ProgressiveDeepeningThreshold = 2;
        bool isDeepened = false;
        bool boundaryWarningIssued = false;
        string? consolidationSummary = null; // Captured when boundary warning fires
        string? midSessionCheckpoint = null; // Quiet 50% checkpoint for handoff fallback
        string? assumptionsText = null; // Captured from early steps when agent states assumptions
        var pivotReasons = new List<string>(); // Captured when agent changes approach (AriadneMem transition history)

        _logger.Information("Starting task: {Task}", task);

        for (int stepNum = 0; stepNum < _options.MaxSteps; stepNum++)
        {
            // Check for cancellation (e.g., Ctrl+C) before each step
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.Information("Session cancelled by user");
                onToken?.Invoke("\n  ⏹ Session cancelled\n");
                return await Finish(false, "Stopped: session cancelled by user.",
                    steps, totalPromptTokens, totalCompletionTokens, sessionSw, eventLog, task, consolidationSummary, assumptionsText, pivotReasons);
            }

            // Check resource limits
            var (exceeded, limitReason) = _guardrails.CheckLimits(stepNum, totalPromptTokens + totalCompletionTokens);
            if (exceeded)
            {
                _logger.Warning("Limit reached: {Reason}", limitReason);
                // Use mid-session checkpoint as consolidation fallback (same as max-steps path)
                consolidationSummary ??= midSessionCheckpoint;
                return await Finish(false, $"Stopped: {limitReason}", steps, totalPromptTokens, totalCompletionTokens, sessionSw, eventLog, task, consolidationSummary, assumptionsText, pivotReasons);
            }

            // Proactive boundary detection (BAO pattern): warn at 80% budget
            if (!boundaryWarningIssued)
            {
                var totalTokens = totalPromptTokens + totalCompletionTokens;
                var tokenRatio = (double)totalTokens / _options.MaxTotalTokens;
                var stepRatio = (double)stepNum / _options.MaxSteps;

                // Mid-session checkpoint at 50%: quietly capture the agent's current
                // narrative without injecting a consolidation prompt. This provides a
                // fallback for sessions that fail between 50-80% budget.
                // Research basis: Steve-Evolving (experience anchoring at subgoal boundaries),
                // Mem-α (multi-tier memory with episodic checkpoints).
                if (midSessionCheckpoint is null && _options.MaxSteps > 8
                    && (tokenRatio >= 0.5 || stepRatio >= 0.5))
                {
                    // Capture the most recent substantive thought as a quiet checkpoint
                    var recentThought = steps.LastOrDefault(s =>
                        s.Thought is { Length: > 80 } && s.ToolCalls.Count > 0)?.Thought;
                    if (recentThought is not null)
                    {
                        midSessionCheckpoint = recentThought.Length > 1000
                            ? recentThought[..1000] + "..."
                            : recentThought;
                        _logger.Debug("Mid-session checkpoint captured ({Length} chars)", midSessionCheckpoint.Length);
                    }
                }

                // Lower threshold for short sessions so consolidation fires with time to respond
                var stepThreshold = _options.MaxSteps <= 5 ? 0.6 : 0.8;
                if (tokenRatio >= 0.8 || stepRatio >= stepThreshold)
                {
                    var remaining = stepRatio >= stepThreshold
                        ? $"{_options.MaxSteps - stepNum} steps"
                        : $"{(_options.MaxTotalTokens - totalTokens):N0} tokens";

                    llmClient.AddUserMessage(
                        $"⚠️ Approaching session limit ({remaining} remaining). " +
                        "If the task is not yet complete, consolidate your progress now:\n" +
                        "1. Summarize what you've done and what remains\n" +
                        "2. List any assumptions you made and whether they were validated\n" +
                        "3. Identify the highest-risk remaining step\n" +
                        "4. List the specific next steps someone would need to continue\n" +
                        "5. Then continue working if you can finish in the remaining budget");

                    onToken?.Invoke($"\n  ⏳ Approaching limit ({remaining} remaining) — consolidation prompt injected\n");
                    _logger.Information("Proactive boundary detection: {Remaining} remaining", remaining);
                    boundaryWarningIssued = true;
                }
            }

            var stepSw = Stopwatch.StartNew();

            // Sync the LLM's tool list with the registry (may have changed via find_tools)
            llmClient.UpdateTools(registry.GetActiveTools());

            // Stream the LLM response
            LlmResponse response;
            try
            {
                response = await llmClient.GetStreamingResponseAsync(onToken, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.Information("LLM call cancelled at step {Step}", stepNum);
                return await Finish(false, "Stopped: session cancelled by user.",
                    steps, totalPromptTokens, totalCompletionTokens, sessionSw, eventLog, task, consolidationSummary, assumptionsText, pivotReasons);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "LLM call failed at step {Step}", stepNum);
                return await Finish(false, $"LLM error: {ex.Message}", steps, totalPromptTokens, totalCompletionTokens, sessionSw, eventLog, task, consolidationSummary, assumptionsText, pivotReasons);
            }

            totalPromptTokens += response.PromptTokens;
            totalCompletionTokens += response.CompletionTokens;

            // Capture the agent's consolidation response when boundary warning was just issued.
            // Only capture substantive responses, not brief tool-calling thoughts.
            // Research basis: L2MAC (agent manages its own knowledge store).
            if (boundaryWarningIssued && consolidationSummary is null
                && response.Text is { Length: > 100 }
                && (response.Text.Length > 200 || response.Text.Contains('\n')
                    || response.Text.Contains("step", StringComparison.OrdinalIgnoreCase)))
            {
                consolidationSummary = response.Text;
                _logger.Debug("Captured consolidation summary ({Length} chars)", consolidationSummary.Length);
            }

            // No tool calls → agent is done
            if (response.ToolCalls.Count == 0)
            {
                var finalText = response.Text ?? "(no response)";
                _logger.Information("Agent completed in {Steps} steps", stepNum + 1);

                var finalStep = new StepRecord
                {
                    StepNumber = stepNum,
                    Timestamp = DateTimeOffset.UtcNow,
                    Thought = finalText,
                    PromptTokens = response.PromptTokens,
                    CompletionTokens = response.CompletionTokens,
                    DurationMs = stepSw.Elapsed.TotalMilliseconds,
                };
                steps.Add(finalStep);
                await eventLog.RecordStepAsync(finalStep);

                return await Finish(true, finalText, steps, totalPromptTokens, totalCompletionTokens, sessionSw, eventLog, task, consolidationSummary, assumptionsText, pivotReasons);
            }

            // Execute tool calls
            var toolExecution = await toolExecutor.ExecuteAsync(
                response.ToolCalls,
                registry.GetActiveTools(),
                onToken,
                cancellationToken);

            // Update verification state based on tool results (FuseSearch-inspired redundancy tracking)
            // Check redundancy BEFORE updating state, so intra-step parallel calls
            // (e.g., run_tests + dotnet build in the same step) don't flag each other.
            var resultsWithHints = AppendRedundancyHints(
                toolExecution.ToolResults, toolExecution.ToolCallRecords, verificationState);
            UpdateVerificationState(verificationState, toolExecution.ToolCallRecords, stepNum);

            // Send tool results back to the LLM
            llmClient.AddToolResults(resultsWithHints);

            // Verification tracking: check if the agent followed up file edits with verification.
            // Inject a reminder only when verification is overdue (CoRefine-inspired: cheap detect → targeted intervene).
            var verificationReminder = verificationTracker.RecordStep(stepNum, toolExecution.ToolCallRecords);
            if (verificationReminder is not null && !toolExecution.WasCancelled)
            {
                llmClient.AddUserMessage(verificationReminder);
                onToken?.Invoke($"\n  🔍 {verificationReminder}\n");
                _logger.Information("Verification reminder injected at step {Step}", stepNum);
            }

            // Failure handling: progressive deepening + taxonomy-aware nudging
            // Skip failure handling entirely if the session was cancelled — cancelled
            // tool calls are not real failures and shouldn't trigger escalation.
            bool stepHadExecutionErrors = toolExecution.HadExecutionErrors;
            if (stepHadExecutionErrors && !toolExecution.WasCancelled)
            {
                consecutiveFailures++;

                // Progressive deepening: escalate reasoning effort after threshold
                if (consecutiveFailures >= ProgressiveDeepeningThreshold && !isDeepened
                    && _options.ReasoningEffort.HasValue
                    && _options.ReasoningEffort.Value != ReasoningEffort.High)
                {
                    llmClient.SetReasoningEffort(ReasoningEffort.High);
                    isDeepened = true;
                    onToken?.Invoke("\n  🧠 Escalating reasoning effort to High\n");
                    _logger.Information("Progressive deepening: escalated to High reasoning after {Count} failures", consecutiveFailures);
                }

                // Failure-taxonomy-aware nudging
                // Nudge at 2+ failures for debugging-related types (TestFailure, Unknown)
                // to enable L2 monologue reasoning escalation.
                // Other failure types nudge at 3+ (the original threshold).
                var failureType = ClassifyFailure(toolExecution.ToolCallRecords);
                var nudgeThreshold = failureType is FailureType.TestFailure or FailureType.Unknown
                    ? ProgressiveDeepeningThreshold  // 2
                    : MaxConsecutiveFailures;         // 3

                if (consecutiveFailures >= nudgeThreshold)
                {
                    var nudge = BuildFailureNudge(consecutiveFailures, failureType);
                    llmClient.AddUserMessage(nudge);
                    onToken?.Invoke($"\n  ⚠️ {consecutiveFailures} consecutive failures ({failureType}) — nudging re-plan\n");
                    _logger.Warning("Consecutive failures: {Count} ({Type}), injecting re-plan nudge", consecutiveFailures, failureType);
                }
            }
            else
            {
                // Reset on success
                if (consecutiveFailures > 0)
                {
                    consecutiveFailures = 0;
                    if (isDeepened)
                    {
                        // Restore original reasoning effort after recovery
                        llmClient.SetReasoningEffort(_options.ReasoningEffort);
                        isDeepened = false;
                        _logger.Debug("Progressive deepening: restored reasoning effort after success");
                    }
                }
            }

            var step = new StepRecord
            {
                StepNumber = stepNum,
                Timestamp = DateTimeOffset.UtcNow,
                Thought = response.Text,
                ToolCalls = toolExecution.ToolCallRecords,
                PromptTokens = response.PromptTokens,
                CompletionTokens = response.CompletionTokens,
                DurationMs = stepSw.Elapsed.TotalMilliseconds,
            };
            steps.Add(step);
            await eventLog.RecordStepAsync(step);

            // Hypothesis detection: log when the agent's reasoning contains hypothesis indicators.
            // Zero infrastructure cost — enables future analysis of hypothesis quality.
            // Research basis: FVDebug (for-and-against prompting), A2P (counterfactual reasoning).
            if (ContainsHypothesisReasoning(response.Text))
                _logger.Debug("Hypothesis reasoning detected in step {Step}", stepNum);

            // Assumption detection: capture when the agent states interpretation assumptions.
            // Only capture from early steps (0-2) — these are planning-phase assumptions.
            // Later "assumptions" are typically mid-execution reasoning, not intent interpretation.
            // Research basis: Ambig-SWE (ICLR 2026) — exploration-first, then state assumptions.
            if (stepNum <= 2 && assumptionsText is null && ContainsAssumptionReasoning(response.Text))
            {
                assumptionsText = ExtractAssumptionText(response.Text);
                _logger.Debug("Assumption reasoning detected in step {Step}: {Preview}",
                    stepNum, assumptionsText?.Length > 80 ? assumptionsText[..80] + "..." : assumptionsText);
            }

            // Pivot detection: capture when the agent explicitly changes approach.
            // The pivot REASON ("config values are runtime-computed, not from file") is the
            // highest-value signal for handoffs — it tells the next session WHY the approach changed.
            // This text gets compressed away by the sawtooth pattern if not captured here.
            // Research basis: AriadneMem (transition history), SAMULE meso-level, Nemori (event boundaries).
            if (ContainsPivotReasoning(response.Text))
            {
                var pivotReason = ExtractPivotReason(response.Text);
                if (pivotReason is not null)
                {
                    pivotReasons.Add($"[Step {stepNum}] {pivotReason}");
                    _logger.Debug("Pivot reasoning detected in step {Step}: {Preview}",
                        stepNum, pivotReason.Length > 80 ? pivotReason[..80] + "..." : pivotReason);
                }
            }

            _logger.Information("Step {Step}: {ToolCount} tool calls, {Tokens} tokens (cumulative: {Total:N0} / {Max:N0})",
                stepNum, toolExecution.ToolCallRecords.Count, response.PromptTokens + response.CompletionTokens,
                totalPromptTokens + totalCompletionTokens, _options.MaxTotalTokens);
        }

        _logger.Warning("Max steps reached ({Max})", _options.MaxSteps);
        var vStats = verificationTracker.GetStats();
        if (vStats.TotalEdits > 0)
            _logger.Information("Verification compliance: {Verified}/{Total} edits verified ({Rate:P0})",
                vStats.VerifiedEdits, vStats.TotalEdits, vStats.ComplianceRate);

        // Detect "work complete but budget exhausted": if all edits were verified
        // and the last step was a verification read (not an edit), the task is likely done
        // but the agent didn't get a step to report. Mark as incomplete (not failed).
        // Research: EET (arXiv:2601.05777) — detect early-termination from execution patterns.
        var workLikelyComplete = vStats.TotalEdits > 0
            && vStats.ComplianceRate >= 1.0
            && steps.Count > 0
            && steps[^1].ToolCalls.Any(tc => tc.ToolName is "read_file" or "run_tests" && !tc.IsError)
            && !steps[^1].ToolCalls.Any(tc => tc.ToolName is "replace_string_in_file" or "create_file");

        var failureMsg = workLikelyComplete
            ? $"Stopped: maximum steps ({_options.MaxSteps}) reached. Note: all edits were verified successfully — the task may be complete."
            : $"Stopped: maximum steps ({_options.MaxSteps}) reached.";

        // Use mid-session checkpoint as consolidation fallback if the 80% consolidation
        // never fired (e.g., session ran out of steps before reaching the 80% boundary).
        // Research basis: Steve-Evolving (experience anchoring at subgoal boundaries).
        consolidationSummary ??= midSessionCheckpoint;

        return await Finish(false, failureMsg, steps, totalPromptTokens, totalCompletionTokens, sessionSw, eventLog, task, consolidationSummary, assumptionsText, pivotReasons);
    }

    // ── Failure taxonomy ───────────────────────────────────────────────────

    /// <summary>
    /// Heuristic: detect tasks complex enough to benefit from subagent delegation.
    /// When true, run_subagent is auto-activated (skipping find_tools discovery step).
    /// Conservative: requires 2+ signals. False positives cost ~200 tokens/turn;
    /// false negatives cost 1 step (agent uses find_tools).
    /// </summary>
    internal static bool TaskLooksComplex(string task)
    {
        var lower = task.ToLowerInvariant();
        string[] complexitySignals =
        [
            "all files", "every file", "across the codebase", "all endpoints",
            "refactor", "migrate", "each module", "parallel", "independent",
            "untrusted", "external", "github issue", "web page",
            "multiple files", "entire project", "all tests", "full test suite",
        ];
        return complexitySignals.Count(s => lower.Contains(s)) >= 2;
    }

    /// <summary>
    /// Detect whether the agent's reasoning text contains hypothesis-driven thinking.
    /// Used for observability logging — no infrastructure impact.
    /// </summary>
    internal static bool ContainsHypothesisReasoning(string? text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        var lower = text.ToLowerInvariant();
        return lower.Contains("hypothesis")
            || lower.Contains("root cause")
            || lower.Contains("suspect")
            || (lower.Contains("if ") && lower.Contains("then ") && lower.Contains("expect"));
    }

    // Shared keyword lists for detection + extraction (prevents drift between the two)
    private static readonly string[] AssumptionKeywords =
    [
        "assuming", "assumption", "proceeding with",
        "interpreting this as", "i'll treat this as",
        "i interpret this", "i'm choosing to",
    ];

    /// <summary>
    /// Detect whether the agent's reasoning text states interpretation assumptions.
    /// Research basis: Ambig-SWE (ICLR 2026) — agents that state assumptions recover
    /// 74% of performance lost to underspecification. AwN (arXiv:2409.00557) — forcing
    /// explicit assumptions interrupts the overconfidence-hallucination pipeline.
    /// </summary>
    internal static bool ContainsAssumptionReasoning(string? text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        var lower = text.ToLowerInvariant();
        return AssumptionKeywords.Any(lower.Contains);
    }

    /// <summary>
    /// Extract a concise summary of the agent's stated assumptions from its reasoning text.
    /// Finds sentences containing assumption language and extracts them.
    /// </summary>
    internal static string? ExtractAssumptionText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;

        var sentences = text.Split(['.', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var assumptionSentences = sentences
            .Where(s =>
            {
                var lower = s.ToLowerInvariant();
                return AssumptionKeywords.Any(lower.Contains);
            })
            .Select(s => s.Trim())
            .Where(s => s.Length > 10) // filter noise
            .Take(4) // Ambig-SWE: 3-4 targeted assumptions is the sweet spot
            .ToList();

        if (assumptionSentences.Count == 0) return null;

        var result = string.Join(". ", assumptionSentences);
        return result.Length > 500 ? result[..500] + "..." : result;
    }

    private static readonly string[] PivotKeywords =
    [
        "different approach", "alternative approach",
        "let me try a different", "instead, i'll", "instead, let me",
        "that didn't work", "that won't work", "this won't work",
        "pivoting to", "switching to",
        "won't work because", "doesn't work because", "failed because",
    ];

    /// <summary>
    /// Detect whether the agent's reasoning text indicates an approach change or pivot.
    /// Pivots happen when the agent explicitly abandons one approach for another.
    /// The pivot REASON is high-value for handoffs — it tells the next session WHY,
    /// preventing it from retrying the same dead-end approach.
    /// Research basis: AriadneMem (transition history), Nemori (event boundaries),
    /// SAMULE meso-level (intra-task trajectory).
    /// </summary>
    internal static bool ContainsPivotReasoning(string? text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        var lower = text.ToLowerInvariant();
        return PivotKeywords.Any(lower.Contains);
    }

    /// <summary>
    /// Extract a concise reason for why the agent pivoted from its reasoning text.
    /// Finds sentences containing pivot language and returns the most informative one.
    /// </summary>
    internal static string? ExtractPivotReason(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;

        var sentences = text.Split(['.', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var pivotSentences = sentences
            .Where(s =>
            {
                var lower = s.ToLowerInvariant();
                return PivotKeywords.Any(lower.Contains);
            })
            .Select(s => s.Trim())
            .Where(s => s.Length > 15) // filter noise
            .Take(2) // Usually 1-2 sentences explain the pivot
            .ToList();

        if (pivotSentences.Count == 0) return null;

        var result = string.Join(". ", pivotSentences);
        return result.Length > 300 ? result[..300] + "..." : result;
    }

    /// <summary>
    /// Classify the dominant failure type from a step's tool call records.
    /// Research basis: DeepVerifier's failure taxonomy (5 major, 13 sub-categories)
    /// and DEFT's 14 failure modes show categorized failures enable targeted recovery.
    /// </summary>
    internal static FailureType ClassifyFailure(IReadOnlyList<ToolCallRecord> records)
    {
        var errors = records.Where(r => r.IsError).ToList();
        if (errors.Count == 0) return FailureType.Unknown;

        // Check for specific patterns in error messages
        foreach (var err in errors)
        {
            var summary = err.ResultSummary.ToLowerInvariant();

            // Hallucinated tool (agent called a tool that doesn't exist at all)
            // Must check before StaleContext since both contain "not found"
            if (summary.Contains("not found") && summary.Contains("use find_tools"))
                return FailureType.ToolMissing;
            if (summary.Contains("not found") && err.ToolName is "replace_string_in_file")
                return FailureType.StaleContext;
            if (summary.Contains("blocked") || summary.Contains("outside the workspace"))
                return FailureType.Blocked;
            if (summary.Contains("not implemented") || summary.Contains("not yet implemented"))
                return FailureType.ToolMissing;
            if (err.ToolName is "run_subagent" or "RunSubagent"
                && (summary.Contains("error:") || summary.Contains("timed out")))
                return FailureType.DelegationFailure;
            if (summary.Contains("compile") || summary.Contains("syntax") || summary.Contains("build failed"))
                return FailureType.SyntaxError;
            if (err.ToolName is "run_tests" && (summary.Contains("fail") || summary.Contains("assert")))
                return FailureType.TestFailure;
            if (summary.Contains("duplicate") || summary.Contains("already called"))
                return FailureType.DuplicateAttempt;
            if (err.DurationMs > 30000)
                return FailureType.Timeout;
        }

        return FailureType.Unknown;
    }

    private static string BuildFailureNudge(int consecutiveFailures, FailureType failureType)
    {
        var typeSpecificAdvice = failureType switch
        {
            FailureType.StaleContext =>
                "Your edits aren't matching the file content. Re-read the target file with read_file to get current content before editing.",
            FailureType.SyntaxError =>
                "You introduced a syntax/compile error. Read the file back, run 'dotnet build' to see the exact error, and fix the specific issue.",
            FailureType.TestFailure =>
                "Tests are failing. Read the full test error output. Identify which assertion failed and what the actual vs expected values are before making changes.",
            FailureType.Timeout =>
                "That tool call took more than 30 seconds before failing. Avoid retrying it unchanged; narrow the scope, use a faster tool, or add filters so the next attempt finishes sooner.",
            FailureType.DuplicateAttempt =>
                "You're repeating the same tool call that already failed. Try a completely different approach.",
            FailureType.Blocked =>
                "That action was blocked by guardrails. Use a different tool or approach that stays within allowed boundaries.",
            FailureType.ToolMissing =>
                "That tool isn't implemented. Use find_tools to search for an alternative, or use the core tools (read_file, grep_search, run_bash_command).",
            FailureType.DelegationFailure =>
                "The subagent returned an empty or unhelpful result. "
                + "Options: (1) Try the task inline with explore_codebase for initial context, "
                + "(2) Retry with a more specific prompt and explicit return format, "
                + "(3) Break the task into smaller pieces you can handle directly.",
            _ =>
                "Look at the error messages above and identify what's going wrong before trying again.",
        };

        // Debugging escalation: add monologue reasoning (L2) or runtime inspection (L3)
        // for debugging-related failures after repeated attempts.
        // Research basis: NExT/SemCoder (monologue), InspectCoder/ChatDBG (runtime inspection).
        var escalation = "";
        if (failureType is FailureType.TestFailure or FailureType.Unknown)
        {
            if (consecutiveFailures >= 3)
            {
                escalation = "\n\n**Escalating to runtime inspection:** "
                    + "Insert a targeted print/log statement near the suspected bug, "
                    + "run the failing test, and read the output. "
                    + "This gives you actual runtime state instead of guessing. "
                    + "Remove the diagnostic output after diagnosis.";
            }
            else if (consecutiveFailures >= 2)
            {
                escalation = "\n\n**Try monologue reasoning:** "
                    + "Stop and explain what the code does line by line in natural language. "
                    + "Trace the execution path that leads to the failure. "
                    + "What does each variable hold at each step? "
                    + "This often reveals assumptions that don't match reality.";
            }
        }

        return $"You have had {consecutiveFailures} consecutive failed steps. {typeSpecificAdvice}{escalation}\n\n"
            + "Either:\n"
            + "1. RETHINK: Fix the specific issue identified above.\n"
            + "2. ALTERNATIVE: Try a completely different strategy.\n"
            + "3. GIVE UP: Explain what you tried and why it failed.";
    }

    // ── Verification state tracking ────────────────────────────────────────

    /// <summary>
    /// Update verification state based on successful tool calls.
    /// Tracks what has been verified to detect redundant verification.
    /// Research basis: FuseSearch (arXiv:2601.19568) — tool efficiency metric.
    /// </summary>
    private static void UpdateVerificationState(
        VerificationState state, IReadOnlyList<ToolCallRecord> records, int stepNumber)
    {
        foreach (var record in records)
        {
            if (record.IsError) continue;

            // File-modifying tools invalidate prior verification
            if (record.ToolName is "replace_string_in_file" or "create_file")
            {
                state.RecordEdit(stepNumber);
                // Invalidate the read cache for the edited file
                var editedPath = ExtractFilePath(record.Arguments);
                if (editedPath is not null)
                    state.InvalidateFileRead(editedPath);
            }
            // Successful test run confirms both tests and compilation
            else if (record.ToolName is "run_tests"
                     && !record.ResultSummary.Contains("fail", StringComparison.OrdinalIgnoreCase))
            {
                state.RecordTestsPassed(stepNumber);
            }
            // Build commands confirm compilation
            else if (record.ToolName is "run_bash_command"
                     && record.Arguments.Contains("build", StringComparison.OrdinalIgnoreCase)
                     && (record.ResultSummary.Contains("exit code: 0", StringComparison.OrdinalIgnoreCase)
                         || record.ResultSummary.Contains("succeeded", StringComparison.OrdinalIgnoreCase)))
            {
                state.RecordBuildPassed(stepNumber);
            }
            // Track file reads for re-read detection
            else if (record.ToolName is "read_file")
            {
                var readPath = ExtractFilePath(record.Arguments);
                if (readPath is not null)
                    state.RecordFileRead(readPath, stepNumber);
            }
        }
    }

    /// <summary>
    /// Append redundancy hints to tool results before they enter the conversation.
    /// When the agent tries to re-verify something already confirmed, we append
    /// a note to help it skip redundant steps in the future.
    /// </summary>
    private IReadOnlyList<LlmToolResult> AppendRedundancyHints(
        IReadOnlyList<LlmToolResult> results,
        IReadOnlyList<ToolCallRecord> records,
        VerificationState verificationState)
    {
        if (results.Count != records.Count) return results;

        var modified = new List<LlmToolResult>(results.Count);
        for (int i = 0; i < results.Count; i++)
        {
            var record = records[i];
            var result = results[i];

            if (!record.IsError)
            {
                // Check verification redundancy (build-after-test, etc.)
                var hint = verificationState.CheckRedundancy(record.ToolName, record.Arguments);

                // Check file re-read redundancy
                if (hint is null && record.ToolName is "read_file")
                {
                    var filePath = ExtractFilePath(record.Arguments);
                    if (filePath is not null)
                        hint = verificationState.CheckFileReRead(filePath);
                }

                if (hint is not null)
                {
                    _logger.Debug("Redundancy detected: {Tool}", record.ToolName);
                    modified.Add(new LlmToolResult
                    {
                        CallId = result.CallId,
                        Output = result.Output + $"\n\n💡 {hint}",
                    });
                    continue;
                }
            }

            modified.Add(result);
        }

        return modified;
    }

    /// <summary>Extract filePath from tool call arguments JSON.</summary>
    private static string? ExtractFilePath(string argsJson) =>
        EpisodeSegmenter.ExtractFilePath(argsJson);

    // ── Todo plan state ────────────────────────────────────────────────────────

    /// <summary>
    /// Read the agent's todo plan state from the persistent todo file.
    /// Returns a formatted plan summary or null if no todos were created.
    /// The path mirrors ManageTodosTool's GetTodoFilePath().
    /// </summary>
    private string? LoadTodoPlanState()
    {
        try
        {
            var memoryRoot = Environment.GetEnvironmentVariable("FORGE_MEMORY_ROOT")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".forge", "memories");
            var todoPath = Path.Combine(memoryRoot, "session", "todos.json");

            if (!File.Exists(todoPath)) return null;

            var json = File.ReadAllText(todoPath);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var items = doc.RootElement.EnumerateArray().ToList();

            if (items.Count == 0) return null;

            var lines = new List<string>();
            foreach (var item in items)
            {
                var id = item.GetProperty("id").GetInt32();
                var title = item.GetProperty("title").GetString() ?? "?";
                var status = item.GetProperty("status").GetString() ?? "?";
                var icon = status switch
                {
                    "completed" => "✅",
                    "in-progress" => "🔄",
                    _ => "⬜",
                };
                lines.Add($"  {id}. {icon} {title} ({status})");
            }

            return string.Join("\n", lines);
        }
        catch
        {
            return null;
        }
    }

    // ── Git checkpoint ──────────────────────────────────────────────────────────

    /// <summary>
    /// Create a lightweight git checkpoint before the agent modifies files.
    /// Uses `git stash create` which creates a stash commit without modifying the working tree.
    /// Returns the stash sha if successful, null if git isn't available or workspace is clean.
    /// On failure, the user can rollback with `git stash apply {sha}`.
    /// </summary>
    private string? CreateGitCheckpoint()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("git", "stash create")
            {
                WorkingDirectory = _options.WorkspacePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = System.Diagnostics.Process.Start(psi);
            if (process is null) return null;

            if (!process.WaitForExit(5000))
            {
                try { process.Kill(); } catch { }
                return null;
            }

            var sha = process.StandardOutput.ReadToEnd().Trim();

            if (process.ExitCode == 0 && sha.Length > 0)
            {
                _logger.Information("Git checkpoint created: {Sha}", sha[..Math.Min(8, sha.Length)]);
                return sha;
            }
            // Exit code 0 + empty sha = clean working tree (nothing to stash)
            return null;
        }
        catch
        {
            // git not available or not a git repo — non-fatal
            return null;
        }
    }

    // ── LESSONS.md: Cross-session learning ─────────────────────────────────

    /// <summary>
    /// Load lessons from previous sessions.
    /// Reflexion (2023): verbal self-reflection achieves 91% HumanEval without weight updates.
    /// </summary>
    private string? LoadLessons()
    {
        var lessonsPath = _options.LessonsPath;
        if (string.IsNullOrEmpty(lessonsPath)) return null;

        try
        {
            if (!File.Exists(lessonsPath)) return null;

            var lines = File.ReadAllLines(lessonsPath);
            // Take the last N lessons (most recent = most relevant)
            var recentLessons = lines
                .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith('#'))
                .TakeLast(30)
                .ToList();

            if (recentLessons.Count == 0) return null;

            _logger.Information("Loaded {Count} lessons from {Path}", recentLessons.Count, lessonsPath);
            return string.Join("\n", recentLessons);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to load lessons from {Path}", lessonsPath);
            return null;
        }
    }

    /// <summary>
    /// After a failed or costly session, generate and save a lesson.
    /// </summary>
    private void SaveLesson(string task, AgentResult result)
    {
        var lessonsPath = _options.LessonsPath;
        if (string.IsNullOrEmpty(lessonsPath)) return;

        // Only save lessons for genuine failures or costly successes (>300K tokens).
        // Don't save lessons for cancelled sessions — cancellation is user-initiated,
        // not a failure. Lessons like "failed tools: read_file" from Ctrl+C are misleading.
        // Don't save lessons for "work likely complete" sessions — the task was done,
        // the agent just ran out of steps before reporting.
        // Research: SWE-ContextBench shows incorrectly attributed experience hurts.
        // Research: BREW (arXiv:2511.20297) shows noisy lessons degrade performance.
        var totalTokens = result.TotalPromptTokens + result.TotalCompletionTokens;
        if (result.Success && totalTokens < 300_000) return;
        if (result.FailureReason?.Contains("cancelled", StringComparison.OrdinalIgnoreCase) == true) return;
        if (result.FailureReason?.Contains("task may be complete", StringComparison.OrdinalIgnoreCase) == true) return;

        try
        {
            var dir = Path.GetDirectoryName(lessonsPath);
            if (dir is not null) Directory.CreateDirectory(dir);

            var lesson = GenerateLesson(task, result);
            if (lesson is null) return;

            File.AppendAllText(lessonsPath, lesson + "\n");
            _logger.Information("Saved lesson to {Path}: {Lesson}", lessonsPath, lesson);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to save lesson to {Path}", lessonsPath);
        }
    }

    /// <summary>
    /// Generate a concise lesson from a session result.
    /// Format: "- [YYYY-MM-DD] [fail|costly]: lesson text"
    /// </summary>
    internal static string? GenerateLesson(string task, AgentResult result)
    {
        var date = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");
        var totalTokens = result.TotalPromptTokens + result.TotalCompletionTokens;
        var taskPreview = task.Length > 60 ? task[..60] + "..." : task;

        if (!result.Success)
        {
            // Determine if the failure was budget exhaustion vs. a genuine execution failure
            var isBudgetExhaustion = result.FailureReason?.Contains("maximum steps") == true
                || result.FailureReason?.Contains("Maximum tokens") == true;

            var details = new List<string>();

            // For budget-exhaustion failures, don't blame incidental tool errors.
            // A session that ran out of steps didn't fail BECAUSE semantic_search errored —
            // it failed because the task was too complex for the budget.
            // Research basis: BREW (noisy lessons degrade), A2P (counterfactual attribution).
            if (!isBudgetExhaustion)
            {
                var failedTools = result.Steps
                    .SelectMany(s => s.ToolCalls)
                    .Where(tc => tc.IsError)
                    .Select(tc => tc.ToolName)
                    .Distinct()
                    .ToList();
                if (failedTools.Count > 0)
                    details.Add($"failed tools: {string.Join(", ", failedTools.Take(3))}");
            }

            // Include failure type for categorized learning (BREW-inspired rubric matching)
            var failureType = result.Steps.Count > 0
                ? ClassifyFailure(result.Steps[^1].ToolCalls)
                : FailureType.Unknown;
            if (failureType != FailureType.Unknown)
                details.Add($"type: {failureType}");

            // Include assumptions if the agent stated any — helps future sessions
            // avoid repeating wrong interpretations.
            // Research basis: BREW (categorized lessons), Ambig-SWE (wrong assumptions waste budget).
            var assumptions = ExtractAssumptionText(
                result.Steps.Take(3).Select(s => s.Thought).FirstOrDefault(t => ContainsAssumptionReasoning(t)));
            if (assumptions is not null)
                details.Add($"assumed: {(assumptions.Length > 80 ? assumptions[..80] + "..." : assumptions)}");

            var detailStr = details.Count > 0 ? $" ({string.Join(", ", details)})" : "";

            // Append episode trajectory if the session had enough steps to segment
            // Research basis: SAMULE meso-level, AriadneMem transition history.
            var trajectory = EpisodeSegmenter.BuildTrajectoryLine(result.Steps);
            var trajectorySuffix = trajectory is not null ? $"\n  Trajectory: {trajectory}" : "";

            return $"- [{date}] fail: \"{taskPreview}\" — {result.FailureReason?.Split('\n').FirstOrDefault()}{detailStr}. Steps: {result.Steps.Count}, Tokens: {totalTokens:N0}{trajectorySuffix}";
        }

        if (totalTokens >= 300_000)
        {
            return $"- [{date}] costly: \"{taskPreview}\" — succeeded but used {totalTokens:N0} tokens in {result.Steps.Count} steps. Consider more targeted exploration.";
        }

        return null;
    }

    // ── Session finish ─────────────────────────────────────────────────────

    private async Task<AgentResult> Finish(
        bool success, string output, List<StepRecord> steps,
        int promptTokens, int completionTokens, Stopwatch sw, EventLog eventLog, string task,
        string? consolidationSummary = null, string? assumptionsText = null,
        IReadOnlyList<string>? pivotReasons = null)
    {
        var result = new AgentResult
        {
            Success = success,
            Output = output,
            Steps = steps,
            TotalPromptTokens = promptTokens,
            TotalCompletionTokens = completionTokens,
            TotalDurationMs = sw.Elapsed.TotalMilliseconds,
            SessionLogPath = eventLog.FilePath,
            FailureReason = success ? null : output,
        };
        await eventLog.RecordSessionEndAsync(result);

        // Read the agent's todo plan state only if manage_todos was used THIS session.
        // Prevents stale todos from a previous session leaking into the handoff.
        var usedTodos = steps.Any(s => s.ToolCalls.Any(tc =>
            tc.ToolName is "manage_todos" && !tc.IsError));
        var todoPlanState = usedTodos ? LoadTodoPlanState() : null;

        // Generate and record session handoff note (for resume capability)
        var handoff = HandoffGenerator.Generate(task, result, _options.MaxSteps, consolidationSummary, todoPlanState, assumptionsText, pivotReasons);
        await eventLog.RecordHandoffAsync(handoff);
        _logger.Debug("Session handoff note recorded (status: {Status})", handoff.Status);

        // Cross-session learning: save lesson on failure or high cost
        SaveLesson(task, result);

        return result;
    }
}

/// <summary>
/// Categorization of agent step failures for targeted recovery.
/// Research basis: DeepVerifier (2026) and DEFT (2025) showed that categorizing
/// failures enables targeted recovery strategies vs generic "try again" nudges.
/// </summary>
public enum FailureType
{
    Unknown,
    /// <summary>Edit didn't match file content — file changed since last read.</summary>
    StaleContext,
    /// <summary>Edit introduced compile/syntax errors.</summary>
    SyntaxError,
    /// <summary>Tests are failing after an edit.</summary>
    TestFailure,
    /// <summary>Errored tool call exceeded the 30 second timeout threshold.</summary>
    Timeout,
    /// <summary>Repeating the exact same failed tool call.</summary>
    DuplicateAttempt,
    /// <summary>Action was blocked by guardrails.</summary>
    Blocked,
    /// <summary>Requested tool is not implemented.</summary>
    ToolMissing,
    /// <summary>Subagent delegation returned empty, errored, or timed out.</summary>
    DelegationFailure,
}
