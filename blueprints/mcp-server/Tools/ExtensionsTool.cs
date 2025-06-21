using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpServer.Tools;

[McpServerToolType]
public static class ExtensionsTool
{
    [McpServerTool, Description("Searches for and provides information about VS Code extensions.")]
    public static string SearchExtensions(
        [Description("The search query to find relevant extensions.")] string query)
    {
        throw new NotImplementedException();
    }
}
