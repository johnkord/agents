using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace McpServer.Tools;

[McpServerToolType]
public static class AwaitTerminalTool
{
    [McpServerTool, Description("Waits for a background process to complete and returns whether it exited. Use to check if a background process started with run_bash_command has finished.")]
    public static async Task<string> AwaitTerminal(
        [Description("The process ID (PID) to wait for.")] string id,
        [Description("Maximum milliseconds to wait. Defaults to 30000 (30 seconds).")] int? timeout = null)
    {
        if (!int.TryParse(id, out var pid))
            return $"Error: '{id}' is not a valid process ID.";

        var effectiveTimeout = timeout ?? 30_000;

        try
        {
            var process = Process.GetProcessById(pid);
            using var cts = new CancellationTokenSource(effectiveTimeout);

            try
            {
                await process.WaitForExitAsync(cts.Token);
                return $"Process {pid} exited with code {process.ExitCode}.";
            }
            catch (OperationCanceledException)
            {
                return $"Process {pid} is still running after {effectiveTimeout}ms timeout.";
            }
        }
        catch (ArgumentException)
        {
            return $"Process {pid} has already exited.";
        }
        catch (Exception ex)
        {
            return $"Error: Unable to wait for process {pid}. {ex.Message}";
        }
    }
}
