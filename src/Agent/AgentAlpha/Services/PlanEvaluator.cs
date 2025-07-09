using AgentAlpha.Configuration;   // +cfg
using AgentAlpha.Interfaces;
using Microsoft.Extensions.Logging;
using OpenAIIntegration;
using OpenAIIntegration.Model;
using System.Text.Json;
using AgentAlpha.Models;
using System.Linq;                           // +LINQ for FirstOrDefault

namespace AgentAlpha.Services;

public class PlanEvaluator : IPlanEvaluator
{
    private readonly ISessionAwareOpenAIService _openAi;
    private readonly ILogger<PlanEvaluator>     _log;
    private readonly AgentConfiguration         _cfg;

    public PlanEvaluator(ISessionAwareOpenAIService openAi,
                         ILogger<PlanEvaluator> log,
                         AgentConfiguration cfg)
    {
        _openAi = openAi;
        _log    = log;
        _cfg    = cfg;
    }

    /* NEW – tool schema for structured evaluation results */
    private static readonly ToolDefinition PlanEvaluationTool = new()
    {
        Type = "function",
        Name = "plan_evaluation",
        Description = "Return a numeric quality score (0-1) and short feedback for the execution plan.",
        Parameters = new
        {
            type = "object",
            properties = new
            {
                score    = new { type = "number", description = "Quality score between 0 and 1" },
                feedback = new { type = "string", description = "Max 100-char feedback" }
            },
            required = new[] { "score", "feedback" }
        }
    };

    public async Task<EvaluationResult> EvaluateAsync(string plan, string task)
    {
        // Resolve model: PlanningModel → Model → "gpt-4.1"
        var model = _cfg.PlanningModel ?? _cfg.Model ?? "gpt-4.1";

        var prompt = $""" 
            Critically evaluate the execution plan for the given task.
            After thinking, call the plan_evaluation tool with your numeric score (0-1) and concise feedback.
            Task:
            {task}

            Plan:
            {plan}
            """;

        var req = new ResponsesCreateRequest
        {
            Model  = model,
            Input  = new[] { new { role = "user", content = prompt } },
            MaxOutputTokens = 200,
            Temperature     = 0,
            Tools           = new[] { PlanEvaluationTool },
            ToolChoice      = "required"
        };

        try
        {
            var resp  = await _openAi.CreateResponseAsync(req);

            // --- NEW: extract tool call result ---------------------------------
            var fnCall = resp.Output?
                            .OfType<FunctionToolCall>()
                            .FirstOrDefault(fc => fc.Name == PlanEvaluationTool.Name);

            if (fnCall != null && fnCall.Arguments.HasValue)
            {
                var args = ExtractArguments(fnCall.Arguments);
                var score    = args.TryGetValue("score", out var s) && s is double d ? d : 0.0;
                var feedback = args.TryGetValue("feedback", out var f) ? f?.ToString() ?? "" : "";
                return new EvaluationResult(score, feedback);
            }
            // --------------------------------------------------------------------

            // Fallback – legacy plain-JSON message -------------------------------
            var content = resp.Output?
                              .OfType<OutputMessage>()
                              .FirstOrDefault()?.Content?.ToString();

            if (string.IsNullOrWhiteSpace(content))
            {
                _log.LogWarning("Empty evaluator response, returning default low score");
                return new EvaluationResult(0.0, "Empty evaluator response");
            }

            double fallbackScore     = 0.0;
            string fallbackFeedback  = "";

            try
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                if (root.TryGetProperty("score", out var se) && se.ValueKind == JsonValueKind.Number)
                    fallbackScore = se.GetDouble();
                if (root.TryGetProperty("feedback", out var fe) && fe.ValueKind == JsonValueKind.String)
                    fallbackFeedback = fe.GetString() ?? "";
            }
            catch (JsonException)
            {
                // Could not parse JSON; treat raw content as feedback
                fallbackFeedback = content.Length > 100 ? content[..100] : content;
            }

            return new EvaluationResult(fallbackScore, fallbackFeedback);
            // --------------------------------------------------------------------
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Plan evaluation failed, returning default low score");
            return new EvaluationResult(0.0, "Evaluator error – refine structure & clarity");
        }
    }

    /* ------------------------------------------------------------------ */
    // NEW – unified argument extraction (mirrors ConversationManager logic)
    private static Dictionary<string, object?> ExtractArguments(JsonElement? arguments)
    {
        if (arguments == null) return new();

        var elem = arguments.Value;

        if (elem.ValueKind == JsonValueKind.Object)
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(elem.GetRawText()) 
                   ?? new();
        }

        if (elem.ValueKind == JsonValueKind.String)
        {
            var raw = elem.GetString();
            if (string.IsNullOrWhiteSpace(raw)) return new();

            // If the string itself contains JSON, try to parse it
            if (raw.TrimStart().StartsWith("{"))
            {
                try
                {
                    using var doc = JsonDocument.Parse(raw);
                    return JsonSerializer.Deserialize<Dictionary<string, object?>>(doc.RootElement.GetRawText()) 
                           ?? new();
                }
                catch
                {
                    // fall through
                }
            }
        }

        return new();
    }
}
