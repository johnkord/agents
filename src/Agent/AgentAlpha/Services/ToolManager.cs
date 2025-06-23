using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using AgentAlpha.Configuration;
using AgentAlpha.Interfaces;
using OpenAIIntegration.Model;

namespace AgentAlpha.Services;

/// <summary>
/// Implementation of tool discovery, validation, and schema management
/// </summary>
public class ToolManager : IToolManager
{
    private readonly ILogger<ToolManager> _logger;

    public ToolManager(ILogger<ToolManager> logger)
    {
        _logger = logger;
    }

    public async Task<IList<McpClientTool>> DiscoverToolsAsync(IConnectionManager connection)
    {
        try
        {
            var tools = await connection.ListToolsAsync();
            _logger.LogInformation("Discovered {Count} tools from MCP server", tools.Count);
            return tools;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover tools");
            throw;
        }
    }

    public IList<McpClientTool> ApplyFilters(IList<McpClientTool> tools, ToolFilterConfig filter)
    {
        var filteredTools = tools.Where(t => filter.ShouldIncludeTool(t.Name)).ToList();
        
        _logger.LogInformation("Applied filters: {Total} tools -> {Filtered} tools", tools.Count, filteredTools.Count);
        
        if (filteredTools.Count != tools.Count)
        {
            var excluded = tools.Where(t => !filter.ShouldIncludeTool(t.Name)).Select(t => t.Name);
            _logger.LogDebug("Excluded tools: {ExcludedTools}", string.Join(", ", excluded));
        }
        
        return filteredTools;
    }

    public OpenAIIntegration.Model.ToolDefinition CreateOpenAiToolDefinition(McpClientTool mcpTool)
    {
        return new OpenAIIntegration.Model.ToolDefinition
        {
            Type = "function",
            Name = mcpTool.Name,
            Description = mcpTool.Description,
            Parameters = CreateParametersForTool(mcpTool.Name)
        };
    }

    public async Task<string> ExecuteToolAsync(IConnectionManager connection, string toolName, Dictionary<string, object?> arguments)
    {
        try
        {
            var result = await connection.CallToolAsync(toolName, arguments);
            var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "<no text>";
            
            _logger.LogDebug("Tool {ToolName} executed successfully", toolName);
            return text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute tool {ToolName}", toolName);
            return $"Error executing {toolName}: {ex.Message}";
        }
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
                    format = new { type = "string", description = "Format type (uppercase, lowercase, title, sentence)" }
                },
                required = new[] { "text", "format" }
            },

            "split_text" => new
            {
                type = "object",
                properties = new
                {
                    text = new { type = "string", description = "Text to split" },
                    delimiter = new { type = "string", description = "Delimiter to split on", @default = "\n" }
                },
                required = new[] { "text" }
            },

            // HTTP tools
            "http_get" or "http_post" or "http_put" or "http_delete" => new
            {
                type = "object",
                properties = new
                {
                    url = new { type = "string", description = "URL to make the request to" },
                    headers = new { type = "object", description = "HTTP headers (optional)" },
                    body = new { type = "string", description = "Request body (for POST/PUT)" }
                },
                required = new[] { "url" }
            },

            // Shell commands
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
}