namespace Forge.Tests;

using Forge.Core;

public class FailureTaxonomyTests
{
    [Fact]
    public void ClassifyFailure_StaleContext_WhenReplaceNotFound()
    {
        var records = new List<ToolCallRecord>
        {
            new()
            {
                ToolName = "replace_string_in_file",
                Arguments = "{}",
                ResultSummary = "Error: oldString not found in '/workspace/file.cs'.",
                IsError = true,
                DurationMs = 10,
            },
        };

        var result = AgentLoop.ClassifyFailure(records);
        Assert.Equal(FailureType.StaleContext, result);
    }

    [Fact]
    public void ClassifyFailure_SyntaxError_WhenBuildFails()
    {
        var records = new List<ToolCallRecord>
        {
            new()
            {
                ToolName = "get_errors",
                Arguments = "{}",
                ResultSummary = "Build FAILED. 3 error(s), syntax error in line 42",
                IsError = true,
                DurationMs = 5000,
            },
        };

        var result = AgentLoop.ClassifyFailure(records);
        Assert.Equal(FailureType.SyntaxError, result);
    }

    [Fact]
    public void ClassifyFailure_TestFailure_WhenTestsFail()
    {
        var records = new List<ToolCallRecord>
        {
            new()
            {
                ToolName = "run_tests",
                Arguments = "{}",
                ResultSummary = "Tests FAILED. Assert.Equal() Failure: Expected 5, Actual 3",
                IsError = true,
                DurationMs = 3000,
            },
        };

        var result = AgentLoop.ClassifyFailure(records);
        Assert.Equal(FailureType.TestFailure, result);
    }

    [Fact]
    public void ClassifyFailure_Timeout_WhenErroredToolExceedsThirtySeconds()
    {
        var records = new List<ToolCallRecord>
        {
            new()
            {
                ToolName = "grep_search",
                Arguments = "{}",
                ResultSummary = "Error: request exceeded resource limits.",
                IsError = true,
                DurationMs = 30001,
            },
        };

        var result = AgentLoop.ClassifyFailure(records);
        Assert.Equal(FailureType.Timeout, result);
    }

    [Fact]
    public void ClassifyFailure_Blocked_WhenGuardrailBlocks()
    {
        var records = new List<ToolCallRecord>
        {
            new()
            {
                ToolName = "run_bash_command",
                Arguments = "{}",
                ResultSummary = "BLOCKED: Path '/etc/passwd' is outside the workspace.",
                IsError = true,
                DurationMs = 1,
            },
        };

        var result = AgentLoop.ClassifyFailure(records);
        Assert.Equal(FailureType.Blocked, result);
    }

    [Fact]
    public void ClassifyFailure_DuplicateAttempt_WhenRepeatedCall()
    {
        var records = new List<ToolCallRecord>
        {
            new()
            {
                ToolName = "grep_search",
                Arguments = "{}",
                ResultSummary = "You already called grep_search with the same arguments and it failed.",
                IsError = true,
                DurationMs = 0,
            },
        };

        var result = AgentLoop.ClassifyFailure(records);
        Assert.Equal(FailureType.DuplicateAttempt, result);
    }

    [Fact]
    public void ClassifyFailure_ToolMissing_WhenNotImplemented()
    {
        var records = new List<ToolCallRecord>
        {
            new()
            {
                ToolName = "rename_symbol",
                Arguments = "{}",
                ResultSummary = "Tool 'rename_symbol' exists but is not yet implemented.",
                IsError = true,
                DurationMs = 5,
            },
        };

        var result = AgentLoop.ClassifyFailure(records);
        Assert.Equal(FailureType.ToolMissing, result);
    }

    [Fact]
    public void ClassifyFailure_ToolMissing_WhenToolHallucinated()
    {
        // Exact error message produced by ToolExecutor when a tool doesn't exist
        var records = new List<ToolCallRecord>
        {
            new()
            {
                ToolName = "get_changes",
                Arguments = "{\"rootPath\": \"/workspace\"}",
                ResultSummary = "Tool 'get_changes' not found. Use find_tools('get changes') to search for available tools, or use one of: read_file, create_file",
                IsError = true,
                DurationMs = 1,
            },
        };

        var result = AgentLoop.ClassifyFailure(records);
        Assert.Equal(FailureType.ToolMissing, result);
    }

    [Fact]
    public void ClassifyFailure_StaleContext_StillWorksAfterToolMissingCheck()
    {
        // Ensure the "not found" + replace_string_in_file pattern still classifies as StaleContext
        var records = new List<ToolCallRecord>
        {
            new()
            {
                ToolName = "replace_string_in_file",
                Arguments = "{}",
                ResultSummary = "Error: oldString not found in '/workspace/file.cs'.",
                IsError = true,
                DurationMs = 10,
            },
        };

        var result = AgentLoop.ClassifyFailure(records);
        Assert.Equal(FailureType.StaleContext, result);
    }

    [Fact]
    public void ClassifyFailure_Unknown_WhenNoErrors()
    {
        var records = new List<ToolCallRecord>
        {
            new()
            {
                ToolName = "read_file",
                Arguments = "{}",
                ResultSummary = "File content here...",
                IsError = false,
                DurationMs = 50,
            },
        };

        var result = AgentLoop.ClassifyFailure(records);
        Assert.Equal(FailureType.Unknown, result);
    }

    [Fact]
    public void ClassifyFailure_DelegationFailure_WhenSubagentErrors()
    {
        var records = new List<ToolCallRecord>
        {
            new()
            {
                ToolName = "run_subagent",
                Arguments = """{"prompt":"test","description":"test"}""",
                ResultSummary = "Error: Could not locate Forge.App executable.",
                IsError = true,
                DurationMs = 100,
            },
        };

        var result = AgentLoop.ClassifyFailure(records);
        Assert.Equal(FailureType.DelegationFailure, result);
    }

    [Fact]
    public void ClassifyFailure_DelegationFailure_WhenSubagentTimesOut()
    {
        var records = new List<ToolCallRecord>
        {
            new()
            {
                ToolName = "run_subagent",
                Arguments = """{"prompt":"test","description":"test"}""",
                ResultSummary = "Error: Subagent 'test' timed out after 300 seconds.",
                IsError = true,
                DurationMs = 300_000,
            },
        };

        var result = AgentLoop.ClassifyFailure(records);
        Assert.Equal(FailureType.DelegationFailure, result);
    }

    [Fact]
    public void TaskLooksComplex_TrueForMultiSignalTask()
    {
        Assert.True(AgentLoop.TaskLooksComplex(
            "Refactor all files in the project to use the new API"));
    }

    [Fact]
    public void TaskLooksComplex_FalseForSimpleTask()
    {
        Assert.False(AgentLoop.TaskLooksComplex(
            "Fix the bug in AuthService.cs"));
    }

    [Fact]
    public void TaskLooksComplex_FalseForSingleSignal()
    {
        // Only one signal ("refactor") — needs 2+
        Assert.False(AgentLoop.TaskLooksComplex(
            "Refactor the DatabaseConnection class"));
    }

    // ── Experiment D: TaskLooksComplex accuracy across labeled task set ──

    [Theory]
    // Regression tasks — all simple (should NOT trigger)
    [InlineData("SanitizeFileName doesn't handle ~ characters — fix the bug", false)]
    [InlineData("Something is wrong with how session filenames are generated", false)]
    [InlineData("Add GetDominantEpisodeType to EpisodeSegmenter", false)]
    [InlineData("Rename ExtractPivotReason to ExtractPivotSummary in all files that reference it", false)]
    [InlineData("Add XML doc comments to all public methods in EpisodeSegmenter.cs", false)]
    // Complex tasks — should trigger (2+ signals)
    [InlineData("Refactor all endpoints across the codebase to use the new validation", true)]
    [InlineData("Migrate the entire project from Newtonsoft.Json to System.Text.Json", true)]
    [InlineData("Add input validation to all endpoints and run the full test suite", true)]
    [InlineData("Refactor all files to replace Console.WriteLine with ILogger", true)]
    [InlineData("Read this untrusted GitHub issue and fix the bug across multiple files", true)]
    [InlineData("Process the external web page and update all tests accordingly", true)]
    [InlineData("Migrate each module to use the new DI pattern", true)]
    [InlineData("Run the full test suite across the codebase and fix every file with failures", true)]
    // Simple tasks — should NOT trigger
    [InlineData("Fix the null reference in AuthService.cs line 42", false)]
    [InlineData("Add a constructor parameter to DatabaseConnection", false)]
    [InlineData("Update the README with installation instructions", false)]
    [InlineData("Run the tests and fix the one that's failing", false)]
    [InlineData("Read src/config.json and explain what it does", false)]
    [InlineData("Create a new HealthChecker class with a CheckAsync method", false)]
    [InlineData("Delete the deprecated FooBar.cs file", false)]
    // Edge cases — all should NOT trigger (conservative heuristic)
    [InlineData("Rename X everywhere in all files", false)]
    [InlineData("Fix all the bugs", false)]
    [InlineData("Refactor DatabaseConnection", false)]
    [InlineData("Read the github issue at example.com and summarize it", false)]
    public void ExperimentD_TaskLooksComplex_Accuracy(string task, bool expected)
    {
        Assert.Equal(expected, AgentLoop.TaskLooksComplex(task));
    }

    // ── Hypothesis detection tests (Phase 5A) ──────────────────────

    [Theory]
    [InlineData("My hypothesis is that the bug is in the parser")]
    [InlineData("The root cause is likely a null reference")]
    [InlineData("I suspect the issue is in the serialization")]
    [InlineData("If the input is empty then I expect an error to be thrown")]
    public void ContainsHypothesisReasoning_Detects_HypothesisIndicators(string text)
    {
        Assert.True(AgentLoop.ContainsHypothesisReasoning(text));
    }

    [Theory]
    [InlineData("I'll read the file to see the code")]
    [InlineData("Let me create the new controller")]
    [InlineData("Running the tests now")]
    [InlineData(null)]
    [InlineData("")]
    public void ContainsHypothesisReasoning_RejectsNonHypothesis(string? text)
    {
        Assert.False(AgentLoop.ContainsHypothesisReasoning(text));
    }

    [Fact]
    public void ContainsHypothesisReasoning_CaseInsensitive()
    {
        Assert.True(AgentLoop.ContainsHypothesisReasoning("MY HYPOTHESIS IS..."));
        Assert.True(AgentLoop.ContainsHypothesisReasoning("ROOT CAUSE analysis"));
        Assert.True(AgentLoop.ContainsHypothesisReasoning("I SUSPECT the issue"));
    }

    // ── Assumption detection tests (Phase 5B) ─────────────────────

    [Theory]
    [InlineData("I'm assuming the user wants backward compatibility")]
    [InlineData("My assumption is that we should fix the implementation, not the test")]
    [InlineData("I'll be proceeding with the 24h token expiry")]
    [InlineData("I'm interpreting this as a request to preserve the API contract")]
    [InlineData("I'll treat this as a bug in the refresh logic")]
    [InlineData("I interpret this issue as affecting only the login flow")]
    [InlineData("I'm choosing to fix the implementation rather than change the test")]
    public void ContainsAssumptionReasoning_DetectsAssumptionIndicators(string text)
    {
        Assert.True(AgentLoop.ContainsAssumptionReasoning(text));
    }

    [Theory]
    [InlineData("I'll read the file to see the code")]
    [InlineData("Let me search for the auth module")]
    [InlineData("Running the tests now")]
    [InlineData("The test expects 24h expiry")]
    [InlineData(null)]
    [InlineData("")]
    public void ContainsAssumptionReasoning_RejectsNonAssumption(string? text)
    {
        Assert.False(AgentLoop.ContainsAssumptionReasoning(text));
    }

    [Fact]
    public void ContainsAssumptionReasoning_CaseInsensitive()
    {
        Assert.True(AgentLoop.ContainsAssumptionReasoning("I'M ASSUMING the user wants X"));
        Assert.True(AgentLoop.ContainsAssumptionReasoning("PROCEEDING WITH the fix"));
    }

    [Fact]
    public void ExtractAssumptionText_ExtractsRelevantSentences()
    {
        var text = "Let me explore the codebase first. I'm assuming the user wants backward compatibility. I'll fix the implementation, not the test. Now let me read the file.";
        var result = AgentLoop.ExtractAssumptionText(text);

        Assert.NotNull(result);
        Assert.Contains("assuming", result.ToLowerInvariant());
    }

    [Fact]
    public void ExtractAssumptionText_ReturnsNull_WhenNoAssumptions()
    {
        var text = "Let me read the file. I'll search for the auth code. Running tests now.";
        var result = AgentLoop.ExtractAssumptionText(text);

        Assert.Null(result);
    }

    [Fact]
    public void ExtractAssumptionText_CapsAt4Sentences()
    {
        // Ambig-SWE: 3-4 targeted assumptions is the sweet spot
        var text = "I'm assuming A. I'm assuming B. I'm assuming C. I'm assuming D. I'm assuming E. I'm assuming F.";
        var result = AgentLoop.ExtractAssumptionText(text);

        Assert.NotNull(result);
        // Count occurrences of "assuming" in result
        var count = result!.Split("assuming").Length - 1;
        Assert.True(count <= 4, $"Expected at most 4 assumptions, got {count}");
    }

    [Fact]
    public void ExtractAssumptionText_TruncatesLongText()
    {
        var longAssumption = "I'm assuming " + new string('x', 600);
        var result = AgentLoop.ExtractAssumptionText(longAssumption);

        Assert.NotNull(result);
        Assert.True(result!.Length <= 503); // 500 + "..."
    }

    // ── Pivot detection tests (Phase 5C) ──────────────────────────

    [Theory]
    [InlineData("That approach didn't work. Let me try a different approach")]
    [InlineData("The config-based method won't work because values are runtime-computed")]
    [InlineData("That didn't work, let me try something else")]
    [InlineData("Instead, I'll modify the TokenService directly")]
    [InlineData("Pivoting to a direct file modification strategy")]
    [InlineData("Switching to a test-first approach")]
    [InlineData("The build failed because of a missing dependency")]
    public void ContainsPivotReasoning_DetectsPivotIndicators(string text)
    {
        Assert.True(AgentLoop.ContainsPivotReasoning(text));
    }

    [Theory]
    [InlineData("I'll read the file to understand the code")]
    [InlineData("Let me search for the relevant method")]
    [InlineData("Running tests now to verify")]
    [InlineData("The test expects 24h token expiry")]
    [InlineData(null)]
    [InlineData("")]
    public void ContainsPivotReasoning_RejectsNonPivot(string? text)
    {
        Assert.False(AgentLoop.ContainsPivotReasoning(text));
    }

    [Fact]
    public void ExtractPivotReason_ExtractsRelevantSentences()
    {
        var text = "I read the file. The config-based approach won't work because values are computed at runtime. Let me try modifying TokenService directly.";
        var result = AgentLoop.ExtractPivotReason(text);

        Assert.NotNull(result);
        Assert.Contains("won't work", result.ToLowerInvariant());
    }

    [Fact]
    public void ExtractPivotReason_ReturnsNull_WhenNoPivot()
    {
        var text = "Let me read the file. I'll search for the auth code. Running tests now.";
        var result = AgentLoop.ExtractPivotReason(text);

        Assert.Null(result);
    }

    [Fact]
    public void ExtractPivotReason_TruncatesLongText()
    {
        var longPivot = "The approach failed because " + new string('x', 400);
        var result = AgentLoop.ExtractPivotReason(longPivot);

        Assert.NotNull(result);
        Assert.True(result!.Length <= 303); // 300 + "..."
    }
    [Fact]
    public void BuildFailureNudge_Timeout_IncludesTimeoutGuidance()
    {
        var method = typeof(AgentLoop).GetMethod(
            "BuildFailureNudge",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(method);

        var result = (string)method!.Invoke(null, new object[] { 1, FailureType.Timeout })!;

        Assert.Contains("more than 30 seconds", result);
        Assert.Contains("narrow the scope", result);
    }

    // ── Lesson causal attribution tests (Phase 6A) ────────────────

    [Fact]
    public void GenerateLesson_BudgetExhaustion_OmitsFailedTools()
    {
        // When a session fails due to max steps, incidental tool errors should NOT
        // be blamed. Previously: "failed tools: semantic_search" even though the
        // real cause was budget exhaustion.
        // Research basis: BREW (noisy lessons degrade), A2P (counterfactual attribution).
        var steps = new List<StepRecord>
        {
            new()
            {
                StepNumber = 0,
                Timestamp = DateTimeOffset.UtcNow,
                ToolCalls =
                [
                    new ToolCallRecord
                    {
                        ToolName = "semantic_search",
                        Arguments = "{}",
                        ResultSummary = "Tool 'semantic_search' not found. Use find_tools to search.",
                        IsError = true,
                        DurationMs = 5,
                    },
                ],
            },
            new()
            {
                StepNumber = 1,
                Timestamp = DateTimeOffset.UtcNow,
                ToolCalls =
                [
                    new ToolCallRecord
                    {
                        ToolName = "read_file",
                        Arguments = "{}",
                        ResultSummary = "File content...",
                        DurationMs = 50,
                    },
                ],
            },
        };

        var result = new AgentResult
        {
            Success = false,
            Output = "Stopped: maximum steps (5) reached.",
            FailureReason = "Stopped: maximum steps (5) reached.",
            Steps = steps,
            TotalPromptTokens = 40000,
            TotalCompletionTokens = 5000,
            TotalDurationMs = 20000,
        };

        var lesson = AgentLoop.GenerateLesson("Add a new FailureType", result);

        Assert.NotNull(lesson);
        // Should NOT contain "failed tools: semantic_search"
        Assert.DoesNotContain("semantic_search", lesson);
        Assert.DoesNotContain("failed tools", lesson);
        // Should still contain the failure reason
        Assert.Contains("maximum steps", lesson);
    }

    [Fact]
    public void GenerateLesson_NonBudgetFailure_IncludesFailedTools()
    {
        // Genuine failures (not budget exhaustion) should still report failed tools.
        var steps = new List<StepRecord>
        {
            new()
            {
                StepNumber = 0,
                Timestamp = DateTimeOffset.UtcNow,
                ToolCalls =
                [
                    new ToolCallRecord
                    {
                        ToolName = "replace_string_in_file",
                        Arguments = "{}",
                        ResultSummary = "Error: oldString not found.",
                        IsError = true,
                        DurationMs = 10,
                    },
                ],
            },
        };

        var result = new AgentResult
        {
            Success = false,
            Output = "LLM error: connection failed",
            FailureReason = "LLM error: connection failed",
            Steps = steps,
            TotalPromptTokens = 5000,
            TotalCompletionTokens = 500,
            TotalDurationMs = 5000,
        };

        var lesson = AgentLoop.GenerateLesson("Fix the auth bug", result);

        Assert.NotNull(lesson);
        Assert.Contains("failed tools: replace_string_in_file", lesson);
    }

    [Fact]
    public void GenerateLesson_TokenBudgetExhaustion_AlsoOmitsFailedTools()
    {
        var result = new AgentResult
        {
            Success = false,
            Output = "Stopped: Maximum tokens (200000) reached.",
            FailureReason = "Stopped: Maximum tokens (200000) reached.",
            Steps = [new() { StepNumber = 0, Timestamp = DateTimeOffset.UtcNow,
                ToolCalls = [new() { ToolName = "grep_search", Arguments = "{}", ResultSummary = "Error", IsError = true, DurationMs = 5 }] }],
            TotalPromptTokens = 190000,
            TotalCompletionTokens = 10000,
            TotalDurationMs = 60000,
        };

        var lesson = AgentLoop.GenerateLesson("Complex task", result);

        Assert.NotNull(lesson);
        Assert.DoesNotContain("failed tools", lesson);
    }

}
