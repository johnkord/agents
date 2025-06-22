using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MCPServer.Tools;

[McpServerToolType]
public class SystemTools
{
    [McpServerTool(Name = "get_current_time"), Description("Get the current date and time.")]
    public static string GetCurrentTime()
    {
        try
        {
            var now = DateTime.Now;
            var utcNow = DateTime.UtcNow;
            
            return $"Current local time: {now:yyyy-MM-dd HH:mm:ss}\n" +
                   $"Current UTC time: {utcNow:yyyy-MM-dd HH:mm:ss}\n" +
                   $"Time zone: {TimeZoneInfo.Local.DisplayName}";
        }
        catch (Exception ex)
        {
            return $"Error getting current time: {ex.Message}";
        }
    }

    [McpServerTool(Name = "get_system_info"), Description("Get basic system information.")]
    public static string GetSystemInfo()
    {
        try
        {
            var info = new
            {
                OS = RuntimeInformation.OSDescription,
                Architecture = RuntimeInformation.OSArchitecture.ToString(),
                Framework = RuntimeInformation.FrameworkDescription,
                MachineName = Environment.MachineName,
                UserName = Environment.UserName,
                ProcessorCount = Environment.ProcessorCount,
                WorkingDirectory = Environment.CurrentDirectory
            };

            return $"System Information:\n" +
                   $"Operating System: {info.OS}\n" +
                   $"Architecture: {info.Architecture}\n" +
                   $"Framework: {info.Framework}\n" +
                   $"Machine Name: {info.MachineName}\n" +
                   $"User Name: {info.UserName}\n" +
                   $"Processor Count: {info.ProcessorCount}\n" +
                   $"Working Directory: {info.WorkingDirectory}";
        }
        catch (Exception ex)
        {
            return $"Error getting system info: {ex.Message}";
        }
    }

    [McpServerTool(Name = "get_environment_variable"), Description("Get the value of an environment variable.")]
    public static string GetEnvironmentVariable(string variableName)
    {
        try
        {
            var value = Environment.GetEnvironmentVariable(variableName);
            
            if (value == null)
                return $"Environment variable '{variableName}' is not set";
            
            return $"Environment variable '{variableName}' = '{value}'";
        }
        catch (Exception ex)
        {
            return $"Error getting environment variable '{variableName}': {ex.Message}";
        }
    }

    [McpServerTool(Name = "list_environment_variables"), Description("List all environment variables (or filter by pattern).")]
    public static string ListEnvironmentVariables(string pattern = "")
    {
        try
        {
            var variables = Environment.GetEnvironmentVariables();
            var results = new List<string>();

            foreach (string key in variables.Keys.Cast<string>().OrderBy(k => k))
            {
                if (string.IsNullOrEmpty(pattern) || 
                    key.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    var value = variables[key]?.ToString() ?? "";
                    // Truncate very long values for readability
                    if (value.Length > 100)
                        value = value[..97] + "...";
                    
                    results.Add($"{key} = {value}");
                }
            }

            if (results.Count == 0)
            {
                return string.IsNullOrEmpty(pattern) 
                    ? "No environment variables found" 
                    : $"No environment variables found matching pattern '{pattern}'";
            }

            var title = string.IsNullOrEmpty(pattern) 
                ? $"All environment variables ({results.Count} total):" 
                : $"Environment variables matching '{pattern}' ({results.Count} found):";

            return $"{title}\n{string.Join("\n", results)}";
        }
        catch (Exception ex)
        {
            return $"Error listing environment variables: {ex.Message}";
        }
    }

    [McpServerTool(Name = "get_current_directory"), Description("Get the current working directory.")]
    public static string GetCurrentDirectory()
    {
        try
        {
            var currentDir = Environment.CurrentDirectory;
            return $"Current working directory: {currentDir}";
        }
        catch (Exception ex)
        {
            return $"Error getting current directory: {ex.Message}";
        }
    }

    [McpServerTool(Name = "generate_uuid"), Description("Generate a new UUID/GUID.")]
    public static string GenerateUuid()
    {
        try
        {
            var uuid = Guid.NewGuid();
            return $"Generated UUID: {uuid}";
        }
        catch (Exception ex)
        {
            return $"Error generating UUID: {ex.Message}";
        }
    }
}