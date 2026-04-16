using Serilog;

namespace Forge.Core;

/// <summary>
/// Writes large tool results to disk under a per-session directory and returns
/// the spill path. The agent can then read_file(spillPath, startLine, endLine) to
/// retrieve arbitrary slices — paying the token cost only for what it actually needs,
/// instead of dragging the full output through every subsequent LLM turn.
///
/// Sessions older than a configurable age are garbage-collected at startup.
/// </summary>
public sealed class ToolResultSpillStore
{
    private readonly string _sessionDir;
    private readonly ILogger _logger;
    private int _counter;

    public string SessionDirectory => _sessionDir;

    public ToolResultSpillStore(string sessionDir, ILogger logger)
    {
        _sessionDir = sessionDir ?? throw new ArgumentNullException(nameof(sessionDir));
        _logger = logger;
        Directory.CreateDirectory(_sessionDir);
    }

    /// <summary>
    /// Write <paramref name="rawContent"/> to a new file in this session's spill directory
    /// and return the absolute path. Thread-safe.
    /// </summary>
    public string Spill(string toolName, string rawContent)
    {
        var idx = Interlocked.Increment(ref _counter);
        var safeName = Sanitize(toolName);
        var fileName = $"{idx:D4}-{safeName}.txt";
        var path = Path.Combine(_sessionDir, fileName);
        File.WriteAllText(path, rawContent);
        _logger.Debug("Spilled {Bytes} bytes from {Tool} → {Path}", rawContent.Length, toolName, path);
        return path;
    }

    /// <summary>
    /// Delete spill directories under <paramref name="root"/> whose last-write time is older
    /// than <paramref name="maxAge"/>. Silently swallows IO errors per directory.
    /// </summary>
    public static void GcOldSessions(string root, TimeSpan maxAge, ILogger logger)
    {
        try
        {
            if (!Directory.Exists(root)) return;
            var cutoff = DateTime.UtcNow - maxAge;
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                try
                {
                    var info = new DirectoryInfo(dir);
                    if (info.LastWriteTimeUtc < cutoff)
                    {
                        Directory.Delete(dir, recursive: true);
                        logger.Debug("Spill GC: removed {Dir}", dir);
                    }
                }
                catch (Exception ex)
                {
                    logger.Debug(ex, "Spill GC: failed to remove {Dir}", dir);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "Spill GC: enumeration failed for {Root}", root);
        }
    }

    private static string Sanitize(string name)
    {
        if (string.IsNullOrEmpty(name)) return "tool";
        var chars = name.Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_').ToArray();
        var result = new string(chars);
        return result.Length > 40 ? result[..40] : result;
    }
}
