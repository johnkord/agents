using System;
using System.Reflection;
using ResearchAgent.App;
using Xunit;

namespace ResearchAgent.Tests;

/// <summary>
/// Locks in the inner-exception unwrapping added 2026-04-17 after the live Run G/H sweeps
/// where a real HTTP 429 quota error showed up as five empty sub-agents with null errorText.
/// Regression guard: if someone "simplifies" <c>FormatWorkflowException</c> to just read
/// <c>exception.Message</c>, operators are back to diagnosing by Serilog grepping.
/// </summary>
public class ResearchOrchestratorWorkflowErrorTests
{
    [Fact]
    public void Null_exception_uses_fallback_text()
    {
        var (outer, root, msg) = ResearchOrchestrator.FormatWorkflowException(null, "fallback-string");
        Assert.Equal("Unknown", outer);
        Assert.Equal("Unknown", root);
        Assert.Equal("fallback-string", msg);
    }

    [Fact]
    public void Flat_exception_reports_itself_as_both_outer_and_root()
    {
        var ex = new InvalidOperationException("raw config error");
        var (outer, root, msg) = ResearchOrchestrator.FormatWorkflowException(ex, "(unused)");
        Assert.Equal("InvalidOperationException", outer);
        Assert.Equal("InvalidOperationException", root);
        Assert.Equal("raw config error", msg);
    }

    [Fact]
    public void TargetInvocationException_wrapping_unwraps_to_inner()
    {
        // This is the shape MAF actually produces for LLM HTTP errors.
        var inner = new InvalidOperationException("HTTP 429 (insufficient_quota)");
        var outer = new TargetInvocationException("Error invoking handler", inner);
        var (outerType, rootType, rootMsg) = ResearchOrchestrator.FormatWorkflowException(outer, "(unused)");
        Assert.Equal("TargetInvocationException", outerType);
        Assert.Equal("InvalidOperationException", rootType);
        Assert.Equal("HTTP 429 (insufficient_quota)", rootMsg);
    }

    [Fact]
    public void Deeply_nested_exceptions_unwrap_to_deepest_leaf()
    {
        var leaf = new ApplicationException("leaf message");
        var mid = new TargetInvocationException("mid", leaf);
        var top = new TargetInvocationException("top", mid);
        var (outerType, rootType, rootMsg) = ResearchOrchestrator.FormatWorkflowException(top, "fb");
        Assert.Equal("TargetInvocationException", outerType);
        Assert.Equal("ApplicationException", rootType);
        Assert.Equal("leaf message", rootMsg);
    }

    [Fact]
    public void Aggregate_exception_unwraps_only_inner_exception_chain_not_inner_exceptions_collection()
    {
        // AggregateException.InnerException returns InnerExceptions[0]; this test pins
        // that we walk that one chain rather than merging. Matches the guarantee given by
        // Exception.InnerException, which is all we depend on.
        var sideFault = new TimeoutException("unrelated");
        var primary = new InvalidOperationException("the real cause");
        var agg = new AggregateException("multiple things went wrong", primary, sideFault);
        var (outerType, rootType, rootMsg) = ResearchOrchestrator.FormatWorkflowException(agg, "fb");
        Assert.Equal("AggregateException", outerType);
        // AggregateException.InnerException == InnerExceptions[0] == primary; primary has no inner.
        Assert.Equal("InvalidOperationException", rootType);
        Assert.Equal("the real cause", rootMsg);
    }

    [Fact]
    public void Null_message_on_root_is_allowed_but_would_never_happen_in_practice()
    {
        // Exception.Message is never null in .NET (framework provides a default),
        // but the code path has a fallback anyway. This test documents the contract.
        var ex = new InvalidOperationException();
        var (_, _, msg) = ResearchOrchestrator.FormatWorkflowException(ex, "fb");
        // Default message is non-null & non-empty even without a custom string.
        Assert.False(string.IsNullOrEmpty(msg));
    }
}
