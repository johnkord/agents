using Microsoft.Extensions.Logging;
using System.Text.Json;
using ModelContextProtocol.Client;
using OpenAIIntegration;
using OpenAIIntegration.Model;
using AgentAlpha.Interfaces;
using AgentAlpha.Models;
using AgentAlpha.Configuration;
using Common.Models.Session;
using Common.Interfaces.Session;

namespace AgentAlpha.Services;

/// <summary>
/// Implementation of task planning service using LLM-based analysis
/// </summary>
public class PlanningService : IPlanningService
{
    private readonly ISessionAwareOpenAIService _openAi;
    private readonly ILogger<PlanningService> _logger;
    private readonly AgentConfiguration _config;
    private ISessionActivityLogger? _activityLogger;

    public PlanningService(
        ISessionAwareOpenAIService openAi,
        ILogger<PlanningService> logger,
        AgentConfiguration config)
    {
        _openAi = openAi;
        _logger = logger;
        _config = config;
    }

    /// <summary>
    /// Set the session activity logger for automatic OpenAI request logging
    /// </summary>
    public void SetActivityLogger(ISessionActivityLogger? activityLogger)
    {
        _activityLogger = activityLogger;
        _openAi.SetActivityLogger(activityLogger);
        _logger.LogDebug("Activity logger {Status} for PlanningService", 
            activityLogger != null ? "set" : "cleared");
    }

    /// <summary>
    /// Initialize task planning directly into markdown format
    /// </summary>
    public async Task<string> InitializeTaskPlanningAsync(string sessionId, string task, IList<IUnifiedTool> availableTools, string? context = null)
    {
        _logger.LogInformation("Initializing task planning in markdown format for session {SessionId}: {Task}", sessionId, task);

        try
        {
            // Create a basic current state if none provided
            var basicState = new CurrentState
            {
                SessionContext = context,
                CapturedAt = DateTime.UtcNow
            };

            return await InitializeTaskPlanningWithStateAsync(sessionId, task, availableTools, basicState, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize task planning for session {SessionId}", sessionId);
            return CreateFallbackMarkdownPlan(task, availableTools);
        }
    }

    /// <summary>
    /// Initialize task planning with current state analysis directly into markdown format
    /// </summary>
    public async Task<string> InitializeTaskPlanningWithStateAsync(string sessionId, string task, IList<IUnifiedTool> availableTools, CurrentState currentState, string? context = null)
    {
        _logger.LogInformation("Initializing state-aware task planning in markdown format for session {SessionId}: {Task}", sessionId, task);

        try
        {
            // Create markdown-based planning prompt
            var markdownPlanPrompt = CreateMarkdownPlanningPrompt(task, availableTools, currentState, context);
            
            var request = new RequestsCreateRequest
            {
                Model = _config.ModelId,
                Messages = new[]
                {
                    new Message 
                    { 
                        Role = MessageRole.System, 
                        Content = "You are an expert task planning assistant. Create comprehensive markdown-based task plans with clear checklist items for execution tracking." 
                    },
                    new Message 
                    { 
                        Role = MessageRole.User, 
                        Content = markdownPlanPrompt 
                    }
                },
                MaxTokens = 2000
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

            var response = await _openAi.CreateRequestsAsync(request);

            if (response?.Choices?.FirstOrDefault()?.Message?.Content != null)
            {
                var markdownPlan = response.Choices.First().Message.Content;
                _logger.LogInformation("Successfully created markdown task plan for session {SessionId}", sessionId);
                return markdownPlan;
            }
            else
            {
                _logger.LogWarning("Failed to get valid markdown plan response for session {SessionId}", sessionId);
                return CreateFallbackMarkdownPlan(task, availableTools);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize state-aware task planning for session {SessionId}", sessionId);
            return CreateFallbackMarkdownPlan(task, availableTools);
        }
    }

    public async Task<TaskPlan> CreatePlanAsync(string task, IList<IUnifiedTool> availableTools, string? context = null)
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

    public async Task<TaskPlan> CreatePlanWithStateAnalysisAsync(string task, IList<IUnifiedTool> availableTools, CurrentState currentState, string? context = null)
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
                Model = _config.Model, // Use configured model for planning
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
            
            // Log detailed plan information for better activity tracking
            await LogPlanDetailsAsync(plan, availableTools);
            
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

    public async Task<TaskPlan> RefinePlanAsync(TaskPlan existingPlan, string feedback, IList<IUnifiedTool> availableTools)
    {
        // Create a basic current state from the existing plan context
        var currentState = new CurrentState
        {
            SessionContext = $"Refining plan for: {existingPlan.Task}",
            AvailableResources = new Dictionary<string, string>
            {
                ["ExistingStrategy"] = existingPlan.Strategy,
                ["ExistingComplexity"] = existingPlan.Complexity.ToString(),
                ["ExistingConfidence"] = existingPlan.Confidence.ToString("F2"),
                ["ExistingStepCount"] = existingPlan.Steps.Count.ToString()
            }
        };

        // If the plan has additional context, extract relevant state information
        if (existingPlan.AdditionalContext != null)
        {
            foreach (var context in existingPlan.AdditionalContext)
            {
                currentState.AdditionalContext[context.Key] = context.Value;
            }
        }

        return await RefinePlanWithStateAsync(existingPlan, feedback, availableTools, currentState);
    }

    /// <summary>
    /// Refine an existing plan with current state analysis
    /// </summary>
    public async Task<TaskPlan> RefinePlanWithStateAsync(TaskPlan existingPlan, string feedback, IList<IUnifiedTool> availableTools, CurrentState currentState)
    {
        _logger.LogInformation("Refining existing plan with state analysis based on feedback");

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

        // Analyze current state for refinement context
        var stateAnalysis = AnalyzeCurrentState(currentState, availableTools);

        var prompt = $"""
            You are an advanced AI task planning assistant performing plan refinement with comprehensive state analysis.
            
            ORIGINAL TASK: {existingPlan.Task}

            CURRENT PLAN TO REFINE:
            {existingPlanJson}

            FEEDBACK/NEW REQUIREMENTS:
            {feedback}

            CURRENT STATE ANALYSIS:
            {stateAnalysis}

            AVAILABLE TOOLS:
            {string.Join("\n", toolDescriptions)}

            REFINEMENT INSTRUCTIONS:
            Based on the feedback and current state analysis, refine the execution plan with these priorities:

            1. FEEDBACK INTEGRATION: Address all specific feedback points and requirements
            2. STATE OPTIMIZATION: Leverage current state insights to improve efficiency
            3. CONTEXT PRESERVATION: Maintain valuable aspects of the original plan
            4. CONSTRAINT ADAPTATION: Adapt to any new constraints or limitations identified
            5. LEARNING APPLICATION: Apply lessons from previous executions if available

            For each modification:
            - EXPLAIN why the change is needed based on feedback and state analysis
            - JUSTIFY how it improves upon the original plan
            - CONSIDER impact on overall plan coherence and feasibility
            - ACCOUNT FOR any state-specific constraints or opportunities

            Use the create_execution_plan tool to return the refined plan that addresses the feedback while optimizing for current conditions.
            """;

        try
        {
            // Use the enhanced tool definition for refinement
            var planCreationTool = CreateEnhancedPlanCreationToolDefinition();

            var request = new ResponsesCreateRequest
            {
                Model = _config.Model,
                Input = new[]
                {
                    new { role = "user", content = prompt }
                },
                Tools = new[] { planCreationTool },
                ToolChoice = "required"
            };

            var response = await _openAi.CreateResponseAsync(request);
            
            var refinedPlan = ExtractPlanFromToolCall(response, existingPlan.Task);
            refinedPlan.CreatedAt = DateTime.UtcNow; // Update creation time for refined plan

            // Enhance the refined plan with state insights
            EnhancePlanWithStateInsights(refinedPlan, currentState);
            
            // Add refinement metadata
            refinedPlan.AdditionalContext ??= new Dictionary<string, object>();
            refinedPlan.AdditionalContext["RefinementFeedback"] = feedback;
            refinedPlan.AdditionalContext["RefinedAt"] = DateTime.UtcNow;
            refinedPlan.AdditionalContext["OriginalComplexity"] = existingPlan.Complexity.ToString();
            refinedPlan.AdditionalContext["OriginalConfidence"] = existingPlan.Confidence;

            _logger.LogInformation("Refined plan with {StepCount} steps. Complexity: {OriginalComplexity} -> {NewComplexity}", 
                refinedPlan.Steps.Count, existingPlan.Complexity, refinedPlan.Complexity);

            return refinedPlan;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refine plan with state analysis, returning original");
            return existingPlan;
        }
    }

    private TaskPlan CreateFallbackPlan(string task, IList<IUnifiedTool> availableTools)
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
                    PotentialTools = availableTools.Select(t => t.Name).ToList(),
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
            RequiredTools = availableTools.Select(t => t.Name).ToList(),
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
        FunctionToolCall? planToolCall = null;
        try
        {
            // Find the function tool call for plan creation
            planToolCall = response.Output?
                .OfType<FunctionToolCall>()
                .FirstOrDefault(tc => tc.Name == "create_execution_plan");

            if (planToolCall?.Arguments == null)
            {
                _logger.LogWarning("No plan creation tool call found in response");
                LogFallbackPlanCreation(originalTask, "No plan creation tool call found in response", null);
                return CreateFallbackPlan(originalTask, new List<IUnifiedTool>());
            }

            var rawArguments = planToolCall.Arguments.Value;
            JsonElement args;

            // Handle both string and object arguments
            if (rawArguments.ValueKind == JsonValueKind.String)
            {
                // Arguments is a JSON string that needs to be parsed
                var argumentsString = rawArguments.GetString();
                _logger.LogDebug("Tool call arguments received as string, parsing JSON: {ArgumentsString}", argumentsString);
                
                if (string.IsNullOrWhiteSpace(argumentsString))
                {
                    _logger.LogWarning("Tool call arguments string is null or empty");
                    LogFallbackPlanCreation(originalTask, "Tool call arguments string is null or empty", argumentsString);
                    return CreateFallbackPlan(originalTask, new List<IUnifiedTool>());
                }

                try
                {
                    args = JsonSerializer.Deserialize<JsonElement>(argumentsString);
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogWarning(jsonEx, "Failed to parse tool call arguments JSON string: {ArgumentsString}", argumentsString);
                    LogFallbackPlanCreation(originalTask, $"Failed to parse tool call arguments JSON: {jsonEx.Message}", argumentsString);
                    return CreateFallbackPlan(originalTask, new List<IUnifiedTool>());
                }
            }
            else if (rawArguments.ValueKind == JsonValueKind.Object)
            {
                // Arguments is already a JSON object
                args = rawArguments;
                _logger.LogDebug("Tool call arguments received as object, using directly");
            }
            else
            {
                var argumentsRaw = rawArguments.GetRawText();
                _logger.LogWarning("Tool call arguments has unexpected JSON type: {ValueKind}, Raw content: {ArgumentsRaw}", 
                    rawArguments.ValueKind, argumentsRaw);
                LogFallbackPlanCreation(originalTask, $"Tool call arguments has unexpected JSON type: {rawArguments.ValueKind}", argumentsRaw);
                return CreateFallbackPlan(originalTask, new List<IUnifiedTool>());
            }
            
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
            var rawArgumentsText = planToolCall?.Arguments?.GetRawText() ?? "null";
            _logger.LogWarning(ex, "Failed to extract plan from tool call, creating fallback plan. Raw arguments: {RawArguments}", rawArgumentsText);
            LogFallbackPlanCreation(originalTask, $"Exception during plan extraction: {ex.Message}", rawArgumentsText);
            return CreateFallbackPlan(originalTask, new List<IUnifiedTool>());
        }
    }

    /// <summary>
    /// Analyzes the current state to provide context for planning
    /// </summary>
    private string AnalyzeCurrentState(CurrentState currentState, IList<IUnifiedTool> availableTools)
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
    private TaskPlan CreateStateFallbackPlan(string task, IList<IUnifiedTool> availableTools, CurrentState currentState)
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

    /// <summary>
    /// Log detailed plan information to activity log for better tracking and debugging
    /// </summary>
    private async Task LogPlanDetailsAsync(TaskPlan plan, IList<IUnifiedTool> availableTools)
    {
        if (_activityLogger == null) return;

        var planDetails = new
        {
            Task = plan.Task,
            Strategy = plan.Strategy,
            Complexity = plan.Complexity.ToString(),
            Confidence = plan.Confidence,
            StepCount = plan.Steps.Count,
            Steps = plan.Steps.Select(s => new
            {
                StepNumber = s.StepNumber,
                Description = s.Description,
                PotentialTools = s.PotentialTools,
                IsMandatory = s.IsMandatory,
                ExpectedInput = s.ExpectedInput,
                ExpectedOutput = s.ExpectedOutput
            }).ToList(),
            RequiredTools = plan.RequiredTools,
            AvailableToolsCount = availableTools.Count,
            SelectedToolsRatio = plan.RequiredTools.Count > 0 ? (double)plan.RequiredTools.Count / availableTools.Count : 0.0,
            CreatedAt = plan.CreatedAt,
            AdditionalContext = plan.AdditionalContext
        };

        await _activityLogger.LogActivityAsync(
            ActivityTypes.PlanDetails,
            $"Created execution plan with {plan.Steps.Count} steps and {plan.Complexity} complexity",
            planDetails
        );
    }

    /// <summary>
    /// Logs when a fallback plan has to be created due to parsing failures
    /// </summary>
    private void LogFallbackPlanCreation(string originalTask, string reason, string? rawContent)
    {
        _logger.LogWarning("Creating fallback plan due to parsing failure. Task: {Task}, Reason: {Reason}", 
            originalTask, reason);

        // Also log to activity logger if available for comprehensive audit trail
        if (_activityLogger != null)
        {
            var fallbackData = new
            {
                OriginalTask = originalTask,
                FailureReason = reason,
                RawContent = SessionActivity.TruncateString(rawContent, 2000), // Limit raw content size
                FallbackCreatedAt = DateTime.UtcNow,
                Severity = "Warning"
            };

            // Use async fire-and-forget since this is a synchronous method
            _ = Task.Run(async () =>
            {
                try
                {
                    await _activityLogger.LogFailedActivityAsync(
                        ActivityTypes.TaskPlanning,
                        $"Plan extraction failed, creating fallback plan for task: {originalTask}",
                        reason,
                        fallbackData
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to log fallback plan creation to activity logger");
                }
            });
        }
    }

    /// <summary>
    /// Creates a markdown planning prompt for LLM task planning
    /// </summary>
    private string CreateMarkdownPlanningPrompt(string task, IList<IUnifiedTool> availableTools, CurrentState currentState, string? context)
    {
        var prompt = new List<string>();
        
        prompt.Add("Create a comprehensive markdown-based execution plan for the following task:");
        prompt.Add($"\n**TASK:** {task}\n");
        
        if (!string.IsNullOrEmpty(context))
        {
            prompt.Add($"**ADDITIONAL CONTEXT:** {context}\n");
        }
        
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
        prompt.Add("4. Required Tools - List of tools needed for execution");
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
        
        return string.Join("\n", prompt);
    }

    /// <summary>
    /// Creates a fallback markdown plan when LLM planning fails
    /// </summary>
    private string CreateFallbackMarkdownPlan(string task, IList<IUnifiedTool> availableTools)
    {
        var plan = new List<string>();
        
        plan.Add("# Task Execution Plan");
        plan.Add("");
        plan.Add("## Task Overview");
        plan.Add($"{task}");
        plan.Add("");
        plan.Add("## Strategy");
        plan.Add("Execute the task using available tools in a systematic approach.");
        plan.Add("");
        plan.Add("## Execution Steps");
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
}