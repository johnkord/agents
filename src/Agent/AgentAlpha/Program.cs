using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OpenAIIntegration;
using OpenAIIntegration.Model;
using System.Text.Json;
using MCPClient;
using System.Linq;               // NEW

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
        var config        = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        var logger        = loggerFactory.CreateLogger<Program>();

        var openAiApiKey = config["OPENAI_API_KEY"];
        if (string.IsNullOrEmpty(openAiApiKey))
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
            var agent = new SimpleAgentAlpha(openAiApiKey, loggerFactory);
            await agent.ExecuteTaskAsync(task);
        }
        catch (Exception ex)
        {
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

/* ======================================================================= */
internal sealed class SimpleAgentAlpha
{
    private readonly ILogger<SimpleAgentAlpha> _logger;
    // private readonly IOpenAIChatService        _openAi;
    private readonly IOpenAIResponsesService _openAi;
    private readonly ILoggerFactory _lf;

    public SimpleAgentAlpha(string apiKey, ILoggerFactory lf)
    {
        _lf     = lf;
        _logger = lf.CreateLogger<SimpleAgentAlpha>();
        // _openAi = new OpenAIChatService(apiKey);
        _openAi = new OpenAIResponsesService(apiKey);
    }

    public async Task ExecuteTaskAsync(string task)
    {
        /* --- connect to MCP ------------------------------------------------ */
        var mcp = new McpClientService(_lf);
        await using var _ = mcp;

        var transport = Program.GetMcpTransportType();
        if (transport == McpTransportType.Http)
        {
            var url = Environment.GetEnvironmentVariable("MCP_SERVER_URL") ?? "http://localhost:3000";
            await mcp.ConnectAsync(McpTransportType.Http, "Agent MCP Server", serverUrl: url);
        }
        else
        {
            await mcp.ConnectAsync(
                McpTransportType.Stdio,
                "Agent MCP Server",
                "dotnet",
                ["run", "--project", "../../MCPServer/MCPServer.csproj"]);
        }

        /* --- prepare OpenAI tool schema ----------------------------------- */
        var tools = await mcp.ListToolsAsync();
        var openAiTools = tools.Select(t => new ToolDefinition
        {
            Type        = "function",
            Name        = t.Name,
            Description = t.Description,
            Parameters  = CreateParametersForTool(t.Name)
        }).ToArray();

        /* --- chat loop ----------------------------------------------------- */
        var msgs = new List<object>
        {
            new { role = "system", content = """
                You are AgentAlpha, a helpful AI assistant that can perform various tasks using available tools.
                
                Available capabilities include:
                - Mathematical calculations (add, subtract, multiply, divide)
                - File operations (read, write, list directories, file information)
                - Text processing (search, replace, format, word count, split text)
                - System information (current time, environment variables, system details)
                
                When given a task:
                1. Break it down into steps if needed
                2. Use appropriate tools to accomplish each step
                3. Provide clear feedback on what you're doing
                4. Explain the results and next steps
                
                Always use tools when possible rather than trying to do calculations or file operations yourself.
                If you're unsure about a tool's parameters, start with simpler operations and build up.
                """ },
            new { role = "user",   content = task }
        };

        const int MaxIterations = 10;
        for (int i = 0; i < MaxIterations; i++)
        {
            Console.WriteLine($"\n--- Iteration {i + 1} ---");

            var req = new ResponsesCreateRequest
            {
                Model      = "gpt-3.5-turbo",
                Input      = msgs.ToArray(),
                Tools      = openAiTools,
                ToolChoice = "auto"
            };

            var res = await _openAi.CreateResponseAsync(req);

            /* -------- extract normal assistant text ------------------------ */
            string assistantText = res.Output?
                .OfType<OutputMessage>()
                .FirstOrDefault(m => string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                ?.Content?.ToString() ?? "";

            /* -------- handle tool-call items ------------------------------- */
            var followUpSummaries = new List<string>();

            foreach (var item in res.Output ?? Array.Empty<ResponseOutputItem>())
            {
                switch (item)
                {
                    case McpToolCall call:
                        {
                            var args = string.IsNullOrWhiteSpace(call.Arguments)
                                ? new Dictionary<string, object?>()
                                : JsonSerializer.Deserialize<Dictionary<string, object?>>(call.Arguments!)!;
                            var mcpRes = await mcp.CallToolAsync(call.Name!, args);
                            var txt = mcpRes.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text
                                      ?? "<no text>";
                            followUpSummaries.Add($"Tool '{call.Name}' called with args {call.Arguments}. Result: {txt}");
                            break;
                        }
                    case McpListTools list:
                        {
                            var toolsList = await mcp.ListToolsAsync();
                            followUpSummaries.Add($"Listed tools on server '{list.ServerLabel}': {string.Join(", ", toolsList.Select(t => t.Name))}");
                            break;
                        }
                    case McpApprovalRequest appr:
                        {
                            // simplistic automatic approval for now
                            followUpSummaries.Add($"Automatically approved request '{appr.Name}' on server '{appr.ServerLabel}'.");
                            break;
                        }
                    default: break; // ignore other item types
                }
            }

            /* -------- build next round messages ---------------------------- */
            if (followUpSummaries.Count > 0)
            {
                var summary = string.Join("\n", followUpSummaries);
                msgs.Add(new { role = "assistant", content = assistantText }); // preserve any assistant text
                msgs.Add(new { role = "assistant", content = summary });
                msgs.Add(new { role = "user",      content = $"I executed the requested tools.\n{summary}\n\nIs the task complete?" });
                Console.WriteLine($"🔧 {summary}");
                continue; // go to next iteration
            }

            Console.WriteLine($"AI: {assistantText}");
            if (assistantText.Contains("TASK COMPLETED", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("✅ Task completed!");
                return;
            }

            msgs.Add(new { role = "assistant", content = assistantText });
        }

        Console.WriteLine($"⚠️  Reached maximum iterations ({MaxIterations}).");
    }

    /// <summary>
    /// Create parameter schema for a tool based on its name and known patterns
    /// </summary>
    private static object CreateParametersForTool(string toolName)
    {
        return toolName switch
        {
            // Math tools - require two numbers
            "add" or "subtract" or "multiply" or "divide" => new
            {
                type = "object",
                properties = new
                {
                    a = new { type = "number", description = "First number" },
                    b = new { type = "number", description = "Second number" }
                },
                required = new[] { "a", "b" }
            },

            // File operations
            "read_file" or "file_exists" or "delete_file" or "get_file_info" => new
            {
                type = "object",
                properties = new
                {
                    filePath = new { type = "string", description = "Path to the file" }
                },
                required = new[] { "filePath" }
            },

            "write_file" => new
            {
                type = "object",
                properties = new
                {
                    filePath = new { type = "string", description = "Path to the file" },
                    content = new { type = "string", description = "Content to write to the file" }
                },
                required = new[] { "filePath", "content" }
            },

            "list_directory" or "create_directory" => new
            {
                type = "object",
                properties = new
                {
                    directoryPath = new { type = "string", description = "Path to the directory" }
                },
                required = new[] { "directoryPath" }
            },

            // Text tools
            "search_text" => new
            {
                type = "object",
                properties = new
                {
                    text = new { type = "string", description = "Text to search in" },
                    pattern = new { type = "string", description = "Pattern to search for" },
                    caseSensitive = new { type = "boolean", description = "Whether search should be case sensitive", @default = false }
                },
                required = new[] { "text", "pattern" }
            },

            "replace_text" => new
            {
                type = "object",
                properties = new
                {
                    text = new { type = "string", description = "Text to perform replacement in" },
                    searchPattern = new { type = "string", description = "Pattern to search for" },
                    replacement = new { type = "string", description = "Replacement text" },
                    caseSensitive = new { type = "boolean", description = "Whether search should be case sensitive", @default = false }
                },
                required = new[] { "text", "searchPattern", "replacement" }
            },

            "extract_lines" => new
            {
                type = "object",
                properties = new
                {
                    text = new { type = "string", description = "Text to extract lines from" },
                    lineNumbers = new { type = "string", description = "Line numbers to extract (e.g., '1,3,5-8')" }
                },
                required = new[] { "text", "lineNumbers" }
            },

            "word_count" => new
            {
                type = "object",
                properties = new
                {
                    text = new { type = "string", description = "Text to count words in" }
                },
                required = new[] { "text" }
            },

            "format_text" => new
            {
                type = "object",
                properties = new
                {
                    text = new { type = "string", description = "Text to format" },
                    format = new { type = "string", description = "Format to apply (uppercase, lowercase, titlecase, trim, remove_extra_spaces)" }
                },
                required = new[] { "text", "format" }
            },

            "split_text" => new
            {
                type = "object",
                properties = new
                {
                    text = new { type = "string", description = "Text to split" },
                    delimiter = new { type = "string", description = "Delimiter to split by" },
                    maxParts = new { type = "integer", description = "Maximum number of parts (0 for unlimited)", @default = 0 }
                },
                required = new[] { "text", "delimiter" }
            },

            // System tools
            "get_environment_variable" => new
            {
                type = "object",
                properties = new
                {
                    variableName = new { type = "string", description = "Name of the environment variable" }
                },
                required = new[] { "variableName" }
            },

            "list_environment_variables" => new
            {
                type = "object",
                properties = new
                {
                    pattern = new { type = "string", description = "Pattern to filter environment variables (optional)", @default = "" }
                },
                required = new string[] { }
            },

            // Tools with no parameters
            "get_current_time" or "get_system_info" or "get_current_directory" or "generate_uuid" => new
            {
                type = "object",
                properties = new { },
                required = new string[] { }
            },

            // Default fallback for unknown tools
            _ => new
            {
                type = "object",
                properties = new { },
                required = new string[] { }
            }
        };
    }
}