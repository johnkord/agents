using System.Threading.Channels;
using LifeAgent.Core;
using Microsoft.Extensions.Logging;

namespace LifeAgent.App.Channels;

/// <summary>
/// Console-based notification channel for CLI/development mode.
/// Writes notifications to stdout, reads approval responses from stdin.
/// Also accepts task submissions interactively.
/// </summary>
public sealed class ConsoleChannel : INotificationChannel
{
    private readonly ILogger<ConsoleChannel> _logger;
    private readonly Channel<(string TaskId, TaskCompletionSource<ApprovalResponse?> Tcs)> _pendingApprovals = 
        System.Threading.Channels.Channel.CreateUnbounded<(string, TaskCompletionSource<ApprovalResponse?>)>();

    public string ChannelName => "console";
    public bool SupportsBidirectional => true;

    public ConsoleChannel(ILogger<ConsoleChannel> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string message, NotificationUrgency urgency, CancellationToken ct)
    {
        var prefix = urgency switch
        {
            NotificationUrgency.Critical => "🔴 CRITICAL",
            NotificationUrgency.High => "🟠 HIGH",
            NotificationUrgency.Medium => "🟡",
            NotificationUrgency.Low => "🔵",
            _ => "  ",
        };

        Console.WriteLine();
        Console.WriteLine($"  {prefix} {message}");
        Console.WriteLine();

        return Task.CompletedTask;
    }

    public async Task<ApprovalResponse?> RequestApprovalAsync(
        string taskId, string question, TimeSpan timeout, CancellationToken ct)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  ❓ APPROVAL NEEDED [{taskId}]");
        Console.ResetColor();
        Console.WriteLine($"  {question}");
        Console.Write("  Approve? (y/n/comment): ");

        var tcs = new TaskCompletionSource<ApprovalResponse?>();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        // Read from stdin with timeout
        var readTask = Task.Run(async () =>
        {
            try
            {
                var line = await Task.Run(Console.ReadLine, cts.Token);
                if (line is null) return null;

                var trimmed = line.Trim().ToLowerInvariant();
                var approved = trimmed is "y" or "yes" or "approve" or "ok" or "1";
                var comment = trimmed is "y" or "yes" or "n" or "no" ? null : line.Trim();

                return new ApprovalResponse(approved, comment, DateTimeOffset.UtcNow);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }, cts.Token);

        try
        {
            return await readTask;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\n  ⏰ Approval timed out.");
            return null;
        }
    }
}
