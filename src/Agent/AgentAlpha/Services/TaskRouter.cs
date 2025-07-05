using AgentAlpha.Interfaces;
using AgentAlpha.Models;
using Microsoft.Extensions.Logging;
using OpenAIIntegration;
using OpenAIIntegration.Model;
using System.Text.Json;

namespace AgentAlpha.Services;

/// <summary>
/// Routes tasks to either fast-path execution or full ReAct loop based on task analysis
/// </summary>
public class TaskRouter : ITaskRouter
{
    private readonly ISessionAwareOpenAIService _openAiService;
    private readonly ILogger<TaskRouter> _logger;

    public TaskRouter(ISessionAwareOpenAIService openAiService, ILogger<TaskRouter> logger)
    {
        _openAiService = openAiService ?? throw new ArgumentNullException(nameof(openAiService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<(TaskRoute route, double confidence)> RouteAsync(
        TaskExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.Task))
            throw new ArgumentException("Task cannot be null or empty", nameof(request));

        try
        {
            _logger.LogInformation("Analyzing task for routing: {Task}", request.Task);

            // Use a lightweight LLM call to classify the task
            var messages = new[]
            {
                new { role = "system", content = GetSystemPrompt() },
                new { role = "user", content = $"Task: {request.Task}" }
            };

            var openAiRequest = new ResponsesCreateRequest
            {
                Model = "gpt-4.1-nano", // Lightweight model for routing
                Input = messages,
                MaxOutputTokens = 200,
                Temperature = 0.3
            };

            var response = await _openAiService.CreateResponseAsync(openAiRequest, cancellationToken);
            var content = response.Output?.OfType<OutputMessage>().FirstOrDefault()?.Content?.ToString();

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Empty response from routing LLM, defaulting to ReactLoop");
                return (TaskRoute.ReactLoop, 0.5);
            }

            // Parse the JSON response
            try
            {
                var routingDecision = JsonSerializer.Deserialize<RoutingDecision>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (routingDecision is null)
                {
                    _logger.LogWarning("Failed to deserialize routing decision, defaulting to ReactLoop");
                    return (TaskRoute.ReactLoop, 0.5);
                }

                // Apply confidence threshold
                if (routingDecision.Confidence < 0.7)
                {
                    _logger.LogInformation("Low confidence ({Confidence}), routing to ReactLoop",
                        routingDecision.Confidence);
                    return (TaskRoute.ReactLoop, routingDecision.Confidence);
                }

                // Map classification to route
                var route = routingDecision.Classification?.ToUpperInvariant() switch
                {
                    "SIMPLE_TOOL" => TaskRoute.FastPath,
                    "SIMPLE_QUERY" => TaskRoute.FastPath,
                    "COMPLEX" => TaskRoute.ReactLoop,
                    _ => TaskRoute.ReactLoop
                };

                _logger.LogInformation("Task routed to {Route} with confidence {Confidence}",
                    route, routingDecision.Confidence);

                return (route, routingDecision.Confidence);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse routing response JSON");
                return (TaskRoute.ReactLoop, 0.3);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during task routing, defaulting to ReactLoop");
            return (TaskRoute.ReactLoop, 0.0);
        }
    }

    private string GetSystemPrompt()
    {
        return @"You are a task routing assistant. Classify tasks into categories for routing:
1. SIMPLE_TOOL: Single tool call can complete this (e.g., 'what time is it?', 'list files')
2. SIMPLE_QUERY: Single LLM response needed, no tools (e.g., 'explain X', 'what is Y?')
3. COMPLEX: Requires multiple steps, planning, or tool orchestration

Respond with JSON:
{
  ""classification"": ""SIMPLE_TOOL"" | ""SIMPLE_QUERY"" | ""COMPLEX"",
  ""confidence"": 0.0-1.0,
  ""reasoning"": ""brief explanation""
}";
    }
}

public class RoutingDecision
{
    public string? Classification { get; set; }
    public double Confidence { get; set; }
    public string? Reasoning { get; set; }
}
