using Microsoft.Agents.AI;
#pragma warning disable MAAI001 // Microsoft.Agents.AI.Compaction is experimental in 1.1.0
using Microsoft.Agents.AI.Compaction;
#pragma warning restore MAAI001
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ResearchAgent.App;

/// <summary>
/// Builds the MAF compaction pipeline for a research-agent sub-agent (P2.2).
///
/// <para>Modes, via <c>AI:Compaction:Mode</c>:</para>
/// <list type="bullet">
/// <item><c>Off</c> — no compaction provider is attached (default).</item>
/// <item><c>ToolResultOnly</c> — the P1.1 behavior: just
/// <see cref="ToolResultCompactionStrategy"/>. Back-compat for anyone still reading
/// the legacy <c>AI:ToolResultCompaction:Enabled</c> flag.</item>
/// <item><c>Pipeline</c> — the full P2.2 stack:
/// <c>ToolResult → Summarization → SlidingWindow → Truncation</c>. Ordering matters:
/// cheap local compactions run first; the LLM-backed summarizer runs before the
/// lossy window/truncation fallbacks, so we only pay for summarization when cheaper
/// tactics have not already cleared the threshold.</item>
/// </list>
///
/// <para>Back-compat rule: if the new <c>AI:Compaction</c> section is absent but
/// <c>AI:ToolResultCompaction:Enabled</c> is <c>true</c>, we silently promote to
/// <c>ToolResultOnly</c> mode with the legacy threshold. This keeps P1.1's existing
/// configs working without edits.</para>
///
/// <para>The summarizer chat client is a separate concern: by default it reuses the
/// primary <see cref="IChatClient"/>. An explicit <c>AI:Compaction:Summarization:Model</c>
/// can name a cheaper/faster model for summarization calls; when set, we spin up a
/// dedicated <see cref="IChatClient"/> keyed off the same endpoint+credential.</para>
/// </summary>
public static class CompactionConfigurator
{
    public enum Mode
    {
        Off,
        ToolResultOnly,
        Pipeline,
    }

    /// <summary>Per-stage knobs as resolved from config. Null entries mean "stage disabled".</summary>
    public sealed record CompactionConfig
    {
        public Mode Mode { get; init; }
        public int ToolResultTokensThreshold { get; init; }
        public int? SummarizationTokensThreshold { get; init; }
        public int SummarizationMinimumPreservedGroups { get; init; }
        public string? SummarizationModel { get; init; }
        public int? SlidingWindowTurnsThreshold { get; init; }
        public int SlidingWindowPreservedTurns { get; init; }
        public int? TruncationTokensThreshold { get; init; }
        public int TruncationPreservedMessages { get; init; }

        /// <summary>One-line human-readable description for telemetry.</summary>
        public string Describe()
        {
            if (Mode == Mode.Off) return "Off";
            if (Mode == Mode.ToolResultOnly)
                return $"ToolResultOnly(tokens>{ToolResultTokensThreshold})";

            var stages = new List<string>
            {
                $"ToolResult(tokens>{ToolResultTokensThreshold})"
            };
            if (SummarizationTokensThreshold is int st)
                stages.Add($"Summarization(tokens>{st}, keep≥{SummarizationMinimumPreservedGroups})");
            if (SlidingWindowTurnsThreshold is int sw)
                stages.Add($"SlidingWindow(turns>{sw}, keep={SlidingWindowPreservedTurns})");
            if (TruncationTokensThreshold is int tr)
                stages.Add($"Truncation(tokens>{tr}, keep={TruncationPreservedMessages})");
            return $"Pipeline[{string.Join(" → ", stages)}]";
        }
    }

    /// <summary>
    /// Resolves compaction config from <paramref name="config"/> including back-compat
    /// for the legacy <c>AI:ToolResultCompaction:*</c> keys. Never throws; invalid
    /// values fall back to sensible defaults (and a later Build call will log).
    /// </summary>
    public static CompactionConfig Resolve(IConfiguration config)
    {
        var modeSection = config["AI:Compaction:Mode"];
        var legacyEnabled = config.GetValue<bool>("AI:ToolResultCompaction:Enabled", false);

        Mode mode;
        if (!string.IsNullOrWhiteSpace(modeSection))
        {
            mode = Enum.TryParse<Mode>(modeSection, ignoreCase: true, out var parsed) ? parsed : Mode.Off;
        }
        else if (legacyEnabled)
        {
            mode = Mode.ToolResultOnly;
        }
        else
        {
            mode = Mode.Off;
        }

        // Legacy key wins for tool-result threshold when new key is absent.
        var toolResultTokens = config.GetValue<int?>("AI:Compaction:ToolResult:TokensThreshold")
            ?? config.GetValue<int>("AI:ToolResultCompaction:TokensThreshold", 0x2000);

        // Stages are individually skippable in Pipeline mode by setting their threshold to 0 or negative.
        int? Optional(string key, int fallback)
        {
            var v = config.GetValue<int?>(key);
            if (v is null) return fallback;
            return v.Value > 0 ? v.Value : null;
        }

        return new CompactionConfig
        {
            Mode = mode,
            ToolResultTokensThreshold = toolResultTokens,

            SummarizationTokensThreshold = mode == Mode.Pipeline
                ? Optional("AI:Compaction:Summarization:TokensThreshold", 0x5000) : null,
            SummarizationMinimumPreservedGroups =
                config.GetValue<int>("AI:Compaction:Summarization:MinimumPreservedGroups", 8),
            SummarizationModel = config["AI:Compaction:Summarization:Model"],

            SlidingWindowTurnsThreshold = mode == Mode.Pipeline
                ? Optional("AI:Compaction:SlidingWindow:TurnsThreshold", 6) : null,
            SlidingWindowPreservedTurns =
                config.GetValue<int>("AI:Compaction:SlidingWindow:PreservedTurns", 4),

            TruncationTokensThreshold = mode == Mode.Pipeline
                ? Optional("AI:Compaction:Truncation:TokensThreshold", 0x10000) : null,
            TruncationPreservedMessages =
                config.GetValue<int>("AI:Compaction:Truncation:PreservedMessages", 8),
        };
    }

    /// <summary>
    /// Builds the <see cref="CompactionStrategy"/> requested by <paramref name="cfg"/>, or
    /// null when <see cref="Mode.Off"/> (callers should skip provider attachment in that case).
    /// The returned strategy is safe to wrap in a <see cref="CompactionProvider"/>.
    /// </summary>
    /// <param name="summarizerClientFactory">
    /// Produces the <see cref="IChatClient"/> used by <see cref="SummarizationCompactionStrategy"/>.
    /// The factory is invoked at most once per <see cref="Build"/> call and only when
    /// <see cref="Mode.Pipeline"/> has the summarization stage enabled. Returning null
    /// suppresses just the summarization stage (useful for tests).
    /// </param>
#pragma warning disable MAAI001
    public static CompactionStrategy? Build(
        CompactionConfig cfg,
        Func<IChatClient?>? summarizerClientFactory,
        ILogger? logger = null)
    {
        switch (cfg.Mode)
        {
            case Mode.Off:
                return null;

            case Mode.ToolResultOnly:
                return new ToolResultCompactionStrategy(
                    trigger: CompactionTriggers.TokensExceed(cfg.ToolResultTokensThreshold));

            case Mode.Pipeline:
            {
                var stages = new List<CompactionStrategy>
                {
                    new ToolResultCompactionStrategy(
                        trigger: CompactionTriggers.TokensExceed(cfg.ToolResultTokensThreshold)),
                };

                if (cfg.SummarizationTokensThreshold is int sumTokens)
                {
                    var chat = summarizerClientFactory?.Invoke();
                    if (chat is not null)
                    {
                        stages.Add(new SummarizationCompactionStrategy(
                            chatClient: chat,
                            trigger: CompactionTriggers.TokensExceed(sumTokens),
                            minimumPreservedGroups: cfg.SummarizationMinimumPreservedGroups));
                    }
                    else
                    {
                        logger?.LogWarning(
                            "Compaction: summarization stage requested (tokens>{Tokens}) but no IChatClient was provided — skipping stage.",
                            sumTokens);
                    }
                }

                if (cfg.SlidingWindowTurnsThreshold is int swTurns)
                {
                    stages.Add(new SlidingWindowCompactionStrategy(
                        trigger: CompactionTriggers.TurnsExceed(swTurns),
                        minimumPreservedTurns: cfg.SlidingWindowPreservedTurns));
                }

                if (cfg.TruncationTokensThreshold is int trTokens)
                {
                    stages.Add(new TruncationCompactionStrategy(
                        trigger: CompactionTriggers.TokensExceed(trTokens),
                        minimumPreservedGroups: cfg.TruncationPreservedMessages));
                }

                // A single-stage pipeline is equivalent to the bare strategy; skip the wrapper.
                return stages.Count == 1 ? stages[0] : new PipelineCompactionStrategy(stages);
            }

            default:
                return null;
        }
    }
#pragma warning restore MAAI001
}
