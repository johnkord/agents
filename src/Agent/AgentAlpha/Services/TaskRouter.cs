using AgentAlpha.Interfaces;
using AgentAlpha.Models;
using Microsoft.Extensions.Logging;
using OpenAIIntegration;
using OpenAIIntegration.Model;
using System.Text.Json;
using System.Text.Json.Nodes;
using Common.Interfaces.Session;
using Common.Models.Session;      // +NEW

namespace AgentAlpha.Services;

/// <summary>
/// Routes tasks to either fast-path execution or full ReAct loop based on task analysis
/// </summary>
public class TaskRouter : ITaskRouter
{
    private readonly ISessionAwareOpenAIService _openAiService;
    private readonly ILogger<TaskRouter>        _logger;
    private readonly ISessionActivityLogger     _activityLogger;   // +NEW

    public TaskRouter(
        ISessionAwareOpenAIService openAiService,
        ILogger<TaskRouter> logger,
        ISessionActivityLogger activityLogger)                     // +NEW
    {
        _openAiService   = openAiService ?? throw new ArgumentNullException(nameof(openAiService));
        _logger          = logger         ?? throw new ArgumentNullException(nameof(logger));
        _activityLogger  = activityLogger ?? throw new ArgumentNullException(nameof(activityLogger));
    }

    // NEW – single reusable tool definition
    private static readonly ToolDefinition ClassifyTaskTool = new()
    {
        Type = "function",
        Name = "classify_task",
        Description = "Classify a user task for routing and return confidence score.",
        Parameters = new
        {
            type = "object",
            properties = new
            {
                classification = new
                {
                    type = "string",
                    description = "SIMPLE_TOOL | SIMPLE_QUERY | COMPLEX"
                },
                confidence = new
                {
                    type = "number",
                    description = "Confidence 0-1"
                },
                reasoning = new
                {
                    type = "string",
                    description = "Short rationale"
                }
            },
            required = new[] { "classification", "confidence" }
        }
    };

    /// <summary>
    /// Routes the given task execution request to the appropriate task route (FastPath or ReactLoop)
    /// </summary>
    /// <param name="request">The task execution request containing the task to be routed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A tuple containing the selected task route and the confidence level of the routing decision</returns>
    public async Task<(TaskRoute route, double confidence)> RouteAsync(
        TaskExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.Task))
            throw new ArgumentException("Task cannot be null or empty", nameof(request));

        string? actId = null;

        try
        {
            _logger.LogInformation("Analyzing task for routing: {Task}", request.Task);

            actId = _activityLogger.StartActivity(
                       ActivityTypes.TaskRouting, 
                       "Routing task", 
                       new { request.Task });

            var messages = new[]
            {
                new { role = "system", content = GetSystemPrompt() },
                new { role = "user", content = $"Task: {request.Task}" }
            };

            // --- CHANGED: instruct model to call our tool -----------------
            var openAiRequest = new ResponsesCreateRequest
            {
                Model = "gpt-4.1-nano",
                Input = messages,
                Tools = new[] { ClassifyTaskTool },
                ToolChoice = "auto",
                MaxOutputTokens = 200,
                Temperature = 0.3
            };
            // ---------------------------------------------------------------

            var response = await _openAiService.CreateResponseAsync(
                openAiRequest, cancellationToken);

            // --- NEW: extract tool call result ----------------------------
            var fnCall = response.Output?
                                   .OfType<FunctionToolCall>()
                                   .FirstOrDefault(fc => fc.Name == ClassifyTaskTool.Name);

            if (fnCall != null && fnCall.Arguments.HasValue)
            {
                var rd = ParseRoutingDecision(fnCall.Arguments.Value);

                await _activityLogger.CompleteActivityAsync(       // +NEW
                        actId, new { rd.Classification, rd.Confidence });

                return (MapRoute(rd.Classification), rd.Confidence);
            }
            // ----------------------------------------------------------------

            // Fallback – previous plain-JSON behaviour
            var content = response.Output?
                                .OfType<OutputMessage>()
                                .FirstOrDefault()?.Content?.ToString();

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Empty response from routing LLM, defaulting to ReactLoop");
                return (TaskRoute.ReactLoop, 0.5);
            }

            var legacy = JsonSerializer.Deserialize<RoutingDecision>(
                             content, new JsonSerializerOptions
                             { PropertyNameCaseInsensitive = true });

            if (legacy == null)
                return (TaskRoute.ReactLoop, 0.3);

            await _activityLogger.CompleteActivityAsync(          // +NEW
                    actId, new { legacy.Classification, legacy.Confidence });

            return (MapRoute(legacy.Classification), legacy.Confidence);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during task routing, defaulting to ReactLoop");

            if (actId != null)                                     // +NEW
                await _activityLogger.FailActivityAsync(
                        actId, ex.Message, new { Exception = ex.GetType().Name });

            return (TaskRoute.ReactLoop, 0.0);
        }
    }

    // --- helpers --------------------------------------------------------
    private static RoutingDecision ParseRoutingDecision(JsonElement args)
    {
        // NEW: unwrap when the tool arguments arrive as a JSON-encoded string
        if (args.ValueKind == JsonValueKind.String)
        {
            var json = args.GetString();
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    return ParseRoutingDecision(doc.RootElement);   // recurse with parsed object
                }
                catch (JsonException)
                {
                    // fall through to default handling below
                }
            }
        }

        // Fallback if we still don't have an object
        if (args.ValueKind != JsonValueKind.Object)
            return new RoutingDecision { Classification = "", Confidence = 0.0 };

        var cls = args.GetProperty("classification").GetString() ?? "";
        var conf = args.GetProperty("confidence").GetDouble();
        var reas = args.TryGetProperty("reasoning", out var r) ? r.GetString() : "";
        return new RoutingDecision { Classification = cls, Confidence = conf, Reasoning = reas };
    }

    private static TaskRoute MapRoute(string? classification) => (classification ?? "")
        .ToUpperInvariant() switch
        {
            "SIMPLE_TOOL" => TaskRoute.FastPath,
            "SIMPLE_QUERY" => TaskRoute.FastPath,
            "COMPLEX" => TaskRoute.ReactLoop,
            _ => TaskRoute.ReactLoop
        };

    private string GetSystemPrompt()
    {
        return @"You are a task-routing assistant.
After analysing the task, CALL the function classify_task with the arguments:
classification (SIMPLE_TOOL | SIMPLE_QUERY | COMPLEX),
confidence (0-1), reasoning (short).
Return nothing else.";
    }
}

public class RoutingDecision
{
    public string? Classification { get; set; }
    public double Confidence { get; set; }
    public string? Reasoning { get; set; }
}
