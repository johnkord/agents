using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Text.Json;
using System.Text;
using System.IO;

namespace MCPMathTools.AI.Tests;

public class Program
{
    private static readonly HttpClient httpClient = new();
    
    public static async Task Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var openAiApiKey = configuration["OPENAI_API_KEY"];
        if (string.IsNullOrEmpty(openAiApiKey))
        {
            Console.WriteLine("OPENAI_API_KEY environment variable is not set. Skipping AI validation.");
            return;
        }

        using var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        Console.WriteLine("Starting AI validation of MCP Math Tools...");

        try
        {
            // Connect to MCP Server
            // Try to find the MCP Server project file using common locations
            var possiblePaths = new[]
            {
                "../../src/MCPServer/MCPServer.csproj", // From tests/MCPMathTools.AI.Tests
                "../../../src/MCPServer/MCPServer.csproj", // From bin/Release/net9.0 when running built exe
                "src/MCPServer/MCPServer.csproj" // From repository root
            };
            
            string mcpServerPath = "";
            foreach (var path in possiblePaths)
            {
                var fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                {
                    mcpServerPath = fullPath;
                    break;
                }
            }
            
            if (string.IsNullOrEmpty(mcpServerPath))
            {
                throw new FileNotFoundException("Could not find MCPServer.csproj in any of the expected locations");
            }
            
            Console.WriteLine($"Using MCP Server path: {mcpServerPath}");
            
            var clientTransport = new StdioClientTransport(new()
            {
                Name = "AI Test Math MCP Server",
                Command = "dotnet",
                Arguments = ["run", "--project", mcpServerPath]
            });

            await using var mcpClient = await McpClientFactory.CreateAsync(clientTransport, loggerFactory: loggerFactory);
            
            Console.WriteLine("Connected to MCP Server. Retrieving available tools...");
            
            var tools = await mcpClient.ListToolsAsync();
            Console.WriteLine($"Found {tools.Count} tools: {string.Join(", ", tools.Select(t => t.Name))}");

            // Prepare OpenAI API request with function calling
            var openAiTools = tools.Select(tool => new
            {
                type = "function",
                function = new
                {
                    name = tool.Name,
                    description = tool.Description,
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            a = new { type = "number", description = "First number" },
                            b = new { type = "number", description = "Second number" }
                        },
                        required = new[] { "a", "b" }
                    }
                }
            }).ToArray();

            var testScenarios = new[]
            {
                "Calculate 15 + 27",
                "What is 100 minus 43?",
                "Multiply 8 by 9",
                "Divide 144 by 12",
                "Can you add 5.5 and 3.3?",
                "What happens if I try to divide 10 by 0?"
            };

            Console.WriteLine("Running AI validation scenarios...");

            foreach (var scenario in testScenarios)
            {
                Console.WriteLine($"\nTesting scenario: {scenario}");
                
                var result = await CallOpenAI(openAiApiKey, scenario, openAiTools);
                
                if (result.toolCalls?.Any() == true)
                {
                    foreach (var toolCall in result.toolCalls)
                    {
                        Console.WriteLine($"AI chose tool: {toolCall.function.name}");
                        Console.WriteLine($"Parameters: {toolCall.function.arguments}");

                        // Parse arguments and call MCP tool
                        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(toolCall.function.arguments);
                        var parameters = new Dictionary<string, object?>
                        {
                            ["a"] = arguments!["a"].GetDouble(),
                            ["b"] = arguments["b"].GetDouble()
                        };

                        var mcpResult = await mcpClient.CallToolAsync(toolCall.function.name, parameters);
                        
                        if (!mcpResult.IsError)
                        {
                            var textContent = mcpResult.Content.OfType<TextContentBlock>().FirstOrDefault();
                            Console.WriteLine($"MCP Result: {textContent?.Text}");
                        }
                        else
                        {
                            Console.WriteLine($"MCP Error: {mcpResult.Content.FirstOrDefault()}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"AI Response: {result.content}");
                }
            }

            Console.WriteLine("\nAI validation completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during AI validation: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static async Task<(string content, ToolCall[]? toolCalls)> CallOpenAI(string apiKey, string prompt, object[] tools)
    {
        var requestData = new
        {
            model = "gpt-3.5-turbo",
            messages = new[]
            {
                new { role = "system", content = "You are a helpful assistant that can perform mathematical calculations using the provided tools. Always use the appropriate tool for mathematical operations." },
                new { role = "user", content = prompt }
            },
            tools,
            tool_choice = "auto"
        };

        var reqJson = JsonSerializer.Serialize(requestData);
        var content = new StringContent(reqJson, Encoding.UTF8, "application/json");

        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        var response = await httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
        var respJson = await response.Content.ReadAsStringAsync();

        LogOpenAiInteraction(reqJson, respJson);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"OpenAI API error: {response.StatusCode} - {respJson}");
        }

        var result = JsonSerializer.Deserialize<JsonElement>(respJson);
        var message = result.GetProperty("choices")[0].GetProperty("message");

        var responseContent = message.TryGetProperty("content", out var contentProp) ? contentProp.GetString() ?? "" : "";
        
        ToolCall[]? toolCalls = null;
        if (message.TryGetProperty("tool_calls", out var toolCallsProp))
        {
            toolCalls = JsonSerializer.Deserialize<ToolCall[]>(toolCallsProp.GetRawText());
        }

        return (responseContent, toolCalls);
    }

    /* ---------- helper -------------------------------------------------- */
    private static void LogOpenAiInteraction(string requestJson, string responseJson)
    {
        try
        {
            Directory.CreateDirectory("logs");
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff");
            var path  = Path.Combine("logs", $"openai_{stamp}.json");

            var wrapper = new
            {
                timestamp = stamp,
                request   = JsonSerializer.Deserialize<JsonElement>(requestJson),
                response  = JsonSerializer.Deserialize<JsonElement>(responseJson)
            };

            File.WriteAllText(path, JsonSerializer.Serialize(wrapper));
        }
        catch { /* ignore logging failures */ }
    }

    public class ToolCall
    {
        public string id { get; set; } = "";
        public string type { get; set; } = "";
        public FunctionCall function { get; set; } = new();
    }

    public class FunctionCall
    {
        public string name { get; set; } = "";
        public string arguments { get; set; } = "";
    }
}