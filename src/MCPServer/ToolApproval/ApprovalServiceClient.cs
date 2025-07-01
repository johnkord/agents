namespace MCPServer.ToolApproval;

using System.Text;
using System.Text.Json;
using System.Net.Http;
using System.Collections.Generic;

public static class ApprovalServiceClient
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private const string BaseUrl = "http://localhost:5000/api/approvals";

    /// <summary>
    /// Synchronously ask the Approval Service for permission to run <paramref name="toolName"/>.
    /// Returns true if approved, false when denied or timed-out/errored.
    /// </summary>
    public static bool EnsureApproved(string toolName,
                                      Dictionary<string, object?> args,
                                      TimeSpan? timeout = null)
    {
        var id = Guid.NewGuid();
        var body = JsonSerializer.Serialize(new
        {
            Id        = id,
            ToolName  = toolName,
            Arguments = args,
            CreatedAt = DateTimeOffset.UtcNow,
            Status    = 0 // Pending
        });

        try
        {
            // Submit request
            var resp = _http.PostAsync(BaseUrl,
                                       new StringContent(body, Encoding.UTF8, "application/json"))
                            .Result;
            if (!resp.IsSuccessStatusCode) return false;

            // Poll
            var stop = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
            while (DateTime.UtcNow < stop)
            {
                var statusResp = _http.GetAsync($"{BaseUrl}/{id}").Result;
                if (!statusResp.IsSuccessStatusCode)
                {
                    Thread.Sleep(1000);
                    continue;
                }

                var json = JsonDocument.Parse(statusResp.Content.ReadAsStringAsync().Result);
                var status = json.RootElement.GetProperty("status").GetString();
                if (status == "Approved") return true;
                if (status == "Denied")   return false;

                Thread.Sleep(1000);
            }
        }
        catch { /* network errors → treated as denial */ }

        return false;
    }
}
