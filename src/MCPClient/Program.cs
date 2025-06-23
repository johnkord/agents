using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Text.Json;

namespace MCPClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("MCP Client Starting...");
            
            // Create a simple console logger
            using var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => 
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });
            var logger = loggerFactory.CreateLogger<Program>();
            
            var mcpService = new McpClientService(loggerFactory);
            
            try
            {
                // Connect to the MCP Server via stdio
                await mcpService.ConnectAsync(
                    "Math MCP Server",
                    "dotnet",
                    ["run", "--project", "../../MCPServer/MCPServer.csproj"]
                );
                
                logger.LogInformation("Connected to MCP Server");

                // List available tools
                var tools = await mcpService.ListToolsAsync();
                Console.WriteLine("\nAvailable tools:");
                foreach (var tool in tools)
                {
                    Console.WriteLine($"- {tool.Name}: {tool.Description}");
                }
                Console.WriteLine();

                // Interactive command loop
                ShowHelp();
                while (true)
                {
                    Console.Write("mcp> ");
                    var input = await Console.In.ReadLineAsync();
                    
                    if (string.IsNullOrWhiteSpace(input))
                        continue;
                    
                    var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var command = parts[0].ToLower();

                    switch (command)
                    {
                        case "help":
                            ShowHelp();
                            break;
                        case "list":
                            Console.WriteLine("Available tools:");
                            foreach (var tool in tools)
                            {
                                Console.WriteLine($"- {tool.Name}: {tool.Description}");
                            }
                            break;
                        case "add":
                        case "subtract":
                        case "multiply":
                        case "divide":
                            await ExecuteMathOperation(mcpService, command, parts);
                            break;
                        case "quit":
                        case "exit":
                            Console.WriteLine("Goodbye!");
                            return;
                        default:
                            Console.WriteLine($"Unknown command: {command}. Type 'help' for available commands.");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred in MCP Client");
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                await mcpService.DisposeAsync();
            }
        }

        static void ShowHelp()
        {
            Console.WriteLine("Available commands:");
            Console.WriteLine("- help: Show this help message");
            Console.WriteLine("- list: List available tools");
            Console.WriteLine("- add <num1> <num2>: Add two numbers");
            Console.WriteLine("- subtract <num1> <num2>: Subtract second number from first");
            Console.WriteLine("- multiply <num1> <num2>: Multiply two numbers");
            Console.WriteLine("- divide <num1> <num2>: Divide first number by second");
            Console.WriteLine("- quit/exit: Exit the client");
            Console.WriteLine();
        }

        static async Task ExecuteMathOperation(McpClientService mcpService, string operation, string[] parts)
        {
            if (parts.Length != 3)
            {
                Console.WriteLine($"Usage: {operation} <number1> <number2>");
                return;
            }

            if (!double.TryParse(parts[1], out double a) || !double.TryParse(parts[2], out double b))
            {
                Console.WriteLine("Error: Both arguments must be valid numbers");
                return;
            }

            try
            {
                var result = await mcpService.CallToolAsync(
                    operation,
                    new Dictionary<string, object?>
                    {
                        ["a"] = a,
                        ["b"] = b
                    }
                );

                if (result.IsError)
                {
                    Console.WriteLine($"Error calling tool: {result.Content.FirstOrDefault()?.ToString()}");
                }
                else
                {
                    var textContent = result.Content.OfType<TextContentBlock>().FirstOrDefault();
                    if (textContent != null)
                    {
                        Console.WriteLine(textContent.Text);
                    }
                    else
                    {
                        Console.WriteLine("No result returned from tool");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing {operation}: {ex.Message}");
            }
        }
    }
}
