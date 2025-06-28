using System.ComponentModel;
using ModelContextProtocol.Server;
using ModelContextProtocol.Protocol;
using System.Text.Json;

[McpServerToolType]
public class SubtaskCompletionTool
{
    [McpServerTool(Name = "complete_subtask"), Description("Marks a specific subtask as completed with reasoning, evidence, and context for the next subtasks")]
    public static string CompleteSubtask(
        int stepNumber,
        string? summary = null,
        string? reasoning = null,
        string? evidence = null,
        string? context = null,
        string? nextStepGuidance = null,
        bool mandatory = true)
    {
        var completionData = new
        {
            StepNumber = stepNumber,
            Summary = summary ?? $"Subtask {stepNumber} completed",
            Reasoning = reasoning ?? "Subtask completion criteria have been met",
            Evidence = evidence ?? "Based on execution results",
            Context = context ?? "",
            NextStepGuidance = nextStepGuidance ?? "",
            IsMandatory = mandatory,
            CompletedAt = DateTime.UtcNow,
            Status = "SUBTASK_COMPLETED"
        };

        var jsonReport = JsonSerializer.Serialize(completionData, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });

        // Return structured data for processing by the task executor
        var result = $"SUBTASK {stepNumber} COMPLETED: {summary ?? $"Step {stepNumber} finished"}\n\nSUBTASK COMPLETION REPORT:\n{jsonReport}";
        
        if (!string.IsNullOrEmpty(context))
        {
            result += $"\n\nCONTEXT FOR NEXT STEPS:\n{context}";
        }
        
        if (!string.IsNullOrEmpty(nextStepGuidance))
        {
            result += $"\n\nGUIDANCE FOR NEXT STEP:\n{nextStepGuidance}";
        }
        
        return result;
    }
    
    [McpServerTool(Name = "update_subtask_notes"), Description("Updates notes or status for a subtask without marking it as completed")]
    public static string UpdateSubtaskNotes(
        int stepNumber,
        string? notes = null,
        string? progressUpdate = null,
        string? blockers = null)
    {
        var updateData = new
        {
            StepNumber = stepNumber,
            Notes = notes ?? "",
            ProgressUpdate = progressUpdate ?? "",
            Blockers = blockers ?? "",
            UpdatedAt = DateTime.UtcNow,
            Status = "SUBTASK_UPDATED"
        };

        var jsonReport = JsonSerializer.Serialize(updateData, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });

        var result = $"SUBTASK {stepNumber} UPDATED\n\nUPDATE REPORT:\n{jsonReport}";
        
        return result;
    }
    
    [McpServerTool(Name = "get_task_state"), Description("Gets the current task state showing all subtasks and their completion status")]
    public static string GetTaskState()
    {
        // This tool provides a way for the AI to request the current task state
        // The actual implementation will be handled by the TaskExecutor
        return "TASK_STATE_REQUEST";
    }
}