using System.Diagnostics;

namespace ResearchAgent.App;

/// <summary>
/// Centralized OpenTelemetry instrumentation for the Research Agent.
///
/// Provides an ActivitySource for distributed tracing. Spans are created
/// throughout the orchestrator to track workflow phases, agent execution,
/// and event processing.
///
/// Usage:
///   using var activity = Diagnostics.Source.StartActivity("Orchestrator.RunWorkflow");
///   activity?.SetTag("session.id", sessionId);
///
/// To export traces, set Telemetry:Enabled=true in config. By default traces
/// go to the console exporter; swap for OTLP (Jaeger, Zipkin, Azure Monitor)
/// in production.
///
/// Metrics (counters/histograms) are intentionally omitted — they require a
/// MeterProvider and make little sense for a single-invocation CLI tool.
/// The structured logs and OTel spans provide all the diagnostics needed.
/// </summary>
public static class Diagnostics
{
    public const string ServiceName = "ResearchAgent";
    public const string ServiceVersion = "0.1.0";

    public static readonly ActivitySource Source = new(ServiceName, ServiceVersion);
}
