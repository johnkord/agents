using AgentAlpha.Interfaces;
using Microsoft.Extensions.Logging;
using OpenAIIntegration;
using OpenAIIntegration.Model;
using System.Text.Json;
using AgentAlpha.Models;

namespace AgentAlpha.Services;

public class PlanEvaluator : IPlanEvaluator
{
    private readonly ISessionAwareOpenAIService _openAi;
    private readonly ILogger<PlanEvaluator> _log;

    public PlanEvaluator(ISessionAwareOpenAIService openAi, ILogger<PlanEvaluator> log)
    {
        _openAi = openAi;
        _log    = log;
    }

    public async Task<EvaluationResult> EvaluateAsync(string plan, string task)
    {
        // switched to verbatim-interpolated string to avoid brace issues
        var prompt = $@"You are a strict execution-plan critic. Rate the plan 0-1 and give short feedback as JSON:
{{ ""score"": <0-1>, ""feedback"": ""<max 100 chars>"" }}.
Task:
{task}

Plan:
{plan}
";

        var req = new ResponsesCreateRequest
        {
            Model  = "gpt-4.1-nano",
            Input  = new[] { new { role = "user", content = prompt } },
            MaxOutputTokens = 200,
            Temperature = 0
        };

        try
        {
            var resp = await _openAi.CreateResponseAsync(req);
            var json = resp.Output?.OfType<OutputMessage>()
                              .FirstOrDefault()?.Content?.ToString() ?? "{}";

            using var doc = JsonDocument.Parse(json);
            var score = doc.RootElement.TryGetProperty("score", out var sProp) && sProp.TryGetDouble(out var sVal) ? sVal : 0;
            var fb    = doc.RootElement.TryGetProperty("feedback", out var fProp) ? fProp.GetString() ?? "" : "";
            return new EvaluationResult(score, fb);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Plan evaluation failed, returning default low score");
            return new EvaluationResult(0.0, "Evaluator error – refine structure & clarity");
        }
    }
}
