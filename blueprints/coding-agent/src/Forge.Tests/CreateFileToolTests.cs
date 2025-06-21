using McpServer.Tools;

namespace Forge.Tests;

public class CreateFileToolTests : IDisposable
{
    private readonly string _tempDir;

    public CreateFileToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"forge-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void CreateFile_CreatesNewFile()
    {
        var path = Path.Combine(_tempDir, "new.txt");

        var result = CreateFileTool.CreateFile(path, "hello");

        Assert.StartsWith("File created:", result);
        Assert.True(File.Exists(path));
        Assert.Equal("hello", File.ReadAllText(path));
    }

    [Fact]
    public void CreateFile_CreatesDirectories()
    {
        var path = Path.Combine(_tempDir, "sub", "deep", "file.txt");

        CreateFileTool.CreateFile(path, "nested");

        Assert.True(File.Exists(path));
        Assert.Equal("nested", File.ReadAllText(path));
    }

    [Fact]
    public void CreateFile_RefusesOverwrite()
    {
        var path = Path.Combine(_tempDir, "existing.txt");
        File.WriteAllText(path, "original");

        var result = CreateFileTool.CreateFile(path, "overwrite attempt");

        Assert.StartsWith("Error: File already exists", result);
        Assert.Equal("original", File.ReadAllText(path));
    }

    [Fact]
    public void CreateFile_InvalidPath_ReturnsFriendlyError()
    {
        var result = CreateFileTool.CreateFile("\0bad-path", "content");

        Assert.StartsWith("Error: Unable to create file", result);
    }

    public void Dispose() => Directory.Delete(_tempDir, true);
}
