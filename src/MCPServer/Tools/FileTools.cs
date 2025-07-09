using ModelContextProtocol.Server;
using System.ComponentModel;
using MCPServer.ToolApproval;
using MCPServer.Logging;                       // +++
using System.Collections.Generic;              // NEW

namespace MCPServer.Tools;

[McpServerToolType]
public class FileTools
{
    [McpServerTool(Name = "read_file"), Description("Read the contents of a file. Set 'raw' to true to return only the content.")]
    public static string ReadFile(string filePath, bool raw = false)
    {
        ToolLogger.LogStart("read_file");
        try
        {
            if (!File.Exists(filePath))
                return $"Error: File '{filePath}' does not exist";

            var content = File.ReadAllText(filePath);
            return raw ? content
                       : $"Successfully read file '{filePath}'. Content:\n{content}";
        }
        catch (Exception ex)
        {
            ToolLogger.LogError("read_file", ex);
            return $"Error reading file '{filePath}': {ex.Message}";
        }
        finally
        {
            ToolLogger.LogEnd("read_file");
        }
    }

    [McpServerTool(Name = "write_file"), Description("Write text content to a file.")]
    public static string WriteFile(string filePath, string content)
    {
        // approval gate
        if (!ToolApprovalManager.Instance.EnsureApproved(
                "write_file",
                new Dictionary<string, object?> { ["filePath"] = filePath, ["contentLength"] = content.Length }))
            return "Error: Tool execution was denied by approval system.";

        ToolLogger.LogStart("write_file");
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
            ToolLogger.LogError("write_file", ex);
            return $"Error writing to file '{filePath}': {ex.Message}";
        }
        finally
        {
            ToolLogger.LogEnd("write_file");
        }
    }

    [McpServerTool(Name = "list_directory"), Description("List files and directories in a given path.")]
    public static string ListDirectory(string directoryPath)
    {
        ToolLogger.LogStart("list_directory");
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
            ToolLogger.LogError("list_directory", ex);
            return $"Error listing directory '{directoryPath}': {ex.Message}";
        }
        finally
        {
            ToolLogger.LogEnd("list_directory");
        }
    }

    [McpServerTool(Name = "file_exists"), Description("Check if a file exists at the given path.")]
    public static string FileExists(string filePath)
    {
        ToolLogger.LogStart("file_exists");
        try
        {
            bool exists = File.Exists(filePath);
            return $"File '{filePath}' {(exists ? "exists" : "does not exist")}";
        }
        catch (Exception ex)
        {
            ToolLogger.LogError("file_exists", ex);
            return $"Error checking file existence '{filePath}': {ex.Message}";
        }
        finally
        {
            ToolLogger.LogEnd("file_exists");
        }
    }

    [McpServerTool(Name = "delete_file"), Description("Delete a file at the given path.")]
    public static string DeleteFile(string filePath)
    {
        if (!ToolApprovalManager.Instance.EnsureApproved(
                "delete_file",
                new Dictionary<string, object?> { ["filePath"] = filePath }))
            return "Error: Tool execution was denied by approval system.";

        ToolLogger.LogStart("delete_file");
        try
        {
            if (!File.Exists(filePath))
                return $"Error: File '{filePath}' does not exist";

            File.Delete(filePath);
            return $"Successfully deleted file '{filePath}'";
        }
        catch (Exception ex)
        {
            ToolLogger.LogError("delete_file", ex);
            return $"Error deleting file '{filePath}': {ex.Message}";
        }
        finally
        {
            ToolLogger.LogEnd("delete_file");
        }
    }

    [McpServerTool(Name = "create_directory"), Description("Create a new directory at the given path.")]
    public static string CreateDirectory(string directoryPath)
    {
        if (!ToolApprovalManager.Instance.EnsureApproved(
                "create_directory",
                new Dictionary<string, object?> { ["directoryPath"] = directoryPath }))
            return "Error: Tool execution was denied by approval system.";

        ToolLogger.LogStart("create_directory");
        try
        {
            if (Directory.Exists(directoryPath))
                return $"Directory '{directoryPath}' already exists";

            Directory.CreateDirectory(directoryPath);
            return $"Successfully created directory '{directoryPath}'";
        }
        catch (Exception ex)
        {
            ToolLogger.LogError("create_directory", ex);
            return $"Error creating directory '{directoryPath}': {ex.Message}";
        }
        finally
        {
            ToolLogger.LogEnd("create_directory");
        }
    }

    [McpServerTool(Name = "get_file_info"), Description("Get information about a file (size, creation date, etc.).")]
    public static string GetFileInfo(string filePath)
    {
        ToolLogger.LogStart("get_file_info");
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
            ToolLogger.LogError("get_file_info", ex);
            return $"Error getting file info for '{filePath}': {ex.Message}";
        }
        finally
        {
            ToolLogger.LogEnd("get_file_info");
        }
    }
}