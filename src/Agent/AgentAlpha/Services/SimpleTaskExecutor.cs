using Microsoft.Extensions.Logging;
using AgentAlpha.Configuration;
using AgentAlpha.Interfaces;
using AgentAlpha.Models;
using Common.Interfaces.Session;
using MCPClient;
using ModelContextProtocol.Client;
using System.Collections.Generic;
using Common.Models.Session;              // ← explicit for Dictionary
using System.Linq;                               // NEW
using System.Text.RegularExpressions;    // NEW
using System.Text.Json;                 // + ADD
using OpenAIIntegration;
using OpenAIIntegration.Model;                       // +NEW

namespace AgentAlpha.Services;

/// <summary>
/// Simplified task executor with reduced dependencies and cleaner responsibility chain
/// </summary>
public class SimpleTaskExecutor : ITaskExecutor
{
    private readonly IConnectionManager _connectionManager;
    private readonly SimpleToolManager _toolManager;
    private readonly IConversationManager _conversationManager;
    private readonly ISessionManager _sessionManager;
    private readonly ISessionActivityLogger _activityLogger;
    private readonly AgentConfiguration _config;
    private readonly ILogger<SimpleTaskExecutor> _logger;
    private readonly IPlanner _planner;
    private readonly PlanRefinementLoop _planRefiner;
    private readonly ISessionAwareOpenAIService _openAiService;   // +NEW

    private string _currentPlan = "";          // NEW – latest plan in memory
    private readonly int _refineEvery;         // NEW – cadence (0 = off)

    public SimpleTaskExecutor(
        IConnectionManager connectionManager,
        SimpleToolManager toolManager,
        IConversationManager conversationManager,
        ISessionManager sessionManager,
        ISessionActivityLogger activityLogger,
        AgentConfiguration config,
        ILogger<SimpleTaskExecutor> logger,
        IPlanner planner,
        PlanRefinementLoop planRefiner,
        ISessionAwareOpenAIService openAiService)                 // +NEW
    {
        _connectionManager = connectionManager;
        _toolManager = toolManager;
        _conversationManager = conversationManager;
        _sessionManager = sessionManager;
        _activityLogger = activityLogger;
        _config = config;
        _logger = logger;
        _planner = planner;
        _planRefiner = planRefiner;
        _openAiService = openAiService;                          // +NEW
        _refineEvery = config.PlanRefinementEveryIterations;   // NEW

        // Set up activity logging
        _toolManager.SetActivityLogger(_activityLogger);
    }

    public async Task ExecuteAsync(string task)
    {
        var request = new TaskExecutionRequest { Task = task };
        await ExecuteAsync(request);
    }

    public async Task ExecuteAsync(TaskExecutionRequest request)
    {
        // ------------------------------------------------------------------
        // Ensure the request always contains a unique session name
        // ------------------------------------------------------------------
        if (string.IsNullOrEmpty(request.SessionName) && string.IsNullOrEmpty(request.SessionId))
        {
            request.SessionName = $"session-{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        }

        var sessionName = !string.IsNullOrEmpty(request.SessionName)
            ? request.SessionName
            : $"session-{request.SessionId}"; // SessionId is not null/empty here

        try
        {
            // Connect to MCP server
            await ConnectToMcpServerAsync();

            // Get or create session
            var session = await GetOrCreateSessionAsync(request, sessionName);

            // >>> INFORM ACTIVITY LOGGER ABOUT THE CURRENT SESSION <<<
            _activityLogger.SetCurrentSession(session);

            // Set up conversation
            await SetupConversationAsync(session, request);

            /* ---------- NEW: generate and log execution plan ---------------- */
            // Discover & filter tools once (also reused later)
            var availableTools = await _toolManager.DiscoverToolsAsync(_connectionManager);
            var filteredTools  = _toolManager.ApplyFilters(availableTools, _config.ToolFilter);

            // convert to names ↓↓↓
            var toolNames      = filteredTools.Select(t => t.Name).ToList();

            var plan           = await _planner.CreatePlanAsync(request.Task, toolNames);
            plan               = await _planRefiner.RefinePlanAsync(plan, request.Task); // NEW
            _currentPlan       = plan;                          // NEW

            _logger.LogInformation("Generated execution plan:\n{Plan}", plan);

            // Persist the initial plan as a normal activity (uses logger ↔ truncation, etc.)
            await _activityLogger.LogActivityAsync(
                ActivityTypes.Planning,
                "Initial execution plan generated",
                new { Plan = plan });

            _logger.LogInformation("Starting ReAct conversation loop");

            // Execute main conversation loop
            var effectiveMaxIterations = request.MaxIterations ?? _config.MaxIterations;   // NEW
            await ExecuteConversationLoopAsync(
                request.Task,
                availableTools,
                filteredTools,
                effectiveMaxIterations);                                                  // CHANGED

            _logger.LogInformation("Task completed successfully");

            // Persist / show final markdown
            var md = _conversationManager.GetTaskMarkdown();
            await _activityLogger.LogActivityAsync(
                ActivityTypes.Result,
                "Final task markdown generated",
                new { Markdown = md.Length <= 5000 ? md : md[..5000] + "...[truncated]" });
            _logger.LogInformation("Final task markdown:\n{Markdown}", md);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task execution failed");
            throw;
        }
        finally
        {
            if (_connectionManager.IsConnected)
            {
                await _connectionManager.DisposeAsync();
            }
        }
    }

    private async Task ConnectToMcpServerAsync()
    {
        var url = Environment.GetEnvironmentVariable("MCP_SERVER_URL") ?? "http://localhost:3000";
        await _connectionManager.ConnectAsync(McpTransportType.Http, "MCP Server", serverUrl: url);
        _logger.LogInformation("Connected to MCP server using HTTP (SSE) at {Url}", url);
    }

    private async Task<Common.Models.Session.AgentSession> GetOrCreateSessionAsync(TaskExecutionRequest request, string sessionName)
    {
        Common.Models.Session.AgentSession? session = null;

        if (!string.IsNullOrEmpty(request.SessionId))
        {
            // Try to get existing session
            try
            {
                session = await _sessionManager.GetSessionAsync(request.SessionId);
                _logger.LogInformation("Retrieved existing session: {SessionName}", session?.Name ?? "Unknown");
            }
            catch
            {
                // Session not found, create new one
                session = await _sessionManager.CreateSessionAsync(sessionName);
                _logger.LogInformation("Created new session: {SessionName}", sessionName);
            }
        }
        else if (!string.IsNullOrEmpty(request.SessionName))
        {
            // Try to get session by name
            try
            {
                session = await _sessionManager.GetSessionByNameAsync(request.SessionName);
                _logger.LogInformation("Retrieved existing session by name: {SessionName}", request.SessionName);
            }
            catch
            {
                // Session not found, create new one
                session = await _sessionManager.CreateSessionAsync(request.SessionName);
                _logger.LogInformation("Created new session: {SessionName}", request.SessionName);
            }
        }

        // Ensure we have a session
        session ??= await _sessionManager.CreateSessionAsync(sessionName);
        _logger.LogInformation("Using session: {SessionName}", sessionName);

        return session;
    }

    private Task SetupConversationAsync(Common.Models.Session.AgentSession session, TaskExecutionRequest request)
    {
        if (!string.IsNullOrEmpty(session.ConversationState))
        {
            // Resume existing conversation
            _conversationManager.InitializeFromSession(session, request.Task);
            _logger.LogInformation("Resumed conversation from session");
        }
        else
        {
            // Start new conversation
            var systemPrompt = CreateSystemPrompt();
            _conversationManager.InitializeConversation(systemPrompt, request.Task);
            _logger.LogInformation("Started new conversation");
        }

        return Task.CompletedTask;
    }

    /* -------------------- SIGNATURE CHANGE ------------------------------ */
    private async Task ExecuteConversationLoopAsync(
        string task,
        IList<McpClientTool> availableTools,
        IList<McpClientTool> filteredTools,
        int maxIterations)                                        // NEW PARAM
    {
        string? sessionId = _activityLogger.GetCurrentSession()?.SessionId;
        if (string.IsNullOrEmpty(sessionId))
        {
            _logger.LogWarning("No session ID available for activity logging");
            throw new InvalidOperationException("Session ID is required for activity logging");
        }

        var iteration = 0;                                        // maxIterations comes from param

        while (iteration < maxIterations)
        {
            iteration++;

            if (_refineEvery > 0 && iteration % _refineEvery == 0)
            {
                var refined = await _planRefiner.RefinePlanAsync(
                                   _currentPlan,
                                   $"Progress after {iteration} iterations");
                if (!string.Equals(refined, _currentPlan, StringComparison.Ordinal))
                {
                    _currentPlan = refined;
                    _logger.LogInformation("Plan refined at iteration {It}.", iteration);

                    await _activityLogger.LogActivityAsync(
                        ActivityTypes.PlanRefined,
                        $"Plan refined at iteration {iteration}",
                        new { Plan = _currentPlan });
                }
            }

            // NO pre-computed selectedTools here – we refresh every iteration
            var selectedTools = await _toolManager.SelectToolsForPlanAsync(
                                    task,
                                    filteredTools);

            _logger.LogDebug("ReAct iteration {Iteration}: Using {ToolCount} tools",
                             iteration, selectedTools.Count);

            var response = await _conversationManager.ProcessIterationAsync(selectedTools);

            if (!response.HasToolCalls)
            {
                // No tool calls - check if this is reasoning only or task completion
                if (_conversationManager.IsTaskComplete(response))
                {
                    _logger.LogInformation("Task completed after {Iterations} ReAct iterations", iteration);
                    break;
                }
                else
                {
                    // This might be a reasoning-only response, which is valid in ReAct pattern
                    _logger.LogInformation("ReAct iteration {Iteration}: Reasoning phase completed, no immediate action planned", iteration);
                    
                    // For reasoning-only responses, we'll continue to the next iteration
                    // The ReAct pattern allows for pure reasoning phases
                    if (iteration < maxIterations - 1) // Leave room for final iteration
                    {
                        continue;
                    }
                    else
                    {
                        _logger.LogWarning("ReAct loop completed without final action in iteration {Iteration}", iteration);
                        break;
                    }
                }
            }

            // Execute tool calls (Action phase)
            _logger.LogDebug("ReAct iteration {Iteration}: Executing {ToolCallCount} actions", iteration, response.ToolCalls.Count);
            var toolResults = new List<string>();
            bool taskCompletedViaTools = false;
            
            foreach (var toolCall in response.ToolCalls)
            {
                try
                {
                    var result = await _toolManager.ExecuteToolAsync(_connectionManager, toolCall.Name, toolCall.Arguments);
                    toolResults.Add($"[{toolCall.Name}] {result}");
                    _logger.LogDebug("ReAct iteration {Iteration}: Tool '{ToolName}' executed successfully", iteration, toolCall.Name);
                    
                    // Check if this was a completion tool
                    if (toolCall.Name.Equals("complete_task", StringComparison.OrdinalIgnoreCase))
                    {
                        taskCompletedViaTools = true;
                        _logger.LogInformation("Task completion detected via complete_task tool execution");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ReAct iteration {Iteration}: Tool execution failed for {ToolName}", iteration, toolCall.Name);
                    toolResults.Add($"[{toolCall.Name}] Error: {ex.Message}");
                }
            }


            // -------- summarise & integrate ---------------------------------
            var combinedResults = string.Join("\n", toolResults);
            var summary = await GetSummaryAsync(combinedResults);
            var summarisedResults = new List<string> { summary };

            // Append a small section to the current plan so future iterations
            // can reason about what happened without bloating the context.
            if (summarisedResults.Count > 0)
            {
                _currentPlan += $"""
                                    
                                    ### Iteration {iteration} – Tool Outputs
                                    {string.Join("\n", summarisedResults.Select(s => "- " + s))}
                                    """;
            }
            // -----------------------------------------------------------------

            // Add tool results to conversation for observation phase
            _conversationManager.AddToolResults(summarisedResults);
            _logger.LogDebug("ReAct iteration {Iteration}: Tool results added, prompting observation phase", iteration);
            
            // Check if task was completed via tool execution
            if (taskCompletedViaTools)
            {
                _logger.LogInformation("Task completed via complete_task tool after {Iterations} ReAct iterations", iteration);
                break;
            }
        }

        if (iteration >= maxIterations)
        {
            _logger.LogWarning("ReAct conversation loop reached maximum iterations ({MaxIterations})", maxIterations);
        }
    }

    private string CreateSystemPrompt()
    {
        return """
            You are an AI agent that follows the ReAct (Reasoning and Acting) methodology to solve tasks systematically.

            **ReAct Pattern - Follow these steps in each iteration:**

            1. **REASONING**: Think step-by-step about the task
               - Analyze what you need to accomplish
               - Consider what information you have vs. what you need
               - Plan your approach and identify the next logical action
               - Explain your reasoning clearly

            2. **ACTING**: Execute a specific action using available tools
               - Choose the most appropriate tool for your current step
               - Use precise parameters based on your reasoning
               - Take only one focused action per iteration

            3. **OBSERVING**: Analyze the results of your action
               - Examine what the tool returned
               - Determine if the result is what you expected
               - Assess if this moves you closer to task completion
               - Identify any new information or insights gained

            4. **ITERATING**: Decide on the next step
               - If task is complete, use the 'complete_task' tool with comprehensive details
               - If more work is needed, reason about the next action
               - If you encounter errors, adjust your approach

            **Important Guidelines**
            - Always start with reasoning before taking action
            - Take one action at a time and observe the results
            - Be explicit about your thought process
            - Build upon previous observations
            - If stuck, adjust your approach
            - When finished, ALWAYS use the 'complete_task' tool; include summary, reasoning, evidence, deliverables, key actions.

            Follow this cycle until you formally complete the task with 'complete_task'.
            """;
    }

    private static McpTransportType GetMcpTransportType() => McpTransportType.Http;

    private async Task<string> BuildExecutionPlanAsync(
        string task, IList<string> toolNames, string? sessionId)
        => await _planner.CreatePlanAsync(task, toolNames, sessionId);

    /* ---------- NEW: helper to extract bullet-list subtasks ------------ */
    private static List<string> ExtractSubTasks(string plan)
    {
        var subs = new List<string>();
        foreach (var line in plan.Split('\n'))
        {
            var m = Regex.Match(line.Trim(), @"^- \[.\]\s*(.+)$");
            if (m.Success)
                subs.Add(m.Groups[1].Value.Trim());
        }
        return subs;
    }

    // ------------------------------------------------------------------
    // NEW: Uses an LLM call to generate a concise summary (<1000 chars)
    // ------------------------------------------------------------------
    private async Task<string> GetSummaryAsync(string content)
    {
        int maxContentSize = 16000;
        // Short inputs don't need summarisation
        if (content.Length < maxContentSize) return content;

        // TODO: maybe the user content should contain the current plan state?
        var req = new ResponsesCreateRequest
        {
            Model = _config.Model,
            Input = new[]
            {
                new { role = "system",  content = "You are a helpful assistant that summarises tool outputs." },
                new { role = "user",    content = $"""
                    Please provide a summary of the following tool output:

                    ---
                    {content}
                    ---
                    """ }
            },
            MaxOutputTokens = 5000,
            Temperature     = 0.2
        };

        try
        {
            var resp = await _openAiService.CreateResponseAsync(req);
            var summary = resp.Output?
                              .OfType<OutputMessage>()
                              .FirstOrDefault()?.Content?.ToString() ?? "";

            return string.IsNullOrWhiteSpace(summary) ? content[..Math.Min(1000, content.Length)] : summary.Trim();
        }
        catch
        {
            // Fallback: return first 1000 chars on error
            return content[..Math.Min(1000, content.Length)];
        }
    }
}