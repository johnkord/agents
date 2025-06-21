namespace Forge.Tests;

using Forge.Core;

public class SystemPromptTests
{
    private static AgentOptions MakeOptions(string workspace = "/workspace") => new()
    {
        Model = "test-model",
        WorkspacePath = workspace,
    };

    [Fact]
    public void Build_WithoutLessons_ContainsWorkflowSections()
    {
        var prompt = SystemPrompt.Build(MakeOptions());

        Assert.Contains("Plan → Act → Verify", prompt);
        Assert.Contains("PLAN", prompt);
        Assert.Contains("ACT", prompt);
        Assert.Contains("VERIFY", prompt);
        Assert.Contains("REPORT", prompt);
    }

    [Fact]
    public void Build_ContainsVerificationChecklists()
    {
        var prompt = SystemPrompt.Build(MakeOptions());

        // Verification checklists per tool type (DeepVerifier-inspired)
        Assert.Contains("replace_string_in_file", prompt);
        Assert.Contains("read_file the changed region", prompt);
        Assert.Contains("run_tests", prompt);
        Assert.Contains("exit code", prompt.ToLowerInvariant());
    }

    [Fact]
    public void Build_ContainsVerificationScaling()
    {
        var prompt = SystemPrompt.Build(MakeOptions());

        Assert.Contains("Scale verification to risk", prompt);
        Assert.Contains("Trivial", prompt);
        Assert.Contains("tests pass, compilation is already verified", prompt);
    }

    /// <summary>
    /// Prompt integrity test: verifies the system prompt never references tools
    /// that don't exist in the core registry. This catches the exact bug from
    /// session 20260320-010300-906 where the prompt referenced 'get_errors' but
    /// the tool didn't exist.
    ///
    /// Research basis: "The Reasoning Trap" (arXiv:2510.22977) — system prompt
    /// tool references create expectations that amplify tool hallucination.
    /// </summary>
    [Fact]
    public void Build_DefaultCoreTools_DoesNotReferenceGetErrors()
    {
        // get_errors is NOT a core tool — verify the prompt doesn't mention it
        var coreTools = ToolRegistry.GetCoreToolNames();
        Assert.DoesNotContain("get_errors", coreTools);

        var prompt = SystemPrompt.Build(MakeOptions(), availableToolNames: coreTools);

        // The word "get_errors" should not appear anywhere in the prompt
        Assert.DoesNotContain("get_errors", prompt);
    }

    [Fact]
    public void Build_WithGetErrorsRegistered_DoesReferenceGetErrors()
    {
        var toolsWithGetErrors = new HashSet<string>(ToolRegistry.GetCoreToolNames(), StringComparer.OrdinalIgnoreCase)
        {
            "get_errors"
        };

        var prompt = SystemPrompt.Build(MakeOptions(), availableToolNames: toolsWithGetErrors);

        Assert.Contains("get_errors", prompt);
    }

    [Fact]
    public void BuildVerificationChecklist_AdaptsToAvailableTools()
    {
        var minimal = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "read_file", "replace_string_in_file" };
        var checklist = SystemPrompt.BuildVerificationChecklist(minimal);

        // Should still have read_file reference
        Assert.Contains("read_file", checklist);
        // Should NOT reference run_tests since it's not available
        Assert.DoesNotContain("run_tests", checklist);
        // Should suggest building since get_errors isn't available
        Assert.Contains("Build the project", checklist);
    }

    [Fact]
    public void Build_ContainsGroundedThinkingPrompts()
    {
        var prompt = SystemPrompt.Build(MakeOptions());

        // Grounded thinking: predict outcomes (Dyna-Think, Brittle ReAct findings)
        Assert.Contains("Predict", prompt);
        Assert.Contains("prediction correct", prompt.ToLowerInvariant());
    }

    [Fact]
    public void Build_WithLessons_IncludesLessonsSection()
    {
        var lessons = "- [2026-03-18] fail: \"Fix auth\" — stale context after read";
        var prompt = SystemPrompt.Build(MakeOptions(), lessons);

        Assert.Contains("Lessons from Previous Sessions", prompt);
        Assert.Contains("stale context", prompt);
    }

    [Fact]
    public void Build_WithNullLessons_OmitsLessonsSection()
    {
        var prompt = SystemPrompt.Build(MakeOptions(), null);

        Assert.DoesNotContain("Lessons from Previous Sessions", prompt);
    }

    [Fact]
    public void Build_ContainsWorkspacePath()
    {
        var prompt = SystemPrompt.Build(MakeOptions("/my/workspace"));

        Assert.Contains("/my/workspace", prompt);
    }

    [Fact]
    public void Build_ContainsAllCoreTools()
    {
        var prompt = SystemPrompt.Build(MakeOptions());

        Assert.Contains("read_file", prompt);
        Assert.Contains("create_file", prompt);
        Assert.Contains("replace_string_in_file", prompt);
        Assert.Contains("grep_search", prompt);
        Assert.Contains("file_search", prompt);
        Assert.Contains("list_directory", prompt);
        Assert.Contains("run_bash_command", prompt);
        Assert.Contains("run_tests", prompt);
        Assert.Contains("find_tools", prompt);
    }

    // ── Debugging Protocol tests (Phase 5A) ──────────────────────

    [Theory]
    [InlineData("Fix the bug in parser")]
    [InlineData("Tests are FAILING after my change")]
    [InlineData("There's an error in the auth module")]
    [InlineData("The app is broken")]
    [InlineData("Diagnose the crash in the API")]
    [InlineData("Debug the login issue")]
    [InlineData("Investigate why tests fail")]
    [InlineData("Something is wrong with the output")]
    [InlineData("The server is not working")]
    [InlineData("There's a regression in v2")]
    [InlineData("Handle the exception in startup")]
    [InlineData("Getting unexpected results from query")]
    public void Build_WithDebuggingTask_IncludesDebuggingProtocol(string task)
    {
        var prompt = SystemPrompt.Build(MakeOptions(), task: task);

        Assert.Contains("Debugging Protocol", prompt);
        Assert.Contains("Reproduce", prompt);
        Assert.Contains("Hypothesize", prompt);
        Assert.Contains("IS the cause", prompt);
        Assert.Contains("is NOT the cause", prompt);
    }

    [Theory]
    [InlineData("Add a new endpoint to the API")]
    [InlineData("Refactor the service layer")]
    [InlineData("Create a README.md")]
    [InlineData("Read the file and summarize it")]
    [InlineData(null)]
    [InlineData("")]
    public void Build_WithNonDebuggingTask_OmitsDebuggingProtocol(string? task)
    {
        var prompt = SystemPrompt.Build(MakeOptions(), task: task);

        Assert.DoesNotContain("Debugging Protocol", prompt);
    }

    [Fact]
    public void IsDebuggingTask_CaseInsensitive()
    {
        Assert.True(SystemPrompt.IsDebuggingTask("FIX THE BUG"));
        Assert.True(SystemPrompt.IsDebuggingTask("There is an ERROR"));
        Assert.True(SystemPrompt.IsDebuggingTask("Tests FAILING"));
    }

    [Fact]
    public void Build_DebuggingProtocol_ContainsForAndAgainstPattern()
    {
        // FVDebug-inspired: for-and-against prompting to prevent confirmation bias
        var prompt = SystemPrompt.Build(MakeOptions(), task: "Fix the failing test");

        Assert.Contains("IS the cause", prompt);
        Assert.Contains("is NOT the cause", prompt);
    }

    // ── Assumption guidance tests (Phase 5B) ──────────────────────

    [Fact]
    public void Build_PlanSection_ContainsAssumptionGuidance()
    {
        // Ambig-SWE (ICLR 2026): always-on assumption guidance in PLAN section
        var prompt = SystemPrompt.Build(MakeOptions());

        Assert.Contains("multiple valid interpretations", prompt);
        Assert.Contains("existing tests", prompt);
    }

    [Fact]
    public void Build_PlanSection_AssumptionGuidanceIsAlwaysOn()
    {
        // The assumption guidance should be present regardless of task type
        // (not conditional like the Debugging Protocol)
        var prompt1 = SystemPrompt.Build(MakeOptions(), task: "Add a new API endpoint");
        var prompt2 = SystemPrompt.Build(MakeOptions(), task: "Fix the auth bug");
        var prompt3 = SystemPrompt.Build(MakeOptions());

        Assert.Contains("multiple valid interpretations", prompt1);
        Assert.Contains("multiple valid interpretations", prompt2);
        Assert.Contains("multiple valid interpretations", prompt3);
    }
}
