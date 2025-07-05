using AgentAlpha.Configuration;
using AgentAlpha.Interfaces;
using AgentAlpha.Models;
using Common.Interfaces.Session;
using MCPClient;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OpenAIIntegration;
using OpenAIIntegration.Model;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AgentAlpha.Services
{
    /// <summary>
    /// Executes simple tasks using fast-path optimizations, bypassing the full ReAct loop
    /// </summary>
    public class FastPathExecutor : IFastPathExecutor
    {
        private readonly AgentConfiguration _configuration;
        private readonly ISessionAwareOpenAIService _openAiService;
        private readonly ISessionManager _sessionManager;
        private readonly ISessionActivityLogger _activityLogger;
        private readonly ILogger<FastPathExecutor> _logger;
        private readonly IConnectionManager _connectionManager;

        public FastPathExecutor(
            AgentConfiguration configuration,
            ISessionAwareOpenAIService openAiService,
            ISessionManager sessionManager,
            ISessionActivityLogger activityLogger,
            IConnectionManager connectionManager,
            ILogger<FastPathExecutor> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _openAiService = openAiService ?? throw new ArgumentNullException(nameof(openAiService));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _activityLogger = activityLogger ?? throw new ArgumentNullException(nameof(activityLogger));
            _connectionManager = connectionManager ??
                                 throw new ArgumentNullException(nameof(connectionManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ExecuteAsync(TaskExecutionRequest request)
        {
            _logger.LogInformation("Starting fast-path execution for task: {Task}", request.Task);

            await _connectionManager.EnsureConnectedAsync();   // ← guarantee connection

            var startTime = DateTime.UtcNow;

            try
            {
                // Determine if this is a tool task or LLM task
                var taskAnalysis = await AnalyzeTaskAsync(request);

                if (taskAnalysis.RequiresTool && taskAnalysis.ToolName != null)
                {
                    var callResult = await _connectionManager.CallToolAsync(
                        taskAnalysis.ToolName,
                        taskAnalysis.Arguments ?? new Dictionary<string, object?>());

                    var output = callResult.IsError
                        ? $"Error: {callResult.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text}"
                        : string.Join("\n", callResult.Content
                                         .OfType<TextContentBlock>()
                                         .Select(t => t.Text));

                    _logger.LogInformation("Tool {Tool} completed. Result length: {Len}",
                        taskAnalysis.ToolName, output.Length);

                    await LogActivityAsync(request, taskAnalysis.ToolName, taskAnalysis.Arguments, output);
                    Console.WriteLine($"\n✅ Fast-path tool result ({taskAnalysis.ToolName}):\n{output}\n");
                }
                else
                {
                    await ExecuteLLMTaskAsync(request);
                }

                // TODO: Take input & output here to the user

                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation("Fast-path execution completed in {Duration}ms", duration.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fast-path execution failed");
                throw new InvalidOperationException($"Fast-path execution failed: {ex.Message}", ex);
            }
        }

        // + helper: convert MCP tool → OpenAI ToolDefinition
        private static ToolDefinition ToToolDefinition(McpClientTool t) => new()
        {
            Type        = "function",
            Name        = t.Name,
            Description = t.Description,
            Parameters  = JsonSerializer.Deserialize<object>(
                              t.ProtocolTool.InputSchema.GetRawText())
        };

        // + helper: robust argument extraction from FunctionToolCall
        private static Dictionary<string, object?> ExtractArguments(JsonElement? args)
        {
            if (args is not { } a) return new();
            if (a.ValueKind == JsonValueKind.Object)
                return JsonSerializer.Deserialize<Dictionary<string, object?>>(a.GetRawText()) ?? new();
            if (a.ValueKind == JsonValueKind.String)
            {
                try
                {
                    using var doc = JsonDocument.Parse(a.GetString() ?? "{}");
                    return JsonSerializer.Deserialize<Dictionary<string, object?>>(doc.RootElement.GetRawText()) ?? new();
                }
                catch { }
            }
            return new();
        }

        // ------- updated AnalyseTaskAsync -------------------------------------
        private async Task<TaskAnalysis> AnalyzeTaskAsync(TaskExecutionRequest request)
        {
            var tools     = await _connectionManager.ListToolsAsync();
            var toolDefs  = tools.Select(ToToolDefinition).ToArray();
            var toolNames = tools.Select(t => t.Name).ToArray();

            var messages = new[]
            {
                new { role = "system", content = """
                    You decide whether the user's task can be fulfilled by calling ONE of the provided tools.
                    If a tool fits, CALL IT with correct arguments.
                    Otherwise answer the user directly without calling any tool.
                    """ },
                new { role = "user", content = $"""
                    Task: {request.Task}
                    """ }
            };

            var llmReq = new ResponsesCreateRequest
            {
                Model           = _configuration.Model,
                Input           = messages,
                Tools           = toolDefs,   // <-- pass real tools
                ToolChoice      = "auto",
                MaxOutputTokens = 300,
                Temperature     = 0.0
            };

            var llmResp = await _openAiService.CreateResponseAsync(llmReq);

            var fnCall = llmResp.Output?
                             .OfType<FunctionToolCall>()
                             .FirstOrDefault();          // any tool call qualifies

            if (fnCall == null || string.IsNullOrWhiteSpace(fnCall.Name))
                return new();                            // fall back to LLM path

            return new TaskAnalysis
            {
                Mode          = "TOOL",
                RequiresTool  = true,
                ToolName      = fnCall.Name,
                Arguments     = ExtractArguments(fnCall.Arguments)
            };
        }
        // ---------------------------------------------------------------------

        private async Task ExecuteLLMTaskAsync(TaskExecutionRequest request)
        {
            _logger.LogInformation("Executing LLM-only task");

            // Create a simple one-shot LLM request
            var messages = new[]
            {
                new { role = "system", content = "You are a helpful AI assistant. Answer the user's question concisely." },
                new { role = "user", content = request.Task }
            };

            var openAiRequest = new ResponsesCreateRequest
            {
                Model = _configuration.Model,
                Input = messages,
                MaxOutputTokens = 500,
                Temperature = 0.7
            };

            var response = await _openAiService.CreateResponseAsync(openAiRequest);

            var content = response.Output?.OfType<OutputMessage>().FirstOrDefault()?.Content?.ToString() ?? "";

            _logger.LogInformation("LLM response received: {Length} characters", content.Length);

            // Log the result
            await LogActivityAsync(request, "LLM", null, content);

            // Display the result
            Console.WriteLine($"\n✅ Fast-path result:\n{content}\n");
        }

        private async Task LogActivityAsync(TaskExecutionRequest request, string toolName,
            Dictionary<string, object?>? toolInput, string result)
        {
            if (string.IsNullOrEmpty(request.SessionId))
                return;

            await _activityLogger.LogActivityAsync(
                request.SessionId,
                "FastPath activity",
                new
                {
                    task = request.Task,
                    tool = toolName,
                    input = toolInput,
                    result,
                    executor = "FastPath"
                }
            );
        }

        // ----------------------- UPDATED helper types ---------------------
        private class TaskAnalysis
        {
            public string Mode { get; set; } = "LLM";
            public bool   RequiresTool { get; set; }      // filled after parsing
            public string? ToolName { get; set; }
            public Dictionary<string, object?> Arguments { get; set; } = new();
        }
        // -----------------------------------------------------------------
    }
}