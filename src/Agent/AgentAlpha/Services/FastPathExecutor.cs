using AgentAlpha.Interfaces;
using AgentAlpha.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using MCPClient; // ← add enum & client transport
using OpenAIIntegration;
using OpenAIIntegration.Model;

namespace AgentAlpha.Services;

public class FastPathExecutor : IFastPathExecutor
{
    private readonly SimpleToolManager _tools;
    private readonly IConnectionManager _conn;
    private readonly ISessionAwareOpenAIService _openAi;
    private readonly ILogger<FastPathExecutor> _log;

    public FastPathExecutor(SimpleToolManager tools,
                            IConnectionManager conn,
                            ISessionAwareOpenAIService openAi,
                            ILogger<FastPathExecutor> log)
    {
        _tools = tools; _conn = conn; _openAi = openAi; _log = log;
    }

    public async Task ExecuteAsync(TaskExecutionRequest req)
    {
        var task = req.Task.Trim();
        if (string.IsNullOrEmpty(task))
        {
            Console.WriteLine("No task provided.");
            return;
        }

        // Connect quickly if a tool might be needed
        await EnsureConnectedAsync();

        // Very simple heuristics – extend as needed
        if (task.Contains("time"))
        {
            var res = await _tools.ExecuteToolAsync(_conn, "get_current_time", new());
            Console.WriteLine(res);
            return;
        }

        // Fallback: one-shot LLM
        var resp = await _openAi.CreateResponseAsync(new ResponsesCreateRequest
        {
            Model  = req.Model ?? "gpt-3.5-turbo",
            Input  = new[] { new { role = "user", content = task } },
            MaxOutputTokens = 800
        });
        var answer = resp.Output?.OfType<OutputMessage>().FirstOrDefault()?.Content?.ToString();
        Console.WriteLine(answer);
    }

    private async Task EnsureConnectedAsync()
    {
        if (_conn.IsConnected) return;

        var url = Environment.GetEnvironmentVariable("MCP_SERVER_URL") ?? "http://localhost:3000";
        await _conn.ConnectAsync(McpTransportType.Http, "FastPath MCP", serverUrl: url);
    }
}
