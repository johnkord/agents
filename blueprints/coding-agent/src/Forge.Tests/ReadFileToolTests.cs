using McpServer.Tools;

namespace Forge.Tests;

public class ReadFileToolTests : IDisposable
{
    private readonly string _tempDir;

    public ReadFileToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"forge-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task ReadFile_ReturnsContents()
    {
        var path = Path.Combine(_tempDir, "test.txt");
        await File.WriteAllTextAsync(path, "hello world");

        var result = await ReadFileTool.ReadFile(path);

        Assert.Equal("hello world", result);
    }

    [Fact]
    public async Task ReadFile_NotFound_ReturnsError()
    {
        var result = await ReadFileTool.ReadFile(Path.Combine(_tempDir, "nonexistent.txt"));

        Assert.StartsWith("Error: File not found", result);
    }

    [Fact]
    public async Task ReadFile_WithLineRange_ReturnsSubset()
    {
        var path = Path.Combine(_tempDir, "lines.txt");
        await File.WriteAllTextAsync(path, "line1\nline2\nline3\nline4\nline5\n");

        var result = await ReadFileTool.ReadFile(path, startLine: 2, endLine: 4);

        Assert.Contains("Lines 2-4 of", result);
        Assert.Contains("line2", result);
        Assert.Contains("line4", result);
        Assert.DoesNotContain("line1", result);
        Assert.DoesNotContain("line5", result);
    }

    [Fact]
    public async Task ReadFile_StartLineBeyondEnd_ReturnsError()
    {
        var path = Path.Combine(_tempDir, "short.txt");
        await File.WriteAllTextAsync(path, "one\ntwo\n");

        var result = await ReadFileTool.ReadFile(path, startLine: 100);

        Assert.StartsWith("Error: startLine", result);
    }

    [Fact]
    public async Task ReadFile_InvalidPath_ReturnsFriendlyError()
    {
        var result = await ReadFileTool.ReadFile("\0bad-path");

        Assert.StartsWith("Error: Unable to read file", result);
    }

    [Fact]
    public async Task ReadFile_NoRange_ReturnsFullFile()
    {
        var path = Path.Combine(_tempDir, "full.txt");
        var content = "first\nsecond\nthird";
        await File.WriteAllTextAsync(path, content);

        var result = await ReadFileTool.ReadFile(path);

        Assert.Equal(content, result);
    }

    public void Dispose() => Directory.Delete(_tempDir, true);
}
