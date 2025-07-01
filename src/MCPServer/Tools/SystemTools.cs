using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Runtime.InteropServices;
using MCPServer.ToolApproval;
using MCPServer.Logging;                       // +++

namespace MCPServer.Tools;

[McpServerToolType]
public class SystemTools
{
    [McpServerTool(Name = "get_current_time"), Description("Get the current date and time.")]
    public static string GetCurrentTime()
    {
        ToolLogger.LogStart("get_current_time");
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
            ToolLogger.LogError("get_current_time", ex);
            return $"Error getting current time: {ex.Message}";
        }
        finally
        {
            ToolLogger.LogEnd("get_current_time");
        }
    }

    [McpServerTool(Name = "get_system_info"), Description("Get basic system information.")]
    public static string GetSystemInfo()
    {
        ToolLogger.LogStart("get_system_info");
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
            ToolLogger.LogError("get_system_info", ex);
            return $"Error getting system info: {ex.Message}";
        }
        finally
        {
            ToolLogger.LogEnd("get_system_info");
        }
    }

    [McpServerTool(Name = "get_environment_variable"), Description("Get the value of an environment variable.")]
    public static string GetEnvironmentVariable(string variableName)
    {
        ToolLogger.LogStart("get_environment_variable");
        try
        {
            var value = Environment.GetEnvironmentVariable(variableName);
            
            if (value == null)
                return $"Environment variable '{variableName}' is not set";
            
            return $"Environment variable '{variableName}' = '{value}'";
        }
        catch (Exception ex)
        {
            ToolLogger.LogError("get_environment_variable", ex);
            return $"Error getting environment variable '{variableName}': {ex.Message}";
        }
        finally
        {
            ToolLogger.LogEnd("get_environment_variable");
        }
    }

    [McpServerTool(Name = "list_environment_variables"), Description("List all environment variables (or filter by pattern).")]
    public static string ListEnvironmentVariables(string pattern = "")
    {
        ToolLogger.LogStart("list_environment_variables");
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
            ToolLogger.LogError("list_environment_variables", ex);
            return $"Error listing environment variables: {ex.Message}";
        }
        finally
        {
            ToolLogger.LogEnd("list_environment_variables");
        }
    }

    [McpServerTool(Name = "get_current_directory"), Description("Get the current working directory.")]
    public static string GetCurrentDirectory()
    {
        ToolLogger.LogStart("get_current_directory");
        try
        {
            var currentDir = Environment.CurrentDirectory;
            return $"Current working directory: {currentDir}";
        }
        catch (Exception ex)
        {
            ToolLogger.LogError("get_current_directory", ex);
            return $"Error getting current directory: {ex.Message}";
        }
        finally
        {
            ToolLogger.LogEnd("get_current_directory");
        }
    }

    [McpServerTool(Name = "generate_uuid"), Description("Generate a new UUID/GUID.")]
    public static string GenerateUuid()
    {
        ToolLogger.LogStart("generate_uuid");
        try
        {
            var uuid = Guid.NewGuid();
            return $"Generated UUID: {uuid}";
        }
        catch (Exception ex)
        {
            ToolLogger.LogError("generate_uuid", ex);
            return $"Error generating UUID: {ex.Message}";
        }
        finally
        {
            ToolLogger.LogEnd("generate_uuid");
        }
    }
}