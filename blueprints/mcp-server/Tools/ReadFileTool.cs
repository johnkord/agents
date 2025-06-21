using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpServer.Tools;

[McpServerToolType]
public static class ReadFileTool
{
    [McpServerTool, Description("Reads the contents of a file at the specified path. For large files, use startLine and endLine to read a specific range.")]
    public static async Task<string> ReadFile(
        [Description("The absolute path to the file to read.")] string filePath,
        [Description("The 1-based line number to start reading from. Optional.")] int? startLine = null,
        [Description("The inclusive 1-based line number to stop reading at. Optional.")] int? endLine = null)
    {
        try
        {
            var fullPath = Path.GetFullPath(filePath);

            if (!File.Exists(fullPath))
            {
                return $"Error: File not found at '{fullPath}'.";
            }

            if (startLine is null && endLine is null)
            {
                return await File.ReadAllTextAsync(fullPath);
            }

            var lines = await File.ReadAllLinesAsync(fullPath);
            int start = Math.Max(1, startLine ?? 1) - 1; // convert to 0-based
            int end = Math.Min(lines.Length, endLine ?? lines.Length); // inclusive, 1-based

            if (start >= lines.Length)
            {
                return $"Error: startLine {startLine} is beyond end of file ({lines.Length} lines).";
            }

            var selected = lines[start..end];
            var header = $"Lines {start + 1}-{end} of {lines.Length}:\n";
            return header + string.Join("\n", selected);
        }
        catch (Exception ex)
        {
            return $"Error: Unable to read file '{filePath}'. {ex.Message}";
        }
    }
}
