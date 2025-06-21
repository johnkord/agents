namespace Forge.Core;

/// <summary>
/// Segments a session's step records into coherent episodes based on tool call patterns.
///
/// An episode is a contiguous sequence of steps with a coherent activity type.
/// Episode boundaries are detected heuristically from tool type transitions.
/// Pivot reasons and failure context are captured separately by AgentLoop during
/// execution (not reconstructed here).
///
/// Research basis:
///   - Steve-Evolving (arXiv:2603.13131, Mar 2026): structured experience tuples with fixed schema
///   - Nemori (arXiv:2508.03341, Aug 2025): Event Segmentation Theory — boundaries at context shifts
///   - SAMULE (arXiv:2509.20562, EMNLP 2025): meso-level trajectory for intra-task learning
///   - SeaView (arXiv:2504.08696, Apr 2025): step categorization for trajectory comprehension
///
/// The agent already has 10 episode-like signals scattered across 4 files (AgentLoop,
/// LlmClient, VerificationTracker, VerificationState). This class unifies them into
/// structured episode chains for handoffs and lessons.
/// </summary>
public static class EpisodeSegmenter
{
    private static readonly HashSet<string> ExplorationTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "read_file", "grep_search", "file_search", "list_directory", "semantic_search",
        "get_project_setup_info",
    };

    private static readonly HashSet<string> ImplementationTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "replace_string_in_file", "create_file", "create_directory",
    };

    private static readonly HashSet<string> VerificationTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "run_tests", "get_errors",
    };

    /// <summary>
    /// Segments an ordered sequence of step records into contiguous episodes that share the same
    /// dominant activity type.
    /// </summary>
    /// <param name="steps">
    /// The step records to analyze, in execution order.
    /// </param>
    /// <returns>
    /// A read-only list of <see cref="EpisodeSummary"/> values describing the detected episode
    /// boundaries, activity type, outcome, and files involved. Returns an empty list when
    /// <paramref name="steps"/> is empty.
    /// </returns>
    public static IReadOnlyList<EpisodeSummary> Segment(IReadOnlyList<StepRecord> steps)
    {
        if (steps.Count == 0) return [];

        var episodes = new List<EpisodeSummary>();
        var currentType = ClassifyStep(steps[0]);
        var currentStart = 0;
        var currentHasErrors = steps[0].ToolCalls.Any(tc => tc.IsError);
        var currentFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectFiles(steps[0], currentFiles);

        for (int i = 1; i < steps.Count; i++)
        {
            var stepType = ClassifyStep(steps[i]);
            var stepHasErrors = steps[i].ToolCalls.Any(tc => tc.IsError);

            // Episode boundary: type changed
            if (stepType != currentType)
            {
                episodes.Add(BuildEpisode(currentType, currentStart, i - 1, currentHasErrors, currentFiles));
                currentType = stepType;
                currentStart = i;
                currentHasErrors = stepHasErrors;
                currentFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                CollectFiles(steps[i], currentFiles);
            }
            else
            {
                currentHasErrors |= stepHasErrors;
                CollectFiles(steps[i], currentFiles);
            }
        }

        // Final episode
        episodes.Add(BuildEpisode(currentType, currentStart, steps.Count - 1, currentHasErrors, currentFiles));

        return episodes;
    }

    /// <summary>
    /// Builds a compact, human-readable trajectory string that summarizes the episode transitions
    /// found in a sequence of step records.
    /// </summary>
    /// <param name="steps">
    /// The step records to summarize, in execution order.
    /// </param>
    /// <returns>
    /// A trajectory string such as <c>"explore(0-3) → impl(4-7,FAIL) → verify(8-9)"</c> when
    /// the input contains multiple meaningful episodes; otherwise, <see langword="null"/> for
    /// sessions that are too short or do not contain any episode transitions.
    /// </returns>
    public static string? BuildTrajectoryLine(IReadOnlyList<StepRecord> steps)
    {
        if (steps.Count < 3) return null; // Too short for meaningful episodes

        var episodes = Segment(steps);
        if (episodes.Count <= 1) return null; // Single episode = no transitions

        var parts = episodes.Select(e =>
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

        return string.Join(" → ", parts);
    }

    /// <summary>
    /// Classify a step by its dominant tool type.
    /// </summary>
    internal static string ClassifyStep(StepRecord step)
    {
        // No tool calls = planning/reasoning step
        if (step.ToolCalls.Count == 0) return "planning";

        var successfulTools = step.ToolCalls.Where(tc => !tc.IsError).ToList();
        if (successfulTools.Count == 0)
        {
            // All failed — classify by what was attempted
            var attempted = step.ToolCalls.Select(tc => tc.ToolName).ToList();
            if (attempted.Any(t => ImplementationTools.Contains(t))) return "implementation";
            if (attempted.Any(t => VerificationTools.Contains(t))) return "verification";
            return "exploration";
        }

        // Classify by highest-priority tool present (implementation > verification > exploration)
        if (successfulTools.Any(tc => ImplementationTools.Contains(tc.ToolName)))
            return "implementation";
        if (successfulTools.Any(tc => VerificationTools.Contains(tc.ToolName)))
            return "verification";
        if (successfulTools.Any(tc => tc.ToolName is "run_bash_command"))
        {
            // run_bash_command could be build (verification) or install/other
            var hasBuild = successfulTools.Any(tc =>
                tc.ToolName is "run_bash_command"
                && tc.Arguments.Contains("build", StringComparison.OrdinalIgnoreCase));
            return hasBuild ? "verification" : "exploration";
        }

        return "exploration";
    }

    private static EpisodeSummary BuildEpisode(
        string type, int startStep, int endStep, bool hadErrors,
        HashSet<string> files)
    {
        return new EpisodeSummary
        {
            Type = type,
            StartStep = startStep,
            EndStep = endStep,
            Outcome = hadErrors ? "failure" : "success",
            FilesInvolved = files.Take(5).ToList(), // Cap to prevent bloat
        };
    }

    private static void CollectFiles(StepRecord step, HashSet<string> files)
    {
        foreach (var tc in step.ToolCalls)
        {
            if (tc.IsError) continue;
            var path = ExtractFilePath(tc.Arguments);
            if (path is not null)
                files.Add(path);
        }
    }

    /// <summary>
    /// Extract a file path from tool call arguments JSON.
    /// Checks common parameter names: filePath, path, rootPath.
    /// Shared by AgentLoop, SessionAnalyzer, and EpisodeSegmenter.
    /// </summary>
    internal static string? ExtractFilePath(string argsJson)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(argsJson);
            foreach (var key in (ReadOnlySpan<string>)["filePath", "path", "rootPath"])
            {
                if (doc.RootElement.TryGetProperty(key, out var val) &&
                    val.ValueKind == System.Text.Json.JsonValueKind.String)
                    return val.GetString();
            }
        }
        catch { }
        return null;
    }
}

/// <summary>
/// A contiguous sequence of steps with a coherent activity type.
/// </summary>
public sealed record EpisodeSummary
{
    /// <summary>Episode activity type: "exploration", "implementation", "verification", "planning".</summary>
    public required string Type { get; init; }

    /// <summary>First step number in this episode (inclusive).</summary>
    public required int StartStep { get; init; }

    /// <summary>Last step number in this episode (inclusive).</summary>
    public required int EndStep { get; init; }

    /// <summary>Episode outcome: "success" or "failure".</summary>
    public required string Outcome { get; init; }

    /// <summary>Files touched during this episode (capped at 5).</summary>
    public IReadOnlyList<string> FilesInvolved { get; init; } = [];
}
