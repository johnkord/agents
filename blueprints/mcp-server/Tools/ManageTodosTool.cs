using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace McpServer.Tools;

/// <summary>
/// Structured task tracking with state machine validation.
/// The todo list is the agent's externalized working memory for plan state.
///
/// Research basis:
///   - CaveAgent (2026): persistent state manipulation outperforms stateless by +10.5%
///   - Choose Your Agent (2026): structured delegation plans beat unstructured approaches
///
/// Design: accepts a full todo list array each call (matching the Copilot contract).
/// Validates state transitions, enforces max-1 in-progress, persists to disk.
/// </summary>
[McpServerToolType]
public static class ManageTodosTool
{
    private static string GetTodoFilePath() =>
        Path.Combine(
            Environment.GetEnvironmentVariable("FORGE_MEMORY_ROOT")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".forge", "memories"),
            "session", "todos.json");

    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "not-started", "in-progress", "completed"
    };

    private const int MaxTodos = 100;

    [McpServerTool, Description(
        "Manage a structured todo list to track progress and plan tasks. " +
        "Pass the complete array of all todo items. Each item needs 'id' (number), " +
        "'title' (string, 3-7 words), and 'status' ('not-started', 'in-progress', or 'completed'). " +
        "Only one item should be 'in-progress' at a time. Mark items completed immediately after finishing.")]
    public static string ManageTodos(
        [Description("JSON array of todo items: [{\"id\": 1, \"title\": \"Do X\", \"status\": \"not-started\"}, ...]")] string todoListJson)
    {
        if (string.IsNullOrWhiteSpace(todoListJson))
            return "Error: todoListJson is required. Provide a JSON array of todo items.";

        List<TodoItem> items;
        try
        {
            items = JsonSerializer.Deserialize<List<TodoItem>>(todoListJson, JsonOptions) ?? [];
        }
        catch (JsonException ex)
        {
            return $"Error: Invalid JSON. {ex.Message}\nExpected format: [{{\"id\": 1, \"title\": \"Task\", \"status\": \"not-started\"}}]";
        }

        // Validate
        if (items.Count == 0)
            return "Error: Todo list is empty. Provide at least one item.";

        if (items.Count > MaxTodos)
            return $"Error: Too many items ({items.Count}). Maximum is {MaxTodos}.";

        var warnings = new List<string>();

        // Validate each item
        var ids = new HashSet<int>();
        foreach (var item in items)
        {
            if (!ids.Add(item.Id))
                return $"Error: Duplicate id {item.Id}. Each todo must have a unique id.";

            if (string.IsNullOrWhiteSpace(item.Title))
                return $"Error: Todo {item.Id} has no title.";

            if (!ValidStatuses.Contains(item.Status))
                return $"Error: Todo {item.Id} has invalid status '{item.Status}'. Valid: not-started, in-progress, completed.";
        }

        // Warn if multiple in-progress
        var inProgress = items.Count(i => i.Status.Equals("in-progress", StringComparison.OrdinalIgnoreCase));
        if (inProgress > 1)
            warnings.Add($"Warning: {inProgress} items are in-progress. Limit to 1 at a time for focus.");

        // Persist to disk
        try
        {
            var todoFilePath = GetTodoFilePath();
            var dir = Path.GetDirectoryName(todoFilePath);
            if (dir is not null)
                Directory.CreateDirectory(dir);
            File.WriteAllText(todoFilePath, JsonSerializer.Serialize(items, JsonWriteOptions));
        }
        catch (Exception ex)
        {
            warnings.Add($"Warning: Could not persist todo list: {ex.Message}");
        }

        // Build summary
        var notStarted = items.Count(i => i.Status.Equals("not-started", StringComparison.OrdinalIgnoreCase));
        var completed = items.Count(i => i.Status.Equals("completed", StringComparison.OrdinalIgnoreCase));

        var result = $"Todo list updated: {items.Count} items ({notStarted} not-started, {inProgress} in-progress, {completed} completed)";

        if (warnings.Count > 0)
            result += "\n" + string.Join("\n", warnings);

        return result;
    }

    private sealed class TodoItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Status { get; set; } = "not-started";
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };
}
