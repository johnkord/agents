using Microsoft.Extensions.Logging;
using MCPClient;
using AgentAlpha.Configuration;
using AgentAlpha.Interfaces;

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
        _logger.LogInformation("Starting task execution: {Task}", task);

        try
        {
            // Step 1: Connect to MCP Server
            await ConnectToMcpServerAsync();

            // Step 2: Discover and filter tools
            var availableTools = await DiscoverToolsAsync();

            // Step 3: Initialize conversation
            InitializeConversation(task);

            // Step 4: Execute conversation loop
            await ExecuteConversationLoopAsync(availableTools);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task execution failed");
            throw;
        }
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

    private async Task<OpenAIIntegration.Model.ToolDefinition[]> DiscoverToolsAsync()
    {
        var allTools = await _toolManager.DiscoverToolsAsync(_connectionManager);
        var filteredTools = _toolManager.ApplyFilters(allTools, _config.ToolFilter);

        Console.WriteLine($"🔧 Discovered {allTools.Count} tools total, {filteredTools.Count} after filtering: " +
                         $"{string.Join(", ", filteredTools.Take(5).Select(t => t.Name))}" +
                         $"{(filteredTools.Count > 5 ? "..." : "")}");

        if (filteredTools.Count != allTools.Count)
        {
            var excluded = allTools.Where(t => !_config.ToolFilter.ShouldIncludeTool(t.Name)).Select(t => t.Name);
            Console.WriteLine($"🚫 Excluded tools: {string.Join(", ", excluded)}");
        }

        return filteredTools.Select(t => _toolManager.CreateOpenAiToolDefinition(t)).ToArray();
    }

    private void InitializeConversation(string task)
    {
        var systemPrompt = """
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

        _conversationManager.InitializeConversation(systemPrompt, task);
        Console.WriteLine($"📝 Task: {task}");
    }

    private async Task ExecuteConversationLoopAsync(OpenAIIntegration.Model.ToolDefinition[] availableTools)
    {
        for (int i = 0; i < _config.MaxIterations; i++)
        {
            Console.WriteLine($"\n--- Iteration {i + 1} ---");

            // Process one iteration of the conversation
            var response = await _conversationManager.ProcessIterationAsync(availableTools);

            // Handle tool calls if present
            if (response.HasToolCalls)
            {
                var toolSummaries = new List<string>();

                foreach (var toolCall in response.ToolCalls)
                {
                    if (!_config.ToolFilter.ShouldIncludeTool(toolCall.Name))
                    {
                        toolSummaries.Add($"Tool '{toolCall.Name}' call blocked by tool filter configuration.");
                        continue;
                    }

                    var result = await _toolManager.ExecuteToolAsync(_connectionManager, toolCall.Name, toolCall.Arguments);
                    toolSummaries.Add($"Tool '{toolCall.Name}' called with args {toolCall.Arguments}. Result: {result}");
                }

                // Add tool results back to conversation
                _conversationManager.AddToolResults(toolSummaries);
                var summary = string.Join("\n", toolSummaries);
                Console.WriteLine($"🔧 {summary}");
                continue; // go to next iteration
            }

            // Display assistant response
            Console.WriteLine($"AI: {response.AssistantText}");

            // Check if task is complete
            if (_conversationManager.IsTaskComplete(response.AssistantText))
            {
                Console.WriteLine("✅ Task completed!");
                return;
            }

            // Add assistant message to conversation
            _conversationManager.AddAssistantMessage(response.AssistantText);
        }

        Console.WriteLine($"⚠️  Reached maximum iterations ({_config.MaxIterations}).");
    }
}