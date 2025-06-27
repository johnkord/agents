using Microsoft.Extensions.Logging;
using System.Text.Json;
using ModelContextProtocol.Client;
using OpenAIIntegration;
using OpenAIIntegration.Model;
using AgentAlpha.Interfaces;
using AgentAlpha.Models;
using AgentAlpha.Configuration;
using Common.Models.Session;

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

Create a comprehensive execution plan by calling the create_execution_plan tool. For each step, specify:
- Step description
- Potential tools needed
- Whether the step is mandatory
- Expected input/output

Focus on creating a logical, executable plan that efficiently uses the available tools.";

        try
        {
            // Define the tool for creating execution plans
            var planCreationTool = CreatePlanCreationToolDefinition();

            var request = new ResponsesCreateRequest
            {
                Model = "gpt-3.5-turbo", // Use faster model for planning
                Input = new[]
                {
                    new { role = "user", content = prompt }
                },
                Tools = new[] { planCreationTool },
                ToolChoice = "required" // Force the model to call one of the available tools
            };

            var response = await _openAi.CreateResponseAsync(request);
            
            // Extract plan from tool call
            var plan = ExtractPlanFromToolCall(response, task);
            
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
            Use the create_execution_plan tool to return the refined plan.

            Focus on:
            - Addressing specific feedback points
            - Maintaining plan coherence
            - Optimizing tool usage
            - Adjusting complexity and confidence as needed
            """;

        try
        {
            // Use the same tool definition for refinement
            var planCreationTool = CreatePlanCreationToolDefinition();

            var request = new ResponsesCreateRequest
            {
                Model = "gpt-3.5-turbo",
                Input = new[]
                {
                    new { role = "user", content = prompt }
                },
                Tools = new[] { planCreationTool },
                ToolChoice = "required" // Force the model to call one of the available tools
            };

            var response = await _openAi.CreateResponseAsync(request);
            
            var refinedPlan = ExtractPlanFromToolCall(response, existingPlan.Task);
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

    private ToolDefinition CreatePlanCreationToolDefinition()
    {
        return new ToolDefinition
        {
            Type = "function",
            Name = "create_execution_plan",
            Description = "Create a detailed execution plan for a given task",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    strategy = new
                    {
                        type = "string",
                        description = "High-level strategy for completing the task"
                    },
                    steps = new
                    {
                        type = "array",
                        description = "Ordered list of execution steps",
                        items = new
                        {
                            type = "object",
                            properties = new
                            {
                                stepNumber = new
                                {
                                    type = "integer",
                                    description = "Step number in the sequence"
                                },
                                description = new
                                {
                                    type = "string",
                                    description = "Description of what this step accomplishes"
                                },
                                potentialTools = new
                                {
                                    type = "array",
                                    description = "Tools that might be needed for this specific step",
                                    items = new { type = "string" }
                                },
                                isMandatory = new
                                {
                                    type = "boolean",
                                    description = "Whether this step is mandatory or optional"
                                },
                                expectedInput = new
                                {
                                    type = "string",
                                    description = "Expected input for this step"
                                },
                                expectedOutput = new
                                {
                                    type = "string",
                                    description = "Expected output from this step"
                                }
                            },
                            required = new[] { "stepNumber", "description", "potentialTools", "isMandatory" }
                        }
                    },
                    requiredTools = new
                    {
                        type = "array",
                        description = "Tools identified as potentially needed for this plan",
                        items = new { type = "string" }
                    },
                    complexity = new
                    {
                        type = "string",
                        description = "Estimated complexity level of the task",
                        @enum = new[] { "Simple", "Medium", "Complex", "VeryComplex" }
                    },
                    confidence = new
                    {
                        type = "number",
                        description = "Confidence level in the plan (0.0 to 1.0)",
                        minimum = 0.0,
                        maximum = 1.0
                    }
                },
                required = new[] { "strategy", "steps", "requiredTools", "complexity", "confidence" }
            }
        };
    }

    private TaskPlan ExtractPlanFromToolCall(ResponsesCreateResponse response, string originalTask)
    {
        try
        {
            // Find the function tool call for plan creation
            var planToolCall = response.Output?
                .OfType<FunctionToolCall>()
                .FirstOrDefault(tc => tc.Name == "create_execution_plan");

            if (planToolCall?.Arguments == null)
            {
                _logger.LogWarning("No plan creation tool call found in response");
                return CreateFallbackPlan(originalTask, new List<McpClientTool>());
            }

            var args = planToolCall.Arguments.Value;
            
            var plan = new TaskPlan
            {
                Task = originalTask,
                Strategy = args.TryGetProperty("strategy", out var strategyElement) ? strategyElement.GetString() ?? "" : "",
                RequiredTools = JsonSerializer.Deserialize<List<string>>(args.TryGetProperty("requiredTools", out var toolsElement) ? toolsElement.GetRawText() : "[]") ?? new(),
                Confidence = args.TryGetProperty("confidence", out var confidenceElement) ? confidenceElement.GetDouble() : 0.5
            };

            // Parse complexity
            if (args.TryGetProperty("complexity", out var complexityElement) &&
                Enum.TryParse<TaskComplexity>(complexityElement.GetString(), true, out var complexity))
            {
                plan.Complexity = complexity;
            }

            // Parse steps
            if (args.TryGetProperty("steps", out var stepsElement))
            {
                foreach (var stepElement in stepsElement.EnumerateArray())
                {
                    var step = new PlanStep
                    {
                        StepNumber = stepElement.TryGetProperty("stepNumber", out var numElement) ? numElement.GetInt32() : 0,
                        Description = stepElement.TryGetProperty("description", out var descElement) ? descElement.GetString() ?? "" : "",
                        PotentialTools = JsonSerializer.Deserialize<List<string>>(stepElement.TryGetProperty("potentialTools", out var stepToolsElement) ? stepToolsElement.GetRawText() : "[]") ?? new(),
                        IsMandatory = stepElement.TryGetProperty("isMandatory", out var mandatoryElement) ? mandatoryElement.GetBoolean() : true,
                        ExpectedInput = stepElement.TryGetProperty("expectedInput", out var inputElement) ? inputElement.GetString() : null,
                        ExpectedOutput = stepElement.TryGetProperty("expectedOutput", out var outputElement) ? outputElement.GetString() : null
                    };
                    plan.Steps.Add(step);
                }
            }

            // Sort steps by step number
            plan.Steps.Sort((a, b) => a.StepNumber.CompareTo(b.StepNumber));

            _logger.LogDebug("Successfully extracted plan from tool call with {StepCount} steps", plan.Steps.Count);

            return plan;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract plan from tool call, creating fallback plan");
            return CreateFallbackPlan(originalTask, new List<McpClientTool>());
        }
    }
}