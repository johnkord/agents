using Forge.Core;

namespace Forge.Tests;

public class EventLogTests : IAsyncLifetime, IAsyncDisposable
{
    private readonly string _tempDir;
    private EventLog? _log;

    public EventLogTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"forge-test-{Guid.NewGuid():N}");
    }

    public async Task InitializeAsync()
    {
        _log = await EventLog.CreateAsync(_tempDir, "test task", "gpt-5.4", "/workspace");
    }

    public async Task DisposeAsync()
    {
        if (_log is not null)
            await _log.DisposeAsync();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await DisposeAsync();
    }

    [Fact]
    public void EventLog_CreatesSessionFile()
    {
        Assert.True(File.Exists(_log!.FilePath));
    }

    [Fact]
    public void EventLog_FileIsInSessionsDir()
    {
        Assert.StartsWith(_tempDir, _log!.FilePath);
    }

    [Fact]
    public async Task EventLog_SessionStart_ContainsTask()
    {
        await _log!.DisposeAsync();

        var content = await File.ReadAllTextAsync(_log.FilePath);

        Assert.Contains("session_start", content);
        Assert.Contains("test task", content);
        Assert.Contains("gpt-5.4", content);
        Assert.Contains("/workspace", content);
    }

    [Fact]
    public async Task EventLog_RecordStep_WritesJsonLine()
    {
        var step = new StepRecord
        {
            StepNumber = 0,
            Timestamp = DateTimeOffset.UtcNow,
            Thought = "thinking",
            ToolCalls = [new ToolCallRecord
            {
                ToolName = "read_file",
                Arguments = "{}",
                ResultSummary = "content",
                ResultLength = 100,
                DurationMs = 42.0,
            }],
            PromptTokens = 1000,
            CompletionTokens = 50,
            DurationMs = 500.0,
        };

        await _log!.RecordStepAsync(step);
        await _log.DisposeAsync();

        var lines = await File.ReadAllLinesAsync(_log.FilePath);
        // Line 0 = session_start, Line 1 = step
        Assert.True(lines.Length >= 2);
        Assert.Contains("\"step\"", lines[1]);
        Assert.Contains("read_file", lines[1]);
    }

    [Fact]
    public async Task EventLog_RecordSessionEnd_WritesTotalTokens()
    {
        var result = new AgentResult
        {
            Success = true,
            Output = "done",
            Steps = [],
            TotalPromptTokens = 5000,
            TotalCompletionTokens = 200,
            TotalDurationMs = 3000,
        };

        await _log!.RecordSessionEndAsync(result);
        await _log.DisposeAsync();

        var content = await File.ReadAllTextAsync(_log.FilePath);

        Assert.Contains("session_end", content);
        Assert.Contains("5000", content);
    }

    // ── SanitizeFileName tests ─────────────────────────────────────────────

    [Fact]
    public void SanitizeFileName_ReplacesSpacesWithHyphens()
    {
        var result = EventLog.SanitizeFileName("Write comprehensive tests for the class");

        Assert.DoesNotContain(" ", result);
        Assert.Contains("Write-comprehensive-tests", result);
    }

    [Fact]
    public void SanitizeFileName_ReplacesQuotesAndParens()
    {
        var result = EventLog.SanitizeFileName("Add a comment '// Phase 3 complete' to the top of");

        Assert.DoesNotContain("'", result);
        Assert.Contains("Phase-3-complete", result);
    }

    [Fact]
    public void SanitizeFileName_ReplacesPipeCharacter()
    {
        var result = EventLog.SanitizeFileName("Investigate A|B behavior");

        Assert.DoesNotContain("|", result);
        Assert.Equal("Investigate-A-B-behavior", result);
    }

    [Fact]
    public void SanitizeFileName_ReplacesBackticksBracesAndDollarSigns()
    {
        var result = EventLog.SanitizeFileName("Fix `temp` {value} $now");

        Assert.DoesNotContain("`", result);
        Assert.DoesNotContain("{", result);
        Assert.DoesNotContain("}", result);
        Assert.DoesNotContain("$", result);
        Assert.Equal("Fix-temp-value-now", result);
    }

    [Fact]
    public void SanitizeFileName_CollapsesMultipleHyphens()
    {
        var result = EventLog.SanitizeFileName("Fix   the   bug   now");

        Assert.DoesNotContain("--", result);
    }

    [Fact]
    public void SanitizeFileName_TruncatesLongInput()
    {
        var longInput = new string('a', 100);
        var result = EventLog.SanitizeFileName(longInput);

        Assert.True(result.Length <= 50);
    }

    [Fact]
    public void SanitizeFileName_EmptyInput_ReturnsUnnamed()
    {
        Assert.Equal("unnamed", EventLog.SanitizeFileName(""));
        Assert.Equal("unnamed", EventLog.SanitizeFileName("   "));
    }

    [Fact]
    public void SanitizeFileName_NoLeadingOrTrailingHyphens()
    {
        var result = EventLog.SanitizeFileName("  spaced task  ");

        Assert.False(result.StartsWith('-'));
        Assert.False(result.EndsWith('-'));
    }

    [Fact]
    public void SanitizeFileName_ReplacesSmartPunctuationAndEmoji()
    {
        var result = EventLog.SanitizeFileName("Fix smart quotes “hello” and emoji 😅");

        Assert.Equal("Fix-smart-quotes-hello-and-emoji", result);
    }

    [Fact]
    public void SanitizeFileName_NormalizesAccentedAndCombiningCharacters()
    {
        var result = EventLog.SanitizeFileName("Combine café and naïve");

        Assert.Equal("Combine-cafe-and-naive", result);
    }

    [Fact]
    public void SanitizeFileName_ReplacesPathAndShellPunctuation()
    {
        var result = EventLog.SanitizeFileName("Use a/b c\\d : * ? < > | path");

        Assert.Equal("Use-a-b-c-d-path", result);
    }

    [Fact]
    public void SanitizeFileName_AllSymbols_ReturnsUnnamed()
    {
        var result = EventLog.SanitizeFileName("😅 !!! ###");

        Assert.Equal("unnamed", result);
    }
}
