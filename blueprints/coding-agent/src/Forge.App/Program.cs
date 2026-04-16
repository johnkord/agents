using System.ClientModel;
using Forge.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using OpenAI;
using OpenAI.Responses;
using Serilog;
using Serilog.Formatting.Compact;

// ── Configuration ──────────────────────────────────────────────────────────────
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables("FORGE_")
    .AddUserSecrets<Program>(optional: true)
    .AddCommandLine(args)
    .Build();

var dryRun = args.Any(arg => string.Equals(arg, "--dry-run", StringComparison.OrdinalIgnoreCase));
var analyzeTarget = args.SkipWhile(a => !string.Equals(a, "--analyze", StringComparison.OrdinalIgnoreCase))
    .Skip(1).FirstOrDefault();
var diffMode = args.Any(a => string.Equals(a, "--diff", StringComparison.OrdinalIgnoreCase));
var resumeFile = args.SkipWhile(a => !string.Equals(a, "--resume", StringComparison.OrdinalIgnoreCase))
    .Skip(1).FirstOrDefault();
var sessionMemoryOverride = args.Any(a => string.Equals(a, "--session-memory", StringComparison.OrdinalIgnoreCase));

// ── Analyze mode: offline session analysis ─────────────────────────────────────
if (!string.IsNullOrEmpty(analyzeTarget))
{
    // --diff mode: compare exactly two files side-by-side.
    // Usage: forge --analyze A.jsonl B.jsonl --diff
    if (diffMode)
    {
        var analyzeIdx = Array.FindIndex(args, a => string.Equals(a, "--analyze", StringComparison.OrdinalIgnoreCase));
        var pathA = analyzeIdx >= 0 && analyzeIdx + 1 < args.Length ? args[analyzeIdx + 1] : null;
        var pathB = analyzeIdx >= 0 && analyzeIdx + 2 < args.Length && !args[analyzeIdx + 2].StartsWith("--")
            ? args[analyzeIdx + 2] : null;

        if (pathA is null || pathB is null || !File.Exists(pathA) || !File.Exists(pathB))
        {
            Console.WriteLine("--diff requires two existing session file paths: forge --analyze A.jsonl B.jsonl --diff");
            Environment.Exit(1);
        }

        var sA = SessionAnalyzer.Analyze(pathA);
        var sB = SessionAnalyzer.Analyze(pathB);
        if (sA is null || sB is null)
        {
            Console.WriteLine("Could not parse one or both session files.");
            Environment.Exit(1);
        }

        Console.WriteLine(SessionAnalyzer.FormatDiff(sA, sB));
        Environment.Exit(0);
    }

    var files = Directory.Exists(analyzeTarget)
        ? Directory.GetFiles(analyzeTarget, "*.jsonl")
        : File.Exists(analyzeTarget)
            ? new[] { analyzeTarget }
            : Directory.GetFiles(Path.GetDirectoryName(analyzeTarget) ?? ".", Path.GetFileName(analyzeTarget));

    var analyses = files
        .Select(SessionAnalyzer.Analyze)
        .Where(a => a is not null)
        .Cast<SessionAnalysis>()
        .ToList();

    if (analyses.Count == 0)
    {
        Console.WriteLine($"No valid session files found at: {analyzeTarget}");
        Environment.Exit(1);
    }

    if (analyses.Count == 1)
    {
        Console.WriteLine(SessionAnalyzer.FormatReport(analyses[0]));
    }
    else
    {
        foreach (var a in analyses)
        {
            Console.WriteLine(SessionAnalyzer.FormatReport(a));
            Console.WriteLine();
        }
        Console.WriteLine("---");
        Console.WriteLine(SessionAnalyzer.FormatAggregate(analyses));
    }
    Environment.Exit(0);
}

var model = config["Model"] ?? "gpt-5.4";
var mcpServerUrl = config["McpServerUrl"] ?? "http://localhost:5000/mcp";
var workspace = config["Workspace"] ?? Directory.GetCurrentDirectory();
var sessionsDir = config["SessionsDir"] ?? Path.Combine(workspace, "sessions");

// ── Logging (Serilog) ──────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Forge")
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        new RenderedCompactJsonFormatter(),
        path: Path.Combine(sessionsDir, "forge-.clef"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .CreateLogger();

try
{
    Log.Information("Forge v0.1 — workspace: {Workspace}", workspace);
    Log.Information("Model: {Model} | MCP: {McpUrl}", model, mcpServerUrl);

    if (dryRun)
    {
        Log.Information("Dry-run mode enabled; the LLM will not be called.");
    }

    // ── Get the task (or resume from previous session) ───────────────────────
    var task = string.Empty;
    SessionHandoff? resumeHandoff = null;

    if (!dryRun)
    {
        // Handle --resume: load handoff note from previous session
        if (!string.IsNullOrEmpty(resumeFile))
        {
            resumeHandoff = HandoffGenerator.LoadFromSessionFile(resumeFile);
            if (resumeHandoff is null)
            {
                Log.Error("Could not load handoff note from {File}. Ensure the file is a valid session JSONL.", resumeFile);
                return 1;
            }
            task = resumeHandoff.Task;
            Log.Information("Resuming session: {Task} (status: {Status}, {Steps} steps completed)",
                task, resumeHandoff.Status, resumeHandoff.StepsCompleted);
        }
        else if (args.Length > 0 && !args[0].StartsWith("--"))
        {
            task = string.Join(" ", args.TakeWhile(a => !a.StartsWith("--")));
        }
        else
        {
            Console.Write("Enter task: ");
            task = Console.ReadLine()?.Trim() ?? "";
        }

        if (string.IsNullOrWhiteSpace(task))
        {
            Log.Warning("No task provided. Exiting.");
            return 1;
        }
    }

    // ── MCP Client ─────────────────────────────────────────────────────────────
    Log.Information("Connecting to MCP server at {Url}...", mcpServerUrl);
    var mcpTransport = new HttpClientTransport(new HttpClientTransportOptions
    {
        Endpoint = new Uri(mcpServerUrl),
    });
    await using var mcpClient = await McpClient.CreateAsync(mcpTransport);
    var mcpTools = await mcpClient.ListToolsAsync();
    var mcpToolList = mcpTools.ToList<AITool>();
    Log.Information("Discovered {Count} MCP tools", mcpTools.Count);

    // ── Agent Options ──────────────────────────────────────────────────────────
    // Fallback values here match AgentOptions class defaults. In production,
    // appsettings.json provides values (e.g., ReasoningEffort=Medium, MaxTotalTokens=500000).
    var options = new AgentOptions
    {
        Model = model,
        MaxSteps = int.TryParse(config["MaxSteps"], out var ms) ? ms : 30,
        MaxTotalTokens = int.TryParse(config["MaxTotalTokens"], out var mt) ? mt : 500_000,
        Temperature = float.TryParse(config["Temperature"], out var temp) ? temp : 0f,
        ReasoningEffort = Enum.TryParse<ReasoningEffort>(config["ReasoningEffort"], true, out var re) ? re : null,
        ObservationMaxLines = int.TryParse(config["ObservationMaxLines"], out var oml) ? oml : 200,
        ObservationMaxChars = int.TryParse(config["ObservationMaxChars"], out var omc) ? omc : 10_000,
        DryRun = dryRun,
        WorkspacePath = workspace,
        SessionsPath = sessionsDir,
        LessonsPath = config["LessonsPath"] ?? Path.Combine(sessionsDir, "lessons.md"),
        ToolMode = config["ToolMode"],
        // ── P2.1: session memory ──────────────────────────────────────────────
        SessionMemoryEnabled = sessionMemoryOverride
            || bool.TryParse(config["SessionMemoryEnabled"], out var sme) && sme,
        SessionMemoryMinInitTokens = int.TryParse(config["SessionMemoryMinInitTokens"], out var smit)
            ? smit : 15_000,
        SessionMemoryStepsBetweenUpdates = int.TryParse(config["SessionMemoryStepsBetweenUpdates"], out var smsu)
            ? smsu : 5,
        SessionMemoryRoot = config["SessionMemoryRoot"] ?? ".forge/session-memory",
        SessionMemoryPersistRawResponses = bool.TryParse(config["SessionMemoryPersistRawResponses"], out var smprr) && smprr,
    };

    if (options.DryRun)
    {
        Console.WriteLine(DryRunPreview.Build(options, mcpToolList));
        return 0;
    }

    // ── LLM Client (OpenAI Responses API) ──────────────────────────────────────
    // Try multiple config keys in order of preference so a single shared secret can
    // power all three blueprints (research-agent + life-agent use AI:ApiKey; this
    // one historically used OpenAIKey / OPENAI_API_KEY). The old keys stay accepted
    // for back-compat with existing user-secrets stores.
    var apiKey = config["AI:ApiKey"]
        ?? config["OPENAI_API_KEY"]
        ?? config["OpenAIKey"]
        ?? throw new InvalidOperationException(
            "Set the OpenAI API key. Supported config keys (checked in order): AI:ApiKey, OPENAI_API_KEY, OpenAIKey. " +
            "Source priority: command-line → environment variables (FORGE_ prefix) → user-secrets → appsettings.json.");

    var openAiClient = new OpenAIClient(new ApiKeyCredential(apiKey));
    var responsesClient = openAiClient.GetResponsesClient();
    await using var llmClient = new OpenAIResponsesLlmClient(
        responsesClient, model, options, mcpToolList, Log.Logger);

    Log.Information("Using OpenAI Responses API (reasoning effort: {Effort})",
        options.ReasoningEffort?.ToString() ?? "default");

    // ── Run the Agent ──────────────────────────────────────────────────────────
    var agent = new AgentLoop(options, Log.Logger);

    // ── Session-memory extractor (P2.1) ────────────────────────────────────────
    // Built only when enabled. Uses a dedicated OpenAIResponsesLlmClient with
    // tools: [] to keep the extraction conversation isolated from the main loop.
    Forge.Core.SessionMemory.SessionMemoryExtractor? memoryExtractor = null;
    if (options.SessionMemoryEnabled)
    {
        memoryExtractor = Forge.App.SessionMemoryWiring.Build(responsesClient, model, options, Log.Logger);
        Log.Information("Session memory ENABLED (min-init={Init} tokens, every {Steps} steps, root={Root})",
            options.SessionMemoryMinInitTokens, options.SessionMemoryStepsBetweenUpdates, options.SessionMemoryRoot);
    }

    // Set up cancellation for graceful SIGINT handling
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true; // Prevent immediate termination
        Log.Information("SIGINT received — finishing current step and writing handoff note...");
        Console.WriteLine("\n⏹ Interrupted — writing handoff note...");
        cts.Cancel();
    };

    // If resuming, build the continuation context separately from the original task
    string? continuationContext = null;
    if (resumeHandoff is not null)
    {
        continuationContext = HandoffGenerator.BuildContinuationPrompt(resumeHandoff)
            + "\n\nOriginal task: " + task;
        Log.Information("Injected continuation context from previous session ({Chars} chars)", continuationContext.Length);
    }

    var result = await agent.RunAsync(task, llmClient, mcpToolList, token => Console.Write(token), cts.Token, continuationContext, memoryExtractor);

    // ── Print Result ───────────────────────────────────────────────────────────
    Console.WriteLine();
    Console.WriteLine();
    if (result.Success)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ Task completed");
    }
    else if (result.Output.Contains("task may be complete", StringComparison.OrdinalIgnoreCase))
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("⚠ Budget exhausted — but edits were verified successfully");
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("✗ Task failed");
    }
    Console.ResetColor();
    Console.WriteLine();
    Console.WriteLine(result.Output);
    Console.WriteLine();
    Console.WriteLine($"Steps: {result.Steps.Count} | Tokens: {result.TotalPromptTokens + result.TotalCompletionTokens:N0} (prompt: {result.TotalPromptTokens:N0}, completion: {result.TotalCompletionTokens:N0}) | Duration: {result.TotalDurationMs / 1000:F1}s");

    if (result.SessionLogPath is not null)
    {
        Console.WriteLine($"Session log: {result.SessionLogPath}");
    }

    return result.Success ? 0 : 1;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Forge crashed");
    return 2;
}
finally
{
    await Log.CloseAndFlushAsync();
}

// Required for user secrets
public partial class Program;
