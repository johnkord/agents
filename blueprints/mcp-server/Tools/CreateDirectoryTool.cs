using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpServer.Tools;

[McpServerToolType]
public static class CreateDirectoryTool
{
    [McpServerTool, Description("Creates a directory at the specified path, creating parent directories if needed.")]
    public static string CreateDirectory(
        [Description("The absolute path of the directory to create.")] string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(fullPath);
            return $"Directory created: {fullPath}";
        }
        catch (Exception ex)
        {
            return $"Error: Unable to create directory '{path}'. {ex.Message}";
        }
    }
}
