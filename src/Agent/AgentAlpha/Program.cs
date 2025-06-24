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
using AgentAlpha.Models;         // NEW

namespace AgentAlpha;

internal class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("AI Agent Starting...");

        /* --- acquire task -------------------------------------------------- */
        var request = ParseTaskExecutionRequest(args);
        if (string.IsNullOrWhiteSpace(request.Task)) return;

        /* --- configuration / logging --------------------------------------- */
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var logger = loggerFactory.CreateLogger<Program>();

        var agentConfig = AgentConfiguration.FromEnvironment();
        
        if (string.IsNullOrEmpty(agentConfig.OpenAiApiKey))
        {
            if (request.Task.Equals("test", StringComparison.OrdinalIgnoreCase))
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
            var sessionManager = new Services.SessionManager(loggerFactory.CreateLogger<Services.SessionManager>());
            var openAiService = new OpenAIResponsesService(agentConfig.OpenAiApiKey);
            var toolSelector = new Services.ToolSelector(
                openAiService,
                toolManager,
                loggerFactory.CreateLogger<Services.ToolSelector>(),
                agentConfig.ToolSelection);
            var conversationManager = new Services.ConversationManager(
                openAiService, 
                loggerFactory.CreateLogger<Services.ConversationManager>(), 
                agentConfig);
            var taskExecutor = new Services.TaskExecutor(
                connectionManager,
                toolManager,
                toolSelector,
                conversationManager,
                sessionManager,
                agentConfig,
                loggerFactory.CreateLogger<Services.TaskExecutor>());

            await taskExecutor.ExecuteAsync(request);
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

    /* --------------------------------------------------------------------- */
    private static TaskExecutionRequest ParseTaskExecutionRequest(string[] args)
    {
        if (args.Length == 0)
        {
            // Interactive mode
            var task = PromptForTask();
            return TaskExecutionRequest.FromTask(task);
        }

        var request = new TaskExecutionRequest();
        var taskParts = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            
            switch (arg.ToLowerInvariant())
            {
                case "--model" or "-m":
                    if (i + 1 < args.Length)
                    {
                        request.Model = args[++i];
                    }
                    break;
                    
                case "--temperature" or "-t":
                    if (i + 1 < args.Length && double.TryParse(args[++i], out var temp))
                    {
                        request.Temperature = Math.Clamp(temp, 0.0, 1.0);
                    }
                    break;
                    
                case "--max-iterations" or "--iterations":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var iterations))
                    {
                        request.MaxIterations = Math.Max(1, iterations);
                    }
                    break;
                    
                case "--priority":
                    if (i + 1 < args.Length && Enum.TryParse<TaskPriority>(args[++i], true, out var priority))
                    {
                        request.Priority = priority;
                    }
                    break;
                    
                case "--timeout":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var timeoutMinutes))
                    {
                        request.Timeout = TimeSpan.FromMinutes(timeoutMinutes);
                    }
                    break;
                    
                case "--verbose" or "-v":
                    request.VerboseLogging = true;
                    break;
                    
                case "--system-prompt":
                    if (i + 1 < args.Length)
                    {
                        request.SystemPrompt = args[++i];
                    }
                    break;
                    
                case "--session" or "--session-id":
                    if (i + 1 < args.Length)
                    {
                        request.SessionId = args[++i];
                    }
                    break;
                    
                case "--session-name":
                    if (i + 1 < args.Length)
                    {
                        request.SessionName = args[++i];
                    }
                    break;
                    
                default:
                    // Part of the task description
                    taskParts.Add(arg);
                    break;
            }
        }

        request.Task = string.Join(" ", taskParts);
        
        if (request.VerboseLogging && !string.IsNullOrEmpty(request.Task))
        {
            Console.WriteLine($"🔍 Parsed request - Task: '{request.Task}', Model: {request.Model ?? "default"}, Temperature: {request.Temperature?.ToString() ?? "default"}");
        }

        return request;
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
