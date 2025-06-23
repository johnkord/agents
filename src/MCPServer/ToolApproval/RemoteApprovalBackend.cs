using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MCPServer.ToolApproval;

/// <summary>
/// Remote approval backend that communicates with an external approval service via HTTP.
/// Supports both polling and webhook-based approval workflows.
/// </summary>
public class RemoteApprovalBackend : IApprovalBackend, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly RemoteApprovalConfig _config;

    public string Name => "Remote";

    public RemoteApprovalBackend(RemoteApprovalConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClient = new HttpClient
        {
            Timeout = _config.RequestTimeout
        };
        
        if (!string.IsNullOrEmpty(_config.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiKey}");
        }
    }

    public async Task<bool> RequestApprovalAsync(ApprovalInvocationToken token, CancellationToken cancellationToken = default)
    {
        try
        {
            // Submit approval request
            var requestId = await SubmitApprovalRequestAsync(token, cancellationToken);
            
            // Poll for approval status
            return await PollForApprovalAsync(requestId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Log error and fall back to denial for safety
            Console.Error.WriteLine($"Remote approval failed: {ex.Message}");
            return false;
        }
    }

    private async Task<string> SubmitApprovalRequestAsync(ApprovalInvocationToken token, CancellationToken cancellationToken)
    {
        var payload = new
        {
            id = token.Id.ToString(),
            toolName = token.ToolName,
            arguments = token.Arguments,
            createdAt = token.CreatedAt,
            timeout = _config.ApprovalTimeout
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(_config.SubmitUrl, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var responseData = JsonSerializer.Deserialize<JsonElement>(responseJson);
        
        return responseData.GetProperty("requestId").GetString() ?? token.Id.ToString();
    }

    private async Task<bool> PollForApprovalAsync(string requestId, CancellationToken cancellationToken)
    {
        var pollUrl = $"{_config.PollUrl}?requestId={requestId}";
        var startTime = DateTime.UtcNow;
        var pollInterval = _config.PollInterval;

        while (DateTime.UtcNow - startTime < _config.ApprovalTimeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var response = await _httpClient.GetAsync(pollUrl, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                    var responseData = JsonSerializer.Deserialize<JsonElement>(responseJson);
                    
                    var status = responseData.GetProperty("status").GetString();
                    switch (status?.ToLowerInvariant())
                    {
                        case "approved":
                            return true;
                        case "denied":
                            return false;
                        case "pending":
                            break; // Continue polling
                        default:
                            throw new InvalidOperationException($"Unknown approval status: {status}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Polling error: {ex.Message}");
            }

            await Task.Delay(pollInterval, cancellationToken);
        }

        // Timeout reached - deny for safety
        return false;
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

/// <summary>
/// Configuration for remote approval backend.
/// </summary>
public class RemoteApprovalConfig
{
    /// <summary>
    /// Base URL for the approval service (e.g., "https://approval-service.example.com")
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Endpoint for submitting approval requests (relative to BaseUrl)
    /// </summary>
    public string SubmitEndpoint { get; set; } = "/api/approvals";

    /// <summary>
    /// Endpoint for polling approval status (relative to BaseUrl)
    /// </summary>
    public string PollEndpoint { get; set; } = "/api/approvals/status";

    /// <summary>
    /// API key for authentication
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// How long to wait for the HTTP request itself
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long to wait for human approval before timing out
    /// </summary>
    public TimeSpan ApprovalTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How frequently to poll for approval status
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);

    internal string SubmitUrl => $"{BaseUrl.TrimEnd('/')}{SubmitEndpoint}";
    internal string PollUrl => $"{BaseUrl.TrimEnd('/')}{PollEndpoint}";
}