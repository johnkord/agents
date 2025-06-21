using McpServer.Tools;

namespace Forge.Tests;

public class SearchCodebaseToolTests
{
    [Fact]
    public void FindsFilesByName()
    {
        var dir = Path.Combine(Path.GetTempPath(), "forge-search-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "AuthService.cs"), "public class AuthService { }");
            File.WriteAllText(Path.Combine(dir, "UserRepo.cs"), "public class UserRepo { }");
            File.WriteAllText(Path.Combine(dir, "Program.cs"), "class Program { }");

            var result = SearchCodebaseTool.SearchCodebase("authentication service", "Find auth", rootPath: dir);

            Assert.Contains("AuthService.cs", result);
            // Score: file name + content match for "auth"
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void FindsContentMatches()
    {
        var dir = Path.Combine(Path.GetTempPath(), "forge-search-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Startup.cs"),
                "public class Startup\n{\n    // Configure database connection\n    var conn = new SqlConnection();\n}");
            File.WriteAllText(Path.Combine(dir, "Empty.cs"), "// nothing here");

            var result = SearchCodebaseTool.SearchCodebase("database connection", "Find DB setup", rootPath: dir);

            Assert.Contains("Startup.cs", result);
            Assert.Contains("database", result.ToLowerInvariant());
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ExtractsNearbyStructure()
    {
        var dir = Path.Combine(Path.GetTempPath(), "forge-search-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Handler.cs"),
                "namespace App;\n\npublic class RequestHandler\n{\n    public void HandleLogin(string token)\n    {\n        // validate token\n    }\n}");

            var result = SearchCodebaseTool.SearchCodebase("login token validation", "Find login handler", rootPath: dir);

            Assert.Contains("Handler.cs", result);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void SkipsBinDirectories()
    {
        var dir = Path.Combine(Path.GetTempPath(), "forge-search-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "bin"));
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "bin", "Auth.cs"), "class Auth { // login }");
            File.WriteAllText(Path.Combine(dir, "src", "Auth.cs"), "class Auth { // login }");

            var result = SearchCodebaseTool.SearchCodebase("login", "Find login", rootPath: dir);

            Assert.Contains("src", result);
            Assert.DoesNotContain("bin/Auth", result);
            Assert.DoesNotContain("bin\\Auth", result);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void NoResults_ReportsSearchTerms()
    {
        var dir = Path.Combine(Path.GetTempPath(), "forge-search-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "empty.cs"), "// nothing");

            var result = SearchCodebaseTool.SearchCodebase("nonexistent functionality xyz", "Find xyz", rootPath: dir);

            Assert.Contains("No results found", result);
            Assert.Contains("nonexistent", result);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void InvalidDirectory_ReturnsError()
    {
        var result = SearchCodebaseTool.SearchCodebase("anything", "test", rootPath: "/nonexistent");
        Assert.Contains("not found", result.ToLowerInvariant());
    }
}
