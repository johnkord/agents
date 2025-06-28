using AgentAlpha.Models;
using Common.Interfaces.Session;
using Common.Models.Session;
using ModelContextProtocol.Client;

namespace AgentAlpha.Interfaces;

/// <summary>
/// Service responsible for creating task execution plans
/// </summary>
public interface IPlanningService
{
    /// <summary>
    /// Create a detailed execution plan for the given task
    /// </summary>
    /// <param name="task">The task to plan for</param>
    /// <param name="availableTools">Tools available to the agent</param>
    /// <param name="context">Optional context from previous conversations or sessions</param>
    /// <returns>A detailed execution plan</returns>
    Task<TaskPlan> CreatePlanAsync(string task, IList<IUnifiedTool> availableTools, string? context = null);
    
    /// <summary>
    /// Create a detailed execution plan for the given task with current state analysis
    /// </summary>
    /// <param name="task">The task to plan for</param>
    /// <param name="availableTools">Tools available to the agent</param>
    /// <param name="currentState">Current state of the environment and context</param>
    /// <param name="context">Optional additional context</param>
    /// <returns>A detailed execution plan based on current state analysis</returns>
    Task<TaskPlan> CreatePlanWithStateAnalysisAsync(string task, IList<IUnifiedTool> availableTools, CurrentState currentState, string? context = null);
    
    /// <summary>
    /// Refine an existing plan based on new information or execution results
    /// </summary>
    /// <param name="existingPlan">The plan to refine</param>
    /// <param name="feedback">Feedback from execution or new requirements</param>
    /// <param name="availableTools">Currently available tools</param>
    /// <returns>A refined execution plan</returns>
    Task<TaskPlan> RefinePlanAsync(TaskPlan existingPlan, string feedback, IList<IUnifiedTool> availableTools);
    
    /// <summary>
    /// Refine an existing plan with current state analysis
    /// </summary>
    /// <param name="existingPlan">The plan to refine</param>
    /// <param name="feedback">Feedback from execution or new requirements</param>
    /// <param name="availableTools">Currently available tools</param>
    /// <param name="currentState">Current state of the environment and context</param>
    /// <returns>A refined execution plan based on current state analysis</returns>
    Task<TaskPlan> RefinePlanWithStateAsync(TaskPlan existingPlan, string feedback, IList<IUnifiedTool> availableTools, CurrentState currentState);
    
    /// <summary>
    /// Validate that a plan is feasible with the available tools
    /// </summary>
    /// <param name="plan">The plan to validate</param>
    /// <param name="availableTools">Tools available to the agent</param>
    /// <returns>Validation result with any issues identified</returns>
    Task<PlanValidationResult> ValidatePlanAsync(TaskPlan plan, IList<IUnifiedTool> availableTools);
    
    /// <summary>
    /// Set the session activity logger for automatic OpenAI request logging
    /// </summary>
    /// <param name="activityLogger">The activity logger to use for this session</param>
    void SetActivityLogger(ISessionActivityLogger? activityLogger);
}

/// <summary>
/// Result of plan validation
/// </summary>
public class PlanValidationResult
{
    /// <summary>
    /// Whether the plan is valid and executable
    /// </summary>
    public bool IsValid { get; set; }
    
    /// <summary>
    /// Issues found during validation
    /// </summary>
    public List<string> Issues { get; set; } = new();
    
    /// <summary>
    /// Missing tools required by the plan
    /// </summary>
    public List<string> MissingTools { get; set; } = new();
    
    /// <summary>
    /// Confidence level in the validation (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; set; } = 1.0;
    
    /// <summary>
    /// Suggestions for improving the plan
    /// </summary>
    public List<string> Suggestions { get; set; } = new();
}