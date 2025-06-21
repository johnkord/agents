using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpServer.Tools;

/// <summary>
/// Persistent file-based memory with three scopes:
///   /memories/         — user-level (survives across workspaces/conversations)
///   /memories/session/  — current conversation only (volatile)
///   /memories/repo/     — repository-scoped facts
///
/// Research basis:
///   - Anatomy of Agentic Memory (2026): hierarchical multi-tier is the proven pattern
///   - AMemGym (2026): write/read failures increase over long interactions — tool must
///     return explicit confirmation with line numbers, not just "OK"
///   - AGENTSYS (2026): schema-validated boundaries — paths restricted to /memories/ scope
/// </summary>
[McpServerToolType]
public static class MemoryTool
{
    // Physical root on disk — configurable via FORGE_MEMORY_ROOT env var
    private static readonly string MemoryRoot =
        Environment.GetEnvironmentVariable("FORGE_MEMORY_ROOT")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".forge", "memories");

    private const int MaxFileSize = 100_000; // 100KB per file
    private const int MaxFiles = 500;        // per scope

    [McpServerTool, Description(
        "Manage persistent memory files organized in three scopes. " +
        "Commands: 'view' (read file or list directory), 'create' (new file, requires fileText), " +
        "'str_replace' (find/replace exact string, requires oldStr + newStr), " +
        "'insert' (add text at line number, requires insertLine + insertText), " +
        "'delete' (remove file or directory), 'rename' (move file, requires newPath). " +
        "Paths must start with /memories/. Scopes: /memories/ (user), /memories/session/ (conversation), /memories/repo/ (repository).")]
    public static string Memory(
        [Description("The operation: 'view', 'create', 'str_replace', 'insert', 'delete', or 'rename'.")] string command,
        [Description("Path starting with /memories/ (e.g. '/memories/notes.md', '/memories/session/').")] string path,
        [Description("Content for 'create'. Required for create.")] string? fileText = null,
        [Description("For 'str_replace': exact string to find. Must appear exactly once.")] string? oldStr = null,
        [Description("For 'str_replace': replacement string.")] string? newStr = null,
        [Description("For 'insert': 0-based line number (0 = before first line).")] int? insertLine = null,
        [Description("For 'insert': text to insert at that line.")] string? insertText = null,
        [Description("For 'rename': new path (must stay within same scope).")] string? newPath = null,
        [Description("For 'view': optional [startLine, endLine] range (1-indexed).")] int[]? viewRange = null)
    {
        // Validate path stays within /memories/
        if (!ValidatePath(path, out var physicalPath, out var error))
            return error;

        return command.ToLowerInvariant() switch
        {
            "view" => View(physicalPath, path, viewRange),
            "create" => Create(physicalPath, path, fileText),
            "str_replace" => StrReplace(physicalPath, path, oldStr, newStr),
            "insert" => Insert(physicalPath, path, insertLine, insertText),
            "delete" => Delete(physicalPath, path),
            "rename" => Rename(physicalPath, path, newPath),
            _ => $"Unknown command '{command}'. Valid commands: view, create, str_replace, insert, delete, rename.",
        };
    }

    private static string View(string physicalPath, string logicalPath, int[]? viewRange)
    {
        if (Directory.Exists(physicalPath))
        {
            var entries = new List<string>();
            foreach (var dir in Directory.GetDirectories(physicalPath))
                entries.Add(Path.GetFileName(dir) + "/");
            foreach (var file in Directory.GetFiles(physicalPath))
                entries.Add(Path.GetFileName(file));

            if (entries.Count == 0)
                return $"Directory '{logicalPath}' is empty.";
            return string.Join("\n", entries);
        }

        if (!File.Exists(physicalPath))
            return $"Not found: '{logicalPath}'. Use 'view' on the parent directory to see what exists.";

        var lines = File.ReadAllLines(physicalPath);

        if (viewRange is { Length: 2 })
        {
            int start = Math.Max(1, viewRange[0]);
            int end = Math.Min(lines.Length, viewRange[1]);
            if (start > end)
                return $"Invalid range [{start}, {end}]. File has {lines.Length} lines.";
            var slice = lines[(start - 1)..end];
            return $"Lines {start}-{end} of {lines.Length}:\n" + string.Join("\n", slice);
        }

        return string.Join("\n", lines);
    }

    private static string Create(string physicalPath, string logicalPath, string? fileText)
    {
        if (fileText is null)
            return "Error: 'fileText' is required for create.";

        if (fileText.Length > MaxFileSize)
            return $"Error: Content exceeds maximum file size ({MaxFileSize:N0} characters).";

        if (File.Exists(physicalPath))
            return $"Error: File already exists at '{logicalPath}'. Use 'str_replace' or 'insert' to modify, or 'delete' first.";

        var dir = Path.GetDirectoryName(physicalPath);
        if (dir is not null)
            Directory.CreateDirectory(dir);

        File.WriteAllText(physicalPath, fileText);
        var lineCount = fileText.Split('\n').Length;
        return $"Created '{logicalPath}' ({lineCount} lines, {fileText.Length} characters).";
    }

    private static string StrReplace(string physicalPath, string logicalPath, string? oldStr, string? newStr)
    {
        if (oldStr is null)
            return "Error: 'oldStr' is required for str_replace.";
        if (newStr is null)
            return "Error: 'newStr' is required for str_replace.";

        if (!File.Exists(physicalPath))
            return $"Error: File not found at '{logicalPath}'.";

        var content = File.ReadAllText(physicalPath);
        var count = CountOccurrences(content, oldStr);

        if (count == 0)
        {
            // Help the agent understand what's actually there
            var preview = content.Length > 500 ? content[..500] + "..." : content;
            return $"Error: oldStr not found in '{logicalPath}'. File content preview:\n{preview}";
        }
        if (count > 1)
            return $"Error: oldStr appears {count} times in '{logicalPath}'. It must appear exactly once. Add more context to make it unique.";

        var newContent = content.Replace(oldStr, newStr);
        File.WriteAllText(physicalPath, newContent);

        // Find the line number of the replacement for confirmation
        var linesBeforeChange = content[..content.IndexOf(oldStr, StringComparison.Ordinal)].Split('\n').Length;
        var newLineCount = newContent.Split('\n').Length;
        return $"Replaced in '{logicalPath}' at line {linesBeforeChange}. File now has {newLineCount} lines.";
    }

    private static string Insert(string physicalPath, string logicalPath, int? insertLine, string? insertText)
    {
        if (insertLine is null)
            return "Error: 'insertLine' is required for insert.";
        if (insertText is null)
            return "Error: 'insertText' is required for insert.";

        if (!File.Exists(physicalPath))
            return $"Error: File not found at '{logicalPath}'.";

        var lines = File.ReadAllLines(physicalPath).ToList();
        var line = Math.Clamp(insertLine.Value, 0, lines.Count);
        var newLines = insertText.Split('\n');

        lines.InsertRange(line, newLines);
        File.WriteAllLines(physicalPath, lines);

        return $"Inserted {newLines.Length} line(s) at line {line} in '{logicalPath}'. File now has {lines.Count} lines.";
    }

    private static string Delete(string physicalPath, string logicalPath)
    {
        if (Directory.Exists(physicalPath))
        {
            Directory.Delete(physicalPath, recursive: true);
            return $"Deleted directory '{logicalPath}' and all contents.";
        }

        if (!File.Exists(physicalPath))
            return $"Error: Not found at '{logicalPath}'.";

        File.Delete(physicalPath);
        return $"Deleted '{logicalPath}'.";
    }

    private static string Rename(string physicalPath, string logicalPath, string? newPath)
    {
        if (newPath is null)
            return "Error: 'newPath' is required for rename.";

        if (!ValidatePath(newPath, out var newPhysicalPath, out var error))
            return error;

        // Enforce same scope
        var oldScope = GetScope(logicalPath);
        var newScope = GetScope(newPath);
        if (oldScope != newScope)
            return $"Error: Cannot rename across scopes ('{oldScope}' → '{newScope}'). Delete and recreate instead.";

        if (!File.Exists(physicalPath) && !Directory.Exists(physicalPath))
            return $"Error: Not found at '{logicalPath}'.";

        var newDir = Path.GetDirectoryName(newPhysicalPath);
        if (newDir is not null)
            Directory.CreateDirectory(newDir);

        if (Directory.Exists(physicalPath))
            Directory.Move(physicalPath, newPhysicalPath);
        else
            File.Move(physicalPath, newPhysicalPath);

        return $"Renamed '{logicalPath}' → '{newPath}'.";
    }

    // ── Path validation: confine all operations to the memory root ────────────

    private static bool ValidatePath(string logicalPath, out string physicalPath, out string error)
    {
        physicalPath = "";
        error = "";

        if (string.IsNullOrWhiteSpace(logicalPath))
        {
            error = "Error: Path cannot be empty.";
            return false;
        }

        if (!logicalPath.StartsWith("/memories/") && logicalPath != "/memories")
        {
            error = "Error: Path must start with '/memories/'. Valid scopes: /memories/, /memories/session/, /memories/repo/.";
            return false;
        }

        // Strip the /memories/ prefix and map to physical path
        var relative = logicalPath == "/memories"
            ? ""
            : logicalPath["/memories/".Length..];

        // Block path traversal
        if (relative.Contains("..") || relative.Contains('\0'))
        {
            error = "Error: Path traversal not allowed.";
            return false;
        }

        physicalPath = Path.GetFullPath(Path.Combine(MemoryRoot, relative));

        // Double-check the resolved path is still under the memory root
        var resolvedRoot = Path.GetFullPath(MemoryRoot);
        if (!physicalPath.StartsWith(resolvedRoot, StringComparison.OrdinalIgnoreCase))
        {
            error = "Error: Resolved path escapes the memory directory.";
            return false;
        }

        return true;
    }

    private static string GetScope(string logicalPath)
    {
        if (logicalPath.StartsWith("/memories/session/")) return "session";
        if (logicalPath.StartsWith("/memories/repo/")) return "repo";
        return "user";
    }

    private static int CountOccurrences(string text, string search)
    {
        int count = 0, index = 0;
        while ((index = text.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += search.Length;
        }
        return count;
    }
}
