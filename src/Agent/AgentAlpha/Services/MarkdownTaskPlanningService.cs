using Microsoft.Extensions.Logging;
using System.Text.Json;
using ModelContextProtocol.Client;
using OpenAIIntegration;
using OpenAIIntegration.Model;
using AgentAlpha.Models;
using AgentAlpha.Configuration;
using AgentAlpha.Interfaces;
using Common.Models.Session;
using Common.Interfaces.Session;
using System.Text;
using System.Text.RegularExpressions;
using Common.Interfaces.Tools;

namespace AgentAlpha.Services;

/// <summary>
/// Wrapper around IMarkdownTaskStateManager that adds planning functionality
/// This replaces PlanningService by extending MarkdownTaskStateManager with planning capabilities
/// </summary>
public class MarkdownTaskPlanningService
{
    private readonly IMarkdownTaskStateManager _markdownManager;
    private readonly ISessionAwareOpenAIService _openAi;
    private readonly ILogger<MarkdownTaskPlanningService> _logger;
    private readonly AgentConfiguration _config;
    private readonly IToolScopeManager _toolScope;
    private ISessionActivityLogger? _activityLogger;

    private string[] _lastRequiredTools = Array.Empty<string>();
    public string[] LastRequiredTools => _lastRequiredTools;

    public MarkdownTaskPlanningService(
        IMarkdownTaskStateManager markdownManager,
        ISessionAwareOpenAIService openAi,
        ILogger<MarkdownTaskPlanningService> logger,
        AgentConfiguration config,
        IToolScopeManager toolScope)
    {
        _markdownManager = markdownManager;
        _openAi = openAi;
        _logger = logger;
        _config = config;
        _toolScope = toolScope;
    }

    /// <summary>
    /// Set the session activity logger for automatic OpenAI request logging
    /// </summary>
    public void SetActivityLogger(ISessionActivityLogger? activityLogger)
    {
        _activityLogger = activityLogger;
        _openAi.SetActivityLogger(activityLogger);
        _markdownManager.SetActivityLogger(activityLogger);
        _logger.LogDebug("Activity logger {Status} for MarkdownTaskPlanningService", 
            activityLogger != null ? "set" : "cleared");
    }

    /// <summary>
    /// Initialize task planning directly into markdown format
    /// </summary>
    public async Task<string> InitializeTaskPlanningAsync(
        string sessionId,
        string task,
        IList<IUnifiedTool> availableTools)
    {
        _logger.LogInformation("Initializing task planning in markdown format for session {SessionId}: {Task}", sessionId, task);

        try
        {
            // Create a basic current state if none provided
            var basicState = new CurrentState
            {
                CapturedAt = DateTime.UtcNow
            };

            return await InitializeTaskPlanningWithStateAsync(
                sessionId,
                task,
                availableTools,
                basicState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize task planning for session {SessionId}", sessionId);
            _lastRequiredTools = Array.Empty<string>();
            return CreateFallbackMarkdownPlan(task, availableTools);
        }
    }

    /// <summary>
    /// Initialize task planning with current state analysis directly into markdown format
    /// </summary>
    public async Task<string> InitializeTaskPlanningWithStateAsync(
        string sessionId,
        string task,
        IList<IUnifiedTool> availableTools,
        CurrentState currentState)
    {
        _logger.LogInformation("Initializing state-aware task planning in markdown format for session {SessionId}: {Task}", sessionId, task);

        try
        {
            // Create markdown-based planning prompt
            var markdownPlanPrompt = CreateMarkdownPlanningPrompt(
                task,
                availableTools,
                currentState);
            
            var request = new ResponsesCreateRequest
            {
                Model  = _config.Model,
                Input  = new[]
                {
                    new { role = "system", content = "You are an expert task planning assistant." },
                    new { role = "user",   content = markdownPlanPrompt }
                },
                Tools       = new[] { SaveMarkdownPlanTool },
                ToolChoice  = "auto",
                MaxOutputTokens = 2000
            };

            // Log the planning activity
            if (_activityLogger != null)
            {
                await _activityLogger.LogActivityAsync(
                    ActivityTypes.TaskPlanning,
                    $"Creating markdown task plan for: {task}",
                    new { SessionId = sessionId, Task = task, AvailableToolsCount = availableTools.Count }
                );
            }

            var response = await _openAi.CreateResponseAsync(request);

            // Look for the function call output
            var fnCall = response.Output?
                             .OfType<FunctionToolCall>()
                             .FirstOrDefault(fc => fc.Name == SaveMarkdownPlanTool.Name);

            var (markdownPlan, requiredTools) = ParseFunctionCall(fnCall);

            _toolScope.SetRequiredTools(sessionId, requiredTools);
            _lastRequiredTools = requiredTools;
            
            if (!string.IsNullOrEmpty(markdownPlan))
            {
                if (_activityLogger != null)
                {
                    await _activityLogger!.LogActivityAsync(
                        ActivityTypes.TaskMarkdownUpdate,
                        "Initial markdown task plan",
                        markdownPlan);

                    await _activityLogger!.LogActivityAsync(
                        ActivityTypes.TaskPlanning,
                        "Required tools extracted",
                        requiredTools);
                }

                // Use MarkdownTaskStateManager to save the plan 
                // We can't call InitializeTaskMarkdownAsync because it would generate a different plan
                // So we need to save directly to session - but MarkdownTaskStateManager doesn't expose that
                // Let's use reflection or create a custom approach
                await SaveMarkdownDirectlyToSession(sessionId, markdownPlan);

                _logger.LogInformation("Successfully created markdown task plan for session {SessionId}", sessionId);
                return markdownPlan;
            }
            else
            {
                _toolScope.Clear(sessionId);
                _lastRequiredTools = Array.Empty<string>();
                _logger.LogWarning("Failed to get valid markdown plan response for session {SessionId}", sessionId);
                var fallback = CreateFallbackMarkdownPlan(task, availableTools);
                await SaveMarkdownDirectlyToSession(sessionId, fallback);
                return fallback;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize state-aware task planning for session {SessionId}", sessionId);
            var fallback = CreateFallbackMarkdownPlan(task, availableTools);

            // Still log the fallback plan
            if (_activityLogger != null)
            {
                await _activityLogger.LogActivityAsync(
                    ActivityTypes.TaskPlanning,
                    "Fallback markdown plan created",
                    new { SessionId = sessionId, Task = task, MarkdownPlan = fallback });

                // Log the raw fallback markdown plan
                await _activityLogger!.LogActivityAsync(
                    ActivityTypes.TaskMarkdownUpdate,
                    "Initial markdown task plan (fallback)",
                    fallback);
            }

            _toolScope.Clear(sessionId);
            _lastRequiredTools = Array.Empty<string>();

            await SaveMarkdownDirectlyToSession(sessionId, fallback);
            return fallback;
        }
    }

    // Temporary approach - we'll need to save the markdown directly
    // Since we can't easily integrate with MarkdownTaskStateManager's private SaveTaskMarkdownToSessionAsync
    // We'll create a new temporary plan and then replace it
    private async Task SaveMarkdownDirectlyToSession(string sessionId, string markdown)
    {
        // Initialize with a temporary task first
        await _markdownManager.InitializeTaskMarkdownAsync(sessionId, "Temporary task for planning initialization");
        
        // Then update it with our planned markdown
        await _markdownManager.UpdateTaskMarkdownAsync(sessionId, "Planning completed", markdown, "Replaced with LLM-generated plan");
    }

    // Single tool definition that the LLM will call
    private static readonly ToolDefinition SaveMarkdownPlanTool = new()
    {
        Type        = "function",
        Name        = "save_markdown_plan",
        Description = "Return the generated markdown plan and the list of required tools.",
        Parameters  = new
        {
            type       = "object",
            properties = new
            {
                markdown_plan  = new { type = "string", description = "The markdown task plan." },
                required_tools = new
                {
                    type  = "array",
                    items = new { type = "string" },
                    description = "Names of tools required for executing the plan."
                }
            },
            required = new[] { "markdown_plan", "required_tools" }
        }
    };

    /// <summary>
    /// Creates a markdown planning prompt for LLM task planning
    /// </summary>
    private string CreateMarkdownPlanningPrompt(
        string task,
        IList<IUnifiedTool> availableTools,
        CurrentState currentState)
    {
        var prompt = new List<string>
        {
            $"*Prompt generated on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC*",
            "",
            "Create a comprehensive markdown-based execution plan for the following task:",
            $"\n**TASK:** {task}\n"
        };
        
        // Add state analysis
        var stateAnalysis = AnalyzeCurrentState(currentState, availableTools);
        if (!string.IsNullOrEmpty(stateAnalysis))
        {
            prompt.Add("**CURRENT STATE ANALYSIS:**");
            prompt.Add(stateAnalysis);
            prompt.Add("");
        }
        
        // Add available tools
        prompt.Add("**AVAILABLE TOOLS:**");
        foreach (var tool in availableTools)
        {
            prompt.Add($"- {tool.Name}: {tool.Description}");
        }
        prompt.Add("");
        
        prompt.Add("**INSTRUCTIONS:**");
        prompt.Add("Create a markdown document with the following structure:");
        prompt.Add("1. Task Overview - Brief summary of what needs to be accomplished");
        prompt.Add("2. Strategy - High-level approach to complete the task");
        prompt.Add("3. Execution Steps - Numbered checklist with specific, actionable items");
        prompt.Add("4. Required Tools - Full list of tools needed for execution");
        prompt.Add("5. Success Criteria - How to determine if the task is complete");
        prompt.Add("");
        prompt.Add("Format the execution steps as a numbered checklist using markdown checkboxes:");
        prompt.Add("- [ ] Step description");
        prompt.Add("");
        prompt.Add("Each step should be:");
        prompt.Add("- Specific and actionable");
        prompt.Add("- Include the tool to use if applicable");
        prompt.Add("- Have clear success criteria");
        prompt.Add("- Be granular enough to track progress");
        prompt.Add("");
        prompt.Add("When the plan is complete, call the tool " +
                   $"**{SaveMarkdownPlanTool.Name}** with two arguments:");
        prompt.Add("1. `markdown_plan`  – the full markdown document");
        prompt.Add("2. `required_tools` – an array with the tool names that appear in the \"Required Tools\" section.");
        prompt.Add("Return **nothing else**.");
        
        return string.Join("\n", prompt);
    }

    /// <summary>
    /// Creates a fallback markdown plan when LLM planning fails
    /// </summary>
    private string CreateFallbackMarkdownPlan(string task, IList<IUnifiedTool> availableTools)
    {
        var plan = new List<string>();

        // HEADER – tests look for "# Task:" explicitly
        plan.Add($"# Task: {task}");
        plan.Add("");

        // Strategy identical to the LLM-driven format used elsewhere
        plan.Add("**Strategy:** Execute the task using available tools in a systematic approach.");
        plan.Add("");

        // Rename section so the tests find "## Subtasks"
        plan.Add("## Subtasks");
        plan.Add("- [ ] Analyze the task requirements");
        plan.Add("- [ ] Identify necessary tools and resources");
        plan.Add("- [ ] Execute the task using appropriate tools");
        plan.Add("- [ ] Verify the results");
        plan.Add("- [ ] Complete the task");
        plan.Add("");

        plan.Add("## Required Tools");
        foreach (var tool in availableTools.Take(5)) // Limit to first 5 tools for fallback
        {
            plan.Add($"- {tool.Name}");
        }
        plan.Add("");

        plan.Add("## Success Criteria");
        plan.Add("- Task completed successfully");
        plan.Add("- All requirements met");
        plan.Add("- Results verified");

        _logger.LogInformation("Created fallback markdown plan for task: {Task}", task);
        return string.Join("\n", plan);
    }

    /// <summary>
    /// Very lightweight fallback analysis – just summarises counts.
    /// </summary>
    private static string AnalyzeCurrentState(CurrentState state, IList<IUnifiedTool> tools)
    {
        // Very lightweight summary – keeps build fast and avoids the heavy analyser previously removed
        if (state == null) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine($"• Context provided: {(!string.IsNullOrWhiteSpace(state.SessionContext))}");
        sb.AppendLine($"• Previous results: {state.PreviousResults?.Count ?? 0}");
        sb.AppendLine($"• User prefs set : {state.UserPreferences != null}");
        sb.AppendLine($"• Available tools: {tools.Count}");
        return sb.ToString();
    }

    // Parse FunctionToolCall to get plan & tools
    private static (string markdown, string[] tools) ParseFunctionCall(FunctionToolCall? fnCall)
    {
        if (fnCall == null)                         // no call at all
            return ("", Array.Empty<string>());

        // Try to obtain a JsonElement representing the arguments
        JsonElement root;
        if (fnCall.Arguments.HasValue)              // preferred: already a JsonElement
        {
            var argElement = fnCall.Arguments.Value;

            if (argElement.ValueKind == JsonValueKind.Object)
            {
                root = argElement;
            }
            else if (argElement.ValueKind == JsonValueKind.String)
            {
                // Some SDK versions return a *string* that contains serialized JSON
                var jsonText = argElement.GetString();
                if (string.IsNullOrWhiteSpace(jsonText))
                    return ("", Array.Empty<string>());

                using var doc = JsonDocument.Parse(jsonText);
                root = doc.RootElement.Clone();
            }
            else
            {
                return ("", Array.Empty<string>());
            }
        }
        else
        {
            // No arguments available
            return ("", Array.Empty<string>());
        }

        // From here on we are guaranteed to have a JSON object
        if (root.ValueKind != JsonValueKind.Object)
            return ("", Array.Empty<string>());

        var markdown = root.GetProperty("markdown_plan").GetString() ?? "";

        var toolsArr = root.TryGetProperty("required_tools", out var arr) && arr.ValueKind == JsonValueKind.Array
            ? arr.EnumerateArray()
                 .Where(e => e.ValueKind == JsonValueKind.String)
                 .Select(e => e.GetString()!)
                 .ToArray()
            : Array.Empty<string>();

        return (markdown.Trim(), toolsArr);
    }
}