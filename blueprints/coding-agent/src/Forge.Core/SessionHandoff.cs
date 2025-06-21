using System.Text.Json;
using System.Text.Json.Serialization;

namespace Forge.Core;

/// <summary>
/// Structured session handoff note for resuming interrupted sessions.
///
/// Research basis (research-review §8):
///   - AriadneMem: transition history (failed approaches + reasons) prevents retry of dead ends
///   - BAO: proactive consolidation before hitting limits
///   - Auton: episodic insight extraction maximizes mutual information with future tasks
///   - CaveAgent: the filesystem IS the checkpoint; handoff preserves intent, not raw state
/// </summary>
public sealed record SessionHandoff
{
    /// <summary>
    /// The task the session was working on.
    /// </summary>
    public required string Task { get; init; }

    /// <summary>
    /// The overall completion status for the session handoff.
    /// </summary>
    public required string Status { get; init; } // "complete", "incomplete", "failed"

    /// <summary>
    /// The number of steps completed before the session ended.
    /// </summary>
    public required int StepsCompleted { get; init; }

    /// <summary>
    /// The maximum number of steps allowed for the session.
    /// </summary>
    public required int MaxSteps { get; init; }

    /// <summary>
    /// The total number of tokens used across the session.
    /// </summary>
    public required int TokensUsed { get; init; }

    /// <summary>
    /// A summary of the session's progress and current state.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// The files modified during the session.
    /// </summary>
    public IReadOnlyList<string> FilesModified { get; init; } = [];

    /// <summary>
    /// Previously attempted approaches that failed.
    /// </summary>
    public IReadOnlyList<string> FailedApproaches { get; init; } = [];

    /// <summary>
    /// The most recent test output captured during the session.
    /// </summary>
    public string? LastTestOutput { get; init; }

    /// <summary>
    /// Suggested next actions for resuming the work.
    /// </summary>
    public IReadOnlyList<string> NextSteps { get; init; } = [];

    /// <summary>
    /// The path to the persisted session log, if available.
    /// </summary>
    public string? SessionLogPath { get; init; }
    /// <summary>
    /// The agent's own consolidation summary, captured when the boundary warning fires.
    /// This is the agent's self-assessment of what it accomplished and what remains.
    /// When available, this is higher-quality than the auto-generated Summary because
    /// the agent knows what it intended to do, not just what tools it called.
    /// Research basis: L2MAC (agent manages its own knowledge store), CaveAgent (agent writes its own state).
    /// </summary>
    public string? ConsolidationSummary { get; init; }
    /// <summary>
    /// Structured plan state from the agent's todo list, if it used manage_todos.
    /// Persisted separately from the conversation — survives context compression.
    /// On resume, gives the agent a concrete plan with completion status.
    /// Research basis: CaveAgent (persistent state +10.5%), SWE-Adept (step-indexed working memory).
    /// </summary>
    public string? TodoPlanState { get; init; }

    /// <summary>
    /// Assumptions the agent stated about task interpretation during planning.
    /// Captured from early steps when the agent explicitly states its chosen interpretation.
    /// On resume, lets the successor session know what interpretation was chosen (and challenge it).
    /// Research basis: Ambig-SWE (ICLR 2026) — stated assumptions recover 74% of underspecification loss.
    /// </summary>
    public string? Assumptions { get; init; }

    /// <summary>
    /// Approach transitions captured during execution — WHY the agent changed direction.
    /// Each entry is "[Step N] reason text" captured at the moment of pivot.
    /// Prevents resumed sessions from retrying approaches that were already abandoned.
    /// Research basis: AriadneMem (transition history), SAMULE meso-level, Nemori (event boundaries).
    /// </summary>
    public IReadOnlyList<string>? PivotReasons { get; init; }

    /// <summary>
    /// Structured episode chain segmented from step records post-hoc.
    /// Shows the session's narrative arc: explore → implement → verify → pivot → etc.
    /// Research basis: Steve-Evolving (structured experience tuples), SeaView (step categorization).
    /// </summary>
    public IReadOnlyList<EpisodeSummary>? Episodes { get; init; }
}

/// <summary>
/// Generates structured handoff notes from session data.
/// </summary>
public static class HandoffGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Generate a handoff note from completed session data.
    /// </summary>
    public static SessionHandoff Generate(string task, AgentResult result, int maxSteps,
        string? consolidationSummary = null, string? todoPlanState = null,
        string? assumptionsText = null, IReadOnlyList<string>? pivotReasons = null)
    {
        var status = result.Success ? "complete"
            : result.FailureReason?.Contains("maximum steps", StringComparison.OrdinalIgnoreCase) == true ? "incomplete"
            : result.FailureReason?.Contains("Maximum tokens", StringComparison.OrdinalIgnoreCase) == true ? "incomplete"
            : "failed";

        return new SessionHandoff
        {
            Task = task,
            Status = status,
            StepsCompleted = result.Steps.Count,
            MaxSteps = maxSteps,
            TokensUsed = result.TotalPromptTokens + result.TotalCompletionTokens,
            Summary = ExtractSummary(result),
            FilesModified = ExtractModifiedFiles(result),
            FailedApproaches = ExtractFailedApproaches(result),
            LastTestOutput = ExtractLastTestOutput(result),
            NextSteps = ExtractNextSteps(result),
            SessionLogPath = result.SessionLogPath,
            ConsolidationSummary = consolidationSummary is { Length: > 0 }
                ? (consolidationSummary.Length > 2000 ? consolidationSummary[..2000] + "..." : consolidationSummary)
                : null,
            TodoPlanState = todoPlanState,
            Assumptions = assumptionsText ?? ExtractAssumptions(result),
            PivotReasons = pivotReasons is { Count: > 0 } ? pivotReasons : ExtractPivotReasons(result),
            Episodes = EpisodeSegmenter.Segment(result.Steps),
        };
    }

    /// <summary>
    /// Build a continuation prompt from a handoff note for injecting into a new session.
    /// </summary>
    public static string BuildContinuationPrompt(SessionHandoff handoff)
    {
        var lines = new List<string>
        {
            "Continuing a previous session. Here is where things stand:",
            "",
            $"Task: {handoff.Task}",
            $"Status: {handoff.Status} ({handoff.StepsCompleted}/{handoff.MaxSteps} steps, {handoff.TokensUsed:N0} tokens used)",
            "",
            "Progress so far:",
        };

        // Prefer the agent's own consolidation summary (higher quality: captures intent
        // and discovered knowledge) over the auto-generated tool-usage summary.
        // Research basis: L2MAC (agent manages its own file store), CaveAgent (agent writes state).
        if (handoff.ConsolidationSummary is { Length: > 0 })
        {
            lines.Add(handoff.ConsolidationSummary);
            // Also include the auto-summary as supplementary context
            if (handoff.Summary.Length > 0 && handoff.Summary != handoff.ConsolidationSummary)
            {
                lines.Add("");
                lines.Add("Auto-extracted context:");
                lines.Add(handoff.Summary);
            }
        }
        else
        {
            lines.Add(handoff.Summary);
        }

        // Assumptions come early in the prompt so the resumed session understands
        // what interpretation was chosen BEFORE seeing the file changes based on it.
        if (handoff.Assumptions is { Length: > 0 })
        {
            lines.Add("");
            lines.Add("Assumptions from previous session (verify these still apply):");
            lines.Add($"  {handoff.Assumptions}");
        }

        if (handoff.FilesModified.Count > 0)
        {
            lines.Add("");
            lines.Add($"Files modified: {string.Join(", ", handoff.FilesModified)}");
        }

        if (handoff.TodoPlanState is { Length: > 0 })
        {
            lines.Add("");
            lines.Add("Plan state from previous session:");
            lines.Add(handoff.TodoPlanState);
        }

        if (handoff.FailedApproaches.Count > 0)
        {
            lines.Add("");
            lines.Add("Approaches that were tried and failed (do NOT retry these):");
            foreach (var approach in handoff.FailedApproaches)
                lines.Add($"  - {approach}");
        }

        if (handoff.LastTestOutput is not null)
        {
            lines.Add("");
            lines.Add($"Last test output: {handoff.LastTestOutput}");
        }

        if (handoff.NextSteps.Count > 0)
        {
            lines.Add("");
            lines.Add("Suggested next steps:");
            for (int i = 0; i < handoff.NextSteps.Count; i++)
                lines.Add($"  {i + 1}. {handoff.NextSteps[i]}");
        }

        if (handoff.PivotReasons is { Count: > 0 })
        {
            lines.Add("");
            lines.Add("Approach transitions (why previous session changed direction):");
            foreach (var reason in handoff.PivotReasons)
                lines.Add($"  - {reason}");
        }

        if (handoff.Episodes is { Count: > 1 })
        {
            var trajectory = EpisodeSegmenter.BuildTrajectoryLine(
                Enumerable.Range(0, handoff.StepsCompleted)
                    .Select(i => new StepRecord { StepNumber = i, Timestamp = default })
                    .ToList());
            // Use the pre-computed episodes to build trajectory inline
            var parts = handoff.Episodes.Select(e =>
            {
                var range = e.StartStep == e.EndStep ? $"{e.StartStep}" : $"{e.StartStep}-{e.EndStep}";
                var outcome = e.Outcome == "failure" ? ",FAIL" : "";
                var typeShort = e.Type switch
                {
                    "exploration" => "explore",
                    "implementation" => "impl",
                    "verification" => "verify",
                    "planning" => "plan",
                    _ => e.Type,
                };
                return $"{typeShort}({range}{outcome})";
            });
            lines.Add("");
            lines.Add($"Session trajectory: {string.Join(" → ", parts)}");
        }

        lines.Add("");
        lines.Add("IMPORTANT: Before editing, verify the current file state. For files mentioned above, "
            + "a targeted grep_search confirming key function/class names still exist is sufficient. "
            + "Full re-reads are only needed if the structure has changed.");

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Serialize a handoff note to JSON.
    /// </summary>
    public static string ToJson(SessionHandoff handoff) =>
        JsonSerializer.Serialize(handoff, JsonOptions);

    /// <summary>
    /// Deserialize a handoff note from JSON.
    /// </summary>
    public static SessionHandoff? FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<SessionHandoff>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Load a handoff note from a session JSONL file (finds the session_handoff event).
    /// </summary>
    public static SessionHandoff? LoadFromSessionFile(string sessionFilePath)
    {
        if (!File.Exists(sessionFilePath))
            return null;

        // Read lines in reverse to find the handoff event (usually near the end)
        var lines = File.ReadAllLines(sessionFilePath);
        for (int i = lines.Length - 1; i >= 0; i--)
        {
            if (lines[i].Contains("\"session_handoff\""))
            {
                try
                {
                    using var doc = JsonDocument.Parse(lines[i]);
                    var dataElement = doc.RootElement.GetProperty("data");
                    return JsonSerializer.Deserialize<SessionHandoff>(
                        dataElement.GetRawText(), JsonOptions);
                }
                catch { }
            }
        }

        return null;
    }

    // ── Extraction helpers ─────────────────────────────────────────────────

    private static string ExtractSummary(AgentResult result)
    {
        // Use the agent's final output as the summary (it contains the REPORT)
        if (result.Success && result.Output.Length > 0)
        {
            var summary = result.Output;
            return summary.Length > 1000 ? summary[..1000] + "..." : summary;
        }

        // For failures/incomplete, build a summary from the step history
        // Include discovery context so resumed sessions don't re-explore
        var toolCalls = result.Steps
            .SelectMany(s => s.ToolCalls)
            .Where(tc => !tc.IsError)
            .Select(tc => $"{tc.ToolName}")
            .Distinct()
            .ToList();

        var errorCalls = result.Steps
            .SelectMany(s => s.ToolCalls)
            .Where(tc => tc.IsError)
            .Select(tc => $"{tc.ToolName}: {Truncate(tc.ResultSummary, 100)}")
            .Distinct()
            .Take(3)
            .ToList();

        var lines = new List<string>();
        lines.Add($"Completed {result.Steps.Count} steps. Tools used: {string.Join(", ", toolCalls.Take(8))}.");
        if (errorCalls.Count > 0)
            lines.Add($"Errors: {string.Join("; ", errorCalls)}");

        // Append discovery context — what was LEARNED, not just what tools ran
        var discoveries = ExtractDiscoveryContext(result);
        if (discoveries.Count > 0)
        {
            lines.Add("");
            lines.Add("Key discoveries:");
            foreach (var discovery in discoveries)
                lines.Add($"  - {discovery}");
        }

        var combined = string.Join("\n", lines);
        return combined.Length > 1500 ? combined[..1500] + "..." : combined;
    }

    /// <summary>
    /// Extract what was DISCOVERED during the session — file locations, class structures,
    /// test results, code patterns — so that a resumed session starts with knowledge
    /// rather than repeating exploration.
    ///
    /// Research basis:
    ///   - AriadneMem: Transition history preserves "tried A → failed → found B" narratives
    ///   - TraceMem: Episodic → semantic distillation captures facts from interactions
    ///   - SWE-ContextBench: Filtered experience improves accuracy; unfiltered hurts
    ///   - M2A: Separate raw log from extracted observations
    /// </summary>
    internal static IReadOnlyList<string> ExtractDiscoveryContext(AgentResult result)
    {
        var discoveries = new List<string>();
        var filesRead = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var filesSearched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var step in result.Steps)
        {
            foreach (var tc in step.ToolCalls)
            {
                if (tc.IsError) continue;

                switch (tc.ToolName)
                {
                    case "grep_search":
                        // Extract what was found via search
                        var grepResult = tc.ResultSummary;
                        if (grepResult.Contains("match", StringComparison.OrdinalIgnoreCase))
                        {
                            // Extract file paths from grep results
                            // Format: "N match(es):\npath/file.cs:line: content"
                            // Skip the header line (contains "match(es):")
                            var matchFiles = grepResult.Split('\n')
                                .Skip(1) // skip "N match(es):" header
                                .Where(l => l.Contains(':') && l.Contains('/'))
                                .Select(l => l.Split(':')[0].Trim())
                                .Where(p => p.Length > 0 && p.Contains('.'))
                                .Distinct()
                                .Take(5)
                                .ToList();

                            if (matchFiles.Count > 0)
                            {
                                var query = ExtractArgValue(tc.Arguments, "query") ?? "?";
                                var uniqueFiles = matchFiles.Where(f => filesSearched.Add(f)).ToList();
                                if (uniqueFiles.Count > 0)
                                    discoveries.Add($"grep '{query}' found matches in: {string.Join(", ", uniqueFiles)}");
                            }
                        }
                        break;

                    case "read_file":
                        var filePath = ExtractPathFromArgs(tc.Arguments);
                        if (filePath is not null && filesRead.Add(filePath))
                        {
                            // Try to extract total line count from "Lines X-Y of Z:" format
                            var linesInfo = ExtractTotalLines(tc.ResultSummary);
                            discoveries.Add(linesInfo is not null
                                ? $"Read {filePath} ({linesInfo} lines)"
                                : $"Read {filePath}");
                        }
                        break;

                    case "run_tests":
                        // Capture test results — critical for understanding where we left off
                        if (tc.ResultSummary.Contains("Passed", StringComparison.OrdinalIgnoreCase) ||
                            tc.ResultSummary.Contains("Failed", StringComparison.OrdinalIgnoreCase))
                        {
                            discoveries.Add($"Tests: {Truncate(tc.ResultSummary, 150)}");
                        }
                        break;

                    case "file_search":
                        if (tc.ResultSummary.Contains("file(s) found", StringComparison.OrdinalIgnoreCase))
                        {
                            var query = ExtractArgValue(tc.Arguments, "query") ?? "?";
                            discoveries.Add($"file_search '{query}': {Truncate(tc.ResultSummary.Split('\n')[0], 100)}");
                        }
                        break;
                }
            }
        }

        // Cap to prevent bloating the handoff note
        return discoveries.Take(15).ToList();
    }

    private static string? ExtractArgValue(string argsJson, string key)
    {
        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            if (doc.RootElement.TryGetProperty(key, out var val) &&
                val.ValueKind == JsonValueKind.String)
                return val.GetString();
        }
        catch { }
        return null;
    }

    /// <summary>
    /// Extract total line count from read_file result summary format: "Lines X-Y of Z:"
    /// Returns the total "Z" as a string, or null if the format doesn't match.
    /// </summary>
    private static string? ExtractTotalLines(string resultSummary)
    {
        // Format: "Lines 1-303 of 303:\n..."
        var match = System.Text.RegularExpressions.Regex.Match(
            resultSummary, @"Lines \d+-\d+ of (\d+)");
        return match.Success ? match.Groups[1].Value : null;
    }

    internal static IReadOnlyList<string> ExtractModifiedFiles(AgentResult result)
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var step in result.Steps)
        {
            foreach (var tc in step.ToolCalls)
            {
                if (tc.ToolName is "replace_string_in_file" or "create_file" or "create_directory" && !tc.IsError)
                {
                    // Extract file path from arguments
                    var path = ExtractPathFromArgs(tc.Arguments);
                    if (path is not null)
                        files.Add(path);
                }
            }
        }
        return files.ToList();
    }

    internal static IReadOnlyList<string> ExtractFailedApproaches(AgentResult result)
    {
        var approaches = new List<string>();
        foreach (var step in result.Steps)
        {
            foreach (var tc in step.ToolCalls)
            {
                if (tc.IsError && tc.ToolName is "replace_string_in_file")
                {
                    var summary = tc.ResultSummary;
                    if (summary.Contains("not found", StringComparison.OrdinalIgnoreCase))
                    {
                        approaches.Add($"Step {step.StepNumber}: edit failed — oldString not found in target file");
                    }
                }
            }
        }

        // Deduplicate and cap
        return approaches.Distinct().Take(5).ToList();
    }

    private static string? ExtractLastTestOutput(AgentResult result)
    {
        // Find the last run_tests call
        for (int i = result.Steps.Count - 1; i >= 0; i--)
        {
            var testCall = result.Steps[i].ToolCalls
                .LastOrDefault(tc => tc.ToolName is "run_tests" or "test_failure");

            if (testCall is not null)
            {
                return Truncate(testCall.ResultSummary, 300);
            }
        }
        return null;
    }

    private static IReadOnlyList<string> ExtractNextSteps(AgentResult result)
    {
        // For incomplete sessions, suggest continuing from where we left off
        if (result.Success) return [];

        var steps = new List<string>();

        // If there were test failures, suggest fixing them
        var lastTest = ExtractLastTestOutput(result);
        if (lastTest?.Contains("FAIL", StringComparison.OrdinalIgnoreCase) == true)
            steps.Add("Fix remaining test failures");

        // If there were compile errors, suggest fixing them
        var hasCompileErrors = result.Steps.Any(s =>
            s.ToolCalls.Any(tc => tc.IsError && tc.ResultSummary.Contains("build failed", StringComparison.OrdinalIgnoreCase)));
        if (hasCompileErrors)
            steps.Add("Fix compile errors before proceeding");

        // Generic continuation
        if (steps.Count == 0)
            steps.Add("Review the current state and continue the task");

        steps.Add("Run the full test suite to verify no regressions");

        return steps;
    }

    private static string? ExtractPathFromArgs(string argsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            foreach (var key in new[] { "filePath", "path" })
            {
                if (doc.RootElement.TryGetProperty(key, out var val) &&
                    val.ValueKind == JsonValueKind.String)
                    return val.GetString();
            }
        }
        catch { }
        return null;
    }

    private static string Truncate(string text, int maxLen) =>
        text.Length <= maxLen ? text : text[..maxLen] + "...";

    /// <summary>
    /// Extract assumption text from early session steps (0-2) as a fallback
    /// when the AgentLoop didn't capture assumptions at runtime.
    /// Scans the agent's thought text for assumption indicators.
    /// </summary>
    private static string? ExtractAssumptions(AgentResult result)
    {
        var earlySteps = result.Steps.Take(3);
        foreach (var step in earlySteps)
        {
            if (AgentLoop.ContainsAssumptionReasoning(step.Thought))
            {
                return AgentLoop.ExtractAssumptionText(step.Thought);
            }
        }
        return null;
    }

    /// <summary>
    /// Extract pivot reasons from step records as a fallback
    /// when the AgentLoop didn't capture them at runtime.
    /// Scans all steps for pivot language in the agent's thought text.
    /// </summary>
    private static IReadOnlyList<string>? ExtractPivotReasons(AgentResult result)
    {
        var pivots = new List<string>();
        foreach (var step in result.Steps)
        {
            if (AgentLoop.ContainsPivotReasoning(step.Thought))
            {
                var reason = AgentLoop.ExtractPivotReason(step.Thought);
                if (reason is not null)
                    pivots.Add($"[Step {step.StepNumber}] {reason}");
            }
        }
        return pivots.Count > 0 ? pivots : null;
    }
}
