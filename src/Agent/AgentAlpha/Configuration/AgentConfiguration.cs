using ModelContextProtocol.Client;
using MCPClient;
using AgentAlpha.Models;

namespace AgentAlpha.Configuration;

/// <summary>
/// Configuration for activity logging verbosity and behavior
/// </summary>
public class ActivityLoggingConfig
{
    /// <summary>
    /// Enable verbose logging that includes full OpenAI request/response data
    /// </summary>
    public bool VerboseOpenAI { get; set; } = true;
    
    /// <summary>
    /// Enable verbose logging that includes full tool input/output data
    /// </summary>
    public bool VerboseTools { get; set; } = true;
    
    /// <summary>
    /// Maximum size of data to log before truncation (in characters)
    /// </summary>
    public int MaxDataSize { get; set; } = 50000;
    
    /// <summary>
    /// Maximum size for individual string fields before truncation
    /// </summary>
    public int MaxStringSize { get; set; } = 5000;
    
    /// <summary>
    /// Maximum number of messages to include in OpenAI request logging
    /// </summary>
    public int MaxMessagesInLog { get; set; } = 50;
}

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
    /// Activity logging configuration
    /// </summary>
    public ActivityLoggingConfig ActivityLogging { get; set; } = new();
    
    /// <summary>
    /// Create configuration from environment variables
    /// </summary>
    /// <returns>Validated configuration instance</returns>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid</exception>
    public static AgentConfiguration FromEnvironment()
    {
        var config = new AgentConfiguration
        {
            OpenAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "",
            ServerUrl = Environment.GetEnvironmentVariable("MCP_SERVER_URL")
        };

        // Parse and validate transport type
        var transport = (Environment.GetEnvironmentVariable("MCP_TRANSPORT") ?? "stdio").ToLowerInvariant();
        config.Transport = transport switch
        {
            "http" or "sse" => McpTransportType.Http,
            "stdio" => McpTransportType.Stdio,
            _ => throw new InvalidOperationException($"Invalid MCP_TRANSPORT value: '{transport}'. Supported values: 'stdio', 'http', 'sse'")
        };

        // Parse and validate model name
        var model = Environment.GetEnvironmentVariable("AGENT_MODEL");
        if (!string.IsNullOrEmpty(model))
        {
            config.Model = ValidateModel(model);
        }

        // Parse and validate max iterations
        var maxIterationsStr = Environment.GetEnvironmentVariable("MAX_ITERATIONS");
        if (!string.IsNullOrEmpty(maxIterationsStr))
        {
            if (int.TryParse(maxIterationsStr, out var maxIterations) && maxIterations > 0)
            {
                config.MaxIterations = maxIterations;
            }
            else
            {
                throw new InvalidOperationException($"Invalid MAX_ITERATIONS value: '{maxIterationsStr}'. Must be a positive integer.");
            }
        }

        config.ToolFilter = ToolFilterConfig.FromEnvironment();
        
        // Parse tool selection configuration from environment with validation
        ParseToolSelectionConfig(config);
        
        // Parse MaxConversationMessages from environment with validation
        ParseMaxConversationMessages(config);
        
        // Validate the complete configuration
        ValidateConfiguration(config);
        
        return config;
    }

    /// <summary>
    /// Validates the complete configuration for consistency and required values
    /// </summary>
    /// <param name="config">Configuration to validate</param>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid</exception>
    private static void ValidateConfiguration(AgentConfiguration config)
    {
        // Validate HTTP transport has server URL
        if (config.Transport == McpTransportType.Http && string.IsNullOrEmpty(config.ServerUrl))
        {
            throw new InvalidOperationException("MCP_SERVER_URL is required when using HTTP transport");
        }

        // Validate tool selection settings
        if (config.ToolSelection.MaxToolsPerRequest <= 0)
        {
            throw new InvalidOperationException($"MaxToolsPerRequest must be positive, got: {config.ToolSelection.MaxToolsPerRequest}");
        }

        // Validate conversation message limits
        if (config.MaxConversationMessages < 0)
        {
            throw new InvalidOperationException($"MaxConversationMessages cannot be negative, got: {config.MaxConversationMessages}");
        }
    }

    /// <summary>
    /// Validates and normalizes the AI model name
    /// </summary>
    /// <param name="model">Model name to validate</param>
    /// <returns>Validated model name</returns>
    /// <exception cref="InvalidOperationException">Thrown when model name is invalid</exception>
    private static string ValidateModel(string model)
    {
        var validModels = new[] { "gpt-4o", "gpt-4", "gpt-4-turbo", "gpt-3.5-turbo", "gpt-4o-mini" };
        
        if (validModels.Contains(model, StringComparer.OrdinalIgnoreCase))
        {
            return model;
        }
        
        throw new InvalidOperationException($"Invalid AGENT_MODEL value: '{model}'. Supported models: {string.Join(", ", validModels)}");
    }

    /// <summary>
    /// Parses tool selection configuration with validation
    /// </summary>
    /// <param name="config">Configuration to update</param>
    /// <exception cref="InvalidOperationException">Thrown when values are invalid</exception>
    private static void ParseToolSelectionConfig(AgentConfiguration config)
    {
        if (int.TryParse(Environment.GetEnvironmentVariable("MAX_TOOLS_PER_REQUEST"), out var maxTools))
        {
            if (maxTools > 0)
            {
                config.ToolSelection.MaxToolsPerRequest = maxTools;
            }
            else
            {
                throw new InvalidOperationException($"MAX_TOOLS_PER_REQUEST must be positive, got: {maxTools}");
            }
        }
        
        if (bool.TryParse(Environment.GetEnvironmentVariable("USE_LLM_TOOL_SELECTION"), out var useLLM))
        {
            config.ToolSelection.UseLLMSelection = useLLM;
        }
        
        var selectionModel = Environment.GetEnvironmentVariable("TOOL_SELECTION_MODEL");
        if (!string.IsNullOrEmpty(selectionModel))
        {
            config.ToolSelection.SelectionModel = ValidateModel(selectionModel);
        }
    }

    /// <summary>
    /// Parses maximum conversation messages with validation
    /// </summary>
    /// <param name="config">Configuration to update</param>
    /// <exception cref="InvalidOperationException">Thrown when value is invalid</exception>
    private static void ParseMaxConversationMessages(AgentConfiguration config)
    {
        var maxMessagesStr = Environment.GetEnvironmentVariable("MAX_CONVERSATION_MESSAGES");
        if (!string.IsNullOrEmpty(maxMessagesStr))
        {
            if (int.TryParse(maxMessagesStr, out var maxMessages) && maxMessages >= 0)
            {
                config.MaxConversationMessages = maxMessages;
            }
            else
            {
                throw new InvalidOperationException($"Invalid MAX_CONVERSATION_MESSAGES value: '{maxMessagesStr}'. Must be a non-negative integer.");
            }
        }
    }
}