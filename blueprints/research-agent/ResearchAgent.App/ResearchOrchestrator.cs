using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
#pragma warning disable MAAI001 // Microsoft.Agents.AI.Compaction is experimental in 1.1.0
using Microsoft.Agents.AI.Compaction;
#pragma warning restore MAAI001
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;

// Resolve ambiguity: ChatMessage exists in both OpenAI.Chat and Microsoft.Extensions.AI
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

using ResearchAgent.Core.Memory;
using ResearchAgent.Core.Models;
using ResearchAgent.Plugins.Content;
using ResearchAgent.Plugins.Search;
using ResearchAgent.Plugins.Synthesis;
using ResearchAgent.Plugins.Verification;

namespace ResearchAgent.App;

/// <summary>
/// The main research agent orchestrator.
///
/// Architecture follows insights from multiple papers:
/// - CASTER's Scientific Discovery workflow: Planner → Researcher → Analyst → Synthesizer
/// - Pensieve's self-context engineering: read → note → prune cycle
/// - Agentic RAG's retrieval patterns: dynamic multi-step retrieval
/// - HiMAC's hierarchical planning: decompose → execute → synthesize
///
/// V2 improvements (2025–2026 research wave):
/// - RE-Searcher: Goal-oriented search with explicit reflection
/// - Reflection-Driven Control (AAAI 2026): Reflective memory repository
/// - FINDER/DeepVerifier: Checklist-based report verification
/// - CoRefine-inspired: Adaptive iteration based on gap analysis
///
/// Pipeline: Planner → [Researcher ↔ Analyst]×N → Synthesizer → Verifier
/// </summary>
public sealed class ResearchOrchestrator : IDisposable
{
    private readonly IConfiguration _config;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<ResearchOrchestrator> _logger;
    private readonly ResearchMemory _memory;
    private readonly ResearchStateFile? _priorState;
    private readonly IProgress<ResearchProgressEvent>? _progress;
    private readonly IWebSearchProvider _searchProvider;
    private readonly HttpClient? _ownedHttpClient;
    private readonly SearchStatistics _searchStats;

    public ResearchOrchestrator(
        IConfiguration config,
        ILoggerFactory loggerFactory,
        ResearchStateFile? priorState = null,
        IProgress<ResearchProgressEvent>? progress = null)
    {
        _config = config;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<ResearchOrchestrator>();
        _memory = new ResearchMemory();
        _priorState = priorState;
        _progress = progress;

        (_searchProvider, _ownedHttpClient) = CreateSearchProvider(config, loggerFactory);
        _searchStats = new SearchStatistics(_searchProvider.Name);
        _logger.LogInformation("Web search provider: {Provider}", _searchProvider.Name);

        if (priorState is not null)
        {
            _memory.LoadPriorState(priorState);
            _logger.LogInformation("Loaded prior state from session {PriorSessionId}: {FindingCount} findings, {SourceCount} sources",
                priorState.Metadata.SessionId, priorState.Findings.Count, priorState.Sources.Count);
        }
    }

    /// <summary>
    /// Select the search provider based on <c>Research:Search:Provider</c>:
    /// <c>Tavily</c> (requires <c>Research:Tavily:ApiKey</c>) or <c>Simulated</c> (default).
    /// If Tavily is requested but no key is configured, falls back to Simulated with a
    /// warning — this keeps dev environments functional without hiding the misconfig.
    /// </summary>
    private static (IWebSearchProvider provider, HttpClient? ownedHttp) CreateSearchProvider(
        IConfiguration config, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(typeof(ResearchOrchestrator));
        var requested = (config["Research:Search:Provider"] ?? "Simulated").Trim();

        if (string.Equals(requested, "Tavily", StringComparison.OrdinalIgnoreCase))
        {
            var key = config["Research:Tavily:ApiKey"];
            if (string.IsNullOrWhiteSpace(key))
            {
                logger.LogWarning("Research:Search:Provider=Tavily but Research:Tavily:ApiKey is empty — falling back to Simulated.");
                return (new SimulatedWebSearchProvider(), null);
            }
            var depth = config["Research:Tavily:SearchDepth"] ?? "basic";
            // `advanced` searches occasionally take 30+s on Tavily; give them more runway.
            var timeout = string.Equals(depth, "advanced", StringComparison.OrdinalIgnoreCase)
                ? TimeSpan.FromSeconds(60)
                : TimeSpan.FromSeconds(30);
            var http = new HttpClient { Timeout = timeout };
            var provider = new TavilyWebSearchProvider(http, key!,
                loggerFactory.CreateLogger<TavilyWebSearchProvider>(), depth);
            return (provider, http);
        }

        if (!string.Equals(requested, "Simulated", StringComparison.OrdinalIgnoreCase))
            logger.LogWarning("Unknown Research:Search:Provider=\"{Requested}\" — falling back to Simulated.", requested);

        return (new SimulatedWebSearchProvider(), null);
    }

    /// <summary>
    /// Execute a full research workflow for the given query.
    ///
    /// V2 flow:
    ///   1. Planner decomposes query
    ///   2. Researcher ↔ Analyst loop (up to MaxIterations, exits early if no critical gaps)
    ///   3. Synthesizer writes the report
    ///   4. Verifier checks report claims against findings (FINDER checklist pattern)
    /// </summary>
    public async Task<ResearchResult> ResearchAsync(string query, CancellationToken ct = default)
    {
        using var sessionActivity = Diagnostics.Source.StartActivity("ResearchSession");
        var sessionStopwatch = Stopwatch.StartNew();

        var state = new ResearchState { Query = query };
        var history = new List<ChatMessage>();
        var interactions = new List<AgentInteraction>();
        var startedAt = DateTimeOffset.UtcNow;

        var provider = _config["AI:Provider"] ?? "openai";
        var model = _config["AI:Model"] ?? "gpt-4o";
        var maxIterations = _config.GetValue("Research:MaxIterations", 2);
        var verificationEnabled = _config.GetValue("Research:VerificationEnabled", true);

        sessionActivity?.SetTag("session.id", state.SessionId);
        sessionActivity?.SetTag("session.query", query);
        sessionActivity?.SetTag("ai.provider", provider);
        sessionActivity?.SetTag("ai.model", model);
        sessionActivity?.SetTag("research.max_iterations", maxIterations);
        sessionActivity?.SetTag("research.verification_enabled", verificationEnabled);

        _logger.LogInformation("Session {SessionId} | query={Query} | provider={Provider} | model={Model} | maxIterations={MaxIter} | verification={Verification}",
            state.SessionId, query, provider, model, maxIterations, verificationEnabled ? "on" : "off");

        ReportProgress(ResearchProgressKind.SessionInfo, $"Session {state.SessionId} | {provider}/{model} | maxIter={maxIterations}");
        if (_priorState is not null)
            ReportProgress(ResearchProgressKind.SessionInfo, $"Continuing from prior session {_priorState.Metadata.SessionId} ({_priorState.Findings.Count} findings loaded)");

        // ── Phase 1: Create AI client ──────────────────────
        ChatClient chatClient;
        using (var clientActivity = Diagnostics.Source.StartActivity("CreateChatClient"))
        {
            var clientSw = Stopwatch.StartNew();
            chatClient = CreateChatClient();
            clientSw.Stop();
            _logger.LogDebug("ChatClient created in {ElapsedMs}ms", clientSw.ElapsedMilliseconds);
            clientActivity?.SetTag("duration_ms", clientSw.ElapsedMilliseconds);
        }

        // ── Phase 2: Create agents ─────────────────────────
        AIAgent plannerAgent, researcherAgent, analystAgent, synthesizerAgent;
        AIAgent? verifierAgent = null;
        using (var agentsActivity = Diagnostics.Source.StartActivity("CreateAgents"))
        {
            var agentsSw = Stopwatch.StartNew();

            plannerAgent = CreatePlannerAgent(chatClient);
            researcherAgent = CreateResearcherAgent(chatClient);
            analystAgent = CreateAnalystAgent(chatClient);
            synthesizerAgent = CreateSynthesizerAgent(chatClient);
            if (verificationEnabled)
                verifierAgent = CreateVerifierAgent(chatClient);

            var count = verificationEnabled ? 5 : 4;
            agentsSw.Stop();
            _logger.LogDebug("{AgentCount} agents created in {ElapsedMs}ms", count, agentsSw.ElapsedMilliseconds);
            agentsActivity?.SetTag("agent_count", count);
            agentsActivity?.SetTag("duration_ms", agentsSw.ElapsedMilliseconds);
        }

        string? finalReport = null;

        // ── Phase 3: Run Planner ───────────────────────────
        state.CurrentPhase = ResearchPhase.Planning;
        _memory.SetResearchQuestion(query);   // P2.3 — make question available to downstream context provider
        var taskPrompt = _priorState is not null
            ? BuildTaskPromptWithPrior(query, _priorState)
            : BuildTaskPrompt(query);
        _logger.LogInformation("Phase 3: Running Planner...");
        ReportProgress(ResearchProgressKind.PhaseChange, _priorState is not null ? "Planning (with prior context)..." : "Planning...");

        var plannerOutput = await RunSingleAgentAsync(
            plannerAgent, "Planner", taskPrompt, state.SessionId, interactions, history, ct);
        _memory.SetPlannerOutput(plannerOutput);   // P2.3 — surface to downstream agents via context provider

        // ── Phase 4: Iterative Research ↔ Analyst Loop ─────
        // Inspired by SFR-DeepResearch and RE-Searcher: top agents iterate,
        // with the Analyst identifying gaps that send the Researcher back.
        for (int iteration = 1; iteration <= maxIterations; iteration++)
        {
            state.IterationCount = iteration;

            // Clear knowledge gaps from previous iteration so only freshly-identified
            // gaps drive re-research (not stale gaps that may have been addressed).
            if (iteration > 1)
                _memory.ClearKnowledgeGaps();

            // ── 4a: Researcher ──────────────────────────────
            state.CurrentPhase = ResearchPhase.Searching;
            _logger.LogInformation("Phase 4a: Running Researcher (iteration {Iteration}/{MaxIter})...",
                iteration, maxIterations);
            ReportProgress(ResearchProgressKind.Iteration, $"Research iteration {iteration}/{maxIterations}", iteration);
            ReportProgress(ResearchProgressKind.PhaseChange, $"Researching (iteration {iteration})...");

            var researcherInput = iteration == 1
                ? $"""
                    ## Research Plan (from Planner)

                    {plannerOutput}

                    ---
                    Please execute this research plan. For each sub-question, follow the goal-reflect pattern:
                    1. State your search GOAL for this sub-question
                    2. Execute the search
                    3. REFLECT: Did results meet your goal? If not, refine the query and retry (max 2 retries)
                    4. Record findings and move to the next sub-question
                    """
                : $"""
                    ## Research Plan (from Planner)

                    {plannerOutput}

                    ## Gap Analysis from Analyst (iteration {iteration})

                    The Analyst identified knowledge gaps that need additional research.
                    Focus your searches on filling these specific gaps.
                    Use GetReflections to review what approaches have already been tried — avoid repeating them.
                    Use CheckResearchProgress to see which sub-questions need more work.

                    ## Previous Analysis

                    {interactions.LastOrDefault(i => i.Agent == "Analyst")?.Text ?? "(no previous analysis)"}
                    """;

            var researcherOutput = await RunSingleAgentAsync(
                researcherAgent, "Researcher", researcherInput, state.SessionId, interactions, history, ct);

            // ── 4b: Analyst ─────────────────────────────────
            state.CurrentPhase = ResearchPhase.Analyzing;
            _logger.LogInformation("Phase 4b: Running Analyst (iteration {Iteration}/{MaxIter})...",
                iteration, maxIterations);
            ReportProgress(ResearchProgressKind.PhaseChange, $"Analyzing (iteration {iteration})...");
            ReportProgress(ResearchProgressKind.FindingDiscovered, $"{_memory.GetAllFindings().Count} findings accumulated", _memory.GetAllFindings().Count);

            var analystInput = $"""
                ## Research Findings (iteration {iteration})

                {researcherOutput}

                ---
                Analyze the accumulated research findings. Use GetAllResearchContext for the full picture.

                **Critical task**: After your analysis, use the RecordKnowledgeGap tool to record any
                specific knowledge gaps that need additional research. Only record gaps that are:
                1. Critical to answering the original research question
                2. Potentially fillable with additional targeted searches
                3. Not already covered by existing findings

                If all sub-questions are adequately answered, state "NO CRITICAL GAPS REMAINING" clearly.
                """;

            var analystOutput = await RunSingleAgentAsync(
                analystAgent, "Analyst", analystInput, state.SessionId, interactions, history, ct);

            // ── 4c: Check if more iteration is needed ───────
            var underResearched = _memory.GetUnderResearchedQuestions();
            if (underResearched.Count == 0 || iteration == maxIterations)
            {
                if (underResearched.Count == 0)
                    _logger.LogInformation("No critical gaps remaining — exiting research loop after {Iteration} iteration(s)", iteration);
                else
                    _logger.LogInformation("Max iterations ({MaxIter}) reached — {GapCount} gaps remain, proceeding to synthesis",
                        maxIterations, underResearched.Count);
                break;
            }

            _logger.LogInformation("Analyst identified {GapCount} under-researched sub-questions — iterating...",
                underResearched.Count);
            ReportProgress(ResearchProgressKind.GapIdentified, $"{underResearched.Count} knowledge gap(s) — iterating", underResearched.Count);
        }

        // ── Phase 4.5: Evidence-Sufficiency Gate (P2.4) ─────
        // Decide whether the accumulated evidence justifies a synthesis pass. The
        // gate is pure-functional over memory; it logs a verdict regardless of
        // mode and only blocks synthesis when Mode=Enforce + decision=Refuse.
        var gateOptions = EvidenceGateOptions.FromConfig(_config);
        var gateLogger = _loggerFactory.CreateLogger<EvidenceSufficiencyEvaluator>();
        var evidenceGate = new EvidenceSufficiencyEvaluator(gateOptions, gateLogger);
        var evidenceVerdict = gateOptions.Mode == EvidenceGateMode.Off
            ? null
            : evidenceGate.Evaluate(
                _memory.GetAllFindings(),
                _memory.GetAllSources(),
                _memory.GetAllProgress());

        if (evidenceVerdict is not null)
        {
            sessionActivity?.SetTag("evidence_gate.mode", evidenceVerdict.Mode.ToString());
            sessionActivity?.SetTag("evidence_gate.decision", evidenceVerdict.Decision.ToString());
            sessionActivity?.SetTag("evidence_gate.failing_sub_questions", evidenceVerdict.FailingSubQuestions.Count);
            ReportProgress(
                ResearchProgressKind.SessionInfo,
                $"Evidence gate: {evidenceVerdict.Decision} (mode={evidenceVerdict.Mode}, failingQ={evidenceVerdict.FailingSubQuestions.Count})");
        }

        if (evidenceVerdict?.ShouldRefuseSynthesis == true)
        {
            _logger.LogWarning("Evidence gate REFUSED synthesis (mode=Enforce). Emitting diagnostic report instead.");
            foreach (var reason in evidenceVerdict.Reasons)
                _logger.LogWarning("Evidence gate reason: {Reason}", reason);

            finalReport = evidenceVerdict.RenderDiagnostic(query);
            state.FinalReport = finalReport;
            ReportProgress(
                ResearchProgressKind.PhaseChange,
                $"Evidence gate refused synthesis — emitting diagnostic ({evidenceVerdict.Reasons.Count} reason(s))");
        }

        // ── Phase 5: Synthesizer ───────────────────────────
        // Skipped when the evidence gate refused — `finalReport` already holds the diagnostic.
        var synthesisRefused = evidenceVerdict?.ShouldRefuseSynthesis == true;
        if (!synthesisRefused)
        {
            state.CurrentPhase = ResearchPhase.Synthesizing;
            _logger.LogInformation("Phase 5: Running Synthesizer...");
            ReportProgress(ResearchProgressKind.PhaseChange, $"Synthesizing report ({_memory.GetAllFindings().Count} findings, {_memory.GetAllSources().Count} sources)...");

            var warnBanner = evidenceVerdict?.ShouldAnnotateSynthesis == true
                ? $"""
                   ⚠️ EVIDENCE-GATE WARNING — DO NOT IGNORE:
                   The evidence-sufficiency gate flagged this research session. Reasons:
                   {string.Join("\n", evidenceVerdict.Reasons.Select(r => "  - " + r))}

                   You MUST either (a) explicitly acknowledge these limitations in the report's
                   "Limitations" section with the same specificity shown above, or (b) refuse to
                   synthesize and state so directly. Do NOT paper over the gaps with hedged language.

                   ---
                   """
                : string.Empty;

            var synthesizerInput = $"""
                {warnBanner}## Analysis Summary

                {interactions.LastOrDefault(i => i.Agent == "Analyst")?.Text ?? "(no analysis)"}

                ---
                Use GetAllResearchContext to access all findings and notes.
                Produce the final comprehensive research report.
                Total research iterations completed: {state.IterationCount}
                """;

            var synthesizerOutput = await RunSingleAgentAsync(
                synthesizerAgent, "Synthesizer", synthesizerInput, state.SessionId, interactions, history, ct);
            finalReport = synthesizerOutput;
        }
        else
        {
            _logger.LogInformation("Phase 5: Synthesizer SKIPPED (evidence gate refused; diagnostic report substituted)");
        }

        // ── Phase 6: Verifier (optional) ─────────────────────────
        // Also skipped on refusal — there are no claims to verify against synthetic evidence.
        if (!synthesisRefused && verificationEnabled && verifierAgent is not null)
        {
            state.CurrentPhase = ResearchPhase.Verifying;
            _logger.LogInformation("Phase 6: Running Verifier...");
            ReportProgress(ResearchProgressKind.PhaseChange, "Verifying claims...");

            var verifierInput = $"""
                ## Report to Verify

                {finalReport}

                ---
                Verify this research report against the accumulated evidence.
                Use GetVerificationContext to access all findings and sources.

                For each major factual claim or conclusion in the report:
                1. Use RecordClaimVerification to record whether it is SUPPORTED, UNSUPPORTED, CONTRADICTED, or UNVERIFIABLE
                2. Provide specific evidence (the finding ID or source that supports/contradicts)
                3. If the claim fails, categorize the failure type (Factual, Reasoning, Completeness, Coherence, Attribution)

                After checking all claims, use GetVerificationSummary to produce the final verification report.
                """;

            var verifierOutput = await RunSingleAgentAsync(
                verifierAgent, "Verifier", verifierInput, state.SessionId, interactions, history, ct);
        }
        else if (synthesisRefused)
        {
            _logger.LogInformation("Phase 6: Verification SKIPPED (evidence gate refused — nothing to verify)");
        }
        else if (!verificationEnabled)
        {
            _logger.LogInformation("Phase 6: Verification SKIPPED (disabled via config)");
        }

        // ── Phase 7: Build result ──────────────────────────
        state.FinalReport = finalReport;
        state.CurrentPhase = ResearchPhase.Complete;

        sessionStopwatch.Stop();
        var totalDuration = sessionStopwatch.Elapsed;

        var verificationResult = _memory.GetVerificationResult();

        _logger.LogInformation("Session {SessionId} complete in {TotalSec:F1}s — findings={FindingCount} sources={SourceCount} interactions={InteractionCount} iterations={Iterations} verificationPassRate={PassRate:P0}",
            state.SessionId, totalDuration.TotalSeconds,
            _memory.GetAllFindings().Count, _memory.GetAllSources().Count,
            interactions.Count, state.IterationCount, verificationResult.PassRate);

        sessionActivity?.SetTag("duration_ms", totalDuration.TotalMilliseconds);
        sessionActivity?.SetTag("finding_count", _memory.GetAllFindings().Count);
        sessionActivity?.SetTag("source_count", _memory.GetAllSources().Count);
        sessionActivity?.SetTag("iteration_count", state.IterationCount);
        sessionActivity?.SetTag("verification_pass_rate", verificationResult.PassRate);
        sessionActivity?.SetTag("has_report", finalReport is not null);
        sessionActivity?.SetStatus(finalReport is not null ? ActivityStatusCode.Ok : ActivityStatusCode.Error);

        return new ResearchResult
        {
            SessionId = state.SessionId,
            Query = query,
            Report = finalReport,
            Findings = _memory.GetAllFindings(),
            Sources = _memory.GetAllSources(),
            AgentHistory = history,
            AgentInteractions = interactions,
            ContextLog = _memory.GetContextLog(),
            VerificationResult = verificationEnabled ? verificationResult : null,
            IterationCount = state.IterationCount,
            ReflectionCount = _memory.GetReflections().Count,
            Reflections = _memory.GetReflections(),
            PlannerOutput = plannerOutput,
            SubQuestionProgress = _memory.GetAllProgress(),
            PriorSessionId = _priorState?.Metadata.SessionId,
            StartedAt = startedAt,
            CompletedAt = DateTimeOffset.UtcNow,
            Provider = provider,
            Model = model,
            SearchProvider = _searchStats.ProviderName,
            WebSearchCallCount = _searchStats.CallCount,
            WebSearchResultCount = _searchStats.ResultCount,
            WebSearchTotalLatencyMs = _searchStats.TotalLatencyMs,
            EvidenceVerdict = evidenceVerdict,
            SynthesisRefused = synthesisRefused,
        };
    }

    // ──────────────────────────────────────────────────────────
    // Single-Agent Execution Helper
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Run a single agent as a one-agent sequential workflow and return its text output.
    /// This replaces the monolithic BuildSequential pipeline, enabling iterative control flow.
    /// </summary>
    private async Task<string> RunSingleAgentAsync(
        AIAgent agent,
        string agentName,
        string inputText,
        string sessionId,
        List<AgentInteraction> interactions,
        List<ChatMessage> history,
        CancellationToken ct)
    {
        using var activity = Diagnostics.Source.StartActivity($"RunAgent.{agentName}");
        var sw = Stopwatch.StartNew();

        var workflow = AgentWorkflowBuilder.BuildSequential([agent]);
        var input = new ChatMessage(ChatRole.User, inputText);

        // Use a unique sub-session ID per invocation to prevent MAF from
        // accumulating conversation state across separate agent runs.
        var subSession = $"{sessionId}_{agentName}_{Guid.NewGuid().ToString("N")[..8]}";

        _logger.LogDebug("Running agent {AgentName} — input {InputChars} chars (~{TokenEstimate} tokens)",
            agentName, inputText.Length, inputText.Length / 4);

        try
        {
            var run = await InProcessExecution.RunAsync(workflow, input, subSession, ct);
            var events = run.NewEvents.ToList();

            sw.Stop();
            _logger.LogInformation("Agent {AgentName} completed in {ElapsedMs}ms ({ElapsedSec:F1}s) — {EventCount} events",
                agentName, sw.ElapsedMilliseconds, sw.Elapsed.TotalSeconds, events.Count);

            activity?.SetTag("event_count", events.Count);
            activity?.SetTag("duration_ms", sw.ElapsedMilliseconds);

            // Extract text output from events
            var responseText = ExtractAgentOutput(events, agentName, out var workflowError);
            string interactionStatus = "ok";
            string? interactionError = null;

            if (string.IsNullOrEmpty(responseText))
            {
                if (workflowError is not null)
                {
                    _logger.LogWarning("Agent {AgentName} produced EMPTY response due to workflow error: {Error}", agentName, workflowError);
                    interactionStatus = "failed";
                    interactionError = workflowError;
                    responseText = $"(Agent {agentName} failed: {workflowError})";
                }
                else
                {
                    _logger.LogWarning("Agent {AgentName} produced EMPTY response — check model name, API key, and quota. Run with Logging:MinLevel=Debug for the underlying LLM error.", agentName);
                    interactionStatus = "empty";
                    responseText = $"(Agent {agentName} produced no output)";
                }
                activity?.SetStatus(ActivityStatusCode.Error, interactionStatus);
            }
            else
            {
                _logger.LogInformation("■ Agent {AgentName}: {CharCount} chars (~{TokenEstimate} tokens)",
                    agentName, responseText.Length, responseText.Length / 4);
                activity?.SetStatus(ActivityStatusCode.Ok);
            }

            // Record in history
            var now = DateTimeOffset.UtcNow;
            history.Add(new ChatMessage(ChatRole.Assistant, responseText) { AuthorName = agentName });
            interactions.Add(new AgentInteraction
            {
                Agent = agentName,
                Role = "assistant",
                Text = responseText,
                Timestamp = now,
                Status = interactionStatus,
                ErrorText = interactionError,
            });

            activity?.SetTag("output_chars", responseText.Length);
            return responseText;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Agent {AgentName} FAILED after {ElapsedMs}ms: {Error}",
                agentName, sw.ElapsedMilliseconds, ex.Message);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            // Record the failure in the session log before rethrowing — so a
            // post-mortem doesn't have to scrape the Serilog stream.
            interactions.Add(new AgentInteraction
            {
                Agent = agentName,
                Role = "assistant",
                Text = $"(Agent {agentName} failed: {ex.GetType().Name})",
                Timestamp = DateTimeOffset.UtcNow,
                Status = "failed",
                ErrorText = ex.Message,
            });
            throw;
        }
    }

    /// <summary>
    /// Extract the accumulated text output from workflow events for a single agent.
    /// Returns the output text. When a <see cref="WorkflowErrorEvent"/> is seen, captures
    /// its exception (including inner exceptions — MAF commonly wraps the real error in a
    /// <c>TargetInvocationException</c>) into <paramref name="workflowError"/> so the
    /// caller can record it on the <c>AgentInteraction</c>.
    /// </summary>
    private string ExtractAgentOutput(List<WorkflowEvent> events, string agentName, out string? workflowError)
    {
        var sb = new StringBuilder();
        workflowError = null;

        foreach (var evt in events)
        {
            switch (evt)
            {
                case WorkflowErrorEvent errorEvt:
                    // Walk the InnerException chain — MAF's TurnToken handler wraps the
                    // underlying LLM error (HTTP 404, invalid model, auth failure) in one
                    // or more TargetInvocationException layers. The inner message is the
                    // one a human needs.
                    var (outerType, rootType, rootMsg) = FormatWorkflowException(errorEvt.Exception, errorEvt.ToString());
                    _logger.LogError("WORKFLOW ERROR in {AgentName}: {OuterType} → {RootType}: {RootMessage}",
                        agentName, outerType, rootType, rootMsg);
                    // First error wins — downstream ones are usually cascades.
                    workflowError ??= $"{rootType}: {rootMsg}";
                    break;

                case WorkflowOutputEvent outputEvt when outputEvt.ExecutorId != "OutputMessages":
                    if (outputEvt.Data is AgentResponseUpdate update && update.Text is not null)
                    {
                        sb.Append(update.Text);
                    }
                    break;
            }
        }

        return sb.ToString().Trim();
    }

    // ──────────────────────────────────────────────────────────
    // Agent Factories — each agent gets specialized instructions and tools
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Create an agent, optionally wrapped with a <see cref="CompactionProvider"/>
    /// built by <see cref="CompactionConfigurator"/> (P2.2).
    ///
    /// <para>Modes, via <c>AI:Compaction:Mode</c>: <c>Off</c> (default) · <c>ToolResultOnly</c>
    /// (P1.1 shape) · <c>Pipeline</c> (ToolResult → Summarization → SlidingWindow → Truncation).
    /// The legacy <c>AI:ToolResultCompaction:Enabled = true</c> promotes to <c>ToolResultOnly</c>
    /// for back-compat.</para>
    ///
    /// <para>Only applied to agents chosen by the caller (currently Researcher + Verifier) —
    /// these produce the largest tool-result payloads (web pages, evidence bundles) and the
    /// longest multi-turn conversations. Leaves Planner / Analyst / Synthesizer untouched
    /// because their contexts are already summary-shaped.</para>
    /// </summary>
    private AIAgent CreateAgentWithOptionalCompaction(
        ChatClient chatClient,
        string name,
        string description,
        string instructions,
        IList<AITool> tools,
        bool attachStateProvider = false)
    {
        var cfg = CompactionConfigurator.Resolve(_config);

        // P2.3 — opt-in research-state provider. Decoupled from compaction so either
        // feature can be enabled independently.
        var stateProviderEnabled = attachStateProvider &&
            _config.GetValue<bool>("AI:ResearchContextProvider:Enabled", false);
        AIContextProvider? stateProvider = stateProviderEnabled
            ? new ResearchStateContextProvider(_memory, _logger)
            : null;

        if (cfg.Mode == CompactionConfigurator.Mode.Off && stateProvider is null)
        {
            return chatClient.AsAIAgent(
                name: name,
                description: description,
                instructions: instructions,
                tools: tools);
        }

#pragma warning disable MAAI001 // Microsoft.Agents.AI.Compaction is experimental in 1.1.0
        var providers = new List<AIContextProvider>();

        if (cfg.Mode != CompactionConfigurator.Mode.Off)
        {
            var strategy = CompactionConfigurator.Build(
                cfg,
                summarizerClientFactory: () => CreateSummarizerChatClient(cfg.SummarizationModel),
                logger: _logger);

            if (strategy is not null)
            {
                providers.Add(new CompactionProvider(
                    strategy,
                    name + "-compaction",
                    _loggerFactory));
                _logger.LogInformation("Compaction ENABLED for {Agent}: {Shape}", name, cfg.Describe());
            }
        }

        if (stateProvider is not null)
        {
            providers.Add(stateProvider);
            _logger.LogInformation("Research-state context provider ENABLED for {Agent}", name);
        }

        if (providers.Count == 0)
        {
            return chatClient.AsAIAgent(
                name: name, description: description,
                instructions: instructions, tools: tools);
        }

        var options = new ChatClientAgentOptions
        {
            Name = name,
            Description = description,
            ChatOptions = new ChatOptions
            {
                Instructions = instructions,
                Tools = tools.ToList(),
            },
            AIContextProviders = providers,
        };
#pragma warning restore MAAI001

        return chatClient.AsAIAgent(options);
    }

    /// <summary>
    /// Builds the <see cref="IChatClient"/> used by the summarization stage of the compaction
    /// pipeline. When <paramref name="modelOverride"/> is null or empty, reuses the primary
    /// chat client; otherwise instantiates a dedicated client on the same provider+credential
    /// using the override model. The result is wrapped via <see cref="ChatClientExtensions.AsIChatClient"/>.
    /// </summary>
    private IChatClient? CreateSummarizerChatClient(string? modelOverride)
    {
        try
        {
            var provider = _config["AI:Provider"] ?? "openai";
            var apiKey = _config["AI:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("Compaction summarization requested but AI:ApiKey is not set — summarization stage will be skipped.");
                return null;
            }

            var model = string.IsNullOrWhiteSpace(modelOverride)
                ? (_config["AI:Model"] ?? "gpt-4o")
                : modelOverride!;

            ChatClient inner = provider.ToLowerInvariant() switch
            {
                "openai" => new OpenAIClient(apiKey).GetChatClient(model),
                "azure" => CreateAzureChatClient(model, apiKey),
                _ => throw new InvalidOperationException($"Unknown AI provider: {provider}"),
            };

            _logger.LogDebug("Summarizer IChatClient ready (model={Model}, override={HasOverride})",
                model, !string.IsNullOrWhiteSpace(modelOverride));
            return inner.AsIChatClient();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build summarizer chat client — summarization stage will be skipped.");
            return null;
        }
    }

    /// <summary>
    /// Creates the Planning Agent — decomposes the research query into sub-questions.
    /// Inspired by HiMAC's hierarchical task decomposition.
    /// </summary>
    private AIAgent CreatePlannerAgent(ChatClient chatClient)
    {
        return chatClient.AsAIAgent(
            name: "Planner",
            description: "Decomposes research queries into structured sub-questions and creates a research plan.",
            instructions: """
                You are a Research Planner. Your job is to take a research query and decompose it
                into a structured research plan with specific, answerable sub-questions.

                ## Your Process:
                1. Analyze the query to understand its scope and intent
                2. Identify 3-7 key sub-questions that, when answered, will comprehensively address the query
                3. Order sub-questions by dependency (answer foundational ones first)
                4. For each sub-question, suggest what types of sources would be most valuable
                5. Assign each sub-question a unique ID (e.g., SQ1, SQ2, SQ3...)

                ## Output Format:
                Produce a structured research plan with:
                - A brief restatement of the research goal
                - Numbered sub-questions with IDs (SQ1, SQ2...) and priority levels (1=highest)
                - For each sub-question: suggested search strategies
                - Any known constraints or scope limitations

                Be specific and actionable. Each sub-question should be independently searchable.
                Avoid overly broad questions — break them down further if needed.
                """);
    }

    /// <summary>
    /// Creates the Researcher Agent — executes searches and extracts information.
    /// V2: Adds goal-reflect pattern (RE-Searcher) and reflective memory tools.
    /// </summary>
    private AIAgent CreateResearcherAgent(ChatClient chatClient)
    {
        var tools = new List<AITool>();
        tools.AddRange(CreateToolsFrom(new WebSearchPlugin(_searchProvider, _memory, _loggerFactory, _searchStats)));
        tools.AddRange(CreateToolsFrom(new ContentExtractionPlugin(_memory, _loggerFactory)));
        tools.AddRange(CreateToolsFrom(new NoteTakingPlugin(_memory, _loggerFactory)));

        _logger.LogDebug("Researcher agent tools: {ToolNames}",
            string.Join(", ", tools.Select(t => t.GetType().Name)));

        return CreateAgentWithOptionalCompaction(
            chatClient,
            name: "Researcher",
            description: "Searches for information, reads sources, and extracts key findings using goal-reflect pattern and Pensieve memory.",
            instructions: """
                You are a Research Investigator. You have access to web search, content extraction,
                note-taking, and reflection tools. Your job is to find and gather information for each
                sub-question in the research plan.

                ## Your Process — Goal-Reflect Pattern (from RE-Searcher):
                For each sub-question in the plan:

                1. **Goal**: State explicitly what specific information you need to find
                2. **Search**: Use web search or academic search to find relevant sources
                3. **Reflect**: Did the results address your goal?
                   - YES → Record findings using RecordFinding, move to next sub-question
                   - NO → Use RecordReflection to log what went wrong and why, then reformulate
                          and retry (max 2 retries per sub-question)
                4. **Read**: Fetch content from the most promising sources
                5. **Note**: Record key findings — distill the essential facts with confidence scores
                6. **Progress**: Use CheckResearchProgress to monitor your coverage

                ## Reflection Guidelines:
                - Before starting research on a sub-question, use GetReflections to check if
                  previous iterations tried approaches that didn't work — DON'T repeat them
                - When a search returns irrelevant results, use RecordReflection to note the
                  failed query and your analysis of why it failed
                - Record the query you plan to try next as the revisedAction

                ## Research Guidelines:
                - Prioritize high-quality, authoritative sources (academic papers, official docs)
                - Record findings with accurate confidence scores (not always 0.7!)
                - Note contradictions between sources — they're valuable for analysis
                - Aim for at least 2-3 findings per sub-question
                - Always attribute findings to their source IDs
                - Use UpdateWorkingNote to maintain running synthesis per sub-question

                ## Important:
                After gathering findings, use GetAllResearchContext to compile everything,
                then pass the complete research context forward.
                Do NOT pass raw fetched content — only distilled findings and notes.
                """,
            tools: tools,
            attachStateProvider: true);
    }

    /// <summary>
    /// Creates the Analyst Agent — evaluates findings, identifies patterns and tensions.
    /// V2: Uses RecordKnowledgeGap to drive iterative research loop.
    /// </summary>
    private AIAgent CreateAnalystAgent(ChatClient chatClient)
    {
        var tools = new List<AITool>();
        tools.AddRange(CreateToolsFrom(new NoteTakingPlugin(_memory, _loggerFactory)));

        return chatClient.AsAIAgent(
            name: "Analyst",
            description: "Analyzes research findings, evaluates quality, identifies patterns, contradictions, and knowledge gaps that may trigger additional research.",
            instructions: """
                You are a Research Analyst. You receive gathered research findings and your job
                is to critically evaluate them and produce an analytical assessment.

                ## Your Process:
                1. Use GetAllResearchContext to review all accumulated findings, notes, and reflections
                2. Use CheckResearchProgress to see per-sub-question coverage
                3. Evaluate source reliability and finding confidence
                4. Identify patterns, themes, and consensus across findings
                5. Flag contradictions, tensions, and areas of disagreement
                6. **CRITICAL**: Use RecordKnowledgeGap for each sub-question with inadequate coverage
                7. Assess overall confidence in the research conclusions

                ## Knowledge Gap Assessment (drives iteration):
                For each sub-question, evaluate:
                - Are there at least 2 findings with confidence > 0.5?
                - Are key aspects of the question answered?
                - Are there contradictions that need resolution?

                If a gap is critical to answering the research question, use RecordKnowledgeGap
                to flag it. This may trigger additional research iteration.

                If no critical gaps remain, state "NO CRITICAL GAPS REMAINING" clearly.

                ## Output Format:
                Produce a structured analysis including:
                - Key themes and patterns discovered
                - Areas of consensus and disagreement
                - Source quality assessment
                - Knowledge gaps and limitations (reference any recorded gaps)
                - Confidence assessment per sub-question
                - Recommended narrative structure for the final report

                Be intellectually honest — flag uncertainty rather than overstating confidence.
                """,
            tools: tools);
    }

    /// <summary>
    /// Creates the Synthesizer Agent — produces the final research report.
    /// </summary>
    private AIAgent CreateSynthesizerAgent(ChatClient chatClient)
    {
        var tools = new List<AITool>();
        tools.AddRange(CreateToolsFrom(new NoteTakingPlugin(_memory, _loggerFactory)));
        tools.AddRange(CreateToolsFrom(new ReportFormattingPlugin(_loggerFactory)));

        return chatClient.AsAIAgent(
            name: "Synthesizer",
            description: "Produces the final comprehensive research report from analyzed findings.",
            instructions: """
                You are a Research Report Writer. You receive the analysis of research findings
                and your job is to produce a comprehensive, well-structured final report.

                ## Your Process:
                1. Use GetAllResearchContext to access all findings and notes
                2. Follow the narrative structure recommended by the Analyst
                3. Synthesize findings into a coherent narrative
                4. Include proper citations to sources
                5. Highlight key insights, tensions, and open questions

                ## Report Structure:
                - **Executive Summary**: 2-3 paragraph overview of key findings
                - **Background**: Context and scope of the research question
                - **Findings**: Organized by theme or sub-question (use the research plan structure)
                - **Analysis**: Patterns, tensions, and implications
                - **Limitations**: Knowledge gaps and confidence caveats
                - **Conclusion**: Key takeaways and potential next steps
                - **Sources**: List of all sources consulted

                ## Writing Guidelines:
                - Be precise and evidence-based — cite sources for claims
                - Every factual claim should be traceable to a specific finding and source
                - Acknowledge uncertainty and competing viewpoints
                - Use clear, professional language
                - Keep the report focused and actionable
                """,
            tools: tools);
    }

    /// <summary>
    /// Creates the Verifier Agent — checks report claims against findings.
    /// New in V2. Inspired by FINDER (checklist methodology) and DeepVerifier (rubric-guided).
    /// Implements the Asymmetry Thesis: verification is cheaper than generation.
    /// </summary>
    private AIAgent CreateVerifierAgent(ChatClient chatClient)
    {
        var tools = new List<AITool>();
        tools.AddRange(CreateToolsFrom(new VerificationPlugin(_memory, _loggerFactory)));

        return CreateAgentWithOptionalCompaction(
            chatClient,
            name: "Verifier",
            description: "Verifies research report claims against accumulated evidence using checklist-based verification.",
            instructions: """
                You are a Research Report Verifier. Your job is to check every major claim
                in the report against the accumulated evidence base.

                ## Your Process:
                1. Use GetVerificationContext to load all findings and sources
                2. Read through the report and identify every factual claim or conclusion
                3. For each claim, use RecordClaimVerification with:
                   - The specific claim text
                   - Verdict: SUPPORTED, UNSUPPORTED, CONTRADICTED, or UNVERIFIABLE
                   - Evidence: the specific finding/source that supports or contradicts
                   - Failure category (if not SUPPORTED): Factual, Reasoning, Completeness, Coherence, or Attribution
                4. After all claims are checked, use GetVerificationSummary for the final report

                ## DEFT Failure Categories (from FINDER taxonomy):
                - **Factual**: Incorrect claims, fabricated information, outdated data
                - **Reasoning**: Logical fallacies, overgeneralization, false equivalences
                - **Completeness**: Missing key arguments, insufficient evidence, scope gaps
                - **Coherence**: Internal contradictions, structural disconnects
                - **Attribution**: Missing citations, misattribution of claims

                ## Verification Guidelines:
                - Be thorough — check EVERY major factual claim, not just a sample
                - A claim is SUPPORTED only if a finding with reasonable confidence backs it
                - A claim is UNSUPPORTED if no finding addresses it (even if it might be true)
                - A claim is CONTRADICTED if a finding directly disagrees with it
                - Mark as UNVERIFIABLE only if the claim is about something outside the research scope
                - Be strict but fair — the goal is accuracy, not nitpicking
                """,
            tools: tools,
            attachStateProvider: true);
    }

    // ──────────────────────────────────────────────────────────
    // AI Client Creation
    // ──────────────────────────────────────────────────────────

    private ChatClient CreateChatClient()
    {
        var provider = _config["AI:Provider"] ?? "openai";
        var model = _config["AI:Model"] ?? "gpt-4o";
        var apiKey = _config["AI:ApiKey"] ?? throw new InvalidOperationException(
            "AI:ApiKey must be configured. Set it via user secrets, environment variable, or appsettings.json");

        _logger.LogDebug("ChatClient config — provider={Provider}, model={Model}, apiKeyLength={KeyLength}",
            provider, model, apiKey.Length);

        return provider.ToLowerInvariant() switch
        {
            "openai" => new OpenAIClient(apiKey).GetChatClient(model),
            "azure" => CreateAzureChatClient(model, apiKey),
            _ => throw new InvalidOperationException($"Unknown AI provider: {provider}. Supported: openai, azure")
        };
    }

    private ChatClient CreateAzureChatClient(string model, string apiKey)
    {
        var endpoint = _config["AI:Endpoint"] ?? throw new InvalidOperationException(
            "AI:Endpoint required for Azure OpenAI provider");

        _logger.LogDebug("Azure ChatClient — endpoint={Endpoint}, model={Model}", endpoint, model);

        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri(endpoint.TrimEnd('/') + "/openai/v1")
        };
        return new OpenAIClient(
            new System.ClientModel.ApiKeyCredential(apiKey), options)
            .GetChatClient(model);
    }

    // ──────────────────────────────────────────────────────────
    // Tool Discovery — wraps plugin methods as AIFunction tools
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Discovers all public methods with [Description] attributes on a plugin instance
    /// and wraps them as AIFunction tools via AIFunctionFactory.
    /// </summary>
    private IEnumerable<AITool> CreateToolsFrom(object plugin)
    {
        var methods = plugin.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<DescriptionAttribute>() is not null)
            .ToList();

        _logger.LogDebug("Plugin {PluginType}: found {MethodCount} tool methods: {Methods}",
            plugin.GetType().Name, methods.Count,
            string.Join(", ", methods.Select(m => m.Name)));

        return methods.Select(m => AIFunctionFactory.Create(m, plugin));
    }

    private static string BuildTaskPrompt(string query)
    {
        return $"""
            # Research Task

            **Query**: {query}

            Please decompose this query into a structured research plan with specific,
            answerable sub-questions. Assign each sub-question a unique ID (SQ1, SQ2, SQ3...).
            """;
    }

    private static string BuildTaskPromptWithPrior(string query, ResearchStateFile priorState)
    {
        var priorFindings = string.Join("\n", priorState.Findings.Select(f =>
            $"- [{f.Confidence:P0}] {Truncate(f.Content, 120)} (sub-question: {f.SubQuestionId})"));

        var completedQuestions = priorState.Plan.CompletedQuestionIds.Count > 0
            ? string.Join(", ", priorState.Plan.CompletedQuestionIds)
            : "(none)";

        return $"""
            # Research Task (Continuation)

            **Query**: {query}

            ## Prior Research Context

            This is a follow-up research session. A previous session researched a related query:
            **Prior query**: {priorState.Metadata.Query}
            **Prior session**: {priorState.Metadata.SessionId} ({priorState.Metadata.CreatedAt:yyyy-MM-dd})

            ### Prior Findings ({priorState.Findings.Count} total)
            {priorFindings}

            ### Prior Plan Coverage
            Completed sub-questions: {completedQuestions}

            ## Instructions

            Build a **delta plan** — focus on what's NEW or needs DEEPENING compared to the prior research.
            - Skip sub-questions already well-answered in the prior findings
            - Add new sub-questions specific to the new query
            - Flag prior findings that should be re-verified or updated
            - Assign each sub-question a unique ID (SQ1, SQ2, SQ3...)

            The prior findings are already loaded into memory and available via GetAllResearchContext.
            """;
    }

    private void ReportProgress(ResearchProgressKind kind, string message, int? numericValue = null)
    {
        _progress?.Report(new ResearchProgressEvent
        {
            Kind = kind,
            Message = message,
            NumericValue = numericValue
        });
    }

    private static string Truncate(string text, int maxLength)
    {
        return text.Length <= maxLength ? text : string.Concat(text.AsSpan(0, maxLength), "...");
    }

    /// <summary>
    /// Unwrap a MAF <see cref="WorkflowErrorEvent"/> exception down to its root cause.
    /// MAF commonly surfaces workflow errors as
    /// <c>TargetInvocationException → … → ClientResultException</c> (for LLM HTTP errors) or
    /// <c>TargetInvocationException → InvalidOperationException</c> (for config errors).
    /// The root message is the one operators actually need.
    /// </summary>
    /// <param name="exception">The exception on the <c>WorkflowErrorEvent</c>, possibly null.</param>
    /// <param name="fallbackText">Text to use for the root message if <paramref name="exception"/> is null.</param>
    /// <returns>
    /// <c>outerType</c> — the original exception type name (for log context).
    /// <c>rootType</c> — the deepest inner exception's type name (the actionable one).
    /// <c>rootMessage</c> — the deepest message, or <paramref name="fallbackText"/> if nothing was available.
    /// </returns>
    internal static (string OuterType, string RootType, string RootMessage) FormatWorkflowException(
        Exception? exception, string fallbackText)
    {
        if (exception is null)
        {
            return ("Unknown", "Unknown", fallbackText);
        }
        var root = exception;
        while (root.InnerException is not null)
        {
            root = root.InnerException;
        }
        return (
            OuterType: exception.GetType().Name,
            RootType: root.GetType().Name,
            RootMessage: root.Message ?? fallbackText);
    }

    /// <summary>
    /// Dispose the owned <see cref="HttpClient"/> used by the Tavily search provider (if any).
    /// Simulated provider owns nothing, so disposal is a no-op in that case.
    /// </summary>
    public void Dispose()
    {
        _ownedHttpClient?.Dispose();
    }
}

/// <summary>
/// The complete result of a research session.
/// V2: Includes verification results and iteration count.
/// </summary>
public sealed class ResearchResult
{
    public required string SessionId { get; init; }
    public required string Query { get; init; }
    public string? Report { get; init; }
    public required string Provider { get; init; }
    public required string Model { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public required DateTimeOffset CompletedAt { get; init; }
    public IReadOnlyList<ResearchFinding> Findings { get; init; } = [];
    public IReadOnlyList<SourceRecord> Sources { get; init; } = [];
    public IReadOnlyList<ChatMessage> AgentHistory { get; init; } = [];
    public IReadOnlyList<AgentInteraction> AgentInteractions { get; init; } = [];
    public IReadOnlyList<string> ContextLog { get; init; } = [];

    /// <summary>
    /// Verification results from the FINDER-inspired checklist verification phase.
    /// </summary>
    public VerificationResult? VerificationResult { get; init; }

    /// <summary>
    /// Number of Researcher↔Analyst iteration cycles completed.
    /// </summary>
    public int IterationCount { get; init; }

    /// <summary>
    /// Number of reflections recorded by the Researcher across all iterations.
    /// </summary>
    public int ReflectionCount { get; init; }

    /// <summary>
    /// All reflection entries for state file export.
    /// </summary>
    public IReadOnlyList<ReflectionEntry> Reflections { get; init; } = [];

    /// <summary>
    /// Raw output from the Planner agent — the research plan text.
    /// Persisted in the state file so follow-up sessions can see what was planned.
    /// </summary>
    public string? PlannerOutput { get; init; }

    /// <summary>
    /// Sub-question progress entries for state file export.
    /// </summary>
    public IReadOnlyList<SubQuestionProgress> SubQuestionProgress { get; init; } = [];

    /// <summary>
    /// Session ID of the prior state that was loaded, if any.
    /// </summary>
    public string? PriorSessionId { get; init; }

    /// <summary>
    /// Name of the web-search provider used (<c>Tavily</c>, <c>Simulated</c>, …). Distinct from
    /// <see cref="Provider"/> which names the LLM backend.
    /// </summary>
    public string SearchProvider { get; init; } = "Simulated";

    /// <summary>Number of web-search tool invocations during this session.</summary>
    public int WebSearchCallCount { get; init; }

    /// <summary>Total number of search results returned across all web-search calls.</summary>
    public int WebSearchResultCount { get; init; }

    /// <summary>Wall-clock time spent waiting on the search backend, summed over all calls.</summary>
    public long WebSearchTotalLatencyMs { get; init; }

    /// <summary>
    /// Verdict from the evidence-sufficiency gate (P2.4). <c>null</c> when
    /// <c>Research:EvidenceGate:Mode=Off</c>.
    /// </summary>
    public EvidenceVerdict? EvidenceVerdict { get; init; }

    /// <summary>True when the evidence gate rejected synthesis and a diagnostic report was emitted.</summary>
    public bool SynthesisRefused { get; init; }
}
