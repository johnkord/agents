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
            var resp = await _openAi.CreateResponseAsync(req);

            var call = resp.Output?
                           .OfType<FunctionToolCall>()
                           .FirstOrDefault(fc => fc.Name == "plan_evaluation");

            if (call is null)
                throw new InvalidOperationException("plan_evaluation call missing");

            var args = call.Arguments ?? default;
            double score = 0;
            string feedback = "";

            if (args.TryGetProperty("score", out var s) && s.TryGetDouble(out var sv))
                score = sv;

            if (args.TryGetProperty("feedback", out var f))
                feedback = f.GetString() ?? "";

            return new EvaluationResult(score, feedback);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Plan evaluation failed, returning default low score");
            return new EvaluationResult(0.0, "Evaluator error – refine structure & clarity");
        }
    }
}
