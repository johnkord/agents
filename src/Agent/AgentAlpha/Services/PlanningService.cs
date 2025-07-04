using Microsoft.Extensions.Logging;
using AgentAlpha.Configuration;
using OpenAIIntegration;
using OpenAIIntegration.Model;
using Common.Interfaces.Session;
using System.Text.Json;
using AgentAlpha.Interfaces;

namespace AgentAlpha.Services;

/// <summary>
/// Service responsible for creating task execution plans using OpenAI reasoning models
/// </summary>
public class PlanningService : IPlanner
{
    private readonly AgentConfiguration _config;
    private readonly ISessionAwareOpenAIService _openAiService;
    private readonly ILogger<PlanningService> _logger;

    public PlanningService(
        AgentConfiguration config,
        ISessionAwareOpenAIService openAiService,
        ILogger<PlanningService> logger)
    {
        _config = config;
        _openAiService = openAiService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a detailed execution plan for a given task using the configured planning model
    /// </summary>
    /// <param name="task">The task to create a plan for</param>
    /// <param name="availableTools">List of available tools</param>
    /// <param name="sessionId">Optional session ID for context</param>
    /// <returns>A detailed execution plan</returns>
    public async Task<string> CreatePlanAsync(string task, IList<string>? availableTools = null, string? sessionId = null)
    {
        var planningModel = _config.GetPlanningModel();
        _logger.LogInformation("Creating execution plan using model: {Model} for task: {Task}", planningModel, task);

        var prompt = CreatePlanningPrompt(task, availableTools);

        var request = new ResponsesCreateRequest
        {
            Model = planningModel,
            Input = new[]
            {
                new { role = "system", content = "You are an expert task planning assistant specialized in creating detailed, actionable execution plans." },
                new { role = "user", content = prompt }
            },
            Instructions = "Create a comprehensive, step-by-step execution plan for the given task.",
            MaxOutputTokens = 3000,
            Temperature = 0.2  // Lower temperature for more consistent planning
        };

        try
        {
            var response = await _openAiService.CreateResponseAsync(request);
            var plan = ExtractPlanFromResponse(response);

            if (!string.IsNullOrEmpty(plan))
            {
                _logger.LogInformation("Successfully created execution plan with {Length} characters", plan.Length);
                return plan;
            }
            else
            {
                _logger.LogWarning("Failed to extract plan from OpenAI response, creating fallback plan");
                return CreateFallbackPlan(task, availableTools);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating execution plan, falling back to basic plan");
            return CreateFallbackPlan(task, availableTools);
        }
    }

    /// <summary>
    /// Refines an existing plan based on feedback or new requirements
    /// </summary>
    /// <param name="existingPlan">The current plan</param>
    /// <param name="feedback">Feedback or new requirements</param>
    /// <param name="sessionId">Optional session ID for context</param>
    /// <returns>A refined execution plan</returns>
    public async Task<string> RefinePlanAsync(string existingPlan, string feedback, string? sessionId = null)
    {
        var planningModel = _config.GetPlanningModel();
        _logger.LogInformation("Refining execution plan using model: {Model}", planningModel);

        var prompt = $"""
            Please refine the following execution plan based on the provided feedback:

            **Current Plan:**
            {existingPlan}

            **Feedback/Requirements:**
            {feedback}

            **Instructions:**
            - Analyze the current plan and the feedback
            - Identify areas that need improvement or modification
            - Create an updated plan that addresses the feedback
            - Maintain the same structure and level of detail
            - Ensure all steps remain actionable and specific

            Provide the complete refined plan:
            """;

        var request = new ResponsesCreateRequest
        {
            Model = planningModel,
            Input = new[]
            {
                new { role = "system", content = "You are an expert task planning assistant specialized in refining and improving execution plans based on feedback." },
                new { role = "user", content = prompt }
            },
            Instructions = "Refine the execution plan to address the provided feedback while maintaining clarity and actionability.",
            MaxOutputTokens = 3000,
            Temperature = 0.2
        };

        try
        {
            var response = await _openAiService.CreateResponseAsync(request);
            var refinedPlan = ExtractPlanFromResponse(response);

            if (!string.IsNullOrEmpty(refinedPlan))
            {
                _logger.LogInformation("Successfully refined execution plan");
                return refinedPlan;
            }
            else
            {
                _logger.LogWarning("Failed to refine plan, returning original plan");
                return existingPlan;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refining execution plan, returning original plan");
            return existingPlan;
        }
    }

    private string CreatePlanningPrompt(string task, IList<string>? availableTools)
    {
        var toolsSection = "";
        if (availableTools != null && availableTools.Any())
        {
            toolsSection = $"""

            **Available Tools:**
            {string.Join("\n", availableTools.Select(tool => $"- {tool}"))}
            """;
        }

        return $"""
            Create a detailed execution plan for the following task:

            **Task:** {task}{toolsSection}

            **Planning Requirements:**
            1. Break down the task into logical, sequential steps
            2. Each step should be specific and actionable
            3. Consider dependencies between steps
            4. Identify potential challenges and mitigation strategies
            5. Specify which tools to use for each step (if applicable)
            6. Include validation/verification points
            7. Estimate complexity and time requirements

            **Plan Structure:**
            Please provide your plan in the following format:

            # Execution Plan: [Task Title]

            ## Overview
            Brief description of the overall approach

            ## Steps
            1. **Step Name**: Detailed description
               - Tools needed: [list]
               - Expected outcome: [description]
               - Potential issues: [if any]

            2. **Step Name**: Detailed description
               - Tools needed: [list]
               - Expected outcome: [description]
               - Potential issues: [if any]

            [Continue for all steps...]

            ## Success Criteria
            How to determine if the task has been completed successfully

            ## Risk Mitigation
            Potential problems and how to address them

            Create a comprehensive plan that leverages systematic reasoning and careful analysis.
            """;
    }

    private string ExtractPlanFromResponse(ResponsesCreateResponse response)
    {
        if (response.Output == null) return "";

        foreach (var output in response.Output)
        {
            if (output is OutputMessage message && !string.IsNullOrEmpty(message.Content?.ToString()))
            {
                return message.Content.ToString() ?? "";
            }
        }

        return "";
    }

    private string CreateFallbackPlan(string task, IList<string>? availableTools)
    {
        var toolsSection = "";
        if (availableTools != null && availableTools.Any())
        {
            toolsSection = $"""

            ## Available Tools
            {string.Join("\n", availableTools.Select(tool => $"- {tool}"))}
            """;
        }

        return $"""
            # Execution Plan: {task}

            ## Overview
            This is a basic execution plan for the requested task. The plan follows a systematic approach to ensure successful completion.

            ## Steps
            1. **Analysis Phase**: Understand the requirements and scope
               - Tools needed: Analysis tools
               - Expected outcome: Clear understanding of what needs to be done
               - Potential issues: Ambiguous requirements

            2. **Preparation Phase**: Gather necessary resources and tools
               - Tools needed: Resource management tools
               - Expected outcome: All required resources are available
               - Potential issues: Missing resources or dependencies

            3. **Execution Phase**: Perform the main task activities
               - Tools needed: Task-specific tools
               - Expected outcome: Core task objectives completed
               - Potential issues: Implementation challenges

            4. **Validation Phase**: Verify results and quality
               - Tools needed: Testing and validation tools
               - Expected outcome: Confirmed successful completion
               - Potential issues: Quality issues requiring rework

            5. **Completion Phase**: Finalize and document results
               - Tools needed: Documentation tools
               - Expected outcome: Task fully completed and documented
               - Potential issues: Documentation gaps{toolsSection}

            ## Success Criteria
            - All task objectives are met
            - Quality standards are satisfied
            - Results are properly documented
            - No critical issues remain unresolved

            ## Risk Mitigation
            - Regular progress reviews to catch issues early
            - Maintain backup plans for critical steps
            - Ensure proper resource allocation
            - Document decisions and rationale

            *Note: This is a fallback plan. For more detailed planning, ensure the planning service is properly configured.*
            """;
    }
}