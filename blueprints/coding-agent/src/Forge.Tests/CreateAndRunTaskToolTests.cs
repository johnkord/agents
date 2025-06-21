using McpServer.Tools;

namespace Forge.Tests;

public class CreateAndRunTaskToolTests
{
    [Fact]
    public async Task RunsEchoCommand()
    {
        var result = await CreateAndRunTaskTool.CreateAndRunTask(
            "Echo test", "echo hello", timeoutSeconds: 10);
        Assert.Contains("Exit code: 0", result);
        Assert.Contains("hello", result);
        Assert.Contains("succeeded", result);
    }

    [Fact]
    public async Task CaptuersExitCode_OnFailure()
    {
        var result = await CreateAndRunTaskTool.CreateAndRunTask(
            "Fail test", "exit 42", timeoutSeconds: 10);
        Assert.Contains("Exit code: 42", result);
        Assert.Contains("failed", result);
    }

    [Fact]
    public async Task BlocksDangerousCommands()
    {
        var result = await CreateAndRunTaskTool.CreateAndRunTask(
            "Bad", "sudo rm -rf /", timeoutSeconds: 10);
        Assert.Contains("blocked", result.ToLowerInvariant());
    }

    [Fact]
    public async Task NonexistentWorkingDirectory_Errors()
    {
        var result = await CreateAndRunTaskTool.CreateAndRunTask(
            "Test", "echo hi", workingDirectory: "/nonexistent");
        Assert.Contains("not found", result.ToLowerInvariant());
    }

    [Fact]
    public async Task EmptyCommand_Errors()
    {
        var result = await CreateAndRunTaskTool.CreateAndRunTask("Test", "");
        Assert.Contains("required", result.ToLowerInvariant());
    }

    [Fact]
    public async Task ClassifiesSafeCommands()
    {
        var result = await CreateAndRunTaskTool.CreateAndRunTask(
            "Build", "dotnet build --version", timeoutSeconds: 10);
        Assert.Contains("safe", result);
    }
}
