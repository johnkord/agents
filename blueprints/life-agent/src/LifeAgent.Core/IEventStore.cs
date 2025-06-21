using LifeAgent.Core.Events;

namespace LifeAgent.Core;

/// <summary>
/// Append-only event store. All state mutations flow through here.
/// Implementations: SQLite (dev/single-node), PostgreSQL (production).
/// </summary>
public interface IEventStore
{
    /// <summary>Append an event to the store. Returns the sequence number.</summary>
    Task<long> AppendAsync(LifeEvent evt, CancellationToken ct = default);

    /// <summary>Append a batch of events atomically.</summary>
    Task<long> AppendBatchAsync(IReadOnlyList<LifeEvent> events, CancellationToken ct = default);

    /// <summary>Read all events since a given sequence number (inclusive).</summary>
    IAsyncEnumerable<(long Sequence, LifeEvent Event)> ReadFromAsync(
        long fromSequence, CancellationToken ct = default);

    /// <summary>Read events matching a specific type within a date range.</summary>
    IAsyncEnumerable<LifeEvent> ReadByTypeAsync<T>(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
        where T : LifeEvent;

    /// <summary>Get the current latest sequence number.</summary>
    Task<long> GetLatestSequenceAsync(CancellationToken ct = default);
}
