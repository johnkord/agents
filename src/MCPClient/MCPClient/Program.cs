using Microsoft.Extensions.Logging;
using ModelContextProtocol;

namespace MCPClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Simple MCP Client Starting...");
            
            // Create a simple console logger
            using var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<Program>();
            
            logger.LogInformation("MCP Client is running...");
            logger.LogInformation("This client will connect to MCP servers and use their tools.");
            
            // For now, just demonstrate that the client can start
            Console.WriteLine("Available commands:");
            Console.WriteLine("- help: Show this help message");
            Console.WriteLine("- quit: Exit the client");
            Console.WriteLine();
            
            while (true)
            {
                Console.Write("mcp> ");
                var input = await Console.In.ReadLineAsync();
                
                if (string.IsNullOrWhiteSpace(input))
                    continue;
                
                switch (input.ToLower().Trim())
                {
                    case "help":
                        Console.WriteLine("Available commands:");
                        Console.WriteLine("- help: Show this help message");
                        Console.WriteLine("- quit: Exit the client");
                        break;
                    case "quit":
                    case "exit":
                        Console.WriteLine("Goodbye!");
                        return;
                    default:
                        Console.WriteLine($"Unknown command: {input}. Type 'help' for available commands.");
                        break;
                }
            }
        }
    }
}
