using AgentAlpha.Configuration;
using AgentAlpha.Interfaces;
using Microsoft.Extensions.Logging;
using OpenAIIntegration;
using OpenAIIntegration.Model;

namespace AgentAlpha.Services;

/// <summary>
/// Prompt-chained planner: Analyse → Outline → Detail.
/// Falls back to PlanningService on error.
/// </summary>
public class ChainedPlanner : IPlanner
{
    private readonly AgentConfiguration _cfg;
    private readonly ISessionAwareOpenAIService _openAi;
    private readonly PlanningService _fallback;
    private readonly ILogger<ChainedPlanner> _log;

    public ChainedPlanner(AgentConfiguration cfg,
                          ISessionAwareOpenAIService openAi,
                          PlanningService fallback,
                          ILogger<ChainedPlanner> log)
    {
        _cfg = cfg; _openAi = openAi; _fallback = fallback; _log = log;
    }

    public async Task<string> CreatePlanAsync(
        string task, IList<string>? availableTools = null, string? sessionId = null)
    {
        try
        {
            var analyse = await CallStageAsync(
                _cfg.ChainedPlanner.AnalyseModel,
                BuildAnalysePrompt(task),
                "Analyse");

            var outline = await CallStageAsync(
                _cfg.ChainedPlanner.OutlineModel,
                BuildOutlinePrompt(analyse),
                "Outline");

            var detail = await CallStageAsync(
                _cfg.ChainedPlanner.DetailModel,
                BuildDetailPrompt(outline, availableTools),
                "Detail");

            return detail;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "ChainedPlanner failed – falling back to single-shot planner");
            return await _fallback.CreatePlanAsync(task, availableTools, sessionId);
        }
    }

    public async Task<string> RefinePlanAsync(
        string existingPlan, string feedback, string? sessionId = null)
        => await _fallback.RefinePlanAsync(existingPlan, feedback, sessionId);

    /* ---------------- helper methods ---------------------------------- */

    private async Task<string> CallStageAsync(
        string model, string prompt, string stage)
    {
        var req = new ResponsesCreateRequest
        {
            Model = model,
            Input = new[] { new { role = "user", content = prompt } },
            MaxOutputTokens = _cfg.ChainedPlanner.MaxTokens,
            Temperature = 0.3
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var resp = await _openAi.CreateResponseAsync(req);
        sw.Stop();

        var txt = resp.Output?.OfType<OutputMessage>().FirstOrDefault()?.Content?.ToString() ?? "";
        _log.LogInformation("Planner stage {Stage} completed in {Ms} ms – {Chars} chars",
            stage, sw.ElapsedMilliseconds, txt.Length);
        return txt;
    }

    private static string BuildAnalysePrompt(string task) => $"""
        Analyse the task and list goals, constraints, unknowns and tool gaps as JSON.
        Task: {task}
        Return JSON with keys: task, goals[], constraints[], unknowns[], tool_gaps[].
        """;

    private static string BuildOutlinePrompt(string analyseJson) => $"""
        Given the analysis JSON below, create a high-level ordered list of steps
        (JSON array of strings).
        Analysis: 
        {analyseJson}
        """;

    private static string BuildDetailPrompt(string outlineJson, IList<string>? tools) => $"""
        Expand each outline step into detailed actionable steps with tool mappings.
        Outline: {outlineJson}
        AvailableTools: {string.Join(",", tools ?? new List<string>())}
        Return JSON array where each element has: step, description, tools[], success.
        """;
}
