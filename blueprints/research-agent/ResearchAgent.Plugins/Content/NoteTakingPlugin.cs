using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ResearchAgent.Core.Memory;
using ResearchAgent.Core.Models;

namespace ResearchAgent.Plugins.Content;

/// <summary>
/// Note-taking plugin — implements Pensieve's core note/updateNote/readNote cycle.
///
/// This is the critical bridge between raw content and persistent knowledge.
/// The agent reads content, distills findings into notes, then the raw content
/// can be pruned. Notes survive indefinitely in ResearchMemory.
/// </summary>
public sealed class NoteTakingPlugin
{
    private readonly ResearchMemory _memory;
    private readonly ILogger _logger;

    public NoteTakingPlugin(ResearchMemory memory, ILoggerFactory loggerFactory)
    {
        _memory = memory;
        _logger = loggerFactory.CreateLogger<NoteTakingPlugin>();
    }

    [Description("Record a key finding from research. This distills important information into a persistent note that survives context management. Include the source ID and which sub-question this finding addresses.")]
    public string RecordFinding(
        [Description("The key finding or fact to record")] string content,
        [Description("The ID of the source this finding came from")] string sourceId,
        [Description("The ID of the sub-question this finding addresses")] string subQuestionId,
        [Description("Confidence level from 0.0 to 1.0")] double confidence = 0.7)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("[TOOL] RecordFinding — subQ=\"{SubQuestionId}\", source=\"{SourceId}\", confidence={Confidence:F2}, contentLen={ContentLen}",
            subQuestionId, sourceId, confidence, content.Length);
        _logger.LogDebug("[TOOL] RecordFinding content preview: {Preview}",
            content.Length > 120 ? content[..120] + "..." : content);

        // Resolve the agent-supplied sourceId against the actual registered SourceRecord
        // pool. LLMs routinely invent human-readable slugs (e.g. "anthropic_claude_code_doc")
        // instead of quoting the short hash ID we returned from the search tool. When this
        // happens the finding's SourceId is orphan — it resolves to no real source, which
        // silently defeats downstream consumers like EvidenceSufficiencyEvaluator's
        // per-question distinct-domain check. Resolve upstream so downstream is honest.
        var (resolvedSourceId, resolutionOutcome) = ResolveSourceId(sourceId);

        var finding = new ResearchFinding
        {
            Content = content,
            SourceId = resolvedSourceId,
            SubQuestionId = subQuestionId,
            Confidence = Math.Clamp(confidence, 0.0, 1.0)
        };

        _memory.StoreFinding(finding);
        _memory.UpdateProgressForFinding(subQuestionId, Math.Clamp(confidence, 0.0, 1.0));

        var totalForQuestion = _memory.GetFindingsForQuestion(subQuestionId).Count;
        var totalAll = _memory.GetAllFindings().Count;

        sw.Stop();
        _logger.LogInformation("[TOOL] RecordFinding done — id={FindingId}, findingsForQ={ForQuestion}, totalFindings={Total}, {ElapsedMs}ms",
            finding.Id, totalForQuestion, totalAll, sw.ElapsedMilliseconds);

        var baseMsg = $"Finding recorded (ID: {finding.Id}, confidence: {finding.Confidence:P0}). " +
                      $"Total findings for '{subQuestionId}': {totalForQuestion}";

        // When the agent-supplied sourceId didn't map to a real SourceRecord AND the memory
        // already has some registered sources to choose from, surface a visible warning to
        // the LLM so it self-corrects on subsequent calls. We deliberately *accept* the
        // finding (losing it silently would be worse) but include the problem in the return
        // text, which the model will see in its next turn.
        if (resolutionOutcome == ResolutionOutcome.Orphan && _memory.GetAllSources().Count > 0)
        {
            return baseMsg +
                $" ⚠ WARNING: sourceId '{sourceId}' did not match any source ID returned by SearchWeb. " +
                $"Next time, pass the exact 'id:' value from the search results (e.g. an 8-hex token). " +
                $"This finding will not contribute to the evidence-sufficiency gate's domain-coverage check.";
        }

        return baseMsg;
    }

    [Description("Update the working notes for a specific sub-question. This creates or overwrites the summary note for that sub-question with an updated synthesis of findings so far.")]
    public string UpdateWorkingNote(
        [Description("The sub-question ID to update notes for")] string subQuestionId,
        [Description("The updated summary note synthesizing all findings so far")] string note)
    {
        _logger.LogInformation("[TOOL] UpdateWorkingNote — subQ=\"{SubQuestionId}\", noteLen={NoteLen} chars (~{TokenEst} tokens)",
            subQuestionId, note.Length, note.Length / 4);

        _memory.UpdateNote(subQuestionId, note);

        _logger.LogDebug("[TOOL] UpdateWorkingNote done for subQ=\"{SubQuestionId}\"", subQuestionId);

        return $"Working note updated for sub-question '{subQuestionId}'. " +
               $"Note length: {note.Length} characters (~{note.Length / 4} tokens)";
    }

    [Description("Read the current working notes for a sub-question. Returns the accumulated synthesis so far.")]
    public string ReadWorkingNote(
        [Description("The sub-question ID to read notes for")] string subQuestionId)
    {
        _logger.LogInformation("[TOOL] ReadWorkingNote — subQ=\"{SubQuestionId}\"", subQuestionId);

        var note = _memory.ReadNote(subQuestionId);
        var found = !string.IsNullOrEmpty(note);

        _logger.LogInformation("[TOOL] ReadWorkingNote done — subQ=\"{SubQuestionId}\", found={Found}, noteLen={NoteLen}",
            subQuestionId, found, note?.Length ?? 0);

        return found
            ? $"Working note for '{subQuestionId}':\n\n{note}"
            : $"No working notes found for sub-question '{subQuestionId}'.";
    }

    [Description("Get a summary of all research progress including findings count per sub-question and context budget usage.")]
    public string CheckResearchProgress()
    {
        _logger.LogInformation("[TOOL] CheckResearchProgress called");

        var findings = _memory.GetAllFindings();
        var sources = _memory.GetAllSources();
        var tokenEstimate = _memory.EstimateTokenCount();

        _logger.LogInformation("[TOOL] CheckResearchProgress — findings={FindingCount}, sources={SourceCount}, " +
            "sourcesRead={SourcesRead}, memoryTokens=~{TokenEstimate}",
            findings.Count, sources.Count, sources.Count(s => s.HasBeenRead), tokenEstimate);

        var summary = $"""
            ## Research Progress Summary

            - Total findings recorded: {findings.Count}
            - Total sources discovered: {sources.Count}
            - Sources read: {sources.Count(s => s.HasBeenRead)} / {sources.Count}
            - Estimated memory tokens: ~{tokenEstimate:N0}

            ### Findings by confidence:
            - High (>80%): {findings.Count(f => f.Confidence > 0.8)}
            - Medium (50-80%): {findings.Count(f => f.Confidence is >= 0.5 and <= 0.8)}
            - Low (<50%): {findings.Count(f => f.Confidence < 0.5)}
            """;

        // Append per-sub-question progress
        var progress = _memory.GetAllProgress();
        if (progress.Count > 0)
        {
            var progressSection = new System.Text.StringBuilder();
            progressSection.AppendLine("\n### Per Sub-Question Progress:");
            foreach (var p in progress)
            {
                var status = p.MarkedComplete ? "✓ COMPLETE" : "in-progress";
                progressSection.AppendLine($"- {p.SubQuestionId}: {status}, searches={p.SearchAttempts}, findings={p.FindingsRecorded}, confidence={p.AverageConfidence:P0}");
                if (p.KnowledgeGaps.Count > 0)
                    progressSection.AppendLine($"  ⚠ Gaps: {string.Join("; ", p.KnowledgeGaps)}");
                if (p.FailedQueries.Count > 0)
                    progressSection.AppendLine($"  ✗ Failed queries: {string.Join("; ", p.FailedQueries.TakeLast(3))}");
            }
            summary += progressSection.ToString();
        }

        // Append under-researched questions alert
        var underResearched = _memory.GetUnderResearchedQuestions();
        if (underResearched.Count > 0)
        {
            summary += $"\n### ⚠ Sub-Questions Needing More Research ({underResearched.Count}):\n";
            foreach (var q in underResearched)
            {
                summary += $"- {q.SubQuestionId}: findings={q.FindingsRecorded}, confidence={q.AverageConfidence:P0}\n";
            }
        }

        return summary;
    }

    [Description("Record a reflection about a failed or suboptimal research action. "
        + "Use this when a search didn't return useful results, when a source was unreliable, "
        + "or when you realize you need a different approach. This helps avoid repeating mistakes.")]
    public string RecordReflection(
        [Description("The sub-question this reflection relates to")] string subQuestionId,
        [Description("What you originally tried (e.g., the search query or approach)")] string originalAction,
        [Description("Your analysis of why it didn't work and what to try instead")] string reflection,
        [Description("The revised approach you plan to take next (optional)")] string revisedAction = "")
    {
        _logger.LogInformation("[TOOL] RecordReflection — subQ=\"{SubQuestionId}\", originalAction=\"{OriginalAction}\"",
            subQuestionId, originalAction.Length > 80 ? originalAction[..80] + "..." : originalAction);

        var entry = new ReflectionEntry
        {
            SubQuestionId = subQuestionId,
            OriginalAction = originalAction,
            Reflection = reflection,
            RevisedAction = string.IsNullOrWhiteSpace(revisedAction) ? null : revisedAction
        };

        _memory.StoreReflection(entry);

        // Also record the failed query in progress tracking
        _memory.RecordSearchAttempt(subQuestionId, originalAction);

        var totalReflections = _memory.GetReflections().Count;
        _logger.LogInformation("[TOOL] RecordReflection done — id={ReflectionId}, total={Total}",
            entry.Id, totalReflections);

        return $"Reflection recorded (ID: {entry.Id}). Total reflections: {totalReflections}. " +
               $"Consider trying: {(string.IsNullOrWhiteSpace(revisedAction) ? "a different approach" : revisedAction)}";
    }

    [Description("Get all past reflections, optionally filtered by sub-question. "
        + "Review these before starting research on a sub-question to avoid repeating failed approaches.")]
    public string GetReflections(
        [Description("Optional sub-question ID to filter reflections. Leave empty for all reflections.")] string subQuestionId = "")
    {
        var filterById = string.IsNullOrWhiteSpace(subQuestionId) ? null : subQuestionId;
        _logger.LogInformation("[TOOL] GetReflections — filter={Filter}", filterById ?? "(all)");

        var reflections = _memory.GetReflections(filterById);

        if (reflections.Count == 0)
            return filterById is null
                ? "No reflections recorded yet."
                : $"No reflections recorded for sub-question '{filterById}'.";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"## Reflections ({reflections.Count})");
        foreach (var r in reflections)
        {
            sb.AppendLine($"- [{r.SubQuestionId}] Tried: {r.OriginalAction}");
            sb.AppendLine($"  Reflection: {r.Reflection}");
            if (r.RevisedAction is not null)
                sb.AppendLine($"  Revised approach: {r.RevisedAction}");
            sb.AppendLine($"  Outcome: {r.Outcome}");
        }

        _logger.LogInformation("[TOOL] GetReflections done — {Count} reflections returned", reflections.Count);
        return sb.ToString();
    }

    [Description("Record a knowledge gap identified during analysis. "
        + "This marks a sub-question as needing additional research in the next iteration.")]
    public string RecordKnowledgeGap(
        [Description("The sub-question this gap relates to")] string subQuestionId,
        [Description("Description of what information is missing")] string gap)
    {
        _logger.LogInformation("[TOOL] RecordKnowledgeGap — subQ=\"{SubQuestionId}\", gap=\"{Gap}\"",
            subQuestionId, gap.Length > 80 ? gap[..80] + "..." : gap);

        _memory.RecordKnowledgeGap(subQuestionId, gap);

        return $"Knowledge gap recorded for '{subQuestionId}': {gap}";
    }

    [Description("Retrieve all accumulated findings as a structured context block, suitable for synthesis. This provides the full 'Pensieve' — all distilled notes and findings.")]
    public string GetAllResearchContext()
    {
        _logger.LogInformation("[TOOL] GetAllResearchContext called");

        var context = _memory.BuildContextSummary();

        _logger.LogInformation("[TOOL] GetAllResearchContext done — {ContextChars} chars (~{TokenEstimate} tokens)",
            context.Length, context.Length / 4);
        _logger.LogDebug("[TOOL] GetAllResearchContext preview: {Preview}",
            context.Length > 200 ? context[..200] + "..." : context);

        return context;
    }

    /// <summary>Outcome classification from <see cref="ResolveSourceId"/>.</summary>
    private enum ResolutionOutcome { Exact, FuzzyMatched, Orphan, Empty }

    /// <summary>
    /// Resolve an agent-supplied <c>sourceId</c> argument to a real <see cref="SourceRecord.Id"/>.
    /// LLMs tend to cite human-readable slugs (<c>"anthropic_claude_code_memory_doc"</c>)
    /// rather than the 8-hex IDs our search tool returns. When that happens, the finding's
    /// <c>SourceId</c> is an orphan — it doesn't match any real source, which silently breaks
    /// downstream consumers like <c>EvidenceSufficiencyEvaluator</c>. This resolver tries in
    /// order:
    /// <list type="number">
    /// <item>Exact ID match (the happy path).</item>
    /// <item>Substring match against titles (handles slugs derived from either).</item>
    /// <item>As a last resort, return the original string so the finding is still recorded.
    /// A Warning is logged so orphan rates are visible.</item>
    /// </list>
    /// Returns the resolved id and an outcome classification so the caller can surface a
    /// self-correcting warning to the LLM on orphan.
    /// </summary>
    private (string id, ResolutionOutcome outcome) ResolveSourceId(string agentSourceId)
    {
        if (string.IsNullOrWhiteSpace(agentSourceId))
            return (agentSourceId, ResolutionOutcome.Empty);

        var sources = _memory.GetAllSources();

        // Exact match — short hash IDs we returned from the search tool.
        var exact = sources.FirstOrDefault(s =>
            string.Equals(s.Id, agentSourceId, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return (exact.Id, ResolutionOutcome.Exact);

        // Slug-style IDs frequently embed fragments of the title or domain. Try a simple
        // containment match in either direction. We normalize to lowercase + replace
        // underscores/dashes with spaces so "anthropic_claude_code_memory_doc" can match a
        // title like "How Claude remembers your project - Claude Code Docs".
        var needle = Normalize(agentSourceId);
        if (needle.Length >= 4)
        {
            var titleHit = sources.FirstOrDefault(s =>
                !string.IsNullOrEmpty(s.Title) &&
                TokensOverlap(needle, Normalize(s.Title), minOverlap: 2));
            if (titleHit is not null)
            {
                _logger.LogInformation(
                    "[TOOL] RecordFinding source-resolution: agent slug '{Slug}' matched by title → {Id} ({Title})",
                    agentSourceId, titleHit.Id, titleHit.Title);
                return (titleHit.Id, ResolutionOutcome.FuzzyMatched);
            }

            var urlHit = sources.FirstOrDefault(s =>
                !string.IsNullOrEmpty(s.Url) &&
                TokensOverlap(needle, Normalize(s.Url), minOverlap: 2));
            if (urlHit is not null)
            {
                _logger.LogInformation(
                    "[TOOL] RecordFinding source-resolution: agent slug '{Slug}' matched by URL → {Id} ({Url})",
                    agentSourceId, urlHit.Id, urlHit.Url);
                return (urlHit.Id, ResolutionOutcome.FuzzyMatched);
            }
        }

        _logger.LogWarning(
            "[TOOL] RecordFinding source-resolution FAILED: agent supplied sourceId '{AgentId}' that matches none of {Count} registered sources. Finding will be orphan w.r.t. evidence gate.",
            agentSourceId, sources.Count);
        return (agentSourceId, ResolutionOutcome.Orphan);
    }

    private static string Normalize(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length + 8);
        char prev = ' ';
        foreach (var c in s)
        {
            // Treat all non-alnum chars as word boundaries so URLs, slugs, and titles
            // tokenize consistently.
            if (!char.IsLetterOrDigit(c))
            {
                sb.Append(' ');
                prev = ' ';
                continue;
            }
            // Split CamelCase: insert a space when transitioning lower→upper or
            // digit→letter. This is what makes 'ClaudeMemory' / 'GitHubCodingAgent'
            // decompose into the individual words a title or URL path will contain.
            if ((char.IsUpper(c) && char.IsLower(prev)) ||
                (char.IsLetter(c) && char.IsDigit(prev)) ||
                (char.IsDigit(c) && char.IsLetter(prev)))
            {
                sb.Append(' ');
            }
            sb.Append(char.ToLowerInvariant(c));
            prev = c;
        }
        return sb.ToString();
    }

    /// <summary>
    /// Returns true when the two normalized strings share at least <paramref name="minOverlap"/>
    /// tokens of length ≥ 4. Avoids false positives from common stop-sized tokens like "the".
    /// </summary>
    private static bool TokensOverlap(string a, string b, int minOverlap)
    {
        var aTokens = a.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length >= 4).ToHashSet();
        if (aTokens.Count == 0) return false;
        var bTokens = b.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length >= 4);
        return bTokens.Count(aTokens.Contains) >= minOverlap;
    }
}
