using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ResearchAgent.App;
using ResearchAgent.Core.Models;

// ──────────────────────────────────────────────────────────────────
// Research Agent — General Purpose Research Agent Blueprint
//
// Built on: .NET 10 + Microsoft Agent Framework
//
// Architecture (from our paper collection):
//   Pensieve/StateLM  → Self-context engineering (read-note-prune)
//   CASTER            → Sequential multi-agent workflow
//   Agentic RAG       → Dynamic multi-step retrieval
//   HiMAC             → Hierarchical task decomposition
//
// Output model:
//   Report      → stdout (pipeable: `dotnet run -- "query" > report.md`)
//   Progress    → stderr (phase transitions, finding count, timing)
//   State file  → {SessionDir}/{sessionId}.state.json (importable via --prior)
//   Session log → {SessionDir}/{sessionId}.json (full trajectory for analysis)
//
// Pipeline: Planner → [Researcher ↔ Analyst]×N → Synthesizer → Verifier
// ──────────────────────────────────────────────────────────────────

// Redirect console output to stderr so stdout is reserved exclusively for the report.
// The ILogger ConsoleProvider uses Console.Out, which would otherwise contaminate stdout.
var stdout = Console.Out;
Console.SetOut(Console.Error);

var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
};

var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development";

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile($"appsettings.{environment}.json", optional: true)
    .AddEnvironmentVariables("RESEARCH_AGENT_")
    .AddUserSecrets<Program>(optional: true)
    .AddCommandLine(args)
    .Build();

// ── OpenTelemetry ──────────────────────────────────────────
var enableOTel = config.GetValue("Telemetry:Enabled", false);

TracerProvider? tracerProvider = null;
if (enableOTel)
{
    tracerProvider = Sdk.CreateTracerProviderBuilder()
        .SetResourceBuilder(ResourceBuilder.CreateDefault()
            .AddService(Diagnostics.ServiceName, serviceVersion: Diagnostics.ServiceVersion))
        .AddSource(Diagnostics.ServiceName)
        .AddConsoleExporter()
        .Build();
}

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddConsole()
        .SetMinimumLevel(
            Enum.TryParse<LogLevel>(config["Logging:MinLevel"], out var level)
                ? level
                : LogLevel.Information);
});

var logger = loggerFactory.CreateLogger("ResearchAgent");

// ── Parse arguments ────────────────────────────────────────
// Supports: dotnet run -- "query" [--prior <path>] [--config:Key=Value...]
var (query, priorStatePath) = ParseArguments(args);

if (string.IsNullOrWhiteSpace(query))
{
    query = PromptForQuery();
}

if (string.IsNullOrWhiteSpace(query))
{
    logger.LogError("No research query provided. Usage: dotnet run -- \"your research question\" [--prior <state-file>]");
    return 1;
}

// ── Load prior state (if specified) ────────────────────────
ResearchStateFile? priorState = null;
if (!string.IsNullOrWhiteSpace(priorStatePath))
{
    try
    {
        var json = await File.ReadAllTextAsync(priorStatePath);
        priorState = JsonSerializer.Deserialize<ResearchStateFile>(json, jsonOptions);
        if (priorState is null)
        {
            logger.LogError("Failed to deserialize prior state file: {Path}", priorStatePath);
            return 1;
        }
        if (priorState.Version > 1)
        {
            logger.LogError("Unsupported state file version {Version} (expected 1): {Path}", priorState.Version, priorStatePath);
            return 1;
        }
        WriteStderr($"Loaded prior state: session {priorState.Metadata.SessionId} — {priorState.Findings.Count} findings, {priorState.Sources.Count} sources");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to load prior state file: {Path}", priorStatePath);
        return 1;
    }
}

logger.LogInformation("Research Agent starting | env={Environment} | provider={Provider} | model={Model} | otel={OTel} | logLevel={LogLevel} | prior={Prior}",
    environment,
    config["AI:Provider"] ?? "openai",
    config["AI:Model"] ?? "gpt-4o",
    enableOTel ? "on" : "off",
    Enum.TryParse<LogLevel>(config["Logging:MinLevel"], out var parsedLevel) ? parsedLevel : LogLevel.Information,
    priorState is not null ? priorState.Metadata.SessionId : "none");
logger.LogInformation("Query: {Query}", query);

try
{
    // ── Progress callback → stderr ─────────────────────────
    var progress = new Progress<ResearchProgressEvent>(evt =>
    {
        WriteStderr(evt.ToString());
    });

    var orchestrator = new ResearchOrchestrator(config, loggerFactory, priorState, progress);

    var overallSw = Stopwatch.StartNew();
    var result = await orchestrator.ResearchAsync(query);
    overallSw.Stop();

    logger.LogInformation("Research completed in {ElapsedMs}ms ({ElapsedSec:F1}s)",
        overallSw.ElapsedMilliseconds, overallSw.Elapsed.TotalSeconds);

    // ── Output: Report → stdout ────────────────────────────
    // The report goes to the original stdout (saved before redirect) so it can
    // be piped: dotnet run -- "query" > report.md
    stdout.WriteLine(result.Report ?? "(No report generated)");

    // ── Output: Metadata → stderr ──────────────────────────
    WriteStderr("");
    WriteStderr("──────────────────────────────────────────────────────");
    WriteStderr($"Session: {result.SessionId}");
    if (result.PriorSessionId is not null)
        WriteStderr($"Prior session: {result.PriorSessionId}");
    WriteStderr($"Findings: {result.Findings.Count}");
    WriteStderr($"Sources: {result.Sources.Count}");
    WriteStderr($"Agent interactions: {result.AgentInteractions.Count}");
    WriteStderr($"Research iterations: {result.IterationCount}");
    if (result.VerificationResult is not null)
    {
        WriteStderr($"Verification: {result.VerificationResult.PassedItems}/{result.VerificationResult.TotalItems} claims passed ({result.VerificationResult.PassRate:P0})");
        var failed = result.VerificationResult.Items
            .Where(i => i.Verdict != VerificationVerdict.Supported)
            .Select(i => i.Claim)
            .ToList();
        if (failed.Count > 0)
        {
            WriteStderr("Failed claims:");
            foreach (var claim in failed)
                WriteStderr($"  ✗ {claim}");
        }
    }
    WriteStderr($"Duration: {overallSw.Elapsed.TotalSeconds:F1}s");
    WriteStderr("──────────────────────────────────────────────────────");

    // Optionally output agent history
    if (config.GetValue("Output:ShowHistory", false))
    {
        WriteStderr("");
        WriteStderr("AGENT HISTORY:");
        foreach (var msg in result.AgentHistory)
        {
            WriteStderr($"  [{msg.AuthorName}] {Truncate(msg.Text ?? "", 150)}");
        }
    }

    // Optionally output context log
    if (config.GetValue("Output:ShowContextLog", false))
    {
        WriteStderr("");
        WriteStderr("CONTEXT LOG (Memory Operations):");
        foreach (var entry in result.ContextLog)
        {
            WriteStderr($"  {entry}");
        }
    }

    // ── State File Export ───────────────────────────────────
    // The importable research state — findings, sources, plan, reflections, quality.
    // This is the file you pass to --prior for follow-up sessions.
    var sessionDir = config["Output:SessionDir"] ?? "sessions";
    if (!string.IsNullOrWhiteSpace(sessionDir))
    {
        Directory.CreateDirectory(sessionDir);

        var failedClaims = result.VerificationResult?.Items
            .Where(i => i.Verdict != VerificationVerdict.Supported)
            .Select(i => i.Claim)
            .ToList();

        var stateFile = new ResearchStateFile
        {
            Metadata = new StateFileMetadata
            {
                SessionId = result.SessionId,
                Query = result.Query,
                CreatedAt = result.StartedAt,
                CompletedAt = result.CompletedAt,
                Model = result.Model,
                Provider = result.Provider,
                ParentSessionId = result.PriorSessionId,
            },
            Plan = new PlanSnapshot
            {
                RawPlan = result.PlannerOutput ?? "",
                SubQuestionIds = result.SubQuestionProgress.Select(p => p.SubQuestionId).ToList(),
                CompletedQuestionIds = result.SubQuestionProgress
                    .Where(p => p.MarkedComplete)
                    .Select(p => p.SubQuestionId)
                    .ToList(),
            },
            Findings = result.Findings,
            Sources = result.Sources,
            Reflections = result.Reflections,
            Quality = new QualitySnapshot
            {
                FindingCount = result.Findings.Count,
                SourceCount = result.Sources.Count,
                AverageFindingConfidence = result.Findings.Count > 0
                    ? result.Findings.Average(f => f.Confidence)
                    : 0,
                IterationCount = result.IterationCount,
                ReflectionCount = result.ReflectionCount,
                VerificationPassRate = result.VerificationResult?.PassRate,
                FailedClaims = failedClaims,
            },
        };

        var statePath = Path.Combine(sessionDir, $"{result.SessionId}.state.json");
        await File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(stateFile, jsonOptions));
        WriteStderr($"State file: {statePath}");

        // ── Session Export (full trajectory for analysis) ───
        var export = new SessionExport
        {
            SessionId = result.SessionId,
            Query = result.Query,
            Provider = result.Provider,
            Model = result.Model,
            StartedAt = result.StartedAt,
            CompletedAt = result.CompletedAt,
            Report = result.Report,
            AgentInteractions = result.AgentInteractions,
            Findings = result.Findings,
            Sources = result.Sources,
            ContextLog = result.ContextLog,
            Metrics = new SessionMetrics
            {
                FindingCount = result.Findings.Count,
                SourceCount = result.Sources.Count,
                AgentInteractionCount = result.AgentInteractions.Count,
                ReportCharCount = result.Report?.Length ?? 0,
                AverageFindingConfidence = result.Findings.Count > 0
                    ? result.Findings.Average(f => f.Confidence)
                    : 0,
                ContextLogEntryCount = result.ContextLog.Count,
                IterationCount = result.IterationCount,
                ReflectionCount = result.ReflectionCount,
                VerificationChecklistItems = result.VerificationResult?.TotalItems ?? 0,
                VerificationItemsPassed = result.VerificationResult?.PassedItems ?? 0,
                VerificationPassRate = result.VerificationResult?.PassRate ?? 0,
                VerificationFailedItems = failedClaims,
            }
        };

        var sessionPath = Path.Combine(sessionDir, $"{result.SessionId}.json");
        await File.WriteAllTextAsync(sessionPath, JsonSerializer.Serialize(export, jsonOptions));
        WriteStderr($"Session log: {sessionPath}");
    }

    return 0;
}
catch (InvalidOperationException ex) when (ex.Message.Contains("ApiKey"))
{
    logger.LogError("API key not configured. Set it with one of:");
    logger.LogError("  dotnet user-secrets set AI:ApiKey <your-key>");
    logger.LogError("  export RESEARCH_AGENT_AI__APIKEY=<your-key>");
    logger.LogError("  Add AI:ApiKey to appsettings.json");
    return 1;
}
catch (Exception ex)
{
    logger.LogError(ex, "Research session failed");
    return 1;
}
finally
{
    // Flush OTel traces before exit
    tracerProvider?.Dispose();
}

// ──────────────────────────────────────────────────────────────────
// Helpers
// ──────────────────────────────────────────────────────────────────

static (string? query, string? priorPath) ParseArguments(string[] args)
{
    string? query = null;
    string? priorPath = null;

    var queryParts = new List<string>();
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] == "--prior" && i + 1 < args.Length)
        {
            priorPath = args[++i];
        }
        else if (args[i].StartsWith("--"))
        {
            // Skip config arguments (--AI:Model=gpt-4.1 etc.)
            continue;
        }
        else
        {
            queryParts.Add(args[i]);
        }
    }

    if (queryParts.Count > 0)
        query = string.Join(" ", queryParts);

    return (query, priorPath);
}

static string? PromptForQuery()
{
    if (!Console.IsInputRedirected)
    {
        Console.Error.Write("Enter your research question: ");
        return Console.ReadLine();
    }
    return null;
}

static void WriteStderr(string message)
{
    Console.Error.WriteLine(message);
}

static string Truncate(string text, int maxLength) =>
    text.Length <= maxLength ? text : string.Concat(text.AsSpan(0, maxLength), "...");

// Needed for user-secrets
public partial class Program;
