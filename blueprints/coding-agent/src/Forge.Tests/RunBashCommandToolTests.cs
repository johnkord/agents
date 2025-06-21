using McpServer.Tools;

namespace Forge.Tests;

public class RunBashCommandToolTests
{
    [Fact]
    public async Task RunBashCommand_SimpleCommand_ReturnsOutput()
    {
        var result = await RunBashCommandTool.RunBashCommand(
            "echo hello", "test echo", isBackground: false);

        Assert.Contains("Exit code: 0", result);
        Assert.Contains("hello", result);
    }

    [Fact]
    public async Task RunBashCommand_FailingCommand_ReturnsExitCode()
    {
        var result = await RunBashCommandTool.RunBashCommand(
            "exit 42", "test failure", isBackground: false);

        Assert.Contains("Exit code: 42", result);
    }

    [Fact]
    public async Task RunBashCommand_WithPipes_Works()
    {
        var result = await RunBashCommandTool.RunBashCommand(
            "echo 'line1\nline2\nline3' | wc -l", "test pipes", isBackground: false);

        Assert.Contains("Exit code: 0", result);
        Assert.Contains("3", result);
    }

    [Fact]
    public async Task RunBashCommand_WithSingleQuotes_Works()
    {
        var result = await RunBashCommandTool.RunBashCommand(
            "echo 'it'\\''s a test'", "test single quotes", isBackground: false);

        Assert.Contains("Exit code: 0", result);
    }

    [Fact]
    public async Task RunBashCommand_WithDoubleQuotes_Works()
    {
        var result = await RunBashCommandTool.RunBashCommand(
            "echo \"hello world\"", "test double quotes", isBackground: false);

        Assert.Contains("hello world", result);
    }

    [Fact]
    public async Task RunBashCommand_WithAndChain_Works()
    {
        var result = await RunBashCommandTool.RunBashCommand(
            "echo first && echo second", "test chained commands", isBackground: false);

        Assert.Contains("Exit code: 0", result);
        Assert.Contains("first", result);
        Assert.Contains("second", result);
    }

    [Fact]
    public async Task RunBashCommand_Timeout_KillsProcess()
    {
        var result = await RunBashCommandTool.RunBashCommand(
            "sleep 30", "test timeout", isBackground: false, timeout: 500);

        Assert.Contains("timed out", result);
    }

    [Fact]
    public async Task RunBashCommand_WorkingDirectory_Respected()
    {
        var result = await RunBashCommandTool.RunBashCommand(
            "pwd", "test cwd", isBackground: false, workingDirectory: "/tmp");

        Assert.Contains("/tmp", result);
    }

    [Fact]
    public async Task RunBashCommand_Background_ReturnsImmediately()
    {
        var result = await RunBashCommandTool.RunBashCommand(
            "sleep 60", "test background", isBackground: true);

        Assert.Contains("Background process started", result);
        Assert.Contains("PID:", result);
    }

    [Fact]
    public async Task RunBashCommand_StderrCaptured()
    {
        var result = await RunBashCommandTool.RunBashCommand(
            "echo error >&2", "test stderr", isBackground: false);

        Assert.Contains("STDERR:", result);
        Assert.Contains("error", result);
    }

    [Fact]
    public async Task RunBashCommand_DotnetBuild_Works()
    {
        // This is the exact command that was failing before BUG-1 fix
        var result = await RunBashCommandTool.RunBashCommand(
            "dotnet --version", "test dotnet availability", isBackground: false);

        Assert.Contains("Exit code: 0", result);
    }
}
