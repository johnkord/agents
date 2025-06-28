# Unified Tool Management System - Usage Examples

This document demonstrates how the new unified tool management system works in practice, showing how both MCP tools and built-in OpenAI tools can be managed seamlessly.

## Before vs After

### Before (MCP tools only)
```csharp
// Old approach - only MCP tools were managed
var mcpTools = await toolManager.DiscoverToolsAsync(connection);
var filteredMcpTools = toolManager.ApplyFilters(mcpTools, config.ToolFilter);
var toolDefinitions = filteredMcpTools.Select(t => toolManager.CreateOpenAiToolDefinition(t)).ToArray();

// Built-in tools like web_search were handled separately and inconsistently
if (ShouldIncludeWebSearch(task))
{
    var webSearchTool = config.WebSearch.ToToolDefinition();
    // Manual handling required
}
```

### After (Unified approach)
```csharp
// New unified approach - all tools managed consistently
var allTools = await toolManager.DiscoverAllToolsAsync(connection);
var filteredTools = toolManager.ApplyFiltersToAllTools(allTools, config.ToolFilter);
var toolDefinitions = toolManager.ConvertToToolDefinitions(filteredTools);

// Both MCP and built-in tools are automatically included and managed uniformly
```

## Basic Usage Examples

### 1. Tool Discovery
```csharp
// Discover all available tools (MCP + built-in)
var connection = serviceProvider.GetRequiredService<IConnectionManager>();
var toolManager = serviceProvider.GetRequiredService<IToolManager>();

var allTools = await toolManager.DiscoverAllToolsAsync(connection);

foreach (var tool in allTools)
{
    Console.WriteLine($"Tool: {tool.Name} ({tool.Type})");
    Console.WriteLine($"Description: {tool.Description}");
    Console.WriteLine($"Can Execute: {tool.CanExecute()}");
    Console.WriteLine();
}
```

**Example Output:**
```
Tool: read_file (MCP)
Description: Read the contents of a file
Can Execute: True

Tool: write_file (MCP)
Description: Write content to a file
Can Execute: True

Tool: web_search (BuiltInOpenAI)
Description: Search the web for current information and real-time data
Can Execute: True
```

### 2. Tool Filtering
```csharp
// Apply consistent filtering to all tool types
var config = new AgentConfiguration
{
    ToolFilter = new ToolFilterConfig()
};

// Exclude specific tools
config.ToolFilter.Blacklist.Add("web_search");

var allTools = await toolManager.DiscoverAllToolsAsync(connection);
var filteredTools = toolManager.ApplyFiltersToAllTools(allTools, config.ToolFilter);

// web_search will be filtered out along with any blacklisted MCP tools
```

### 3. Built-in Tool Registration
```csharp
// Built-in tools are automatically registered based on configuration
var config = new AgentConfiguration
{
    WebSearch = new WebSearchTool
    {
        UserLocation = new WebSearchUserLocation 
        { 
            Country = "US", 
            City = "New York" 
        },
        SearchContextSize = "medium"
    }
};

var registry = new BuiltInToolRegistry(logger, config);

// Web search tool is now available in the unified system
var webSearchTool = registry.GetBuiltInTool("web_search");
var toolDefinition = webSearchTool.ToToolDefinition();
```

### 4. Tool Execution
```csharp
var tools = await toolManager.DiscoverAllToolsAsync(connection);
var webSearchTool = tools.FirstOrDefault(t => t.Name == "web_search");

if (webSearchTool != null)
{
    var arguments = new Dictionary<string, object?>
    {
        ["query"] = "latest AI developments"
    };
    
    // Unified execution - handles different tool types appropriately
    var result = await toolManager.ExecuteUnifiedToolAsync(webSearchTool, connection, arguments);
    Console.WriteLine($"Result: {result}");
}
```

## Integration with ToolSelector

The `ToolSelector` service already integrates with the unified system:

```csharp
var toolSelector = serviceProvider.GetRequiredService<IToolSelector>();
var allMcpTools = await toolManager.DiscoverToolsAsync(connection); // Still works

// Tool selection now includes built-in tools automatically
var selectedTools = await toolSelector.SelectToolsForTaskAsync(
    "Find current stock prices for Apple", 
    allMcpTools, 
    maxTools: 5
);

// selectedTools will include web_search if the task requires current information
```

## Configuration Integration

Built-in tools are configured through the standard configuration system:

```csharp
// Environment variable approach
Environment.SetEnvironmentVariable("AGENT_ENABLE_WEB_SEARCH", "true");
Environment.SetEnvironmentVariable("WEB_SEARCH_CONTEXT_SIZE", "high");

var config = AgentConfiguration.FromEnvironment();
// Web search tool will be automatically available

// Programmatic approach
var config = new AgentConfiguration
{
    WebSearch = new WebSearchTool
    {
        SearchContextSize = "medium",
        UserLocation = new WebSearchUserLocation { Country = "US" }
    }
};
```

## Backward Compatibility

All existing code continues to work unchanged:

```csharp
// Existing MCP-specific methods still work exactly as before
var mcpTools = await toolManager.DiscoverToolsAsync(connection);
var filteredMcpTools = toolManager.ApplyFilters(mcpTools, config.ToolFilter);
var mcpToolDefinition = toolManager.CreateOpenAiToolDefinition(mcpTools.First());
var mcpResult = await toolManager.ExecuteToolAsync(connection, "read_file", arguments);

// No changes needed to existing code
```

## Tool Metadata and Capabilities

The unified system provides rich metadata for all tool types:

```csharp
var tools = await toolManager.DiscoverAllToolsAsync(connection);

foreach (var tool in tools)
{
    var metadata = tool.GetMetadata();
    
    switch (tool.Type)
    {
        case ToolType.MCP:
            var mcpTool = (McpUnifiedTool)tool;
            Console.WriteLine($"MCP Tool: {mcpTool.McpTool.Name}");
            Console.WriteLine($"Input Schema: {metadata["inputSchema"]}");
            break;
            
        case ToolType.BuiltInOpenAI:
            var builtInTool = (BuiltInOpenAITool)tool;
            Console.WriteLine($"Built-in Tool: {builtInTool.Name}");
            Console.WriteLine($"Execution Type: {metadata["executionType"]}");
            break;
    }
}
```

## Future Extensions

The unified system is designed for easy extension:

```csharp
// Future: Custom tools
public class CustomAgentTool : IUnifiedTool
{
    public string Name => "custom_analysis";
    public string Description => "Perform custom data analysis";
    public ToolType Type => ToolType.Custom;
    
    public ToolDefinition ToToolDefinition() { /* implementation */ }
    public bool CanExecute() { /* implementation */ }
    public Dictionary<string, object?> GetMetadata() { /* implementation */ }
}

// Register custom tools
registry.RegisterBuiltInTool(new CustomAgentTool());
```

This unified approach ensures that all tools—regardless of their source—are managed consistently and can be provided to OpenAI requests seamlessly, solving the original issue where built-in OpenAI tools were not properly accounted for.