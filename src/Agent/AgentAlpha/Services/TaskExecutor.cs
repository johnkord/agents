using Microsoft.Extensions.Logging;
using MCPClient;
using ModelContextProtocol.Client;
using AgentAlpha.Configuration;
using AgentAlpha.Interfaces;
using AgentAlpha.Models;
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
    private readonly AgentConfiguration _config;
    private readonly ILogger<TaskExecutor> _logger;

    public TaskExecutor(
        IConnectionManager connectionManager,
        IToolManager toolManager,
        IToolSelector toolSelector,
        IConversationManager conversationManager,
        ISessionManager sessionManager,
        IPlanningService planningService,
        ISessionActivityLogger activityLogger,
        AgentConfiguration config,
        ILogger<TaskExecutor> logger)
    {
        _connectionManager = connectionManager;
        _toolManager = toolManager;
        _toolSelector = toolSelector;
        _conversationManager = conversationManager;
        _sessionManager = sessionManager;
        _planningService = planningService;
        _activityLogger = activityLogger;
        _config = config;
        _logger = logger;
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
                await _activityLogger.LogActivityAsync(
                    ActivityTypes.SessionStart,
                    $"Created default session for task: {request.Task}",
                    new { TaskRequest = request.Task, SessionName = defaultSessionName });
                request.SessionId = currentSession.SessionId; // Update request with new session ID
            }

            // Step 3: Create execution plan for the task
            TaskPlan? taskPlan = null;
            
            // Check if we're resuming a session with an existing plan
            if (isResumingSession && !string.IsNullOrEmpty(request.SessionId))
            {
                var session = await _sessionManager.GetSessionAsync(request.SessionId);
                var existingPlan = session?.GetCurrentPlan();
                
                if (existingPlan != null)
                {
                    Console.WriteLine("📋 Found existing plan in session");
                    await _activityLogger.LogActivityAsync(
                        ActivityTypes.TaskPlanning,
                        "Found existing plan in session",
                        new { ExistingPlan = existingPlan.Task, NewTask = request.Task });
                    
                    // Check if the existing plan is still relevant for the new task
                    if (IsPlanRelevantForTask(existingPlan, request.Task))
                    {
                        Console.WriteLine("✅ Reusing existing plan for similar task");
                        await _activityLogger.LogActivityAsync(
                            ActivityTypes.TaskPlanning,
                            "Reusing existing plan for similar task",
                            new { PlanTask = existingPlan.Task, NewTask = request.Task });
                        taskPlan = existingPlan;
                    }
                    else
                    {
                        Console.WriteLine("🔄 Refining existing plan for new task");
                        var planningActivityId = _activityLogger.StartActivity(
                            ActivityTypes.TaskPlanning,
                            "Refining existing plan for new task",
                            new { ExistingPlan = existingPlan.Task, NewTask = request.Task });
                        
                        try
                        {
                            // Refine the existing plan for the new task
                            var discoveredTools = await DiscoverAvailableToolsAsync();
                            var feedback = $"New task requirement: {request.Task}. Please adapt the existing plan accordingly.";
                            taskPlan = await _planningService.RefinePlanAsync(existingPlan, feedback, discoveredTools);
                            await _activityLogger.CompleteActivityAsync(planningActivityId, new { RefinedPlan = taskPlan?.Task });
                        }
                        catch (Exception ex)
                        {
                            await _activityLogger.FailActivityAsync(planningActivityId, ex.Message);
                            throw;
                        }
                    }
                }
            }
            
            // If we don't have a plan yet, create a new one
            if (taskPlan == null)
            {
                var planningActivityId = _activityLogger.StartActivity(
                    ActivityTypes.TaskPlanning,
                    "Creating new task plan",
                    new { Task = request.Task });
                
                try
                {
                    taskPlan = await CreateTaskPlanAsync(request.Task);
                    await _activityLogger.CompleteActivityAsync(planningActivityId, new { CreatedPlan = taskPlan?.Task });
                }
                catch (Exception ex)
                {
                    await _activityLogger.FailActivityAsync(planningActivityId, ex.Message);
                    throw;
                }
            }
            
            // Save the plan to the session if we have one
            if (!string.IsNullOrEmpty(request.SessionId) && taskPlan != null)
            {
                await SavePlanToSessionAsync(request.SessionId, taskPlan);
            }
            
            // Display the plan to the user
            if (taskPlan != null)
            {
                DisplayTaskPlan(taskPlan);
            }

            // Step 4: Discover tools and select relevant ones based on the plan
            var toolSelectionActivityId = _activityLogger.StartActivity(
                ActivityTypes.ToolSelection,
                "Discovering and selecting tools for plan",
                new { PlanTask = taskPlan?.Task, TaskCreatedAt = taskPlan?.CreatedAt });
            
            try
            {
                var availableTools = await DiscoverAndSelectToolsForPlanAsync(taskPlan!, request, isResumingSession);
                await _activityLogger.CompleteActivityAsync(toolSelectionActivityId, 
                    new { SelectedToolCount = availableTools?.Length, ToolNames = availableTools?.Select(t => t.Name).ToArray() });

                // Step 5: Execute conversation loop with timeout if specified
                if (request.Timeout.HasValue)
                {
                    using var cts = new CancellationTokenSource(request.Timeout.Value);
                    await ExecuteConversationLoopAsync(availableTools!, effectiveConfig, taskPlan, cts.Token);
                }
                else
                {
                    await ExecuteConversationLoopAsync(availableTools!, effectiveConfig, taskPlan);
                }
            }
            catch (Exception ex)
            {
                await _activityLogger.FailActivityAsync(toolSelectionActivityId, ex.Message);
                throw;
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

    private bool IsPlanRelevantForTask(TaskPlan existingPlan, string newTask)
    {
        // Simple heuristic to check if the existing plan is still relevant
        // Check for keyword overlap and task similarity
        var existingTaskWords = existingPlan.Task.ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .ToHashSet();
            
        var newTaskWords = newTask.ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .ToHashSet();
            
        var commonWords = existingTaskWords.Intersect(newTaskWords).Count();
        var totalUniqueWords = existingTaskWords.Union(newTaskWords).Count();
        
        // Consider plan relevant if there's significant word overlap (>30%)
        var similarity = totalUniqueWords > 0 ? (double)commonWords / totalUniqueWords : 0;
        
        _logger.LogDebug("Plan relevance check: {Similarity:P0} similarity between tasks", similarity);
        return similarity > 0.3;
    }

    private async Task<IList<McpClientTool>> DiscoverAvailableToolsAsync()
    {
        var allTools = await _toolManager.DiscoverToolsAsync(_connectionManager);
        return _toolManager.ApplyFilters(allTools, _config.ToolFilter);
    }

    private async Task SavePlanToSessionAsync(string sessionId, TaskPlan plan)
    {
        try
        {
            var session = await _sessionManager.GetSessionAsync(sessionId);
            if (session != null)
            {
                session.SetCurrentPlan(plan);
                await _sessionManager.SaveSessionAsync(session);
                _logger.LogDebug("Saved plan to session {SessionId}", sessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save plan to session {SessionId}", sessionId);
        }
    }

    private async Task ConsiderPlanUpdateAsync(TaskPlan taskPlan, List<string> executionFeedback, IList<McpClientTool> availableTools)
    {
        try
        {
            _logger.LogInformation("Considering plan update due to execution feedback");
            
            var feedback = $"Execution issues encountered: {string.Join("; ", executionFeedback)}. " +
                          "Please refine the plan to address these issues and improve execution.";
            
            var refinedPlan = await _planningService.RefinePlanAsync(taskPlan, feedback, availableTools);
            
            // Check if the refined plan is significantly different
            if (IsPlanSignificantlyDifferent(taskPlan, refinedPlan))
            {
                Console.WriteLine("🔄 Plan updated based on execution feedback");
                
                // Update the task plan reference (this would need to be passed by reference or managed differently in a real scenario)
                taskPlan.Strategy = refinedPlan.Strategy;
                taskPlan.Steps = refinedPlan.Steps;
                taskPlan.RequiredTools = refinedPlan.RequiredTools;
                taskPlan.Confidence = refinedPlan.Confidence;
                taskPlan.CreatedAt = DateTime.UtcNow;
                
                // Provide updated plan context to the conversation
                var updatedPlanContext = $"""
                    📋 Plan updated based on execution feedback:
                    New Strategy: {taskPlan.Strategy}
                    Updated Steps: {string.Join(", ", taskPlan.Steps.Select(s => $"{s.StepNumber}. {s.Description}"))}
                    
                    Please follow the updated plan going forward.
                    """;
                _conversationManager.AddAssistantMessage(updatedPlanContext);
                
                _logger.LogInformation("Plan successfully updated with {StepCount} steps", taskPlan.Steps.Count);
            }
            else
            {
                _logger.LogDebug("Plan refinement did not result in significant changes");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update plan based on execution feedback");
        }
    }

    private bool IsPlanSignificantlyDifferent(TaskPlan originalPlan, TaskPlan refinedPlan)
    {
        // Check if the number of steps changed significantly
        if (Math.Abs(originalPlan.Steps.Count - refinedPlan.Steps.Count) > 1)
            return true;
            
        // Check if the strategy changed significantly
        var originalWords = originalPlan.Strategy.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var refinedWords = refinedPlan.Strategy.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var strategySimilarity = originalWords.Intersect(refinedWords).Count() / (double)originalWords.Union(refinedWords).Count();
        
        if (strategySimilarity < 0.7) // Less than 70% similarity in strategy
            return true;
            
        // Check if required tools changed significantly
        var originalTools = originalPlan.RequiredTools.ToHashSet();
        var refinedTools = refinedPlan.RequiredTools.ToHashSet();
        var toolOverlap = originalTools.Intersect(refinedTools).Count();
        var totalTools = originalTools.Union(refinedTools).Count();
        
        if (totalTools > 0 && toolOverlap / (double)totalTools < 0.8) // Less than 80% tool overlap
            return true;
            
        return false;
    }

    public async Task<TaskPlan> CreatePlanAsync(string task)
    {
        _logger.LogInformation("Creating plan for task: {Task}", task);
        
        try
        {
            // Connect to MCP Server to get available tools
            await ConnectToMcpServerAsync();
            
            // Discover all available tools
            var allTools = await _toolManager.DiscoverToolsAsync(_connectionManager);
            var filteredTools = _toolManager.ApplyFilters(allTools, _config.ToolFilter);
            
            // Create the plan
            var plan = await _planningService.CreatePlanAsync(task, filteredTools);
            
            _logger.LogInformation("Created plan with {StepCount} steps for task", plan.Steps.Count);
            return plan;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create plan for task: {Task}", task);
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

    private async Task<OpenAIIntegration.Model.ToolDefinition[]> DiscoverAndSelectToolsAsync(TaskExecutionRequest request, bool isResumingSession = false)
    {
        var filterConfig = request.ToolFilter ?? _config.ToolFilter;
        
        // Step 1: Discover all tools from MCP server
        var allTools = await _toolManager.DiscoverToolsAsync(_connectionManager);
        
        // Step 2: Apply filtering configuration
        var filteredTools = _toolManager.ApplyFilters(allTools, filterConfig);
        
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
            
            var selectionContext = isResumingSession ? "for new task in session" : "for task";
            Console.WriteLine($"🎯 Selected {selectedTools.Length} relevant tools {selectionContext}: " +
                            $"{string.Join(", ", selectedTools.Take(5).Select(t => t.Name))}" +
                            $"{(selectedTools.Length > 5 ? "..." : "")}");
            
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
            return filteredTools.Select(t => _toolManager.CreateOpenAiToolDefinition(t)).ToArray();
        }
    }

    private async Task<TaskPlan> CreateTaskPlanAsync(string task)
    {
        Console.WriteLine("\n📋 Creating execution plan...");
        
        try
        {
            // Discover all available tools for planning
            var allTools = await _toolManager.DiscoverToolsAsync(_connectionManager);
            var filteredTools = _toolManager.ApplyFilters(allTools, _config.ToolFilter);
            
            // Create the plan using the planning service
            var plan = await _planningService.CreatePlanAsync(task, filteredTools);
            
            // Validate the plan
            var validationResult = await _planningService.ValidatePlanAsync(plan, filteredTools);
            
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Created plan has validation issues: {Issues}", 
                    string.Join(", ", validationResult.Issues));
                
                // Try to refine the plan if there are issues
                if (validationResult.Issues.Count > 0)
                {
                    var feedback = $"Plan validation found issues: {string.Join("; ", validationResult.Issues)}";
                    plan = await _planningService.RefinePlanAsync(plan, feedback, filteredTools);
                    Console.WriteLine("⚠️  Plan refined due to validation issues");
                }
            }
            
            return plan;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create plan, using fallback approach");
            Console.WriteLine("⚠️  Planning failed, using adaptive approach");
            
            // Return a simple fallback plan
            return new TaskPlan
            {
                Task = task,
                Strategy = "Adaptive execution using available tools",
                Steps = new List<PlanStep>
                {
                    new PlanStep
                    {
                        StepNumber = 1,
                        Description = "Execute task using appropriate tools",
                        IsMandatory = true,
                        ExpectedOutput = "Task completion"
                    }
                },
                Complexity = TaskComplexity.Medium,
                Confidence = 0.7
            };
        }
    }

    private void DisplayTaskPlan(TaskPlan plan)
    {
        Console.WriteLine($"\n📋 Execution Plan for: {plan.Task}");
        Console.WriteLine($"Strategy: {plan.Strategy}");
        Console.WriteLine($"Complexity: {plan.Complexity}");
        Console.WriteLine($"Confidence: {plan.Confidence:P0}");
        Console.WriteLine($"Required Tools: {string.Join(", ", plan.RequiredTools)}");
        
        Console.WriteLine("\nExecution Steps:");
        foreach (var step in plan.Steps)
        {
            var mandatory = step.IsMandatory ? "✓" : "○";
            Console.WriteLine($"  {mandatory} Step {step.StepNumber}: {step.Description}");
            if (step.PotentialTools.Count > 0)
            {
                Console.WriteLine($"    Tools: {string.Join(", ", step.PotentialTools)}");
            }
        }
        Console.WriteLine();
    }

    private async Task<OpenAIIntegration.Model.ToolDefinition[]> DiscoverAndSelectToolsForPlanAsync(TaskPlan plan, TaskExecutionRequest request, bool isResumingSession = false)
    {
        var filterConfig = request.ToolFilter ?? _config.ToolFilter;
        
        // Discover all tools from MCP server
        var allTools = await _toolManager.DiscoverToolsAsync(_connectionManager);
        var filteredTools = _toolManager.ApplyFilters(allTools, filterConfig);
        
        Console.WriteLine($"🔧 Discovered {allTools.Count} tools total, {filteredTools.Count} after filtering");
        
        // Prioritize tools mentioned in the plan
        var planTools = plan.RequiredTools.Concat(
            plan.Steps.SelectMany(s => s.PotentialTools)
        ).Distinct().ToList();
        
        var availableToolNames = filteredTools.Select(t => t.Name).ToHashSet();
        var planToolsAvailable = planTools.Where(t => availableToolNames.Contains(t)).ToList();
        var planToolsMissing = planTools.Where(t => !availableToolNames.Contains(t)).ToList();
        
        if (planToolsMissing.Count > 0)
        {
            Console.WriteLine($"⚠️  Plan requires unavailable tools: {string.Join(", ", planToolsMissing)}");
        }
        
        try
        {
            // Select tools based on the plan, ensuring plan-required tools are included
            var maxTools = _config.ToolSelection.MaxToolsPerRequest;
            
            // Start with plan-required tools that are available
            var selectedToolNames = new HashSet<string>(planToolsAvailable);
            
            // If we need more tools, use intelligent selection
            if (selectedToolNames.Count < maxTools)
            {
                var remainingSlots = maxTools - selectedToolNames.Count;
                var additionalTools = await _toolSelector.SelectToolsForTaskAsync(
                    request.Task, 
                    filteredTools.Where(t => !selectedToolNames.Contains(t.Name)).ToList(),
                    remainingSlots);
                
                foreach (var tool in additionalTools)
                {
                    selectedToolNames.Add(tool.Name);
                }
            }
            
            // Convert to tool definitions
            var selectedTools = filteredTools
                .Where(t => selectedToolNames.Contains(t.Name))
                .Select(t => _toolManager.CreateOpenAiToolDefinition(t))
                .ToArray();
            
            Console.WriteLine($"🎯 Selected {selectedTools.Length} tools based on plan: " +
                            $"{string.Join(", ", selectedTools.Take(5).Select(t => t.Name))}" +
                            $"{(selectedTools.Length > 5 ? "..." : "")}");
            
            return selectedTools;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plan-based tool selection failed, falling back to all filtered tools");
            Console.WriteLine($"⚠️  Plan-based tool selection failed, using all {filteredTools.Count} filtered tools");
            return filteredTools.Select(t => _toolManager.CreateOpenAiToolDefinition(t)).ToArray();
        }
    }

    private async Task<bool> InitializeConversationAsync(TaskExecutionRequest request)
    {
        var systemPrompt = request.SystemPrompt ?? """
            You are AgentAlpha, a helpful AI assistant that can perform various tasks using available tools.
            
            Available capabilities include:
            - Mathematical calculations (add, subtract, multiply, divide)
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

    private async Task ExecuteConversationLoopAsync(OpenAIIntegration.Model.ToolDefinition[] availableTools, AgentConfiguration config, TaskPlan? taskPlan = null, CancellationToken cancellationToken = default)
    {
        // Keep track of currently available tools for dynamic expansion
        var currentTools = availableTools.ToList();
        var allAvailableTools = await _toolManager.DiscoverToolsAsync(_connectionManager);
        var filteredAvailableTools = _toolManager.ApplyFilters(allAvailableTools, config.ToolFilter);
        
        // Track plan execution for potential updates
        var planStepsCompleted = new HashSet<int>();
        var iterationsSincePlanUpdate = 0;
        const int maxIterationsBeforePlanReview = 5;
        
        // If we have a plan, provide it as context to the conversation
        if (taskPlan != null)
        {
            var planContext = $"""
                Following execution plan:
                Strategy: {taskPlan.Strategy}
                Steps: {string.Join(", ", taskPlan.Steps.Select(s => $"{s.StepNumber}. {s.Description}"))}
                
                Execute the plan step by step, using the identified tools appropriately.
                """;
            _conversationManager.AddAssistantMessage($"📋 Plan created: {planContext}");
        }
        
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
                            var toolDef = _toolManager.CreateOpenAiToolDefinition(tool);
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
                var taskCompleted = false;       // NEW
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
                    
                    // Track execution progress for plan updates
                    if (taskPlan != null)
                    {
                        var correspondingStep = taskPlan.Steps.FirstOrDefault(s => 
                            s.PotentialTools.Contains(toolCall.Name, StringComparer.OrdinalIgnoreCase));
                        if (correspondingStep != null && !planStepsCompleted.Contains(correspondingStep.StepNumber))
                        {
                            planStepsCompleted.Add(correspondingStep.StepNumber);
                            _logger.LogDebug("Plan step {StepNumber} appears to be completed", correspondingStep.StepNumber);
                        }
                    }
                    
                    // Check for execution issues that might require plan updates
                    if (result.ToString().ToLowerInvariant().Contains("error") || 
                        result.ToString().ToLowerInvariant().Contains("failed"))
                    {
                        executionFeedback.Add($"Tool '{toolCall.Name}' encountered issues: {result}");
                    }
                    // ------------------------------------------------------

                    if (toolCall.Name.Equals("complete_task", StringComparison.OrdinalIgnoreCase))
                        taskCompleted = true;
                }

                _conversationManager.AddToolResults(toolSummaries);
                Console.WriteLine($"🔧 {string.Join("\n", toolSummaries)}");

                // Log conversation statistics for monitoring
                var stats = _conversationManager.GetConversationStatistics();
                _logger.LogDebug("Conversation stats: {TotalMessages} messages ({EstimatedTokens} estimated tokens)", 
                    stats.TotalMessages, stats.EstimatedTokens);

                // Check if plan needs updating based on execution feedback
                if (taskPlan != null && executionFeedback.Count > 0 && iterationsSincePlanUpdate >= maxIterationsBeforePlanReview)
                {
                    await ConsiderPlanUpdateAsync(taskPlan, executionFeedback, filteredAvailableTools);
                    iterationsSincePlanUpdate = 0;
                }
                else
                {
                    iterationsSincePlanUpdate++;
                }

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
            
            iterationsSincePlanUpdate++;
        }

        Console.WriteLine($"⚠️  Reached maximum iterations ({config.MaxIterations}).");
    }

    private async Task<OpenAIIntegration.Model.ToolDefinition[]> GetAdditionalToolsAsync(
        IList<McpClientTool> allAvailableTools, 
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
        if (!string.IsNullOrEmpty(request.SessionId))
        {
            try
            {
                var session = await _sessionManager.GetSessionAsync(request.SessionId);
                if (session != null)
                {
                    // Update the session with current conversation state
                    var currentMessages = _conversationManager.GetCurrentMessages();
                    session.SetConversationMessages(currentMessages);
                    session.Status = SessionStatus.Active; // Keep as active for continued use
                    
                    await _sessionManager.SaveSessionAsync(session);
                    _logger.LogInformation("Saved session state for {SessionId}", request.SessionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save session {SessionId}", request.SessionId);
            }
        }
    }
}