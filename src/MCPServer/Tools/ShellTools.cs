using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MCPServer.ToolApproval; // +import
using System.Collections.Generic; // +import for Dictionary

namespace MCPServer.Tools;

[McpServerToolType]
public class ShellTools
{
    [McpServerTool(Name = "run_command"), Description("Run a shell command and capture its output.")]
    [RequiresApproval] // dangerous
    public static string RunCommand(
        string command,
        string arguments = "",
        int timeoutSeconds = 30)
    {
        // Check for approval before executing the dangerous operation
        var args = new Dictionary<string, object?>
        {
            ["command"] = command,
            ["arguments"] = arguments,
            ["timeoutSeconds"] = timeoutSeconds
        };

        var approved = ToolApprovalManager.Instance.EnsureApproved("run_command", args);
        if (!approved)
        {
            return "Error: Tool execution was denied by approval system.";
        }

        try
        {
            // Always execute through shell to support full bash functionality including pipes, redirections, etc.
            // Combine command and arguments into a single shell command string
            string fullCommand = string.IsNullOrWhiteSpace(arguments) 
                ? command 
                : $"{command} {arguments}";

            // Execute through appropriate shell based on platform
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                arguments = $"-Command \"{fullCommand}\"";
                command   = "powershell";
            }
            else
            {
                arguments = $"-c \"{fullCommand}\"";
                command   = "/bin/bash";
            }

            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName               = command,
                    Arguments              = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                }
            };

            proc.Start();

            if (!proc.WaitForExit(timeoutSeconds * 1000))
            {
                try { proc.Kill(); } catch { /* ignore */ }
                return $"Error: Command timed out after {timeoutSeconds} s";
            }

            var stdout = proc.StandardOutput.ReadToEnd();
            var stderr = proc.StandardError.ReadToEnd();

            return $"Exit code: {proc.ExitCode}\n" +
                   $"STDOUT:\n{stdout}\n" +
                   $"STDERR:\n{stderr}";
        }
        catch (Exception ex)
        {
            return $"Error executing command: {ex.Message}";
        }
    }
}
