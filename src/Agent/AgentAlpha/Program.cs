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

/// <summary>
/// Configuration for filtering MCP server tools
/// </summary>
public sealed class ToolFilterConfig
{
    /// <summary>
    /// Tools to explicitly include (if specified, only these tools will be available)
    /// </summary>
    public HashSet<string> Whitelist { get; set; } = new();

    /// <summary>
    /// Tools to explicitly exclude (takes precedence over whitelist)
    /// </summary>
    public HashSet<string> Blacklist { get; set; } = new();

    /// <summary>
    /// Check if a tool should be included based on the filter configuration
    /// </summary>
    public bool ShouldIncludeTool(string toolName)
    {
        // Blacklist takes precedence
        if (Blacklist.Contains(toolName))
            return false;

        // If whitelist is specified, only include whitelisted tools
        if (Whitelist.Count > 0)
            return Whitelist.Contains(toolName);

        // If no whitelist specified, include all tools (except blacklisted)
        return true;
    }

    /// <summary>
    /// Create filter configuration from environment variables
    /// </summary>
    public static ToolFilterConfig FromEnvironment()
    {
        var config = new ToolFilterConfig();
        
        var whitelist = Environment.GetEnvironmentVariable("MCP_TOOL_WHITELIST");
        if (!string.IsNullOrEmpty(whitelist))
        {
            config.Whitelist = whitelist.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                       .Select(s => s.Trim())
                                       .ToHashSet();
        }

        var blacklist = Environment.GetEnvironmentVariable("MCP_TOOL_BLACKLIST");
        if (!string.IsNullOrEmpty(blacklist))
        {
            config.Blacklist = blacklist.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                       .Select(s => s.Trim())
                                       .ToHashSet();
        }

        return config;
    }
}

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
            var toolFilter = ToolFilterConfig.FromEnvironment();
            var agent = new SimpleAgentAlpha(openAiApiKey, loggerFactory, toolFilter);
            await agent.ExecuteTaskAsync(task);
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
    private readonly ToolFilterConfig _toolFilter;

    public SimpleAgentAlpha(string apiKey, ILoggerFactory lf, ToolFilterConfig? toolFilter = null)
    {
        _lf         = lf;
        _logger     = lf.CreateLogger<SimpleAgentAlpha>();
        // _openAi = new OpenAIChatService(apiKey);
        _openAi     = new OpenAIResponsesService(apiKey);
        _toolFilter = toolFilter ?? new ToolFilterConfig();
    }

    public async Task ExecuteTaskAsync(string task)
    {
        /* --- connect to MCP ------------------------------------------------ */
        var mcp = new McpClientService(_lf);
        await using var _ = mcp;

        try
        {
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
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ Failed to connect to MCP Server. Please ensure:");
            Console.WriteLine("   - The MCPServer project builds successfully");
            Console.WriteLine("   - No other MCP server instances are running");
            Console.WriteLine("   - Run from the correct directory (src/Agent/AgentAlpha)");
            Console.WriteLine($"   Error: {ex.Message}");
            return;
        }

        Console.WriteLine("✅ Connected to MCP Server");

        /* --- prepare OpenAI tool schema ----------------------------------- */
        var allTools = await mcp.ListToolsAsync();
        var filteredTools = allTools.Where(t => _toolFilter.ShouldIncludeTool(t.Name)).ToList();
        
        Console.WriteLine($"🔧 Discovered {allTools.Count} tools total, {filteredTools.Count} after filtering: {string.Join(", ", filteredTools.Take(5).Select(t => t.Name))}{(filteredTools.Count > 5 ? "..." : "")}");
        
        if (filteredTools.Count != allTools.Count)
        {
            var excluded = allTools.Where(t => !_toolFilter.ShouldIncludeTool(t.Name)).Select(t => t.Name);
            Console.WriteLine($"🚫 Excluded tools: {string.Join(", ", excluded)}");
        }
        
        var openAiTools = filteredTools.Select(t => new ToolDefinition
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
                    case FunctionToolCall funcCall:
                        {
                            // Handle FunctionToolCall by invoking MCP Client+Server
                            var name = funcCall.Name;
                            if (!string.IsNullOrEmpty(name))
                            {
                                if (!_toolFilter.ShouldIncludeTool(name))
                                {
                                    followUpSummaries.Add($"Function '{name}' call blocked by tool filter configuration.");
                                    break;
                                }

                                var args = funcCall.Arguments?.ValueKind == JsonValueKind.Object
                                    ? JsonSerializer.Deserialize<Dictionary<string, object?>>(funcCall.Arguments.Value.GetRawText())!
                                    : new Dictionary<string, object?>();
                                var mcpRes = await mcp.CallToolAsync(name, args);
                                var txt = mcpRes.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text
                                          ?? "<no text>";
                                followUpSummaries.Add($"Function '{name}' called via MCP. Result: {txt}");
                            }
                            break;
                        }
                    case McpToolCall call:
                        {
                            var name = call.Name;
                            if (!string.IsNullOrEmpty(name))
                            {
                                if (!_toolFilter.ShouldIncludeTool(name))
                                {
                                    followUpSummaries.Add($"Tool '{name}' call blocked by tool filter configuration.");
                                    break;
                                }

                                var args = string.IsNullOrWhiteSpace(call.Arguments)
                                    ? new Dictionary<string, object?>()
                                    : JsonSerializer.Deserialize<Dictionary<string, object?>>(call.Arguments!)!;
                                var mcpRes = await mcp.CallToolAsync(name, args);
                                var txt = mcpRes.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text
                                          ?? "<no text>";
                                followUpSummaries.Add($"Tool '{name}' called with args {call.Arguments}. Result: {txt}");
                            }
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
                    case FileSearchToolCall fileSearch:
                        {
                            followUpSummaries.Add($"File search requested with queries: {string.Join(", ", fileSearch.Queries ?? Array.Empty<string>())}. Status: {fileSearch.Status}");
                            break;
                        }
                    case WebSearchToolCall webSearch:
                        {
                            followUpSummaries.Add($"Web search requested with queries: {string.Join(", ", webSearch.Queries ?? Array.Empty<string>())}. Status: {webSearch.Status}");
                            break;
                        }
                    case ComputerToolCall computerCall:
                        {
                            followUpSummaries.Add($"Computer tool call executed. Status: {computerCall.Status}");
                            break;
                        }
                    case ReasoningItem reasoning:
                        {
                            followUpSummaries.Add($"Reasoning step completed. Status: {reasoning.Status}");
                            break;
                        }
                    case ImageGenerationCall imageGen:
                        {
                            followUpSummaries.Add($"Image generation requested. Status: {imageGen.Status}");
                            break;
                        }
                    case CodeInterpreterToolCall codeInterpreter:
                        {
                            followUpSummaries.Add($"Code interpreter executed code. Status: {codeInterpreter.Status}");
                            break;
                        }
                    case LocalShellToolCall shellCall:
                        {
                            followUpSummaries.Add($"Local shell command executed. Status: {shellCall.Status}");
                            break;
                        }
                    case OutputMessage message:
                        {
                            // Handle normal assistant messages - already extracted above
                            break;
                        }
                    default: 
                        {
                            // Log unhandled item types for debugging
                            followUpSummaries.Add($"Unhandled response item type: {item.GetType().Name}");
                            break;
                        }
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