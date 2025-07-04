using AgentAlpha.Interfaces;
using AgentAlpha.Models;
using Microsoft.Extensions.Logging;

namespace AgentAlpha.Services;

public class TaskRouter : ITaskRouter
{
    private readonly ILogger<TaskRouter> _log;

    public TaskRouter(ILogger<TaskRouter> log) => _log = log;

    public Task<(TaskRoute route, double confidence)> RouteAsync(TaskExecutionRequest req,
                                                                 CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Task))
            return Task.FromResult((TaskRoute.FastPath, 1.0));

        var txt = req.Task.Trim().ToLowerInvariant();

        // trivial greetings/help
        if (txt is "hi" or "hello" or "help")
            return Task.FromResult((TaskRoute.FastPath, 0.9));

        // questions that map obviously to builtin tools
        var toolish = new[] { "time", "date", "uuid", "current directory" }
                      .Any(k => txt.Contains(k));
        if (toolish)
            return Task.FromResult((TaskRoute.FastPath, 0.8));

        // longer than 20 words → assume complex
        var wordCount = txt.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (wordCount > 20)
            return Task.FromResult((TaskRoute.ReactLoop, 0.8));

        // default unknown → React
        return Task.FromResult((TaskRoute.ReactLoop, 0.5));
    }
}
