using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace McpServer.Tools;

[McpServerToolType]
public static class TestFailureTool
{
    [McpServerTool, Description("Gets detailed information about the most recent test failures. Runs 'dotnet test' and extracts failed test names, error messages, and stack traces.")]
    public static async Task<string> TestFailure(
        [Description("Path to a test project (.csproj). Optional — auto-discovers if omitted.")] string? projectPath = null,
        [Description("Filter to run specific tests. Optional.")] string? filter = null,
        [Description("The working directory. Optional.")] string? workingDirectory = null)
    {
        var cwd = workingDirectory ?? Directory.GetCurrentDirectory();

        // Find test project if not specified
        if (string.IsNullOrEmpty(projectPath))
        {
            var testProjects = Directory.EnumerateFiles(cwd, "*.csproj", SearchOption.AllDirectories)
                .Where(f => { try { return File.ReadAllText(f).Contains("Microsoft.NET.Test.Sdk"); } catch { return false; } })
                .Take(5)
                .ToList();

            if (testProjects.Count == 0)
                return "No test projects found.";
            if (testProjects.Count == 1)
                projectPath = testProjects[0];
            else
                return $"Multiple test projects found. Specify one:\n{string.Join("\n", testProjects.Select(p => $"  • {Path.GetRelativePath(cwd, p)}"))}";
        }

        var args = $"test \"{projectPath}\" --no-restore --verbosity normal --logger \"console;verbosity=detailed\"";
        if (!string.IsNullOrEmpty(filter))
            args += $" --filter \"{filter}\"";

        try
        {
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

            using var process = Process.Start(psi)!;
            using var cts = new CancellationTokenSource(120_000);

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);
            await process.WaitForExitAsync(cts.Token);
            var stdout = await stdoutTask;
            _ = await stderrTask; // drain stderr to prevent deadlock

            if (process.ExitCode == 0)
                return "All tests passed. No failures to report.";

            // Extract failure details
            var lines = stdout.Split('\n');
            var failureBlocks = new List<string>();
            var inFailure = false;
            var currentBlock = new List<string>();

            foreach (var line in lines)
            {
                if (line.Contains("[FAIL]"))
                {
                    if (currentBlock.Count > 0)
                        failureBlocks.Add(string.Join("\n", currentBlock));
                    currentBlock = [line.Trim()];
                    inFailure = true;
                }
                else if (inFailure && (line.TrimStart().StartsWith("Error Message") ||
                    line.TrimStart().StartsWith("Stack Trace") ||
                    line.TrimStart().StartsWith("at ") ||
                    line.TrimStart().StartsWith("Assert.")))
                {
                    currentBlock.Add(line.Trim());
                }
                else if (inFailure && string.IsNullOrWhiteSpace(line))
                {
                    inFailure = false;
                }
            }
            if (currentBlock.Count > 0)
                failureBlocks.Add(string.Join("\n", currentBlock));

            if (failureBlocks.Count == 0)
            {
                // Fallback: just show lines with FAIL
                var failLines = lines.Where(l => l.Contains("[FAIL]") || l.Contains("Error Message")).Take(20);
                return $"Tests FAILED (exit code {process.ExitCode}):\n{string.Join("\n", failLines)}";
            }

            var result = $"{failureBlocks.Count} test failure(s):\n\n";
            result += string.Join("\n\n", failureBlocks.Take(10));
            return result;
        }
        catch (OperationCanceledException)
        {
            return "Test run timed out after 120 seconds.";
        }
        catch (Exception ex)
        {
            return $"Error running tests: {ex.Message}";
        }
    }
}
