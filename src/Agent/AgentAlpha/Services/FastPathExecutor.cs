using AgentAlpha.Configuration;
using AgentAlpha.Interfaces;
using AgentAlpha.Models;
using Common.Interfaces.Session;
using Microsoft.Extensions.Logging;
using OpenAIIntegration;
using OpenAIIntegration.Model;
using System.Text.Json;

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

        public FastPathExecutor(
            AgentConfiguration configuration,
            ISessionAwareOpenAIService openAiService,
            ISessionManager sessionManager,
            ISessionActivityLogger activityLogger,
            ILogger<FastPathExecutor> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _openAiService = openAiService ?? throw new ArgumentNullException(nameof(openAiService));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _activityLogger = activityLogger ?? throw new ArgumentNullException(nameof(activityLogger));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ExecuteAsync(TaskExecutionRequest request)
        {
            _logger.LogInformation("Starting fast-path execution for task: {Task}", request.Task);

            var startTime = DateTime.UtcNow;

            try
            {
                // Determine if this is a tool task or LLM task
                var taskAnalysis = await AnalyzeTaskAsync(request);

                if (taskAnalysis.RequiresTool)
                {
                    // Placeholder for tool task execution
                    _logger.LogWarning("Tool execution not implemented, falling back to LLM");
                    await ExecuteLLMTaskAsync(request);
                }
                else
                {
                    await ExecuteLLMTaskAsync(request);
                }

                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation("Fast-path execution completed in {Duration}ms", duration.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fast-path execution failed");
                throw new InvalidOperationException($"Fast-path execution failed: {ex.Message}", ex);
            }
        }

        private Task<TaskAnalysis> AnalyzeTaskAsync(TaskExecutionRequest request)
        {
            // Simple heuristics to determine if task requires a tool
            var taskLower = request.Task.ToLowerInvariant();

            if (taskLower.Contains("time") || taskLower.Contains("date"))
                return Task.FromResult(new TaskAnalysis { RequiresTool = true, ToolName = "get_current_time" });

            if (taskLower.Contains("file") || taskLower.Contains("directory") || taskLower.Contains("folder"))
                return Task.FromResult(new TaskAnalysis { RequiresTool = true, ToolName = "list_files" });

            if (taskLower.Contains("weather"))
                return Task.FromResult(new TaskAnalysis { RequiresTool = true, ToolName = "get_weather" });

            return Task.FromResult(new TaskAnalysis { RequiresTool = false });
        }

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

        private class TaskAnalysis
        {
            public bool RequiresTool { get; set; }
            public string? ToolName { get; set; }
        }

        private enum ActivityTypes
        {
            ToolExecution,
            FastPathResult
        }
    }
}