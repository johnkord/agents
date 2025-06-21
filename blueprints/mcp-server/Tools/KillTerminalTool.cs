using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace McpServer.Tools;

[McpServerToolType]
public static class KillTerminalTool
{
    [McpServerTool, Description("Terminates a running process by its PID. Use this to stop background processes started with run_bash_command.")]
    public static string KillTerminal(
        [Description("The process ID (PID) to terminate.")] string id)
    {
        if (!int.TryParse(id, out var pid))
            return $"Error: '{id}' is not a valid process ID.";

        try
        {
            var process = Process.GetProcessById(pid);
            process.Kill(entireProcessTree: true);
            return $"Process {pid} ({process.ProcessName}) killed successfully.";
        }
        catch (ArgumentException)
        {
            return $"Process {pid} is not running (already exited).";
        }
        catch (Exception ex)
        {
            return $"Error: Unable to kill process {pid}. {ex.Message}";
        }
    }
}
