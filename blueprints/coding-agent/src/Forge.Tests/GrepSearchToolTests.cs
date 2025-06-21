using McpServer.Tools;

namespace Forge.Tests;

public class GrepSearchToolTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _originalDir;

    public GrepSearchToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"forge-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _originalDir = Directory.GetCurrentDirectory();

        // Create test files
        File.WriteAllText(Path.Combine(_tempDir, "app.cs"), "// TODO: fix this\nvar x = 1;\n// TODO: refactor\n");
        File.WriteAllText(Path.Combine(_tempDir, "readme.md"), "# Project\nNothing to see here.\n");
        Directory.CreateDirectory(Path.Combine(_tempDir, "sub"));
        File.WriteAllText(Path.Combine(_tempDir, "sub", "deep.txt"), "TODO: nested match\n");
    }

    [Fact]
    public void GrepSearch_FindsMatches_PlainText()
    {
        var result = GrepSearchTool.GrepSearch("TODO", isRegexp: false, rootPath: _tempDir);

        Assert.Contains("app.cs:1:", result);
        Assert.Contains("app.cs:3:", result);
        Assert.Contains("deep.txt:1:", result);
        Assert.Contains("3 match", result);
    }

    [Fact]
    public void GrepSearch_FindsMatches_Regex()
    {
        var result = GrepSearchTool.GrepSearch(@"TODO.*fix", isRegexp: true, rootPath: _tempDir);

        Assert.Contains("app.cs:1:", result);
        Assert.Contains("1 match", result);
    }

    [Fact]
    public void GrepSearch_NoMatches_ReturnsMessage()
    {
        var result = GrepSearchTool.GrepSearch("ZZZZNOTHERE", isRegexp: false, rootPath: _tempDir);

        Assert.Contains("No matches found", result);
    }

    [Fact]
    public void GrepSearch_InvalidRegex_ReturnsError()
    {
        var result = GrepSearchTool.GrepSearch("[invalid", isRegexp: true, rootPath: _tempDir);

        Assert.StartsWith("Error: Invalid regex", result);
    }

    [Fact]
    public void GrepSearch_RespectsMaxResults()
    {
        var result = GrepSearchTool.GrepSearch("TODO", isRegexp: false, rootPath: _tempDir, maxResults: 1);

        Assert.Contains("Showing 1 of 3", result);
    }

    [Fact]
    public void GrepSearch_RootPath_SearchesCorrectDirectory()
    {
        var result = GrepSearchTool.GrepSearch("nested match", isRegexp: false, rootPath: _tempDir);

        Assert.Contains("deep.txt", result);
    }

    [Fact]
    public void GrepSearch_InvalidRootPath_ReturnsError()
    {
        var result = GrepSearchTool.GrepSearch("test", isRegexp: false, rootPath: "/nonexistent/path");

        Assert.StartsWith("Error: Directory not found", result);
    }

    public void Dispose() => Directory.Delete(_tempDir, true);
}
