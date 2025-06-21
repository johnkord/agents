using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpServer.Tools;

[McpServerToolType]
public static class RenameSymbolTool
{
    [McpServerTool, Description("Renames a symbol (variable, function, class, etc.) across the entire codebase using the language server's rename functionality.")]
    public static string RenameSymbol(
        [Description("The file path where the symbol is located.")] string filePath,
        [Description("The line number of the symbol (1-based).")] int line,
        [Description("The column number of the symbol (1-based).")] int column,
        [Description("The new name for the symbol.")] string newName)
    {
        throw new NotImplementedException();
    }
}
