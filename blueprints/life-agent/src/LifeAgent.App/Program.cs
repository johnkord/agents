using System.Threading.Channels;
using LifeAgent.App;
using LifeAgent.App.Channels;
using LifeAgent.App.Orchestrator;
using LifeAgent.App.Scheduler;
using LifeAgent.App.Services;
using LifeAgent.App.Workers;
using LifeAgent.Audio;
using LifeAgent.Core;
using LifeAgent.Core.Events;
using LifeAgent.Core.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Trace;

// ── Configuration ──────────────────────────────────────────────────

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .AddUserSecrets<Program>();

// ── Logging ────────────────────────────────────────────────────────

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var minLevel = builder.Configuration.GetValue<string>("Logging:MinLevel");
if (Enum.TryParse<LogLevel>(minLevel, true, out var level))
    builder.Logging.SetMinimumLevel(level);

// ── OpenTelemetry ──────────────────────────────────────────────────

if (builder.Configuration.GetValue<bool>("Telemetry:Enabled"))
{
    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing => tracing
            .AddSource(Diagnostics.ServiceName)
            .AddConsoleExporter());
}

// ── Event store ────────────────────────────────────────────────────

var eventStoreDbPath = builder.Configuration.GetValue<string>("Storage:EventStoreDbPath") ?? "data/events.db";
var eventStoreDir = Path.GetDirectoryName(eventStoreDbPath);
if (!string.IsNullOrEmpty(eventStoreDir))
    Directory.CreateDirectory(eventStoreDir);

builder.Services.AddSingleton<IEventStore>(sp =>
{
    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<SqliteEventStore>();
    var store = new SqliteEventStore(eventStoreDbPath, logger);
    store.InitializeAsync().GetAwaiter().GetResult();
    return store;
});

// ── LLM chat completion (shared by orchestrator + audio pipeline) ──

builder.Services.AddSingleton<IChatCompletionService, OpenAIChatService>();

// ── Audio lifelogging pipeline ─────────────────────────────────────

var conversationalMemoryDbPath = builder.Configuration.GetValue<string>("Storage:ConversationalMemoryDbPath")
    ?? "data/conversational-memory.db";

builder.Services.AddAudioLifelogging(conversationalMemoryDbPath);

// ── Notification channels ──────────────────────────────────────────

builder.Services.AddSingleton<INotificationChannel, ConsoleChannel>();

// ── Orchestrator ───────────────────────────────────────────────────

// Shared event channel — all components submit events through this
builder.Services.AddSingleton(Channel.CreateUnbounded<LifeEvent>(
    new UnboundedChannelOptions { SingleReader = true }));

// State projection (singleton — rebuilt from event log on startup)
builder.Services.AddSingleton<LifeAgentState>();

// Core orchestrator components
builder.Services.AddSingleton<TaskClassifier>();
builder.Services.AddSingleton<LifeAgentOrchestrator>();

// ── Worker agents ──────────────────────────────────────────────────

builder.Services.AddSingleton<IWorkerAgent, AudioLifelogAgent>();
builder.Services.AddSingleton<IWorkerAgent, ReminderWorker>();
builder.Services.AddSingleton<IWorkerAgent, GeneralWorker>();
builder.Services.AddSingleton<IWorkerAgent, DailyBriefingWorker>();

// ── Background services (order matters: orchestrator first, then scanner, scheduler, CLI) ──

builder.Services.AddHostedService<OrchestratorService>();
builder.Services.AddHostedService<ProactivityScanner>();
builder.Services.AddHostedService<SchedulerService>();
builder.Services.AddHostedService<ConsoleInputService>();

// ── Build and run ──────────────────────────────────────────────────

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("LifeAgent");
logger.LogInformation("Life Agent v{Version} starting...", Diagnostics.ServiceVersion);
logger.LogInformation("Event store: {Path}", eventStoreDbPath);
logger.LogInformation("Conversational memory: {Path}", conversationalMemoryDbPath);

var audioConfig = builder.Configuration.GetSection("Audio");
if (!string.IsNullOrEmpty(audioConfig["DeepgramApiKey"]))
{
    logger.LogInformation("Audio pipeline: ENABLED (Deepgram {Model}, ws://0.0.0.0:{Port}/audio/)",
        audioConfig["DeepgramModel"], audioConfig["WebSocketPort"]);
}
else
{
    logger.LogWarning("Audio pipeline: DISABLED (no Deepgram API key configured)");
}

logger.LogInformation("Orchestrator: ENABLED (budget ${Limit:F2}/day)",
    builder.Configuration.GetValue<decimal>("Orchestrator:DailyBudgetUsd", 5.00m));

await host.RunAsync();

// Needed for user secrets
public partial class Program;
