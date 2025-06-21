using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpServer.Tools;

[McpServerToolType]
public static class FindUsagesTool
{
    [McpServerTool, Description("Finds all references, implementations, and definitions of a symbol. Combination of 'Find All References', 'Find Implementation', and 'Go to Definition'.")]
    public static string FindUsages(
        [Description("The symbol name to find usages for.")] string symbol,
        [Description("The file path where the symbol is defined, to disambiguate.")] string? filePath = null)
    {
        throw new NotImplementedException();
    }
}
