using System.Text;

namespace Forge.Core;

/// <summary>
/// Generates a compact repository structural map (REPO.md) by analyzing build files.
///
/// Research basis:
///   - RIG (2026): +12.2% accuracy, -53.9% completion time from repo intelligence
///   - Agent Skills Architecture: progressive disclosure — only metadata at startup
///
/// Detects .NET, Node.js, Python, Rust, Go projects. Extracts components,
/// test projects, build/test commands, and key config files.
/// </summary>
public static class RepoMapGenerator
{
    /// <summary>
    /// Generate a REPO.md content string for the given workspace, or null if
    /// no recognizable project structure is found.
    /// </summary>
    public static string? Generate(string workspacePath)
    {
        if (!Directory.Exists(workspacePath))
            return null;

        var sb = new StringBuilder();
        var repoName = Path.GetFileName(workspacePath);
        sb.AppendLine($"# Repository: {repoName}");

        var detections = new List<string>();

        // .NET detection
        var dotnetInfo = DetectDotNet(workspacePath);
        if (dotnetInfo is not null)
        {
            detections.Add(".NET");
            sb.AppendLine(dotnetInfo);
        }

        // Node.js detection
        var nodeInfo = DetectNode(workspacePath);
        if (nodeInfo is not null)
        {
            detections.Add("Node.js");
            sb.AppendLine(nodeInfo);
        }

        // Python detection
        var pythonInfo = DetectPython(workspacePath);
        if (pythonInfo is not null)
        {
            detections.Add("Python");
            sb.AppendLine(pythonInfo);
        }

        // Rust detection
        var rustInfo = DetectRust(workspacePath);
        if (rustInfo is not null)
        {
            detections.Add("Rust");
            sb.AppendLine(rustInfo);
        }

        // Go detection
        var goInfo = DetectGo(workspacePath);
        if (goInfo is not null)
        {
            detections.Add("Go");
            sb.AppendLine(goInfo);
        }

        if (detections.Count == 0)
            return null; // No recognizable project

        // Add type summary at top (after repo name)
        var typeStr = detections.Count == 1
            ? detections[0]
            : $"Monorepo ({string.Join(", ", detections)})";

        // Insert type after first line
        var result = sb.ToString();
        var firstNewline = result.IndexOf('\n');
        result = result[..(firstNewline + 1)] + $"Type: {typeStr}\n" + result[(firstNewline + 1)..];

        return result.TrimEnd();
    }

    /// <summary>
    /// Generate and cache REPO.md to disk. Returns cached version if build files haven't changed.
    /// </summary>
    public static string? GenerateOrLoadCached(string workspacePath, string? cachePath = null)
    {
        cachePath ??= Path.Combine(workspacePath, ".forge", "REPO.md");
        var cacheDir = Path.GetDirectoryName(cachePath);

        // Check if cache is still valid (newer than all build files)
        if (File.Exists(cachePath))
        {
            var cacheTime = File.GetLastWriteTimeUtc(cachePath);
            var buildFiles = FindBuildFiles(workspacePath);
            var latestBuildFile = buildFiles.Any()
                ? buildFiles.Max(f => File.GetLastWriteTimeUtc(f))
                : DateTimeOffset.MinValue.UtcDateTime;

            if (cacheTime > latestBuildFile)
                return File.ReadAllText(cachePath);
        }

        // Generate fresh
        var content = Generate(workspacePath);
        if (content is null)
            return null;

        // Cache to disk
        try
        {
            if (cacheDir is not null)
                Directory.CreateDirectory(cacheDir);
            File.WriteAllText(cachePath, content);
        }
        catch
        {
            // Cache write failure is non-fatal
        }

        return content;
    }

    // ── .NET detection ─────────────────────────────────────────────────────

    private static string? DetectDotNet(string root)
    {
        // Find solution files
        var slnFiles = Directory.GetFiles(root, "*.sln", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(root, "*.slnx", SearchOption.TopDirectoryOnly))
            .ToList();

        // Find csproj files
        var csprojFiles = Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(f => !f.Contains("/bin/") && !f.Contains("/obj/") && !f.Contains("\\bin\\") && !f.Contains("\\obj\\"))
            .ToList();

        if (slnFiles.Count == 0 && csprojFiles.Count == 0)
            return null;

        var sb = new StringBuilder();

        if (slnFiles.Count > 0)
        {
            sb.AppendLine($"Solution: {string.Join(", ", slnFiles.Select(f => Path.GetRelativePath(root, f)))}");
        }

        // Categorize projects
        var projects = new List<(string Path, string Name, bool IsTest, string? TargetFramework)>();
        foreach (var csproj in csprojFiles)
        {
            var name = Path.GetFileNameWithoutExtension(csproj);
            var relativePath = Path.GetRelativePath(root, Path.GetDirectoryName(csproj)!);
            var isTest = IsTestProject(csproj);
            var tfm = ExtractTargetFramework(csproj);
            projects.Add((relativePath, name, isTest, tfm));
        }

        var appProjects = projects.Where(p => !p.IsTest).ToList();
        var testProjects = projects.Where(p => p.IsTest).ToList();

        if (appProjects.Count > 0)
        {
            sb.AppendLine("Components:");
            foreach (var p in appProjects.Take(20))
            {
                var tfmStr = p.TargetFramework is not null ? $" ({p.TargetFramework})" : "";
                var fileCount = CountSourceFiles(Path.Combine(root, p.Path), "*.cs");
                var fileStr = fileCount > 0 ? $", {fileCount} {(fileCount == 1 ? "file" : "files")}" : "";
                sb.AppendLine($"  - {p.Path} — {p.Name}{tfmStr}{fileStr}");
            }
            if (appProjects.Count > 20)
                sb.AppendLine($"  ... and {appProjects.Count - 20} more");
        }

        if (testProjects.Count > 0)
        {
            sb.AppendLine($"Test projects: {string.Join(", ", testProjects.Take(10).Select(p => p.Name))}");
        }

        sb.AppendLine($"Build: dotnet build{(slnFiles.Count > 0 ? $" {Path.GetRelativePath(root, slnFiles[0])}" : "")}");
        if (testProjects.Count > 0)
            sb.AppendLine($"Test: dotnet test{(testProjects.Count == 1 ? $" {testProjects[0].Path}" : "")}");

        return sb.ToString();
    }

    // ── Node.js detection ──────────────────────────────────────────────────

    private static string? DetectNode(string root)
    {
        var packageJson = Path.Combine(root, "package.json");
        if (!File.Exists(packageJson))
            return null;

        var sb = new StringBuilder();

        try
        {
            var content = File.ReadAllText(packageJson);
            using var doc = System.Text.Json.JsonDocument.Parse(content);
            var rootEl = doc.RootElement;

            if (rootEl.TryGetProperty("name", out var nameEl))
                sb.AppendLine($"Package: {nameEl.GetString()}");

            // Detect workspaces (monorepo)
            if (rootEl.TryGetProperty("workspaces", out var workspacesEl))
            {
                sb.AppendLine("Workspaces:");
                foreach (var ws in workspacesEl.EnumerateArray().Take(15))
                    sb.AppendLine($"  - {ws.GetString()}");
            }

            // List scripts
            if (rootEl.TryGetProperty("scripts", out var scriptsEl))
            {
                var scripts = new List<string>();
                foreach (var prop in scriptsEl.EnumerateObject().Take(10))
                    scripts.Add(prop.Name);

                if (scripts.Count > 0)
                    sb.AppendLine($"Scripts: {string.Join(", ", scripts)}");
            }
        }
        catch
        {
            sb.AppendLine("Package: (unable to parse package.json)");
        }

        // Detect TypeScript
        if (File.Exists(Path.Combine(root, "tsconfig.json")))
            sb.AppendLine("TypeScript: yes");

        sb.AppendLine("Build: npm run build");
        sb.AppendLine("Test: npm test");

        return sb.ToString();
    }

    // ── Python detection ───────────────────────────────────────────────────

    private static string? DetectPython(string root)
    {
        var pyproject = Path.Combine(root, "pyproject.toml");
        var setupPy = Path.Combine(root, "setup.py");
        var requirements = Path.Combine(root, "requirements.txt");

        if (!File.Exists(pyproject) && !File.Exists(setupPy) && !File.Exists(requirements))
            return null;

        var sb = new StringBuilder();

        if (File.Exists(pyproject))
            sb.AppendLine("Config: pyproject.toml");
        else if (File.Exists(setupPy))
            sb.AppendLine("Config: setup.py");

        // Detect package manager
        if (File.Exists(Path.Combine(root, "uv.lock")))
            sb.AppendLine("Package manager: uv");
        else if (File.Exists(Path.Combine(root, "poetry.lock")))
            sb.AppendLine("Package manager: poetry");
        else if (File.Exists(Path.Combine(root, "Pipfile")))
            sb.AppendLine("Package manager: pipenv");
        else
            sb.AppendLine("Package manager: pip");

        // Detect venv
        if (Directory.Exists(Path.Combine(root, ".venv")))
            sb.AppendLine("Virtual env: .venv/");

        sb.AppendLine("Test: pytest");

        return sb.ToString();
    }

    // ── Rust detection ─────────────────────────────────────────────────────

    private static string? DetectRust(string root)
    {
        var cargoToml = Path.Combine(root, "Cargo.toml");
        if (!File.Exists(cargoToml))
            return null;

        var sb = new StringBuilder();
        sb.AppendLine("Config: Cargo.toml");
        sb.AppendLine("Build: cargo build");
        sb.AppendLine("Test: cargo test");

        return sb.ToString();
    }

    // ── Go detection ───────────────────────────────────────────────────────

    private static string? DetectGo(string root)
    {
        var goMod = Path.Combine(root, "go.mod");
        if (!File.Exists(goMod))
            return null;

        var sb = new StringBuilder();
        sb.AppendLine("Config: go.mod");
        sb.AppendLine("Build: go build ./...");
        sb.AppendLine("Test: go test ./...");

        return sb.ToString();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static bool IsTestProject(string csprojPath)
    {
        try
        {
            return File.ReadLines(csprojPath)
                .Any(l => l.Contains("Microsoft.NET.Test.Sdk") || l.Contains("xunit") || l.Contains("NUnit") || l.Contains("MSTest"));
        }
        catch { return false; }
    }

    private static string? ExtractTargetFramework(string csprojPath)
    {
        try
        {
            foreach (var line in File.ReadLines(csprojPath))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("<TargetFramework>") && trimmed.EndsWith("</TargetFramework>"))
                    return trimmed["<TargetFramework>".Length..^"</TargetFramework>".Length];
            }
        }
        catch { }
        return null;
    }

    private static int CountSourceFiles(string dirPath, string pattern)
    {
        try
        {
            return Directory.GetFiles(dirPath, pattern, SearchOption.TopDirectoryOnly).Length;
        }
        catch { return 0; }
    }

    private static IReadOnlyList<string> FindBuildFiles(string root)
    {
        var patterns = new[] { "*.sln", "*.slnx", "*.csproj", "package.json", "pyproject.toml", "Cargo.toml", "go.mod" };
        var files = new List<string>();
        foreach (var pattern in patterns)
        {
            try
            {
                files.AddRange(Directory.GetFiles(root, pattern, SearchOption.AllDirectories)
                    .Where(f => !f.Contains("/bin/") && !f.Contains("/obj/") && !f.Contains("/node_modules/")));
            }
            catch { }
        }
        return files;
    }
}
