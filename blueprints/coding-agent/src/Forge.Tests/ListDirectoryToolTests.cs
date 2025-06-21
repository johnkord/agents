using McpServer.Tools;

namespace Forge.Tests;

public class ListDirectoryToolTests : IDisposable
{
    private readonly string _tempDir;

    public ListDirectoryToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"forge-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void ListDirectory_ShowsFilesAndDirs()
    {
        File.WriteAllText(Path.Combine(_tempDir, "file.txt"), "");
        Directory.CreateDirectory(Path.Combine(_tempDir, "subdir"));

        var result = ListDirectoryTool.ListDirectory(_tempDir);

        Assert.Contains("subdir/", result);
        Assert.Contains("file.txt", result);
    }

    [Fact]
    public void ListDirectory_EmptyDir_ReturnsMessage()
    {
        var result = ListDirectoryTool.ListDirectory(_tempDir);

        Assert.Equal("(empty directory)", result);
    }

    [Fact]
    public void ListDirectory_NotFound_ReturnsError()
    {
        var result = ListDirectoryTool.ListDirectory(Path.Combine(_tempDir, "nope"));

        Assert.StartsWith("Error: Directory not found", result);
    }

    [Fact]
    public void ListDirectory_InvalidPath_ReturnsFriendlyError()
    {
        var result = ListDirectoryTool.ListDirectory("\0bad-path");

        Assert.StartsWith("Error: Unable to list directory", result);
    }

    public void Dispose() => Directory.Delete(_tempDir, true);
}
