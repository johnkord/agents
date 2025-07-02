using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using System.ComponentModel;
using MCPServer.Logging;
using ModelContextProtocol.Server;

namespace MCPServer.Tools;

[McpServerToolType]
public class EventHubTools
{
    // ---------------- 1. Hub metrics ----------------
    [McpServerTool(Name = "eh_get_hub_metrics"),
     Description("Get properties & runtime metrics for an Event Hub (incl. per-partition stats).")]
    public static async Task<string> GetHubMetrics(
        string connectionString,
        string eventHubName)
    {
        ToolLogger.LogStart("eh_get_hub_metrics");
        try
        {
            await using var consumer = new EventHubConsumerClient(
                EventHubConsumerClient.DefaultConsumerGroupName,
                connectionString,
                eventHubName);

            var hubProps = await consumer.GetEventHubPropertiesAsync();

            var partitionLines = new List<string>();
            foreach (string pid in hubProps.PartitionIds)
            {
                var p = await consumer.GetPartitionPropertiesAsync(pid);
                partitionLines.Add(
$@"  ▸ Partition {pid}
    Beginning Seq #: {p.BeginningSequenceNumber}
    Last Enqueued Seq #: {p.LastEnqueuedSequenceNumber}
    Last Enqueued Offset: {p.LastEnqueuedOffset}
    Last Enqueued Time  : {p.LastEnqueuedTime:u}");
            }

            return
$@"Event Hub: {eventHubName}
Created   : {hubProps.CreatedOn:u}
Partitions: {hubProps.PartitionIds.Length}

{string.Join("\n\n", partitionLines)}";
        }
        catch (Exception ex)
        {
            ToolLogger.LogError("eh_get_hub_metrics", ex);
            return $"Error getting Event Hub metrics: {ex.Message}";
        }
        finally
        {
            ToolLogger.LogEnd("eh_get_hub_metrics");
        }
    }

    // ---------------- 2. Receive messages ----------------
    [McpServerTool(Name = "eh_receive_messages"),
     Description("Read messages from an Event Hub (earliest or latest).")]
    public static async Task<string> ReceiveMessages(
        string connectionString,
        string eventHubName,
        string consumerGroup = EventHubConsumerClient.DefaultConsumerGroupName,
        int    maxEvents     = 10,
        bool   fromBeginning = false)
    {
        ToolLogger.LogStart("eh_receive_messages");
        try
        {
            await using var consumer = new EventHubConsumerClient(
                consumerGroup,
                connectionString,
                eventHubName);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var details = new List<string>();
            int read = 0;

            await foreach (PartitionEvent ev in consumer.ReadEventsAsync(
                               startReadingAtEarliestEvent: fromBeginning,
                               cancellationToken: cts.Token))
            {
                if (ev.Data == null) continue;

                details.Add(
$@"• Partition:{ev.Partition.PartitionId} | Seq #{ev.Data.SequenceNumber}
  Enqueued: {ev.Data.EnqueuedTime:u}
  Body-Preview: {GetBodyPreview(ev.Data)}");

                if (++read >= maxEvents) break;
            }

            return read == 0
                ? "No events found."
                : $"Read {read} event(s) from Event Hub '{eventHubName}':\n{string.Join("\n", details)}";
        }
        catch (Exception ex)
        {
            ToolLogger.LogError("eh_receive_messages", ex);
            return $"Error receiving events: {ex.Message}";
        }
        finally
        {
            ToolLogger.LogEnd("eh_receive_messages");
        }

        static string GetBodyPreview(EventData data)
        {
            var txt = data.EventBody.ToString();
            return txt.Length <= 200 ? txt : txt[..200] + "...";
        }
    }

    // ---------------- 3. Checkpoint info ----------------
    [McpServerTool(Name = "eh_get_checkpoints"),
     Description("List blob-based checkpoints for a consumer group and show the last processed event.")]
    public static async Task<string> GetCheckpoints(
        string storageConnectionString,
        string containerName,
        string eventHubConnectionString,
        string eventHubName,
        string consumerGroup     = EventHubConsumerClient.DefaultConsumerGroupName,
        int    bodyPreviewBytes  = 200)
    {
        // The required Azure SDK type (BlobCheckpointStore) is not present in the
        // preview package we can restore.  Provide a graceful fallback message.
        ToolLogger.LogStart("eh_get_checkpoints");
        try
        {
            // add a no-op await so that the async keyword is justified
            await Task.CompletedTask;
             return "Checkpoint inspection is not available with the current Azure SDK version.";
        }
        finally
        {
            ToolLogger.LogEnd("eh_get_checkpoints");
        }
    }
}