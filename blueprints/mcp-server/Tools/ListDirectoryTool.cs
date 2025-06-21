using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpServer.Tools;

[McpServerToolType]
public static class ListDirectoryTool
{
    [McpServerTool, Description("Lists the contents of a directory. Returns child names; names ending in '/' are folders, otherwise files.")]
    public static string ListDirectory(
        [Description("The absolute path to the directory to list.")] string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);

            if (!Directory.Exists(fullPath))
            {
                return $"Error: Directory not found at '{fullPath}'.";
            }

            var entries = new List<string>();
            foreach (var dir in Directory.GetDirectories(fullPath))
            {
                entries.Add(Path.GetFileName(dir) + "/");
            }
            foreach (var file in Directory.GetFiles(fullPath))
            {
                entries.Add(Path.GetFileName(file));
            }

            if (entries.Count == 0)
            {
                return "(empty directory)";
            }

            return string.Join("\n", entries);
        }
        catch (Exception ex)
        {
            return $"Error: Unable to list directory '{path}'. {ex.Message}";
        }
    }
}
