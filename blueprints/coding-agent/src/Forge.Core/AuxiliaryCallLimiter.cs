namespace Forge.Core;

/// <summary>
/// Circuit breaker for auxiliary (non-main-agent) calls such as summarizers,
/// consolidators, or session-memory updaters. After N consecutive failures for
/// a given named caller, subsequent invocations are blocked until a success
/// resets the counter. This prevents runaway retry loops that silently burn
/// tokens when an auxiliary model or tool is chronically failing.
///
/// Thread-safe for concurrent callers; intended to be shared across a session.
/// </summary>
public sealed class AuxiliaryCallLimiter
{
    private readonly object _sync = new();
    private readonly Dictionary<string, int> _consecutiveFailures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _tripped = new(StringComparer.Ordinal);
    private readonly int _threshold;

    /// <param name="threshold">Consecutive failures before the named caller is blocked. Default 3.</param>
    public AuxiliaryCallLimiter(int threshold = 3)
    {
        if (threshold < 1) throw new ArgumentOutOfRangeException(nameof(threshold));
        _threshold = threshold;
    }

    /// <summary>
    /// Returns true if a call under <paramref name="name"/> is currently allowed.
    /// Once tripped, returns false until <see cref="RecordSuccess"/> is called
    /// (or <see cref="Reset"/> for the name).
    /// </summary>
    public bool Allow(string name)
    {
        if (string.IsNullOrEmpty(name)) return true;
        lock (_sync)
        {
            return !_tripped.TryGetValue(name, out var tripped) || !tripped;
        }
    }

    /// <summary>
    /// Record a successful invocation. Clears any failure count and un-trips the breaker.
    /// </summary>
    public void RecordSuccess(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        lock (_sync)
        {
            _consecutiveFailures.Remove(name);
            _tripped.Remove(name);
        }
    }

    /// <summary>
    /// Record a failed invocation. Returns true if this failure caused the breaker
    /// to trip (i.e., failure count reached the threshold on this call).
    /// </summary>
    public bool RecordFailure(string name, string? reason = null)
    {
        if (string.IsNullOrEmpty(name)) return false;
        lock (_sync)
        {
            var count = _consecutiveFailures.TryGetValue(name, out var existing) ? existing + 1 : 1;
            _consecutiveFailures[name] = count;
            if (count >= _threshold && !(_tripped.TryGetValue(name, out var t) && t))
            {
                _tripped[name] = true;
                return true;
            }
            return false;
        }
    }

    /// <summary>Current consecutive-failure count for the named caller.</summary>
    public int FailureCount(string name)
    {
        lock (_sync)
        {
            return _consecutiveFailures.TryGetValue(name, out var c) ? c : 0;
        }
    }

    /// <summary>Manually reset the breaker for a specific name.</summary>
    public void Reset(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        lock (_sync)
        {
            _consecutiveFailures.Remove(name);
            _tripped.Remove(name);
        }
    }
}
