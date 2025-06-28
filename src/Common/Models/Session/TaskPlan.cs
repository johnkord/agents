namespace Common.Models.Session;

/// <summary>
/// Represents a complete execution plan for a task
/// </summary>
public class TaskPlan
{
    /// <summary>
    /// The original task description
    /// </summary>
    public string Task { get; set; } = string.Empty;
    
    /// <summary>
    /// High-level strategy for completing the task
    /// </summary>
    public string Strategy { get; set; } = string.Empty;
    
    /// <summary>
    /// Ordered list of steps to complete the task
    /// </summary>
    public List<PlanStep> Steps { get; set; } = new();
    
    /// <summary>
    /// Tools identified as potentially needed for this plan
    /// </summary>
    public List<string> RequiredTools { get; set; } = new();
    
    /// <summary>
    /// Estimated complexity level of the task
    /// </summary>
    public TaskComplexity Complexity { get; set; } = TaskComplexity.Medium;
    
    /// <summary>
    /// Confidence level in the plan (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; set; } = 0.5;
    
    /// <summary>
    /// When this plan was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Additional context and metadata for the plan
    /// </summary>
    public Dictionary<string, object>? AdditionalContext { get; set; }
}

/// <summary>
/// Represents a single step in a task execution plan
/// </summary>
public class PlanStep
{
    /// <summary>
    /// Step number in the sequence
    /// </summary>
    public int StepNumber { get; set; }
    
    /// <summary>
    /// Description of what this step accomplishes
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Tools that might be needed for this specific step
    /// </summary>
    public List<string> PotentialTools { get; set; } = new();
    
    /// <summary>
    /// Whether this step is mandatory or optional
    /// </summary>
    public bool IsMandatory { get; set; } = true;
    
    /// <summary>
    /// Expected input for this step
    /// </summary>
    public string? ExpectedInput { get; set; }
    
    /// <summary>
    /// Expected output from this step
    /// </summary>
    public string? ExpectedOutput { get; set; }
}

/// <summary>
/// Complexity levels for task classification
/// </summary>
public enum TaskComplexity
{
    /// <summary>
    /// Simple tasks requiring 1-2 tools and minimal steps
    /// </summary>
    Simple,
    
    /// <summary>
    /// Medium complexity tasks requiring multiple tools and steps
    /// </summary>
    Medium,
    
    /// <summary>
    /// Complex tasks requiring many tools and sophisticated coordination
    /// </summary>
    Complex,
    
    /// <summary>
    /// Very complex tasks that may require iterative refinement
    /// </summary>
    VeryComplex
}