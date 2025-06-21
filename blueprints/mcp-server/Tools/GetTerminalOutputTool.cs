using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace McpServer.Tools;

[McpServerToolType]
public static class GetTerminalOutputTool
{
    [McpServerTool, Description("Gets the status and any available output of a process by its PID. Use this to check on background processes started with run_bash_command.")]
    public static string GetTerminalOutput(
        [Description("The process ID (PID) to check.")] string id)
    {
        if (!int.TryParse(id, out var pid))
            return $"Error: '{id}' is not a valid process ID.";

        try
        {
            var process = Process.GetProcessById(pid);
            return $"Process {pid} ({process.ProcessName}) is still running.";
        }
        catch (ArgumentException)
        {
            return $"Process {pid} has exited (no longer running).";
        }
        catch (Exception ex)
        {
            return $"Error: Unable to check process {pid}. {ex.Message}";
        }
    }
}
