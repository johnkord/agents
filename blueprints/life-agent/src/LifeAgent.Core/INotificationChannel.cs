namespace LifeAgent.Core;

/// <summary>
/// Output channel for agent → user communication.
/// Implementations: Console (CLI), Discord, Push, Email.
/// Follows 12-Factor Agents: trigger from anywhere, output to anywhere.
/// </summary>
public interface INotificationChannel
{
    /// <summary>Unique name for this channel (e.g., "console", "discord").</summary>
    string ChannelName { get; }

    /// <summary>Whether this channel supports bidirectional interaction (user can reply).</summary>
    bool SupportsBidirectional { get; }

    /// <summary>Send a notification to the user.</summary>
    Task SendAsync(string message, NotificationUrgency urgency = NotificationUrgency.Low,
        CancellationToken ct = default);

    /// <summary>
    /// Request approval from the user. Returns null if the channel doesn't support
    /// bidirectional communication, or if the user doesn't respond within the timeout.
    /// Follows the Human-as-Tool pattern (12-Factor #7).
    /// </summary>
    Task<ApprovalResponse?> RequestApprovalAsync(
        string taskId, string question, TimeSpan timeout,
        CancellationToken ct = default);
}

public enum NotificationUrgency { Low, Medium, High, Critical }

public sealed record ApprovalResponse(
    bool Approved,
    string? Comment = null,
    DateTimeOffset RespondedAt = default)
{
    public ApprovalResponse() : this(false) { }
}
