using System.Threading.Channels;
using LifeAgent.Core.Events;
using LifeAgent.Core.Memory;
using LifeAgent.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LifeAgent.App.Channels;

/// <summary>
/// Interactive CLI for submitting tasks and querying state.
/// Reads from stdin and writes events to the orchestrator channel.
///
/// Commands:
///   /task &lt;description&gt;  — Submit a new task
///   /status               — Show active tasks and budget
///   /workers              — List registered workers
///   /quit                 — Shut down the agent
///   (anything else)       — Treated as a task submission
/// </summary>
public sealed class ConsoleInputService : BackgroundService
{
    private readonly ChannelWriter<LifeEvent> _eventWriter;
    private readonly LifeAgentState _state;
    private readonly ILogger<ConsoleInputService> _logger;
    private int _taskCounter;

    public ConsoleInputService(
        Channel<LifeEvent> eventChannel,
        LifeAgentState state,
        ILogger<ConsoleInputService> logger)
    {
        _eventWriter = eventChannel.Writer;
        _state = state;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Wait for orchestrator to initialize
        await Task.Delay(TimeSpan.FromSeconds(2), ct);

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  ╔════════════════════════════════════════╗");
        Console.WriteLine("  ║          Life Agent v0.1               ║");
        Console.WriteLine("  ║  Type a task, or /help for commands    ║");
        Console.WriteLine("  ╚════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();

        while (!ct.IsCancellationRequested)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("  > ");
            Console.ResetColor();

            string? line;
            try
            {
                line = await Task.Run(Console.ReadLine, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (line is null) break;
            var input = line.Trim();
            if (string.IsNullOrEmpty(input)) continue;

            try
            {
                await HandleInputAsync(input, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling input");
                Console.WriteLine($"  Error: {ex.Message}");
            }
        }
    }

    private async Task HandleInputAsync(string input, CancellationToken ct)
    {
        if (input.StartsWith('/'))
        {
            var parts = input.Split(' ', 2);
            var command = parts[0].ToLowerInvariant();
            var arg = parts.Length > 1 ? parts[1] : "";

            switch (command)
            {
                case "/task":
                    if (string.IsNullOrWhiteSpace(arg))
                    {
                        Console.WriteLine("  Usage: /task <description>");
                        return;
                    }
                    await SubmitTaskAsync(arg, ct);
                    break;

                case "/status":
                    ShowStatus();
                    break;

                case "/workers":
                    ShowWorkers();
                    break;

                case "/budget":
                    ShowBudget();
                    break;

                case "/help":
                    ShowHelp();
                    break;

                case "/quit":
                case "/exit":
                    Console.WriteLine("  Shutting down...");
                    // Requesting application stop through the host lifetime
                    // would be cleaner but this works for the CLI case
                    Environment.Exit(0);
                    break;

                default:
                    Console.WriteLine($"  Unknown command: {command}. Type /help for commands.");
                    break;
            }
        }
        else
        {
            // Bare text = task submission
            await SubmitTaskAsync(input, ct);
        }
    }

    private async Task SubmitTaskAsync(string description, CancellationToken ct)
    {
        var taskId = $"user-{DateTimeOffset.UtcNow:yyyyMMdd}-{Interlocked.Increment(ref _taskCounter):D4}";

        var task = new LifeTask
        {
            Id = taskId,
            Title = description,
            Origin = TaskOrigin.User,
            Priority = TaskPriority.Medium, // Classifier will override
            RequiredTrust = TrustLevel.AskAndAct, // Classifier will override
        };

        await _eventWriter.WriteAsync(new TaskCreated(task), ct);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  ✓ Task submitted: {taskId}");
        Console.ResetColor();
    }

    private void ShowStatus()
    {
        var active = _state.GetActiveTasks();
        Console.WriteLine();
        Console.WriteLine($"  Active tasks: {active.Count}");
        Console.WriteLine($"  Budget: ${_state.Budget.SpentUsd:F2} / ${_state.Budget.LimitUsd:F2}");
        Console.WriteLine($"  Event sequence: #{_state.LastAppliedSequence}");

        if (active.Count > 0)
        {
            Console.WriteLine();
            foreach (var task in active.Take(10))
            {
                var deadline = task.Deadline.HasValue ? $" (due: {task.Deadline.Value:g})" : "";
                var worker = task.AssignedWorker is not null ? $" → {task.AssignedWorker}" : "";
                Console.WriteLine($"    [{task.Status}] {task.Title}{worker}{deadline}");
            }
            if (active.Count > 10)
                Console.WriteLine($"    ... and {active.Count - 10} more");
        }

        var approvals = _state.GetPendingApprovals();
        if (approvals.Count > 0)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  Pending approvals: {approvals.Count}");
            Console.ResetColor();
        }

        Console.WriteLine();
    }

    private void ShowBudget()
    {
        var b = _state.Budget;
        Console.WriteLine();
        Console.WriteLine($"  Date:      {b.Date:yyyy-MM-dd}");
        Console.WriteLine($"  Spent:     ${b.SpentUsd:F4}");
        Console.WriteLine($"  Limit:     ${b.LimitUsd:F2}");
        Console.WriteLine($"  Remaining: ${b.RemainingUsd:F4}");
        Console.WriteLine($"  Tasks:     {b.TasksExecuted} executed, {b.TasksFailed} failed");
        Console.WriteLine($"  Exhausted: {(b.IsExhausted ? "YES" : "No")}");
        Console.WriteLine();
    }

    private static void ShowWorkers()
    {
        Console.WriteLine();
        Console.WriteLine("  Registered worker agents:");
        Console.WriteLine("    general        — LLM-powered catch-all for unclassified tasks");
        Console.WriteLine("    reminder       — Time-based reminders and deadline tracking");
        Console.WriteLine("    daily-briefing — Personalized morning briefings from agent state");
        Console.WriteLine("    audio-lifelog  — Conversation recall, daily digest, speaker enrollment");
        Console.WriteLine();
        Console.WriteLine("  Planned (not yet implemented):");
        Console.WriteLine("    research       — Web research and information gathering");
        Console.WriteLine("    scheduling     — Calendar management");
        Console.WriteLine("    monitoring     — Price tracking, status checks");
        Console.WriteLine();
    }

    private static void ShowHelp()
    {
        Console.WriteLine();
        Console.WriteLine("  Commands:");
        Console.WriteLine("    <text>         Submit a task (natural language)");
        Console.WriteLine("    /task <text>   Submit a task explicitly");
        Console.WriteLine("    /status        Show active tasks and system state");
        Console.WriteLine("    /budget        Show LLM cost budget details");
        Console.WriteLine("    /workers       List available worker agents");
        Console.WriteLine("    /help          Show this help");
        Console.WriteLine("    /quit          Shut down the agent");
        Console.WriteLine();
    }
}
