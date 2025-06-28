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

        // Try to create a basic current state if none provided
        var basicState = new CurrentState
        {
            SessionContext = context,
            AvailableResources = new Dictionary<string, string>
            {
                ["ToolCount"] = availableTools.Count.ToString(),
                ["AvailableTools"] = string.Join(", ", availableTools.Select(t => t.Name))
            }
        };

        // Use the enhanced state-aware planning when possible
        return await CreatePlanWithStateAnalysisAsync(task, availableTools, basicState, context);
    }

    public async Task<TaskPlan> CreatePlanWithStateAnalysisAsync(string task, IList<McpClientTool> availableTools, CurrentState currentState, string? context = null)
    {
        _logger.LogInformation("Creating execution plan with state analysis for task: {Task}", task);

        var toolDescriptions = availableTools.Select(t => $"- {t.Name}: {t.Description}").ToList();
        var contextSection = string.IsNullOrEmpty(context) ? "" : $"\n\nAdditional Context:\n{context}";
        
        // Analyze the current state to provide comprehensive context
        var stateAnalysis = AnalyzeCurrentState(currentState, availableTools);
        
        var prompt = $@"You are an advanced AI task planning assistant with deep analytical capabilities. 
You must analyze the current state and create a sophisticated execution plan that considers all available context.

TASK TO PLAN: {task}{contextSection}

CURRENT STATE ANALYSIS:
{stateAnalysis}

AVAILABLE TOOLS:
{string.Join("\n", toolDescriptions)}

PLANNING INSTRUCTIONS:
Based on your analysis of the current state, create a comprehensive execution plan that:

1. LEVERAGES CONTEXT: Use insights from previous executions, available resources, and environmental constraints
2. OPTIMIZES FOR EFFICIENCY: Consider user preferences, risk tolerance, and resource limitations  
3. ADAPTS TO CONDITIONS: Account for current environment capabilities and any constraints
4. LEARNS FROM HISTORY: Apply lessons from previous similar tasks if available
5. MANAGES COMPLEXITY: Break down the task appropriately based on available tools and resources

For each step in your plan:
- Explain WHY this step is necessary given the current state
- Specify HOW it builds on previous context or resources
- Identify DEPENDENCIES on state conditions or previous results
- Note any ADAPTATIONS made based on current constraints

Create a logical, state-aware execution plan using the create_execution_plan tool.";

        try
        {
            // Define the enhanced tool for creating execution plans
            var planCreationTool = CreateEnhancedPlanCreationToolDefinition();

            var request = new ResponsesCreateRequest
            {
                Model = "gpt-3.5-turbo", // Consider upgrading to gpt-4 for more sophisticated analysis
                Input = new[]
                {
                    new { role = "user", content = prompt }
                },
                Tools = new[] { planCreationTool },
                ToolChoice = "required"
            };

            var response = await _openAi.CreateResponseAsync(request);
            
            // Extract plan from tool call with state context
            var plan = ExtractPlanFromToolCall(response, task);
            
            // Enhance plan with state-derived insights
            EnhancePlanWithStateInsights(plan, currentState);
            
            _logger.LogInformation("Created state-aware plan with {StepCount} steps and complexity {Complexity}", 
                plan.Steps.Count, plan.Complexity);

            return plan;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create state-aware plan for task: {Task}", task);
            
            // Return a fallback plan that still considers basic state information
            return CreateStateFallbackPlan(task, availableTools, currentState);
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

    /// <summary>
    /// Analyzes the current state to provide context for planning
    /// </summary>
    private string AnalyzeCurrentState(CurrentState currentState, IList<McpClientTool> availableTools)
    {
        var analysis = new List<string>();
        
        // Session context analysis
        if (!string.IsNullOrEmpty(currentState.SessionContext))
        {
            analysis.Add($"SESSION CONTEXT: {currentState.SessionContext}");
        }
        
        // Previous execution results analysis
        if (currentState.PreviousResults.Any())
        {
            analysis.Add("PREVIOUS EXECUTION HISTORY:");
            foreach (var result in currentState.PreviousResults.TakeLast(3)) // Limit to last 3 results
            {
                var status = result.Success ? "SUCCESS" : "FAILED";
                analysis.Add($"  - Task: '{result.Task}' [{status}] - {result.Summary}");
                if (!string.IsNullOrEmpty(result.Insights))
                {
                    analysis.Add($"    Insights: {result.Insights}");
                }
                if (result.ToolsUsed.Any())
                {
                    analysis.Add($"    Tools Used: {string.Join(", ", result.ToolsUsed)}");
                }
            }
        }
        
        // Resource availability analysis
        if (currentState.AvailableResources.Any())
        {
            analysis.Add("AVAILABLE RESOURCES:");
            foreach (var resource in currentState.AvailableResources)
            {
                analysis.Add($"  - {resource.Key}: {resource.Value}");
            }
        }
        
        // User preferences analysis
        if (currentState.UserPreferences != null)
        {
            analysis.Add("USER PREFERENCES:");
            var prefs = currentState.UserPreferences;
            
            if (!string.IsNullOrEmpty(prefs.PreferredApproach))
            {
                analysis.Add($"  - Preferred Approach: {prefs.PreferredApproach}");
            }
            
            if (prefs.PreferredTools.Any())
            {
                analysis.Add($"  - Preferred Tools: {string.Join(", ", prefs.PreferredTools)}");
            }
            
            if (prefs.AvoidedTools.Any())
            {
                analysis.Add($"  - Avoided Tools: {string.Join(", ", prefs.AvoidedTools)}");
            }
            
            analysis.Add($"  - Risk Tolerance: {prefs.RiskTolerance:F2} (0.0=conservative, 1.0=high risk)");
            
            if (prefs.MaxExecutionTime.HasValue)
            {
                analysis.Add($"  - Max Execution Time: {prefs.MaxExecutionTime.Value}");
            }
        }
        
        // Environment capabilities analysis
        if (currentState.Environment != null)
        {
            analysis.Add("ENVIRONMENT CAPABILITIES:");
            var env = currentState.Environment;
            
            if (!string.IsNullOrEmpty(env.ComputeResources))
            {
                analysis.Add($"  - Compute Resources: {env.ComputeResources}");
            }
            
            if (!string.IsNullOrEmpty(env.NetworkStatus))
            {
                analysis.Add($"  - Network Status: {env.NetworkStatus}");
            }
            
            if (!string.IsNullOrEmpty(env.StorageInfo))
            {
                analysis.Add($"  - Storage Info: {env.StorageInfo}");
            }
            
            if (env.SecurityConstraints.Any())
            {
                analysis.Add($"  - Security Constraints: {string.Join(", ", env.SecurityConstraints)}");
            }
            
            if (env.PerformanceMetrics.Any())
            {
                analysis.Add("  - Performance Metrics:");
                foreach (var metric in env.PerformanceMetrics)
                {
                    analysis.Add($"    {metric.Key}: {metric.Value:F2}");
                }
            }
        }
        
        // Tool compatibility analysis
        analysis.Add("\nTOOL COMPATIBILITY ANALYSIS:");
        var toolNames = availableTools.Select(t => t.Name).ToHashSet();
        
        if (currentState.UserPreferences?.PreferredTools.Any() == true)
        {
            var availablePreferred = currentState.UserPreferences.PreferredTools
                .Where(t => toolNames.Contains(t)).ToList();
            if (availablePreferred.Any())
            {
                analysis.Add($"  - Available Preferred Tools: {string.Join(", ", availablePreferred)}");
            }
            
            var unavailablePreferred = currentState.UserPreferences.PreferredTools
                .Where(t => !toolNames.Contains(t)).ToList();
            if (unavailablePreferred.Any())
            {
                analysis.Add($"  - Unavailable Preferred Tools: {string.Join(", ", unavailablePreferred)}");
            }
        }
        
        // Additional context analysis
        if (currentState.AdditionalContext.Any())
        {
            analysis.Add("\nADDITIONAL CONTEXT:");
            foreach (var context in currentState.AdditionalContext)
            {
                analysis.Add($"  - {context.Key}: {context.Value}");
            }
        }
        
        analysis.Add($"\nSTATE CAPTURED AT: {currentState.CapturedAt:yyyy-MM-dd HH:mm:ss} UTC");
        
        return string.Join("\n", analysis);
    }

    /// <summary>
    /// Creates an enhanced tool definition for state-aware plan creation
    /// </summary>
    private ToolDefinition CreateEnhancedPlanCreationToolDefinition()
    {
        // Use the same base structure but with enhanced descriptions
        var baseTool = CreatePlanCreationToolDefinition();
        
        // Enhance the description to emphasize state awareness
        return new ToolDefinition
        {
            Name = baseTool.Name,
            Description = "Create a comprehensive execution plan that leverages current state analysis, environmental context, user preferences, and historical insights to optimize task completion",
            Parameters = baseTool.Parameters
        };
    }

    /// <summary>
    /// Enhances a plan with insights derived from current state analysis
    /// </summary>
    private void EnhancePlanWithStateInsights(TaskPlan plan, CurrentState currentState)
    {
        // Add state-derived metadata to the plan
        plan.AdditionalContext ??= new Dictionary<string, object>();
        plan.AdditionalContext["StateAnalysisTimestamp"] = currentState.CapturedAt;
        
        // Record user preferences that influenced the plan
        if (currentState.UserPreferences != null)
        {
            plan.AdditionalContext["UserRiskTolerance"] = currentState.UserPreferences.RiskTolerance;
            plan.AdditionalContext["PreferredApproach"] = currentState.UserPreferences.PreferredApproach ?? "default";
        }
        
        // Record environmental constraints that influenced the plan
        if (currentState.Environment?.SecurityConstraints.Any() == true)
        {
            plan.AdditionalContext["SecurityConstraints"] = currentState.Environment.SecurityConstraints;
        }
        
        // Adjust confidence based on state factors
        var confidenceAdjustment = CalculateStateBasedConfidenceAdjustment(currentState);
        plan.Confidence = Math.Max(0.0, Math.Min(1.0, plan.Confidence + confidenceAdjustment));
        
        _logger.LogDebug("Enhanced plan with state insights. Confidence adjusted by {Adjustment:F3}", confidenceAdjustment);
    }

    /// <summary>
    /// Calculates confidence adjustment based on current state factors
    /// </summary>
    private double CalculateStateBasedConfidenceAdjustment(CurrentState currentState)
    {
        double adjustment = 0.0;
        
        // Boost confidence if we have successful execution history
        var recentSuccesses = currentState.PreviousResults
            .Where(r => r.Success && r.CompletedAt > DateTime.UtcNow.AddHours(-24))
            .Count();
        adjustment += Math.Min(0.2, recentSuccesses * 0.05);
        
        // Reduce confidence if there are recent failures
        var recentFailures = currentState.PreviousResults
            .Where(r => !r.Success && r.CompletedAt > DateTime.UtcNow.AddHours(-24))
            .Count();
        adjustment -= Math.Min(0.2, recentFailures * 0.08);
        
        // Adjust based on resource availability
        if (currentState.AvailableResources.Any())
        {
            adjustment += 0.05; // Having documented resources boosts confidence
        }
        
        // Adjust based on environmental constraints
        if (currentState.Environment?.SecurityConstraints.Any() == true)
        {
            adjustment -= 0.03; // Security constraints add complexity
        }
        
        return adjustment;
    }

    /// <summary>
    /// Creates a fallback plan that considers basic state information
    /// </summary>
    private TaskPlan CreateStateFallbackPlan(string task, IList<McpClientTool> availableTools, CurrentState currentState)
    {
        _logger.LogInformation("Creating state-aware fallback plan for task: {Task}", task);

        var fallbackPlan = CreateFallbackPlan(task, availableTools);
        
        // Enhance fallback plan with basic state considerations
        if (currentState.UserPreferences?.PreferredApproach != null)
        {
            fallbackPlan.Strategy = $"Execute the task using a {currentState.UserPreferences.PreferredApproach} approach with available tools";
        }
        
        // Adjust complexity based on user risk tolerance
        if (currentState.UserPreferences?.RiskTolerance < 0.3)
        {
            // Conservative users get simpler plans
            if (fallbackPlan.Complexity > TaskComplexity.Medium)
            {
                fallbackPlan.Complexity = TaskComplexity.Medium;
            }
        }
        
        EnhancePlanWithStateInsights(fallbackPlan, currentState);
        
        return fallbackPlan;
    }
}