namespace Forge.Tests;

using McpServer.Tools;
using Xunit.Abstractions;

public class ExploreCodebaseToolTests(ITestOutputHelper output)
{
    [Fact]
    public void ReturnsStructuredSummary()
    {
        var dir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "AuthService.cs"),
                "namespace App;\n\npublic class AuthService\n{\n    public bool ValidateToken(string token)\n    {\n        return token.Length > 0;\n    }\n}");
            File.WriteAllText(Path.Combine(dir, "UserController.cs"),
                "namespace App;\n\npublic class UserController\n{\n    private readonly AuthService _auth;\n    public void Login(string token)\n    {\n        _auth.ValidateToken(token);\n    }\n}");

            var result = ExploreCodebaseTool.ExploreCodebase(
                "authentication token validation", "Find auth flow", focusPath: dir);

            Assert.Contains("Exploration:", result);
            Assert.Contains("AuthService.cs", result);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void QuickDepth_LimitsFiles()
    {
        var dir = CreateTempDir();
        try
        {
            for (int i = 0; i < 10; i++)
                File.WriteAllText(Path.Combine(dir, $"Service{i}.cs"),
                    $"public class Service{i} {{ public void Handle() {{ /* auth check */ }} }}");

            var result = ExploreCodebaseTool.ExploreCodebase(
                "auth", "Quick auth search", focusPath: dir, depth: "quick");

            // Quick should show ≤3 file headers
            var fileHeaders = result.Split('\n').Count(l => l.StartsWith("## "));
            Assert.True(fileHeaders <= 3, $"Quick depth showed {fileHeaders} files, expected ≤3");
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void EmptyQuery_ReturnsError()
    {
        var result = ExploreCodebaseTool.ExploreCodebase("", "test");
        Assert.Contains("Error:", result);
    }

    [Fact]
    public void NoResults_FallsThrough()
    {
        var dir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "empty.cs"), "// nothing here");
            var result = ExploreCodebaseTool.ExploreCodebase(
                "nonexistent_xyz_term", "Not found test", focusPath: dir);
            Assert.Contains("No results", result);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void ExtractsStructure()
    {
        var dir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "Handler.cs"),
                "namespace App;\n\npublic class RequestHandler\n{\n    public void Process() { }\n    private int Count() { return 0; }\n}");

            var result = ExploreCodebaseTool.ExploreCodebase(
                "request handler", "Find handler", focusPath: dir);

            Assert.Contains("File Map", result);
            Assert.Contains("RequestHandler", result);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void SearchInternal_ReturnsStructuredResults()
    {
        var dir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "AuthService.cs"),
                "public class AuthService { public void Validate() { } }");
            File.WriteAllText(Path.Combine(dir, "UserRepo.cs"),
                "public class UserRepo { // auth dependency }");

            var (ranked, bridges, terms) = SearchCodebaseTool.SearchInternal(dir, "auth service");

            Assert.True(ranked.Count >= 1);
            Assert.Contains(ranked, r => r.RelativePath.Contains("AuthService"));
            Assert.True(ranked[0].Score > 0);
            Assert.NotEmpty(terms);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void ShowsMatchingLinesWithContext()
    {
        var dir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "Config.cs"),
                "namespace App;\n\npublic class Config\n{\n    // database connection string\n    public string DbConn { get; set; }\n    public int Timeout { get; set; } = 30;\n}");

            var result = ExploreCodebaseTool.ExploreCodebase(
                "database connection", "Find DB config", focusPath: dir);

            Assert.Contains("database", result.ToLowerInvariant());
            Assert.Contains("Config.cs", result);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void ThoroughDepth_ShowsMoreFiles()
    {
        var dir = CreateTempDir();
        try
        {
            for (int i = 0; i < 20; i++)
                File.WriteAllText(Path.Combine(dir, $"Module{i}.cs"),
                    $"public class Module{i} {{ public void Execute() {{ /* auth logic */ }} }}");

            var quickResult = ExploreCodebaseTool.ExploreCodebase(
                "auth", "Quick search", focusPath: dir, depth: "quick");
            var thoroughResult = ExploreCodebaseTool.ExploreCodebase(
                "auth", "Thorough search", focusPath: dir, depth: "thorough");

            var quickFiles = quickResult.Split('\n').Count(l => l.TrimStart().StartsWith("Module") && l.Contains("score:"));
            var thoroughFiles = thoroughResult.Split('\n').Count(l => l.TrimStart().StartsWith("Module") && l.Contains("score:"));

            Assert.True(thoroughFiles > quickFiles,
                $"Thorough ({thoroughFiles} files) should show more than quick ({quickFiles} files)");
        }
        finally { Directory.Delete(dir, true); }
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "forge-explore-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    // ── Experiment B-lite: Output quality against real Forge codebase ──

    private static readonly string ForgeSrc = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Forge.Core"));

    [Fact]
    public void ExperimentB_FindsSessionFilenameHandling()
    {
        if (!Directory.Exists(ForgeSrc)) { output.WriteLine("SKIPPED: Forge.Core source not at expected path"); return; }

        var result = ExploreCodebaseTool.ExploreCodebase(
            "session filename sanitization file naming",
            "Find filename handling",
            focusPath: ForgeSrc,
            depth: "quick");

        output.WriteLine(result);
        output.WriteLine($"\n[Output: {result.Length} chars]");

        // EventLog.cs contains SanitizeFileName — the tool should find it
        var foundEventLog = result.Contains("EventLog");
        output.WriteLine($"Found EventLog.cs? {foundEventLog}");
        Assert.True(foundEventLog, "explore_codebase should find EventLog.cs when searching for filename handling");
    }

    [Fact]
    public void ExperimentB_FindsFailureTaxonomy()
    {
        if (!Directory.Exists(ForgeSrc)) { output.WriteLine("SKIPPED: Forge.Core source not available"); return; }

        var result = ExploreCodebaseTool.ExploreCodebase(
            "failure taxonomy classify nudge recovery",
            "Understand failure handling",
            focusPath: ForgeSrc,
            depth: "medium");

        output.WriteLine(result);
        output.WriteLine($"\n[Output: {result.Length} chars]");

        var foundAgentLoop = result.Contains("AgentLoop");
        var foundFailure = result.Contains("Failure") || result.Contains("failure");
        output.WriteLine($"Found AgentLoop? {foundAgentLoop}");
        output.WriteLine($"Found failure terminology? {foundFailure}");
        Assert.True(foundAgentLoop, "explore_codebase should find AgentLoop.cs for failure taxonomy");
    }

    [Fact]
    public void ExperimentB_FindsToolRegistry()
    {
        if (!Directory.Exists(ForgeSrc)) { output.WriteLine("SKIPPED: Forge.Core source not available"); return; }

        var result = ExploreCodebaseTool.ExploreCodebase(
            "tool registry core tools progressive disclosure",
            "Understand tool management",
            focusPath: ForgeSrc,
            depth: "quick");

        output.WriteLine(result);
        output.WriteLine($"\n[Output: {result.Length} chars]");

        var foundToolRegistry = result.Contains("ToolRegistry");
        output.WriteLine($"Found ToolRegistry? {foundToolRegistry}");
        Assert.True(foundToolRegistry, "explore_codebase should find ToolRegistry.cs");
    }
}
