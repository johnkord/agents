namespace ResearchAgent.Plugins.Search;

/// <summary>
/// Mutable, thread-safe counters describing the web-search activity during a single
/// research session. Owned by the orchestrator, mutated by <see cref="WebSearchPlugin"/>,
/// and snapshotted into the session export at the end of the run.
///
/// Tracks the three metrics the implementation plan calls out for P0.4:
/// provider name, total latency, and aggregate result count — plus call count for context.
/// </summary>
public sealed class SearchStatistics
{
    private long _callCount;
    private long _resultCount;
    private long _totalLatencyMs;

    public string ProviderName { get; }

    public int CallCount => (int)Interlocked.Read(ref _callCount);
    public int ResultCount => (int)Interlocked.Read(ref _resultCount);
    public long TotalLatencyMs => Interlocked.Read(ref _totalLatencyMs);

    public SearchStatistics(string providerName)
    {
        ProviderName = providerName;
    }

    public void RecordCall(int resultCount, long elapsedMs)
    {
        Interlocked.Increment(ref _callCount);
        Interlocked.Add(ref _resultCount, resultCount);
        Interlocked.Add(ref _totalLatencyMs, elapsedMs);
    }
}
