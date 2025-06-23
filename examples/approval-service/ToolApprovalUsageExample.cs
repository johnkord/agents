using MCPServer.ToolApproval;
using System;

namespace Examples.ToolApproval;

/// <summary>
/// Example showing how to configure and use the tool approval system
/// for different deployment scenarios.
/// </summary>
public static class ToolApprovalUsageExample
{
    /// <summary>
    /// Configure for local development (console approval)
    /// </summary>
    public static void ConfigureForLocalDevelopment()
    {
        var options = new ToolApprovalOptions
        {
            BackendType = ApprovalBackendType.Console
        };

        ToolApprovalManager.Instance.SetApprovalBackend(options.CreateBackend());
        Console.WriteLine("Tool approval configured for local development (console prompts)");
    }

    /// <summary>
    /// Configure for cloud deployment (remote approval service)
    /// </summary>
    public static void ConfigureForCloudDeployment()
    {
        var options = new ToolApprovalOptions
        {
            BackendType = ApprovalBackendType.Remote,
            RemoteConfig = new RemoteApprovalConfig
            {
                BaseUrl = Environment.GetEnvironmentVariable("APPROVAL_SERVICE_URL") 
                         ?? "https://approval-service.example.com",
                ApiKey = Environment.GetEnvironmentVariable("APPROVAL_API_KEY"),
                ApprovalTimeout = TimeSpan.FromMinutes(5),
                PollInterval = TimeSpan.FromSeconds(2),
                RequestTimeout = TimeSpan.FromSeconds(30)
            }
        };

        ToolApprovalManager.Instance.SetApprovalBackend(options.CreateBackend());
        Console.WriteLine("Tool approval configured for cloud deployment (remote service)");
    }

    /// <summary>
    /// Example of a tool that requires approval
    /// </summary>
    [MCPServer.ToolApproval.RequiresApproval]
    public static string DeleteFile(string path)
    {
        // This method will automatically trigger the approval workflow
        // when called through the MCP server
        System.IO.File.Delete(path);
        return $"File {path} deleted successfully";
    }

    /// <summary>
    /// Example of programmatic approval checking
    /// </summary>
    public static async Task<bool> ExampleProgrammaticApproval()
    {
        var toolName = "dangerous_operation";
        var args = new Dictionary<string, object?>
        {
            ["action"] = "delete_database",
            ["target"] = "production_data"
        };

        try
        {
            var approved = await ToolApprovalManager.Instance.EnsureApprovedAsync(toolName, args);
            
            if (approved)
            {
                Console.WriteLine("Operation approved - proceeding with dangerous operation");
                // Perform the dangerous operation here
                return true;
            }
            else
            {
                Console.WriteLine("Operation denied - aborting dangerous operation");
                return false;
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Approval request was cancelled");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Approval request failed: {ex.Message}");
            return false;
        }
    }
}