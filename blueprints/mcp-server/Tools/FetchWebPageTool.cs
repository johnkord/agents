using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace McpServer.Tools;

[McpServerToolType]
public static class FetchWebPageTool
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders =
        {
            { "User-Agent", "Forge/0.1 (coding-agent)" },
            { "Accept", "text/html,text/plain,application/json" },
        },
    };

    [McpServerTool, Description("Fetches the content from a web page URL and returns the text. Strips HTML tags for readable output. Use for reading documentation, API references, or web content. Blocks private/internal URLs for security.")]
    public static async Task<string> FetchWebPage(
        [Description("The URL to fetch (must start with http:// or https://).")] string url)
    {
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return "Error: URL must start with http:// or https://.";
        }

        // SSRF protection: block private/internal IPs
        if (!await IsPublicUrl(url))
            return "Error: URL resolves to a private/internal address. Only public URLs are allowed.";

        try
        {
            var response = await HttpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            var body = await response.Content.ReadAsStringAsync();

            // Strip HTML tags for readability
            if (contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
            {
                body = StripHtml(body);
            }

            // Truncate very large responses
            if (body.Length > 15_000)
            {
                body = body[..15_000] + $"\n\n... truncated ({body.Length:N0} total characters)";
            }

            return $"URL: {url}\nStatus: {(int)response.StatusCode}\n\n{body}";
        }
        catch (HttpRequestException ex)
        {
            return $"Error fetching {url}: {ex.Message}";
        }
        catch (TaskCanceledException)
        {
            return $"Error: Request to {url} timed out after 15 seconds.";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private static string StripHtml(string html)
    {
        // Remove script and style blocks
        html = Regex.Replace(html, @"<(script|style)[^>]*>[\s\S]*?</\1>", "", RegexOptions.IgnoreCase);
        // Remove HTML tags
        html = Regex.Replace(html, @"<[^>]+>", " ");
        // Decode common entities
        html = html.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">")
            .Replace("&quot;", "\"").Replace("&nbsp;", " ").Replace("&#39;", "'");
        // Collapse whitespace
        html = Regex.Replace(html, @"\s+", " ");
        html = Regex.Replace(html, @"(\s*\n\s*){3,}", "\n\n");
        return html.Trim();
    }

    /// <summary>
    /// SSRF protection: resolve the URL's IP and block private ranges.
    /// </summary>
    private static async Task<bool> IsPublicUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var host = uri.Host;

            if (host is "localhost" || host.EndsWith(".local") || host.EndsWith(".internal"))
                return false;

            var addresses = await Dns.GetHostAddressesAsync(host);
            foreach (var addr in addresses)
            {
                var bytes = addr.GetAddressBytes();
                if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && bytes.Length == 4)
                {
                    if (bytes[0] == 127) return false;  // 127.0.0.0/8
                    if (bytes[0] == 10) return false;   // 10.0.0.0/8
                    if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return false; // 172.16.0.0/12
                    if (bytes[0] == 192 && bytes[1] == 168) return false; // 192.168.0.0/16
                    if (bytes[0] == 169 && bytes[1] == 254) return false; // 169.254.0.0/16
                    if (bytes[0] == 0) return false;    // 0.0.0.0/8
                }
                else if (addr.IsIPv6LinkLocal || addr.IsIPv6SiteLocal || IPAddress.IsLoopback(addr))
                {
                    return false;
                }
            }
            return true;
        }
        catch
        {
            return false; // fail closed
        }
    }
}
