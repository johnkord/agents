using McpServer.Tools;

namespace Forge.Tests;

public class GetProjectSetupInfoToolTests
{
    [Fact]
    public void DetectsDotNetProject()
    {
        var dir = Path.Combine(Path.GetTempPath(), "forge-projtest-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "MyApp.csproj"), "<Project></Project>");
            File.WriteAllText(Path.Combine(dir, "appsettings.json"), "{}");

            var result = GetProjectSetupInfoTool.GetProjectSetupInfo(dir);
            Assert.Contains("dotnet", result.ToLowerInvariant());
            Assert.Contains("C#", result);
            Assert.Contains("dotnet build", result);
            Assert.Contains("dotnet test", result);
            Assert.Contains("NuGet", result);
            Assert.Contains("appsettings.json", result);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void DetectsNodeProject()
    {
        var dir = Path.Combine(Path.GetTempPath(), "forge-projtest-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "package.json"), "{}");
            File.WriteAllText(Path.Combine(dir, "tsconfig.json"), "{}");

            var result = GetProjectSetupInfoTool.GetProjectSetupInfo(dir);
            Assert.Contains("node", result.ToLowerInvariant());
            Assert.Contains("JavaScript/TypeScript", result);
            Assert.Contains("tsconfig.json", result);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void DetectsPythonProject()
    {
        var dir = Path.Combine(Path.GetTempPath(), "forge-projtest-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "pyproject.toml"), "[build]");

            var result = GetProjectSetupInfoTool.GetProjectSetupInfo(dir);
            Assert.Contains("python", result.ToLowerInvariant());
            Assert.Contains("pytest", result);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void DetectsMultipleProjectTypes()
    {
        var dir = Path.Combine(Path.GetTempPath(), "forge-projtest-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "package.json"), "{}");
            File.WriteAllText(Path.Combine(dir, "Dockerfile"), "FROM alpine");

            var result = GetProjectSetupInfoTool.GetProjectSetupInfo(dir);
            Assert.Contains("2 project type", result);
            Assert.Contains("node", result.ToLowerInvariant());
            Assert.Contains("docker", result.ToLowerInvariant());
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void UnrecognizedProject_ListsFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), "forge-projtest-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "readme.txt"), "nothing");

            var result = GetProjectSetupInfoTool.GetProjectSetupInfo(dir);
            Assert.Contains("No recognized project type", result);
            Assert.Contains("readme.txt", result);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void NonexistentDirectory_ReturnsError()
    {
        var result = GetProjectSetupInfoTool.GetProjectSetupInfo("/nonexistent/path");
        Assert.Contains("not found", result.ToLowerInvariant());
    }
}
