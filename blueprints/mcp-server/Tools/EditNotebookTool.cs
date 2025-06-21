using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpServer.Tools;

[McpServerToolType]
public static class EditNotebookTool
{
    [McpServerTool, Description("Makes edits to a Jupyter notebook, such as adding, removing, or modifying cells.")]
    public static string EditNotebook(
        [Description("The absolute path to the notebook file.")] string filePath,
        [Description("A description of the edits to make to the notebook.")] string edits)
    {
        throw new NotImplementedException();
    }
}
