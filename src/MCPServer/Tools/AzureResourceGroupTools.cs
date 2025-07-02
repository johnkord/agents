using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text.Json;
using MCPServer.Logging;
using ModelContextProtocol.Server;
using System.Linq;

namespace MCPServer.Tools;

[McpServerToolType]
public class AzureResourceGroupTools
{
    private static readonly HttpClient _client = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private const string ApiVersion = "2021-04-01";   // Resource Groups / Resources API version

    [McpServerTool(Name = "az_rg_list"),
     Description("List resource groups in a subscription with optional filters.\n" +
                 "Filters: nameContains, location, tagName, tagValue")]
    public static string ListResourceGroups(
        string subscriptionId,
        string bearerToken,
        string? nameContains = null,
        string? location     = null,
        string? tagName      = null,
        string? tagValue     = null)
    {
        ToolLogger.LogStart("az_rg_list");
        try
        {
            var url = $"https://management.azure.com/subscriptions/{subscriptionId}/resourcegroups?api-version={ApiVersion}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

            var response = _client.Send(request);
            var json     = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
                return $"Error: {response.StatusCode}\n{json}";

            var groups = JsonDocument.Parse(json)
                                     .RootElement
                                     .GetProperty("value")
                                     .EnumerateArray();

            var filtered = groups.Where(g =>
            {
                bool ok = true;

                if (!string.IsNullOrEmpty(nameContains))
                    ok &= g.GetProperty("name").GetString()!
                          .Contains(nameContains, StringComparison.OrdinalIgnoreCase);

                if (!string.IsNullOrEmpty(location))
                    ok &= string.Equals(g.GetProperty("location").GetString(),
                                        location,
                                        StringComparison.OrdinalIgnoreCase);

                if (!string.IsNullOrEmpty(tagName))
                {
                    if (g.TryGetProperty("tags", out var tagsProp) &&
                        tagsProp.ValueKind == JsonValueKind.Object &&
                        tagsProp.TryGetProperty(tagName, out var tagVal))
                    {
                        ok &= string.IsNullOrEmpty(tagValue) ||
                              string.Equals(tagVal.GetString(), tagValue, StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        ok = false;
                    }
                }

                return ok;
            }).Select(g =>
            {
                var name   = g.GetProperty("name").GetString();
                var loc    = g.GetProperty("location").GetString();
                var state  = g.GetProperty("properties").GetProperty("provisioningState").GetString();
                return $"• {name} ({loc})  –  {state}";
            }).ToList();

            return filtered.Count == 0
                ? "No resource groups found with the specified filters."
                : $"Resource Groups ({filtered.Count}):\n{string.Join("\n", filtered)}";
        }
        catch (Exception ex)
        {
            ToolLogger.LogError("az_rg_list", ex);
            return $"Error listing resource groups: {ex.Message}";
        }
        finally
        {
            ToolLogger.LogEnd("az_rg_list");
        }
    }

    [McpServerTool(Name = "az_rg_list_resources"),
     Description("List all resources (name, type) within a given resource group.")]
    public static string ListResourcesInGroup(
        string subscriptionId,
        string resourceGroup,
        string bearerToken)
    {
        ToolLogger.LogStart("az_rg_list_resources");
        try
        {
            var url =
$"https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/resources?api-version={ApiVersion}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

            var response = _client.Send(request);
            var json     = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
                return $"Error: {response.StatusCode}\n{json}";

            var resources = JsonDocument.Parse(json)
                                        .RootElement
                                        .GetProperty("value")
                                        .EnumerateArray()
                                        .Select(r =>
                                        {
                                            var name = r.GetProperty("name").GetString();
                                            var type = r.GetProperty("type").GetString();
                                            return $"• {name}  →  {type}";
                                        })
                                        .ToList();

            return resources.Count == 0
                ? $"No resources found in resource group '{resourceGroup}'."
                : $"Resources in '{resourceGroup}' ({resources.Count}):\n{string.Join("\n", resources)}";
        }
        catch (Exception ex)
        {
            ToolLogger.LogError("az_rg_list_resources", ex);
            return $"Error listing resources: {ex.Message}";
        }
        finally
        {
            ToolLogger.LogEnd("az_rg_list_resources");
        }
    }
}
