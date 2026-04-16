using Forge.Core;

namespace Forge.Tests;

/// <summary>
/// Tests for AuxiliaryCallLimiter — circuit breaker for auxiliary (non-main-agent)
/// model/tool calls (summarizers, consolidators, memory updaters). Prevents runaway
/// retry loops burning tokens when an auxiliary path is chronically failing.
/// </summary>
public class AuxiliaryCallLimiterTests
{
    [Fact]
    public void Allow_IsTrueByDefault()
    {
        var limiter = new AuxiliaryCallLimiter();
        Assert.True(limiter.Allow("summarizer"));
    }

    [Fact]
    public void RecordFailure_DoesNotTripBeforeThreshold()
    {
        var limiter = new AuxiliaryCallLimiter(threshold: 3);
        Assert.False(limiter.RecordFailure("summarizer", "oops"));
        Assert.False(limiter.RecordFailure("summarizer", "oops"));
        Assert.True(limiter.Allow("summarizer"));
        Assert.Equal(2, limiter.FailureCount("summarizer"));
    }

    [Fact]
    public void RecordFailure_TripsAtThreshold_AndBlocksCalls()
    {
        var limiter = new AuxiliaryCallLimiter(threshold: 3);
        limiter.RecordFailure("summarizer");
        limiter.RecordFailure("summarizer");
        var tripped = limiter.RecordFailure("summarizer");

        Assert.True(tripped);
        Assert.False(limiter.Allow("summarizer"));
    }

    [Fact]
    public void RecordFailure_OnlyReturnsTrueOnTheTrippingCall()
    {
        var limiter = new AuxiliaryCallLimiter(threshold: 2);
        Assert.False(limiter.RecordFailure("x"));
        Assert.True(limiter.RecordFailure("x"));
        // further failures don't re-report as tripping
        Assert.False(limiter.RecordFailure("x"));
    }

    [Fact]
    public void RecordSuccess_ResetsFailureCount_AndUntripsBreaker()
    {
        var limiter = new AuxiliaryCallLimiter(threshold: 2);
        limiter.RecordFailure("x");
        limiter.RecordFailure("x");
        Assert.False(limiter.Allow("x"));

        limiter.RecordSuccess("x");
        Assert.True(limiter.Allow("x"));
        Assert.Equal(0, limiter.FailureCount("x"));
    }

    [Fact]
    public void DifferentNames_TrackedIndependently()
    {
        var limiter = new AuxiliaryCallLimiter(threshold: 2);
        limiter.RecordFailure("a");
        limiter.RecordFailure("a");
        Assert.False(limiter.Allow("a"));
        Assert.True(limiter.Allow("b"));
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var limiter = new AuxiliaryCallLimiter(threshold: 2);
        limiter.RecordFailure("x");
        limiter.RecordFailure("x");
        limiter.Reset("x");
        Assert.True(limiter.Allow("x"));
        Assert.Equal(0, limiter.FailureCount("x"));
    }

    [Fact]
    public void Constructor_RejectsNonPositiveThreshold()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AuxiliaryCallLimiter(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AuxiliaryCallLimiter(-1));
    }
}
