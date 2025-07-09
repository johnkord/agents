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
    /// OpenAI model to use for conversation and general tasks
    /// </summary>
    public string Model { get; set; } = "gpt-4.1";
    
    /// <summary>
    /// OpenAI model to use for planning tasks (reasoning models like o1, o3, etc.)
    /// If not specified, falls back to the main Model
    /// </summary>
    public string? PlanningModel { get; set; }
    
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
    /// Maximum number of messages to keep in conversation history (0 = unlimited)
    /// </summary>
    public int MaxConversationMessages { get; set; } = 0;
    
    /// <summary>
    /// Activity logging configuration
    /// </summary>
    public ActivityLoggingConfig ActivityLogging { get; set; } = new();
    
    /// <summary>
    /// Web search tool configuration
    /// </summary>
    public WebSearchTool WebSearch { get; set; } = new();
    
    /// <summary>
    /// Enable router for task routing and fast-path execution
    /// </summary>
    public bool EnableRouter => true;

    /// <summary>
    /// Enable the use of a chained planner for complex task handling
    /// </summary>
    public bool UseChainedPlanner { get; set; } = false;

    /// <summary>
    /// Configuration for the chained planner's behavior and model usage
    /// </summary>
    public ChainedPlannerConfig ChainedPlanner { get; set; } = new();
    
    /// <summary>
    /// Desired quality level for generated plans, between 0 (low quality) and 1 (high quality)
    /// </summary>
    public double PlanQualityTarget   { get; set; } = 0.8;
    public int    MaxPlanRefinements  { get; set; } = 3;

    // NEW – run-time cadence for additional plan refinements (0 = disabled)
    public int PlanRefinementEveryIterations { get; set; } = 0;

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
            _ => McpTransportType.Http
        };

        // Parse and validate model name
        var model = Environment.GetEnvironmentVariable("AGENT_MODEL");
        if (!string.IsNullOrEmpty(model))
        {
            config.Model = ValidateModel(model);
        }

        // Parse and validate planning model name
        var planningModel = Environment.GetEnvironmentVariable("PLANNING_MODEL");
        if (!string.IsNullOrEmpty(planningModel))
        {
            config.PlanningModel = ValidateModel(planningModel);
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
                config.MaxIterations = 3;
                Console.WriteLine($"Invalid MAX_ITERATIONS value: '{maxIterationsStr}'. Defaulting to {config.MaxIterations}.");
            }
        }

        config.ToolFilter = ToolFilterConfig.FromEnvironment();
        
        // Parse MaxConversationMessages from environment with validation
        ParseMaxConversationMessages(config);

        config.UseChainedPlanner = 
            (Environment.GetEnvironmentVariable("USE_CHAINED_PLANNER") ?? "false")
                .Equals("true", StringComparison.OrdinalIgnoreCase);

        // Parse chained planner specific env vars (optional)
        config.ChainedPlanner.AnalyseModel =
            Environment.GetEnvironmentVariable("CHAINED_PLANNER_MODEL")
                ?? config.ChainedPlanner.AnalyseModel;

        config.ChainedPlanner.OutlineModel = config.ChainedPlanner.AnalyseModel;

        config.ChainedPlanner.DetailModel =
            Environment.GetEnvironmentVariable("CHAINED_PLANNER_DETAIL_MODEL")
                ?? config.ChainedPlanner.DetailModel;

        if (int.TryParse(Environment.GetEnvironmentVariable("CHAINED_PLANNER_MAX_TOKENS"),
                         out var maxTok) && maxTok > 0)
        {
            config.ChainedPlanner.MaxTokens = maxTok;
        }

        // Parse plan-evaluator settings
        if (double.TryParse(Environment.GetEnvironmentVariable("PLAN_QUALITY_TARGET"),
                            out var tgt) && tgt is >= 0 and <= 1)
            config.PlanQualityTarget = tgt;

        if (int.TryParse(Environment.GetEnvironmentVariable("MAX_PLAN_REFINEMENTS"),
                         out var maxRef) && maxRef >= 0)
            config.MaxPlanRefinements = maxRef;

        // Parse and validate plan refinement cadence
        if (int.TryParse(Environment.GetEnvironmentVariable("PLAN_REFINEMENT_EVERY_ITERATIONS"),
                         out var freq) && freq >= 0)
        {
            config.PlanRefinementEveryIterations = freq;
        }

        // Validate the complete configuration
        ValidateConfiguration(config);
        return config;
    }

    /// <summary>
    /// Gets the model to use for planning tasks, falling back to the main model if no planning model is specified
    /// </summary>
    /// <returns>The model name to use for planning</returns>
    public string GetPlanningModel()
    {
        return PlanningModel ?? Model;
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

            config.ServerUrl = "http://localhost:3000";
        }

        // Validate conversation message limits
        if (config.MaxConversationMessages < 0)
        {
            throw new InvalidOperationException($"MaxConversationMessages cannot be negative, got: {config.MaxConversationMessages}");
        }

        // Validate new settings
        if (config.PlanQualityTarget is < 0 or > 1)
            throw new InvalidOperationException("PLAN_QUALITY_TARGET must be between 0 and 1.");

        if (config.PlanRefinementEveryIterations < 0)
            throw new InvalidOperationException("PLAN_REFINEMENT_EVERY_ITERATIONS must be >= 0.");
    }

    /// <summary>
    /// Validates and normalizes the AI model name
    /// </summary>
    /// <param name="model">Model name to validate</param>
    /// <returns>Validated model name</returns>
    /// <exception cref="InvalidOperationException">Thrown when model name is invalid</exception>
    private static string ValidateModel(string model)
    {
        var validModels = new[] { 
            // Standard GPT models
            "gpt-4.1", "gpt-4", "gpt-4-turbo", "gpt-4.1-nano", "gpt-4.1-mini",
            // OpenAI reasoning models
            "o1", "o1-mini", "o1-preview", "o3", "o3-mini"
        };
        
        if (validModels.Contains(model, StringComparer.OrdinalIgnoreCase))
        {
            return model;
        }
        
        throw new InvalidOperationException($"Invalid model value: '{model}'. Supported models: {string.Join(", ", validModels)}");
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