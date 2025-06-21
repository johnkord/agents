using LifeAgent.Core.Models;

namespace LifeAgent.Core.Events;

// ── Task lifecycle events ──────────────────────────────────────────

public record TaskCreated(LifeTask Task) : LifeEvent;
public record TaskDelegated(string TaskId, string WorkerType) : LifeEvent;
public record TaskCompleted(string TaskId, TaskResult Result) : LifeEvent;
public record TaskFailed(string TaskId, string Reason, int RetryCount) : LifeEvent;
public record TaskCancelled(string TaskId, string Reason) : LifeEvent;

// ── Human interaction events ───────────────────────────────────────

public record HumanApprovalRequested(
    string TaskId, string Question, TimeSpan Timeout, string FallbackAction) : LifeEvent;

public record HumanApprovalReceived(
    string TaskId, bool Approved, string? UserComment) : LifeEvent;

public record HumanApprovalTimedOut(string TaskId, string FallbackAction) : LifeEvent;

public record UserFeedbackReceived(
    string TaskId, FeedbackType Type, string? Comment) : LifeEvent;

// ── Proactivity events ─────────────────────────────────────────────

public record ProactiveOpportunityDetected(
    string Domain, string Description, float Confidence) : LifeEvent;

public record ProactiveSuggestionSent(
    string TaskId, string Channel, string Message) : LifeEvent;

public record ProactiveSuggestionDismissed(string TaskId) : LifeEvent;

// ── System events ──────────────────────────────────────────────────

public record ScheduledTriggerFired(string TriggerId, string CronExpression) : LifeEvent;
public record WebhookReceived(string Source, string Payload) : LifeEvent;
public record BudgetThresholdReached(decimal DailySpendUsd, decimal LimitUsd) : LifeEvent;

// ── Feedback ───────────────────────────────────────────────────────

public enum FeedbackType { Helpful, NotHelpful, TooFrequent, WrongTiming, WrongContent }
