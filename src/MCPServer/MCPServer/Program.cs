using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;

namespace MCPServer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Simple MCP Server Starting...");
            
            // Create a simple console logger
            using var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<Program>();
            
            logger.LogInformation("MCP Server is running...");
            logger.LogInformation("Available tools: echo, time, random");
            
            // Keep the server running
            Console.WriteLine("Press Ctrl+C to stop the server...");
            await Task.Delay(-1);
        }
    }
}
