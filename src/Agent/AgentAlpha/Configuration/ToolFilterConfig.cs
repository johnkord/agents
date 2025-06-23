namespace AgentAlpha.Configuration;

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