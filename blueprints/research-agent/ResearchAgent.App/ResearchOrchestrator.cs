using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
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
public sealed class ResearchOrchestrator
{
    private readonly IConfiguration _config;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<ResearchOrchestrator> _logger;
    private readonly ResearchMemory _memory;
    private readonly ResearchStateFile? _priorState;
    private readonly IProgress<ResearchProgressEvent>? _progress;

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

        if (priorState is not null)
        {
            _memory.LoadPriorState(priorState);
            _logger.LogInformation("Loaded prior state from session {PriorSessionId}: {FindingCount} findings, {SourceCount} sources",
                priorState.Metadata.SessionId, priorState.Findings.Count, priorState.Sources.Count);
        }
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
        var taskPrompt = _priorState is not null
            ? BuildTaskPromptWithPrior(query, _priorState)
            : BuildTaskPrompt(query);
        _logger.LogInformation("Phase 3: Running Planner...");
        ReportProgress(ResearchProgressKind.PhaseChange, _priorState is not null ? "Planning (with prior context)..." : "Planning...");

        var plannerOutput = await RunSingleAgentAsync(
            plannerAgent, "Planner", taskPrompt, state.SessionId, interactions, history, ct);

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

        // ── Phase 5: Synthesizer ───────────────────────────
        state.CurrentPhase = ResearchPhase.Synthesizing;
        _logger.LogInformation("Phase 5: Running Synthesizer...");
        ReportProgress(ResearchProgressKind.PhaseChange, $"Synthesizing report ({_memory.GetAllFindings().Count} findings, {_memory.GetAllSources().Count} sources)...");

        var synthesizerInput = $"""
            ## Analysis Summary

            {interactions.LastOrDefault(i => i.Agent == "Analyst")?.Text ?? "(no analysis)"}

            ---
            Use GetAllResearchContext to access all findings and notes.
            Produce the final comprehensive research report.
            Total research iterations completed: {state.IterationCount}
            """;

        var synthesizerOutput = await RunSingleAgentAsync(
            synthesizerAgent, "Synthesizer", synthesizerInput, state.SessionId, interactions, history, ct);
        finalReport = synthesizerOutput;

        // ── Phase 6: Verifier (optional) ─────────────────────────
        if (verificationEnabled && verifierAgent is not null)
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
            var responseText = ExtractAgentOutput(events, agentName);

            if (string.IsNullOrEmpty(responseText))
            {
                _logger.LogWarning("Agent {AgentName} produced EMPTY response", agentName);
                activity?.SetStatus(ActivityStatusCode.Error, "Empty response");
                responseText = $"(Agent {agentName} produced no output)";
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
            throw;
        }
    }

    /// <summary>
    /// Extract the accumulated text output from workflow events for a single agent.
    /// </summary>
    private string ExtractAgentOutput(List<WorkflowEvent> events, string agentName)
    {
        var sb = new StringBuilder();

        foreach (var evt in events)
        {
            switch (evt)
            {
                case WorkflowErrorEvent errorEvt:
                    _logger.LogError("WORKFLOW ERROR in {AgentName}: {ErrorType}: {ErrorMessage}",
                        agentName,
                        errorEvt.Exception?.GetType().Name ?? "Unknown",
                        errorEvt.Exception?.Message ?? errorEvt.ToString());
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
        tools.AddRange(CreateToolsFrom(new WebSearchPlugin(_memory, _loggerFactory)));
        tools.AddRange(CreateToolsFrom(new ContentExtractionPlugin(_memory, _loggerFactory)));
        tools.AddRange(CreateToolsFrom(new NoteTakingPlugin(_memory, _loggerFactory)));

        _logger.LogDebug("Researcher agent tools: {ToolNames}",
            string.Join(", ", tools.Select(t => t.GetType().Name)));

        return chatClient.AsAIAgent(
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
            tools: tools);
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

        return chatClient.AsAIAgent(
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
            tools: tools);
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
}
