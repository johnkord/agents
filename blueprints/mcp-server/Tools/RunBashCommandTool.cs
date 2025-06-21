using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace McpServer.Tools;

[McpServerToolType]
public static class RunBashCommandTool
{
    [McpServerTool, Description("Executes a bash command on Linux. Returns stdout and stderr. Use for running builds, tests, installs, and other shell commands.")]
    public static async Task<string> RunBashCommand(
        [Description("The shell command to execute.")] string command,
        [Description("A one-sentence description of what the command does.")] string explanation,
        [Description("Whether the command starts a background process. If true, returns immediately.")] bool isBackground,
        [Description("The working directory to run the command in. Optional — defaults to the server's current directory.")] string? workingDirectory = null,
        [Description("Optional timeout in milliseconds. Use 0 or omit for default (60 seconds).")] int? timeout = null)
    {
        var effectiveTimeout = timeout is > 0 ? timeout.Value : 60_000;
        var cwd = workingDirectory ?? Directory.GetCurrentDirectory();

        var psi = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = cwd,
        };
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add(command);

        if (isBackground)
        {
            // Don't redirect streams for background processes — avoids SIGPIPE
            // when the Process handle is disposed while the child still writes.
            var bgProcess = new Process { StartInfo = psi };
            bgProcess.Start();
            return $"Background process started (PID: {bgProcess.Id}): {explanation}";
        }

        // Foreground: redirect streams and wait
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        using var process = new Process { StartInfo = psi };
        process.Start();

        using var cts = new CancellationTokenSource(effectiveTimeout);
        try
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);
            await process.WaitForExitAsync(cts.Token);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            var result = $"Exit code: {process.ExitCode}";
            if (!string.IsNullOrWhiteSpace(stdout))
                result += $"\n\nSTDOUT:\n{Truncate(stdout, 4000)}";
            if (!string.IsNullOrWhiteSpace(stderr))
                result += $"\n\nSTDERR:\n{Truncate(stderr, 2000)}";

            return result;
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return $"Command timed out after {effectiveTimeout}ms and was killed.";
        }
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength] + $"\n... ({text.Length - maxLength} characters truncated)";
}
