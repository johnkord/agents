using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MCPServer.ToolApproval; // +import

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
        try
        {
            // Detect the correct shell executable if user only passed a single string (Unix)
            if (string.IsNullOrWhiteSpace(arguments))
            {
                // On Linux/macOS we can call bash -c "<command>"
                // On Windows we fall back to powershell -Command "<command>"
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    arguments = $"-Command \"{command}\"";
                    command   = "powershell";
                }
                else
                {
                    arguments = $"-c \"{command}\"";
                    command   = "/bin/bash";
                }
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
