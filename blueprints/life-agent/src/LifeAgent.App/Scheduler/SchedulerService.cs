using System.Threading.Channels;
using LifeAgent.Core.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LifeAgent.App.Scheduler;

/// <summary>
/// Background service that fires scheduled triggers on cron-like intervals.
/// Uses a simple polling approach with configurable schedules from appsettings.
///
/// Each trigger writes a <see cref="ScheduledTriggerFired"/> event to the
/// orchestrator's event channel, which then creates and routes the task.
///
/// Configured via "Scheduler:Triggers" section in appsettings.json:
/// <code>
/// "Scheduler": {
///   "Triggers": [
///     { "Id": "morning-briefing", "IntervalMinutes": 1440, "AtHour": 7 },
///     { "Id": "proactivity-scan", "IntervalMinutes": 15 },
///     { "Id": "deadline-check", "IntervalMinutes": 60 }
///   ]
/// }
/// </code>
/// </summary>
public sealed class SchedulerService : BackgroundService
{
    private readonly ChannelWriter<LifeEvent> _eventWriter;
    private readonly ILogger<SchedulerService> _logger;
    private readonly List<ScheduleTrigger> _triggers;

    public SchedulerService(
        Channel<LifeEvent> eventChannel,
        IConfiguration config,
        ILogger<SchedulerService> logger)
    {
        _eventWriter = eventChannel.Writer;
        _logger = logger;
        _triggers = LoadTriggers(config);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (_triggers.Count == 0)
        {
            _logger.LogInformation("Scheduler: no triggers configured, sleeping");
            return;
        }

        _logger.LogInformation("Scheduler started with {Count} trigger(s): {Triggers}",
            _triggers.Count, string.Join(", ", _triggers.Select(t => t.Id)));

        // Stagger start by 5s to let orchestrator initialize
        await Task.Delay(TimeSpan.FromSeconds(5), ct);

        while (!ct.IsCancellationRequested)
        {
            var now = DateTimeOffset.Now;

            foreach (var trigger in _triggers)
            {
                if (ShouldFire(trigger, now))
                {
                    trigger.LastFired = now;
                    _logger.LogInformation("Scheduler firing: {TriggerId}", trigger.Id);

                    try
                    {
                        await _eventWriter.WriteAsync(
                            new ScheduledTriggerFired(trigger.Id, $"every {trigger.IntervalMinutes}m"),
                            ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to fire trigger {TriggerId}", trigger.Id);
                    }
                }
            }

            // Poll every 30 seconds
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
        }
    }

    private static bool ShouldFire(ScheduleTrigger trigger, DateTimeOffset now)
    {
        // If AtHour is set, this is a daily trigger (fire once per day at that hour)
        if (trigger.AtHour.HasValue)
        {
            var localHour = now.ToLocalTime().Hour;
            if (localHour != trigger.AtHour.Value) return false;

            // Only fire once per day
            if (trigger.LastFired.HasValue &&
                trigger.LastFired.Value.Date == now.Date)
                return false;

            return true;
        }

        // Interval-based trigger
        if (!trigger.LastFired.HasValue) return true;

        var elapsed = now - trigger.LastFired.Value;
        return elapsed.TotalMinutes >= trigger.IntervalMinutes;
    }

    private static List<ScheduleTrigger> LoadTriggers(IConfiguration config)
    {
        var triggers = new List<ScheduleTrigger>();
        var section = config.GetSection("Scheduler:Triggers");

        foreach (var child in section.GetChildren())
        {
            var id = child["Id"];
            if (string.IsNullOrWhiteSpace(id)) continue;

            var trigger = new ScheduleTrigger
            {
                Id = id,
                IntervalMinutes = child.GetValue("IntervalMinutes", 60),
                AtHour = child.GetValue<int?>("AtHour", null),
                Enabled = child.GetValue("Enabled", true),
            };

            if (trigger.Enabled)
                triggers.Add(trigger);
        }

        return triggers;
    }

    private sealed class ScheduleTrigger
    {
        public required string Id { get; init; }
        public int IntervalMinutes { get; init; } = 60;
        public int? AtHour { get; init; }
        public bool Enabled { get; init; } = true;
        public DateTimeOffset? LastFired { get; set; }
    }
}
