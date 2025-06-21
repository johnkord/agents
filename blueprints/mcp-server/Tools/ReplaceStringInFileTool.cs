using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpServer.Tools;

[McpServerToolType]
public static class ReplaceStringInFileTool
{
    [McpServerTool, Description("Replaces exactly one occurrence of a string in an existing file. Include surrounding context lines to uniquely identify the target text.")]
    public static string ReplaceStringInFile(
        [Description("The absolute path to the file to edit.")] string filePath,
        [Description("The exact literal text to find and replace. Must match exactly one location in the file.")] string oldString,
        [Description("The exact literal text to replace oldString with.")] string newString)
    {
        var fullPath = Path.GetFullPath(filePath);

        if (!File.Exists(fullPath))
        {
            return $"Error: File not found at '{fullPath}'.";
        }

        var content = File.ReadAllText(fullPath);
        var count = CountOccurrences(content, oldString);

        if (count == 0)
        {
            return $"Error: oldString not found in '{fullPath}'. Make sure the text matches exactly, including whitespace and indentation.";
        }

        if (count > 1)
        {
            return $"Error: oldString found {count} times in '{fullPath}'. Include more surrounding context to uniquely identify the target.";
        }

        var newContent = content.Replace(oldString, newString);

        // Atomic write: write to temp then rename to prevent partial-write corruption
        var tmpPath = fullPath + ".tmp";
        File.WriteAllText(tmpPath, newContent);
        File.Move(tmpPath, fullPath, overwrite: true);

        return $"Replaced 1 occurrence in {fullPath}.";
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int idx = 0;
        while ((idx = text.IndexOf(pattern, idx, StringComparison.Ordinal)) != -1)
        {
            count++;
            idx += pattern.Length;
        }
        return count;
    }
}
