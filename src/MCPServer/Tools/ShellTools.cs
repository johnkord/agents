using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MCPServer.ToolApproval;
using MCPServer.Logging;                       // +++
using System.Collections.Generic;          // + new

namespace MCPServer.Tools;

[McpServerToolType]
public class ShellTools
{
    [McpServerTool(Name = "run_command"), Description("Run a shell command and capture its output.")]
    public static string RunCommand(
        string script)
    {
        // NEW – approval gate
        if (!ApprovalServiceClient.EnsureApproved(
                "run_command",
                new() { ["script"] = script }))
        {
            return "Error: request denied by approval service.";
        }
        
        ToolLogger.LogStart("run_command");
        try
        {
            // Always execute through shell to support full bash functionality including pipes, redirections, etc.
            // Use the script parameter directly since it contains the full command
            string fullCommand = script;
            string command;
            string arguments;

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
    
            var timeoutSeconds = 60; // TODO: make configurable
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
            ToolLogger.LogError("run_command", ex);
            return $"Error executing command: {ex.Message}";
        }
        finally
        {
            ToolLogger.LogEnd("run_command");
        }
    }

    [McpServerTool(Name = "get_working_directory"), Description("Return the current working directory.")]
    public static string GetWorkingDirectory()
    {
        ToolLogger.LogStart("get_working_directory");
        try
        {
            return $"Working directory: {Environment.CurrentDirectory}";
        }
        catch (Exception ex)
        {
            ToolLogger.LogError("get_working_directory", ex);
            return $"Error getting working directory: {ex.Message}";
        }
        finally
        {
            ToolLogger.LogEnd("get_working_directory");
        }
    }
}
