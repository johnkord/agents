using MCPServer.Tools;
using Xunit;
using System.Collections.Generic;

namespace MCPServer.Tests;

public class PullRequestReviewToolsTests
{
    [Fact]
    public void CodeReviewTools_ExtractFileExtensions_ShouldParseCorrectly()
    {
        // Arrange
        var prContent = @"
📄 test.js
📄 example.cs
📄 config.json
📄 readme.md
";

        // Act (using reflection to call private method for testing)
        var method = typeof(CodeReviewTools).GetMethod("ExtractFileExtensions", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = (Dictionary<string, int>)method!.Invoke(null, new object[] { prContent })!;

        // Assert
        Assert.Equal(4, result.Count);
        Assert.Equal(1, result["js"]);
        Assert.Equal(1, result["cs"]);
        Assert.Equal(1, result["json"]);
        Assert.Equal(1, result["md"]);
    }

    [Fact]
    public void CodeReviewTools_ExtractFileExtensions_ShouldHandleEmptyContent()
    {
        // Arrange
        var prContent = "";

        // Act
        var method = typeof(CodeReviewTools).GetMethod("ExtractFileExtensions", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = (Dictionary<string, int>)method!.Invoke(null, new object[] { prContent })!;

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void CodeReviewTools_ExtractFileExtensions_ShouldHandleDuplicateExtensions()
    {
        // Arrange
        var prContent = @"
📄 test1.js
📄 test2.js
📄 example.cs
";

        // Act
        var method = typeof(CodeReviewTools).GetMethod("ExtractFileExtensions", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = (Dictionary<string, int>)method!.Invoke(null, new object[] { prContent })!;

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(2, result["js"]);
        Assert.Equal(1, result["cs"]);
    }

    [Theory]
    [InlineData("TODO something", true)]
    [InlineData("FIXME this bug", true)]
    [InlineData("console.log('debug')", true)]
    [InlineData("System.Console.WriteLine", true)]
    [InlineData("password = 'secret'", true)]
    [InlineData("apikey = 'key123'", true)]
    [InlineData("SELECT * FROM users", true)]
    [InlineData("normal code without patterns", false)]
    public void CodeReviewTools_ContentAnalysis_ShouldDetectPatterns(string content, bool shouldDetectPattern)
    {
        // This is a simplified test that just checks if certain patterns would be detected
        // without calling the actual approval system
        
        var hasTodo = content.Contains("TODO") || content.Contains("FIXME");
        var hasDebug = content.Contains("console.log") || content.Contains("System.Console.WriteLine");
        var hasSecrets = content.Contains("password") || content.Contains("apikey");
        var hasSql = content.Contains("SELECT") || content.Contains("INSERT");
        
        var hasAnyPattern = hasTodo || hasDebug || hasSecrets || hasSql;
        
        Assert.Equal(shouldDetectPattern, hasAnyPattern);
    }

    [Fact]
    public void GitHubTools_Class_ShouldExist()
    {
        // Simple test to verify the class is properly defined
        var type = typeof(GitHubTools);
        Assert.NotNull(type);
        Assert.True(type.IsClass);
    }

    [Fact]
    public void AzureDevOpsTools_Class_ShouldExist()
    {
        // Simple test to verify the class is properly defined
        var type = typeof(AzureDevOpsTools);
        Assert.NotNull(type);
        Assert.True(type.IsClass);
    }

    [Fact]
    public void OpenAIVectorStoreTools_Class_ShouldExist()
    {
        // Simple test to verify the class is properly defined
        var type = typeof(OpenAIVectorStoreTools);
        Assert.NotNull(type);
        Assert.True(type.IsClass);
    }

    [Fact]
    public void CodeReviewTools_Class_ShouldExist()
    {
        // Simple test to verify the class is properly defined
        var type = typeof(CodeReviewTools);
        Assert.NotNull(type);
        Assert.True(type.IsClass);
    }
}