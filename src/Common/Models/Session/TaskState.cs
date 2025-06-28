using System.Text;
using System.Text.Json;

namespace Common.Models.Session;

/// <summary>
/// Represents the current state of a task execution with subtask completion tracking
/// </summary>
public class TaskState
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
    /// List of subtasks with their completion status
    /// </summary>
    public List<SubtaskState> Subtasks { get; set; } = new();
    
    /// <summary>
    /// Overall completion status of the task
    /// </summary>
    public TaskCompletionStatus Status { get; set; } = TaskCompletionStatus.InProgress;
    
    /// <summary>
    /// When this task state was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When this task state was last updated
    /// </summary>
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Context that has been gathered from completed subtasks
    /// </summary>
    public Dictionary<string, object> AccumulatedContext { get; set; } = new();
    
    /// <summary>
    /// Generate a markdown representation of the task state with checkboxes
    /// </summary>
    public string ToMarkdown()
    {
        var markdown = new StringBuilder();
        
        markdown.AppendLine($"# Task: {Task}");
        markdown.AppendLine();
        
        if (!string.IsNullOrEmpty(Strategy))
        {
            markdown.AppendLine($"**Strategy:** {Strategy}");
            markdown.AppendLine();
        }
        
        markdown.AppendLine($"**Status:** {Status}");
        markdown.AppendLine($"**Progress:** {GetCompletedCount()}/{Subtasks.Count} subtasks completed");
        markdown.AppendLine();
        
        if (Subtasks.Any())
        {
            markdown.AppendLine("## Subtasks");
            markdown.AppendLine();
            
            foreach (var subtask in Subtasks.OrderBy(s => s.StepNumber))
            {
                var checkbox = subtask.IsCompleted ? "- [x]" : "- [ ]";
                markdown.AppendLine($"{checkbox} **Step {subtask.StepNumber}:** {subtask.Description}");
                
                if (subtask.IsCompleted)
                {
                    if (!string.IsNullOrEmpty(subtask.CompletionSummary))
                    {
                        markdown.AppendLine($"  - *Completed:* {subtask.CompletionSummary}");
                    }
                    if (subtask.CompletedAt.HasValue)
                    {
                        markdown.AppendLine($"  - *Completed at:* {subtask.CompletedAt:yyyy-MM-dd HH:mm:ss}");
                    }
                }
                else if (subtask.Status == SubtaskStatus.InProgress)
                {
                    markdown.AppendLine($"  - *Status:* Currently in progress");
                }
                
                if (!string.IsNullOrEmpty(subtask.Notes))
                {
                    markdown.AppendLine($"  - *Notes:* {subtask.Notes}");
                }
                
                markdown.AppendLine();
            }
        }
        
        if (AccumulatedContext.Any())
        {
            markdown.AppendLine("## Context from Completed Subtasks");
            markdown.AppendLine();
            
            foreach (var context in AccumulatedContext)
            {
                markdown.AppendLine($"- **{context.Key}:** {context.Value}");
            }
            markdown.AppendLine();
        }
        
        markdown.AppendLine($"*Last updated: {LastUpdatedAt:yyyy-MM-dd HH:mm:ss}*");
        
        return markdown.ToString();
    }
    
    /// <summary>
    /// Get the number of completed subtasks
    /// </summary>
    public int GetCompletedCount()
    {
        return Subtasks.Count(s => s.IsCompleted);
    }
    
    /// <summary>
    /// Get the current subtask that should be executed
    /// </summary>
    public SubtaskState? GetCurrentSubtask()
    {
        return Subtasks
            .Where(s => !s.IsCompleted && s.CanStart(Subtasks))
            .OrderBy(s => s.StepNumber)
            .FirstOrDefault();
    }
    
    /// <summary>
    /// Mark a subtask as completed
    /// </summary>
    public void CompleteSubtask(int stepNumber, string completionSummary, string? evidence = null, Dictionary<string, object>? context = null)
    {
        var subtask = Subtasks.FirstOrDefault(s => s.StepNumber == stepNumber);
        if (subtask != null)
        {
            subtask.MarkCompleted(completionSummary, evidence);
            
            // Add context to accumulated context
            if (context != null)
            {
                foreach (var kvp in context)
                {
                    AccumulatedContext[$"Step{stepNumber}_{kvp.Key}"] = kvp.Value;
                }
            }
            
            LastUpdatedAt = DateTime.UtcNow;
            
            // Check if all subtasks are completed
            if (Subtasks.All(s => s.IsCompleted))
            {
                Status = TaskCompletionStatus.Completed;
            }
        }
    }
    
    /// <summary>
    /// Start working on a specific subtask
    /// </summary>
    public void StartSubtask(int stepNumber)
    {
        var subtask = Subtasks.FirstOrDefault(s => s.StepNumber == stepNumber);
        if (subtask != null && subtask.Status == SubtaskStatus.Pending)
        {
            subtask.Status = SubtaskStatus.InProgress;
            subtask.StartedAt = DateTime.UtcNow;
            LastUpdatedAt = DateTime.UtcNow;
        }
    }
    
    /// <summary>
    /// Create a TaskState from a TaskPlan
    /// </summary>
    public static TaskState FromTaskPlan(TaskPlan taskPlan)
    {
        var taskState = new TaskState
        {
            Task = taskPlan.Task,
            Strategy = taskPlan.Strategy,
            Subtasks = taskPlan.Steps.Select(step => new SubtaskState
            {
                StepNumber = step.StepNumber,
                Description = step.Description,
                ExpectedInput = step.ExpectedInput,
                ExpectedOutput = step.ExpectedOutput,
                IsMandatory = step.IsMandatory,
                PotentialTools = step.PotentialTools.ToList()
            }).ToList()
        };
        
        return taskState;
    }
}

/// <summary>
/// Represents the state of an individual subtask
/// </summary>
public class SubtaskState
{
    /// <summary>
    /// Step number in the sequence
    /// </summary>
    public int StepNumber { get; set; }
    
    /// <summary>
    /// Description of what this subtask accomplishes
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Current status of the subtask
    /// </summary>
    public SubtaskStatus Status { get; set; } = SubtaskStatus.Pending;
    
    /// <summary>
    /// Whether this subtask is mandatory or optional
    /// </summary>
    public bool IsMandatory { get; set; } = true;
    
    /// <summary>
    /// Expected input for this subtask
    /// </summary>
    public string? ExpectedInput { get; set; }
    
    /// <summary>
    /// Expected output from this subtask
    /// </summary>
    public string? ExpectedOutput { get; set; }
    
    /// <summary>
    /// Tools that might be needed for this specific subtask
    /// </summary>
    public List<string> PotentialTools { get; set; } = new();
    
    /// <summary>
    /// Prerequisites - step numbers that must be completed before this step can start
    /// </summary>
    public List<int> Prerequisites { get; set; } = new();
    
    /// <summary>
    /// When this subtask was started
    /// </summary>
    public DateTime? StartedAt { get; set; }
    
    /// <summary>
    /// When this subtask was completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// Summary of what was accomplished when completed
    /// </summary>
    public string? CompletionSummary { get; set; }
    
    /// <summary>
    /// Evidence or details about the completion
    /// </summary>
    public string? CompletionEvidence { get; set; }
    
    /// <summary>
    /// Additional notes about this subtask
    /// </summary>
    public string Notes { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether this subtask has been completed
    /// </summary>
    public bool IsCompleted => Status == SubtaskStatus.Completed;
    
    /// <summary>
    /// Check if this subtask can be started based on prerequisites
    /// </summary>
    public bool CanStart(List<SubtaskState> allSubtasks)
    {
        if (Status != SubtaskStatus.Pending)
            return false;
            
        // Check if all prerequisites are completed
        return Prerequisites.All(prereqStep => 
            allSubtasks.Any(s => s.StepNumber == prereqStep && s.IsCompleted));
    }
    
    /// <summary>
    /// Mark this subtask as completed
    /// </summary>
    public void MarkCompleted(string completionSummary, string? evidence = null)
    {
        Status = SubtaskStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        CompletionSummary = completionSummary;
        CompletionEvidence = evidence;
    }
}

/// <summary>
/// Status of an individual subtask
/// </summary>
public enum SubtaskStatus
{
    Pending,
    InProgress,
    Completed,
    Skipped,
    Failed
}

/// <summary>
/// Overall completion status of a task
/// </summary>
public enum TaskCompletionStatus
{
    InProgress,
    Completed,
    Failed,
    Cancelled
}