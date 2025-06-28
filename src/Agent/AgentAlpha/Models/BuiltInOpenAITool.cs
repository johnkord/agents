using OpenAIIntegration.Model;
using AgentAlpha.Interfaces;

namespace AgentAlpha.Models;

/// <summary>
/// Unified tool wrapper for built-in OpenAI tools like web_search_preview
/// </summary>
public class BuiltInOpenAITool : IUnifiedTool
{
    private readonly ToolDefinition _toolDefinition;
    private readonly Dictionary<string, object?> _metadata;

    public BuiltInOpenAITool(string name, string description, ToolDefinition toolDefinition, Dictionary<string, object?>? metadata = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        _toolDefinition = toolDefinition ?? throw new ArgumentNullException(nameof(toolDefinition));
        _metadata = metadata ?? new Dictionary<string, object?>();
    }

    public string Name { get; }

    public string Description { get; }

    public ToolType Type => ToolType.BuiltInOpenAI;

    public ToolDefinition ToToolDefinition()
    {
        return _toolDefinition;
    }

    public virtual bool CanExecute()
    {
        // Built-in OpenAI tools are executed by OpenAI itself, not locally
        // They're always "executable" in the sense that they can be included in requests
        return true;
    }

    public Dictionary<string, object?> GetMetadata()
    {
        var metadata = new Dictionary<string, object?>(_metadata)
        {
            ["toolDefinition"] = _toolDefinition,
            ["requiresConnection"] = false,
            ["executionType"] = "openai_builtin"
        };
        return metadata;
    }

    public override bool Equals(object? obj)
    {
        return obj is BuiltInOpenAITool other && Name == other.Name && Type == other.Type;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Type);
    }

    public override string ToString()
    {
        return $"Built-in OpenAI Tool: {Name} - {Description}";
    }
}

/// <summary>
/// Specific implementation for web search built-in tool
/// </summary>
public class WebSearchBuiltInTool : BuiltInOpenAITool
{
    public WebSearchBuiltInTool(WebSearchTool webSearchConfig) 
        : base(
            name: "web_search_preview",
            description: "Search the web for current information and real-time data",
            toolDefinition: webSearchConfig.ToToolDefinition(),
            metadata: new Dictionary<string, object?>
            {
                ["webSearchConfig"] = webSearchConfig,
                ["supportedModels"] = new[] { "gpt-4.1", "o4-mini", "gpt-4o" },
                ["requiresLocation"] = webSearchConfig.UserLocation != null
            })
    {
        WebSearchConfig = webSearchConfig;
    }

    /// <summary>
    /// The underlying web search configuration
    /// </summary>
    public WebSearchTool WebSearchConfig { get; }

    public override bool CanExecute()
    {
        // Web search has specific model requirements
        // For now, assume it can execute - the actual model check would be done at runtime
        return base.CanExecute();
    }
}