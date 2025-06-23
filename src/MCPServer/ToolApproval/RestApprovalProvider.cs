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
    private readonly Uri _baseUri;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _timeout;

    public string ProviderName => "REST";

    public RestApprovalProvider(string baseUrl, HttpClient? httpClient = null, 
                               TimeSpan? pollInterval = null, TimeSpan? timeout = null)
    {
        // --- fix: normalise base URL ----------------------------------------
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("Base URL must not be empty.", nameof(baseUrl));

        _baseUri = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + '/'); // <—
        _httpClient = httpClient ?? new HttpClient();
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(2);
        _timeout = timeout ?? TimeSpan.FromMinutes(10);
    }

    public async Task<bool> RequestApprovalAsync(ApprovalInvocationToken token, CancellationToken cancellationToken = default)
    {
        // ---------- fixed payload -------------------------------------------------
        var request = new ApprovalRequestDto
        {
            Id         = token.Id,
            ToolName   = token.ToolName,
            Arguments  = token.Arguments,
            CreatedAt  = token.CreatedAt,
            Status     = ApprovalStatus.Pending        // enum → serialises as 0
        };

        var requestJson = JsonSerializer.Serialize(request);           // PascalCase + enum ‑ ok
        var content     = new StringContent(requestJson, Encoding.UTF8, "application/json");
        // -------------------------------------------------------------------------

        var submitResponse = await _httpClient.PostAsync(BuildUri("api/approvals"), content, cancellationToken);
        submitResponse.EnsureSuccessStatusCode();

        Console.Error.WriteLine($"Tool approval request submitted to: {_baseUri}api/approvals");
        Console.Error.WriteLine($"You can approve/deny at: {_baseUri}swagger");
        Console.Error.WriteLine($"Waiting for approval decision...");

        // Poll for approval decision
        var deadline = DateTime.UtcNow.Add(_timeout);
        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var statusResponse = await _httpClient.GetAsync(BuildUri($"api/approvals/{token.Id}"), cancellationToken);
                
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

    private Uri BuildUri(string relativePath)       // helper => safe URI composition
        => new(_baseUri, relativePath);

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    // ---------- helper DTO (local to file) ---------------------------------------
    private sealed class ApprovalRequestDto
    {
        public Guid                                     Id         { get; init; }
        public string                                   ToolName   { get; init; } = "";
        public IReadOnlyDictionary<string, object?>     Arguments  { get; init; } = new Dictionary<string, object?>();
        public DateTimeOffset                           CreatedAt  { get; init; }
        public ApprovalStatus                           Status     { get; init; }
    }
    // -----------------------------------------------------------------------------
}