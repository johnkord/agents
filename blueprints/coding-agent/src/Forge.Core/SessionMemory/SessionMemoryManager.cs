using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Serilog;

namespace Forge.Core.SessionMemory;

/// <summary>
/// Coordinates mid-session memory extraction: decides WHEN to extract (token/step
/// triggers), invokes the extractor, handles failures via the circuit breaker,
/// and persists the snapshot. Stateful — one instance per session.
///
/// Design choices vs the Claude Code reference:
/// - <b>Not forked.</b> A blocking auxiliary call on the main loop. Simpler to
///   reason about. Forking (<i>P4.1</i>) can come later without API change here.
/// - <b>6 sections, not 12.</b> Driven by <see cref="SessionMemorySnapshot"/>.
/// - <b>Conservative triggers.</b> Init waits for real token accumulation before
///   spending money on extraction; updates only every N steps.
/// - <b>NO_TOOLS extractor.</b> Enforced at the call site in the default extractor
///   by constructing an <see cref="ILlmClient"/> with an empty tool list.
/// - <b>Circuit breaker.</b> Uses <see cref="AuxiliaryCallLimiter"/>. After
///   <c>threshold</c> consecutive failures the manager disables itself for the
///   rest of the session and logs once; the main loop is unaffected.
/// </summary>
public sealed class SessionMemoryManager
{
    /// <summary>Breaker name for <see cref="AuxiliaryCallLimiter"/> bookkeeping.</summary>
    public const string CircuitName = "session-memory";

    private readonly SessionMemoryOptions _options;
    private readonly SessionMemoryExtractor _extractor;
    private readonly AuxiliaryCallLimiter _limiter;
    private readonly ILogger _logger;
    private readonly string _sessionDir;

    private SessionMemorySnapshot? _snapshot;
    private int _extractionCount;
    private int _stepsSinceLastExtraction;
    private bool _disabled;

    public SessionMemoryManager(
        SessionMemoryOptions options,
        SessionMemoryExtractor extractor,
        AuxiliaryCallLimiter limiter,
        string sessionDir,
        ILogger logger)
    {
        _options = options;
        _extractor = extractor;
        _limiter = limiter;
        _sessionDir = sessionDir;
        _logger = logger.ForContext<SessionMemoryManager>();
    }

    /// <summary>Current snapshot, or null before the first successful extraction.</summary>
    public SessionMemorySnapshot? Snapshot => _snapshot;

    /// <summary>
    /// True once the circuit breaker has tripped on this manager — further
    /// <see cref="MaybeExtractAsync"/> calls short-circuit. Visible for tests
    /// and the final session summary.
    /// </summary>
    public bool Disabled => _disabled;

    public int ExtractionCount => _extractionCount;

    /// <summary>
    /// Consider an extraction after a step has completed. Returns the snapshot
    /// when an extraction fires (success), otherwise null. Never throws on
    /// extractor errors — failures go through the circuit breaker.
    /// </summary>
    public async Task<SessionMemorySnapshot?> MaybeExtractAsync(
        string task,
        IReadOnlyList<StepRecord> steps,
        int totalTokensSoFar,
        CancellationToken ct)
    {
        if (_disabled) return null;
        if (!_limiter.Allow(CircuitName))
        {
            DisableOnce("circuit breaker tripped");
            return null;
        }

        _stepsSinceLastExtraction++;

        if (!ShouldExtract(totalTokensSoFar))
            return null;

        var request = new SessionMemoryExtractionRequest
        {
            Task = task,
            Steps = steps,
            FromStepIndex = _snapshot?.LastExtractedStep,
            PreviousSummary = _snapshot,
        };

        try
        {
            var result = await _extractor(request, ct);
            _snapshot = result.Snapshot;
            _extractionCount++;
            _stepsSinceLastExtraction = 0;
            _limiter.RecordSuccess(CircuitName);

            _logger.Information(
                "Session memory extracted (#{Count}) — coveredThroughStep={Step}, promptTokens={Prompt}, completionTokens={Completion}",
                _extractionCount, result.Snapshot.LastExtractedStep, result.PromptTokens, result.CompletionTokens);

            await PersistAsync(result.Snapshot, result.RawResponse, ct);
            return result.Snapshot;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _limiter.RecordFailure(CircuitName);
            _logger.Warning(ex, "Session memory extraction failed");
            if (!_limiter.Allow(CircuitName))
                DisableOnce("3 consecutive failures");
            return null;
        }
    }

    private bool ShouldExtract(int totalTokens)
    {
        // First extraction: wait for token accumulation to cross the init threshold.
        // A very early extraction yields a low-signal summary at maximum cost.
        if (_snapshot is null)
            return totalTokens >= _options.MinInitTokens;

        // Subsequent extractions: every N steps (counted since the last extraction).
        return _stepsSinceLastExtraction >= _options.StepsBetweenUpdates;
    }

    private void DisableOnce(string reason)
    {
        if (_disabled) return;
        _disabled = true;
        _logger.Warning("Session memory disabled for this session ({Reason})", reason);
    }

    private async Task PersistAsync(SessionMemorySnapshot snapshot, string? raw, CancellationToken ct)
    {
        try
        {
            Directory.CreateDirectory(_sessionDir);

            // Markdown — the primary artifact a continuation session or a human reads.
            var mdPath = Path.Combine(_sessionDir, "summary.md");
            await File.WriteAllTextAsync(mdPath, RenderMarkdown(snapshot), ct);

            // JSON — machine-readable for analyzers.
            var jsonPath = Path.Combine(_sessionDir, "summary.json");
            var jsonOpts = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(snapshot, jsonOpts), ct);

            // Raw response (including <analysis>) kept behind a debug flag — useful when
            // debugging prompt drift, not needed for normal operation.
            if (raw is not null && _options.PersistRawResponses)
            {
                var rawPath = Path.Combine(_sessionDir, $"extraction-{_extractionCount:D3}.txt");
                await File.WriteAllTextAsync(rawPath, raw, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to persist session memory");
        }
    }

    internal static string RenderMarkdown(SessionMemorySnapshot s)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Session Memory");
        sb.AppendLine();
        sb.AppendLine($"_Updated: {s.UpdatedAt:O} · extraction #{s.ExtractionCount} · covered through step {s.LastExtractedStep}_");
        sb.AppendLine();
        sb.AppendLine("## Task");
        sb.AppendLine();
        sb.AppendLine(s.TaskDescription.TrimEnd());
        sb.AppendLine();
        sb.AppendLine("## Current State");
        sb.AppendLine();
        sb.AppendLine(s.CurrentState.TrimEnd());
        sb.AppendLine();
        AppendList(sb, "Files Touched", s.FilesTouched);
        AppendList(sb, "Errors & Fixes", s.ErrorsAndFixes);
        AppendList(sb, "Pending", s.Pending);
        AppendList(sb, "Worklog", s.Worklog);
        return sb.ToString();
    }

    private static void AppendList(StringBuilder sb, string heading, IReadOnlyList<string> items)
    {
        sb.AppendLine($"## {heading}");
        sb.AppendLine();
        if (items.Count == 0)
        {
            sb.AppendLine("_(none)_");
        }
        else
        {
            foreach (var item in items)
                sb.AppendLine($"- {item.Trim()}");
        }
        sb.AppendLine();
    }

    /// <summary>
    /// Strip a leading <c>&lt;analysis&gt;…&lt;/analysis&gt;</c> block (including
    /// whitespace) from the LLM's raw response before parsing the summary body.
    /// Two-phase prompting keeps the model thinking out loud without polluting
    /// the persisted artifact.
    /// </summary>
    internal static string StripAnalysisBlock(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        // Non-greedy, case-insensitive, dot-matches-newline.
        return Regex.Replace(raw,
            @"<analysis>.*?</analysis>\s*",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
    }
}

public sealed class SessionMemoryOptions
{
    public bool Enabled { get; init; }
    /// <summary>Tokens (prompt+completion) that must accumulate before the first extraction fires.</summary>
    public int MinInitTokens { get; init; } = 15_000;
    /// <summary>Steps between subsequent extractions, counted from the last success.</summary>
    public int StepsBetweenUpdates { get; init; } = 5;
    /// <summary>Consecutive failures before the manager disables itself.</summary>
    public int FailureThreshold { get; init; } = 3;
    /// <summary>Persist the raw LLM response (incl. <c>&lt;analysis&gt;</c>) alongside summary.md.</summary>
    public bool PersistRawResponses { get; init; }
}
