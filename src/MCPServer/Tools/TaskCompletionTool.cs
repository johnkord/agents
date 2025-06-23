using System.ComponentModel;
using ModelContextProtocol.Server;
using ModelContextProtocol.Protocol;

[McpServerToolType]
public class TaskCompletionTool
{
    [McpServerTool(Name = "complete_task"), Description("Marks the current task as completed")]
    public static string CompleteTask(string? summary = null)
        => string.IsNullOrWhiteSpace(summary)
            ? "TASK COMPLETED"
            : $"TASK COMPLETED: {summary}";
}
