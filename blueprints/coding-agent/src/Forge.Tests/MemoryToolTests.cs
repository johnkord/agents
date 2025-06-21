using McpServer.Tools;

namespace Forge.Tests;

[Collection("MemoryEnvironment")]  // Prevents parallel execution with ManageTodosToolTests
public class MemoryToolTests : IDisposable
{
    private readonly string _testRoot;
    private readonly string _originalEnv;

    public MemoryToolTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "forge-memory-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testRoot);
        _originalEnv = Environment.GetEnvironmentVariable("FORGE_MEMORY_ROOT") ?? "";
        Environment.SetEnvironmentVariable("FORGE_MEMORY_ROOT", _testRoot);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("FORGE_MEMORY_ROOT", _originalEnv);
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, true);
    }

    [Fact]
    public void Create_And_View_File()
    {
        var result = MemoryTool.Memory("create", "/memories/test.md", fileText: "Hello\nWorld");
        Assert.Contains("Created", result);
        Assert.Contains("2 lines", result);

        var content = MemoryTool.Memory("view", "/memories/test.md");
        Assert.Equal("Hello\nWorld", content);
    }

    [Fact]
    public void View_Directory_Lists_Contents()
    {
        MemoryTool.Memory("create", "/memories/file1.md", fileText: "a");
        MemoryTool.Memory("create", "/memories/file2.md", fileText: "b");

        var listing = MemoryTool.Memory("view", "/memories");
        Assert.Contains("file1.md", listing);
        Assert.Contains("file2.md", listing);
    }

    [Fact]
    public void View_WithRange_ReturnsSubset()
    {
        MemoryTool.Memory("create", "/memories/lines.md", fileText: "line1\nline2\nline3\nline4\nline5");

        var result = MemoryTool.Memory("view", "/memories/lines.md", viewRange: [2, 4]);
        Assert.Contains("Lines 2-4", result);
        Assert.Contains("line2", result);
        Assert.Contains("line4", result);
        Assert.DoesNotContain("line1", result);
        Assert.DoesNotContain("line5", result);
    }

    [Fact]
    public void Create_DuplicateFails()
    {
        MemoryTool.Memory("create", "/memories/dup.md", fileText: "first");
        var result = MemoryTool.Memory("create", "/memories/dup.md", fileText: "second");
        Assert.Contains("already exists", result);
    }

    [Fact]
    public void StrReplace_Works()
    {
        MemoryTool.Memory("create", "/memories/replace.md", fileText: "foo bar baz");
        var result = MemoryTool.Memory("str_replace", "/memories/replace.md", oldStr: "bar", newStr: "qux");
        Assert.Contains("Replaced", result);

        var content = MemoryTool.Memory("view", "/memories/replace.md");
        Assert.Equal("foo qux baz", content);
    }

    [Fact]
    public void StrReplace_NotFoundShowsPreview()
    {
        MemoryTool.Memory("create", "/memories/nf.md", fileText: "actual content here");
        var result = MemoryTool.Memory("str_replace", "/memories/nf.md", oldStr: "nonexistent", newStr: "x");
        Assert.Contains("not found", result);
        Assert.Contains("actual content here", result); // shows preview
    }

    [Fact]
    public void StrReplace_MultipleOccurrencesFails()
    {
        MemoryTool.Memory("create", "/memories/multi.md", fileText: "aaa bbb aaa");
        var result = MemoryTool.Memory("str_replace", "/memories/multi.md", oldStr: "aaa", newStr: "x");
        Assert.Contains("appears 2 times", result);
    }

    [Fact]
    public void Insert_AtBeginning()
    {
        MemoryTool.Memory("create", "/memories/ins.md", fileText: "line1\nline2");
        MemoryTool.Memory("insert", "/memories/ins.md", insertLine: 0, insertText: "header");

        var content = MemoryTool.Memory("view", "/memories/ins.md");
        Assert.StartsWith("header", content);
    }

    [Fact]
    public void Delete_File()
    {
        MemoryTool.Memory("create", "/memories/del.md", fileText: "x");
        var result = MemoryTool.Memory("delete", "/memories/del.md");
        Assert.Contains("Deleted", result);

        var view = MemoryTool.Memory("view", "/memories/del.md");
        Assert.Contains("Not found", view);
    }

    [Fact]
    public void Rename_File()
    {
        MemoryTool.Memory("create", "/memories/old.md", fileText: "content");
        var result = MemoryTool.Memory("rename", "/memories/old.md", newPath: "/memories/new.md");
        Assert.Contains("Renamed", result);

        var content = MemoryTool.Memory("view", "/memories/new.md");
        Assert.Equal("content", content);
    }

    [Fact]
    public void Rename_AcrossScopesFails()
    {
        MemoryTool.Memory("create", "/memories/session/task.md", fileText: "x");
        var result = MemoryTool.Memory("rename", "/memories/session/task.md", newPath: "/memories/repo/task.md");
        Assert.Contains("Cannot rename across scopes", result);
    }

    [Fact]
    public void PathTraversal_Blocked()
    {
        var result = MemoryTool.Memory("view", "/memories/../etc/passwd");
        Assert.Contains("traversal", result.ToLowerInvariant());
    }

    [Fact]
    public void InvalidPath_Rejected()
    {
        var result = MemoryTool.Memory("view", "/etc/passwd");
        Assert.Contains("must start with '/memories/'", result);
    }

    [Fact]
    public void SessionScope_Works()
    {
        var result = MemoryTool.Memory("create", "/memories/session/context.md", fileText: "session data");
        Assert.Contains("Created", result);

        var content = MemoryTool.Memory("view", "/memories/session/context.md");
        Assert.Equal("session data", content);
    }

    [Fact]
    public void Create_MissingFileText_Errors()
    {
        var result = MemoryTool.Memory("create", "/memories/nope.md");
        Assert.Contains("fileText", result);
    }

    [Fact]
    public void UnknownCommand_Errors()
    {
        var result = MemoryTool.Memory("frobnicate", "/memories/x.md");
        Assert.Contains("Unknown command", result);
    }
}
