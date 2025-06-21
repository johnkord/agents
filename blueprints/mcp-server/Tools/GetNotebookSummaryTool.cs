using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpServer.Tools;

[McpServerToolType]
public static class GetNotebookSummaryTool
{
    [McpServerTool, Description("Gets the list of notebook cells and their details (cell type, language, execution info, output types).")]
    public static string GetNotebookSummary(
        [Description("The absolute path to the notebook file.")] string filePath)
    {
        throw new NotImplementedException();
    }
}
