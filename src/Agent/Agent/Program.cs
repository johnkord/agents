using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Text.Json;
using System.Text;

namespace Agent
{
    class Program
    {
        private static readonly HttpClient httpClient = new();
        private const int MaxIterations = 10; // Prevent infinite loops
        
        static async Task Main(string[] args)
        {
            Console.WriteLine("AI Agent Starting...");
            
            // Get task from command line args or prompt user
            string task;
            if (args.Length > 0)
            {
                task = string.Join(" ", args);
            }
            else
            {
                Console.Write("Enter a task for the agent: ");
                task = Console.ReadLine() ?? "";
                if (string.IsNullOrWhiteSpace(task))
                {
                    Console.WriteLine("No task provided. Exiting.");
                    return;
                }
            }
            
            // Get OpenAI API key
            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            using var loggerFactory = LoggerFactory.Create(builder => 
                builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
            var logger = loggerFactory.CreateLogger<Program>();
            
            var openAiApiKey = configuration["OPENAI_API_KEY"];
            if (string.IsNullOrEmpty(openAiApiKey))
            {
                // Check if this is a test mode
                if (task.Equals("test", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Running in test mode - testing MCP connection only...");
                    await TestMcpConnection(loggerFactory);
                    return;
                }
                
                Console.WriteLine("OPENAI_API_KEY environment variable is not set.");
                Console.WriteLine("Please set this environment variable to use the agent.");
                Console.WriteLine("Or run with 'test' as the task to test MCP connection only.");
                return;
            }

            using var loggerFactory2 = LoggerFactory.Create(builder => 
                builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
            
            try
            {
                var agent = new SimpleAgent(openAiApiKey, loggerFactory2);
                await agent.ExecuteTaskAsync(task);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred in Agent");
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
        
        static async Task TestMcpConnection(ILoggerFactory loggerFactory)
        {
            Console.WriteLine("Testing MCP Server connection...");
            
            try
            {
                // Connect to MCP Server
                var clientTransport = new StdioClientTransport(new()
                {
                    Name = "Test Agent MCP Server",
                    Command = "dotnet",
                    Arguments = ["run", "--project", "../../MCPServer/MCPServer/MCPServer.csproj"]
                });

                await using var mcpClient = await McpClientFactory.CreateAsync(clientTransport, loggerFactory: loggerFactory);
                Console.WriteLine("✅ Successfully connected to MCP Server");
                
                // Get available tools
                var tools = await mcpClient.ListToolsAsync();
                Console.WriteLine($"✅ Found {tools.Count} tools: {string.Join(", ", tools.Select(t => t.Name))}");
                
                // Test a simple tool call
                Console.WriteLine("Testing tool call: add(2, 3)");
                var result = await mcpClient.CallToolAsync("add", new Dictionary<string, object?> { ["a"] = 2.0, ["b"] = 3.0 });
                
                if (!result.IsError)
                {
                    var textContent = result.Content.OfType<TextContentBlock>().FirstOrDefault();
                    Console.WriteLine($"✅ Tool call successful: {textContent?.Text}");
                }
                else
                {
                    Console.WriteLine($"❌ Tool call failed: {result.Content.FirstOrDefault()}");
                }
                
                Console.WriteLine("✅ MCP connection test completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ MCP connection test failed: {ex.Message}");
            }
        }
    }

    public class SimpleAgent
    {
        private readonly string _openAiApiKey;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<SimpleAgent> _logger;
        private readonly HttpClient _httpClient = new();
        
        public SimpleAgent(string openAiApiKey, ILoggerFactory loggerFactory)
        {
            _openAiApiKey = openAiApiKey;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<SimpleAgent>();
        }
        
        public async Task ExecuteTaskAsync(string task)
        {
            Console.WriteLine($"Agent Task: {task}");
            Console.WriteLine("Connecting to MCP Server...");
            
            // Connect to MCP Server
            var clientTransport = new StdioClientTransport(new()
            {
                Name = "Agent MCP Server",
                Command = "dotnet",
                Arguments = ["run", "--project", "../../MCPServer/MCPServer/MCPServer.csproj"]
            });

            await using var mcpClient = await McpClientFactory.CreateAsync(clientTransport, loggerFactory: _loggerFactory);
            _logger.LogInformation("Connected to MCP Server");
            
            // Get available tools
            var tools = await mcpClient.ListToolsAsync();
            Console.WriteLine($"Available tools: {string.Join(", ", tools.Select(t => t.Name))}");
            
            // Prepare OpenAI tools format
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
            
            // Agent loop
            var messages = new List<object>
            {
                new { role = "system", content = "You are a helpful AI agent that can perform mathematical calculations using the provided tools. Work step by step to complete the given task. When the task is complete, clearly state 'TASK COMPLETED' in your response." },
                new { role = "user", content = task }
            };
            
            var iteration = 0;
            const int maxIterations = 10;
            
            while (iteration < maxIterations)
            {
                iteration++;
                Console.WriteLine($"\n--- Iteration {iteration} ---");
                
                // Call OpenAI
                var result = await CallOpenAI(messages.ToArray(), openAiTools);
                Console.WriteLine($"AI Response: {result.content}");
                
                // Check if task is complete
                if (result.content.Contains("TASK COMPLETED", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("\n✅ Task completed successfully!");
                    break;
                }
                
                // Add AI response to messages
                if (result.toolCalls?.Any() == true)
                {
                    messages.Add(new { role = "assistant", content = result.content, tool_calls = result.toolCalls });
                    
                    // Execute tool calls
                    foreach (var toolCall in result.toolCalls)
                    {
                        Console.WriteLine($"Executing tool: {toolCall.function.name}");
                        Console.WriteLine($"Parameters: {toolCall.function.arguments}");
                        
                        try
                        {
                            // Parse arguments and call MCP tool
                            var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(toolCall.function.arguments);
                            var parameters = new Dictionary<string, object?>
                            {
                                ["a"] = arguments!["a"].GetDouble(),
                                ["b"] = arguments["b"].GetDouble()
                            };

                            var mcpResult = await mcpClient.CallToolAsync(toolCall.function.name, parameters);
                            
                            string toolResult;
                            if (!mcpResult.IsError)
                            {
                                var textContent = mcpResult.Content.OfType<TextContentBlock>().FirstOrDefault();
                                toolResult = textContent?.Text ?? "No result returned";
                                Console.WriteLine($"Tool Result: {toolResult}");
                            }
                            else
                            {
                                toolResult = $"Error: {mcpResult.Content.FirstOrDefault()?.ToString()}";
                                Console.WriteLine($"Tool Error: {toolResult}");
                            }
                            
                            // Add tool result to messages
                            messages.Add(new { 
                                role = "tool", 
                                tool_call_id = toolCall.id, 
                                content = toolResult 
                            });
                        }
                        catch (Exception ex)
                        {
                            var errorMsg = $"Error executing tool {toolCall.function.name}: {ex.Message}";
                            Console.WriteLine(errorMsg);
                            messages.Add(new { 
                                role = "tool", 
                                tool_call_id = toolCall.id, 
                                content = errorMsg 
                            });
                        }
                    }
                }
                else
                {
                    messages.Add(new { role = "assistant", content = result.content });
                }
            }
            
            if (iteration >= maxIterations)
            {
                Console.WriteLine($"\n⚠️  Agent reached maximum iterations ({maxIterations}) without completing the task.");
            }
        }
        
        private async Task<(string content, ToolCall[]? toolCalls)> CallOpenAI(object[] messages, object[] tools)
        {
            var requestData = new
            {
                model = "gpt-3.5-turbo",
                messages,
                tools,
                tool_choice = "auto"
            };

            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_openAiApiKey}");

            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"OpenAI API error: {response.StatusCode} - {responseJson}");
            }

            var result = JsonSerializer.Deserialize<JsonElement>(responseJson);
            var message = result.GetProperty("choices")[0].GetProperty("message");

            var responseContent = message.TryGetProperty("content", out var contentProp) ? contentProp.GetString() ?? "" : "";
            
            ToolCall[]? toolCalls = null;
            if (message.TryGetProperty("tool_calls", out var toolCallsProp))
            {
                toolCalls = JsonSerializer.Deserialize<ToolCall[]>(toolCallsProp.GetRawText());
            }

            return (responseContent, toolCalls);
        }
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