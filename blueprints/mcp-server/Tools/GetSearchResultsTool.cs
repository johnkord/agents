using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpServer.Tools;

[McpServerToolType]
public static class GetSearchResultsTool
{
    [McpServerTool, Description("Gets the search results from the Search view, returning matching files and line content.")]
    public static string GetSearchResults(
        [Description("The search query to retrieve results for.")] string query)
    {
        throw new NotImplementedException();
    }
}
