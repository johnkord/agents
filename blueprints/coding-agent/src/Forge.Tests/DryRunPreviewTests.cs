using Forge.Core;
using Microsoft.Extensions.AI;

namespace Forge.Tests;

public class DryRunPreviewTests
{
    private static AgentOptions CreateOptions() => new()
    {
        Model = "gpt-test",
        DryRun = true,
        WorkspacePath = "/workspace",
        SessionsPath = "/workspace/sessions",
    };

    [Fact]
    public void Build_IncludesSystemPromptAndActiveToolsOnly()
    {
        var options = CreateOptions();
        var tools = new AITool[]
        {
            AIFunctionFactory.Create(() => "ok", "read_file", "Read a file from disk."),
            AIFunctionFactory.Create(() => "ok", "run_tests", "Run unit tests."),
            AIFunctionFactory.Create(() => "ok", "grep_search", "Search across files."),
            AIFunctionFactory.Create(() => "ok", "some_obscure_tool", "Does something obscure."),
        };

        var preview = DryRunPreview.Build(options, tools);

        Assert.Contains("=== System Prompt ===", preview);
        Assert.Contains(SystemPrompt.Build(options), preview);
        Assert.Contains("=== Tool List ===", preview);
        Assert.Contains("- read_file: Read a file from disk.", preview);
        Assert.Contains("- grep_search: Search across files.", preview);
        Assert.Contains("- run_tests: Run unit tests.", preview);
        Assert.Contains("- find_tools: Search for additional tools by description.", preview);
        // Non-core tools should NOT be in the active list
        Assert.DoesNotContain("- some_obscure_tool:", preview);
    }

    [Fact]
    public void BuildFromActiveTools_WithNoTools_ShowsPlaceholder()
    {
        var options = CreateOptions();

        var preview = DryRunPreview.BuildFromActiveTools(options, []);

        Assert.Contains("=== Tool List ===", preview);
        Assert.Contains("(no tools available)", preview);
    }
}
