using System.ComponentModel;
using System.Net.Http;
using ModelContextProtocol.Server;
using MCPServer.Logging;

namespace MCPServer.Tools;

[McpServerToolType]
public class AppInsightsTools
{
    private static readonly HttpClient _client = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    [McpServerTool(Name = "appinsights_query"),
     Description("Run an Azure Application Insights (Kusto) query and return the raw JSON response.")]
    public static string Query(
        string appId,
        string apiKey,
        string kustoQuery,
        string? timespan = null /* e.g. PT1H, P1D */)
    {
        ToolLogger.LogStart("appinsights_query");
        try
        {
            if (string.IsNullOrWhiteSpace(appId)  ||
                string.IsNullOrWhiteSpace(apiKey) ||
                string.IsNullOrWhiteSpace(kustoQuery))
            {
                return "Error: 'appId', 'apiKey' and 'kustoQuery' are required parameters.";
            }

            var url = $"https://api.applicationinsights.io/v1/apps/{appId}/query" +
                      $"?query={Uri.EscapeDataString(kustoQuery)}";

            if (!string.IsNullOrEmpty(timespan))
                url += $"&timespan={Uri.EscapeDataString(timespan)}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("x-api-key", apiKey);

            var response = _client.Send(request);
            var content  = response.Content.ReadAsStringAsync().Result;

            return response.IsSuccessStatusCode
                ? $"Status: {response.StatusCode}\nResponse:\n{content}"
                : $"Error: {response.StatusCode}\n{content}";
        }
        catch (Exception ex)
        {
            ToolLogger.LogError("appinsights_query", ex);
            return $"Error querying Application Insights: {ex.Message}";
        }
        finally
        {
            ToolLogger.LogEnd("appinsights_query");
        }
    }
}
