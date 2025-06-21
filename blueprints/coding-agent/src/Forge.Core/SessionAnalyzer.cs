using System.Text.Json;

namespace Forge.Core;

/// <summary>
/// Offline analyzer for Forge session JSONL files.
/// Computes 6 efficiency metrics + feature detection from session logs.
///
/// Research basis:
///   - SWE-Effi (arXiv:2509.09853): effectiveness = accuracy × cost efficiency, token snowball effect
///   - PRInTS (arXiv:2511.19314): multi-dimensional step quality scoring
///   - FuseSearch (arXiv:2601.19568): read coalescence rate (redundant invocation tracking)
///   - HPCA 2026 (arXiv:2506.04301): per-step cost growth / diminishing returns
/// </summary>
public static class SessionAnalyzer
{
    /// <summary>
    /// Parse a session JSONL file and compute all metrics.
    /// </summary>
    public static SessionAnalysis? Analyze(string sessionFilePath)
    {
        if (!File.Exists(sessionFilePath)) return null;

        var steps = new List<ParsedStep>();
        string? task = null;
        string? model = null;
        bool? success = null;
        int totalPromptTokens = 0;
        int totalCompletionTokens = 0;

        foreach (var line in File.ReadLines(sessionFilePath))
        {
            try
            {
                using var doc = JsonDocument.Parse(line);
                var eventType = doc.RootElement.GetProperty("event").GetString();
                var data = doc.RootElement.GetProperty("data");

                switch (eventType)
                {
                    case "session_start":
                        task = data.TryGetProperty("task", out var t) ? t.GetString() : null;
                        model = data.TryGetProperty("model", out var m) ? m.GetString() : null;
                        // Resume detection: continuationContext is present in session_start
                        // when using --resume, but it's injected via AddUserMessage not logged here.
                        // Check if the task mentions "Continuing" as a heuristic fallback.
                        break;

                    case "step":
                        var step = ParseStep(data);
                        if (step is not null) steps.Add(step);
                        break;

                    case "session_end":
                        success = data.TryGetProperty("success", out var s) && s.GetBoolean();
                        totalPromptTokens = data.TryGetProperty("totalPromptTokens", out var tp) ? tp.GetInt32() : 0;
                        totalCompletionTokens = data.TryGetProperty("totalCompletionTokens", out var tc) ? tc.GetInt32() : 0;
                        break;
                }
            }
            catch { /* skip malformed lines */ }
        }

        if (steps.Count == 0) return null;

        // Load handoff for episode/pivot/assumption data
        var handoff = HandoffGenerator.LoadFromSessionFile(sessionFilePath);

        return new SessionAnalysis
        {
            FilePath = sessionFilePath,
            Task = task ?? "(unknown)",
            Model = model,
            Success = success ?? false,
            TotalSteps = steps.Count,
            TotalTokens = totalPromptTokens + totalCompletionTokens,

            // Metric 1: Steps/task (captured in TotalSteps)

            // Metric 2: Tokens/step growth (SWE-Effi "token snowball")
            TokensPerStepGrowth = ComputeTokenGrowth(steps),

            // Metric 3: Read coalescence rate (FuseSearch)
            ReadCoalescenceRate = ComputeReadCoalescence(steps),

            // Metric 4: Verification compliance
            VerificationCompliance = ComputeVerificationCompliance(steps),

            // Metric 5: Consolidation capture rate
            ConsolidationCaptured = handoff?.ConsolidationSummary is { Length: > 0 },
            SessionStatus = handoff?.Status ?? "unknown",

            // Episode data
            EpisodeCount = handoff?.Episodes?.Count ?? 0,
            PivotCount = handoff?.PivotReasons?.Count ?? 0,
            HasAssumptions = handoff?.Assumptions is { Length: > 0 },
            TrajectoryLine = steps.Count >= 3 ? EpisodeSegmenter.BuildTrajectoryLine(
                steps.Select(s => new StepRecord
                {
                    StepNumber = s.StepNumber,
                    Timestamp = DateTimeOffset.UtcNow,
                    ToolCalls = s.ToolCalls.Select(tc => new ToolCallRecord
                    {
                        ToolName = tc.ToolName,
                        Arguments = tc.Arguments,
                        ResultSummary = "",
                        IsError = tc.IsError,
                        DurationMs = 0,
                    }).ToList(),
                }).ToList()) : null,
        };
    }

    /// <summary>
    /// Format a human-readable report for a single session.
    /// </summary>
    public static string FormatReport(SessionAnalysis a)
    {
        var lines = new List<string>
        {
            $"Session: {Path.GetFileName(a.FilePath)}",
            $"  Task: {(a.Task.Length > 80 ? a.Task[..80] + "..." : a.Task)}",
            $"  Status: {a.SessionStatus} ({(a.Success ? "✅" : "❌")})",
            $"  Steps: {a.TotalSteps} | Tokens: {a.TotalTokens:N0}",
            "",
            "Metrics:",
            $"  Tokens/step growth: {a.TokensPerStepGrowth:P0} (step 0 → last step)",
            $"  Read coalescence: {a.ReadCoalescenceRate:P0} ({(a.ReadCoalescenceRate >= 0.75 ? "good" : a.ReadCoalescenceRate >= 0.5 ? "fair" : a.ReadCoalescenceRate < 0 ? "N/A" : "poor")})",
            $"  Verification: {a.VerificationCompliance}",
            $"  Consolidation: {(a.ConsolidationCaptured ? "✅ captured" : "❌ not captured")}",
        };

        if (a.EpisodeCount > 0 || a.PivotCount > 0 || a.HasAssumptions)
        {
            lines.Add("");
            lines.Add("Features:");
            if (a.EpisodeCount > 0) lines.Add($"  Episodes: {a.EpisodeCount}");
            if (a.PivotCount > 0) lines.Add($"  Pivots: {a.PivotCount}");
            if (a.HasAssumptions) lines.Add($"  Assumptions: stated");
            if (a.TrajectoryLine is not null) lines.Add($"  Trajectory: {a.TrajectoryLine}");
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Format an aggregated summary across multiple sessions.
    /// </summary>
    public static string FormatAggregate(IReadOnlyList<SessionAnalysis> sessions)
    {
        if (sessions.Count == 0) return "No sessions to analyze.";

        var successful = sessions.Count(s => s.Success);
        var avgSteps = sessions.Average(s => s.TotalSteps);
        var avgTokens = sessions.Average(s => s.TotalTokens);
        var sessionsWithReads = sessions.Where(s => s.ReadCoalescenceRate >= 0).ToList();
        var avgCoalescence = sessionsWithReads.Count > 0
            ? sessionsWithReads.Average(s => s.ReadCoalescenceRate)
            : 0.0;
        var incomplete = sessions.Where(s => s.SessionStatus == "incomplete").ToList();
        var consolidationRate = incomplete.Count > 0
            ? (double)incomplete.Count(s => s.ConsolidationCaptured) / incomplete.Count
            : 1.0;
        var avgGrowth = sessions.Where(s => s.TokensPerStepGrowth > 0).DefaultIfEmpty()
            .Average(s => s?.TokensPerStepGrowth ?? 0);

        return string.Join("\n",
            $"Aggregate Summary ({sessions.Count} sessions):",
            $"  Resolve rate: {successful}/{sessions.Count} ({(double)successful / sessions.Count:P0})",
            $"  Avg steps/task: {avgSteps:F1}",
            $"  Avg tokens/task: {avgTokens:N0}",
            $"  Avg read coalescence: {avgCoalescence:P0}",
            $"  Avg token/step growth: {avgGrowth:P0}",
            $"  Consolidation capture: {consolidationRate:P0} (of {incomplete.Count} incomplete sessions)",
            $"  Sessions with pivots: {sessions.Count(s => s.PivotCount > 0)}",
            $"  Sessions with assumptions: {sessions.Count(s => s.HasAssumptions)}"
        );
    }

    // ── Metric computations ──────────────────────────────────────────

    private static double ComputeTokenGrowth(List<ParsedStep> steps)
    {
        if (steps.Count < 2) return 0;
        var first = steps[0].PromptTokens;
        var last = steps[^1].PromptTokens;
        if (first <= 0) return 0;
        return (double)(last - first) / first;
    }

    private static double ComputeReadCoalescence(List<ParsedStep> steps)
    {
        var allReads = steps
            .SelectMany(s => s.ToolCalls)
            .Where(tc => tc.ToolName == "read_file" && !tc.IsError)
            .ToList();

        if (allReads.Count == 0) return -1.0; // No reads = undefined (excluded from aggregation)

        var uniqueFiles = allReads
            .Select(tc => ExtractFilePath(tc.Arguments))
            .Where(p => p is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        return (double)uniqueFiles / allReads.Count;
    }

    internal static string ComputeVerificationCompliance(List<ParsedStep> steps)
    {
        var edits = 0;
        var verified = 0;

        for (int i = 0; i < steps.Count; i++)
        {
            var hasEdit = steps[i].ToolCalls.Any(tc =>
                !tc.IsError && tc.ToolName is "replace_string_in_file" or "create_file");

            if (!hasEdit) continue;
            edits++;

            // Check if read_file follows within 2 steps
            for (int j = i + 1; j <= Math.Min(i + 2, steps.Count - 1); j++)
            {
                if (steps[j].ToolCalls.Any(tc => tc.ToolName == "read_file" && !tc.IsError))
                {
                    verified++;
                    break;
                }
            }
        }

        if (edits == 0) return "N/A (no edits)";
        return $"{verified}/{edits} ({(double)verified / edits:P0})";
    }

    // ── Parsing helpers ──────────────────────────────────────────────

    private static ParsedStep? ParseStep(JsonElement data)
    {
        try
        {
            var stepNum = data.GetProperty("stepNumber").GetInt32();
            var promptTokens = data.TryGetProperty("promptTokens", out var pt) ? pt.GetInt32() : 0;
            var thought = data.TryGetProperty("thought", out var th) ? th.GetString() : null;

            var toolCalls = new List<ParsedToolCall>();
            if (data.TryGetProperty("toolCalls", out var tcs) && tcs.ValueKind == JsonValueKind.Array)
            {
                foreach (var tc in tcs.EnumerateArray())
                {
                    toolCalls.Add(new ParsedToolCall
                    {
                        ToolName = tc.GetProperty("toolName").GetString() ?? "unknown",
                        Arguments = tc.TryGetProperty("arguments", out var args) ? args.GetString() ?? "{}" : "{}",
                        IsError = tc.TryGetProperty("isError", out var ie) && ie.GetBoolean(),
                    });
                }
            }

            return new ParsedStep
            {
                StepNumber = stepNum,
                PromptTokens = promptTokens,
                Thought = thought,
                ToolCalls = toolCalls,
            };
        }
        catch { return null; }
    }

    private static string? ExtractFilePath(string argsJson) =>
        EpisodeSegmenter.ExtractFilePath(argsJson);
}

// ── Data types ──────────────────────────────────────────────────────

public sealed record ParsedStep
{
    public required int StepNumber { get; init; }
    public int PromptTokens { get; init; }
    public string? Thought { get; init; }
    public IReadOnlyList<ParsedToolCall> ToolCalls { get; init; } = [];
}

public sealed record ParsedToolCall
{
    public required string ToolName { get; init; }
    public string Arguments { get; init; } = "{}";
    public bool IsError { get; init; }
}

public sealed record SessionAnalysis
{
    public required string FilePath { get; init; }
    public required string Task { get; init; }
    public string? Model { get; init; }
    public bool Success { get; init; }
    public int TotalSteps { get; init; }
    public int TotalTokens { get; init; }

    // Metrics
    public double TokensPerStepGrowth { get; init; }
    public double ReadCoalescenceRate { get; init; }
    public string VerificationCompliance { get; init; } = "N/A";
    public bool ConsolidationCaptured { get; init; }
    public string SessionStatus { get; init; } = "unknown";

    // Feature presence
    public int EpisodeCount { get; init; }
    public int PivotCount { get; init; }
    public bool HasAssumptions { get; init; }
    public string? TrajectoryLine { get; init; }
}
