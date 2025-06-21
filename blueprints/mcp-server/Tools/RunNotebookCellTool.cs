using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpServer.Tools;

[McpServerToolType]
public static class RunNotebookCellTool
{
    [McpServerTool, Description("Runs a notebook cell and returns its output.")]
    public static string RunNotebookCell(
        [Description("The absolute path to the notebook file.")] string filePath,
        [Description("The zero-based index of the cell to run.")] int cellIndex)
    {
        throw new NotImplementedException();
    }
}
