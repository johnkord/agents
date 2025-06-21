using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpServer.Tools;

[McpServerToolType]
public static class NewWorkspaceTool
{
    [McpServerTool, Description("Scaffolds a new workspace or project, preconfigured with debug and run configurations.")]
    public static string NewWorkspace(
        [Description("A natural language description of the workspace or project type to create.")] string description)
    {
        throw new NotImplementedException();
    }
}
