namespace LifeAgent.Core.Models;

/// <summary>
/// Tracks daily LLM spending. Reset at midnight UTC.
/// Budget enforcement prevents runaway cost loops (P7: circuit breakers, P6: cost accounting).
/// </summary>
public sealed class DailyBudget
{
    public DateOnly Date { get; init; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public decimal SpentUsd { get; set; }
    public decimal LimitUsd { get; set; } = 5.00m;
    public int TasksExecuted { get; set; }
    public int TasksFailed { get; set; }

    public bool IsExhausted => SpentUsd >= LimitUsd;
    public decimal RemainingUsd => Math.Max(0, LimitUsd - SpentUsd);

    public void RecordCost(decimal costUsd)
    {
        SpentUsd += costUsd;
        TasksExecuted++;
    }

    public void RecordFailure()
    {
        TasksFailed++;
    }

    /// <summary>
    /// Returns true if the budget has rolled over to a new day
    /// and should be reset.
    /// </summary>
    public bool NeedsReset()
    {
        return DateOnly.FromDateTime(DateTime.UtcNow) != Date;
    }
}
