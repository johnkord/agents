using Forge.Core;
using Serilog;

namespace Forge.Tests;

public class ObservationPipelineTests
{
    private static AgentOptions CreateOptions(int observationMaxLines = 200, int observationMaxChars = 10_000) => new()
    {
        Model = "test-model",
        ObservationMaxLines = observationMaxLines,
        ObservationMaxChars = observationMaxChars,
    };

    [Fact]
    public void Process_ShortOutput_PassesThrough()
    {
        var result = ObservationPipeline.Process("read_file", "hello world", CreateOptions());

        Assert.Equal("hello world", result);
    }

    [Fact]
    public void Process_EmptyOutput_ReturnsPlaceholder()
    {
        var result = ObservationPipeline.Process("read_file", "", CreateOptions());

        Assert.Equal("(no output)", result);
    }

    [Fact]
    public void Process_NullOutput_ReturnsPlaceholder()
    {
        var result = ObservationPipeline.Process("read_file", null!, CreateOptions());

        Assert.Equal("(no output)", result);
    }

    [Fact]
    public void Process_LargeByChars_Truncates()
    {
        var bigText = new string('x', 15_000);

        var result = ObservationPipeline.Process("read_file", bigText, CreateOptions());

        Assert.True(result.Text.Length < bigText.Length);
        Assert.Contains("truncated", result.Text);
        Assert.Contains("15,000 total characters", result.Text);
    }

    [Fact]
    public void Process_ManyLines_Truncates()
    {
        var lines = string.Join("\n", Enumerable.Range(1, 300).Select(i => $"line {i}"));

        var result = ObservationPipeline.Process("read_file", lines, CreateOptions());

        Assert.Contains("truncated", result);
        Assert.Contains("200 of 300 lines", result);
    }

    [Fact]
    public void Process_UsesConfiguredLimits()
    {
        var lines = string.Join("\n", Enumerable.Range(1, 5).Select(i => $"line {i}"));

        var result = ObservationPipeline.Process("read_file", lines, CreateOptions(observationMaxLines: 3));

        Assert.Contains("truncated", result);
        Assert.Contains("3 of 5 lines", result);
    }

    [Fact]
    public void Process_StackTrace_Compacts()
    {
        var trace = "System.InvalidOperationException: Something went wrong\n"
            + "   at MyApp.Service.DoWork() in /src/Service.cs:line 42\n"
            + "   at MyApp.Controller.Handle() in /src/Controller.cs:line 15\n"
            + "   at MyApp.Middleware.Invoke() in /src/Middleware.cs:line 88\n"
            + "   at MyApp.Startup.Configure() in /src/Startup.cs:line 20\n"
            + "   at Microsoft.AspNetCore.Hosting.Internal.WebHost.Run()\n"
            + "   at Microsoft.AspNetCore.Hosting.Internal.WebHost.Start()\n"
            + "   at Microsoft.AspNetCore.Hosting.Internal.WebHost.Init()\n"
            + "   at System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start()\n"
            + "   at System.Threading.Tasks.Task.RunContinuations()\n"
            + "   at System.Threading.ThreadPoolWorkQueue.Dispatch()\n"
            + "   at System.Threading.PortableThreadPool.WorkerThread()\n";

        var result = ObservationPipeline.Process("run_bash_command", trace, CreateOptions());

        Assert.Contains("EXCEPTION:", result.Text);
        Assert.Contains("InvalidOperationException", result.Text);
        Assert.Contains("top 5 frames", result.Text);
        Assert.DoesNotContain("ThreadPoolWorkQueue", result.Text); // frame 10 — should be trimmed
        Assert.True(result.Text.Length < trace.Length,
            $"Compacted ({result.Text.Length}) should be shorter than original ({trace.Length})");
    }

    [Fact]
    public void Process_NormalOutput_NotTreatedAsStackTrace()
    {
        var normal = "Build succeeded.\n0 warnings\n0 errors\n";

        var result = ObservationPipeline.Process("run_bash_command", normal, CreateOptions());

        Assert.Equal(normal, result);
        Assert.DoesNotContain("EXCEPTION", result);
    }

    // ── P0.1: Tool-result disk spill ───────────────────────────────────────

    private static ToolResultSpillStore CreateSpillStore(out string dir)
    {
        dir = Path.Combine(Path.GetTempPath(), "forge-spill-test-" + Guid.NewGuid().ToString("N"));
        return new ToolResultSpillStore(dir, new LoggerConfiguration().CreateLogger());
    }

    [Fact]
    public void Process_AboveSpillThreshold_WritesFullContentToDiskAndEmbedsPath()
    {
        var spill = CreateSpillStore(out var dir);
        try
        {
            var opts = new AgentOptions
            {
                Model = "test",
                ObservationMaxChars = 1_000,
                ToolResultSpillThresholdChars = 500,
            };
            var big = new string('x', 2_000);

            var result = ObservationPipeline.Process("run_bash_command", big, opts, spill);

            Assert.Contains("Full output saved to:", result);
            var spilledFiles = Directory.GetFiles(dir);
            Assert.Single(spilledFiles);
            Assert.Equal(big, File.ReadAllText(spilledFiles[0]));
            Assert.Contains(spilledFiles[0], result);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Process_BelowSpillThreshold_DoesNotSpill()
    {
        var spill = CreateSpillStore(out var dir);
        try
        {
            var opts = new AgentOptions
            {
                Model = "test",
                ObservationMaxChars = 100,
                ToolResultSpillThresholdChars = 1_000,
            };
            var text = new string('y', 500); // over observation limit, under spill

            var result = ObservationPipeline.Process("read_file", text, opts, spill);

            Assert.Contains("truncated", result);
            Assert.DoesNotContain("Full output saved to:", result);
            Assert.Empty(Directory.GetFiles(dir));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Process_NoSpillStore_BehavesLikeBefore()
    {
        var opts = new AgentOptions { Model = "test", ObservationMaxChars = 100, ToolResultSpillThresholdChars = 50 };
        var text = new string('z', 1_000);

        var result = ObservationPipeline.Process("read_file", text, opts, spillStore: null);

        Assert.Contains("truncated", result);
        Assert.DoesNotContain("Full output saved to:", result);
    }

    [Fact]
    public void Process_SpillThresholdZero_Disabled()
    {
        var spill = CreateSpillStore(out var dir);
        try
        {
            var opts = new AgentOptions
            {
                Model = "test",
                ObservationMaxChars = 100,
                ToolResultSpillThresholdChars = 0,
            };
            var text = new string('a', 1_000);

            var result = ObservationPipeline.Process("read_file", text, opts, spill);

            Assert.DoesNotContain("Full output saved to:", result);
            Assert.Empty(Directory.GetFiles(dir));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    // ── P2.5: structured ObservationResult tagging ─────────────────────────

    [Fact]
    public void Process_NoGating_TagIsNull()
    {
        var result = ObservationPipeline.Process("read_file", "hello", CreateOptions());
        Assert.Null(result.Tag);
        Assert.Null(result.SpillPath);
        Assert.False(result.WasTruncated);
    }

    [Fact]
    public void Process_TruncationWithoutSpill_TagIsTruncated()
    {
        var result = ObservationPipeline.Process("read_file", new string('x', 15_000), CreateOptions());
        Assert.Equal("truncated", result.Tag);
        Assert.True(result.WasTruncated);
        Assert.Null(result.SpillPath);
    }

    [Fact]
    public void Process_SpillOverride_TagIsSpilled()
    {
        var spill = CreateSpillStore(out var dir);
        try
        {
            var opts = new AgentOptions
            {
                Model = "test",
                ObservationMaxChars = 100,
                ToolResultSpillThresholdChars = 50,
            };
            var text = new string('a', 1_000);

            var result = ObservationPipeline.Process("read_file", text, opts, spill);

            Assert.Equal("spilled", result.Tag);
            Assert.NotNull(result.SpillPath);
            Assert.True(result.WasTruncated);
            Assert.Contains("Full output saved to:", result.Text);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}
