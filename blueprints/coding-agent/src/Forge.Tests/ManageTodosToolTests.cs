using McpServer.Tools;

namespace Forge.Tests;

[Collection("MemoryEnvironment")]  // Prevents parallel execution with MemoryToolTests
public class ManageTodosToolTests : IDisposable
{
    private readonly string _testRoot;
    private readonly string _originalEnv;

    public ManageTodosToolTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "forge-todos-test-" + Guid.NewGuid().ToString("N")[..8]);
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
    public void ValidTodoList_Succeeds()
    {
        var json = """[{"id": 1, "title": "Task A", "status": "not-started"}, {"id": 2, "title": "Task B", "status": "completed"}]""";
        var result = ManageTodosTool.ManageTodos(json);
        Assert.Contains("2 items", result);
        Assert.Contains("1 not-started", result);
        Assert.Contains("1 completed", result);
    }

    [Fact]
    public void InvalidJson_ReturnsError()
    {
        var result = ManageTodosTool.ManageTodos("not json");
        Assert.Contains("Invalid JSON", result);
    }

    [Fact]
    public void EmptyArray_ReturnsError()
    {
        var result = ManageTodosTool.ManageTodos("[]");
        Assert.Contains("empty", result);
    }

    [Fact]
    public void DuplicateIds_ReturnsError()
    {
        var json = """[{"id": 1, "title": "A", "status": "not-started"}, {"id": 1, "title": "B", "status": "not-started"}]""";
        var result = ManageTodosTool.ManageTodos(json);
        Assert.Contains("Duplicate id", result);
    }

    [Fact]
    public void InvalidStatus_ReturnsError()
    {
        var json = """[{"id": 1, "title": "A", "status": "pending"}]""";
        var result = ManageTodosTool.ManageTodos(json);
        Assert.Contains("invalid status", result);
    }

    [Fact]
    public void MultipleInProgress_Warns()
    {
        var json = """[{"id": 1, "title": "A", "status": "in-progress"}, {"id": 2, "title": "B", "status": "in-progress"}]""";
        var result = ManageTodosTool.ManageTodos(json);
        Assert.Contains("Warning", result);
        Assert.Contains("2 items are in-progress", result);
    }

    [Fact]
    public void EmptyTitle_ReturnsError()
    {
        var json = """[{"id": 1, "title": "", "status": "not-started"}]""";
        var result = ManageTodosTool.ManageTodos(json);
        Assert.Contains("no title", result);
    }

    [Fact]
    public void PersistsToDisk()
    {
        // Set env var immediately before calling the tool to avoid test ordering issues
        Environment.SetEnvironmentVariable("FORGE_MEMORY_ROOT", _testRoot);
        var json = """[{"id": 1, "title": "Persistent", "status": "not-started"}]""";
        var result = ManageTodosTool.ManageTodos(json);
        Assert.Contains("1 items", result);

        var filePath = Path.Combine(_testRoot, "session", "todos.json");
        Assert.True(File.Exists(filePath), $"Expected file at {filePath}");
        var content = File.ReadAllText(filePath);
        Assert.Contains("Persistent", content);
    }
}
