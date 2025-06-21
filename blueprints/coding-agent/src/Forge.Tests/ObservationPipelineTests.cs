using Forge.Core;

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

        Assert.True(result.Length < bigText.Length);
        Assert.Contains("truncated", result);
        Assert.Contains("15,000 total characters", result);
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

        Assert.Contains("EXCEPTION:", result);
        Assert.Contains("InvalidOperationException", result);
        Assert.Contains("top 5 frames", result);
        Assert.DoesNotContain("ThreadPoolWorkQueue", result); // frame 10 — should be trimmed
        Assert.True(result.Length < trace.Length,
            $"Compacted ({result.Length}) should be shorter than original ({trace.Length})");
    }

    [Fact]
    public void Process_NormalOutput_NotTreatedAsStackTrace()
    {
        var normal = "Build succeeded.\n0 warnings\n0 errors\n";

        var result = ObservationPipeline.Process("run_bash_command", normal, CreateOptions());

        Assert.Equal(normal, result);
        Assert.DoesNotContain("EXCEPTION", result);
    }
}
