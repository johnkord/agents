using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace MCPServer.ToolApproval.LlmApproval;

/// <summary>
/// Interface for caching LLM approval decisions
/// </summary>
public interface ILlmDecisionCache
{
    /// <summary>
    /// Get a cached decision
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached decision or null if not found</returns>
    Task<LlmApprovalDecision?> GetDecisionAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cache a decision
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="decision">Decision to cache</param>
    /// <param name="ttl">Time to live</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CacheDecisionAsync(string key, LlmApprovalDecision decision, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear expired entries from cache
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ClearExpiredAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cache statistics
    /// </summary>
    LlmCacheStatistics GetStatistics();
}

/// <summary>
/// Cache entry for LLM decisions
/// </summary>
/// <param name="Decision">The cached decision</param>
/// <param name="ExpiresAt">When the cache entry expires</param>
public record LlmCacheEntry(LlmApprovalDecision Decision, DateTimeOffset ExpiresAt);

/// <summary>
/// Cache statistics
/// </summary>
/// <param name="TotalRequests">Total cache requests</param>
/// <param name="CacheHits">Number of cache hits</param>
/// <param name="CacheMisses">Number of cache misses</param>
/// <param name="CachedEntries">Current number of cached entries</param>
/// <param name="HitRate">Cache hit rate (0.0 to 1.0)</param>
public record LlmCacheStatistics(
    long TotalRequests,
    long CacheHits,
    long CacheMisses,
    int CachedEntries,
    double HitRate);

/// <summary>
/// In-memory implementation of LLM decision cache
/// </summary>
public class InMemoryLlmDecisionCache : ILlmDecisionCache
{
    private readonly ConcurrentDictionary<string, LlmCacheEntry> _cache = new();
    private long _totalRequests = 0;
    private long _cacheHits = 0;
    private long _cacheMisses = 0;

    public Task<LlmApprovalDecision?> GetDecisionAsync(string key, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _totalRequests);

        if (_cache.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAt > DateTimeOffset.UtcNow)
            {
                Interlocked.Increment(ref _cacheHits);
                return Task.FromResult<LlmApprovalDecision?>(entry.Decision);
            }
            else
            {
                // Entry has expired, remove it
                _cache.TryRemove(key, out _);
            }
        }

        Interlocked.Increment(ref _cacheMisses);
        return Task.FromResult<LlmApprovalDecision?>(null);
    }

    public Task CacheDecisionAsync(string key, LlmApprovalDecision decision, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(ttl);
        var entry = new LlmCacheEntry(decision, expiresAt);
        
        _cache.AddOrUpdate(key, entry, (k, existing) => entry);
        
        return Task.CompletedTask;
    }

    public Task ClearExpiredAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var expiredKeys = new List<string>();

        foreach (var kvp in _cache)
        {
            if (kvp.Value.ExpiresAt <= now)
            {
                expiredKeys.Add(kvp.Key);
            }
        }

        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    public LlmCacheStatistics GetStatistics()
    {
        var totalRequests = Interlocked.Read(ref _totalRequests);
        var cacheHits = Interlocked.Read(ref _cacheHits);
        var cacheMisses = Interlocked.Read(ref _cacheMisses);
        var hitRate = totalRequests > 0 ? (double)cacheHits / totalRequests : 0.0;

        return new LlmCacheStatistics(
            totalRequests,
            cacheHits,
            cacheMisses,
            _cache.Count,
            hitRate);
    }
}