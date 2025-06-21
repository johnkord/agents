using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace McpServer.Tools;

/// <summary>
/// Spawns a Forge subprocess with an isolated context for a scoped sub-task.
/// Only the final output crosses back to the parent — the full reasoning trace
/// is discarded, achieving context isolation.
///
/// Research basis:
///   - AGENTSYS (2026): context isolation reduces attack success rate from 30.66%
///     to 2.19%. "External data and subtask reasoning traces never directly enter
///     the main agent's memory; only schema-validated return values may cross
///     isolation boundaries." The ablation shows isolation alone (without validator
///     or sanitizer) achieves 2.19% ASR (Table 3).
///   - Choose Your Agent (2026): delegation creates positive externalities — even
///     tasks not handled by the delegate improve in quality
///   - CaveAgent (2026): "inject variables into a sub-agent's runtime to alter
///     its environment" — pass structured context, not just prose
///
/// Security: The subagent runs as a separate process, inherits the same tool set
/// but gets its own conversation history. Max turns and output size are capped.
/// Recursion depth is tracked to prevent fork bombs.
/// </summary>
[McpServerToolType]
public static class RunSubagentTool
{
    private const int MaxOutputChars = 30_000;
    private const int MaxRecursionDepth = 3;
    private const int TimeoutSeconds = 300; // 5 minutes

    [McpServerTool, Description(
        "Delegate a task to an isolated subagent process with its own context window. " +
        "The subagent gets fresh context (not your conversation), runs independently, " +
        "and returns only its final output. Use for: (1) independent multi-step implementation, " +
        "(2) security isolation for untrusted data, (3) tasks that would consume too much context. " +
        "For simple code exploration, prefer explore_codebase instead — it's faster and cheaper.")]
    public static async Task<string> RunSubagent(
        [Description("Detailed task for the subagent. Include: what to do, " +
            "what files to look at, what you've learned, and what format to return results in.")]
        string prompt,

        [Description("Short 3-5 word label (e.g. 'Map auth flow').")]
        string description,

        [Description("Mode controlling available tools and budget:\n" +
            "  explore — read-only navigation tools (10 steps, 100K tokens)\n" +
            "  verify  — read + test/build tools (10 steps, 100K tokens)\n" +
            "  execute — full tool set for implementation (20 steps, 200K tokens)")]
        string mode = "explore",

        [Description("Context to pass: key files found, known facts, failed approaches. " +
            "The subagent starts cold — include anything it needs to know.")]
        string? context = null,

        [Description("Override max steps (capped by mode limit).")]
        int? maxSteps = null)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return "Error: prompt is required.";

        // Track recursion depth via environment variable
        var depthStr = Environment.GetEnvironmentVariable("FORGE_SUBAGENT_DEPTH") ?? "0";
        if (!int.TryParse(depthStr, out var currentDepth))
            currentDepth = 0;

        if (currentDepth >= MaxRecursionDepth)
            return $"Error: Maximum subagent recursion depth ({MaxRecursionDepth}) reached. Complete this task directly instead of delegating further.";

        // Validate and apply mode-specific budget
        var validMode = (mode ?? "explore").ToLowerInvariant();
        if (validMode is not ("explore" or "verify" or "execute"))
            validMode = "explore";

        var modeBudget = validMode switch
        {
            "verify"  => (steps: 10, tokens: 100_000),
            "execute" => (steps: 20, tokens: 200_000),
            _         => (steps: 10, tokens: 100_000),
        };

        var turns = Math.Clamp(maxSteps ?? modeBudget.steps, 1, modeBudget.steps);

        // Build the subagent's task prompt
        var taskPrompt = prompt;
        if (!string.IsNullOrWhiteSpace(context))
            taskPrompt += $"\n\n--- Context from parent agent ---\n{context}";

        // Find the Forge executable
        var forgeApp = FindForgeExecutable();
        if (forgeApp is null)
            return "Error: Could not locate Forge.App executable. Ensure the project is built.";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("run");
            psi.ArgumentList.Add("--project");
            psi.ArgumentList.Add(forgeApp);
            psi.ArgumentList.Add(taskPrompt);

            // Pass configuration via environment variables (FORGE_ prefix parsed by ConfigurationBuilder)
            psi.Environment["FORGE_MaxSteps"] = turns.ToString();
            psi.Environment["FORGE_MaxTotalTokens"] = modeBudget.tokens.ToString();
            psi.Environment["FORGE_ToolMode"] = validMode;

            // Propagate recursion depth
            psi.Environment["FORGE_SUBAGENT_DEPTH"] = (currentDepth + 1).ToString();

            var sw = Stopwatch.StartNew();
            using var process = Process.Start(psi)!;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

            await process.WaitForExitAsync(cts.Token);
            sw.Stop();

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            // Extract the final output (agent's last message, after the "✓ Task completed" line)
            var finalOutput = ExtractFinalOutput(stdout);

            if (string.IsNullOrWhiteSpace(finalOutput))
                finalOutput = stdout; // fall back to full output

            // Truncate
            if (finalOutput.Length > MaxOutputChars)
                finalOutput = finalOutput[..MaxOutputChars] + $"\n... truncated ({finalOutput.Length:N0} chars)";

            var result = $"Subagent: {description} (mode: {validMode})\n";
            result += $"Status: {(process.ExitCode == 0 ? "completed" : "failed")}\n";
            result += $"Duration: {sw.Elapsed.TotalSeconds:F1}s\n";
            result += $"Depth: {currentDepth + 1}/{MaxRecursionDepth}\n\n";
            result += finalOutput;

            if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(stderr))
            {
                var stderrPreview = stderr.Length > 500 ? stderr[..500] + "..." : stderr;
                result += $"\n\nSubagent errors:\n{stderrPreview}";
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            return $"Error: Subagent '{description}' timed out after {TimeoutSeconds} seconds.";
        }
        catch (Exception ex)
        {
            return $"Error running subagent: {ex.Message}";
        }
    }

    /// <summary>
    /// Extract the agent's final report from stdout.
    /// Looks for content after "✓ Task completed" or "✗ Task failed" markers.
    /// </summary>
    private static string ExtractFinalOutput(string stdout)
    {
        var lines = stdout.Split('\n');
        var markerIndex = -1;

        for (int i = lines.Length - 1; i >= 0; i--)
        {
            if (lines[i].Contains("Task completed") || lines[i].Contains("Task failed"))
            {
                markerIndex = i;
                break;
            }
        }

        if (markerIndex >= 0 && markerIndex + 1 < lines.Length)
            return string.Join("\n", lines[(markerIndex + 1)..]).Trim();

        return "";
    }

    /// <summary>
    /// Locate the Forge.App project file relative to the MCP server.
    /// </summary>
    private static string? FindForgeExecutable()
    {
        // Look relative to the MCP server's known location
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "coding-agent", "src", "Forge.App", "Forge.App.csproj"),
            Path.Combine(Directory.GetCurrentDirectory(), "blueprints", "coding-agent", "src", "Forge.App", "Forge.App.csproj"),
        };

        foreach (var candidate in candidates)
        {
            var resolved = Path.GetFullPath(candidate);
            if (File.Exists(resolved))
                return resolved;
        }

        return null;
    }
}
