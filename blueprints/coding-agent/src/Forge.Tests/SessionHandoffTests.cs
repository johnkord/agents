namespace Forge.Tests;

using Forge.Core;

public class SessionHandoffTests : IDisposable
{
    private readonly string _testDir;

    public SessionHandoffTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "forge-handoff-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void Generate_SuccessfulSession_StatusComplete()
    {
        var result = MakeResult(success: true, output: "Fixed the bug in LoginService.cs");
        var handoff = HandoffGenerator.Generate("Fix auth bug", result, maxSteps: 30);

        Assert.Equal("complete", handoff.Status);
        Assert.Equal("Fix auth bug", handoff.Task);
        Assert.Contains("Fixed the bug", handoff.Summary);
    }

    [Fact]
    public void Generate_MaxStepsReached_StatusIncomplete()
    {
        var result = MakeResult(success: false, failureReason: "Stopped: maximum steps (30) reached.");
        var handoff = HandoffGenerator.Generate("Complex refactor", result, maxSteps: 30);

        Assert.Equal("incomplete", handoff.Status);
    }

    [Fact]
    public void Generate_TokenLimitReached_StatusIncomplete()
    {
        var result = MakeResult(success: false, failureReason: "Stopped: Maximum tokens (500000) reached.");
        var handoff = HandoffGenerator.Generate("Large task", result, maxSteps: 30);

        Assert.Equal("incomplete", handoff.Status);
    }

    [Fact]
    public void Generate_OtherFailure_StatusFailed()
    {
        var result = MakeResult(success: false, failureReason: "LLM error: connection refused");
        var handoff = HandoffGenerator.Generate("Some task", result, maxSteps: 30);

        Assert.Equal("failed", handoff.Status);
    }

    [Fact]
    public void Generate_ExtractsModifiedFiles()
    {
        var result = MakeResult(success: true, steps:
        [
            new StepRecord
            {
                StepNumber = 0,
                Timestamp = DateTimeOffset.UtcNow,
                ToolCalls =
                [
                    new ToolCallRecord
                    {
                        ToolName = "replace_string_in_file",
                        Arguments = """{"filePath":"/workspace/src/Auth.cs","oldString":"foo","newString":"bar"}""",
                        ResultSummary = "Replaced 1 occurrence.",
                        DurationMs = 50,
                    },
                    new ToolCallRecord
                    {
                        ToolName = "create_file",
                        Arguments = """{"filePath":"/workspace/tests/AuthTests.cs","content":"test"}""",
                        ResultSummary = "Created file.",
                        DurationMs = 30,
                    },
                ],
                PromptTokens = 1000,
                CompletionTokens = 500,
                DurationMs = 2000,
            },
        ]);

        var handoff = HandoffGenerator.Generate("Fix auth", result, maxSteps: 30);

        Assert.Contains("/workspace/src/Auth.cs", handoff.FilesModified);
        Assert.Contains("/workspace/tests/AuthTests.cs", handoff.FilesModified);
    }

    [Fact]
    public void Generate_ExtractsFailedApproaches()
    {
        var result = MakeResult(success: false, steps:
        [
            new StepRecord
            {
                StepNumber = 0,
                Timestamp = DateTimeOffset.UtcNow,
                ToolCalls =
                [
                    new ToolCallRecord
                    {
                        ToolName = "replace_string_in_file",
                        Arguments = """{"filePath":"/workspace/file.cs"}""",
                        ResultSummary = "Error: oldString not found in '/workspace/file.cs'.",
                        IsError = true,
                        DurationMs = 10,
                    },
                ],
                PromptTokens = 1000,
                CompletionTokens = 500,
                DurationMs = 1000,
            },
        ]);

        var handoff = HandoffGenerator.Generate("Fix bug", result, maxSteps: 30);

        Assert.NotEmpty(handoff.FailedApproaches);
        Assert.Contains("not found", handoff.FailedApproaches[0]);
    }

    [Fact]
    public void Generate_ExtractsLastTestOutput()
    {
        var result = MakeResult(success: false, steps:
        [
            new StepRecord
            {
                StepNumber = 0,
                Timestamp = DateTimeOffset.UtcNow,
                ToolCalls =
                [
                    new ToolCallRecord
                    {
                        ToolName = "run_tests",
                        Arguments = "{}",
                        ResultSummary = "Passed: 3, Failed: 2 — TokenTest, RefreshTest",
                        DurationMs = 5000,
                    },
                ],
                PromptTokens = 1000, CompletionTokens = 500, DurationMs = 6000,
            },
        ]);

        var handoff = HandoffGenerator.Generate("Fix tests", result, maxSteps: 30);

        Assert.NotNull(handoff.LastTestOutput);
        Assert.Contains("Failed: 2", handoff.LastTestOutput);
    }

    [Fact]
    public void BuildContinuationPrompt_ContainsAllSections()
    {
        var handoff = new SessionHandoff
        {
            Task = "Fix the authentication bug",
            Status = "incomplete",
            StepsCompleted = 12,
            MaxSteps = 30,
            TokensUsed = 250_000,
            Summary = "Located the bug at LoginService.cs:47",
            FilesModified = ["src/Auth/LoginService.cs"],
            FailedApproaches = ["Tried string.Equals — wrong approach"],
            LastTestOutput = "Passed: 3, Failed: 2",
            NextSteps = ["Fix mock setup", "Re-run tests"],
        };

        var prompt = HandoffGenerator.BuildContinuationPrompt(handoff);

        Assert.Contains("Continuing a previous session", prompt);
        Assert.Contains("Fix the authentication bug", prompt);
        Assert.Contains("incomplete", prompt);
        Assert.Contains("12/30 steps", prompt);
        Assert.Contains("LoginService.cs:47", prompt);
        Assert.Contains("src/Auth/LoginService.cs", prompt);
        Assert.Contains("do NOT retry", prompt);
        Assert.Contains("string.Equals", prompt);
        Assert.Contains("Passed: 3, Failed: 2", prompt);
        Assert.Contains("Fix mock setup", prompt);
        Assert.Contains("grep_search", prompt);
    }

    [Fact]
    public void ToJson_FromJson_Roundtrip()
    {
        var handoff = new SessionHandoff
        {
            Task = "Test task",
            Status = "complete",
            StepsCompleted = 5,
            MaxSteps = 30,
            TokensUsed = 10_000,
            Summary = "Done.",
            FilesModified = ["a.cs", "b.cs"],
        };

        var json = HandoffGenerator.ToJson(handoff);
        var loaded = HandoffGenerator.FromJson(json);

        Assert.NotNull(loaded);
        Assert.Equal("Test task", loaded.Task);
        Assert.Equal("complete", loaded.Status);
        Assert.Equal(5, loaded.StepsCompleted);
        Assert.Equal(2, loaded.FilesModified.Count);
    }

    [Fact]
    public void LoadFromSessionFile_FindsHandoffEvent()
    {
        var sessionFile = Path.Combine(_testDir, "test-session.jsonl");
        var handoff = new SessionHandoff
        {
            Task = "Test from file",
            Status = "incomplete",
            StepsCompleted = 7,
            MaxSteps = 30,
            TokensUsed = 50_000,
            Summary = "In progress.",
        };

        // Construct the event line exactly as EventLog.RecordHandoffAsync would
        var handoffDataJson = HandoffGenerator.ToJson(handoff);
        var envelope = new { @event = "session_handoff", ts = DateTimeOffset.UtcNow, data = handoff };
        var eventLine = System.Text.Json.JsonSerializer.Serialize(envelope, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        });

        File.WriteAllLines(sessionFile, [
            """{"event":"session_start","ts":"2026-03-19T10:00:00Z","data":{}}""",
            """{"event":"session_end","ts":"2026-03-19T10:02:00Z","data":{}}""",
            eventLine,
        ]);

        var loaded = HandoffGenerator.LoadFromSessionFile(sessionFile);

        Assert.NotNull(loaded);
        Assert.Equal("Test from file", loaded.Task);
        Assert.Equal("incomplete", loaded.Status);
        Assert.Equal(7, loaded.StepsCompleted);
    }

    [Fact]
    public void LoadFromSessionFile_NonexistentFile_ReturnsNull()
    {
        var result = HandoffGenerator.LoadFromSessionFile("/nonexistent/file.jsonl");
        Assert.Null(result);
    }

    [Fact]
    public void LoadFromSessionFile_WithoutHandoffEvent_ReturnsNull()
    {
        var sessionFile = Path.Combine(_testDir, "session-without-handoff.jsonl");

        File.WriteAllLines(sessionFile,
        [
            "{\"event\":\"session_start\",\"ts\":\"2026-03-19T10:00:00Z\",\"data\":{}}",
            "{\"event\":\"session_end\",\"ts\":\"2026-03-19T10:01:00Z\",\"data\":{}}",
        ]);

        var loaded = HandoffGenerator.LoadFromSessionFile(sessionFile);

        Assert.Null(loaded);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static AgentResult MakeResult(
        bool success,
        string? output = null,
        string? failureReason = null,
        IReadOnlyList<StepRecord>? steps = null) => new()
    {
        Success = success,
        Output = output ?? (success ? "Done." : "Failed."),
        Steps = steps ?? [],
        TotalPromptTokens = 10_000,
        TotalCompletionTokens = 2_000,
        TotalDurationMs = 30_000,
        FailureReason = failureReason ?? (success ? null : "Failed."),
    };

    private static int CountOccurrences(string value, string fragment)
    {
        var count = 0;
        var startIndex = 0;

        while ((startIndex = value.IndexOf(fragment, startIndex, StringComparison.Ordinal)) >= 0)
        {
            count++;
            startIndex += fragment.Length;
        }

        return count;
    }

    [Fact]
    public void Generate_SuccessfulSession_TruncatesLongSummary()
    {
        var result = MakeResult(success: true, output: new string('a', 1005));

        var handoff = HandoffGenerator.Generate("Summarize work", result, maxSteps: 30);

        Assert.Equal(1003, handoff.Summary.Length);
        Assert.EndsWith("...", handoff.Summary);
    }

    [Fact]
    public void Generate_ModifiedFiles_DeduplicatesCaseInsensitiveAndSkipsInvalidOrErroredCalls()
    {
        var result = MakeResult(success: true, steps:
        [
            new StepRecord
            {
                StepNumber = 0,
                Timestamp = DateTimeOffset.UtcNow,
                ToolCalls =
                [
                    new ToolCallRecord
                    {
                        ToolName = "create_directory",
                        Arguments = """{"path":"/workspace/Tests"}""",
                        ResultSummary = "Created directory.",
                        DurationMs = 10,
                    },
                    new ToolCallRecord
                    {
                        ToolName = "create_directory",
                        Arguments = """{"path":"/workspace/tests"}""",
                        ResultSummary = "Created directory.",
                        DurationMs = 10,
                    },
                    new ToolCallRecord
                    {
                        ToolName = "create_file",
                        Arguments = "not-json",
                        ResultSummary = "Created file.",
                        DurationMs = 10,
                    },
                    new ToolCallRecord
                    {
                        ToolName = "replace_string_in_file",
                        Arguments = """{"filePath":"/workspace/src/Auth.cs"}""",
                        ResultSummary = "Error: oldString not found.",
                        IsError = true,
                        DurationMs = 10,
                    },
                ],
                PromptTokens = 100,
                CompletionTokens = 50,
                DurationMs = 500,
            },
        ]);

        var handoff = HandoffGenerator.Generate("Track files", result, maxSteps: 30);

        Assert.Single(handoff.FilesModified);
        Assert.Contains(handoff.FilesModified, file =>
            string.Equals(file, "/workspace/Tests", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Generate_FailedApproaches_AreCappedAtFiveEntries()
    {
        var steps = Enumerable.Range(0, 7)
            .Select(stepNumber => new StepRecord
            {
                StepNumber = stepNumber,
                Timestamp = DateTimeOffset.UtcNow,
                ToolCalls =
                [
                    new ToolCallRecord
                    {
                        ToolName = "replace_string_in_file",
                        Arguments = """{"filePath":"/workspace/file.cs"}""",
                        ResultSummary = "Error: oldString not found in '/workspace/file.cs'.",
                        IsError = true,
                        DurationMs = 10,
                    },
                ],
                PromptTokens = 100,
                CompletionTokens = 50,
                DurationMs = 500,
            })
            .ToList();

        var result = MakeResult(success: false, steps: steps);

        var handoff = HandoffGenerator.Generate("Fix repeated edits", result, maxSteps: 30);

        Assert.Equal(5, handoff.FailedApproaches.Count);
        Assert.All(handoff.FailedApproaches, approach => Assert.Contains("oldString not found", approach));
        Assert.DoesNotContain(handoff.FailedApproaches, approach => approach.Contains("Step 5:"));
    }

    [Fact]
    public void Generate_FailureWithTestsAndBuildErrors_SuggestsTargetedNextSteps()
    {
        var result = MakeResult(success: false, steps:
        [
            new StepRecord
            {
                StepNumber = 0,
                Timestamp = DateTimeOffset.UtcNow,
                ToolCalls =
                [
                    new ToolCallRecord
                    {
                        ToolName = "run_tests",
                        Arguments = "{}",
                        ResultSummary = "FAIL ExampleTests.Should_work",
                        DurationMs = 1000,
                    },
                    new ToolCallRecord
                    {
                        ToolName = "create_and_run_task",
                        Arguments = "{}",
                        ResultSummary = "Build failed: CS1002 ; expected",
                        IsError = true,
                        DurationMs = 1000,
                    },
                ],
                PromptTokens = 100,
                CompletionTokens = 50,
                DurationMs = 1000,
            },
        ]);

        var handoff = HandoffGenerator.Generate("Repair test suite", result, maxSteps: 30);

        Assert.Equal(3, handoff.NextSteps.Count);
        Assert.Contains("Fix remaining test failures", handoff.NextSteps);
        Assert.Contains("Fix compile errors before proceeding", handoff.NextSteps);
        Assert.Contains("Run the full test suite to verify no regressions", handoff.NextSteps);
    }

    [Fact]
    public void Generate_FailedSession_SummarizesDistinctToolsErrorsAndSessionLogPath()
    {
        var longError = "Error: " + new string('x', 140);
        var result = MakeResult(success: false, steps:
        [
            new StepRecord
            {
                StepNumber = 0,
                Timestamp = DateTimeOffset.UtcNow,
                ToolCalls =
                [
                    new ToolCallRecord
                    {
                        ToolName = "read_file",
                        Arguments = "{}",
                        ResultSummary = "Read Auth.cs",
                        DurationMs = 10,
                    },
                    new ToolCallRecord
                    {
                        ToolName = "read_file",
                        Arguments = "{}",
                        ResultSummary = "Read Auth.cs again",
                        DurationMs = 10,
                    },
                    new ToolCallRecord
                    {
                        ToolName = "replace_string_in_file",
                        Arguments = "{}",
                        ResultSummary = longError,
                        IsError = true,
                        DurationMs = 10,
                    },
                    new ToolCallRecord
                    {
                        ToolName = "replace_string_in_file",
                        Arguments = "{}",
                        ResultSummary = longError,
                        IsError = true,
                        DurationMs = 10,
                    },
                    new ToolCallRecord
                    {
                        ToolName = "run_tests",
                        Arguments = "{}",
                        ResultSummary = "Timeout while running tests",
                        IsError = true,
                        DurationMs = 10,
                    },
                ],
                PromptTokens = 100,
                CompletionTokens = 50,
                DurationMs = 500,
            },
        ]) with
        {
            SessionLogPath = "/tmp/session.jsonl",
        };

        var handoff = HandoffGenerator.Generate("Diagnose issue", result, maxSteps: 30);

        Assert.Equal("failed", handoff.Status);
        Assert.Equal("/tmp/session.jsonl", handoff.SessionLogPath);
        Assert.Contains("Completed 1 steps.", handoff.Summary);
        Assert.Contains("Tools used: read_file.", handoff.Summary);
        Assert.Equal(1, CountOccurrences(handoff.Summary, "replace_string_in_file:"));
        Assert.Contains("run_tests: Timeout while running tests", handoff.Summary);
        Assert.Contains("...", handoff.Summary);
    }

    [Fact]
    public void Generate_FailureWithoutTestsOrBuildErrors_UsesDefaultNextSteps()
    {
        var result = MakeResult(success: false, steps:
        [
            new StepRecord
            {
                StepNumber = 0,
                Timestamp = DateTimeOffset.UtcNow,
                ToolCalls =
                [
                    new ToolCallRecord
                    {
                        ToolName = "read_file",
                        Arguments = "{}",
                        ResultSummary = "Inspected current implementation.",
                        DurationMs = 10,
                    },
                ],
                PromptTokens = 100,
                CompletionTokens = 50,
                DurationMs = 500,
            },
        ]);

        var handoff = HandoffGenerator.Generate("Continue investigation", result, maxSteps: 30);

        Assert.Equal(["Review the current state and continue the task", "Run the full test suite to verify no regressions"], handoff.NextSteps);
        Assert.Null(handoff.LastTestOutput);
    }

    [Fact]
    public void FromJson_InvalidJson_ReturnsNull()
    {
        var loaded = HandoffGenerator.FromJson("{ not valid json }");

        Assert.Null(loaded);
    }

    [Fact]
    public void LoadFromSessionFile_SkipsInvalidHandoffAndReturnsMostRecentValidEvent()
    {
        var sessionFile = Path.Combine(_testDir, "mixed-session.jsonl");
        var olderHandoff = new SessionHandoff
        {
            Task = "Older handoff",
            Status = "incomplete",
            StepsCompleted = 2,
            MaxSteps = 30,
            TokensUsed = 500,
            Summary = "Resume from here.",
        };

        var olderEventLine = System.Text.Json.JsonSerializer.Serialize(new
        {
            @event = "session_handoff",
            ts = DateTimeOffset.UtcNow.AddMinutes(-1),
            data = olderHandoff,
        }, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        });

        File.WriteAllLines(sessionFile,
        [
            olderEventLine,
            "{\"event\":\"session_handoff\",\"ts\":\"2026-03-19T10:03:00Z\",\"data\":{ not-json }}",
            "{\"event\":\"session_handoff\",\"ts\":\"2026-03-19T10:04:00Z\",\"data\":\"wrong-shape\"}",
        ]);

        var loaded = HandoffGenerator.LoadFromSessionFile(sessionFile);

        Assert.NotNull(loaded);
        Assert.Equal("Older handoff", loaded.Task);
        Assert.Equal("Resume from here.", loaded.Summary);
    }

    [Fact]
    public void Generate_UsesMostRecentTestOutput_AndTruncatesLongResult()
    {
        var latestSummary = "latest failure: " + new string('x', 340);
        var result = MakeResult(success: false, steps:
        [
            new StepRecord
            {
                StepNumber = 0,
                Timestamp = DateTimeOffset.UtcNow,
                ToolCalls =
                [
                    new ToolCallRecord
                    {
                        ToolName = "run_tests",
                        Arguments = "{}",
                        ResultSummary = "older result",
                        DurationMs = 1000,
                    },
                ],
                PromptTokens = 100,
                CompletionTokens = 50,
                DurationMs = 1000,
            },
            new StepRecord
            {
                StepNumber = 1,
                Timestamp = DateTimeOffset.UtcNow,
                ToolCalls =
                [
                    new ToolCallRecord
                    {
                        ToolName = "test_failure",
                        Arguments = "{}",
                        ResultSummary = latestSummary,
                        DurationMs = 1000,
                    },
                ],
                PromptTokens = 100,
                CompletionTokens = 50,
                DurationMs = 1000,
            },
        ]);

        var handoff = HandoffGenerator.Generate("Investigate failures", result, maxSteps: 30);

        Assert.NotNull(handoff.LastTestOutput);
        Assert.StartsWith("latest failure:", handoff.LastTestOutput);
        Assert.EndsWith("...", handoff.LastTestOutput);
        Assert.True(handoff.LastTestOutput.Length <= 303);
    }

    [Fact]
    public void BuildContinuationPrompt_WithoutOptionalSections_OmitsTheirHeadings()
    {
        var handoff = new SessionHandoff
        {
            Task = "Continue cleanup",
            Status = "failed",
            StepsCompleted = 3,
            MaxSteps = 30,
            TokensUsed = 1234,
            Summary = "Hit an unexpected runtime error.",
        };

        var prompt = HandoffGenerator.BuildContinuationPrompt(handoff);

        Assert.DoesNotContain("Files modified:", prompt);
        Assert.DoesNotContain("Approaches that were tried and failed", prompt);
        Assert.DoesNotContain("Last test output:", prompt);
        Assert.DoesNotContain("Suggested next steps:", prompt);
        Assert.Contains("IMPORTANT: Before editing, verify the current file state", prompt);
    }

    // ── Discovery context tests ────────────────────────────────────────────

    [Fact]
    public void ExtractDiscoveryContext_CapturesGrepFindings()
    {
        var result = MakeResult(success: false, steps:
        [
            new StepRecord
            {
                StepNumber = 0,
                Timestamp = DateTimeOffset.UtcNow,
                ToolCalls =
                [
                    new ToolCallRecord
                    {
                        ToolName = "grep_search",
                        Arguments = """{"query": "class HandoffGenerator"}""",
                        ResultSummary = "1 match(es):\nblueprints/coding-agent/src/Forge.Core/SessionHandoff.cs:33: public static class HandoffGenerator",
                        DurationMs = 50,
                    },
                ],
                PromptTokens = 100,
                CompletionTokens = 50,
                DurationMs = 500,
            },
        ]);

        var discoveries = HandoffGenerator.ExtractDiscoveryContext(result);

        Assert.Single(discoveries);
        Assert.Contains("SessionHandoff.cs", discoveries[0]);
        Assert.Contains("class HandoffGenerator", discoveries[0]);
    }

    [Fact]
    public void ExtractDiscoveryContext_CapturesFileReads()
    {
        var result = MakeResult(success: false, steps:
        [
            new StepRecord
            {
                StepNumber = 0,
                Timestamp = DateTimeOffset.UtcNow,
                ToolCalls =
                [
                    new ToolCallRecord
                    {
                        ToolName = "read_file",
                        Arguments = """{"filePath": "/workspace/src/Auth.cs", "startLine": 1, "endLine": 100}""",
                        ResultSummary = "Lines 1-100 of 250:\nnamespace App;",
                        DurationMs = 5,
                    },
                    new ToolCallRecord
                    {
                        ToolName = "read_file",
                        Arguments = """{"filePath": "/workspace/src/Auth.cs", "startLine": 100, "endLine": 200}""",
                        ResultSummary = "Lines 100-200 of 250:\npublic class Auth",
                        DurationMs = 5,
                    },
                ],
                PromptTokens = 100,
                CompletionTokens = 50,
                DurationMs = 500,
            },
        ]);

        var discoveries = HandoffGenerator.ExtractDiscoveryContext(result);

        // Same file read twice → should deduplicate
        Assert.Single(discoveries);
        Assert.Contains("/workspace/src/Auth.cs", discoveries[0]);
        Assert.Contains("250 lines", discoveries[0]); // extracted from "Lines 1-100 of 250"
    }

    [Fact]
    public void ExtractDiscoveryContext_CapturesTestResults()
    {
        var result = MakeResult(success: false, steps:
        [
            new StepRecord
            {
                StepNumber = 0,
                Timestamp = DateTimeOffset.UtcNow,
                ToolCalls =
                [
                    new ToolCallRecord
                    {
                        ToolName = "run_tests",
                        Arguments = "{}",
                        ResultSummary = "Passed! - Failed: 0, Passed: 193, Total: 193",
                        DurationMs = 5000,
                    },
                ],
                PromptTokens = 100,
                CompletionTokens = 50,
                DurationMs = 5000,
            },
        ]);

        var discoveries = HandoffGenerator.ExtractDiscoveryContext(result);

        Assert.Single(discoveries);
        Assert.Contains("193", discoveries[0]);
    }

    [Fact]
    public void ExtractDiscoveryContext_SkipsErrorCalls()
    {
        var result = MakeResult(success: false, steps:
        [
            new StepRecord
            {
                StepNumber = 0,
                Timestamp = DateTimeOffset.UtcNow,
                ToolCalls =
                [
                    new ToolCallRecord
                    {
                        ToolName = "grep_search",
                        Arguments = """{"query": "missing"}""",
                        ResultSummary = "Error: blocked",
                        IsError = true,
                        DurationMs = 5,
                    },
                ],
                PromptTokens = 100,
                CompletionTokens = 50,
                DurationMs = 500,
            },
        ]);

        var discoveries = HandoffGenerator.ExtractDiscoveryContext(result);

        Assert.Empty(discoveries);
    }

    [Fact]
    public void Generate_IncompleteSession_IncludesDiscoveryContext()
    {
        var result = MakeResult(success: false, steps:
        [
            new StepRecord
            {
                StepNumber = 0,
                Timestamp = DateTimeOffset.UtcNow,
                ToolCalls =
                [
                    new ToolCallRecord
                    {
                        ToolName = "read_file",
                        Arguments = """{"filePath": "/workspace/Important.cs"}""",
                        ResultSummary = "Lines 1-50 of 50:\npublic class Important",
                        DurationMs = 5,
                    },
                ],
                PromptTokens = 100,
                CompletionTokens = 50,
                DurationMs = 500,
            },
        ]);

        var handoff = HandoffGenerator.Generate("Investigate issue", result, maxSteps: 30);

        // Summary should contain discovery context for incomplete sessions
        Assert.Contains("Key discoveries", handoff.Summary);
        Assert.Contains("/workspace/Important.cs", handoff.Summary);
    }

    // ── Consolidation summary tests ────────────────────────────────────────

    [Fact]
    public void Generate_WithConsolidationSummary_StoresIt()
    {
        var result = MakeResult(success: false, failureReason: "Stopped: maximum steps (5) reached.");
        var consolidation = "Done: found HandoffGenerator in SessionHandoff.cs. Remaining: add tests for 6 uncovered branches.";

        var handoff = HandoffGenerator.Generate("Write tests", result, maxSteps: 5,
            consolidationSummary: consolidation);

        Assert.Equal(consolidation, handoff.ConsolidationSummary);
    }

    [Fact]
    public void Generate_WithConsolidationSummary_TruncatesIfTooLong()
    {
        var result = MakeResult(success: false);
        var longConsolidation = new string('x', 3000);

        var handoff = HandoffGenerator.Generate("Task", result, maxSteps: 30,
            consolidationSummary: longConsolidation);

        Assert.NotNull(handoff.ConsolidationSummary);
        Assert.True(handoff.ConsolidationSummary!.Length <= 2003); // 2000 + "..."
        Assert.EndsWith("...", handoff.ConsolidationSummary);
    }

    [Fact]
    public void BuildContinuationPrompt_PrefersConsolidationOverAutoSummary()
    {
        var handoff = new SessionHandoff
        {
            Task = "Write tests",
            Status = "incomplete",
            StepsCompleted = 5,
            MaxSteps = 30,
            TokensUsed = 50000,
            Summary = "Completed 5 steps. Tools used: read_file, grep_search.",
            ConsolidationSummary = "Found HandoffGenerator in SessionHandoff.cs (303 lines). Gaps: truncation, edge cases, malformed data.",
        };

        var prompt = HandoffGenerator.BuildContinuationPrompt(handoff);

        // Consolidation should appear BEFORE auto-summary
        var consolidationIdx = prompt.IndexOf("Found HandoffGenerator", StringComparison.Ordinal);
        var autoIdx = prompt.IndexOf("Completed 5 steps", StringComparison.Ordinal);

        Assert.True(consolidationIdx >= 0, "Consolidation summary not found in prompt");
        Assert.True(autoIdx >= 0, "Auto-summary not found in prompt");
        Assert.True(consolidationIdx < autoIdx, "Consolidation should appear before auto-summary");
        Assert.Contains("Auto-extracted context:", prompt);
    }

    [Fact]
    public void BuildContinuationPrompt_WithoutConsolidation_UsesAutoSummary()
    {
        var handoff = new SessionHandoff
        {
            Task = "Write tests",
            Status = "incomplete",
            StepsCompleted = 5,
            MaxSteps = 30,
            TokensUsed = 50000,
            Summary = "Completed 5 steps. Tools used: read_file.",
        };

        var prompt = HandoffGenerator.BuildContinuationPrompt(handoff);

        Assert.Contains("Completed 5 steps", prompt);
        Assert.DoesNotContain("Auto-extracted context:", prompt);
    }

    // ── Todo-aware handoff tests ───────────────────────────────────────────

    [Fact]
    public void Generate_WithTodoPlanState_StoresIt()
    {
        var result = MakeResult(success: false, failureReason: "Stopped: maximum steps (5) reached.");
        var todoState = "  1. ✅ Read implementation (completed)\n  2. 🔄 Add method (in-progress)\n  3. ⬜ Add test (not-started)";

        var handoff = HandoffGenerator.Generate("Add feature", result, maxSteps: 5,
            todoPlanState: todoState);

        Assert.Equal(todoState, handoff.TodoPlanState);
    }

    [Fact]
    public void BuildContinuationPrompt_WithTodoPlanState_IncludesPlanSection()
    {
        var handoff = new SessionHandoff
        {
            Task = "Add feature",
            Status = "incomplete",
            StepsCompleted = 3,
            MaxSteps = 10,
            TokensUsed = 30000,
            Summary = "Completed 3 steps.",
            TodoPlanState = "  1. ✅ Read file (completed)\n  2. ⬜ Edit file (not-started)",
        };

        var prompt = HandoffGenerator.BuildContinuationPrompt(handoff);

        Assert.Contains("Plan state from previous session:", prompt);
        Assert.Contains("✅ Read file", prompt);
        Assert.Contains("⬜ Edit file", prompt);
    }

    [Fact]
    public void BuildContinuationPrompt_WithoutTodoPlanState_OmitsPlanSection()
    {
        var handoff = new SessionHandoff
        {
            Task = "Simple task",
            Status = "incomplete",
            StepsCompleted = 2,
            MaxSteps = 30,
            TokensUsed = 10000,
            Summary = "Completed 2 steps.",
        };

        var prompt = HandoffGenerator.BuildContinuationPrompt(handoff);

        Assert.DoesNotContain("Plan state from previous session:", prompt);
    }

    [Fact]
    public void Generate_LongConsolidationSummary_TruncatesToTwoThousandCharactersPlusEllipsis()
    {
        var result = MakeResult(success: false, failureReason: "Stopped: maximum steps (30) reached.");
        var consolidationSummary = new string('x', 2005);

        var handoff = HandoffGenerator.Generate("Continue task", result, maxSteps: 30, consolidationSummary: consolidationSummary);

        Assert.NotNull(handoff.ConsolidationSummary);
        Assert.Equal(2003, handoff.ConsolidationSummary!.Length);
        Assert.EndsWith("...", handoff.ConsolidationSummary);
    }

    [Fact]
    public void Generate_EmptyConsolidationSummary_SetsNull()
    {
        var result = MakeResult(success: false, failureReason: "Stopped: maximum steps (30) reached.");

        var handoff = HandoffGenerator.Generate("Continue task", result, maxSteps: 30, consolidationSummary: string.Empty);

        Assert.Null(handoff.ConsolidationSummary);
    }

    [Fact]
    public void BuildContinuationPrompt_WhenConsolidationMatchesSummary_OmitsAutoExtractedContext()
    {
        const string sharedSummary = "Located the failing branch and prepared the next edit.";
        var handoff = new SessionHandoff
        {
            Task = "Resume task",
            Status = "incomplete",
            StepsCompleted = 4,
            MaxSteps = 30,
            TokensUsed = 15000,
            Summary = sharedSummary,
            ConsolidationSummary = sharedSummary,
        };

        var prompt = HandoffGenerator.BuildContinuationPrompt(handoff);

        Assert.Contains(sharedSummary, prompt);
        Assert.Equal(1, CountOccurrences(prompt, sharedSummary));
        Assert.DoesNotContain("Auto-extracted context:", prompt);
    }

    [Fact]
    public void LoadFromSessionFile_MissingFile_ReturnsNull()
    {
        var missingPath = Path.Combine(_testDir, "does-not-exist.jsonl");

        var loaded = HandoffGenerator.LoadFromSessionFile(missingPath);

        Assert.Null(loaded);
    }

    [Fact]
    public void FromJson_CamelCaseJsonWithOmittedNulls_DeserializesSuccessfully()
    {
        const string json = """
            {
              "task": "Resume work",
              "status": "incomplete",
              "stepsCompleted": 2,
              "maxSteps": 30,
              "tokensUsed": 1200,
              "summary": "Need to continue",
              "filesModified": ["src/Auth.cs"],
              "failedApproaches": ["Step 1: edit failed — oldString not found in target file"],
              "nextSteps": ["Review the current state and continue the task"]
            }
            """;

        var handoff = HandoffGenerator.FromJson(json);

        Assert.NotNull(handoff);
        Assert.Equal("Resume work", handoff!.Task);
        Assert.Equal("incomplete", handoff.Status);
        Assert.Equal(["src/Auth.cs"], handoff.FilesModified);
        Assert.Null(handoff.LastTestOutput);
        Assert.Null(handoff.SessionLogPath);
        Assert.Null(handoff.ConsolidationSummary);
    }

    // ── Assumption handoff tests (Phase 5B) ─────────────────────────

    [Fact]
    public void Generate_WithAssumptions_StoresAssumptionsField()
    {
        var result = MakeResult(success: false, failureReason: "Stopped: maximum steps (30) reached.");
        var handoff = HandoffGenerator.Generate("Fix auth", result, maxSteps: 30,
            assumptionsText: "I'm assuming backward compat for existing tokens");

        Assert.Equal("I'm assuming backward compat for existing tokens", handoff.Assumptions);
    }

    [Fact]
    public void Generate_WithoutAssumptions_ExtractsFromEarlySteps()
    {
        var steps = new List<StepRecord>
        {
            new()
            {
                StepNumber = 0,
                Timestamp = DateTimeOffset.UtcNow,
                Thought = "Planning. I'm assuming the user wants to fix the implementation, not the test.",
                ToolCalls = [],
            },
            new()
            {
                StepNumber = 1,
                Timestamp = DateTimeOffset.UtcNow,
                Thought = "Let me read the file now.",
                ToolCalls = [],
            },
        };
        var result = MakeResult(success: false, failureReason: "Stopped: maximum steps (30) reached.",
            steps: steps);

        var handoff = HandoffGenerator.Generate("Fix auth", result, maxSteps: 30);

        Assert.NotNull(handoff.Assumptions);
        Assert.Contains("assuming", handoff.Assumptions!.ToLowerInvariant());
    }

    [Fact]
    public void Generate_NoAssumptionsAnywhere_AssumptionsFieldIsNull()
    {
        var steps = new List<StepRecord>
        {
            new()
            {
                StepNumber = 0,
                Timestamp = DateTimeOffset.UtcNow,
                Thought = "Let me read the file and fix the bug.",
                ToolCalls = [],
            },
        };
        var result = MakeResult(success: true, steps: steps);

        var handoff = HandoffGenerator.Generate("Fix auth", result, maxSteps: 30);

        Assert.Null(handoff.Assumptions);
    }

    [Fact]
    public void BuildContinuationPrompt_WithAssumptions_IncludesAssumptionsSection()
    {
        var handoff = new SessionHandoff
        {
            Task = "Fix auth",
            Status = "incomplete",
            StepsCompleted = 5,
            MaxSteps = 30,
            TokensUsed = 20000,
            Summary = "Explored codebase, started fix.",
            Assumptions = "I'm assuming backward compat for existing tokens. Fix impl, not test.",
        };

        var prompt = HandoffGenerator.BuildContinuationPrompt(handoff);

        Assert.Contains("Assumptions from previous session", prompt);
        Assert.Contains("backward compat", prompt);
    }

    [Fact]
    public void BuildContinuationPrompt_WithoutAssumptions_OmitsSection()
    {
        var handoff = new SessionHandoff
        {
            Task = "Simple task",
            Status = "incomplete",
            StepsCompleted = 2,
            MaxSteps = 30,
            TokensUsed = 10000,
            Summary = "Completed 2 steps.",
        };

        var prompt = HandoffGenerator.BuildContinuationPrompt(handoff);

        Assert.DoesNotContain("Assumptions from previous session", prompt);
    }

    // ── Pivot + Episode handoff tests (Phase 5C) ─────────────────────

    [Fact]
    public void Generate_WithPivotReasons_StoresThem()
    {
        var result = MakeResult(success: false, failureReason: "Stopped: maximum steps (30) reached.");
        var pivots = new List<string> { "[Step 5] Config approach won't work — values are runtime-computed" };
        var handoff = HandoffGenerator.Generate("Fix auth", result, maxSteps: 30, pivotReasons: pivots);

        Assert.NotNull(handoff.PivotReasons);
        Assert.Single(handoff.PivotReasons!);
        Assert.Contains("runtime-computed", handoff.PivotReasons![0]);
    }

    [Fact]
    public void Generate_ExtractsPivotReasonsFromSteps_WhenNotProvided()
    {
        var steps = new List<StepRecord>
        {
            new()
            {
                StepNumber = 0,
                Timestamp = DateTimeOffset.UtcNow,
                Thought = "Let me read the code first.",
                ToolCalls = [new ToolCallRecord { ToolName = "read_file", Arguments = "{}", ResultSummary = "OK", DurationMs = 50 }],
            },
            new()
            {
                StepNumber = 1,
                Timestamp = DateTimeOffset.UtcNow,
                Thought = "That approach won't work because the values are computed at runtime. Let me try a different approach.",
                ToolCalls = [new ToolCallRecord { ToolName = "read_file", Arguments = "{}", ResultSummary = "OK", DurationMs = 50 }],
            },
        };
        var result = MakeResult(success: false, failureReason: "Stopped: max steps.", steps: steps);

        var handoff = HandoffGenerator.Generate("Fix config", result, maxSteps: 30);

        Assert.NotNull(handoff.PivotReasons);
        Assert.Contains("won't work", handoff.PivotReasons![0].ToLowerInvariant());
    }

    [Fact]
    public void Generate_SegmentsEpisodes()
    {
        var steps = new List<StepRecord>
        {
            new()
            {
                StepNumber = 0,
                Timestamp = DateTimeOffset.UtcNow,
                ToolCalls = [new ToolCallRecord { ToolName = "grep_search", Arguments = "{}", ResultSummary = "3 matches", DurationMs = 50 }],
            },
            new()
            {
                StepNumber = 1,
                Timestamp = DateTimeOffset.UtcNow,
                ToolCalls = [new ToolCallRecord { ToolName = "read_file", Arguments = "{}", ResultSummary = "content", DurationMs = 50 }],
            },
            new()
            {
                StepNumber = 2,
                Timestamp = DateTimeOffset.UtcNow,
                ToolCalls = [new ToolCallRecord { ToolName = "replace_string_in_file", Arguments = "{}", ResultSummary = "OK", DurationMs = 50 }],
            },
            new()
            {
                StepNumber = 3,
                Timestamp = DateTimeOffset.UtcNow,
                ToolCalls = [new ToolCallRecord { ToolName = "run_tests", Arguments = "{}", ResultSummary = "Passed", DurationMs = 3000 }],
            },
        };
        var result = MakeResult(success: true, steps: steps);

        var handoff = HandoffGenerator.Generate("Fix bug", result, maxSteps: 30);

        Assert.NotNull(handoff.Episodes);
        Assert.True(handoff.Episodes!.Count >= 3); // explore, impl, verify
    }

    [Fact]
    public void BuildContinuationPrompt_WithPivots_IncludesTransitionSection()
    {
        var handoff = new SessionHandoff
        {
            Task = "Fix auth",
            Status = "incomplete",
            StepsCompleted = 10,
            MaxSteps = 30,
            TokensUsed = 50000,
            Summary = "Explored and started fix.",
            PivotReasons = ["[Step 5] Config-based approach won't work — values are runtime-computed"],
        };

        var prompt = HandoffGenerator.BuildContinuationPrompt(handoff);

        Assert.Contains("Approach transitions", prompt);
        Assert.Contains("runtime-computed", prompt);
    }

    [Fact]
    public void BuildContinuationPrompt_WithEpisodes_IncludesTrajectory()
    {
        var handoff = new SessionHandoff
        {
            Task = "Fix auth",
            Status = "incomplete",
            StepsCompleted = 8,
            MaxSteps = 30,
            TokensUsed = 40000,
            Summary = "In progress.",
            Episodes =
            [
                new EpisodeSummary { Type = "exploration", StartStep = 0, EndStep = 2, Outcome = "success" },
                new EpisodeSummary { Type = "implementation", StartStep = 3, EndStep = 5, Outcome = "failure" },
                new EpisodeSummary { Type = "implementation", StartStep = 6, EndStep = 7, Outcome = "success" },
            ],
        };

        var prompt = HandoffGenerator.BuildContinuationPrompt(handoff);

        Assert.Contains("Session trajectory", prompt);
        Assert.Contains("explore", prompt);
        Assert.Contains("impl", prompt);
        Assert.Contains("→", prompt);
    }

    [Fact]
    public void BuildContinuationPrompt_SingleEpisode_OmitsTrajectory()
    {
        var handoff = new SessionHandoff
        {
            Task = "Simple read",
            Status = "complete",
            StepsCompleted = 2,
            MaxSteps = 30,
            TokensUsed = 5000,
            Summary = "Done.",
            Episodes =
            [
                new EpisodeSummary { Type = "exploration", StartStep = 0, EndStep = 1, Outcome = "success" },
            ],
        };

        var prompt = HandoffGenerator.BuildContinuationPrompt(handoff);

        Assert.DoesNotContain("Session trajectory", prompt);
    }

    // ── Resume re-verification prompt tests (Phase 6B) ──────────────

    [Fact]
    public void BuildContinuationPrompt_ContainsTargetedVerificationGuidance()
    {
        // P4: resumed sessions should use grep_search for known files, not full re-reads
        var handoff = new SessionHandoff
        {
            Task = "Fix auth",
            Status = "incomplete",
            StepsCompleted = 5,
            MaxSteps = 30,
            TokensUsed = 50000,
            Summary = "Located code in AgentLoop.cs.",
        };

        var prompt = HandoffGenerator.BuildContinuationPrompt(handoff);

        Assert.Contains("grep_search", prompt);
        Assert.DoesNotContain("Verify the current file state with read_file before making edits", prompt);
    }

}
