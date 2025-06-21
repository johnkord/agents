using System.Text;
using Microsoft.Extensions.AI;

namespace Forge.Core;

/// <summary>
/// Builds the console output shown when Forge runs in dry-run mode.
/// </summary>
public static class DryRunPreview
{
    public static string Build(AgentOptions options, IEnumerable<AITool> allTools)
    {
        var registry = new ToolRegistry();
        registry.RegisterAll(allTools);

        // Apply mode restriction if set (for subagent processes)
        if (options.ToolMode is not null)
            registry.ApplyMode(options.ToolMode);

        return BuildFromActiveTools(options, registry.GetActiveTools());
    }

    public static string BuildFromActiveTools(AgentOptions options, IEnumerable<AITool> activeTools)
    {
        var lines = activeTools
            .OfType<AIFunction>()
            .OrderBy(tool => tool.Name, StringComparer.OrdinalIgnoreCase)
            .Select(tool => $"- {tool.Name}: {tool.Description ?? "(no description)"}")
            .ToList();

        var builder = new StringBuilder();
        builder.AppendLine("=== System Prompt ===");
        builder.AppendLine(SystemPrompt.Build(options));  // no lessons/repoMap in dry-run
        builder.AppendLine();
        builder.AppendLine("=== Tool List ===");

        if (lines.Count == 0)
        {
            builder.AppendLine("(no tools available)");
        }
        else
        {
            foreach (var line in lines)
                builder.AppendLine(line);
        }

        return builder.ToString().TrimEnd();
    }
}
