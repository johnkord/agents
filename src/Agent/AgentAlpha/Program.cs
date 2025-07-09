using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
using AgentAlpha.Extensions;     // NEW
using AgentAlpha.Services;       // NEW
using AgentAlpha.Interfaces;     // NEW
using Common.Interfaces.Session;      // +ADD
using Common.Models.Session;          // +ADD

namespace AgentAlpha;

internal class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("AI Agent Starting...");

        // Create host with dependency injection
        var host = CreateHost();

        // Parse command-line arguments
        var commandLineParser = new CommandLineParser();
        var request = commandLineParser.ParseArguments(args);

        if (string.IsNullOrWhiteSpace(request.Task)) return;

        var agentConfig = host.Services.GetRequiredService<AgentConfiguration>();
        var logger = host.Services.GetRequiredService<ILogger<Program>>();

        if (string.IsNullOrEmpty(agentConfig.OpenAiApiKey))
        {
            if (request.Task.Equals("test", StringComparison.OrdinalIgnoreCase))
            {
                await TestMcpConnection(host.Services.GetRequiredService<ILoggerFactory>());
                return;
            }
            Console.WriteLine("OPENAI_API_KEY not set.");
            return;
        }

        try                                            // <<< restored try wrapper
        {
            /* -----------------------------------------------------------------
               Ensure a session is present for activity logging BEFORE routing
               ----------------------------------------------------------------- */
            var sessionManager  = host.Services.GetRequiredService<ISessionManager>();
            var activityLogger  = host.Services.GetRequiredService<ISessionActivityLogger>();

            if (activityLogger.GetCurrentSession() == null)
            {
                AgentSession? session = null;

                // Try by ID first
                if (!string.IsNullOrWhiteSpace(request.SessionId))
                {
                    try { session = await sessionManager.GetSessionAsync(request.SessionId); }
                    catch { /* ignore – will create below */ }
                }

                // Try by name
                if (session == null && !string.IsNullOrWhiteSpace(request.SessionName))
                {
                    try { session = await sessionManager.GetSessionByNameAsync(request.SessionName); }
                    catch { /* ignore – will create below */ }
                }

                // Create if still missing
                if (session == null)
                {
                    var name = string.IsNullOrWhiteSpace(request.SessionName)
                        ? $"fastpath-{DateTime.UtcNow:yyyyMMdd_HHmmss}"
                        : request.SessionName!;
                    session = await sessionManager.CreateSessionAsync(name);
                    request.SessionId = session.SessionId;   // propagate
                }

                activityLogger.SetCurrentSession(session);
            }
            /* ----------------------------------------------------------------- */

            // Router is always enabled
            var router = host.Services.GetRequiredService<ITaskRouter>();
            var (route, _) = await router.RouteAsync(request);

            if (route == TaskRoute.FastPath)
            {
                await host.Services.GetRequiredService<IFastPathExecutor>()
                                   .ExecuteAsync(request);
                return;
            }

            // Fallback to full ReAct executor
            await host.Services.GetRequiredService<ITaskExecutor>()
                                .ExecuteAsync(request);
        }
        catch (HttpRequestException httpEx) when (httpEx.Message.Contains("api.openai.com"))
        {
            Console.WriteLine("❌ Failed to connect to OpenAI API. Please check:");
            Console.WriteLine("   - Your internet connection");
            Console.WriteLine("   - Your OPENAI_API_KEY is valid");
            Console.WriteLine("   - You have sufficient API credits");
            logger.LogError(httpEx, "OpenAI API connection failed");
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine("❌ OpenAI API authentication failed. Please check your OPENAI_API_KEY.", ex);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Agent failed with error: {ex.Message}");
            Console.WriteLine("💡 Try running 'dotnet run test' to verify MCP server connectivity");
            logger.LogError(ex, "Agent failed");
        }
    }

    /// <summary>
    /// Creates and configures the host with dependency injection
    /// </summary>
    /// <returns>Configured host</returns>
    private static IHost CreateHost()
    {
        var agentConfig = AgentConfiguration.FromEnvironment();

        return Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Debug);
            })
            .ConfigureServices(services =>
            {
                services.AddAgentAlphaServices(agentConfig);
            })
            .Build();
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
        // This method is kept for backward compatibility but now delegates to CommandLineParser
        var parser = new CommandLineParser();
        return parser.ParseArguments(args);
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
            var txt = addRes.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;
            Console.WriteLine(addRes.IsError ? $"❌ add failed: {txt}" : $"✅ add(2,3) = {txt}");

            Console.WriteLine("✅ MCP connection test completed.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ MCP test failed: {ex.Message}");
        }
    }

    internal static McpTransportType GetMcpTransportType() => McpTransportType.Http;
}
