using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpServer.Tools;

[McpServerToolType]
public static class InstallExtensionTool
{
    [McpServerTool, Description("Installs a VS Code extension by its identifier.")]
    public static string InstallExtension(
        [Description("The extension identifier (e.g. 'ms-python.python').")] string extensionId)
    {
        throw new NotImplementedException();
    }
}
