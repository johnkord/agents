using Microsoft.Extensions.Logging.Abstractions;
using ResearchAgent.Core.Memory;
using ResearchAgent.Core.Models;
using ResearchAgent.Plugins.Content;

namespace ResearchAgent.Tests;

/// <summary>
/// Locks in the Source-ID resolution behavior added to <see cref="NoteTakingPlugin.RecordFinding"/>
/// after the 2026-04-16 sweep surfaced a 100%-refuse gate caused by LLM-invented source slugs
/// (see context-management-implementation-plan.md §0.6 closeout).
/// </summary>
public class NoteTakingPluginSourceResolutionTests
{
    [Fact]
    public void CamelCase_slug_resolves_via_url_tokens()
    {
        // Real-world slug format observed in the 2026-04-16 Run C: the Researcher
        // invents IDs like 'SQ3-SRC-ClaudeMemory'. The key content tokens
        // (claude/memory) must be recovered via CamelCase splitting and matched
        // against either the title or URL.
        var src = new SourceRecord
        {
            Id = "83b4aa75",
            Title = "How Claude remembers your project - Claude Code Docs",
            Url = "https://docs.anthropic.com/en/docs/claude-code/memory",
        };
        var (plugin, memory) = Build(src);

        plugin.RecordFinding("fact", "SQ3-SRC-ClaudeMemory", "SQ1", 0.9);

        var f = Assert.Single(memory.GetAllFindings());
        Assert.Equal("83b4aa75", f.SourceId);
    }

    [Fact]
    public void CamelCase_slug_GitHubCodingAgent_resolves()
    {
        var src = new SourceRecord
        {
            Id = "aaaabbbb",
            Title = "Using GitHub Copilot coding agent in VS Code",
            Url = "https://docs.github.com/en/copilot/coding-agent",
        };
        var (plugin, memory) = Build(src);

        plugin.RecordFinding("fact", "SQ1-SRC-GitHubCodingAgent", "SQ1", 0.9);

        var f = Assert.Single(memory.GetAllFindings());
        Assert.Equal("aaaabbbb", f.SourceId);
    }

    private static (NoteTakingPlugin plugin, ResearchMemory memory) Build(params SourceRecord[] sources)
    {
        var memory = new ResearchMemory();
        foreach (var s in sources)
            memory.RegisterSource(s);

        var plugin = new NoteTakingPlugin(memory, NullLoggerFactory.Instance);
        return (plugin, memory);
    }

    [Fact]
    public void Exact_id_match_wins()
    {
        var src = new SourceRecord { Id = "abc12345", Title = "Foo", Url = "https://foo.example/bar" };
        var (plugin, memory) = Build(src);

        plugin.RecordFinding("a finding", "abc12345", "SQ1", 0.9);

        var f = Assert.Single(memory.GetAllFindings());
        Assert.Equal("abc12345", f.SourceId);
    }

    [Fact]
    public void Slug_that_matches_title_resolves_to_real_id()
    {
        var src = new SourceRecord
        {
            Id = "83b4aa75",
            Title = "How Claude remembers your project - Claude Code Docs",
            Url = "https://docs.anthropic.com/en/docs/claude-code/memory",
        };
        var (plugin, memory) = Build(src);

        plugin.RecordFinding("Claude uses memory files.", "anthropic_claude_code_memory_doc", "SQ1", 0.9);

        var f = Assert.Single(memory.GetAllFindings());
        Assert.Equal("83b4aa75", f.SourceId);
    }

    [Fact]
    public void Slug_that_matches_url_domain_resolves()
    {
        var src = new SourceRecord
        {
            Id = "111aa111",
            Title = "Totally unrelated title",
            Url = "https://docs.anthropic.com/en/docs/claude-code/context-management",
        };
        var (plugin, memory) = Build(src);

        plugin.RecordFinding("fact", "docs_anthropic_claude_code", "SQ1", 0.9);

        var f = Assert.Single(memory.GetAllFindings());
        Assert.Equal("111aa111", f.SourceId);
    }

    [Fact]
    public void Orphan_slug_is_preserved_as_is_and_does_not_throw()
    {
        var src = new SourceRecord { Id = "abc12345", Title = "Foo", Url = "https://foo.example/bar" };
        var (plugin, memory) = Build(src);

        plugin.RecordFinding("fact", "totally_unrelated_slug_xyz", "SQ1", 0.9);

        var f = Assert.Single(memory.GetAllFindings());
        Assert.Equal("totally_unrelated_slug_xyz", f.SourceId);
    }

    [Fact]
    public void Empty_or_whitespace_source_id_is_preserved_unchanged()
    {
        var src = new SourceRecord { Id = "abc12345", Title = "Foo", Url = "https://foo.example/bar" };
        var (plugin, memory) = Build(src);

        plugin.RecordFinding("fact", "", "SQ1", 0.9);

        var f = Assert.Single(memory.GetAllFindings());
        Assert.Equal("", f.SourceId);
    }

    [Fact]
    public void Very_short_slug_below_min_length_does_not_false_match()
    {
        // "foo" is only 3 chars — too short to risk noisy token overlap with a title.
        var src = new SourceRecord { Id = "abc12345", Title = "Foo Bar Baz", Url = "https://example.com/foo" };
        var (plugin, memory) = Build(src);

        plugin.RecordFinding("fact", "foo", "SQ1", 0.9);

        var f = Assert.Single(memory.GetAllFindings());
        // Fell through to last-resort: original preserved.
        Assert.Equal("foo", f.SourceId);
    }
}
