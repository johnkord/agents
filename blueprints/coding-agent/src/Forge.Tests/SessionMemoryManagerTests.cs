using Forge.Core;
using Forge.Core.SessionMemory;
using Serilog;

namespace Forge.Tests;

public class SessionMemoryManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ILogger _logger = new LoggerConfiguration().CreateLogger();

    public SessionMemoryManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "forge-session-mem-tests-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private static StepRecord Step(int n, int prompt = 500, int completion = 500, string? thought = null)
        => new()
        {
            StepNumber = n,
            Timestamp = DateTimeOffset.UtcNow,
            Thought = thought ?? $"Step {n} thought",
            PromptTokens = prompt,
            CompletionTokens = completion,
        };

    private SessionMemoryManager NewManager(
        SessionMemoryExtractor extractor,
        int minInitTokens = 1000,
        int stepsBetweenUpdates = 3,
        int failureThreshold = 3,
        bool persistRaw = false)
    {
        var opts = new SessionMemoryOptions
        {
            Enabled = true,
            MinInitTokens = minInitTokens,
            StepsBetweenUpdates = stepsBetweenUpdates,
            FailureThreshold = failureThreshold,
            PersistRawResponses = persistRaw,
        };
        return new SessionMemoryManager(opts, extractor, new AuxiliaryCallLimiter(failureThreshold), _tempDir, _logger);
    }

    private static SessionMemoryExtractor FakeExtractor(
        int startStep = 0,
        Action<SessionMemoryExtractionRequest>? onCall = null)
    {
        int callCount = 0;
        return (req, ct) =>
        {
            onCall?.Invoke(req);
            callCount++;
            return Task.FromResult(new SessionMemoryExtractionResult
            {
                Snapshot = new SessionMemorySnapshot
                {
                    TaskDescription = req.Task,
                    CurrentState = $"After extraction #{callCount}",
                    Worklog = req.Steps.Select(s => $"Step {s.StepNumber}").ToList(),
                    LastExtractedStep = req.Steps.Count > 0 ? req.Steps[^1].StepNumber : startStep,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    ExtractionCount = callCount,
                },
                PromptTokens = 100,
                CompletionTokens = 50,
            });
        };
    }

    [Fact]
    public async Task No_extraction_before_init_token_threshold()
    {
        var calls = 0;
        var extractor = FakeExtractor(onCall: _ => calls++);
        var mgr = NewManager(extractor, minInitTokens: 10_000);

        // Four small steps, well under the init threshold.
        var steps = new List<StepRecord> { Step(0), Step(1), Step(2), Step(3) };

        foreach (var s in steps)
            await mgr.MaybeExtractAsync("test task", steps.Take(s.StepNumber + 1).ToList(), totalTokensSoFar: 500, CancellationToken.None);

        Assert.Equal(0, calls);
        Assert.Null(mgr.Snapshot);
    }

    [Fact]
    public async Task First_extraction_fires_when_token_threshold_crossed()
    {
        var calls = 0;
        var extractor = FakeExtractor(onCall: _ => calls++);
        var mgr = NewManager(extractor, minInitTokens: 1000);

        var steps = new List<StepRecord> { Step(0) };
        await mgr.MaybeExtractAsync("test", steps, totalTokensSoFar: 1500, CancellationToken.None);

        Assert.Equal(1, calls);
        Assert.NotNull(mgr.Snapshot);
        Assert.Equal(0, mgr.Snapshot!.LastExtractedStep);
    }

    [Fact]
    public async Task Update_cadence_is_steps_between_updates_after_init()
    {
        var calls = 0;
        var extractor = FakeExtractor(onCall: _ => calls++);
        var mgr = NewManager(extractor, minInitTokens: 1000, stepsBetweenUpdates: 3);

        var allSteps = new List<StepRecord>();
        for (int i = 0; i < 10; i++)
        {
            allSteps.Add(Step(i));
            await mgr.MaybeExtractAsync("test", allSteps.ToList(), totalTokensSoFar: 2000 + i * 100, CancellationToken.None);
        }

        // Steps 0..9: first extraction on step 0 (budget 2000 ≥ 1000). Then every 3 steps.
        // So: step 0 (init), step 3, step 6, step 9 → 4 extractions.
        Assert.Equal(4, calls);
    }

    [Fact]
    public async Task Persists_markdown_and_json_to_session_directory()
    {
        var extractor = FakeExtractor();
        var mgr = NewManager(extractor, minInitTokens: 1000);

        await mgr.MaybeExtractAsync("my task", new List<StepRecord> { Step(0) }, 1500, CancellationToken.None);

        var md = Path.Combine(_tempDir, "summary.md");
        var json = Path.Combine(_tempDir, "summary.json");
        Assert.True(File.Exists(md));
        Assert.True(File.Exists(json));

        var mdText = await File.ReadAllTextAsync(md);
        Assert.Contains("# Session Memory", mdText);
        Assert.Contains("## Task", mdText);
        Assert.Contains("## Current State", mdText);
        Assert.Contains("## Worklog", mdText);
        Assert.Contains("my task", mdText);
    }

    [Fact]
    public async Task Circuit_breaker_disables_after_threshold_failures()
    {
        int attempted = 0;
        SessionMemoryExtractor failing = (req, ct) =>
        {
            attempted++;
            throw new InvalidOperationException("boom");
        };
        var mgr = NewManager(failing, minInitTokens: 1000, stepsBetweenUpdates: 1, failureThreshold: 3);

        var allSteps = new List<StepRecord>();
        for (int i = 0; i < 10; i++)
        {
            allSteps.Add(Step(i));
            await mgr.MaybeExtractAsync("t", allSteps.ToList(), 5000, CancellationToken.None);
        }

        Assert.Equal(3, attempted);   // breaker trips after 3 failures
        Assert.True(mgr.Disabled);
        Assert.Null(mgr.Snapshot);
    }

    [Fact]
    public async Task Passes_previous_snapshot_and_from_index_on_second_extraction()
    {
        SessionMemoryExtractionRequest? secondRequest = null;
        int calls = 0;
        SessionMemoryExtractor extractor = (req, ct) =>
        {
            calls++;
            if (calls == 2) secondRequest = req;
            return Task.FromResult(new SessionMemoryExtractionResult
            {
                Snapshot = new SessionMemorySnapshot
                {
                    TaskDescription = "t",
                    CurrentState = $"after {calls}",
                    LastExtractedStep = req.Steps[^1].StepNumber,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    ExtractionCount = calls,
                },
            });
        };
        var mgr = NewManager(extractor, minInitTokens: 1000, stepsBetweenUpdates: 2);

        var allSteps = new List<StepRecord>();
        for (int i = 0; i < 5; i++)
        {
            allSteps.Add(Step(i));
            await mgr.MaybeExtractAsync("t", allSteps.ToList(), 5000, CancellationToken.None);
        }

        Assert.True(calls >= 2, $"expected at least 2 calls, got {calls}");
        Assert.NotNull(secondRequest);
        Assert.NotNull(secondRequest!.PreviousSummary);
        Assert.Equal(0, secondRequest.FromStepIndex); // first snapshot covered step 0
    }

    [Fact]
    public void StripAnalysisBlock_removes_leading_analysis_section()
    {
        var raw = "<analysis>\nThinking out loud…\nMulti-line analysis.\n</analysis>\n<summary>{\"x\":1}</summary>";
        var stripped = SessionMemoryManager.StripAnalysisBlock(raw);
        Assert.DoesNotContain("Thinking out loud", stripped);
        Assert.Contains("<summary>", stripped);
    }

    [Fact]
    public void StripAnalysisBlock_case_insensitive()
    {
        var raw = "<ANALYSIS>foo</Analysis>bar";
        Assert.Equal("bar", SessionMemoryManager.StripAnalysisBlock(raw));
    }

    [Fact]
    public void RenderMarkdown_shows_none_placeholder_for_empty_lists()
    {
        var snap = new SessionMemorySnapshot
        {
            TaskDescription = "t",
            CurrentState = "s",
            LastExtractedStep = 0,
            UpdatedAt = DateTimeOffset.UtcNow,
            ExtractionCount = 1,
        };
        var md = SessionMemoryManager.RenderMarkdown(snap);
        Assert.Contains("_(none)_", md);
    }
}
