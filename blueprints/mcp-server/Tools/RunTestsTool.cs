using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace McpServer.Tools;

[McpServerToolType]
public static class RunTestsTool
{
    [McpServerTool, Description("Runs .NET unit tests and returns structured pass/fail results. Searches for test projects automatically, or you can specify a path.")]
    public static async Task<string> RunTests(
        [Description("Path to a test project (.csproj) or solution (.sln). Optional — searches for test projects if omitted.")] string? projectPath = null,
        [Description("Filter expression to run specific tests (e.g. 'ClassName.MethodName' or 'Category=Unit'). Optional.")] string? filter = null,
        [Description("The working directory to run from. Optional.")] string? workingDirectory = null)
    {
        var cwd = workingDirectory ?? Directory.GetCurrentDirectory();

        // Find test project(s) if not specified
        if (string.IsNullOrEmpty(projectPath))
        {
            var testProjects = Directory.EnumerateFiles(cwd, "*.csproj", SearchOption.AllDirectories)
                .Where(f => { try { return File.ReadLines(f).Any(l => l.Contains("Microsoft.NET.Test.Sdk")); } catch { return false; } })
                .Take(5)
                .ToList();

            if (testProjects.Count == 0)
                return "No test projects found. Specify a projectPath or ensure a .csproj with Microsoft.NET.Test.Sdk exists.";

            if (testProjects.Count == 1)
                projectPath = testProjects[0];
            else
                return $"Multiple test projects found. Specify one:\n{string.Join("\n", testProjects.Select(p => $"  • {Path.GetRelativePath(cwd, p)}"))}";
        }

        // Build the dotnet test command
        var args = $"test \"{projectPath}\" --no-restore --verbosity minimal";
        if (!string.IsNullOrEmpty(filter))
            args += $" --filter \"{filter}\"";

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = cwd,
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        using var cts = new CancellationTokenSource(120_000); // 2 min timeout
        try
        {
            var stdout = await process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderr = await process.StandardError.ReadToEndAsync(cts.Token);
            await process.WaitForExitAsync(cts.Token);

            var result = $"Exit code: {process.ExitCode}\n";

            // Extract the summary line (e.g., "Passed! - Failed: 0, Passed: 88, ...")
            var summaryLine = stdout.Split('\n')
                .LastOrDefault(l => l.Contains("Passed") || l.Contains("Failed"))?.Trim();

            if (summaryLine is not null)
                result += $"Summary: {summaryLine}\n";

            result += $"\nTests {(process.ExitCode == 0 ? "PASSED" : "FAILED")}";

            if (process.ExitCode != 0)
            {
                // Include failure details
                var failureLines = stdout.Split('\n')
                    .Where(l => l.Contains("[FAIL]") || l.Contains("Error Message") || l.Contains("Stack Trace"))
                    .Take(20);
                var failures = string.Join("\n", failureLines);
                if (!string.IsNullOrEmpty(failures))
                    result += $"\n\nFailures:\n{failures}";
            }

            if (!string.IsNullOrWhiteSpace(stderr) && process.ExitCode != 0)
                result += $"\n\nSTDERR:\n{Truncate(stderr, 1000)}";

            return result;
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return "Tests timed out after 120 seconds and were killed.";
        }
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength] + $"\n... ({text.Length - maxLength} characters truncated)";
}
