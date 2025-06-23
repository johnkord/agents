using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using MCPServer.ToolApproval;
using System.Collections.Generic;

namespace MCPServer.Tools;

[McpServerToolType]
public class FileTools
{
    [McpServerTool(Name = "read_file"), Description("Read the contents of a file.")]
    [RequiresApproval(false)]
    public static string ReadFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return $"Error: File '{filePath}' does not exist";

            var content = File.ReadAllText(filePath);
            return $"Successfully read file '{filePath}'. Content:\n{content}";
        }
        catch (Exception ex)
        {
            return $"Error reading file '{filePath}': {ex.Message}";
        }
    }

    [McpServerTool(Name = "write_file"), Description("Write text content to a file.")]
    [RequiresApproval] // writes data
    public static string WriteFile(string filePath, string content)
    {
        // Check for approval before executing the dangerous operation
        var args = new Dictionary<string, object?>
        {
            ["filePath"] = filePath,
            ["content"] = content
        };

        var approved = ToolApprovalManager.Instance.EnsureApproved("write_file", args);
        if (!approved)
        {
            return "Error: Tool execution was denied by approval system.";
        }

        try
        {
            // Ensure the directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, content);
            return $"Successfully wrote {content.Length} characters to file '{filePath}'";
        }
        catch (Exception ex)
        {
            return $"Error writing to file '{filePath}': {ex.Message}";
        }
    }

    [McpServerTool(Name = "list_directory"), Description("List files and directories in a given path.")]
    [RequiresApproval(false)]
    public static string ListDirectory(string directoryPath)
    {
        try
        {
            if (!Directory.Exists(directoryPath))
                return $"Error: Directory '{directoryPath}' does not exist";

            var entries = new List<string>();
            
            // Add directories first
            var directories = Directory.GetDirectories(directoryPath)
                .Select(d => $"[DIR]  {Path.GetFileName(d)}")
                .OrderBy(d => d);
            entries.AddRange(directories);

            // Add files
            var files = Directory.GetFiles(directoryPath)
                .Select(f => $"[FILE] {Path.GetFileName(f)}")
                .OrderBy(f => f);
            entries.AddRange(files);

            return $"Contents of directory '{directoryPath}':\n{string.Join("\n", entries)}";
        }
        catch (Exception ex)
        {
            return $"Error listing directory '{directoryPath}': {ex.Message}";
        }
    }

    [McpServerTool(Name = "file_exists"), Description("Check if a file exists at the given path.")]
    [RequiresApproval(false)]
    public static string FileExists(string filePath)
    {
        try
        {
            bool exists = File.Exists(filePath);
            return $"File '{filePath}' {(exists ? "exists" : "does not exist")}";
        }
        catch (Exception ex)
        {
            return $"Error checking file existence '{filePath}': {ex.Message}";
        }
    }

    [McpServerTool(Name = "delete_file"), Description("Delete a file at the given path.")]
    [RequiresApproval] // destructive
    public static string DeleteFile(string filePath)
    {
        // Check for approval before executing the dangerous operation
        var args = new Dictionary<string, object?>
        {
            ["filePath"] = filePath
        };

        var approved = ToolApprovalManager.Instance.EnsureApproved("delete_file", args);
        if (!approved)
        {
            return "Error: Tool execution was denied by approval system.";
        }

        try
        {
            if (!File.Exists(filePath))
                return $"Error: File '{filePath}' does not exist";

            File.Delete(filePath);
            return $"Successfully deleted file '{filePath}'";
        }
        catch (Exception ex)
        {
            return $"Error deleting file '{filePath}': {ex.Message}";
        }
    }

    [McpServerTool(Name = "create_directory"), Description("Create a new directory at the given path.")]
    [RequiresApproval] // creates data
    public static string CreateDirectory(string directoryPath)
    {
        // Check for approval before executing the dangerous operation
        var args = new Dictionary<string, object?>
        {
            ["directoryPath"] = directoryPath
        };

        var approved = ToolApprovalManager.Instance.EnsureApproved("create_directory", args);
        if (!approved)
        {
            return "Error: Tool execution was denied by approval system.";
        }

        try
        {
            if (Directory.Exists(directoryPath))
                return $"Directory '{directoryPath}' already exists";

            Directory.CreateDirectory(directoryPath);
            return $"Successfully created directory '{directoryPath}'";
        }
        catch (Exception ex)
        {
            return $"Error creating directory '{directoryPath}': {ex.Message}";
        }
    }

    [McpServerTool(Name = "get_file_info"), Description("Get information about a file (size, creation date, etc.).")]
    [RequiresApproval(false)]
    public static string GetFileInfo(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return $"Error: File '{filePath}' does not exist";

            var fileInfo = new FileInfo(filePath);
            var info = new
            {
                Name = fileInfo.Name,
                FullPath = fileInfo.FullName,
                Size = fileInfo.Length,
                CreatedAt = fileInfo.CreationTime.ToString("yyyy-MM-dd HH:mm:ss"),
                ModifiedAt = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                Extension = fileInfo.Extension,
                IsReadOnly = fileInfo.IsReadOnly
            };

            return $"File information for '{filePath}':\n" +
                   $"Name: {info.Name}\n" +
                   $"Size: {info.Size} bytes\n" +
                   $"Created: {info.CreatedAt}\n" +
                   $"Modified: {info.ModifiedAt}\n" +
                   $"Extension: {info.Extension}\n" +
                   $"Read-only: {info.IsReadOnly}";
        }
        catch (Exception ex)
        {
            return $"Error getting file info for '{filePath}': {ex.Message}";
        }
    }
}