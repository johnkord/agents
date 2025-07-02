using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using Azure.Cosmos;
using MCPServer.Logging;

namespace MCPServer.Tools;

[McpServerToolType]
public class CosmosDbTools
{
    private static CosmosClient? _client;

    [McpServerTool(Name = "cosmos_query"),
     Description("Run a SQL query against an Azure Cosmos DB container and return the raw JSON response.")]
    public static async Task<string> Query(
        string endpointUri,
        string key,
        string databaseId,
        string containerId,
        string queryText,
        int    maxItemCount = 100)
    {
        ToolLogger.LogStart("cosmos_query");
        try
        {
            if (string.IsNullOrWhiteSpace(endpointUri) ||
                string.IsNullOrWhiteSpace(key)         ||
                string.IsNullOrWhiteSpace(databaseId)  ||
                string.IsNullOrWhiteSpace(containerId) ||
                string.IsNullOrWhiteSpace(queryText))
            {
                return "Error: all parameters are required.";
            }

            _client ??= new CosmosClient(endpointUri, key);
            var container = _client.GetContainer(databaseId, containerId);

            var queryDef = new QueryDefinition(queryText);
            var iterator = container.GetItemQueryIterator<JsonElement>(
                               queryDef,
                               requestOptions: new QueryRequestOptions { MaxItemCount = maxItemCount });

            var results = new List<JsonElement>();
            await foreach (var item in iterator)     // AsyncPageable<T>
            {
                results.Add(item);
                if (results.Count >= maxItemCount)   // manual cap
                    break;
            }

            return JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            ToolLogger.LogError("cosmos_query", ex);
            return $"Error querying Cosmos DB: {ex.Message}";
        }
        finally
        {
            ToolLogger.LogEnd("cosmos_query");
        }
    }
}
