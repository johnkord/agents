using System.Text.Json;
using System.Text.RegularExpressions;

namespace Forge.Core;

/// <summary>
/// Guardrails enforce hard limits on agent behavior.
/// Checks run before each tool execution and at the loop level.
/// </summary>
public sealed class Guardrails
{
    private readonly AgentOptions _options;
    private static readonly HashSet<string> DeniedCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "rm -rf", "rm -r ", "sudo", "mkfs", "dd if=", ":(){", "chmod -R 777",
        "shutdown", "reboot", "halt", "poweroff", "> /dev/",
    };

    // Pipe-to-shell/interpreter patterns: match download commands piped to any interpreter
    private static readonly Regex PipeToShellPattern = new(
        @"(curl|wget)\s*.*?\s*\|\s*(sh|bash|python|python3|ruby|perl|node)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public Guardrails(AgentOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Check whether a tool call is allowed. Returns (allowed, reason).
    /// </summary>
    public (bool Allowed, string? Reason) CheckToolCall(string toolName, string arguments)
    {
        // Path restriction: file operations must target the workspace
        if (IsFileOperation(toolName) && !string.IsNullOrEmpty(arguments))
        {
            // Best-effort path extraction — tools use "filePath" or "path" params
            if (TryExtractPath(arguments, out var path))
            {
                // Resolve relative paths against workspace (not CWD) to prevent traversal
                var fullPath = Path.IsPathRooted(path)
                    ? Path.GetFullPath(path)
                    : Path.GetFullPath(Path.Combine(_options.WorkspacePath, path));
                var workspace = Path.GetFullPath(_options.WorkspacePath);
                if (!fullPath.StartsWith(workspace, StringComparison.OrdinalIgnoreCase))
                {
                    return (false, $"Path '{path}' is outside the workspace '{workspace}'.");
                }
            }
        }

        // Command denylist for terminal execution
        if (string.Equals(toolName, "run_bash_command", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(toolName, "create_and_run_task", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var denied in DeniedCommands)
            {
                if (arguments.Contains(denied, StringComparison.OrdinalIgnoreCase))
                {
                    return (false, $"Command contains denied pattern: '{denied}'.");
                }
            }

            // Pipe-to-shell patterns (handles whitespace variants: curl url | sh)
            if (PipeToShellPattern.IsMatch(arguments))
            {
                return (false, "Command contains pipe-to-shell pattern.");
            }
        }

        return (true, null);
    }

    /// <summary>
    /// Check whether the agent has exceeded resource limits.
    /// </summary>
    public (bool Exceeded, string? Reason) CheckLimits(int currentStep, int totalTokens)
    {
        if (currentStep >= _options.MaxSteps)
            return (true, $"Maximum steps ({_options.MaxSteps}) reached.");

        if (totalTokens >= _options.MaxTotalTokens)
            return (true, $"Maximum tokens ({_options.MaxTotalTokens}) reached.");

        return (false, null);
    }

    private static bool IsFileOperation(string toolName) =>
        toolName is "read_file" or "create_file" or "create_directory" or "list_directory"
            or "grep_search" or "replace_string_in_file" or "file_search"
            or "semantic_search";

    private static bool TryExtractPath(string json, out string path)
    {
        path = "";
        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var key in new[] { "filePath", "path", "rootPath" })
            {
                if (doc.RootElement.TryGetProperty(key, out var val) &&
                    val.ValueKind == JsonValueKind.String)
                {
                    path = val.GetString() ?? "";
                    return !string.IsNullOrEmpty(path);
                }
            }
        }
        catch (JsonException) { }
        return false;
    }
}
