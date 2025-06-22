using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OpenAIIntegration;
using System.Text.Json;
using MCPClient;

namespace AgentAlpha;

internal class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("AI Agent Starting...");

        /* --- acquire task -------------------------------------------------- */
        string task = args.Length > 0
            ? string.Join(" ", args)
            : PromptForTask();
        if (string.IsNullOrWhiteSpace(task)) return;

        /* --- configuration / logging --------------------------------------- */
        var config        = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        var logger        = loggerFactory.CreateLogger<Program>();

        var openAiApiKey = config["OPENAI_API_KEY"];
        if (string.IsNullOrEmpty(openAiApiKey))
        {
            if (task.Equals("test", StringComparison.OrdinalIgnoreCase))
            {
                await TestMcpConnection(loggerFactory);
                return;
            }
            Console.WriteLine("OPENAI_API_KEY not set."); return;
        }

        /* --- run the agent -------------------------------------------------- */
        try
        {
            var agent = new SimpleAgentAlpha(openAiApiKey, loggerFactory);
            await agent.ExecuteTaskAsync(task);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Agent failed");
        }
    }

    /* --------------------------------------------------------------------- */
    private static string PromptForTask()
    {
        Console.Write("Enter a task for the agent: ");
        return Console.ReadLine() ?? "";
    }

    /* ---------------- MCP test helper ------------------------------------ */
    private static async Task TestMcpConnection(ILoggerFactory lf)
    {
        Console.WriteLine("Testing MCP Server connection...");
        try
        {
            var mcp = new McpClientService(lf);
            await using var _ = mcp;

            var transport = GetMcpTransportType();
            if (transport == McpTransportType.Http)
            {
                var url = Environment.GetEnvironmentVariable("MCP_SERVER_URL") ?? "http://localhost:3000";
                await mcp.ConnectAsync(McpTransportType.Http, "Test Agent MCP Server", serverUrl: url);
            }
            else
            {
                await mcp.ConnectAsync(
                    McpTransportType.Stdio,
                    "Test Agent MCP Server",
                    "dotnet",
                    ["run", "--project", "../../MCPServer/MCPServer/MCPServer.csproj"]);
            }

            Console.WriteLine("✅ Connected.");
            var tools = await mcp.ListToolsAsync();
            Console.WriteLine($"✅ Tools: {string.Join(", ", tools.Select(t => t.Name))}");

            var addRes = await mcp.CallToolAsync("add", new Dictionary<string, object?> { ["a"] = 2, ["b"] = 3 });
            var txt    = addRes.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;
            Console.WriteLine(addRes.IsError ? $"❌ add failed: {txt}" : $"✅ add(2,3) = {txt}");

            Console.WriteLine("✅ MCP connection test completed.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ MCP test failed: {ex.Message}");
        }
    }

    internal static McpTransportType GetMcpTransportType()
        => (Environment.GetEnvironmentVariable("MCP_TRANSPORT") ?? "stdio").ToLowerInvariant() switch
        {
            "http" or "sse" => McpTransportType.Http,
            _               => McpTransportType.Stdio
        };
}

/* ======================================================================= */
internal sealed class SimpleAgentAlpha
{
    private readonly ILogger<SimpleAgentAlpha> _logger;
    private readonly IOpenAIChatService        _openAi;
    private readonly ILoggerFactory            _lf;

    public SimpleAgentAlpha(string apiKey, ILoggerFactory lf)
    {
        _lf     = lf;
        _logger = lf.CreateLogger<SimpleAgentAlpha>();
        _openAi = new OpenAIChatService(apiKey);
    }

    public async Task ExecuteTaskAsync(string task)
    {
        /* --- connect to MCP ------------------------------------------------ */
        var mcp = new McpClientService(_lf);
        await using var _ = mcp;

        var transport = Program.GetMcpTransportType();
        if (transport == McpTransportType.Http)
        {
            var url = Environment.GetEnvironmentVariable("MCP_SERVER_URL") ?? "http://localhost:3000";
            await mcp.ConnectAsync(McpTransportType.Http, "Agent MCP Server", serverUrl: url);
        }
        else
        {
            await mcp.ConnectAsync(
                McpTransportType.Stdio,
                "Agent MCP Server",
                "dotnet",
                ["run", "--project", "../../MCPServer/MCPServer/MCPServer.csproj"]);
        }

        /* --- prepare OpenAI tool schema ----------------------------------- */
        var tools = await mcp.ListToolsAsync();
        var openAiTools = tools.Select(t => new
        {
            type     = "function",
            function = new
            {
                name        = t.Name,
                description = t.Description,
                parameters  = new
                {
                    type       = "object",
                    properties = new
                    {
                        a = new { type = "number", description = "First number" },
                        b = new { type = "number", description = "Second number" }
                    },
                    required = new[] { "a", "b" }
                }
            }
        }).ToArray();

        /* --- chat loop ----------------------------------------------------- */
        var msgs = new List<object>
        {
            new { role = "system", content = "You are a helpful AI agent..." },
            new { role = "user",   content = task }
        };

        const int MaxIterations = 10;
        for (int i = 0; i < MaxIterations; i++)
        {
            Console.WriteLine($"\n--- Iteration {i + 1} ---");

            var (content, toolCalls) = await _openAi.CreateChatCompletionAsync(msgs.ToArray(), openAiTools);
            Console.WriteLine($"AI: {content}");

            if (content.Contains("TASK COMPLETED", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("✅ Task completed!");
                return;
            }

            if (toolCalls?.Any() == true)
            {
                msgs.Add(new { role = "assistant", content, tool_calls = toolCalls });
                foreach (var tc in toolCalls)
                {
                    var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(tc.function.arguments)!;
                    var res  = await mcp.CallToolAsync(tc.function.name, new Dictionary<string, object?>
                    {
                        ["a"] = args["a"].GetDouble(),
                        ["b"] = args["b"].GetDouble()
                    });

                    string toolReply = res.IsError
                        ? $"Error: {res.Content.FirstOrDefault()}"
                        : res.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "";

                    msgs.Add(new { role = "tool", tool_call_id = tc.id, content = toolReply });
                }
            }
            else
            {
                msgs.Add(new { role = "assistant", content });
            }
        }

        Console.WriteLine($"⚠️  Reached maximum iterations ({MaxIterations}).");
    }
}