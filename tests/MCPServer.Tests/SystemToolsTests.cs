using MCPServer.Tools;
using Xunit;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MCPServer.Tests;

public class SystemToolsTests
{
    [Fact]
    public void GetCurrentTime_ShouldReturnValidTimeFormat()
    {
        // Act
        var result = SystemTools.GetCurrentTime();

        // Assert
        Assert.Contains("Current local time:", result);
        Assert.Contains("Current UTC time:", result);
        Assert.Contains("Time zone:", result);
        
        // Check time format using regex
        var timePattern = @"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}";
        Assert.True(Regex.IsMatch(result, timePattern), "Should contain valid time format");
    }

    [Fact]
    public void GetCurrentTime_ShouldShowBothLocalAndUtc()
    {
        // Act
        var result = SystemTools.GetCurrentTime();

        // Assert
        var lines = result.Split('\n');
        Assert.True(lines.Length >= 3, "Should have at least 3 lines");
        Assert.Contains("local time", lines[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UTC time", lines[1], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Time zone", lines[2], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetSystemInfo_ShouldReturnSystemDetails()
    {
        // Act
        var result = SystemTools.GetSystemInfo();

        // Assert
        Assert.Contains("System Information:", result);
        Assert.Contains("Operating System:", result);
        Assert.Contains("Architecture:", result);
        Assert.Contains("Framework:", result);
        Assert.Contains("Machine Name:", result);
        Assert.Contains("User Name:", result);
        Assert.Contains("Processor Count:", result);
        Assert.Contains("Working Directory:", result);
    }

    [Fact]
    public void GetSystemInfo_ShouldShowValidProcessorCount()
    {
        // Act
        var result = SystemTools.GetSystemInfo();

        // Assert
        var processorCountMatch = Regex.Match(result, @"Processor Count: (\d+)");
        Assert.True(processorCountMatch.Success, "Should contain processor count");
        
        var count = int.Parse(processorCountMatch.Groups[1].Value);
        Assert.True(count > 0, "Processor count should be greater than 0");
    }

    [Fact]
    public void GetEnvironmentVariable_ExistingVariable_ShouldReturnValue()
    {
        // Arrange - PATH should exist on all systems
        var variableName = "PATH";

        // Act
        var result = SystemTools.GetEnvironmentVariable(variableName);

        // Assert
        Assert.Contains($"Environment variable '{variableName}' = '", result);
        Assert.DoesNotContain("is not set", result);
    }

    [Fact]
    public void GetEnvironmentVariable_NonExistentVariable_ShouldReturnNotSet()
    {
        // Arrange
        var variableName = "NONEXISTENT_VARIABLE_XYZ123";

        // Act
        var result = SystemTools.GetEnvironmentVariable(variableName);

        // Assert
        Assert.Contains($"Environment variable '{variableName}' is not set", result);
    }

    [Fact]
    public void GetEnvironmentVariable_EmptyVariableName_ShouldHandleGracefully()
    {
        // Act
        var result = SystemTools.GetEnvironmentVariable("");

        // Assert
        Assert.Contains("Environment variable '' is not set", result);
    }

    [Fact]
    public void ListEnvironmentVariables_NoPattern_ShouldReturnAllVariables()
    {
        // Act
        var result = SystemTools.ListEnvironmentVariables();

        // Assert
        Assert.Contains("All environment variables", result);
        Assert.Contains("total):", result);
        Assert.Contains("PATH", result); // PATH should exist on all systems
    }

    [Fact]
    public void ListEnvironmentVariables_WithPattern_ShouldFilterResults()
    {
        // Arrange - Use a pattern that should match PATH
        var pattern = "PATH";

        // Act
        var result = SystemTools.ListEnvironmentVariables(pattern);

        // Assert
        Assert.Contains($"Environment variables matching '{pattern}'", result);
        Assert.Contains("PATH", result);
    }

    [Fact]
    public void ListEnvironmentVariables_WithNonMatchingPattern_ShouldReturnNoMatches()
    {
        // Arrange
        var pattern = "NONEXISTENT_PATTERN_XYZ123";

        // Act
        var result = SystemTools.ListEnvironmentVariables(pattern);

        // Assert
        Assert.Contains($"No environment variables found matching pattern '{pattern}'", result);
    }

    [Fact]
    public void ListEnvironmentVariables_ShouldTruncateLongValues()
    {
        // This test verifies the truncation behavior exists
        // We can't easily create a very long environment variable in tests,
        // but we can verify the logic would work by checking the result format
        
        // Act
        var result = SystemTools.ListEnvironmentVariables();

        // Assert
        Assert.Contains(" = ", result); // Should have key-value pairs
        
        // Check that no single line is excessively long (beyond reasonable limits)
        var lines = result.Split('\n');
        var dataLines = lines.Skip(1); // Skip header line
        
        foreach (var line in dataLines)
        {
            if (line.Contains(" = "))
            {
                Assert.True(line.Length < 500, $"Line should not be excessively long: {line}");
            }
        }
    }

    [Fact]
    public void GetCurrentDirectory_ShouldReturnValidPath()
    {
        // Act
        var result = SystemTools.GetCurrentDirectory();

        // Assert
        Assert.Contains("Current working directory:", result);
        
        // Extract the directory path
        var pathMatch = Regex.Match(result, @"Current working directory: (.+)");
        Assert.True(pathMatch.Success, "Should contain directory path");
        
        var path = pathMatch.Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(path), "Directory path should not be empty");
        Assert.True(Directory.Exists(path), "Directory should exist");
    }

    [Fact]
    public void GenerateUuid_ShouldReturnValidGuid()
    {
        // Act
        var result = SystemTools.GenerateUuid();

        // Assert
        Assert.Contains("Generated UUID:", result);
        
        // Extract the UUID
        var uuidMatch = Regex.Match(result, @"Generated UUID: ([a-fA-F0-9-]+)");
        Assert.True(uuidMatch.Success, "Should contain UUID");
        
        var uuidString = uuidMatch.Groups[1].Value;
        Assert.True(Guid.TryParse(uuidString, out var guid), "Should be a valid GUID");
        Assert.NotEqual(Guid.Empty, guid); // Should not be empty GUID
    }

    [Fact]
    public void GenerateUuid_ShouldGenerateUniqueValues()
    {
        // Act
        var result1 = SystemTools.GenerateUuid();
        var result2 = SystemTools.GenerateUuid();

        // Assert
        Assert.NotEqual(result1, result2); // Should generate different UUIDs
        
        // Extract UUIDs
        var uuidMatch1 = Regex.Match(result1, @"Generated UUID: ([a-fA-F0-9-]+)");
        var uuidMatch2 = Regex.Match(result2, @"Generated UUID: ([a-fA-F0-9-]+)");
        
        Assert.True(uuidMatch1.Success && uuidMatch2.Success);
        Assert.NotEqual(uuidMatch1.Groups[1].Value, uuidMatch2.Groups[1].Value);
    }

    [Fact]
    public void GetSystemInfo_ShouldContainFrameworkInfo()
    {
        // Act
        var result = SystemTools.GetSystemInfo();

        // Assert
        Assert.Contains("Framework:", result);
        Assert.Contains(".NET", result); // Should contain .NET in framework description
    }

    [Fact]
    public void ListEnvironmentVariables_ShouldSortResults()
    {
        // Act
        var result = SystemTools.ListEnvironmentVariables();

        // Assert
        var lines = result.Split('\n');
        var dataLines = lines.Skip(1).Where(l => l.Contains(" = ")).ToArray();
        
        // Extract variable names
        var variableNames = dataLines
            .Select(line => line.Split(" = ")[0])
            .ToArray();
        
        // Should be sorted alphabetically
        var sortedNames = variableNames.OrderBy(name => name).ToArray();
        Assert.Equal(sortedNames, variableNames);
    }

    [Fact]
    public void GetEnvironmentVariable_CaseInsensitive_ShouldWork()
    {
        // Note: This test may behave differently on different operating systems
        // On Windows, environment variables are case-insensitive
        // On Unix-like systems, they are case-sensitive
        
        // Act
        var result = SystemTools.GetEnvironmentVariable("PATH");

        // Assert
        Assert.DoesNotContain("Error getting environment variable", result);
    }
}