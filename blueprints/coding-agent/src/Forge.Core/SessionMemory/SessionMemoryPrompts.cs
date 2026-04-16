using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Forge.Core.SessionMemory;

/// <summary>
/// Prompt template + response parser for the session-memory extraction call.
///
/// Two-phase structure:
/// <list type="bullet">
/// <item><c>&lt;analysis&gt;…&lt;/analysis&gt;</c> — the model thinks out loud (kept
/// only for debug logging; stripped before persistence).</item>
/// <item><c>&lt;summary&gt;{ …JSON… }&lt;/summary&gt;</c> — a structured JSON payload
/// we parse into <see cref="SessionMemorySnapshot"/>.</item>
/// </list>
///
/// JSON rather than freeform Markdown because the extractor is a small LLM call;
/// forcing it into a schema keeps the persisted snapshot consistent across runs.
/// The Markdown view is rendered from the parsed structure by
/// <see cref="SessionMemoryManager.RenderMarkdown"/>.
/// </summary>
public static class SessionMemoryPrompts
{
    public const string SystemPrompt = """
        You are a session-memory extractor embedded in a coding agent. Your only job is to
        summarize what has happened in the current session into a compact, structured
        memory that a continuation of this session could hydrate from after compaction.

        Rules:
        - Be concrete and action-oriented. Prefer "edited Foo.cs to add X" over "worked on Foo".
        - Preserve file paths, error messages, test names, and specific failure reasons verbatim.
        - Do NOT speculate. If a step is ambiguous, describe what was done, not what was intended.
        - Output a two-phase response:
            1. A <analysis>…</analysis> block where you think through the session.
            2. A <summary>…</summary> block containing ONLY a JSON object matching the schema below.
        - The JSON must be valid and parseable. No trailing commas, no comments.
        - Keep each list item under 200 characters.
        """;

    public const string ResponseSchema = """
        {
          "taskDescription": "string — the task in the agent's own words (≤ 400 chars)",
          "currentState": "string — where the agent is right now: phase, focus, blockers",
          "filesTouched": ["string — path + one-line purpose"],
          "errorsAndFixes": ["string — 'saw X → fixed by Y' entries"],
          "pending": ["string — outstanding sub-tasks"],
          "worklog": ["string — chronological one-liners covering the session"]
        }
        """;

    public static string BuildUserPrompt(SessionMemoryExtractionRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Extraction Request");
        sb.AppendLine();
        sb.AppendLine("## Task");
        sb.AppendLine(request.Task);
        sb.AppendLine();

        if (request.PreviousSummary is not null)
        {
            sb.AppendLine("## Previous Snapshot (merge & extend, do not discard)");
            sb.AppendLine();
            sb.AppendLine("```json");
            sb.AppendLine(JsonSerializer.Serialize(new
            {
                taskDescription = request.PreviousSummary.TaskDescription,
                currentState = request.PreviousSummary.CurrentState,
                filesTouched = request.PreviousSummary.FilesTouched,
                errorsAndFixes = request.PreviousSummary.ErrorsAndFixes,
                pending = request.PreviousSummary.Pending,
                worklog = request.PreviousSummary.Worklog,
            }, new JsonSerializerOptions { WriteIndented = true }));
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine($"The previous snapshot covered steps ≤ {request.PreviousSummary.LastExtractedStep}.");
            sb.AppendLine("Focus your new analysis on steps AFTER that watermark, and merge new findings with prior content.");
            sb.AppendLine();
        }

        var newSteps = request.FromStepIndex is int from
            ? request.Steps.Where(s => s.StepNumber > from).ToList()
            : request.Steps.ToList();

        sb.AppendLine($"## Session Steps ({newSteps.Count} since last summary)");
        sb.AppendLine();
        foreach (var step in newSteps)
        {
            sb.AppendLine($"### Step {step.StepNumber} — {step.Timestamp:HH:mm:ss}");
            if (!string.IsNullOrWhiteSpace(step.Thought))
            {
                var t = step.Thought.Length > 800 ? step.Thought[..800] + "…" : step.Thought;
                sb.AppendLine("Thought: " + t.Replace("\n", " "));
            }
            if (step.ToolCalls.Count > 0)
            {
                foreach (var tc in step.ToolCalls)
                {
                    var marker = tc.IsError ? "ERR" : "OK";
                    var args = tc.Arguments.Length > 160 ? tc.Arguments[..160] + "…" : tc.Arguments;
                    var result = tc.ResultSummary.Length > 300 ? tc.ResultSummary[..300] + "…" : tc.ResultSummary;
                    sb.AppendLine($"  [{marker}] {tc.ToolName}({args})");
                    sb.AppendLine($"    → {result.Replace("\n", " ")}");
                }
            }
            sb.AppendLine();
        }

        sb.AppendLine("## Required Output");
        sb.AppendLine();
        sb.AppendLine("Emit an <analysis> block then a <summary> block containing JSON matching:");
        sb.AppendLine();
        sb.AppendLine("```json");
        sb.AppendLine(ResponseSchema);
        sb.AppendLine("```");
        return sb.ToString();
    }

    /// <summary>
    /// Parse the LLM response into a snapshot. Looks for the first JSON object
    /// inside a <c>&lt;summary&gt;</c> block; falls back to the first balanced-brace
    /// JSON object in the text if the tags are missing.
    /// </summary>
    /// <exception cref="SessionMemoryParseException">No valid JSON payload found, or required fields missing.</exception>
    public static SessionMemorySnapshot ParseSnapshot(string response, int lastExtractedStep, int extractionCount, DateTimeOffset updatedAt)
    {
        if (string.IsNullOrWhiteSpace(response))
            throw new SessionMemoryParseException("Empty extractor response");

        var json = ExtractJson(response)
            ?? throw new SessionMemoryParseException("No JSON payload found in extractor response");

        SnapshotDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<SnapshotDto>(json, SnapshotJsonOptions);
        }
        catch (JsonException ex)
        {
            throw new SessionMemoryParseException($"Snapshot JSON was not parseable: {ex.Message}", ex);
        }
        if (dto is null)
            throw new SessionMemoryParseException("Snapshot JSON deserialized to null");
        if (string.IsNullOrWhiteSpace(dto.TaskDescription))
            throw new SessionMemoryParseException("Missing required field: taskDescription");
        if (string.IsNullOrWhiteSpace(dto.CurrentState))
            throw new SessionMemoryParseException("Missing required field: currentState");

        return new SessionMemorySnapshot
        {
            TaskDescription = dto.TaskDescription.Trim(),
            CurrentState = dto.CurrentState.Trim(),
            FilesTouched = Sanitize(dto.FilesTouched),
            ErrorsAndFixes = Sanitize(dto.ErrorsAndFixes),
            Pending = Sanitize(dto.Pending),
            Worklog = Sanitize(dto.Worklog),
            LastExtractedStep = lastExtractedStep,
            UpdatedAt = updatedAt,
            ExtractionCount = extractionCount,
        };
    }

    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static IReadOnlyList<string> Sanitize(IReadOnlyList<string>? items)
    {
        if (items is null) return [];
        return items
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToList();
    }

    private static string? ExtractJson(string response)
    {
        // Prefer the <summary>…</summary> block if present.
        var summaryStart = response.IndexOf("<summary>", StringComparison.OrdinalIgnoreCase);
        var summaryEnd = response.IndexOf("</summary>", StringComparison.OrdinalIgnoreCase);
        if (summaryStart >= 0 && summaryEnd > summaryStart)
        {
            var inner = response.Substring(summaryStart + "<summary>".Length,
                summaryEnd - summaryStart - "<summary>".Length);
            var json = FindBalancedJson(inner);
            if (json is not null) return json;
        }
        // Fallback: first balanced-brace object anywhere in the response.
        return FindBalancedJson(response);
    }

    private static string? FindBalancedJson(string text)
    {
        int start = text.IndexOf('{');
        if (start < 0) return null;
        int depth = 0;
        bool inString = false;
        bool escape = false;
        for (int i = start; i < text.Length; i++)
        {
            char c = text[i];
            if (inString)
            {
                if (escape) { escape = false; continue; }
                if (c == '\\') { escape = true; continue; }
                if (c == '"') inString = false;
                continue;
            }
            switch (c)
            {
                case '"': inString = true; break;
                case '{': depth++; break;
                case '}':
                    depth--;
                    if (depth == 0) return text.Substring(start, i - start + 1);
                    break;
            }
        }
        return null;
    }

    private sealed class SnapshotDto
    {
        [JsonPropertyName("taskDescription")] public string TaskDescription { get; set; } = "";
        [JsonPropertyName("currentState")] public string CurrentState { get; set; } = "";
        [JsonPropertyName("filesTouched")] public List<string>? FilesTouched { get; set; }
        [JsonPropertyName("errorsAndFixes")] public List<string>? ErrorsAndFixes { get; set; }
        [JsonPropertyName("pending")] public List<string>? Pending { get; set; }
        [JsonPropertyName("worklog")] public List<string>? Worklog { get; set; }
    }
}

public sealed class SessionMemoryParseException : Exception
{
    public SessionMemoryParseException(string message) : base(message) { }
    public SessionMemoryParseException(string message, Exception inner) : base(message, inner) { }
}
