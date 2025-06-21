using McpServer.Tools;

namespace Forge.Tests;

public class CreateDirectoryToolTests : IDisposable
{
    private readonly string _tempDir;

    public CreateDirectoryToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"forge-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void CreateDirectory_CreatesDirectory()
    {
        var path = Path.Combine(_tempDir, "new-dir");

        var result = CreateDirectoryTool.CreateDirectory(path);

        Assert.StartsWith("Directory created:", result);
        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public void CreateDirectory_CreatesParentDirectories()
    {
        var path = Path.Combine(_tempDir, "sub", "deep", "new-dir");

        CreateDirectoryTool.CreateDirectory(path);

        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public void CreateDirectory_InvalidPath_ReturnsFriendlyError()
    {
        var result = CreateDirectoryTool.CreateDirectory("\0bad-path");

        Assert.StartsWith("Error: Unable to create directory", result);
    }

    public void Dispose() => Directory.Delete(_tempDir, true);
}
