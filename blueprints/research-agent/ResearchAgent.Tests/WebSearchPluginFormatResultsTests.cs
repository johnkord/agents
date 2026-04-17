using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using ResearchAgent.Core.Memory;
using ResearchAgent.Core.Models;
using ResearchAgent.Plugins.Search;
using Xunit;

namespace ResearchAgent.Tests;

/// <summary>
/// Locks in the WebSearchPlugin output shape introduced 2026-04-16 when the first live
/// sweep revealed that hiding <c>SourceRecord.Id</c> from the LLM forced it to invent
/// slugs ("SQ3-SRC-ClaudeMemory", "SQ1-S1"). Every finding then orphaned against
/// <c>EvidenceSufficiencyEvaluator</c> and <c>Enforce</c> mode refused 100% of real
/// sessions. The fix is an inline <c>(id: {Id})</c> marker plus a one-line instruction
/// prepended to the output. If either regresses, orphan findings return.
/// </summary>
public class WebSearchPluginFormatResultsTests
{
    private sealed class StubProvider : IWebSearchProvider
    {
        private readonly IReadOnlyList<SourceRecord> _records;
        public StubProvider(IReadOnlyList<SourceRecord> records) => _records = records;
        public string Name => "Stub";
        public Task<IReadOnlyList<SourceRecord>> SearchWebAsync(string query, int maxResults, CancellationToken ct)
            => Task.FromResult(_records);
        public Task<IReadOnlyList<SourceRecord>> SearchAcademicAsync(string query, int maxResults, CancellationToken ct)
            => Task.FromResult(_records);
    }

    private static WebSearchPlugin NewPlugin(params SourceRecord[] records)
    {
        var memory = new ResearchMemory();
        return new WebSearchPlugin(new StubProvider(records), memory, NullLoggerFactory.Instance);
    }

    [Fact]
    public async Task SearchWebAsync_emits_inline_id_marker_for_every_result()
    {
        var r1 = new SourceRecord { Id = "abc12345", Title = "Alpha", Url = "https://alpha.example/a", Snippet = "one" };
        var r2 = new SourceRecord { Id = "def67890", Title = "Beta", Url = "https://beta.example/b", Snippet = "two" };
        var plugin = NewPlugin(r1, r2);

        var output = await plugin.SearchWebAsync("anything", 10);

        Assert.Contains("(id: abc12345)", output);
        Assert.Contains("(id: def67890)", output);
        Assert.Contains("Alpha", output);
        Assert.Contains("Beta", output);
    }

    [Fact]
    public async Task SearchWebAsync_prepends_citation_instruction_naming_the_id_parameter()
    {
        var r1 = new SourceRecord { Id = "x1", Title = "T", Url = "u", Snippet = "s" };
        var plugin = NewPlugin(r1);

        var output = await plugin.SearchWebAsync("anything", 10);

        // The instruction is what re-teaches the LLM to use real IDs on every call.
        // Wording is deliberate — do not water down without replacement guidance.
        Assert.Contains("When citing a source, pass its 'id' value", output);
        Assert.Contains("RecordFinding", output);
    }

    [Fact]
    public async Task SearchWebAsync_id_appears_before_title_so_models_see_it_first()
    {
        var r1 = new SourceRecord { Id = "zzz99999", Title = "A Very Long Title That Models Might Skim", Url = "u", Snippet = "s" };
        var plugin = NewPlugin(r1);

        var output = await plugin.SearchWebAsync("q", 10);

        var idIdx = output.IndexOf("zzz99999");
        var titleIdx = output.IndexOf("A Very Long Title");
        Assert.True(idIdx < titleIdx, "id marker must precede the title");
    }

    [Fact]
    public async Task SearchWebAsync_empty_results_still_parseable()
    {
        var plugin = NewPlugin();

        var output = await plugin.SearchWebAsync("nothing here", 10);

        Assert.Contains("No results returned", output);
        Assert.Contains("nothing here", output);
        // No dangling instruction block for zero-result runs (instruction would be confusing).
        Assert.DoesNotContain("When citing a source", output);
    }

    [Fact]
    public async Task SearchAcademicPapersAsync_also_emits_inline_id_markers()
    {
        // The academic path uses the same FormatResults helper — pin that it stays that way.
        var p1 = new SourceRecord { Id = "arxiv1", Title = "Paper A", Url = "https://arxiv.org/abs/1", Snippet = "abs" };
        var plugin = NewPlugin(p1);

        var output = await plugin.SearchAcademicPapersAsync("q", 5);

        Assert.Contains("(id: arxiv1)", output);
        Assert.Contains("When citing a source", output);
    }

    [Fact]
    public async Task SearchWebAsync_registers_sources_in_memory_so_resolver_can_match_real_ids()
    {
        // End-to-end shape: the source ID emitted in text is the same ID persisted in memory.
        // If these ever diverged the downstream resolver would go back to token-overlap fallback.
        var r1 = new SourceRecord { Id = "same", Title = "T", Url = "u", Snippet = "s" };
        var memory = new ResearchMemory();
        var plugin = new WebSearchPlugin(new StubProvider(new[] { r1 }), memory, NullLoggerFactory.Instance);

        var output = await plugin.SearchWebAsync("q", 10);

        Assert.Contains("(id: same)", output);
        Assert.Contains(memory.GetAllSources(), s => s.Id == "same");
    }
}
