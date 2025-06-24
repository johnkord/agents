using Microsoft.Extensions.Logging;
using MCPClient;
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
    private readonly IConversationManager _conversationManager;
    private readonly AgentConfiguration _config;
    private readonly ILogger<TaskExecutor> _logger;

    public TaskExecutor(
        IConnectionManager connectionManager,
        IToolManager toolManager,
        IConversationManager conversationManager,
        AgentConfiguration config,
        ILogger<TaskExecutor> logger)
    {
        _connectionManager = connectionManager;
        _toolManager = toolManager;
        _conversationManager = conversationManager;
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

            // Step 2: Discover and filter tools
            var availableTools = await DiscoverToolsAsync(request.ToolFilter ?? effectiveConfig.ToolFilter);

            // Step 3: Initialize conversation
            InitializeConversation(request);

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

    private async Task<OpenAIIntegration.Model.ToolDefinition[]> DiscoverToolsAsync(ToolFilterConfig? toolFilter = null)
    {
        var filterConfig = toolFilter ?? _config.ToolFilter;
        var allTools = await _toolManager.DiscoverToolsAsync(_connectionManager);
        var filteredTools = _toolManager.ApplyFilters(allTools, filterConfig);

        Console.WriteLine($"🔧 Discovered {allTools.Count} tools total, {filteredTools.Count} after filtering: " +
                         $"{string.Join(", ", filteredTools.Take(5).Select(t => t.Name))}" +
                         $"{(filteredTools.Count > 5 ? "..." : "")}");

        if (filteredTools.Count != allTools.Count)
        {
            var excluded = allTools.Where(t => !filterConfig.ShouldIncludeTool(t.Name)).Select(t => t.Name);
            Console.WriteLine($"🚫 Excluded tools: {string.Join(", ", excluded)}");
        }

        return filteredTools.Select(t => _toolManager.CreateOpenAiToolDefinition(t)).ToArray();
    }

    private void InitializeConversation(TaskExecutionRequest request)
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

        _conversationManager.InitializeConversation(systemPrompt, request.Task);
        Console.WriteLine($"📝 Task: {request.Task}");
        
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
        for (int i = 0; i < config.MaxIterations; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            Console.WriteLine($"\n--- Iteration {i + 1} ---");

            // Process one iteration of the conversation
            var response = await _conversationManager.ProcessIterationAsync(availableTools);

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
}