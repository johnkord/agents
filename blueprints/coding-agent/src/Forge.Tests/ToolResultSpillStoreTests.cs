using Forge.Core;
using Serilog;

namespace Forge.Tests;

public class ToolResultSpillStoreTests
{
    private static ILogger CreateLogger() => new LoggerConfiguration().CreateLogger();

    [Fact]
    public void Spill_CreatesFileWithContent()
    {
        var dir = Path.Combine(Path.GetTempPath(), "forge-spill-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new ToolResultSpillStore(dir, CreateLogger());
            var path = store.Spill("read_file", "hello big output");
            Assert.True(File.Exists(path));
            Assert.Equal("hello big output", File.ReadAllText(path));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Spill_MultipleTimes_ProducesDistinctFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), "forge-spill-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new ToolResultSpillStore(dir, CreateLogger());
            var p1 = store.Spill("read_file", "a");
            var p2 = store.Spill("read_file", "b");
            Assert.NotEqual(p1, p2);
            Assert.Equal(2, Directory.GetFiles(dir).Length);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Spill_SanitizesToolName()
    {
        var dir = Path.Combine(Path.GetTempPath(), "forge-spill-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new ToolResultSpillStore(dir, CreateLogger());
            var path = store.Spill("evil/../name with spaces", "x");
            Assert.True(File.Exists(path));
            // Directory has only this file — not traversed
            var parent = Path.GetDirectoryName(path);
            Assert.Equal(dir, parent);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void GcOldSessions_RemovesStaleDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), "forge-spill-gc-" + Guid.NewGuid().ToString("N"));
        try
        {
            var oldDir = Path.Combine(root, "old");
            var newDir = Path.Combine(root, "new");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);
            File.WriteAllText(Path.Combine(oldDir, "f.txt"), "x");
            File.WriteAllText(Path.Combine(newDir, "f.txt"), "x");
            // Backdate the old directory
            Directory.SetLastWriteTimeUtc(oldDir, DateTime.UtcNow - TimeSpan.FromDays(30));

            ToolResultSpillStore.GcOldSessions(root, TimeSpan.FromDays(7), CreateLogger());

            Assert.False(Directory.Exists(oldDir));
            Assert.True(Directory.Exists(newDir));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void GcOldSessions_MissingRoot_NoThrow()
    {
        // Should silently no-op when root doesn't exist
        ToolResultSpillStore.GcOldSessions("/nonexistent/forge-gc-" + Guid.NewGuid().ToString("N"),
            TimeSpan.FromDays(1), CreateLogger());
    }
}
