using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text.Json;
using MCPServer.Logging;
using ModelContextProtocol.Server;

namespace MCPServer.Tools;

[McpServerToolType]
public class AzureFunctionTools
{
    private static readonly HttpClient _client = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private const string ApiVersion = "2022-09-01"; // sites (Functions) API version

    [McpServerTool(Name = "azfunc_get_app_info"),
     Description("Get basic information and metrics for an Azure Function App.")]
    public static string GetFunctionAppInfo(
        string subscriptionId,
        string resourceGroup,
        string functionAppName,
        string bearerToken)
    {
        ToolLogger.LogStart("azfunc_get_app_info");
        try
        {
            var url =
$"https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Web/sites/{functionAppName}?api-version={ApiVersion}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

            var response = _client.Send(request);
            var json     = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
                return $"Error: {response.StatusCode}\n{json}";

            var doc = JsonDocument.Parse(json).RootElement;
            var props = doc.GetProperty("properties");

            return
$@"Function App: {functionAppName}
State          : {props.GetProperty("state").GetString()}
Default Host   : {props.GetProperty("defaultHostName").GetString()}
Outbound IPs   : {props.GetProperty("outboundIpAddresses").GetString()}
Last Modified  : {props.GetProperty("lastModifiedTimeUtc").GetString()}
Kind           : {doc.GetProperty("kind").GetString()}
Location       : {doc.GetProperty("location").GetString()}";
        }
        catch (Exception ex)
        {
            ToolLogger.LogError("azfunc_get_app_info", ex);
            return $"Error getting Function App info: {ex.Message}";
        }
        finally
        {
            ToolLogger.LogEnd("azfunc_get_app_info");
        }
    }

    [McpServerTool(Name = "azfunc_list_functions"),
     Description("List functions (name, invocation URL) within an Azure Function App.")]
    public static string ListFunctions(
        string subscriptionId,
        string resourceGroup,
        string functionAppName,
        string bearerToken)
    {
        ToolLogger.LogStart("azfunc_list_functions");
        try
        {
            var url =
$"https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Web/sites/{functionAppName}/functions?api-version={ApiVersion}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

            var response = _client.Send(request);
            var json     = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
                return $"Error: {response.StatusCode}\n{json}";

            var doc   = JsonDocument.Parse(json).RootElement;
            var funcs = doc.GetProperty("value").EnumerateArray();

            var lines = funcs.Select(f =>
            {
                var name = f.GetProperty("name").GetString();
                var href = f.GetProperty("properties").GetProperty("invokeUrlTemplate").GetString();
                return $"• {name}  →  {href}";
            }).ToList();

            return lines.Count == 0
                ? "No functions found."
                : $"Functions in '{functionAppName}' ({lines.Count}):\n{string.Join("\n", lines)}";
        }
        catch (Exception ex)
        {
            ToolLogger.LogError("azfunc_list_functions", ex);
            return $"Error listing functions: {ex.Message}";
        }
        finally
        {
            ToolLogger.LogEnd("azfunc_list_functions");
        }
    }
}
