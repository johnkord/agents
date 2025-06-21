using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace McpServer.Tools;

[McpServerToolType]
public static class TerminalLastCommandTool
{
    [McpServerTool, Description("Runs a command and returns its output. Simpler alternative to run_bash_command for quick one-liners that don't need background/timeout support.")]
    public static async Task<string> TerminalLastCommand(
        [Description("The shell command to execute.")] string command,
        [Description("The working directory. Optional.")] string? workingDirectory = null)
    {
        var cwd = workingDirectory ?? Directory.GetCurrentDirectory();

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = cwd,
            };
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add(command);

            using var process = Process.Start(psi)!;
            using var cts = new CancellationTokenSource(30_000);

            var stdout = await process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderr = await process.StandardError.ReadToEndAsync(cts.Token);
            await process.WaitForExitAsync(cts.Token);

            var result = $"$ {command}\nExit code: {process.ExitCode}";
            if (!string.IsNullOrWhiteSpace(stdout))
                result += $"\n{stdout.TrimEnd()}";
            if (!string.IsNullOrWhiteSpace(stderr) && process.ExitCode != 0)
                result += $"\nSTDERR: {stderr.TrimEnd()}";

            return result;
        }
        catch (OperationCanceledException)
        {
            return $"Command timed out after 30 seconds: {command}";
        }
        catch (Exception ex)
        {
            return $"Error running command: {ex.Message}";
        }
    }
}
