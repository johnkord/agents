using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using MCPServer.Tools;

namespace MCPServer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            
            builder.Logging.AddConsole(consoleLogOptions =>
            {
                // Configure all logs to go to stderr
                consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
            });

            builder.Services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithTools<MathTools>();

            var host = builder.Build();
            
            Console.WriteLine("MCP Server Starting...");
            Console.WriteLine("Available tools: add, subtract, multiply, divide");
            
            await host.RunAsync();
        }
    }
}
