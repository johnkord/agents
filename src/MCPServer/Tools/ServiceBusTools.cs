using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using ModelContextProtocol.Server;
using System.ComponentModel;
using MCPServer.Logging;

namespace MCPServer.Tools;

[McpServerToolType]
public class ServiceBusTools
{
    [McpServerTool(Name = "sb_get_queue_metrics"),
     Description("Get metrics and properties for an Azure Service Bus queue (incl. dead-letter counts).")]
    public static async Task<string> GetQueueMetrics(
        string connectionString,
        string queueName)
    {
        ToolLogger.LogStart("sb_get_queue_metrics");
        try
        {
            var admin = new ServiceBusAdministrationClient(connectionString);

            var props   = await admin.GetQueueAsync(queueName);
            var runtime = await admin.GetQueueRuntimePropertiesAsync(queueName);

            return
$@"Queue: {queueName}
Max Size (MB): {props.Value.MaxSizeInMegabytes}
Status: {props.Value.Status}
Active Messages: {runtime.Value.ActiveMessageCount}
Scheduled Messages: {runtime.Value.ScheduledMessageCount}
Dead-Letter Messages: {runtime.Value.DeadLetterMessageCount}
Transfer Dead-Letter Messages: {runtime.Value.TransferDeadLetterMessageCount}
Size-in-Bytes: {runtime.Value.SizeInBytes}
Created: {runtime.Value.CreatedAt:u}
Updated: {runtime.Value.UpdatedAt:u}";
        }
        catch (Exception ex)
        {
            ToolLogger.LogError("sb_get_queue_metrics", ex);
            return $"Error getting queue metrics: {ex.Message}";
        }
        finally
        {
            ToolLogger.LogEnd("sb_get_queue_metrics");
        }
    }

    [McpServerTool(Name = "sb_receive_messages"),
     Description("Peek messages from an Azure Service Bus queue or its dead-letter queue.")]
    public static async Task<string> ReceiveMessages(
        string connectionString,
        string queueName,
        int    maxMessages   = 10,
        bool   fromDeadLetter = false)
    {
        ToolLogger.LogStart("sb_receive_messages");
        try
        {
            await using var client = new ServiceBusClient(connectionString);
            var receiver = client.CreateReceiver(
                queueName,
                new ServiceBusReceiverOptions
                {
                    SubQueue = fromDeadLetter ? SubQueue.DeadLetter : SubQueue.None
                });

            var messages = await receiver.PeekMessagesAsync(maxMessages);
            if (messages.Count == 0)
                return "No messages found.";

            var details = messages.Select(m =>
$@"• Seq #{m.SequenceNumber} | Id:{m.MessageId}
  Enqueued: {m.EnqueuedTime:u}
  Body-Preview: {GetBodyPreview(m.Body)}");

            return $"Peeked {messages.Count} message(s) from {(fromDeadLetter ? "DLQ" : "queue")} '{queueName}':\n{string.Join("\n", details)}";
        }
        catch (Exception ex)
        {
            ToolLogger.LogError("sb_receive_messages", ex);
            return $"Error receiving messages: {ex.Message}";
        }
        finally
        {
            ToolLogger.LogEnd("sb_receive_messages");
        }

        static string GetBodyPreview(BinaryData body)
        {
            var txt = body.ToString();
            return txt.Length <= 200 ? txt : txt[..200] + "...";
        }
    }

    [McpServerTool(Name = "sb_send_message"),
     Description("Send a single message to an Azure Service Bus queue.")]
    public static async Task<string> SendMessage(
        string connectionString,
        string queueName,
        string body,
        string? sessionId = null)
    {
        ToolLogger.LogStart("sb_send_message");
        try
        {
            await using var client  = new ServiceBusClient(connectionString);
            ServiceBusSender sender = client.CreateSender(queueName);

            var message = new ServiceBusMessage(body);
            if (!string.IsNullOrEmpty(sessionId))
                message.SessionId = sessionId;

            await sender.SendMessageAsync(message);
            return $"Sent message (Id:{message.MessageId}) to queue '{queueName}'.";
        }
        catch (Exception ex)
        {
            ToolLogger.LogError("sb_send_message", ex);
            return $"Error sending message: {ex.Message}";
        }
        finally
        {
            ToolLogger.LogEnd("sb_send_message");
        }
    }
}
