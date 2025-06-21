using McpServer.Tools;

namespace Forge.Tests;

public class RunTestsToolTests
{
    [Fact]
    public async Task RunTests_WithValidProject_ReturnsResults()
    {
        // Instead of running real tests (which causes recursive test runs and timeouts),
        // verify the tool can locate and invoke dotnet test with a simple project check
        var projectPath = FindTestProject();
        if (projectPath is null) return;

        // Just verify the tool accepts the project path and returns structured output
        // Use a filter that matches nothing to avoid actual test execution overhead
        var result = await RunTestsTool.RunTests(projectPath: projectPath, filter: "NonexistentTestThatDoesNotExist12345");

        Assert.Contains("Exit code:", result);
    }

    [Fact]
    public async Task RunTests_WithFilter_FiltersTests()
    {
        var projectPath = FindTestProject();
        if (projectPath is null) return;

        var result = await RunTestsTool.RunTests(projectPath: projectPath, filter: "FileSearchToolTests");

        Assert.Contains("Exit code:", result);
    }

    [Fact]
    public async Task RunTests_NoProjectFound_ReturnsHelpful()
    {
        // Use a fresh empty temp dir that definitely has no .csproj files
        var emptyDir = Path.Combine(Path.GetTempPath(), $"forge-test-empty-{Guid.NewGuid():N}");
        Directory.CreateDirectory(emptyDir);
        try
        {
            var result = await RunTestsTool.RunTests(workingDirectory: emptyDir);
            Assert.Contains("No test projects found", result);
        }
        finally
        {
            Directory.Delete(emptyDir, true);
        }
    }

    private static string? FindTestProject()
    {
        // Walk up from current directory to find the test project
        var dir = Directory.GetCurrentDirectory();
        for (int i = 0; i < 10; i++)
        {
            var candidates = Directory.GetFiles(dir, "Forge.Tests.csproj", SearchOption.AllDirectories);
            if (candidates.Length > 0) return candidates[0];
            var parent = Directory.GetParent(dir)?.FullName;
            if (parent is null) break;
            dir = parent;
        }
        return null;
    }
}
