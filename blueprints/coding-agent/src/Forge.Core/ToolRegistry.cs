using Microsoft.Extensions.AI;

namespace Forge.Core;

/// <summary>
/// Manages which tools are visible to the LLM on each call.
///
/// Research basis:
/// - Agent Skills Architecture (2026): progressive disclosure — only pay tokens
///   for tools the agent actually needs. "Beyond a critical library size, skill
///   selection accuracy degrades sharply."
/// - HyFunc (2026): redundant context processing — sending all tool descriptions
///   every turn is the #1 token waste.
/// - MCP Description Smells (2026): tool selection probability is "predominantly
///   determined by semantic alignment between the query and the description."
/// - Research notes §4: DynamicToolRegistry pattern — always include core tools,
///   add task-specific tools on demand, keep recently-used tools available.
///
/// Design: two-tier progressive disclosure.
///   Tier 1 (always active): core tools the agent needs for any task.
///   Tier 2 (discoverable): all other tools. Not sent to the LLM unless
///     explicitly activated via the find_tools meta-tool or by the agent
///     requesting them.
///
/// The agent sees a "find_tools" tool that searches the full registry by
/// description. When it finds what it needs, those tools are activated for
/// the remainder of the session.
/// </summary>
public sealed class ToolRegistry
{
    private readonly Dictionary<string, AITool> _allTools = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _activatedNames = new(StringComparer.OrdinalIgnoreCase);
    private AITool? _findToolsFunction; // cached to avoid recreating per call
    private HashSet<string>? _modeRestriction; // when set, only these tools are allowed

    /// <summary>
    /// Tool name sets for each subagent mode. Null means "all tools" (no restriction).
    /// </summary>
    private static readonly Dictionary<string, HashSet<string>?> ModeTools = new(StringComparer.OrdinalIgnoreCase)
    {
        ["explore"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "read_file", "list_directory", "grep_search", "file_search",
            "search_codebase", "explore_codebase", "get_project_setup_info",
        },
        ["verify"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "read_file", "list_directory", "grep_search", "file_search",
            "run_tests", "run_bash_command", "get_errors", "test_failure",
        },
        ["execute"] = null, // all tools
    };

    /// <summary>
    /// Tool names that are always active — the core toolkit for any coding task.
    /// </summary>
    private static readonly HashSet<string> CoreTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "read_file",
        "create_file",
        "replace_string_in_file",
        "list_directory",
        "grep_search",
        "file_search",
        "run_bash_command",
        "run_tests",
        "get_project_setup_info",
        "manage_todos",
    };

    /// <summary>
    /// Register all tools from MCP (or any source). Call once at startup.
    /// </summary>
    public void RegisterAll(IEnumerable<AITool> tools)
    {
        foreach (var tool in tools)
        {
            var name = tool is AIFunction fn ? fn.Name : tool.ToString() ?? "";
            if (!string.IsNullOrEmpty(name))
                _allTools[name] = tool;
        }
    }

    /// <summary>
    /// Get the tools that should be included in the current LLM call.
    /// Returns: core tools + any tools the agent has activated this session
    /// + the find_tools meta-tool itself.
    /// When a mode restriction is active, only mode-allowed tools are included.
    /// </summary>
    public IReadOnlyList<AITool> GetActiveTools()
    {
        var active = new List<AITool>();

        foreach (var (name, tool) in _allTools)
        {
            if (_modeRestriction is not null && !_modeRestriction.Contains(name))
                continue;

            if (CoreTools.Contains(name) || _activatedNames.Contains(name))
                active.Add(tool);
        }

        // Include find_tools only when no mode restriction is active.
        // In restricted modes (explore, verify), find_tools could bypass the
        // restriction by activating tools outside the mode's allowlist.
        if (_modeRestriction is null)
        {
            _findToolsFunction ??= AIFunctionFactory.Create(FindTools, "find_tools",
                "Search for additional tools by description. Returns matching tool names " +
                "and descriptions. Use this when you need a capability not in your current tool set.");
            active.Add(_findToolsFunction);
        }

        return active;
    }

    /// <summary>
    /// Apply a tool mode restriction. Only tools in the mode's allowlist will be active.
    /// Used by subagent processes to limit their capabilities.
    /// </summary>
    public void ApplyMode(string mode)
    {
        if (ModeTools.TryGetValue(mode, out var allowed) && allowed is not null)
            _modeRestriction = allowed;
    }

    /// <summary>
    /// Activate a tool by name for the remainder of this session.
    /// Returns true if the tool exists and was activated.
    /// </summary>
    public bool Activate(string toolName)
    {
        if (_allTools.ContainsKey(toolName))
        {
            _activatedNames.Add(toolName);
            return true;
        }
        return false;
    }

    /// <summary>
    /// The meta-tool implementation. Searches all registered tools by keyword
    /// and returns matching names + descriptions. Also auto-activates matches.
    /// </summary>
    public string FindTools(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return "Provide a search query to find tools (e.g., 'test', 'git', 'notebook').";

        var matches = new List<(string Name, string Description)>();

        foreach (var (name, tool) in _allTools)
        {
            // Skip tools that are already active
            if (CoreTools.Contains(name) || _activatedNames.Contains(name))
                continue;

            var description = tool is AIFunction fn ? fn.Description ?? "" : "";
            if (name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                description.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add((name, description));
            }
        }

        if (matches.Count == 0)
            return $"No additional tools found matching '{query}'. Available core tools: {string.Join(", ", CoreTools)}";

        // Auto-activate all matches
        foreach (var (name, _) in matches)
            _activatedNames.Add(name);

        var result = $"Found and activated {matches.Count} tool(s):\n";
        foreach (var (name, desc) in matches)
        {
            var shortDesc = desc.Length > 100 ? desc[..100] + "..." : desc;
            result += $"  • {name}: {shortDesc}\n";
        }
        result += "\nThese tools are now available for use.";
        return result;
    }

    /// <summary>
    /// Get summary stats for logging.
    /// </summary>
    public (int Total, int Active, int Core) GetStats()
    {
        // Count actual active tools (core + activated non-core) to avoid double-counting
        var activeCount = _allTools.Keys.Count(name =>
            CoreTools.Contains(name) || _activatedNames.Contains(name));
        // find_tools is only included when no mode restriction is active
        var findToolsCount = _modeRestriction is null ? 1 : 0;
        return (_allTools.Count, activeCount + findToolsCount, CoreTools.Count);
    }

    /// <summary>
    /// Returns the set of core tool names (always-active tools).
    /// Used by SystemPrompt to only reference tools that actually exist.
    /// </summary>
    public static IReadOnlySet<string> GetCoreToolNames() => CoreTools;
}
