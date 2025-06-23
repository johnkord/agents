using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OpenAIIntegration;
using OpenAIIntegration.Model;
using System.Text.Json;
using MCPClient;
using System.Linq;               // NEW
using AgentAlpha.Configuration;   // NEW

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
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var logger = loggerFactory.CreateLogger<Program>();

        var agentConfig = AgentConfiguration.FromEnvironment();
        
        if (string.IsNullOrEmpty(agentConfig.OpenAiApiKey))
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
            // Create services using dependency injection pattern
            var connectionManager = new Services.ConnectionManager(loggerFactory);
            var toolManager = new Services.ToolManager(loggerFactory.CreateLogger<Services.ToolManager>());
            var openAiService = new OpenAIResponsesService(agentConfig.OpenAiApiKey);
            var conversationManager = new Services.ConversationManager(
                openAiService, 
                loggerFactory.CreateLogger<Services.ConversationManager>(), 
                agentConfig);
            var taskExecutor = new Services.TaskExecutor(
                connectionManager,
                toolManager,
                conversationManager,
                agentConfig,
                loggerFactory.CreateLogger<Services.TaskExecutor>());

            await taskExecutor.ExecuteAsync(task);
        }
        catch (HttpRequestException httpEx) when (httpEx.Message.Contains("api.openai.com"))
        {
            Console.WriteLine("❌ Failed to connect to OpenAI API. Please check:");
            Console.WriteLine("   - Your internet connection");
            Console.WriteLine("   - Your OPENAI_API_KEY is valid");
            Console.WriteLine("   - You have sufficient API credits");
            logger.LogError(httpEx, "OpenAI API connection failed");
        }
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine("❌ OpenAI API authentication failed. Please check your OPENAI_API_KEY.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Agent failed with error: {ex.Message}");
            Console.WriteLine("💡 Try running 'dotnet run test' to verify MCP server connectivity");
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
                    ["run", "--project", "../../MCPServer/MCPServer.csproj"]);
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
