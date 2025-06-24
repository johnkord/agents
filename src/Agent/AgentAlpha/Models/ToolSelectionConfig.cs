namespace AgentAlpha.Models;

/// <summary>
/// Configuration for tool selection behavior
/// </summary>
public class ToolSelectionConfig
{
    /// <summary>
    /// Maximum number of tools to send to OpenAI per request (default: 10)
    /// </summary>
    public int MaxToolsPerRequest { get; set; } = 10;
    
    /// <summary>
    /// Whether to use LLM-based tool selection (default: true)
    /// </summary>
    public bool UseLLMSelection { get; set; } = true;
    
    /// <summary>
    /// Model to use for tool selection (should be fast/cheap, default: gpt-3.5-turbo)
    /// </summary>
    public string SelectionModel { get; set; } = "gpt-3.5-turbo";
    
    /// <summary>
    /// Temperature for tool selection LLM calls (default: 0.1 for consistent results)
    /// </summary>
    public double SelectionTemperature { get; set; } = 0.1;
    
    /// <summary>
    /// Whether to allow dynamic tool expansion during conversation (default: true)
    /// </summary>
    public bool AllowDynamicExpansion { get; set; } = true;
    
    /// <summary>
    /// Maximum additional tools to add per iteration (default: 3)
    /// </summary>
    public int MaxAdditionalToolsPerIteration { get; set; } = 3;
    
    /// <summary>
    /// Tool names that should always be included (e.g., "complete_task")
    /// </summary>
    public HashSet<string> EssentialTools { get; set; } = new() { "complete_task" };
    
    /// <summary>
    /// Create default configuration
    /// </summary>
    public static ToolSelectionConfig Default => new();
}

/// <summary>
/// Represents a tool with its relevance score and reasoning
/// </summary>
public class ToolRelevanceScore
{
    public string ToolName { get; set; } = "";
    public double RelevanceScore { get; set; }
    public string Reasoning { get; set; } = "";
    public string Category { get; set; } = "";
}

/// <summary>
/// Result of tool selection analysis
/// </summary>
public class ToolSelectionResult
{
    public List<ToolRelevanceScore> ScoredTools { get; set; } = new();
    public string[] SelectedToolNames { get; set; } = Array.Empty<string>();
    public string SelectionReasoning { get; set; } = "";
    public TimeSpan SelectionTime { get; set; }
}