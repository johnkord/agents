using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using MCPServer.Tools;

namespace MCPServer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Check transport mode from command line args or environment variable
            var transportMode = GetTransportMode(args);
            
            if (transportMode == "sse")
            {
                await RunSseServerAsync(args);
            }
            else
            {
                await RunStdioServerAsync(args);
            }
        }
        
        static string GetTransportMode(string[] args)
        {
            // Check command line arguments first
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--transport" && i + 1 < args.Length)
                {
                    return args[i + 1].ToLowerInvariant();
                }
            }
            
            // Check environment variable
            var envTransport = Environment.GetEnvironmentVariable("MCP_TRANSPORT");
            if (!string.IsNullOrEmpty(envTransport))
            {
                return envTransport.ToLowerInvariant();
            }
            
            // Default to stdio
            return "stdio";
        }
        
        static async Task RunStdioServerAsync(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            
            builder.Logging.AddConsole(consoleLogOptions =>
            {
                // Configure all logs to go to stderr
                consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
            });

            builder.Logging.SetMinimumLevel(LogLevel.Debug);

            builder.Services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithTools<ShellTools>()
                .WithTools<TaskCompletionTool>()
                .WithTools<GitHubTools>()
                .WithTools<AzureDevOpsTools>()
                .WithTools<OpenAIVectorStoreTools>()
                .WithTools<CodeReviewTools>()
                .WithTools<AppInsightsTools>()
                .WithTools<CosmosDbTools>()
                .WithTools<ServiceBusTools>()
                .WithTools<FileTools>()
                .WithTools<TextTools>()
                .WithTools<SystemTools>()
                .WithTools<HttpTools>()
                .WithTools<EventHubTools>()
                .WithTools<AzureResourceGroupTools>()
                .WithTools<AzureFunctionTools>()
                .WithTools<AzureStorageTools>()
                ;

            var host = builder.Build();
            
            Console.WriteLine("MCP Server Starting (stdio mode)...");
            Console.WriteLine("Available tools: GitHub PR review, Azure DevOps PR review, OpenAI Vector Store, Code Review analysis, and more...");
            
            await host.RunAsync();
        }
        
        static async Task RunSseServerAsync(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            
            builder.Logging.AddConsole(consoleLogOptions =>
            {
                consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Information;
            });

            builder.Services
                .AddMcpServer()
                .WithHttpTransport()
                .WithTools<ShellTools>()
                .WithTools<TaskCompletionTool>()
                .WithTools<GitHubTools>()
                .WithTools<AzureDevOpsTools>()
                .WithTools<OpenAIVectorStoreTools>()
                .WithTools<CodeReviewTools>()
                .WithTools<AppInsightsTools>()
                .WithTools<CosmosDbTools>()
                .WithTools<ServiceBusTools>()
                .WithTools<FileTools>()
                .WithTools<TextTools>()
                .WithTools<SystemTools>()
                .WithTools<HttpTools>()
                .WithTools<EventHubTools>()
                .WithTools<AzureResourceGroupTools>()
                .WithTools<AzureFunctionTools>()
                .WithTools<AzureStorageTools>()
                ;

            var app = builder.Build();
            
            app.MapMcp();
            
            Console.WriteLine("MCP Server Starting (SSE mode)...");
            Console.WriteLine("Available tools: GitHub PR review, Azure DevOps PR review, OpenAI Vector Store, Code Review analysis, and more...");
            Console.WriteLine($"Server listening on: {builder.Configuration["ASPNETCORE_URLS"] ?? "http://localhost:5000"}");
            
            await app.RunAsync();
        }
    }
}
