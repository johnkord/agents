using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MCPServer.ToolApproval;

/// <summary>
/// REST-based approval provider that submits requests to a REST API
/// and polls for approval decisions. Suitable for cloud/remote scenarios.
/// </summary>
public class RestApprovalProvider : IApprovalProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _timeout;

    public string ProviderName => "REST";

    public RestApprovalProvider(string baseUrl, 
                               HttpClient? httpClient = null,
                               TimeSpan? pollInterval = null, 
                               TimeSpan? timeout = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = httpClient ?? new HttpClient();
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(2);
        _timeout = timeout ?? TimeSpan.FromMinutes(10);
    }

    public async Task<bool> RequestApprovalAsync(ApprovalInvocationToken token, CancellationToken cancellationToken = default)
    {
        // Submit approval request
        var requestData = new
        {
            id = token.Id,
            toolName = token.ToolName,
            arguments = token.Arguments,
            createdAt = token.CreatedAt,
            status = "Pending"
        };

        var requestJson = JsonSerializer.Serialize(requestData);
        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        var submitResponse = await _httpClient.PostAsync($"{_baseUrl}/api/approvals", content, cancellationToken);
        submitResponse.EnsureSuccessStatusCode();

        Console.Error.WriteLine($"Tool approval request submitted to: {_baseUrl}/api/approvals");
        Console.Error.WriteLine($"You can approve/deny at: {_baseUrl}/swagger");
        Console.Error.WriteLine($"Waiting for approval decision...");

        // Poll for approval decision
        var deadline = DateTime.UtcNow.Add(_timeout);
        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var statusResponse = await _httpClient.GetAsync($"{_baseUrl}/api/approvals/{token.Id}", cancellationToken);
                
                if (statusResponse.IsSuccessStatusCode)
                {
                    var statusJson = await statusResponse.Content.ReadAsStringAsync(cancellationToken);
                    var statusData = JsonSerializer.Deserialize<JsonElement>(statusJson);
                    
                    if (statusData.TryGetProperty("status", out var statusElement))
                    {
                        var status = statusElement.GetString();
                        
                        if (status == "Approved")
                        {
                            return true;
                        }
                        else if (status == "Denied")
                        {
                            return false;
                        }
                        // If still "Pending", continue polling
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"Error polling approval status: {ex.Message}");
            }

            await Task.Delay(_pollInterval, cancellationToken);
        }

        throw new TimeoutException($"Approval request for tool '{token.ToolName}' timed out after {_timeout.TotalMinutes} minutes");
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}