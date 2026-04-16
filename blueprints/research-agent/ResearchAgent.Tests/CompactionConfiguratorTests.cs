using Microsoft.Agents.AI;
#pragma warning disable MAAI001
using Microsoft.Agents.AI.Compaction;
#pragma warning restore MAAI001
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ResearchAgent.App;

namespace ResearchAgent.Tests;

public class CompactionConfiguratorTests
{
    private static IConfiguration Cfg(params (string, string?)[] kvs)
    {
        var dict = kvs.ToDictionary(kv => kv.Item1, kv => kv.Item2);
        return new ConfigurationBuilder().AddInMemoryCollection(dict!).Build();
    }

    [Fact]
    public void Resolve_defaults_to_Off_when_no_keys_present()
    {
        var cfg = CompactionConfigurator.Resolve(Cfg());
        Assert.Equal(CompactionConfigurator.Mode.Off, cfg.Mode);
        Assert.Equal("Off", cfg.Describe());
    }

    [Fact]
    public void Resolve_legacy_ToolResultCompaction_enabled_promotes_to_ToolResultOnly()
    {
        var cfg = CompactionConfigurator.Resolve(Cfg(
            ("AI:ToolResultCompaction:Enabled", "true"),
            ("AI:ToolResultCompaction:TokensThreshold", "4096")));

        Assert.Equal(CompactionConfigurator.Mode.ToolResultOnly, cfg.Mode);
        Assert.Equal(4096, cfg.ToolResultTokensThreshold);
    }

    [Fact]
    public void Resolve_new_Mode_key_overrides_legacy()
    {
        var cfg = CompactionConfigurator.Resolve(Cfg(
            ("AI:Compaction:Mode", "Pipeline"),
            ("AI:ToolResultCompaction:Enabled", "true")));
        Assert.Equal(CompactionConfigurator.Mode.Pipeline, cfg.Mode);
    }

    [Fact]
    public void Resolve_Pipeline_populates_all_stage_thresholds_by_default()
    {
        var cfg = CompactionConfigurator.Resolve(Cfg(("AI:Compaction:Mode", "Pipeline")));

        Assert.Equal(CompactionConfigurator.Mode.Pipeline, cfg.Mode);
        Assert.Equal(0x2000, cfg.ToolResultTokensThreshold);
        Assert.Equal(0x5000, cfg.SummarizationTokensThreshold);
        Assert.Equal(6, cfg.SlidingWindowTurnsThreshold);
        Assert.Equal(0x10000, cfg.TruncationTokensThreshold);
    }

    [Fact]
    public void Resolve_Pipeline_stage_can_be_disabled_via_zero_threshold()
    {
        var cfg = CompactionConfigurator.Resolve(Cfg(
            ("AI:Compaction:Mode", "Pipeline"),
            ("AI:Compaction:Summarization:TokensThreshold", "0"),
            ("AI:Compaction:Truncation:TokensThreshold", "-1")));

        Assert.Null(cfg.SummarizationTokensThreshold);
        Assert.Null(cfg.TruncationTokensThreshold);
        Assert.NotNull(cfg.SlidingWindowTurnsThreshold); // still on
    }

    [Fact]
    public void Resolve_unknown_mode_falls_back_to_Off()
    {
        var cfg = CompactionConfigurator.Resolve(Cfg(("AI:Compaction:Mode", "Bogus")));
        Assert.Equal(CompactionConfigurator.Mode.Off, cfg.Mode);
    }

    [Fact]
    public void Build_returns_null_for_Off()
    {
        var cfg = new CompactionConfigurator.CompactionConfig { Mode = CompactionConfigurator.Mode.Off };
        Assert.Null(CompactionConfigurator.Build(cfg, summarizerClientFactory: null));
    }

    [Fact]
    public void Build_ToolResultOnly_returns_bare_ToolResultStrategy()
    {
#pragma warning disable MAAI001
        var cfg = new CompactionConfigurator.CompactionConfig
        {
            Mode = CompactionConfigurator.Mode.ToolResultOnly,
            ToolResultTokensThreshold = 4096,
        };
        var s = CompactionConfigurator.Build(cfg, summarizerClientFactory: null);
        Assert.NotNull(s);
        Assert.IsType<ToolResultCompactionStrategy>(s);
#pragma warning restore MAAI001
    }

    [Fact]
    public void Build_Pipeline_without_summarizer_client_skips_summarization_stage_only()
    {
#pragma warning disable MAAI001
        var cfg = new CompactionConfigurator.CompactionConfig
        {
            Mode = CompactionConfigurator.Mode.Pipeline,
            ToolResultTokensThreshold = 0x2000,
            SummarizationTokensThreshold = 0x5000,
            SummarizationMinimumPreservedGroups = 8,
            SlidingWindowTurnsThreshold = 6,
            SlidingWindowPreservedTurns = 4,
            TruncationTokensThreshold = 0x10000,
            TruncationPreservedMessages = 8,
        };
        // Factory returns null → summarization stage should be skipped, others retained.
        var s = CompactionConfigurator.Build(cfg, summarizerClientFactory: () => null);
        Assert.NotNull(s);
        Assert.IsType<PipelineCompactionStrategy>(s);
#pragma warning restore MAAI001
    }

    [Fact]
    public void Build_Pipeline_single_remaining_stage_is_unwrapped()
    {
#pragma warning disable MAAI001
        var cfg = new CompactionConfigurator.CompactionConfig
        {
            Mode = CompactionConfigurator.Mode.Pipeline,
            ToolResultTokensThreshold = 0x2000,
            // All other stages disabled
            SummarizationTokensThreshold = null,
            SlidingWindowTurnsThreshold = null,
            TruncationTokensThreshold = null,
        };
        var s = CompactionConfigurator.Build(cfg, summarizerClientFactory: null);
        Assert.NotNull(s);
        // Single-stage pipeline collapses to the bare strategy.
        Assert.IsType<ToolResultCompactionStrategy>(s);
#pragma warning restore MAAI001
    }

    [Fact]
    public void Describe_for_Pipeline_lists_stages_in_order()
    {
        var cfg = new CompactionConfigurator.CompactionConfig
        {
            Mode = CompactionConfigurator.Mode.Pipeline,
            ToolResultTokensThreshold = 0x2000,
            SummarizationTokensThreshold = 0x5000,
            SummarizationMinimumPreservedGroups = 8,
            SlidingWindowTurnsThreshold = 6,
            SlidingWindowPreservedTurns = 4,
            TruncationTokensThreshold = 0x10000,
            TruncationPreservedMessages = 8,
        };
        var d = cfg.Describe();
        Assert.StartsWith("Pipeline[", d);
        // Order matters: the description mirrors execution order.
        var idxTool = d.IndexOf("ToolResult");
        var idxSum = d.IndexOf("Summarization");
        var idxSlide = d.IndexOf("SlidingWindow");
        var idxTrunc = d.IndexOf("Truncation");
        Assert.True(idxTool < idxSum);
        Assert.True(idxSum < idxSlide);
        Assert.True(idxSlide < idxTrunc);
    }
}
