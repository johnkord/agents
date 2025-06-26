using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text;
using MCPServer.ToolApproval;

namespace MCPServer.Tools;

[McpServerToolType]
public static class HttpTools
{
    private static readonly HttpClient _client = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    [McpServerTool(Name = "http_request"), Description("Perform an HTTP request and return status, headers and body.")]
    [RequiresApproval] // network I/O
    public static string HttpRequest(
        string method,
        string url,
        string body            = "",
        string headersCsv       = "" /* key:value,key:value */ )
    {
        try
        {
            using var requestMessage = new HttpRequestMessage(new HttpMethod(method.ToUpperInvariant()), url);

            // Add body if supplied and method allows it
            if (!string.IsNullOrEmpty(body) &&
                !string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase))
            {
                requestMessage.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            // Parse optional headers formatted as: "Authorization:Bearer xxx, X-Custom:abc"
            if (!string.IsNullOrWhiteSpace(headersCsv))
            {
                var pairs = headersCsv.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var pair in pairs)
                {
                    var idx = pair.IndexOf(':');
                    if (idx <= 0) continue;
                    var key   = pair[..idx].Trim();
                    var value = pair[(idx + 1)..].Trim();
                    if (!requestMessage.Headers.TryAddWithoutValidation(key, value))
                    {
                        requestMessage.Content?.Headers.TryAddWithoutValidation(key, value);
                    }
                }
            }

            var response = _client.Send(requestMessage);

            var respBody = response.Content.ReadAsStringAsync().Result;

            var headerLines = response.Headers
                                      .Concat(response.Content.Headers)
                                      .Select(h => $"{h.Key}: {string.Join(", ", h.Value)}");

            return $"Status: {(int)response.StatusCode} {response.ReasonPhrase}\n" +
                   $"Headers:\n{string.Join("\n", headerLines)}\n\n" +
                   $"Body:\n{respBody}";
        }
        catch (Exception ex)
        {
            return $"Error performing HTTP request: {ex.Message}";
        }
    }
}
