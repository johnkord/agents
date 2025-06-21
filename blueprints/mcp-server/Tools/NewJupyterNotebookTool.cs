using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpServer.Tools;

[McpServerToolType]
public static class NewJupyterNotebookTool
{
    [McpServerTool, Description("Scaffolds a new Jupyter notebook based on a description of what the notebook should contain.")]
    public static string NewJupyterNotebook(
        [Description("A natural language description of the notebook to create.")] string description)
    {
        throw new NotImplementedException();
    }
}
