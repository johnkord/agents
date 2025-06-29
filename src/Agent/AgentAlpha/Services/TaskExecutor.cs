using Microsoft.Extensions.Logging;
using MCPClient;
using ModelContextProtocol.Client;
using AgentAlpha.Configuration;
using AgentAlpha.Interfaces;
using AgentAlpha.Models;
using Common.Interfaces.Session;
using Common.Models.Session;
using System.Text.Json;                 // +NEW

namespace AgentAlpha.Services;

/// <summary>
/// Implementation of task execution orchestration
/// </summary>
public class TaskExecutor : ITaskExecutor
{
    private readonly IConnectionManager _connectionManager;
    private readonly IToolManager _toolManager;
    private readonly IToolSelector _toolSelector;
    private readonly IConversationManager _conversationManager;
    private readonly ISessionManager _sessionManager;
    private readonly IPlanningService _planningService;
    private readonly ISessionActivityLogger _activityLogger;
    private readonly ITaskStateManager _taskStateManager;
    private readonly IMarkdownTaskStateManager? _markdownTaskStateManager;
    private readonly AgentConfiguration _config;
    private readonly ILogger<TaskExecutor> _logger;

    private readonly IToolScopeManager _toolScope;                 // NEW

    public TaskExecutor(
        IConnectionManager connectionManager,
        IToolManager toolManager,
        IToolSelector toolSelector,
        IConversationManager conversationManager,
        ISessionManager sessionManager,
        IPlanningService planningService,
        ISessionActivityLogger activityLogger,
        ITaskStateManager taskStateManager,
        AgentConfiguration config,
        ILogger<TaskExecutor> logger,
        IToolScopeManager toolScope,                               // NEW
        IMarkdownTaskStateManager? markdownTaskStateManager = null)
    {
        _connectionManager = connectionManager;
        _toolManager = toolManager;
        _toolSelector = toolSelector;
        _conversationManager = conversationManager;
        _sessionManager = sessionManager;
        _planningService = planningService;
        _activityLogger = activityLogger;
        _taskStateManager = taskStateManager;
        _markdownTaskStateManager = markdownTaskStateManager;
        _config = config;
        _logger = logger;
        _toolScope = toolScope;                                    // NEW
    }

    /// <summary>
    /// Executes a simple task string using default configuration
    /// </summary>
    /// <param name="task">The task description to execute</param>
    /// <returns>Task representing the async operation</returns>
    /// <remarks>
    /// This method provides backward compatibility by converting the task string
    /// to a TaskExecutionRequest with default settings
    /// </remarks>
    public async Task ExecuteAsync(string task)
    {
        // Maintain backwards compatibility by creating a simple request
        var request = TaskExecutionRequest.FromTask(task);
        await ExecuteAsync(request);
    }

    /// <summary>
    /// Executes a complete task using the specified request parameters
    /// </summary>
    /// <param name="request">Complete task execution request with all parameters</param>
    /// <returns>Task representing the async operation</returns>
    /// <remarks>
    /// This is the main execution method that:
    /// 1. Applies configuration overrides from the request
    /// 2. Connects to the MCP server
    /// 3. Initializes or resumes a conversation session
    /// 4. Discovers and filters available tools
    /// 5. Executes the conversation loop with the AI
    /// 6. Logs all activities and handles errors gracefully
    /// </remarks>
    public async Task ExecuteAsync(TaskExecutionRequest request)
    {
        _logger.LogInformation("Starting task execution: {Task}", request.Task);

        // Apply request-specific configuration overrides
        var effectiveConfig = ApplyRequestOverrides(request);

        // Initialize session and activity logging
        AgentSession? currentSession = null;

        try
        {
            // Step 1: Connect to MCP Server
            await ConnectToMcpServerAsync();

            // Step 2: Initialize conversation and determine if we're resuming a session
            var isResumingSession = await InitializeConversationAsync(request);

            // Set up activity logging for the session
            if (!string.IsNullOrEmpty(request.SessionId))
            {
                currentSession = await _sessionManager.GetSessionAsync(request.SessionId);
                if (currentSession != null)
                {
                    _activityLogger.SetCurrentSession(currentSession);

                    // Set activity logger for all services that need OpenAI request logging
                    _toolSelector.SetActivityLogger(_activityLogger);
                    _planningService.SetActivityLogger(_activityLogger);

                    await _activityLogger.LogActivityAsync(
                        ActivityTypes.SessionStart,
                        $"Resumed session for task: {request.Task}",
                        new { TaskRequest = request.Task, IsResumingSession = isResumingSession });
                }
            }
            else if (!string.IsNullOrEmpty(request.SessionName))
            {
                // Create new session
                currentSession = await _sessionManager.CreateSessionAsync(request.SessionName);
                _activityLogger.SetCurrentSession(currentSession);

                // Set activity logger for all services that need OpenAI request logging
                _toolSelector.SetActivityLogger(_activityLogger);
                _planningService.SetActivityLogger(_activityLogger);

                await _activityLogger.LogActivityAsync(
                    ActivityTypes.SessionStart,
                    $"Created new session for task: {request.Task}",
                    new { TaskRequest = request.Task, SessionName = request.SessionName });
                request.SessionId = currentSession.SessionId; // Update request with new session ID
            }
            else
            {
                // No session provided - create a default temporary session for activity logging
                var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");
                var defaultSessionName = $"Session {timestamp}";
                currentSession = await _sessionManager.CreateSessionAsync(defaultSessionName);
                _activityLogger.SetCurrentSession(currentSession);

                // Set activity logger for all services that need OpenAI request logging
                _toolSelector.SetActivityLogger(_activityLogger);
                _planningService.SetActivityLogger(_activityLogger);

                await _activityLogger.LogActivityAsync(
                    ActivityTypes.SessionStart,
                    $"Created default session for task: {request.Task}",
                    new { TaskRequest = request.Task, SessionName = defaultSessionName });
                request.SessionId = currentSession.SessionId; // Update request with new session ID
            }

            // Step 3: Initialize or resume markdown-based task planning
            string taskMarkdown = "";

            // Check if we're resuming a session with existing markdown plan
            if (isResumingSession && !string.IsNullOrEmpty(request.SessionId))
            {
                var session = await _sessionManager.GetSessionAsync(request.SessionId);

                if (!string.IsNullOrEmpty(session?.TaskStateMarkdown))
                {
                    Console.WriteLine("📋 Found existing markdown-based plan in session");
                    taskMarkdown = session.TaskStateMarkdown;

                    await _activityLogger.LogActivityAsync(
                        ActivityTypes.TaskPlanning,
                        "Found existing markdown plan in session",
                        new { SessionId = request.SessionId, HasMarkdown = true, MarkdownPlan = taskMarkdown });
                }
                else
                {
                    Console.WriteLine("📋 No existing plan found, creating new markdown plan");
                    taskMarkdown = await InitializeMarkdownPlanAsync(request);
                }
            }
            else
            {
                Console.WriteLine("📋 Creating new markdown-based plan");
                taskMarkdown = await InitializeMarkdownPlanAsync(request);
            }

            // Display the markdown plan to the user
            if (!string.IsNullOrEmpty(taskMarkdown))
            {
                DisplayMarkdownPlan(taskMarkdown);
            }

            // Step 4: Execute using markdown-based task management
            if (!string.IsNullOrEmpty(taskMarkdown) && !string.IsNullOrEmpty(request.SessionId))
            {
                await ExecuteMarkdownBasedTaskAsync(taskMarkdown, request, effectiveConfig, currentSession!);
            }
            else
            {
                // Fallback to conversation-based execution without structured plan
                await ExecuteConversationBasedAsync(request, effectiveConfig);
            }

            // Step 6: Save session if applicable
            await SaveSessionIfApplicableAsync(request);

            // Log successful completion
            if (currentSession != null)
            {
                await _activityLogger.LogActivityAsync(
                    ActivityTypes.SessionEnd,
                    "Task execution completed successfully",
                    new { TaskRequest = request.Task, SessionId = currentSession.SessionId });
            }
        }
        catch (Exception ex)
        {
            // Log error activity
            if (currentSession != null)
            {
                await _activityLogger.LogFailedActivityAsync(
                    ActivityTypes.Error,
                    "Task execution failed",
                    ex.Message,
                    new { TaskRequest = request.Task, ErrorType = ex.GetType().Name });
            }

            _logger.LogError(ex, "Task execution failed");
            throw;
        }
    }



    private async Task<IList<IUnifiedTool>> DiscoverAvailableToolsAsync()
    {
        var allTools = await _toolManager.DiscoverAllToolsAsync(_connectionManager);
        return _toolManager.ApplyFiltersToAllTools(allTools, _config.ToolFilter);
    }

    private async Task<string> InitializeMarkdownPlanAsync(TaskExecutionRequest request)
    {
        _logger.LogInformation("Initializing markdown-based plan for task: {Task}", request.Task);

        var availableTools = await DiscoverAvailableToolsAsync();

        // Build minimal state – session already contains context
        var state = new CurrentState
        {
            CapturedAt = DateTime.UtcNow
        };

        // --- call signature changed (no context arg) ---
        var markdownPlan = await _planningService.InitializeTaskPlanningWithStateAsync(
            request.SessionId ?? Guid.NewGuid().ToString(),
            request.Task,
            availableTools,
            state);

        // No local copy needed
        return markdownPlan;
    }

    private void DisplayMarkdownPlan(string taskMarkdown)
    {
        Console.WriteLine("\n" + "=".PadRight(80, '='));
        Console.WriteLine("📋 TASK EXECUTION PLAN");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine(taskMarkdown);
        Console.WriteLine("=".PadRight(80, '=') + "\n");
    }

    private async Task ExecuteMarkdownBasedTaskAsync(string taskMarkdown, TaskExecutionRequest request, AgentConfiguration config, AgentSession session)
    {
        try
        {
            _logger.LogInformation("Starting markdown-based task execution for session {SessionId}", session.SessionId);

            // Discover available tools for execution
            var availableTools = await DiscoverAvailableToolsAsync();
            var toolDefinitions = availableTools.Select(t => t.ToToolDefinition()).ToArray();

            // Start the conversation-based execution loop
            await ExecuteConversationLoopAsync(toolDefinitions, config, session.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute markdown-based task for session {SessionId}", session.SessionId);
            throw;
        }
    }

    private async Task ExecuteConversationBasedAsync(TaskExecutionRequest request, AgentConfiguration config)
    {
        try
        {
            _logger.LogInformation("Starting conversation-based execution (no session/plan)");

            // Discover available tools for execution
            var availableTools = await DiscoverAvailableToolsAsync();
            var toolDefinitions = availableTools.Select(t => t.ToToolDefinition()).ToArray();

            // Start the conversation-based execution loop without session
            await ExecuteConversationLoopAsync(toolDefinitions, config, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute conversation-based task");
            throw;
        }
    }





    private AgentConfiguration ApplyRequestOverrides(TaskExecutionRequest request)
    {
        // Create a copy of the base configuration with request-specific overrides
        var config = new AgentConfiguration
        {
            OpenAiApiKey = _config.OpenAiApiKey,
            Model = request.Model ?? _config.Model,
            MaxIterations = request.MaxIterations ?? _config.MaxIterations,
            Transport = _config.Transport,
            ServerUrl = _config.ServerUrl,
            ToolFilter = request.ToolFilter ?? _config.ToolFilter
        };

        if (request.VerboseLogging)
        {
            _logger.LogInformation("Request overrides applied - Model: {Model}, MaxIterations: {MaxIterations}, Priority: {Priority}",
                config.Model, config.MaxIterations, request.Priority);
        }

        return config;
    }

    private async Task ConnectToMcpServerAsync()
    {
        try
        {
            var serverUrl = _config.Transport == McpTransportType.Http ? _config.ServerUrl : null;
            await _connectionManager.ConnectAsync(_config.Transport, "Agent MCP Server", serverUrl);
            Console.WriteLine("✅ Connected to MCP Server");
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ Failed to connect to MCP Server. Please ensure:");
            Console.WriteLine("   - The MCPServer project builds successfully");
            Console.WriteLine("   - No other MCP server instances are running");
            Console.WriteLine("   - Run from the correct directory (src/Agent/AgentAlpha)");
            Console.WriteLine($"   Error: {ex.Message}");
            throw;
        }
    }

    private async Task<OpenAIIntegration.Model.ToolDefinition[]> DiscoverAndSelectToolsAsync(
        TaskExecutionRequest request, bool isResumingSession = false)
    {
        var filterConfig = request.ToolFilter ?? _config.ToolFilter;

        // Step 1: Discover all tools from MCP server
        var allTools = await _toolManager.DiscoverAllToolsAsync(_connectionManager);

        // Step 2: Apply filtering configuration
        var filteredTools = _toolManager.ApplyFiltersToAllTools(allTools, filterConfig);

        Console.WriteLine($"🔧 Discovered {allTools.Count} tools total, {filteredTools.Count} after filtering");

        if (filteredTools.Count != allTools.Count)
        {
            var excluded = allTools.Where(t => !filterConfig.ShouldIncludeTool(t.Name)).Select(t => t.Name);
            Console.WriteLine($"🚫 Excluded tools: {string.Join(", ", excluded)}");
        }

        // Step 3: Use intelligent tool selection to reduce context size
        try
        {
            var selectedTools = await _toolSelector.SelectToolsForTaskAsync(
                request.Task,
                filteredTools,
                _config.ToolSelection.MaxToolsPerRequest);

            // --- ensure required tools are always included ----------------
            var required = _toolScope.GetRequiredTools(request.SessionId ?? "");
            if (required.Length > 0)
            {
                var selectedNames = selectedTools.Select(t => t.Name)
                                                 .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var ensured = new List<OpenAIIntegration.Model.ToolDefinition>(selectedTools);

                foreach (var req in required)
                {
                    if (selectedNames.Contains(req)) continue;

                    var match = filteredTools.FirstOrDefault(t =>
                        string.Equals(t.Name, req, StringComparison.OrdinalIgnoreCase));

                    if (match != null)
                    {
                        ensured.Add(match.ToToolDefinition());
                        selectedNames.Add(req);
                    }
                    else
                    {
                        _logger.LogWarning("Required tool '{Tool}' not found among available tools", req);
                    }
                }

                selectedTools = ensured.ToArray();
            }
            // ----------------------------------------------------------------

            var selectionContext = isResumingSession ? "for new task in session" : "for task";
            Console.WriteLine($"🎯 Selected {selectedTools.Length} relevant tools {selectionContext}: " +
                            $"{string.Join(", ", selectedTools.Select(t => t.Name))}");

            if (selectedTools.Length < filteredTools.Count)
            {
                var notSelected = filteredTools
                    .Where(t => !selectedTools.Any(s => s.Name == t.Name))
                    .Select(t => t.Name);
                Console.WriteLine($"💡 Available for expansion: {notSelected.Count()} additional tools");
            }

            return selectedTools;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool selection failed, falling back to all filtered tools");
            Console.WriteLine($"⚠️  Tool selection failed, using all {filteredTools.Count} filtered tools");
            return filteredTools.Select(t => t.ToToolDefinition()).ToArray();
        }
    }

    private async Task<bool> InitializeConversationAsync(TaskExecutionRequest request)
    {
        var systemPrompt = request.SystemPrompt ?? """
            You are AgentAlpha, a helpful AI assistant that can perform various tasks using available tools.
            
            Available capabilities include:
            - File operations (read, write, list directories, file information)
            - Text processing (search, replace, format, word count, split text)
            - System information (current time, environment variables, system details)
            - Task completion tracking (complete_task tool)
            
            When given a task:
            1. Break it down into steps if needed
            2. Use appropriate tools to accomplish each step
            3. Provide clear feedback on what you're doing
            4. Explain the results and next steps
            5. **IMPORTANT: When the task is fully completed, you MUST call the 'complete_task' tool to signal completion**
            
            Always use tools when possible rather than trying to do calculations or file operations yourself.
            If you're unsure about a tool's parameters, start with simpler operations and build up.
            
            **TASK COMPLETION REQUIREMENT:**
            - When you have successfully completed the requested task, you MUST call the 'complete_task' tool
            - You can optionally provide a summary of what was accomplished using the 'summary' parameter
            - Do NOT just say "the task is complete" in text - you must use the complete_task tool
            - This ensures proper task tracking and prevents unnecessary iterations
            """;

        // Handle session-based initialization
        bool isResumingSession = false;

        if (!string.IsNullOrEmpty(request.SessionId))
        {
            // Load existing session
            var session = await _sessionManager.GetSessionAsync(request.SessionId);
            if (session != null)
            {
                _conversationManager.InitializeFromSession(session, request.Task);
                Console.WriteLine($"🔄 Resuming session: {session.Name} ({session.SessionId})");
                Console.WriteLine($"📝 New task: {request.Task}");
                isResumingSession = true;
            }
            else
            {
                _logger.LogWarning("Session {SessionId} not found, starting new conversation", request.SessionId);
                _conversationManager.InitializeConversation(systemPrompt, request.Task);
                Console.WriteLine($"📝 Task: {request.Task}");
            }
        }
        else if (!string.IsNullOrEmpty(request.SessionName))
        {
            // Check if session with this name already exists
            var existingSession = await _sessionManager.GetSessionByNameAsync(request.SessionName);
            if (existingSession != null)
            {
                // Resume existing session
                request.SessionId = existingSession.SessionId; // Set for later saving
                _conversationManager.InitializeFromSession(existingSession, request.Task);
                Console.WriteLine($"🔄 Resuming session: {existingSession.Name} ({existingSession.SessionId})");
                Console.WriteLine($"📝 New task: {request.Task}");
                isResumingSession = true;
            }
            else
            {
                // Create new session
                var session = await _sessionManager.CreateSessionAsync(request.SessionName);
                request.SessionId = session.SessionId; // Set for later saving

                _conversationManager.InitializeConversation(systemPrompt, request.Task);
                Console.WriteLine($"💾 Created new session: {session.Name} ({session.SessionId})");
                Console.WriteLine($"📝 Task: {request.Task}");
            }
        }
        else
        {
            // Standard non-persistent conversation
            _conversationManager.InitializeConversation(systemPrompt, request.Task);
            Console.WriteLine($"📝 Task: {request.Task}");
        }

        if (request.Model != null)
        {
            Console.WriteLine($"🤖 Model: {request.Model}");
        }

        if (request.Temperature.HasValue)
        {
            Console.WriteLine($"🌡️ Temperature: {request.Temperature:F1}");
        }

        if (request.Priority != TaskPriority.Normal)
        {
            Console.WriteLine($"⚡ Priority: {request.Priority}");
        }

        return isResumingSession;
    }

    private async Task ExecuteConversationLoopAsync(OpenAIIntegration.Model.ToolDefinition[] availableTools, AgentConfiguration config, string? sessionId = null, CancellationToken cancellationToken = default)
    {
        // Keep track of currently available tools for dynamic expansion
        var currentTools = availableTools.ToList();
        var allAvailableTools = await _toolManager.DiscoverAllToolsAsync(_connectionManager);
        var filteredAvailableTools = _toolManager.ApplyFiltersToAllTools(allAvailableTools, config.ToolFilter);

        for (int i = 0; i < config.MaxIterations; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Console.WriteLine($"\n--- Iteration {i + 1} ---");

            // Process one iteration with potential tool expansion
            var response = await _conversationManager.ProcessIterationWithExpansionAsync(
                currentTools.ToArray(),
                async () => await GetAdditionalToolsAsync(filteredAvailableTools, currentTools.ToArray()));

            // Update current tools if expansion occurred
            if (response.HasToolCalls)
            {
                // Check if any tool calls used tools not in our current set
                var usedToolNames = response.ToolCalls.Select(tc => tc.Name).ToHashSet();
                var newToolsUsed = usedToolNames.Where(name => !currentTools.Any(t => t.Name == name)).ToList();

                if (newToolsUsed.Count > 0)
                {
                    // Add the newly used tools to our current set for future iterations
                    foreach (var toolName in newToolsUsed)
                    {
                        var tool = filteredAvailableTools.FirstOrDefault(t => t.Name == toolName);
                        if (tool != null)
                        {
                            var toolDef = tool.ToToolDefinition();
                            currentTools.Add(toolDef);
                        }
                    }

                    Console.WriteLine($"🔧 Expanded tools for next iteration: +{newToolsUsed.Count} tools");
                }
            }

            // Handle tool calls if present
            if (response.HasToolCalls)
            {
                var toolSummaries = new List<string>();
                var taskCompleted = false;
                var executionFeedback = new List<string>();

                foreach (var toolCall in response.ToolCalls)
                {
                    if (!config.ToolFilter.ShouldIncludeTool(toolCall.Name))
                    {
                        toolSummaries.Add($"Tool '{toolCall.Name}' call blocked by tool filter configuration.");
                        executionFeedback.Add($"Tool '{toolCall.Name}' was blocked - plan may need adjustment");
                        continue;
                    }

                    var result = await _toolManager.ExecuteToolAsync(
                        _connectionManager,
                        toolCall.Name,
                        toolCall.Arguments ?? new Dictionary<string, object?>()); // ← warning fixed

                    // --- changed: pretty-print arguments ------------------
                    var argsJson = toolCall.Arguments?.Count > 0
                        ? JsonSerializer.Serialize(toolCall.Arguments)
                        : "{}";
                    toolSummaries.Add(
                        $"Tool '{toolCall.Name}' called with args {argsJson}. Result: {result}");

                    // Check for execution issues that might require plan updates
                    if (result.ToString().ToLowerInvariant().Contains("error") ||
                        result.ToString().ToLowerInvariant().Contains("failed"))
                    {
                        executionFeedback.Add($"Tool '{toolCall.Name}' encountered issues: {result}");
                    }
                    // ------------------------------------------------------

                    if (toolCall.Name.Equals("complete_task", StringComparison.OrdinalIgnoreCase))
                    {
                        taskCompleted = true;
                    }
                }

                _conversationManager.AddToolResults(toolSummaries);
                Console.WriteLine($"🔧 {string.Join("\n", toolSummaries)}");

                // Log conversation statistics for monitoring
                var stats = _conversationManager.GetConversationStatistics();
                _logger.LogDebug("Conversation stats: {TotalMessages} messages ({EstimatedTokens} estimated tokens)",
                    stats.TotalMessages, stats.EstimatedTokens);

                if (taskCompleted)               // NEW
                {
                    Console.WriteLine("✅ Task completed!");
                    return;
                }

                continue; // go to next iteration
            }

            // Display assistant response
            Console.WriteLine($"AI: {response.AssistantText}");

            // Check if task is complete
            if (_conversationManager.IsTaskComplete(response.AssistantText))
            {
                _logger.LogInformation("Task completion detected in iteration {Iteration}", i + 1);
                Console.WriteLine("✅ Task completed!");
                return;
            }

            // Check for repetitive responses that indicate the agent is stuck
            if (_conversationManager.WouldBeRepetitive(response.AssistantText))
            {
                Console.WriteLine("🔄 Detected repetitive responses - attempting to break out of loop");
                _logger.LogWarning("Agent appears to be stuck in repetitive responses at iteration {Iteration}", i + 1);

                // Add a guidance message to help the agent move forward
                var guidanceMessage = "I notice I'm providing similar responses repeatedly. Let me try a different approach or acknowledge if there are limitations preventing me from completing this task.";
                _conversationManager.AddAssistantMessage(guidanceMessage);

                // Try one more iteration with guidance, then exit if still stuck
                if (i >= 2) // Allow at least 3 iterations before breaking due to repetition
                {
                    Console.WriteLine("⚠️  Breaking conversation loop due to repetitive responses.");
                    break;
                }
            }
            else
            {
                // Add assistant message to conversation for next iteration
                _conversationManager.AddAssistantMessage(response.AssistantText);
                _logger.LogDebug("Added assistant response to conversation for iteration {Iteration}", i + 1);
            }
        }

        Console.WriteLine($"⚠️  Reached maximum iterations ({config.MaxIterations}).");
    }

    private async Task<OpenAIIntegration.Model.ToolDefinition[]> GetAdditionalToolsAsync(
        IList<IUnifiedTool> allAvailableTools,
        OpenAIIntegration.Model.ToolDefinition[] currentTools)
    {
        try
        {
            var conversationContext = _conversationManager.GetCurrentMessages();
            return await _toolSelector.SelectAdditionalToolsAsync(
                conversationContext,
                allAvailableTools,
                currentTools,
                _config.ToolSelection.MaxAdditionalToolsPerIteration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get additional tools");
            return Array.Empty<OpenAIIntegration.Model.ToolDefinition>();
        }
    }

    private async Task SaveSessionIfApplicableAsync(TaskExecutionRequest request)
    {
        // no session → nothing to save
        if (string.IsNullOrWhiteSpace(request.SessionId))
            return;

        try
        {
            var session = await _sessionManager.GetSessionAsync(request.SessionId);
            if (session == null) return;

            // persist latest conversation
            session.SetConversationMessages(_conversationManager.GetCurrentMessages());
            session.Status = SessionStatus.Active;

            await _sessionManager.SaveSessionAsync(session);
            _logger.LogInformation("Saved session state for {SessionId}", request.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save session {SessionId}", request.SessionId);
        }
    }
}