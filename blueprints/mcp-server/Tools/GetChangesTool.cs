using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace McpServer.Tools;

[McpServerToolType]
public static class GetChangesTool
{
    [McpServerTool, Description("Gets git status and diff summary showing uncommitted changes in the workspace.")]
    public static async Task<string> GetChanges(
        [Description("The root directory of the git repository. Optional — defaults to current directory.")] string? rootPath = null)
    {
        var cwd = rootPath ?? Directory.GetCurrentDirectory();

        try
        {
            var status = await RunGit("status --short", cwd);
            if (string.IsNullOrWhiteSpace(status))
                return "No uncommitted changes.";

            var diffStat = await RunGit("diff --stat", cwd);
            var result = $"Changed files:\n{status}";
            if (!string.IsNullOrWhiteSpace(diffStat))
                result += $"\nDiff summary:\n{diffStat}";

            return result;
        }
        catch (Exception ex)
        {
            return $"Error: Unable to get git changes. {ex.Message}";
        }
    }

    private static async Task<string> RunGit(string arguments, string cwd)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = cwd,
        };
        using var process = Process.Start(psi)!;
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return output.Trim();
    }
}
