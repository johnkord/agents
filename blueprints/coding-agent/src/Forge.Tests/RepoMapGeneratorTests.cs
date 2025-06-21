namespace Forge.Tests;

using Forge.Core;

public class RepoMapGeneratorTests : IDisposable
{
    private readonly string _testDir;

    public RepoMapGeneratorTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "forge-repomap-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void Generate_DotNetProject_DetectsComponents()
    {
        // Create a minimal .NET structure
        File.WriteAllText(Path.Combine(_testDir, "MyApp.sln"), "Microsoft Visual Studio Solution File");
        var srcDir = Path.Combine(_testDir, "src", "MyApp");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "MyApp.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(srcDir, "Program.cs"), "class Program {}");

        var testDir = Path.Combine(_testDir, "tests", "MyApp.Tests");
        Directory.CreateDirectory(testDir);
        File.WriteAllText(Path.Combine(testDir, "MyApp.Tests.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include=\"Microsoft.NET.Test.Sdk\" /></ItemGroup></Project>");

        var result = RepoMapGenerator.Generate(_testDir);

        Assert.NotNull(result);
        Assert.Contains("Repository:", result);
        Assert.Contains(".NET", result);
        Assert.Contains("MyApp.sln", result);
        Assert.Contains("MyApp", result);
        Assert.Contains("dotnet build", result);
        Assert.Contains("Test projects:", result);
    }

    [Fact]
    public void Generate_NodeProject_DetectsPackage()
    {
        File.WriteAllText(Path.Combine(_testDir, "package.json"),
            """{"name": "my-app", "scripts": {"build": "tsc", "test": "jest"}}""");
        File.WriteAllText(Path.Combine(_testDir, "tsconfig.json"), "{}");

        var result = RepoMapGenerator.Generate(_testDir);

        Assert.NotNull(result);
        Assert.Contains("Node.js", result);
        Assert.Contains("my-app", result);
        Assert.Contains("build", result);
        Assert.Contains("test", result);
        Assert.Contains("TypeScript: yes", result);
    }

    [Fact]
    public void Generate_PythonProject_Detected()
    {
        File.WriteAllText(Path.Combine(_testDir, "pyproject.toml"), "[build-system]");
        Directory.CreateDirectory(Path.Combine(_testDir, ".venv"));

        var result = RepoMapGenerator.Generate(_testDir);

        Assert.NotNull(result);
        Assert.Contains("Python", result);
        Assert.Contains("pyproject.toml", result);
        Assert.Contains(".venv", result);
        Assert.Contains("pytest", result);
    }

    [Fact]
    public void Generate_RustProject_Detected()
    {
        File.WriteAllText(Path.Combine(_testDir, "Cargo.toml"), "[package]");

        var result = RepoMapGenerator.Generate(_testDir);

        Assert.NotNull(result);
        Assert.Contains("Rust", result);
        Assert.Contains("cargo build", result);
    }

    [Fact]
    public void Generate_GoProject_Detected()
    {
        File.WriteAllText(Path.Combine(_testDir, "go.mod"), "module example.com/app");

        var result = RepoMapGenerator.Generate(_testDir);

        Assert.NotNull(result);
        Assert.Contains("Go", result);
        Assert.Contains("go build", result);
    }

    [Fact]
    public void Generate_Monorepo_DetectsMultipleTypes()
    {
        File.WriteAllText(Path.Combine(_testDir, "package.json"), """{"name": "mono"}""");
        File.WriteAllText(Path.Combine(_testDir, "pyproject.toml"), "[build]");

        var result = RepoMapGenerator.Generate(_testDir);

        Assert.NotNull(result);
        Assert.Contains("Monorepo", result);
        Assert.Contains("Node.js", result);
        Assert.Contains("Python", result);
    }

    [Fact]
    public void Generate_EmptyDirectory_ReturnsNull()
    {
        var result = RepoMapGenerator.Generate(_testDir);
        Assert.Null(result);
    }

    [Fact]
    public void Generate_NonexistentDirectory_ReturnsNull()
    {
        var result = RepoMapGenerator.Generate("/nonexistent/path");
        Assert.Null(result);
    }

    [Fact]
    public void GenerateOrLoadCached_CachesAndReloads()
    {
        File.WriteAllText(Path.Combine(_testDir, "Cargo.toml"), "[package]");
        var cachePath = Path.Combine(_testDir, ".forge", "REPO.md");

        // First call: generates and caches
        var result1 = RepoMapGenerator.GenerateOrLoadCached(_testDir, cachePath);
        Assert.NotNull(result1);
        Assert.True(File.Exists(cachePath));

        // Second call: loads from cache (same content)
        var result2 = RepoMapGenerator.GenerateOrLoadCached(_testDir, cachePath);
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void GenerateOrLoadCached_InvalidatesOnBuildFileChange()
    {
        var cargoPath = Path.Combine(_testDir, "Cargo.toml");
        File.WriteAllText(cargoPath, "[package]");
        var cachePath = Path.Combine(_testDir, ".forge", "REPO.md");

        // Generate initial cache
        RepoMapGenerator.GenerateOrLoadCached(_testDir, cachePath);
        Assert.True(File.Exists(cachePath));

        // Touch the build file to be newer than cache
        Thread.Sleep(100); // ensure different mtime
        File.SetLastWriteTimeUtc(cargoPath, DateTime.UtcNow);

        // Should regenerate (cache is stale)
        var result = RepoMapGenerator.GenerateOrLoadCached(_testDir, cachePath);
        Assert.NotNull(result);
        Assert.Contains("Rust", result);
    }
}
