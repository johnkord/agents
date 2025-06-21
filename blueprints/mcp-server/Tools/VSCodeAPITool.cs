using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpServer.Tools;

[McpServerToolType]
public static class VSCodeAPITool
{
    [McpServerTool, Description("Answers questions about VS Code functionality and extension development APIs.")]
    public static string VSCodeAPI(
        [Description("The question about VS Code functionality or extension APIs.")] string question)
    {
        throw new NotImplementedException();
    }
}
