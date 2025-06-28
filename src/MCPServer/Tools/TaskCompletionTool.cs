using System.ComponentModel;
using ModelContextProtocol.Server;
using ModelContextProtocol.Protocol;
using System.Text.Json;

[McpServerToolType]
public class TaskCompletionTool
{
    [McpServerTool(Name = "complete_task"), Description("Marks the current task as completed with comprehensive reasoning and evidence")]
    public static string CompleteTask(
        string? summary = null, 
        string? reasoning = null, 
        string? evidence = null, 
        string? deliverables = null,
        string? keyActions = null)
    {
        var completionData = new
        {
            Summary = summary ?? "Task completed",
            Reasoning = reasoning ?? "Task completion criteria have been met",
            Evidence = evidence ?? "Based on execution results",
            Deliverables = deliverables,
            KeyActions = keyActions,
            CompletedAt = DateTime.UtcNow,
            Status = "COMPLETED"
        };

        var jsonReport = JsonSerializer.Serialize(completionData, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });

        // Return both human-readable and structured data
        var result = $"TASK COMPLETED: {summary ?? "Successfully completed"}\n\nCOMPLETION REPORT:\n{jsonReport}";
        
        return result;
    }
}
