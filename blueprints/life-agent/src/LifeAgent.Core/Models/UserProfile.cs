namespace LifeAgent.Core.Models;

/// <summary>
/// User's persistent profile — accumulated over time from interactions,
/// feedback, and observed patterns. Included in every worker agent invocation context.
/// Based on O-Mem (2025) and Personalized LLM Agents survey (2026).
/// </summary>
public sealed class UserProfile
{
    public required string UserId { get; init; }
    public Dictionary<string, string> Preferences { get; set; } = new();
    public Dictionary<string, TrustLevel> DomainTrust { get; set; } = new();
    public ProactivitySettings Proactivity { get; set; } = new();
    public List<SchedulePattern> KnownPatterns { get; set; } = [];
    public DateTimeOffset LastInteraction { get; set; }

    /// <summary>
    /// Audio lifelogging preferences — controls what gets recorded, who is in the speaker gallery, etc.
    /// </summary>
    public AudioLifelogSettings AudioLifelog { get; set; } = new();
}

public sealed class ProactivitySettings
{
    /// <summary>0.0 = fully reactive, 1.0 = fully proactive. Default conservative.</summary>
    public float ProactivityLevel { get; set; } = 0.3f;
    public TimeOnly QuietHoursStart { get; set; } = new(22, 0);
    public TimeOnly QuietHoursEnd { get; set; } = new(7, 0);
    public int MaxNotificationsPerHour { get; set; } = 3;
    public HashSet<string> EnabledDomains { get; set; } = ["research", "reminder"];
}

public sealed class SchedulePattern
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public DayOfWeek[] Days { get; init; } = [];
    public TimeOnly? TypicalTime { get; init; }
    public float Confidence { get; set; }
}

/// <summary>
/// User-configurable settings for the audio lifelogging pipeline.
/// Privacy-first design: user controls what is recorded and retained.
/// </summary>
public sealed class AudioLifelogSettings
{
    /// <summary>Whether audio lifelogging is enabled at all.</summary>
    public bool Enabled { get; set; }

    /// <summary>Auto-create LifeTasks from spoken commitments?</summary>
    public bool AutoCreateTasksFromCommitments { get; set; } = true;

    /// <summary>Include conversation digests in the daily briefing?</summary>
    public bool IncludeInDailyBriefing { get; set; } = true;

    /// <summary>Contacts excluded from recording (by speaker gallery name).</summary>
    public HashSet<string> ExcludedContacts { get; set; } = [];

    /// <summary>Named locations where recording should pause (e.g., "Doctor's office").</summary>
    public HashSet<string> ExcludedLocations { get; set; } = [];

    /// <summary>Retention policy for raw transcripts (days). 0 = permanent.</summary>
    public int TranscriptRetentionDays { get; set; } = 0;
}
