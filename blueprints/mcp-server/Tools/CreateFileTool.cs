using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpServer.Tools;

[McpServerToolType]
public static class CreateFileTool
{
    [McpServerTool, Description("Creates a new file with the specified content. The directory will be created if it does not exist. Cannot be used to overwrite existing files.")]
    public static string CreateFile(
        [Description("The absolute path to the file to create.")] string filePath,
        [Description("The content to write to the file.")] string content)
    {
        try
        {
            var fullPath = Path.GetFullPath(filePath);

            if (File.Exists(fullPath))
            {
                return $"Error: File already exists at '{fullPath}'. Use ReplaceStringInFile to edit existing files.";
            }

            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(fullPath, content);
            return $"File created: {fullPath} ({content.Length} characters)";
        }
        catch (Exception ex)
        {
            return $"Error: Unable to create file '{filePath}'. {ex.Message}";
        }
    }
}
