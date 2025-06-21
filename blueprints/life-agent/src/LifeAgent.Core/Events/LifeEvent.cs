namespace LifeAgent.Core.Events;

/// <summary>
/// Base class for all Life Agent events. All state changes are captured as
/// immutable events following the ESAA (Event-Sourced Agentic Architecture) pattern.
/// State is a projection of these events — never mutated directly.
/// </summary>
public abstract record LifeEvent
{
    public string EventId { get; init; } = Ulid.NewUlid().ToString();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Minimal ULID generator. Produces lexicographically sortable, unique 26-char identifiers.
/// Avoids taking a dependency on a ULID NuGet package for this single use case.
/// </summary>
internal static class Ulid
{
    private static long _lastTimestamp;
    private static readonly Lock _lock = new();

    public static string NewUlid()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        lock (_lock)
        {
            if (timestamp <= _lastTimestamp)
                timestamp = _lastTimestamp + 1;
            _lastTimestamp = timestamp;
        }

        Span<byte> random = stackalloc byte[10];
        Random.Shared.NextBytes(random);

        return string.Create(26, (timestamp, random.ToArray()), static (span, state) =>
        {
            const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
            var (ts, rnd) = state;

            // Encode 48-bit timestamp as 10 Crockford Base32 chars
            for (int i = 9; i >= 0; i--)
            {
                span[i] = alphabet[(int)(ts & 0x1F)];
                ts >>= 5;
            }

            // Encode 80-bit randomness as 16 Crockford Base32 chars
            int bitIndex = 0;
            for (int i = 0; i < 16; i++)
            {
                int byteIdx = bitIndex / 8;
                int bitOff = bitIndex % 8;
                int val = (rnd[byteIdx] >> (8 - bitOff - 5));
                if (bitOff > 3 && byteIdx + 1 < rnd.Length)
                    val |= rnd[byteIdx + 1] << (8 - bitOff);
                span[10 + i] = alphabet[val & 0x1F];
                bitIndex += 5;
            }
        });
    }
}
