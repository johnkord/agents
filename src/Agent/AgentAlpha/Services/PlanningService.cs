using Microsoft.Extensions.Logging;
using System.Text.Json;
using ModelContextProtocol.Client;
using OpenAIIntegration;
using OpenAIIntegration.Model;
using AgentAlpha.Interfaces;
using AgentAlpha.Models;
using AgentAlpha.Configuration;

namespace AgentAlpha.Services;

/// <summary>
/// Implementation of task planning service using LLM-based analysis
/// </summary>
public class PlanningService : IPlanningService
{
    private readonly IOpenAIResponsesService _openAi;
    private readonly ILogger<PlanningService> _logger;
    private readonly AgentConfiguration _config;

    public PlanningService(
        IOpenAIResponsesService openAi,
        ILogger<PlanningService> logger,
        AgentConfiguration config)
    {
        _openAi = openAi;
        _logger = logger;
        _config = config;
    }

    public async Task<TaskPlan> CreatePlanAsync(string task, IList<McpClientTool> availableTools, string? context = null)
    {
        _logger.LogInformation("Creating execution plan for task: {Task}", task);

        var toolDescriptions = availableTools.Select(t => $"- {t.Name}: {t.Description}").ToList();
        var contextSection = string.IsNullOrEmpty(context) ? "" : $"\n\nAdditional Context:\n{context}";

        var prompt = $@"You are an AI task planning assistant. Given a task and available tools, create a detailed execution plan.

Task to plan: {task}{contextSection}

Available Tools:
{string.Join("\n", toolDescriptions)}

Create a comprehensive execution plan with the following structure:

1. **Strategy**: High-level approach to completing the task
2. **Steps**: Detailed breakdown of execution steps
3. **Required Tools**: Tools needed throughout the plan
4. **Complexity**: Assess task complexity (Simple/Medium/Complex/VeryComplex)
5. **Confidence**: Your confidence in this plan (0.0-1.0)

For each step, specify:
- Step description
- Potential tools needed
- Whether the step is mandatory
- Expected input/output

Return your response as a JSON object matching this structure:
{{
    ""strategy"": ""High-level strategy description"",
    ""steps"": [
        {{
            ""stepNumber"": 1,
            ""description"": ""Step description"",
            ""potentialTools"": [""tool1"", ""tool2""],
            ""isMandatory"": true,
            ""expectedInput"": ""Input description"",
            ""expectedOutput"": ""Output description""
        }}
    ],
    ""requiredTools"": [""tool1"", ""tool2"", ""tool3""],
    ""complexity"": ""Medium"",
    ""confidence"": 0.85
}}

Focus on creating a logical, executable plan that efficiently uses the available tools.";

        try
        {
            var request = new ResponsesCreateRequest
            {
                Model = "gpt-3.5-turbo", // Use faster model for planning
                Input = new[]
                {
                    new { role = "user", content = prompt }
                },
                ToolChoice = "none"
            };

            var response = await _openAi.CreateResponseAsync(request);
            var outputMessage = response.Output?
                .OfType<OutputMessage>()
                .FirstOrDefault(m => string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase));
                
            var planJson = outputMessage?.Content?.ToString() ?? "{}";

            _logger.LogDebug("Raw planning response: {Response}", planJson);

            // Parse the JSON response into a TaskPlan
            var plan = ParsePlanFromJsonAsync(planJson, task).Result;
            
            _logger.LogInformation("Created plan with {StepCount} steps and complexity {Complexity}", 
                plan.Steps.Count, plan.Complexity);

            return plan;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create plan for task: {Task}", task);
            
            // Return a fallback plan
            return CreateFallbackPlan(task, availableTools);
        }
    }

    public async Task<TaskPlan> RefinePlanAsync(TaskPlan existingPlan, string feedback, IList<McpClientTool> availableTools)
    {
        _logger.LogInformation("Refining existing plan based on feedback");

        var toolDescriptions = availableTools.Select(t => $"- {t.Name}: {t.Description}").ToList();
        
        var existingPlanJson = JsonSerializer.Serialize(new
        {
            strategy = existingPlan.Strategy,
            steps = existingPlan.Steps.Select(s => new
            {
                stepNumber = s.StepNumber,
                description = s.Description,
                potentialTools = s.PotentialTools,
                isMandatory = s.IsMandatory,
                expectedInput = s.ExpectedInput,
                expectedOutput = s.ExpectedOutput
            }),
            requiredTools = existingPlan.RequiredTools,
            complexity = existingPlan.Complexity.ToString(),
            confidence = existingPlan.Confidence
        }, new JsonSerializerOptions { WriteIndented = true });

        var prompt = $"""
            You are refining an existing task execution plan based on new feedback or information.

            Original Task: {existingPlan.Task}

            Current Plan:
            {existingPlanJson}

            Feedback/New Information:
            {feedback}

            Available Tools:
            {string.Join("\n", toolDescriptions)}

            Based on the feedback, refine the plan to address the issues or incorporate new requirements.
            Return the refined plan in the same JSON format as the original.

            Focus on:
            - Addressing specific feedback points
            - Maintaining plan coherence
            - Optimizing tool usage
            - Adjusting complexity and confidence as needed
            """;

        try
        {
            var request = new ResponsesCreateRequest
            {
                Model = "gpt-3.5-turbo",
                Input = new[]
                {
                    new { role = "user", content = prompt }
                },
                ToolChoice = "none"
            };

            var response = await _openAi.CreateResponseAsync(request);
            var outputMessage = response.Output?
                .OfType<OutputMessage>()
                .FirstOrDefault(m => string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase));
                
            var refinedPlanJson = outputMessage?.Content?.ToString() ?? existingPlanJson;

            var refinedPlan = ParsePlanFromJsonAsync(refinedPlanJson, existingPlan.Task).Result;
            refinedPlan.CreatedAt = DateTime.UtcNow; // Update creation time for refined plan

            _logger.LogInformation("Refined plan with {StepCount} steps", refinedPlan.Steps.Count);

            return refinedPlan;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refine plan, returning original");
            return existingPlan;
        }
    }

    public Task<PlanValidationResult> ValidatePlanAsync(TaskPlan plan, IList<McpClientTool> availableTools)
    {
        _logger.LogDebug("Validating plan for task: {Task}", plan.Task);

        var result = new PlanValidationResult();
        var availableToolNames = availableTools.Select(t => t.Name).ToHashSet();

        // Check for missing tools
        foreach (var requiredTool in plan.RequiredTools)
        {
            if (!availableToolNames.Contains(requiredTool))
            {
                result.MissingTools.Add(requiredTool);
                result.Issues.Add($"Required tool '{requiredTool}' is not available");
            }
        }

        // Check step tool requirements
        foreach (var step in plan.Steps)
        {
            foreach (var tool in step.PotentialTools)
            {
                if (!availableToolNames.Contains(tool))
                {
                    result.Issues.Add($"Step {step.StepNumber} requires unavailable tool '{tool}'");
                }
            }
        }

        // Validate plan structure
        if (plan.Steps.Count == 0)
        {
            result.Issues.Add("Plan has no execution steps");
        }

        if (string.IsNullOrEmpty(plan.Strategy))
        {
            result.Issues.Add("Plan lacks a clear strategy");
        }

        // Check for step sequence issues
        var stepNumbers = plan.Steps.Select(s => s.StepNumber).ToList();
        if (stepNumbers.Count != stepNumbers.Distinct().Count())
        {
            result.Issues.Add("Plan has duplicate step numbers");
        }

        // Calculate overall validity
        result.IsValid = result.Issues.Count == 0;
        result.Confidence = result.IsValid ? 0.9 : Math.Max(0.1, 0.9 - (result.Issues.Count * 0.2));

        // Generate suggestions
        if (result.MissingTools.Count > 0)
        {
            result.Suggestions.Add($"Consider alternative approaches that don't require: {string.Join(", ", result.MissingTools)}");
        }

        if (plan.Steps.Count > 10)
        {
            result.Suggestions.Add("Consider simplifying the plan by combining related steps");
        }

        _logger.LogDebug("Plan validation completed. Valid: {IsValid}, Issues: {IssueCount}", 
            result.IsValid, result.Issues.Count);

        return Task.FromResult(result);
    }

    private Task<TaskPlan> ParsePlanFromJsonAsync(string json, string originalTask)
    {
        try
        {
            // Clean up the JSON (remove markdown code blocks if present)
            var cleanJson = json.Trim();
            if (cleanJson.StartsWith("```json"))
            {
                cleanJson = cleanJson.Substring(7);
            }
            if (cleanJson.StartsWith("```"))
            {
                cleanJson = cleanJson.Substring(3);
            }
            if (cleanJson.EndsWith("```"))
            {
                cleanJson = cleanJson.Substring(0, cleanJson.Length - 3);
            }
            cleanJson = cleanJson.Trim();

            var jsonElement = JsonSerializer.Deserialize<JsonElement>(cleanJson);

            var plan = new TaskPlan
            {
                Task = originalTask,
                Strategy = jsonElement.GetProperty("strategy").GetString() ?? "",
                RequiredTools = JsonSerializer.Deserialize<List<string>>(jsonElement.GetProperty("requiredTools").GetRawText()) ?? new(),
                Confidence = jsonElement.GetProperty("confidence").GetDouble()
            };

            // Parse complexity
            if (jsonElement.TryGetProperty("complexity", out var complexityElement) &&
                Enum.TryParse<TaskComplexity>(complexityElement.GetString(), true, out var complexity))
            {
                plan.Complexity = complexity;
            }

            // Parse steps
            if (jsonElement.TryGetProperty("steps", out var stepsElement))
            {
                foreach (var stepElement in stepsElement.EnumerateArray())
                {
                    var step = new PlanStep
                    {
                        StepNumber = stepElement.GetProperty("stepNumber").GetInt32(),
                        Description = stepElement.GetProperty("description").GetString() ?? "",
                        PotentialTools = JsonSerializer.Deserialize<List<string>>(stepElement.GetProperty("potentialTools").GetRawText()) ?? new(),
                        IsMandatory = stepElement.TryGetProperty("isMandatory", out var mandatoryElement) ? mandatoryElement.GetBoolean() : true,
                        ExpectedInput = stepElement.TryGetProperty("expectedInput", out var inputElement) ? inputElement.GetString() : null,
                        ExpectedOutput = stepElement.TryGetProperty("expectedOutput", out var outputElement) ? outputElement.GetString() : null
                    };
                    plan.Steps.Add(step);
                }
            }

            // Sort steps by step number
            plan.Steps.Sort((a, b) => a.StepNumber.CompareTo(b.StepNumber));

            return Task.FromResult(plan);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse plan JSON, creating fallback plan");
            return Task.FromResult(CreateFallbackPlan(originalTask, new List<McpClientTool>()));
        }
    }

    private TaskPlan CreateFallbackPlan(string task, IList<McpClientTool> availableTools)
    {
        _logger.LogInformation("Creating fallback plan for task: {Task}", task);

        return new TaskPlan
        {
            Task = task,
            Strategy = "Execute the task using available tools with adaptive approach",
            Steps = new List<PlanStep>
            {
                new PlanStep
                {
                    StepNumber = 1,
                    Description = "Analyze the task requirements",
                    PotentialTools = new List<string>(),
                    IsMandatory = true,
                    ExpectedInput = "Task description",
                    ExpectedOutput = "Understanding of requirements"
                },
                new PlanStep
                {
                    StepNumber = 2,
                    Description = "Execute the task using appropriate tools",
                    PotentialTools = availableTools.Take(5).Select(t => t.Name).ToList(),
                    IsMandatory = true,
                    ExpectedInput = "Task requirements",
                    ExpectedOutput = "Task completion"
                },
                new PlanStep
                {
                    StepNumber = 3,
                    Description = "Verify and report results",
                    PotentialTools = new List<string> { "complete_task" },
                    IsMandatory = true,
                    ExpectedInput = "Execution results",
                    ExpectedOutput = "Confirmation of completion"
                }
            },
            RequiredTools = availableTools.Take(5).Select(t => t.Name).ToList(),
            Complexity = TaskComplexity.Medium,
            Confidence = 0.6
        };
    }
}