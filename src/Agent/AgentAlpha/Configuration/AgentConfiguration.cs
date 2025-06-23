using ModelContextProtocol.Client;
using MCPClient;

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
        
        return config;
    }
}