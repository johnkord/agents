using McpServer.Tools;

namespace Forge.Tests;

public class FileSearchToolTests : IDisposable
{
    private readonly string _tempDir;

    public FileSearchToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"forge-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        // Create test file structure
        File.WriteAllText(Path.Combine(_tempDir, "readme.md"), "hello");
        File.WriteAllText(Path.Combine(_tempDir, "app.cs"), "class App {}");
        Directory.CreateDirectory(Path.Combine(_tempDir, "sub"));
        File.WriteAllText(Path.Combine(_tempDir, "sub", "deep.cs"), "class Deep {}");
        File.WriteAllText(Path.Combine(_tempDir, "sub", "data.json"), "{}");
    }

    [Fact]
    public void FileSearch_FindsByExtension()
    {
        var result = FileSearchTool.FileSearch("**/*.cs", rootPath: _tempDir);

        Assert.Contains("app.cs", result);
        Assert.Contains("deep.cs", result);
        Assert.DoesNotContain("readme.md", result);
    }

    [Fact]
    public void FileSearch_FindsByFilename()
    {
        var result = FileSearchTool.FileSearch("**/*.json", rootPath: _tempDir);

        Assert.Contains("data.json", result);
        Assert.DoesNotContain("app.cs", result);
    }

    [Fact]
    public void FileSearch_RespectsMaxResults()
    {
        var result = FileSearchTool.FileSearch("**/*.*", rootPath: _tempDir, maxResults: 2);

        Assert.Contains("2", result); // should show "Showing 2 of 2+"
    }

    [Fact]
    public void FileSearch_NoMatches_ReturnsMessage()
    {
        var result = FileSearchTool.FileSearch("**/*.xyz", rootPath: _tempDir);

        Assert.Contains("No files found", result);
    }

    [Fact]
    public void FileSearch_InvalidRootPath_ReturnsError()
    {
        var result = FileSearchTool.FileSearch("**/*.cs", rootPath: "/nonexistent/path");

        Assert.StartsWith("Error:", result);
    }

    [Fact]
    public void FileSearch_SkipsGitAndBinDirs()
    {
        // Create dirs that should be skipped
        var gitDir = Path.Combine(_tempDir, ".git");
        Directory.CreateDirectory(gitDir);
        File.WriteAllText(Path.Combine(gitDir, "hidden.cs"), "");

        var binDir = Path.Combine(_tempDir, "bin");
        Directory.CreateDirectory(binDir);
        File.WriteAllText(Path.Combine(binDir, "output.cs"), "");

        var result = FileSearchTool.FileSearch("**/*.cs", rootPath: _tempDir);

        Assert.DoesNotContain(".git", result);
        Assert.DoesNotContain("bin", result);
        Assert.Contains("app.cs", result);
    }

    public void Dispose() => Directory.Delete(_tempDir, true);
}
