using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MCPServer.ToolApproval;

/// <summary>
/// File-based approval provider that writes approval requests to files
/// and polls for approval responses. Good for local development with external tools.
/// </summary>
public class FileApprovalProvider : IApprovalProvider
{
    private readonly string _approvalDirectory;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _timeout;

    public string ProviderName => "File";

    public FileApprovalProvider(string approvalDirectory = "./approvals", 
                               TimeSpan? pollInterval = null, 
                               TimeSpan? timeout = null)
    {
        _approvalDirectory = approvalDirectory;
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(1);
        _timeout = timeout ?? TimeSpan.FromMinutes(5);

        if (!Directory.Exists(_approvalDirectory))
        {
            Directory.CreateDirectory(_approvalDirectory);
        }
    }

    public async Task<bool> RequestApprovalAsync(ApprovalInvocationToken token, CancellationToken cancellationToken = default)
    {
        var requestFile = Path.Combine(_approvalDirectory, $"{token.Id}.request.json");
        var responseFile = Path.Combine(_approvalDirectory, $"{token.Id}.response.json");

        // Write the approval request
        var requestData = new
        {
            token.Id,
            token.ToolName,
            token.Arguments,
            token.CreatedAt,
            Instructions = "Create a response file with { \"approved\": true/false } to approve or deny this request"
        };

        await File.WriteAllTextAsync(requestFile, JsonSerializer.Serialize(requestData, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        }), cancellationToken);

        Console.Error.WriteLine($"Tool approval request written to: {requestFile}");
        Console.Error.WriteLine($"Waiting for response file: {responseFile}");

        // Poll for response file
        var deadline = DateTime.UtcNow.Add(_timeout);
        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            if (File.Exists(responseFile))
            {
                try
                {
                    var responseJson = await File.ReadAllTextAsync(responseFile, cancellationToken);
                    var response = JsonSerializer.Deserialize<JsonElement>(responseJson);
                    
                    if (response.TryGetProperty("approved", out var approvedElement))
                    {
                        var approved = approvedElement.GetBoolean();
                        
                        // Clean up files
                        File.Delete(requestFile);
                        File.Delete(responseFile);
                        
                        return approved;
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error reading response file: {ex.Message}");
                }
            }

            await Task.Delay(_pollInterval, cancellationToken);
        }

        // Cleanup on timeout
        if (File.Exists(requestFile)) File.Delete(requestFile);
        if (File.Exists(responseFile)) File.Delete(responseFile);

        throw new TimeoutException($"Approval request for tool '{token.ToolName}' timed out after {_timeout.TotalMinutes} minutes");
    }
}