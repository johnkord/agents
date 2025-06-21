using McpServer.Tools;

namespace Forge.Tests;

public class ReplaceStringInFileToolTests : IDisposable
{
    private readonly string _tempDir;

    public ReplaceStringInFileToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"forge-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void ReplaceString_SingleOccurrence_Succeeds()
    {
        var path = Path.Combine(_tempDir, "code.py");
        File.WriteAllText(path, "def greet(name)\n    print(name)\n");

        var result = ReplaceStringInFileTool.ReplaceStringInFile(
            path, "def greet(name)", "def greet(name):");

        Assert.StartsWith("Replaced 1 occurrence", result);
        Assert.Contains("def greet(name):", File.ReadAllText(path));
    }

    [Fact]
    public void ReplaceString_NotFound_ReturnsError()
    {
        var path = Path.Combine(_tempDir, "code.py");
        File.WriteAllText(path, "hello world");

        var result = ReplaceStringInFileTool.ReplaceStringInFile(
            path, "nonexistent text", "replacement");

        Assert.StartsWith("Error: oldString not found", result);
    }

    [Fact]
    public void ReplaceString_MultipleOccurrences_ReturnsError()
    {
        var path = Path.Combine(_tempDir, "code.py");
        File.WriteAllText(path, "foo\nbar\nfoo\n");

        var result = ReplaceStringInFileTool.ReplaceStringInFile(
            path, "foo", "baz");

        Assert.Contains("found 2 times", result);
        // Original content preserved
        Assert.Equal("foo\nbar\nfoo\n", File.ReadAllText(path));
    }

    [Fact]
    public void ReplaceString_FileNotFound_ReturnsError()
    {
        var result = ReplaceStringInFileTool.ReplaceStringInFile(
            Path.Combine(_tempDir, "nope.txt"), "a", "b");

        Assert.StartsWith("Error: File not found", result);
    }

    public void Dispose() => Directory.Delete(_tempDir, true);
}
