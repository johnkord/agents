using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ModelContextProtocol.Server;
using System.ComponentModel;
using MCPServer.Logging;

namespace MCPServer.Tools;

[McpServerToolType]
public class AzureStorageTools
{
    [McpServerTool(Name = "get_storage_account_info"),
     Description("Return basic information (container count, total blobs, total size estimate) for a Storage account.")]
    public static async Task<string> GetStorageAccountInfo(string connectionString)
    {
        ToolLogger.LogStart("get_storage_account_info");
        try
        {
            var service = new BlobServiceClient(connectionString);
            long totalBlobs = 0;
            long totalBytes = 0;
            int  containerCount = 0;

            await foreach (var container in service.GetBlobContainersAsync())
            {
                containerCount++;
                var containerClient = service.GetBlobContainerClient(container.Name);

                await foreach (var blob in containerClient.GetBlobsAsync())
                {
                    totalBlobs++;
                    if (blob.Properties.ContentLength.HasValue)
                        totalBytes += blob.Properties.ContentLength.Value;
                }
            }

            return
$@"Storage account metrics:
Containers     : {containerCount}
Total blobs    : {totalBlobs}
Approx. size   : {totalBytes:N0} bytes ({totalBytes / 1_048_576.0:F2} MB)";
        }
        catch (Exception ex)
        {
            ToolLogger.LogError("get_storage_account_info", ex);
            return $"Error fetching storage account info: {ex.Message}";
        }
        finally
        {
            ToolLogger.LogEnd("get_storage_account_info");
        }
    }

    [McpServerTool(Name = "list_blobs"),
     Description("List blobs inside a container (name, size, last modified).")]
    public static async Task<string> ListBlobs(
        string connectionString,
        string containerName,
        int    maxResults = 100)
    {
        ToolLogger.LogStart("list_blobs");
        try
        {
            var container = new BlobContainerClient(connectionString, containerName);

            var blobs = new List<string>();
            int seen = 0;
            await foreach (var blob in container.GetBlobsAsync())
            {
                if (seen++ >= maxResults) break;
                var size = blob.Properties.ContentLength ?? 0;
                var last = blob.Properties.LastModified?.UtcDateTime.ToString("u") ?? "n/a";
                blobs.Add($"{blob.Name} | {size:N0} bytes | {last}");
            }

            return blobs.Count == 0
                ? $"No blobs found in container '{containerName}'."
                : $"Blobs in '{containerName}' (showing {blobs.Count}{(blobs.Count == maxResults ? "+" : "")}):\n" +
                  string.Join("\n", blobs);
        }
        catch (Exception ex)
        {
            ToolLogger.LogError("list_blobs", ex);
            return $"Error listing blobs: {ex.Message}";
        }
        finally
        {
            ToolLogger.LogEnd("list_blobs");
        }
    }

    [McpServerTool(Name = "get_blob_content"),
     Description("Download a blob's text content (truncated if large) and show metadata.")]
    public static async Task<string> GetBlobContent(
        string connectionString,
        string containerName,
        string blobName,
        int    maxBytes = 4096)
    {
        ToolLogger.LogStart("get_blob_content");
        try
        {
            var blobClient = new BlobContainerClient(connectionString, containerName)
                                 .GetBlobClient(blobName);

            var props = await blobClient.GetPropertiesAsync();
            var meta  = props.Value.Metadata;
            var size  = props.Value.ContentLength;

            using var stream = new MemoryStream();
            await blobClient.DownloadToAsync(stream);

            var bytes = stream.ToArray();
            var text  = System.Text.Encoding.UTF8.GetString(bytes, 0, (int)Math.Min(bytes.Length, maxBytes));
            if (bytes.Length > maxBytes) text += "\n...[truncated]";

            var metaLines = meta.Count == 0
                ? "No metadata"
                : string.Join("\n", meta.Select(kv => $"{kv.Key}: {kv.Value}"));

            return
$@"Blob: {blobName}
Size: {size:N0} bytes
Metadata:
{metaLines}

Content (UTF-8, first {maxBytes} bytes):
{text}";
        }
        catch (Exception ex)
        {
            ToolLogger.LogError("get_blob_content", ex);
            return $"Error reading blob: {ex.Message}";
        }
        finally
        {
            ToolLogger.LogEnd("get_blob_content");
        }
    }
}
