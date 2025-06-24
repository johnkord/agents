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
    private readonly AgentConfiguration _config;
    private readonly ILogger<TaskExecutor> _logger;

    public TaskExecutor(
        IConnectionManager connectionManager,
        IToolManager toolManager,
        IToolSelector toolSelector,
        IConversationManager conversationManager,
        ISessionManager sessionManager,
        AgentConfiguration config,
        ILogger<TaskExecutor> logger)
    {
        _connectionManager = connectionManager;
        _toolManager = toolManager;
        _toolSelector = toolSelector;
        _conversationManager = conversationManager;
        _sessionManager = sessionManager;
        _config = config;
        _logger = logger;
    }

    public async Task ExecuteAsync(string task)
    {
        // Maintain backwards compatibility by creating a simple request
        var request = TaskExecutionRequest.FromTask(task);
        await ExecuteAsync(request);
    }

    public async Task ExecuteAsync(TaskExecutionRequest request)
    {
        _logger.LogInformation("Starting task execution: {Task}", request.Task);

        // Apply request-specific configuration overrides
        var effectiveConfig = ApplyRequestOverrides(request);
        
        try
        {
            // Step 1: Connect to MCP Server
            await ConnectToMcpServerAsync();

            // Step 2: Discover tools and select relevant ones for the task
            var availableTools = await DiscoverAndSelectToolsAsync(request);

            // Step 3: Initialize conversation
            await InitializeConversationAsync(request);

            // Step 4: Execute conversation loop with timeout if specified
            if (request.Timeout.HasValue)
            {
                using var cts = new CancellationTokenSource(request.Timeout.Value);
                await ExecuteConversationLoopAsync(availableTools, effectiveConfig, cts.Token);
            }
            else
            {
                await ExecuteConversationLoopAsync(availableTools, effectiveConfig);
            }
            
            // Step 5: Save session if applicable
            await SaveSessionIfApplicableAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task execution failed");
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

    private async Task<OpenAIIntegration.Model.ToolDefinition[]> DiscoverAndSelectToolsAsync(TaskExecutionRequest request)
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
            
            Console.WriteLine($"🎯 Selected {selectedTools.Length} relevant tools: " +
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

    private async Task InitializeConversationAsync(TaskExecutionRequest request)
    {
        var systemPrompt = request.SystemPrompt ?? """
            You are AgentAlpha, a helpful AI assistant that can perform various tasks using available tools.
            
            Available capabilities include:
            - Mathematical calculations (add, subtract, multiply, divide)
            - File operations (read, write, list directories, file information)
            - Text processing (search, replace, format, word count, split text)
            - System information (current time, environment variables, system details)
            
            When given a task:
            1. Break it down into steps if needed
            2. Use appropriate tools to accomplish each step
            3. Provide clear feedback on what you're doing
            4. Explain the results and next steps
            
            Always use tools when possible rather than trying to do calculations or file operations yourself.
            If you're unsure about a tool's parameters, start with simpler operations and build up.
            """;

        // Handle session-based initialization
        if (!string.IsNullOrEmpty(request.SessionId))
        {
            // Load existing session
            var session = await _sessionManager.GetSessionAsync(request.SessionId);
            if (session != null)
            {
                _conversationManager.InitializeFromSession(session, request.Task);
                Console.WriteLine($"🔄 Resuming session: {session.Name} ({session.SessionId})");
                Console.WriteLine($"📝 New task: {request.Task}");
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
    }

    private async Task ExecuteConversationLoopAsync(OpenAIIntegration.Model.ToolDefinition[] availableTools, AgentConfiguration config, CancellationToken cancellationToken = default)
    {
        // Keep track of currently available tools for dynamic expansion
        var currentTools = availableTools.ToList();
        var allAvailableTools = await _toolManager.DiscoverToolsAsync(_connectionManager);
        var filteredAvailableTools = _toolManager.ApplyFilters(allAvailableTools, config.ToolFilter);
        
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
                        toolCall.Arguments ?? new Dictionary<string, object?>()); // ← warning fixed

                    // --- changed: pretty-print arguments ------------------
                    var argsJson = toolCall.Arguments?.Count > 0
                        ? JsonSerializer.Serialize(toolCall.Arguments)
                        : "{}";
                    toolSummaries.Add(
                        $"Tool '{toolCall.Name}' called with args {argsJson}. Result: {result}");
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

            // Add assistant message to conversation for next iteration
            _conversationManager.AddAssistantMessage(response.AssistantText);
            _logger.LogDebug("Added assistant response to conversation for iteration {Iteration}", i + 1);
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