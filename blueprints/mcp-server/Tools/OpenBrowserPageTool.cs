using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace McpServer.Tools;

/// <summary>
/// Fetches a web page with enhanced content extraction, SSRF protection,
/// and optional query-focused extraction.
///
/// Differs from FetchWebPageTool in that it:
///   1. Blocks private/internal IPs (SSRF prevention)
///   2. Supports a query parameter to focus extraction
///   3. Returns structured output with title, description, and body
///
/// Security basis:
///   - securing-mcp-tool-poisoning (2025): web content is the primary vector
///     for indirect prompt injection — delimit as untrusted
///   - AGENTSYS (2026): external data should be truncated and clearly delimited
/// </summary>
[McpServerToolType]
public static class OpenBrowserPageTool
{
    private static readonly HttpClient HttpClient = new(new HttpClientHandler
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 5,
    })
    {
        Timeout = TimeSpan.FromSeconds(20),
        DefaultRequestHeaders =
        {
            { "User-Agent", "Mozilla/5.0 (compatible; Forge/0.1; +https://github.com/forge-agent)" },
            { "Accept", "text/html,text/plain,application/json,application/xml" },
        },
    };

    private const int MaxContentLength = 50_000;

    [McpServerTool, Description(
        "Fetches a web page and returns its readable text content. Strips HTML, scripts, and styles. " +
        "Use for reading documentation, API references, or web content. " +
        "Optionally provide a query to focus extraction on relevant sections. " +
        "Blocks private/internal URLs for security.")]
    public static async Task<string> OpenBrowserPage(
        [Description("URL to fetch (must start with http:// or https://).")] string url,
        [Description("Optional: specific content to look for on the page. Helps focus extraction.")] string? query = null)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "Error: URL is required.";

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return "Error: URL must start with http:// or https://.";

        // SSRF protection: block private/internal IPs
        if (!await IsPublicUrl(url))
            return "Error: URL resolves to a private/internal address. Only public URLs are allowed.";

        try
        {
            using var response = await HttpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return $"Error: HTTP {(int)response.StatusCode} ({response.ReasonPhrase}) fetching {url}.";

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            var body = await response.Content.ReadAsStringAsync();

            string extractedText;
            string? title = null;
            string? description = null;

            if (contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
            {
                title = ExtractTag(body, "title");
                description = ExtractMetaDescription(body);
                extractedText = StripHtml(body);
            }
            else if (contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
            {
                // Pretty-print JSON if small enough
                extractedText = body.Length > MaxContentLength ? body[..MaxContentLength] : body;
            }
            else
            {
                extractedText = body;
            }

            // If a query is provided, try to focus on relevant paragraphs
            if (!string.IsNullOrWhiteSpace(query))
            {
                extractedText = FocusOnQuery(extractedText, query);
            }

            // Truncate
            if (extractedText.Length > MaxContentLength)
            {
                extractedText = extractedText[..MaxContentLength]
                    + $"\n\n... truncated ({body.Length:N0} total characters)";
            }

            // Build structured output
            var result = $"--- BEGIN WEB CONTENT (untrusted) ---\nURL: {url}\nStatus: {(int)response.StatusCode}\n";
            if (title is not null)
                result += $"Title: {title}\n";
            if (description is not null)
                result += $"Description: {description}\n";
            result += $"\n{extractedText}\n--- END WEB CONTENT ---";

            return result;
        }
        catch (HttpRequestException ex)
        {
            return $"Error fetching {url}: {ex.Message}";
        }
        catch (TaskCanceledException)
        {
            return $"Error: Request to {url} timed out after 20 seconds.";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
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

            // Block common private hostnames
            if (host is "localhost" || host.EndsWith(".local") || host.EndsWith(".internal"))
                return false;

            var addresses = await Dns.GetHostAddressesAsync(host);
            foreach (var addr in addresses)
            {
                var bytes = addr.GetAddressBytes();
                if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && bytes.Length == 4)
                {
                    // 127.0.0.0/8, 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16, 169.254.0.0/16
                    if (bytes[0] == 127) return false;
                    if (bytes[0] == 10) return false;
                    if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return false;
                    if (bytes[0] == 192 && bytes[1] == 168) return false;
                    if (bytes[0] == 169 && bytes[1] == 254) return false;
                    if (bytes[0] == 0) return false; // 0.0.0.0/8
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

    /// <summary>
    /// Focus extraction on paragraphs containing query terms.
    /// Returns relevant sections + limited surrounding context.
    /// </summary>
    private static string FocusOnQuery(string text, string query)
    {
        var queryTerms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var paragraphs = text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);

        var relevant = paragraphs
            .Where(p => queryTerms.Any(t => p.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .Take(20)
            .ToList();

        if (relevant.Count == 0)
            return text; // fall back to full content

        return $"[Focused on {relevant.Count} sections matching '{query}']\n\n"
            + string.Join("\n\n", relevant);
    }

    private static string? ExtractTag(string html, string tagName)
    {
        var match = Regex.Match(html, $@"<{tagName}[^>]*>(.*?)</{tagName}>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string? ExtractMetaDescription(string html)
    {
        var match = Regex.Match(html,
            @"<meta\s+name=[""']description[""']\s+content=[""']([^""']*)[""']",
            RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            match = Regex.Match(html,
                @"<meta\s+content=[""']([^""']*)[""']\s+name=[""']description[""']",
                RegexOptions.IgnoreCase);
        }
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string StripHtml(string html)
    {
        // Remove script, style, nav, header, footer blocks
        html = Regex.Replace(html, @"<(script|style|nav|header|footer)[^>]*>[\s\S]*?</\1>", "", RegexOptions.IgnoreCase);
        // Remove HTML comments
        html = Regex.Replace(html, @"<!--[\s\S]*?-->", "");
        // Remove HTML tags
        html = Regex.Replace(html, @"<[^>]+>", " ");
        // Decode common entities
        html = html.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">")
            .Replace("&quot;", "\"").Replace("&nbsp;", " ").Replace("&#39;", "'");
        // Collapse whitespace but preserve paragraph breaks
        html = Regex.Replace(html, @"[ \t]+", " ");
        html = Regex.Replace(html, @"(\s*\n\s*){3,}", "\n\n");
        return html.Trim();
    }
}
