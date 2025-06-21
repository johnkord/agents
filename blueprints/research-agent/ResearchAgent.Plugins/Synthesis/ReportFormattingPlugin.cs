using System.ComponentModel;
using Microsoft.Extensions.Logging;

namespace ResearchAgent.Plugins.Synthesis;

/// <summary>
/// Synthesis plugin — provides structured output formatting tools.
/// Used by the Synthesis Agent in the final phase of the research workflow.
/// </summary>
public sealed class ReportFormattingPlugin
{
    private readonly ILogger _logger;

    public ReportFormattingPlugin(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<ReportFormattingPlugin>();
    }

    [Description("Format a research report section in Markdown with proper heading, content, and citations.")]
    public string FormatSection(
        [Description("The section heading")] string heading,
        [Description("The section content in plain text")] string content,
        [Description("Comma-separated list of source IDs used in this section")] string sourceIds)
    {
        _logger.LogInformation("[TOOL] FormatSection — heading=\"{Heading}\", contentLen={ContentLen}, sourceIds=\"{SourceIds}\"",
            heading, content.Length, sourceIds);

        var ids = sourceIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var citations = string.Join(", ", ids.Select((id, i) => $"[{i + 1}]"));

        var section = $"""
            ## {heading}

            {content}

            *Sources: {citations}*

            ---
            """;

        _logger.LogDebug("[TOOL] FormatSection done — {OutputChars} chars, {CitationCount} citations",
            section.Length, ids.Length);

        return section;
    }

    [Description("Generate a bibliography/references section from a list of sources.")]
    public string FormatBibliography(
        [Description("Pipe-separated list of source entries, each in format 'id|title|url'")] string sources)
    {
        _logger.LogInformation("[TOOL] FormatBibliography — inputLen={InputLen}", sources.Length);

        var entries = sources.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var formatted = entries.Select((entry, i) =>
        {
            var parts = entry.Split('|');
            return parts.Length >= 3
                ? $"[{i + 1}] {parts[1].Trim()} — {parts[2].Trim()}"
                : $"[{i + 1}] {entry}";
        });

        var bibliography = $"""
            ## References

            {string.Join("\n", formatted)}
            """;

        _logger.LogInformation("[TOOL] FormatBibliography done — {EntryCount} entries, {OutputChars} chars",
            entries.Length, bibliography.Length);

        return bibliography;
    }
}
