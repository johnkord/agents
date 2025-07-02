using Microsoft.Extensions.Logging;
using AgentAlpha.Configuration;
using AgentAlpha.Interfaces;
using AgentAlpha.Models;
using Common.Interfaces.Session;
using MCPClient;

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

    public SimpleTaskExecutor(
        IConnectionManager connectionManager,
        SimpleToolManager toolManager,
        IConversationManager conversationManager,
        ISessionManager sessionManager,
        ISessionActivityLogger activityLogger,
        AgentConfiguration config,
        ILogger<SimpleTaskExecutor> logger)
    {
        _connectionManager = connectionManager;
        _toolManager = toolManager;
        _conversationManager = conversationManager;
        _sessionManager = sessionManager;
        _activityLogger = activityLogger;
        _config = config;
        _logger = logger;

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
        var sessionName = !string.IsNullOrEmpty(request.SessionName) ? request.SessionName : 
                         !string.IsNullOrEmpty(request.SessionId) ? $"session-{request.SessionId}" : 
                         "simple-agent-session";

        try
        {
            // Connect to MCP server
            await ConnectToMcpServerAsync();

            // Get or create session
            var session = await GetOrCreateSessionAsync(request, sessionName);
            
            // Set up conversation
            await SetupConversationAsync(session, request);

            // Execute main conversation loop
            await ExecuteConversationLoopAsync(request.Task);

            _logger.LogInformation("Task completed successfully");
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
        var transport = GetMcpTransportType();
        if (transport == McpTransportType.Http)
        {
            var url = Environment.GetEnvironmentVariable("MCP_SERVER_URL") ?? "http://localhost:3000";
            await _connectionManager.ConnectAsync(McpTransportType.Http, "MCP Server", serverUrl: url);
        }
        else
        {
            await _connectionManager.ConnectAsync(
                McpTransportType.Stdio,
                "MCP Server",
                command: "dotnet",
                args: ["run", "--project", "../../MCPServer/MCPServer.csproj"]);
        }

        _logger.LogInformation("Connected to MCP server using {Transport}", transport);
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

    private async Task ExecuteConversationLoopAsync(string task)
    {
        const int maxIterations = 10;
        var iteration = 0;

        // Get available tools and select relevant ones
        var availableTools = await _toolManager.DiscoverToolsAsync(_connectionManager);
        var filteredTools = _toolManager.ApplyFilters(availableTools, _config.ToolFilter);
        var selectedTools = await _toolManager.SelectToolsForTaskAsync(task, filteredTools);

        _logger.LogInformation("Starting conversation loop with {ToolCount} tools", selectedTools.Length);

        while (iteration < maxIterations)
        {
            iteration++;
            _logger.LogDebug("Conversation iteration {Iteration}", iteration);

            // Process one iteration
            var response = await _conversationManager.ProcessIterationAsync(selectedTools);

            if (!response.HasToolCalls)
            {
                // No tool calls - check if task is complete
                if (_conversationManager.IsTaskComplete(response.AssistantText))
                {
                    _logger.LogInformation("Task completed after {Iterations} iterations", iteration);
                    break;
                }
                else
                {
                    _logger.LogInformation("Assistant responded without tool calls: {Response}", response.AssistantText);
                    break;
                }
            }

            // Execute tool calls
            var toolResults = new List<string>();
            foreach (var toolCall in response.ToolCalls)
            {
                try
                {
                    var result = await _toolManager.ExecuteToolAsync(_connectionManager, toolCall.Name, toolCall.Arguments);
                    toolResults.Add($"[{toolCall.Name}] {result}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Tool execution failed for {ToolName}", toolCall.Name);
                    toolResults.Add($"[{toolCall.Name}] Error: {ex.Message}");
                }
            }

            // Add tool results to conversation
            _conversationManager.AddToolResults(toolResults);
        }

        if (iteration >= maxIterations)
        {
            _logger.LogWarning("Conversation loop reached maximum iterations ({MaxIterations})", maxIterations);
        }
    }

    private string CreateSystemPrompt()
    {
        return """
            You are a helpful AI assistant that can use various tools to complete tasks.
            
            When given a task:
            1. Think about what tools you might need
            2. Use the available tools to gather information or perform actions
            3. Provide clear updates on your progress
            4. When the task is complete, call the task_complete tool if available, or clearly state that the task is finished
            
            Be concise but informative in your responses.
            """;
    }

    private McpTransportType GetMcpTransportType()
    {
        return (Environment.GetEnvironmentVariable("MCP_TRANSPORT") ?? "stdio").ToLowerInvariant() switch
        {
            "http" or "sse" => McpTransportType.Http,
            _ => McpTransportType.Stdio
        };
    }
}