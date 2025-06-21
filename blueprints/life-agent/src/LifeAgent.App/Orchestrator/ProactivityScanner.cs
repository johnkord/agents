using System.Threading.Channels;
using LifeAgent.Core;
using LifeAgent.Core.Events;
using LifeAgent.Core.Memory;
using LifeAgent.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LifeAgent.App.Orchestrator;

/// <summary>
/// Background service that periodically scans for proactive opportunities.
/// This is the core differentiator from a purely reactive assistant —
/// the agent notices things before you ask.
///
/// Implements design doc §10: Proactivity Engine with:
///   - Deadline proximity detection
///   - Stale/abandoned task detection
///   - Notification fatigue prevention (§10.3)
///   - Quiet hours respect
///   - Confidence-threshold filtering
///
/// Scans every 15 minutes during non-quiet hours.
/// </summary>
public sealed class ProactivityScanner : BackgroundService
{
    private readonly LifeAgentState _state;
    private readonly ChannelWriter<LifeEvent> _eventWriter;
    private readonly ILogger<ProactivityScanner> _logger;

    private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(15);
    private int _notificationsSentThisHour;
    private DateTimeOffset _currentHourStart;

    public ProactivityScanner(
        LifeAgentState state,
        Channel<LifeEvent> eventChannel,
        ILogger<ProactivityScanner> logger)
    {
        _state = state;
        _eventWriter = eventChannel.Writer;
        _logger = logger;
        _currentHourStart = DateTimeOffset.UtcNow;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Wait for system to finish initializing
        await Task.Delay(TimeSpan.FromSeconds(15), ct);

        _logger.LogInformation("Proactivity scanner started (interval: {Interval})", ScanInterval);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ScanAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Proactivity scan failed");
            }

            await Task.Delay(ScanInterval, ct);
        }
    }

    private async Task ScanAsync(CancellationToken ct)
    {
        var profile = _state.UserProfile;
        var now = DateTimeOffset.Now;

        // Respect quiet hours
        var localTime = TimeOnly.FromDateTime(now.LocalDateTime);
        if (IsQuietHours(localTime, profile.Proactivity))
        {
            _logger.LogDebug("Proactivity scan skipped — quiet hours");
            return;
        }

        // Reset hourly notification counter
        if ((now - _currentHourStart).TotalHours >= 1)
        {
            _notificationsSentThisHour = 0;
            _currentHourStart = now;
        }

        // Gather all opportunities
        var opportunities = new List<ProactiveOpportunity>();

        ScanDeadlines(opportunities, now);
        ScanStaleTasks(opportunities, now);
        ScanBudgetWarnings(opportunities);

        if (opportunities.Count == 0) return;

        // Filter by confidence threshold (adjusted by proactivity level)
        // Higher proactivity level = lower threshold = more suggestions
        var threshold = 1.0f - profile.Proactivity.ProactivityLevel;

        var filtered = opportunities
            .Where(o => o.Confidence >= threshold)
            .OrderByDescending(o => o.Confidence)
            .Take(Math.Max(1, profile.Proactivity.MaxNotificationsPerHour - _notificationsSentThisHour))
            .ToList();

        if (filtered.Count == 0) return;

        _logger.LogInformation("Proactivity scan found {Total} opportunities, emitting {Filtered} after filtering",
            opportunities.Count, filtered.Count);

        foreach (var opportunity in filtered)
        {
            // Emit as a proactive opportunity event
            await _eventWriter.WriteAsync(
                new ProactiveOpportunityDetected(
                    opportunity.Domain,
                    opportunity.Description,
                    opportunity.Confidence),
                ct);

            // Create a task from the opportunity if it's actionable
            if (opportunity.SuggestedTask is not null)
            {
                await _eventWriter.WriteAsync(
                    new TaskCreated(opportunity.SuggestedTask), ct);
            }

            _notificationsSentThisHour++;
        }
    }

    /// <summary>
    /// Detect tasks with approaching deadlines that haven't been started or are stalled.
    /// </summary>
    private void ScanDeadlines(List<ProactiveOpportunity> opportunities, DateTimeOffset now)
    {
        // Tasks due within 24 hours that are still queued (not started)
        var urgentTasks = _state.GetTasksWithDeadlineBefore(now.AddHours(24));
        foreach (var task in urgentTasks)
        {
            if (task.Status != LifeTaskStatus.Queued) continue;

            var hoursLeft = (task.Deadline!.Value - now).TotalHours;
            var confidence = hoursLeft switch
            {
                < 2 => 0.95f,
                < 6 => 0.85f,
                < 12 => 0.7f,
                _ => 0.5f,
            };

            opportunities.Add(new ProactiveOpportunity
            {
                Domain = "deadline",
                Description = $"'{task.Title}' is due in {hoursLeft:F0} hours but hasn't been started",
                Confidence = confidence,
                SuggestedTask = null, // Don't duplicate — just alert
            });
        }

        // Tasks due within 3 days that are in progress but might be at risk
        var atRiskTasks = _state.GetTasksWithDeadlineBefore(now.AddDays(3));
        foreach (var task in atRiskTasks)
        {
            if (task.Status != LifeTaskStatus.Delegated) continue;
            if (task.Deadline!.Value - now < TimeSpan.FromDays(1)) continue; // Already caught above

            opportunities.Add(new ProactiveOpportunity
            {
                Domain = "deadline",
                Description = $"'{task.Title}' is due {task.Deadline.Value:MMM dd} and is still in progress",
                Confidence = 0.4f,
            });
        }
    }

    /// <summary>
    /// Detect tasks that have been queued for too long without activity.
    /// </summary>
    private void ScanStaleTasks(List<ProactiveOpportunity> opportunities, DateTimeOffset now)
    {
        var staleCutoff = now.AddDays(-3);
        var activeTasks = _state.GetActiveTasks();

        foreach (var task in activeTasks)
        {
            if (task.CreatedAt > staleCutoff) continue;
            if (task.Status != LifeTaskStatus.Queued) continue;

            var daysStale = (now - task.CreatedAt).TotalDays;
            opportunities.Add(new ProactiveOpportunity
            {
                Domain = "stale-task",
                Description = $"'{task.Title}' has been queued for {daysStale:F0} days with no activity. Cancel or reprioritize?",
                Confidence = daysStale > 7 ? 0.8f : 0.5f,
            });
        }
    }

    /// <summary>
    /// Warn when budget is running low but work remains.
    /// </summary>
    private void ScanBudgetWarnings(List<ProactiveOpportunity> opportunities)
    {
        var budget = _state.Budget;
        if (budget.IsExhausted) return; // Already handled by orchestrator

        var percentUsed = budget.LimitUsd > 0 ? (float)(budget.SpentUsd / budget.LimitUsd) : 0;

        if (percentUsed >= 0.8f && _state.ActiveTaskCount > 0)
        {
            opportunities.Add(new ProactiveOpportunity
            {
                Domain = "budget",
                Description = $"Daily budget is {percentUsed:P0} used (${budget.SpentUsd:F2}/${budget.LimitUsd:F2}) " +
                    $"with {_state.ActiveTaskCount} active tasks remaining",
                Confidence = percentUsed >= 0.9f ? 0.9f : 0.6f,
            });
        }
    }

    private static bool IsQuietHours(TimeOnly currentTime, ProactivitySettings settings)
    {
        var start = settings.QuietHoursStart;
        var end = settings.QuietHoursEnd;

        // Handle overnight quiet hours (e.g., 22:00 to 07:00)
        if (start > end)
            return currentTime >= start || currentTime <= end;

        return currentTime >= start && currentTime <= end;
    }

    private sealed class ProactiveOpportunity
    {
        public required string Domain { get; init; }
        public required string Description { get; init; }
        public required float Confidence { get; init; }
        public LifeTask? SuggestedTask { get; init; }
    }
}
