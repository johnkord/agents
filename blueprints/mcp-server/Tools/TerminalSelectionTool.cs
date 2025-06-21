using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpServer.Tools;

[McpServerToolType]
public static class TerminalSelectionTool
{
    [McpServerTool, Description("Gets the current terminal selection text.")]
    public static string TerminalSelection()
    {
        throw new NotImplementedException();
    }
}
