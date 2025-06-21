using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpServer.Tools;

[McpServerToolType]
public static class RunVscodeCommandTool
{
    [McpServerTool, Description("Runs a VS Code command by its identifier (e.g. 'workbench.action.toggleZenMode').")]
    public static string RunVscodeCommand(
        [Description("The VS Code command identifier to execute.")] string command,
        [Description("Optional arguments to pass to the command.")] string? args = null)
    {
        throw new NotImplementedException();
    }
}
