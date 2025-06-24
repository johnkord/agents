using ModelContextProtocol.Client;
using MCPClient;
using AgentAlpha.Models;

namespace AgentAlpha.Configuration;

/// <summary>
/// Main configuration class for AgentAlpha
/// </summary>
public class AgentConfiguration
{
    /// <summary>
    /// OpenAI API key
    /// </summary>
    public string OpenAiApiKey { get; set; } = "";
    
    /// <summary>
    /// OpenAI model to use
    /// </summary>
    public string Model { get; set; } = "gpt-4o";
    
    /// <summary>
    /// Maximum number of conversation iterations
    /// </summary>
    public int MaxIterations { get; set; } = 10;
    
    /// <summary>
    /// MCP transport type to use
    /// </summary>
    public McpTransportType Transport { get; set; } = McpTransportType.Stdio;
    
    /// <summary>
    /// Server URL for HTTP transport
    /// </summary>
    public string? ServerUrl { get; set; }
    
    /// <summary>
    /// Tool filtering configuration
    /// </summary>
    public ToolFilterConfig ToolFilter { get; set; } = new();
    
    /// <summary>
    /// Tool selection configuration for context optimization
    /// </summary>
    public ToolSelectionConfig ToolSelection { get; set; } = new();
    
    /// <summary>
    /// Maximum number of messages to keep in conversation history (0 = unlimited)
    /// </summary>
    public int MaxConversationMessages { get; set; } = 0;
    
    /// <summary>
    /// Create configuration from environment variables
    /// </summary>
    public static AgentConfiguration FromEnvironment()
    {
        var config = new AgentConfiguration
        {
            OpenAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "",
            ServerUrl = Environment.GetEnvironmentVariable("MCP_SERVER_URL")
        };

        var transport = (Environment.GetEnvironmentVariable("MCP_TRANSPORT") ?? "stdio").ToLowerInvariant();
        config.Transport = transport switch
        {
            "http" or "sse" => McpTransportType.Http,
            _ => McpTransportType.Stdio
        };

        config.ToolFilter = ToolFilterConfig.FromEnvironment();
        
        // Parse tool selection configuration from environment
        if (int.TryParse(Environment.GetEnvironmentVariable("MAX_TOOLS_PER_REQUEST"), out var maxTools))
        {
            config.ToolSelection.MaxToolsPerRequest = maxTools;
        }
        
        if (bool.TryParse(Environment.GetEnvironmentVariable("USE_LLM_TOOL_SELECTION"), out var useLLM))
        {
            config.ToolSelection.UseLLMSelection = useLLM;
        }
        
        var selectionModel = Environment.GetEnvironmentVariable("TOOL_SELECTION_MODEL");
        if (!string.IsNullOrEmpty(selectionModel))
        {
            config.ToolSelection.SelectionModel = selectionModel;
        }
        
        // Parse MaxConversationMessages from environment
        if (int.TryParse(Environment.GetEnvironmentVariable("MAX_CONVERSATION_MESSAGES"), out var maxMessages))
        {
            config.MaxConversationMessages = maxMessages;
        }
        
        return config;
    }
}