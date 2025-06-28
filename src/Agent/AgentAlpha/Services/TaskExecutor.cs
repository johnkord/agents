using Microsoft.Extensions.Logging;
using MCPClient;
using ModelContextProtocol.Client;
using AgentAlpha.Configuration;
using AgentAlpha.Interfaces;
using AgentAlpha.Models;
using Common.Interfaces.Session;
using Common.Models.Session;
using System.Text.Json;                 // +NEW
using System.Text;

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
        ITaskStateManager taskStateManager,
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
        _taskStateManager = taskStateManager;
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

            // Step 4: Create task state from plan and execute subtasks sequentially
            if (taskPlan != null && !string.IsNullOrEmpty(request.SessionId))
            {
                await ExecuteSubtasksSequentiallyAsync(taskPlan, request, effectiveConfig, currentSession!);
            }
            else
            {
                // Fallback to original execution for cases without session or plan
                await ExecuteTraditionalFlowAsync(taskPlan, request, effectiveConfig, isResumingSession);
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

    private async Task<IList<IUnifiedTool>> DiscoverAvailableToolsAsync()
    {
        var allTools = await _toolManager.DiscoverAllToolsAsync(_connectionManager);
        return _toolManager.ApplyFiltersToAllTools(allTools, _config.ToolFilter);
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

    private async Task ConsiderPlanUpdateAsync(TaskPlan taskPlan, List<string> executionFeedback, IList<IUnifiedTool> availableTools)
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
            var allTools = await _toolManager.DiscoverAllToolsAsync(_connectionManager);
            var filteredTools = _toolManager.ApplyFiltersToAllTools(allTools, _config.ToolFilter);
            
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

    private async Task<TaskPlan> CreateTaskPlanAsync(string task)
    {
        Console.WriteLine("\n📋 Creating execution plan...");
        
        try
        {
            // Discover all available tools for planning
            var allTools = await _toolManager.DiscoverAllToolsAsync(_connectionManager);
            var filteredTools = _toolManager.ApplyFiltersToAllTools(allTools, _config.ToolFilter);
            
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
        var allTools = await _toolManager.DiscoverAllToolsAsync(_connectionManager);
        var filteredTools = _toolManager.ApplyFiltersToAllTools(allTools, filterConfig);
        
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
                .Select(t => t.ToToolDefinition())
                .ToArray();
            
            Console.WriteLine($"🎯 Selected {selectedTools.Length} tools based on plan: " +
                            $"{string.Join(", ", selectedTools.Select(t => t.Name))}");
            
            return selectedTools;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plan-based tool selection failed, falling back to all filtered tools");
            Console.WriteLine($"⚠️  Plan-based tool selection failed, using all {filteredTools.Count} filtered tools");
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

    private async Task ExecuteConversationLoopAsync(OpenAIIntegration.Model.ToolDefinition[] availableTools, AgentConfiguration config, TaskPlan? taskPlan = null, CancellationToken cancellationToken = default)
    {
        // Keep track of currently available tools for dynamic expansion
        var currentTools = availableTools.ToList();
        var allAvailableTools = await _toolManager.DiscoverAllToolsAsync(_connectionManager);
        var filteredAvailableTools = _toolManager.ApplyFiltersToAllTools(allAvailableTools, config.ToolFilter);
        
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
                    {
                        taskCompleted = true;
                        // Generate comprehensive task completion report
                        await GenerateTaskCompletionReportAsync(toolCall, result.ToString(), taskPlan, planStepsCompleted);
                    }
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

    /// <summary>
    /// Generates a comprehensive task completion report with reasoning and evidence
    /// </summary>
    private async Task GenerateTaskCompletionReportAsync(ToolCall completeTaskCall, string toolResult, TaskPlan? taskPlan, HashSet<int> completedSteps)
    {
        try
        {
            // Extract completion information from the tool call arguments
            var completionArgs = completeTaskCall.Arguments ?? new Dictionary<string, object?>();
            var summary = completionArgs.GetValueOrDefault("summary")?.ToString() ?? "Task completed";
            var reasoning = completionArgs.GetValueOrDefault("reasoning")?.ToString() ?? "";
            var evidence = completionArgs.GetValueOrDefault("evidence")?.ToString() ?? "";
            var deliverables = completionArgs.GetValueOrDefault("deliverables")?.ToString() ?? "";
            var keyActions = completionArgs.GetValueOrDefault("keyActions")?.ToString() ?? "";

            // Gather evidence from session activities
            var sessionActivities = await _activityLogger.GetSessionActivitiesAsync();
            var evidenceFromActivities = GatherEvidenceFromActivities(sessionActivities);

            // Analyze task plan completion
            var planAnalysis = AnalyzeTaskPlanCompletion(taskPlan, completedSteps);
            
            // Extract completion rate for quality calculation
            var completionRate = GetCompletionRateFromPlanAnalysis(planAnalysis);

            // Gather conversation statistics
            var conversationStats = _conversationManager.GetConversationStatistics();

            // Build comprehensive completion report
            var completionReport = new
            {
                TaskCompletion = new
                {
                    Status = "COMPLETED",
                    CompletedAt = DateTime.UtcNow,
                    Summary = summary,
                    ProvidedReasoning = reasoning,
                    ProvidedEvidence = evidence,
                    Deliverables = deliverables,
                    KeyActions = keyActions
                },
                ExecutionEvidence = evidenceFromActivities,
                PlanCompletion = planAnalysis,
                ConversationMetrics = new
                {
                    TotalMessages = conversationStats.TotalMessages,
                    EstimatedTokens = conversationStats.EstimatedTokens,
                    TotalActivities = sessionActivities.Count,
                    SuccessfulActivities = sessionActivities.Count(a => a.Success),
                    FailedActivities = sessionActivities.Count(a => !a.Success)
                },
                TaskCompletionQuality = new
                {
                    HasDetailedReasoning = !string.IsNullOrWhiteSpace(reasoning),
                    HasExplicitEvidence = !string.IsNullOrWhiteSpace(evidence),
                    HasDeliverables = !string.IsNullOrWhiteSpace(deliverables),
                    PlanCompletionRate = completionRate,
                    RecommendationScore = CalculateCompletionQualityScore(reasoning, evidence, deliverables, completionRate)
                }
            };

            // Log the comprehensive completion report
            await _activityLogger.LogActivityAsync(
                ActivityTypes.TaskCompletionEvaluation,
                $"Task completion report - {summary}",
                completionReport);

            _logger.LogInformation("Generated comprehensive task completion report with {ActivityCount} activities analyzed", 
                sessionActivities.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate task completion report");
            
            // Log a basic completion report if detailed generation fails
            await _activityLogger.LogActivityAsync(
                ActivityTypes.TaskCompletionEvaluation,
                "Task completion (basic report due to error)",
                new { 
                    Status = "COMPLETED", 
                    CompletedAt = DateTime.UtcNow,
                    Error = ex.Message,
                    ToolResult = toolResult
                });
        }
    }

    /// <summary>
    /// Gathers evidence from session activities to support task completion
    /// </summary>
    private object GatherEvidenceFromActivities(List<SessionActivity> activities)
    {
        var toolCalls = activities.Where(a => a.ActivityType == ActivityTypes.ToolCall).ToList();
        var toolResults = activities.Where(a => a.ActivityType == ActivityTypes.ToolResult).ToList();
        var errors = activities.Where(a => !a.Success).ToList();

        var evidenceSummary = new
        {
            ToolsUsed = toolCalls.Select(a => new
            {
                Tool = ExtractToolNameFromActivity(a),
                Timestamp = a.Timestamp,
                Success = a.Success,
                Description = a.Description
            }).ToList(),
            
            ResultsGenerated = toolResults.Select(a => new
            {
                Tool = ExtractToolNameFromActivity(a),
                Timestamp = a.Timestamp,
                Success = a.Success,
                ResultSummary = TruncateForEvidence(a.Description, 200)
            }).Take(10).ToList(), // Limit to prevent oversized logs

            OpenAIInteractions = activities.Where(a => 
                a.ActivityType == ActivityTypes.OpenAIRequest || 
                a.ActivityType == ActivityTypes.OpenAIResponse).Count(),

            ErrorsEncountered = errors.Select(a => new
            {
                Type = a.ActivityType,
                Error = a.ErrorMessage,
                Timestamp = a.Timestamp
            }).Take(5).ToList(), // Limit error details

            ExecutionSpan = new
            {
                StartTime = activities.FirstOrDefault()?.Timestamp,
                EndTime = activities.LastOrDefault()?.Timestamp,
                TotalDurationMinutes = activities.Any() ? 
                    (activities.Last().Timestamp - activities.First().Timestamp).TotalMinutes : 0
            },

            KeyMilestones = activities.Where(a => 
                a.ActivityType == ActivityTypes.TaskPlanning ||
                a.ActivityType == ActivityTypes.PlanDetails ||
                a.ActivityType == ActivityTypes.ToolSelectionReasoning).Select(a => new
                {
                    Type = a.ActivityType,
                    Description = TruncateForEvidence(a.Description, 150),
                    Timestamp = a.Timestamp
                }).ToList()
        };

        return evidenceSummary;
    }

    /// <summary>
    /// Analyzes task plan completion status
    /// </summary>
    private object AnalyzeTaskPlanCompletion(TaskPlan? taskPlan, HashSet<int> completedSteps)
    {
        if (taskPlan == null)
        {
            return new
            {
                HasPlan = false,
                CompletionRate = 1.0, // Assume complete if no plan was used
                Message = "Task completed without explicit plan"
            };
        }

        var totalSteps = taskPlan.Steps.Count;
        var completedCount = completedSteps.Count;
        var completionRate = totalSteps > 0 ? (double)completedCount / totalSteps : 1.0;

        return new
        {
            HasPlan = true,
            TotalSteps = totalSteps,
            CompletedSteps = completedCount,
            CompletionRate = Math.Round(completionRate, 2),
            PlanDetails = new
            {
                Strategy = taskPlan.Strategy,
                Complexity = taskPlan.Complexity,
                Confidence = taskPlan.Confidence
            },
            StepCompletion = taskPlan.Steps.Select(step => new
            {
                StepNumber = step.StepNumber,
                Description = TruncateForEvidence(step.Description, 100),
                Completed = completedSteps.Contains(step.StepNumber),
                PotentialTools = step.PotentialTools.Take(3).ToList() // Limit tools listed
            }).ToList()
        };
    }

    /// <summary>
    /// Calculates a quality score for the task completion
    /// </summary>
    private double CalculateCompletionQualityScore(string reasoning, string evidence, string deliverables, double planCompletionRate)
    {
        double score = 0.0;
        
        // Base score for completion
        score += 0.4;
        
        // Bonus for detailed reasoning
        if (!string.IsNullOrWhiteSpace(reasoning) && reasoning.Length > 50)
            score += 0.2;
        
        // Bonus for explicit evidence
        if (!string.IsNullOrWhiteSpace(evidence) && evidence.Length > 30)
            score += 0.15;
            
        // Bonus for deliverables mentioned
        if (!string.IsNullOrWhiteSpace(deliverables))
            score += 0.1;
            
        // Bonus for plan completion rate
        score += planCompletionRate * 0.15;
        
        return Math.Round(Math.Min(score, 1.0), 2);
    }

    /// <summary>
    /// Extracts tool name from activity data
    /// </summary>
    private string ExtractToolNameFromActivity(SessionActivity activity)
    {
        try
        {
            if (string.IsNullOrEmpty(activity.Data))
                return "unknown";

            var data = JsonSerializer.Deserialize<JsonElement>(activity.Data);
            if (data.TryGetProperty("ToolName", out var toolNameElement))
                return toolNameElement.GetString() ?? "unknown";

            return "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    /// <summary>
    /// Extracts completion rate from plan analysis object
    /// </summary>
    private double GetCompletionRateFromPlanAnalysis(object planAnalysis)
    {
        try
        {
            var json = JsonSerializer.Serialize(planAnalysis);
            var element = JsonSerializer.Deserialize<JsonElement>(json);
            if (element.TryGetProperty("CompletionRate", out var rateElement))
                return rateElement.GetDouble();
            return 1.0; // Default to complete if no plan analysis
        }
        catch
        {
            return 1.0; // Default to complete on error
        }
    }

    /// <summary>
    /// Truncates text for evidence summary to prevent oversized logs
    /// </summary>
    private string TruncateForEvidence(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
            return "";
            
        return text.Length <= maxLength ? text : text.Substring(0, maxLength - 3) + "...";
    }

    /// <summary>
    /// Execute subtasks sequentially with context passing
    /// </summary>
    private async Task ExecuteSubtasksSequentiallyAsync(TaskPlan taskPlan, TaskExecutionRequest request, AgentConfiguration config, AgentSession session)
    {
        _logger.LogInformation("Starting sequential subtask execution for task: {Task}", taskPlan.Task);
        
        // Create or get existing task state
        var taskState = await _taskStateManager.GetTaskStateAsync(request.SessionId!) 
            ?? _taskStateManager.CreateTaskState(taskPlan);
        
        // Save initial task state
        await _taskStateManager.SaveTaskStateAsync(request.SessionId!, taskState);
        
        // Log activity for subtask execution start
        await _activityLogger.LogActivityAsync(
            ActivityTypes.TaskPlanning,
            "Starting sequential subtask execution",
            new { 
                TaskDescription = taskPlan.Task,
                TotalSubtasks = taskState.Subtasks.Count,
                CompletedSubtasks = taskState.GetCompletedCount() 
            });
        
        // Show current task state to user
        Console.WriteLine("\n📋 Task State:");
        Console.WriteLine(taskState.ToMarkdown());
        
        // Execute each subtask sequentially
        while (true)
        {
            var currentSubtask = await _taskStateManager.GetCurrentSubtaskAsync(request.SessionId!);
            
            if (currentSubtask == null)
            {
                _logger.LogInformation("All subtasks completed for task: {Task}", taskPlan.Task);
                Console.WriteLine("✅ All subtasks completed!");
                
                // Get current task state for logging
                var finalTaskState = await _taskStateManager.GetTaskStateAsync(request.SessionId!);
                if (finalTaskState != null)
                {
                    await _activityLogger.LogActivityAsync(
                        ActivityTypes.TaskCompletionEvaluation,
                        "All subtasks completed - task finished",
                        new { 
                            TaskDescription = taskPlan.Task,
                            CompletedSubtasks = finalTaskState.Subtasks.Count 
                        });
                }
                
                break;
            }
            
            // Start the current subtask
            await _taskStateManager.StartSubtaskAsync(request.SessionId!, currentSubtask.StepNumber);
            
            Console.WriteLine($"\n🎯 Starting Subtask {currentSubtask.StepNumber}: {currentSubtask.Description}");
            
            // Get fresh task state for subtask execution
            var currentTaskState = await _taskStateManager.GetTaskStateAsync(request.SessionId!);
            if (currentTaskState == null)
            {
                _logger.LogError("Task state became null during execution");
                break;
            }
            
            // Execute the current subtask
            await ExecuteSubtaskAsync(currentSubtask, currentTaskState, request, config);
            
            // Refresh task state after subtask execution
            taskState = await _taskStateManager.GetTaskStateAsync(request.SessionId!);
            
            // Show updated task state
            if (taskState != null)
            {
                Console.WriteLine("\n📋 Updated Task State:");
                Console.WriteLine(taskState.ToMarkdown());
            }
        }
    }
    
    /// <summary>
    /// Execute a single subtask with context from previous subtasks
    /// </summary>
    private async Task ExecuteSubtaskAsync(SubtaskState subtask, TaskState taskState, TaskExecutionRequest request, AgentConfiguration config)
    {
        _logger.LogInformation("Executing subtask {StepNumber}: {Description}", subtask.StepNumber, subtask.Description);
        
        // Get accumulated context from previous subtasks
        var context = await _taskStateManager.GetAccumulatedContextAsync(request.SessionId!);
        
        // Create subtask-specific conversation context
        var subtaskContext = BuildSubtaskContext(subtask, taskState, context);
        
        // Add subtask context to conversation
        _conversationManager.AddAssistantMessage(subtaskContext);
        
        // Discover and select tools relevant to this subtask
        var allTools = await _toolManager.DiscoverAllToolsAsync(_connectionManager);
        var filteredTools = _toolManager.ApplyFiltersToAllTools(allTools, config.ToolFilter);
        
        // Select tools based on the subtask's potential tools
        var relevantTools = filteredTools.Where(t => 
            subtask.PotentialTools.Contains(t.Name, StringComparer.OrdinalIgnoreCase) ||
            IsToolRelevantForSubtask(t.Name, subtask.Description)).ToList();
        
        // Always include the subtask completion tool
        var subtaskCompletionTool = allTools.FirstOrDefault(t => t.Name == "complete_subtask");
        if (subtaskCompletionTool != null && !relevantTools.Contains(subtaskCompletionTool))
        {
            relevantTools.Add(subtaskCompletionTool);
        }
        
        // Also include task state tools
        var taskStateTools = allTools.Where(t => t.Name.StartsWith("get_task_state") || t.Name.StartsWith("update_subtask")).ToList();
        foreach (var tool in taskStateTools)
        {
            if (!relevantTools.Contains(tool))
            {
                relevantTools.Add(tool);
            }
        }
        
        var toolDefinitions = relevantTools.Select(t => t.ToToolDefinition()).ToArray();
        
        await _activityLogger.LogActivityAsync(
            ActivityTypes.ToolSelection,
            $"Selected tools for subtask {subtask.StepNumber}",
            new { 
                SubtaskDescription = subtask.Description,
                SelectedToolCount = toolDefinitions.Length,
                ToolNames = toolDefinitions.Select(t => t.Name).ToArray() 
            });
        
        // Execute subtask-focused conversation loop
        await ExecuteSubtaskConversationLoopAsync(subtask, toolDefinitions, config, request.SessionId!);
    }
    
    /// <summary>
    /// Build context message for a subtask including accumulated context
    /// </summary>
    private string BuildSubtaskContext(SubtaskState subtask, TaskState taskState, Dictionary<string, object> accumulatedContext)
    {
        var context = new StringBuilder();
        
        context.AppendLine($"🎯 **Current Subtask: Step {subtask.StepNumber}**");
        context.AppendLine($"**Description:** {subtask.Description}");
        context.AppendLine();
        
        if (!string.IsNullOrEmpty(subtask.ExpectedInput))
        {
            context.AppendLine($"**Expected Input:** {subtask.ExpectedInput}");
        }
        
        if (!string.IsNullOrEmpty(subtask.ExpectedOutput))
        {
            context.AppendLine($"**Expected Output:** {subtask.ExpectedOutput}");
        }
        
        if (subtask.PotentialTools.Any())
        {
            context.AppendLine($"**Suggested Tools:** {string.Join(", ", subtask.PotentialTools)}");
        }
        
        context.AppendLine();
        
        // Add context from completed subtasks
        if (accumulatedContext.Any())
        {
            context.AppendLine("📚 **Context from Previous Subtasks:**");
            foreach (var ctx in accumulatedContext)
            {
                context.AppendLine($"- {ctx.Key}: {ctx.Value}");
            }
            context.AppendLine();
        }
        
        // Add overall task context
        context.AppendLine($"**Overall Task:** {taskState.Task}");
        context.AppendLine($"**Strategy:** {taskState.Strategy}");
        context.AppendLine($"**Progress:** {taskState.GetCompletedCount()}/{taskState.Subtasks.Count} subtasks completed");
        context.AppendLine();
        
        context.AppendLine("**Instructions:**");
        context.AppendLine("1. Focus only on completing this specific subtask");
        context.AppendLine("2. Use the provided context from previous subtasks to inform your work");
        context.AppendLine("3. When you have completed this subtask, use the 'complete_subtask' tool");
        context.AppendLine("4. Provide a clear summary, evidence, and any context needed for the next subtask");
        
        return context.ToString();
    }
    
    /// <summary>
    /// Execute conversation loop focused on a specific subtask
    /// </summary>
    private async Task ExecuteSubtaskConversationLoopAsync(SubtaskState subtask, OpenAIIntegration.Model.ToolDefinition[] availableTools, AgentConfiguration config, string sessionId)
    {
        _logger.LogInformation("Starting conversation loop for subtask {StepNumber}", subtask.StepNumber);
        
        for (int i = 0; i < config.MaxIterations; i++)
        {
            Console.WriteLine($"\n--- Subtask {subtask.StepNumber} - Iteration {i + 1} ---");

            var response = await _conversationManager.ProcessIterationAsync(availableTools);

            if (response.HasToolCalls)
            {
                var toolSummaries = new List<string>();
                var subtaskCompleted = false;

                foreach (var toolCall in response.ToolCalls)
                {
                    if (!config.ToolFilter.ShouldIncludeTool(toolCall.Name))
                    {
                        toolSummaries.Add($"Tool '{toolCall.Name}' call blocked by tool filter configuration.");
                        continue;
                    }

                    var result = await _toolManager.ExecuteToolAsync(
                        _connectionManager,
                        toolCall.Name,
                        toolCall.Arguments ?? new Dictionary<string, object?>());

                    var argsJson = toolCall.Arguments?.Count > 0
                        ? JsonSerializer.Serialize(toolCall.Arguments)
                        : "{}";
                    toolSummaries.Add($"Tool '{toolCall.Name}' called with args {argsJson}. Result: {result}");

                    // Handle subtask completion
                    if (toolCall.Name.Equals("complete_subtask", StringComparison.OrdinalIgnoreCase))
                    {
                        await HandleSubtaskCompletionAsync(toolCall, result.ToString(), sessionId);
                        subtaskCompleted = true;
                    }
                    else if (toolCall.Name.Equals("update_subtask_notes", StringComparison.OrdinalIgnoreCase))
                    {
                        await HandleSubtaskNotesUpdateAsync(toolCall, sessionId);
                    }
                    else if (toolCall.Name.Equals("get_task_state", StringComparison.OrdinalIgnoreCase))
                    {
                        await HandleTaskStateRequestAsync(sessionId);
                    }
                }

                _conversationManager.AddToolResults(toolSummaries);
                Console.WriteLine($"🔧 {string.Join("\n", toolSummaries)}");

                if (subtaskCompleted)
                {
                    Console.WriteLine($"✅ Subtask {subtask.StepNumber} completed!");
                    return;
                }

                continue;
            }

            // Display assistant response
            Console.WriteLine($"AI: {response.AssistantText}");

            // Check for repetitive responses
            if (_conversationManager.WouldBeRepetitive(response.AssistantText))
            {
                Console.WriteLine("🔄 Detected repetitive responses for subtask - providing guidance");
                
                var guidanceMessage = $"I notice I'm being repetitive. Let me focus on completing subtask {subtask.StepNumber}: {subtask.Description}. I should use the 'complete_subtask' tool when I'm done.";
                _conversationManager.AddAssistantMessage(guidanceMessage);
                
                if (i >= 2)
                {
                    Console.WriteLine($"⚠️ Breaking subtask {subtask.StepNumber} loop due to repetitive responses.");
                    break;
                }
            }
            else
            {
                _conversationManager.AddAssistantMessage(response.AssistantText);
            }
        }

        Console.WriteLine($"⚠️ Subtask {subtask.StepNumber} reached maximum iterations ({config.MaxIterations}).");
    }
    
    /// <summary>
    /// Handle subtask completion tool call
    /// </summary>
    private async Task HandleSubtaskCompletionAsync(ToolCall toolCall, string toolResult, string sessionId)
    {
        try
        {
            var args = toolCall.Arguments ?? new Dictionary<string, object?>();
            var stepNumber = args.GetValueOrDefault("stepNumber")?.ToString();
            var summary = args.GetValueOrDefault("summary")?.ToString();
            var evidence = args.GetValueOrDefault("evidence")?.ToString();
            var context = args.GetValueOrDefault("context")?.ToString();
            
            if (int.TryParse(stepNumber, out int step))
            {
                var contextDict = new Dictionary<string, object>();
                if (!string.IsNullOrEmpty(context))
                {
                    contextDict["CompletionContext"] = context;
                }
                if (!string.IsNullOrEmpty(evidence))
                {
                    contextDict["Evidence"] = evidence;
                }
                
                await _taskStateManager.CompleteSubtaskAsync(sessionId, step, summary ?? "Subtask completed", evidence, contextDict);
                
                await _activityLogger.LogActivityAsync(
                    ActivityTypes.TaskCompletionEvaluation,
                    $"Subtask {step} completed",
                    new {
                        StepNumber = step,
                        Summary = summary,
                        Evidence = evidence,
                        Context = context
                    });
                
                _logger.LogInformation("Subtask {StepNumber} marked as completed", step);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle subtask completion");
        }
    }
    
    /// <summary>
    /// Handle subtask notes update tool call
    /// </summary>
    private async Task HandleSubtaskNotesUpdateAsync(ToolCall toolCall, string sessionId)
    {
        try
        {
            var args = toolCall.Arguments ?? new Dictionary<string, object?>();
            var stepNumber = args.GetValueOrDefault("stepNumber")?.ToString();
            var notes = args.GetValueOrDefault("notes")?.ToString();
            var progressUpdate = args.GetValueOrDefault("progressUpdate")?.ToString();
            
            if (int.TryParse(stepNumber, out int step))
            {
                await _taskStateManager.UpdateSubtaskNotesAsync(sessionId, step, notes, progressUpdate);
                _logger.LogDebug("Updated notes for subtask {StepNumber}", step);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle subtask notes update");
        }
    }
    
    /// <summary>
    /// Handle task state request
    /// </summary>
    private async Task HandleTaskStateRequestAsync(string sessionId)
    {
        try
        {
            var taskState = await _taskStateManager.GetTaskStateAsync(sessionId);
            if (taskState != null)
            {
                var taskStateMarkdown = taskState.ToMarkdown();
                _conversationManager.AddAssistantMessage($"Current Task State:\n\n{taskStateMarkdown}");
                Console.WriteLine("\n📋 Task State Requested:");
                Console.WriteLine(taskStateMarkdown);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle task state request");
        }
    }
    
    /// <summary>
    /// Check if a tool is relevant for a subtask based on its name and description
    /// </summary>
    private bool IsToolRelevantForSubtask(string toolName, string subtaskDescription)
    {
        // Basic heuristic to determine tool relevance
        var toolLower = toolName.ToLowerInvariant();
        var descLower = subtaskDescription.ToLowerInvariant();
        
        // File operations
        if (descLower.Contains("file") || descLower.Contains("read") || descLower.Contains("write"))
        {
            if (toolLower.Contains("file") || toolLower.Contains("read") || toolLower.Contains("write"))
                return true;
        }
        
        // Math operations
        if (descLower.Contains("calculate") || descLower.Contains("math") || descLower.Contains("compute"))
        {
            if (toolLower.Contains("math") || toolLower.Contains("calculate"))
                return true;
        }
        
        // Text operations
        if (descLower.Contains("text") || descLower.Contains("string") || descLower.Contains("search"))
        {
            if (toolLower.Contains("text") || toolLower.Contains("string") || toolLower.Contains("search"))
                return true;
        }
        
        // System operations
        if (descLower.Contains("system") || descLower.Contains("environment") || descLower.Contains("time"))
        {
            if (toolLower.Contains("system") || toolLower.Contains("environment") || toolLower.Contains("time"))
                return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Fallback to traditional execution flow for backward compatibility
    /// </summary>
    private async Task ExecuteTraditionalFlowAsync(TaskPlan? taskPlan, TaskExecutionRequest request, AgentConfiguration config, bool isResumingSession)
    {
        _logger.LogInformation("Using traditional execution flow");
        
        var toolSelectionActivityId = _activityLogger.StartActivity(
            ActivityTypes.ToolSelection,
            "Discovering and selecting tools for traditional execution",
            new { PlanTask = taskPlan?.Task, TaskCreatedAt = taskPlan?.CreatedAt });
        
        try
        {
            var availableTools = await DiscoverAndSelectToolsForPlanAsync(taskPlan!, request, isResumingSession);
            await _activityLogger.CompleteActivityAsync(toolSelectionActivityId, 
                new { SelectedToolCount = availableTools?.Length, ToolNames = availableTools?.Select(t => t.Name).ToArray() });

            if (request.Timeout.HasValue)
            {
                using var cts = new CancellationTokenSource(request.Timeout.Value);
                await ExecuteConversationLoopAsync(availableTools!, config, taskPlan, cts.Token);
            }
            else
            {
                await ExecuteConversationLoopAsync(availableTools!, config, taskPlan);
            }
        }
        catch (Exception ex)
        {
            await _activityLogger.FailActivityAsync(toolSelectionActivityId, ex.Message);
            throw;
        }
    }
}