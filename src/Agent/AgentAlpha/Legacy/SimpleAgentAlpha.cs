using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OpenAIIntegration;
using OpenAIIntegration.Model;
using System.Text.Json;
using MCPClient;
using AgentAlpha.Configuration;

namespace AgentAlpha.Legacy;

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

        Console.WriteLine($"📝 Task: {task}");

        const int MaxIterations = 10;
        for (int i = 0; i < MaxIterations; i++)
        {
            Console.WriteLine($"\n--- Iteration {i + 1} ---");

            var req = new ResponsesCreateRequest
            {
                Model      = "gpt-4o",
                Input      = msgs.ToArray(),
                Tools      = openAiTools,
                ToolChoice = "auto"
            };

            var res = await _openAi.CreateResponseAsync(req);

            // --- NEW: show the entire response from OpenAI ---------------------------
            Console.WriteLine("🔄 Full OpenAI response:");
            Console.WriteLine(JsonSerializer.Serialize(
                res,
                new JsonSerializerOptions { WriteIndented = true }));
            // -------------------------------------------------------------------------

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
                        var name = funcCall.Name;
                        _logger.LogDebug("OpenAI FunctionToolCall detected: {Name}  Args: {Args}",
                                         name, funcCall.Arguments?.GetRawText());

                        if (!string.IsNullOrEmpty(name))
                        {
                            if (!_toolFilter.ShouldIncludeTool(name))
                            {
                                followUpSummaries.Add($"Function '{name}' call blocked by tool filter configuration.");
                                break;
                            }

                            // --- changed: robust argument extraction -------------
                            Dictionary<string, object?> args = funcCall.Arguments switch
                            {
                                { ValueKind: JsonValueKind.Object } v  => DeserializeArguments(v),
                                { ValueKind: JsonValueKind.String } v  => ParseStringArguments(v.GetString()),
                                _                                        => new()
                            };
                            // -----------------------------------------------------

                            var mcpRes = await mcp.CallToolAsync(name, args);
                            var txt = mcpRes.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text
                                      ?? "<no text>";
                            followUpSummaries.Add($"Function '{name}' called via MCP. Result: {txt}");
                        }
                        break;
                    }
                    // --------------------------------------------------------

                    case McpToolCall call:
                    {
                        var name = call.Name;
                        _logger.LogDebug("OpenAI McpToolCall detected: {Name}  Args: {Args}",
                                         name, call.Arguments);

                        if (!string.IsNullOrEmpty(name))
                        {
                            if (!_toolFilter.ShouldIncludeTool(name))
                            {
                                followUpSummaries.Add($"Tool '{name}' call blocked by tool filter configuration.");
                                break;
                            }

                            var args = string.IsNullOrWhiteSpace(call.Arguments)
                                ? new Dictionary<string, object?>()
                                : DeserializeArguments(JsonDocument.Parse(call.Arguments!).RootElement);
                            var mcpRes = await mcp.CallToolAsync(name, args);
                            var txt = mcpRes.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text
                                      ?? "<no text>";
                            followUpSummaries.Add($"Tool '{name}' called with args {call.Arguments}. Result: {txt}");
                        }
                        break;
                    }                  // ← remove redundant outer break
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

            // Shell tool ---------------------------------------------------
            "run_command" => new
            {
                type = "object",
                properties = new
                {
                    command = new { type = "string", description = "Command to execute in the shell" }
                },
                required = new[] { "command" }
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

    /// <summary>
    /// Convert JsonElement→Dictionary with primitive .NET types
    /// </summary>
    private static Dictionary<string, object?> DeserializeArguments(JsonElement jsonObj)
        => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonObj.GetRawText())!
           .ToDictionary(kvp => kvp.Key, kvp => ConvertJsonElement(kvp.Value));

    private static object? ConvertJsonElement(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String  => e.GetString(),
        JsonValueKind.Number  => e.TryGetInt64(out var l) ? l : e.GetDouble(),
        JsonValueKind.True    => true,
        JsonValueKind.False   => false,
        JsonValueKind.Null    => null,
        _                     => e.GetRawText() // fall back to raw JSON for objects/arrays
    };

    // -------------------- helper added ----------------------------------
    /// <summary>
    /// Converts a string payload into argument dictionary.
    /// If the string itself is JSON&nbsp;→ parse it, otherwise assume it’s the shell
    /// command (for run_command).
    /// </summary>
    private static Dictionary<string, object?> ParseStringArguments(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new();

        raw = raw.Trim();

        // If it looks like JSON, try to parse it.
        if (raw.StartsWith("{"))
        {
            try
            {
                return DeserializeArguments(JsonDocument.Parse(raw).RootElement);
            }
            catch
            {
                // fall through – treat as plain string below
            }
        }

        // Fallback: single “command” parameter.
        return new Dictionary<string, object?> { ["command"] = raw };
    }
}