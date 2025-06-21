using Forge.Core;
using Microsoft.Extensions.AI;

namespace Forge.Tests;

public class ToolRegistryTests
{
    private ToolRegistry CreateRegistry(params string[] toolNames)
    {
        var registry = new ToolRegistry();
        var tools = toolNames.Select(name =>
            (AITool)AIFunctionFactory.Create(() => "result", name, $"Description for {name}"));
        registry.RegisterAll(tools);
        return registry;
    }

    [Fact]
    public void GetActiveTools_IncludesCoreTools()
    {
        var registry = CreateRegistry("read_file", "create_file", "list_directory",
            "grep_search", "replace_string_in_file", "run_bash_command",
            "some_stub_tool", "another_stub");

        var active = registry.GetActiveTools();
        var names = active.OfType<AIFunction>().Select(t => t.Name).ToList();

        Assert.Contains("read_file", names);
        Assert.Contains("create_file", names);
        Assert.Contains("grep_search", names);
        Assert.Contains("run_bash_command", names);
    }

    [Fact]
    public void GetActiveTools_ExcludesStubs()
    {
        var registry = CreateRegistry("read_file", "some_stub_tool", "another_stub");

        var active = registry.GetActiveTools();
        var names = active.OfType<AIFunction>().Select(t => t.Name).ToList();

        Assert.DoesNotContain("some_stub_tool", names);
        Assert.DoesNotContain("another_stub", names);
    }

    [Fact]
    public void GetActiveTools_AlwaysIncludesFindTools()
    {
        var registry = CreateRegistry("read_file");

        var active = registry.GetActiveTools();
        var names = active.OfType<AIFunction>().Select(t => t.Name).ToList();

        Assert.Contains("find_tools", names);
    }

    [Fact]
    public void Activate_MakesToolVisible()
    {
        var registry = CreateRegistry("read_file", "some_custom_tool");

        // Before activation
        var beforeNames = registry.GetActiveTools().OfType<AIFunction>().Select(t => t.Name).ToList();
        Assert.DoesNotContain("some_custom_tool", beforeNames);

        // Activate
        var result = registry.Activate("some_custom_tool");
        Assert.True(result);

        // After activation
        var afterNames = registry.GetActiveTools().OfType<AIFunction>().Select(t => t.Name).ToList();
        Assert.Contains("some_custom_tool", afterNames);
    }

    [Fact]
    public void Activate_UnknownTool_ReturnsFalse()
    {
        var registry = CreateRegistry("read_file");

        Assert.False(registry.Activate("nonexistent_tool"));
    }

    [Fact]
    public void FindTools_MatchesByName()
    {
        var registry = CreateRegistry("read_file", "my_test_helper", "test_failure");

        var result = registry.FindTools("test");

        Assert.Contains("my_test_helper", result);
        Assert.Contains("test_failure", result);
        Assert.Contains("activated", result);
    }

    [Fact]
    public void FindTools_MatchesByDescription()
    {
        var registry = new ToolRegistry();
        registry.RegisterAll([
            AIFunctionFactory.Create(() => "r", "read_file", "Read a file"),
            AIFunctionFactory.Create(() => "r", "custom_tool", "Runs unit tests in the workspace"),
        ]);

        var result = registry.FindTools("unit tests");

        Assert.Contains("custom_tool", result);
    }

    [Fact]
    public void FindTools_AutoActivatesMatches()
    {
        var registry = CreateRegistry("read_file", "run_tests", "get_errors");

        registry.FindTools("test");

        var names = registry.GetActiveTools().OfType<AIFunction>().Select(t => t.Name).ToList();
        Assert.Contains("run_tests", names);
    }

    [Fact]
    public void FindTools_NoMatches_ReturnsHelpful()
    {
        var registry = CreateRegistry("read_file");

        var result = registry.FindTools("quantum_computing");

        Assert.Contains("No additional tools found", result);
    }

    [Fact]
    public void FindTools_SkipsAlreadyActiveTools()
    {
        var registry = CreateRegistry("read_file", "custom_analysis");
        registry.Activate("custom_analysis");

        var result = registry.FindTools("analysis");

        // custom_analysis is already active, so shouldn't appear in results
        Assert.DoesNotContain("custom_analysis", result);
    }

    [Fact]
    public void GetStats_ReturnsCorrectCounts()
    {
        var registry = CreateRegistry("read_file", "create_file", "list_directory",
            "grep_search", "replace_string_in_file", "run_bash_command",
            "file_search", "run_tests", "get_project_setup_info", "manage_todos",
            "explore_codebase",
            "stub1", "stub2", "stub3");

        var (total, active, core) = registry.GetStats();

        Assert.Equal(14, total);
        Assert.Equal(10, core);
        Assert.Equal(11, active); // active = core (10) + find_tools (1), 0 user-activated
    }

    [Fact]
    public void ApplyMode_RestrictsActiveTools()
    {
        var registry = CreateRegistry("read_file", "create_file", "list_directory",
            "grep_search", "file_search", "run_bash_command", "run_tests",
            "explore_codebase", "search_codebase", "get_project_setup_info",
            "manage_todos", "replace_string_in_file");

        // Before mode restriction: core tools + find_tools
        var beforeTools = registry.GetActiveTools();
        var beforeNames = beforeTools.OfType<AIFunction>().Select(f => f.Name).ToHashSet();
        Assert.Contains("find_tools", beforeNames);
        Assert.Contains("create_file", beforeNames);

        // Apply explore mode
        registry.ApplyMode("explore");

        var afterTools = registry.GetActiveTools();
        var afterNames = afterTools.OfType<AIFunction>().Select(f => f.Name).ToHashSet();

        // Explore mode should include read_file but NOT create_file, replace_string_in_file
        Assert.Contains("read_file", afterNames);
        Assert.Contains("grep_search", afterNames);
        Assert.DoesNotContain("create_file", afterNames);
        Assert.DoesNotContain("replace_string_in_file", afterNames);
        Assert.DoesNotContain("run_bash_command", afterNames);
        // find_tools should NOT be included in restricted mode (prevents bypassing restrictions)
        Assert.DoesNotContain("find_tools", afterNames);
        // explore_codebase is in mode allowlist but is Tier 2 — only visible if
        // explicitly activated (e.g., via find_tools in unrestricted mode first)
        Assert.DoesNotContain("explore_codebase", afterNames);
    }
}
