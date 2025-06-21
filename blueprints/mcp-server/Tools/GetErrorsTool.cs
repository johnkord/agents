using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace McpServer.Tools;

[McpServerToolType]
public static class GetErrorsTool
{
    [McpServerTool, Description("Runs a build and returns compile errors and warnings. For .NET projects, runs 'dotnet build' and extracts structured error/warning information.")]
    public static async Task<string> GetErrors(
        [Description("Path to a project (.csproj) or solution (.sln) to build. Optional — uses current directory if omitted.")] string? projectPath = null,
        [Description("The working directory. Optional.")] string? workingDirectory = null)
    {
        var cwd = workingDirectory ?? Directory.GetCurrentDirectory();
        var target = projectPath ?? ".";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{target}\" --no-restore --verbosity quiet",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = cwd,
            };

            using var process = Process.Start(psi)!;
            using var cts = new CancellationTokenSource(60_000);

            var stdout = await process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderr = await process.StandardError.ReadToEndAsync(cts.Token);
            await process.WaitForExitAsync(cts.Token);

            if (process.ExitCode == 0)
            {
                // Extract warnings even on success
                var warnings = ExtractDiagnostics(stdout, "warning");
                if (warnings.Count > 0)
                    return $"Build succeeded with {warnings.Count} warning(s):\n{string.Join("\n", warnings)}";
                return "Build succeeded. No errors or warnings.";
            }

            var errors = ExtractDiagnostics(stdout, "error");
            var warningsOnFail = ExtractDiagnostics(stdout, "warning");

            var result = $"Build FAILED. {errors.Count} error(s), {warningsOnFail.Count} warning(s).\n";
            if (errors.Count > 0)
                result += $"\nErrors:\n{string.Join("\n", errors.Take(20))}";
            if (warningsOnFail.Count > 0)
                result += $"\nWarnings:\n{string.Join("\n", warningsOnFail.Take(10))}";

            return result;
        }
        catch (OperationCanceledException)
        {
            return "Build timed out after 60 seconds.";
        }
        catch (Exception ex)
        {
            return $"Error running build: {ex.Message}";
        }
    }

    private static List<string> ExtractDiagnostics(string output, string severity)
    {
        return output.Split('\n')
            .Where(l => l.Contains($": {severity} ", StringComparison.OrdinalIgnoreCase))
            .Select(l => l.Trim())
            .Distinct()
            .ToList();
    }
}
