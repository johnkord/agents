using MCPServer.Tools;
using Xunit;

namespace MCPServer.Tests;

public class TextToolsTests
{
    [Theory]
    [InlineData("Hello World\nGoodbye World", "World", false, 2)]
    [InlineData("Hello World\nGoodbye world", "world", false, 2)]
    [InlineData("Hello World\nGoodbye world", "world", true, 1)]
    [InlineData("Hello World\nGoodbye World", "Universe", false, 0)]
    public void SearchText_ShouldFindCorrectMatches(string text, string pattern, bool caseSensitive, int expectedMatches)
    {
        // Act
        var result = TextTools.SearchText(text, pattern, caseSensitive);

        // Assert
        if (expectedMatches == 0)
        {
            Assert.Contains("No matches found", result);
        }
        else
        {
            Assert.Contains($"Found {expectedMatches} matches", result);
        }
    }

    [Fact]
    public void SearchText_ShouldReturnLineNumbers()
    {
        // Arrange
        var text = "Line 1\nLine 2 with test\nLine 3\nLine 4 with test";

        // Act
        var result = TextTools.SearchText(text, "test");

        // Assert
        Assert.Contains("Line 2:", result);
        Assert.Contains("Line 4:", result);
        Assert.Contains("Found 2 matches", result);
    }

    [Theory]
    [InlineData("Hello World", "World", "Universe", false, "Hello Universe")]
    [InlineData("Hello world", "WORLD", "Universe", false, "Hello Universe")]
    [InlineData("Hello WORLD", "world", "Universe", true, "Hello WORLD")]
    [InlineData("test test test", "test", "demo", false, "demo demo demo")]
    public void ReplaceText_ShouldReplaceCorrectly(string text, string search, string replacement, bool caseSensitive, string expectedResult)
    {
        // Act
        var result = TextTools.ReplaceText(text, search, replacement, caseSensitive);

        // Assert
        Assert.Contains(expectedResult, result);
    }

    [Theory]
    [InlineData("Line 1\nLine 2\nLine 3\nLine 4", "1,3", 2)]
    [InlineData("Line 1\nLine 2\nLine 3\nLine 4", "2-4", 3)]
    [InlineData("Line 1\nLine 2\nLine 3\nLine 4", "1,3-4", 3)]
    [InlineData("Line 1\nLine 2\nLine 3\nLine 4", "5", 0)]
    public void ExtractLines_ShouldExtractCorrectLines(string text, string lineNumbers, int expectedCount)
    {
        // Act
        var result = TextTools.ExtractLines(text, lineNumbers);

        // Assert
        if (expectedCount == 0)
        {
            Assert.Contains("No valid lines found", result);
        }
        else
        {
            Assert.Contains($"Extracted {expectedCount} lines", result);
        }
    }

    [Fact]
    public void ExtractLines_ShouldHandleRanges()
    {
        // Arrange
        var text = "Line 1\nLine 2\nLine 3\nLine 4\nLine 5";

        // Act
        var result = TextTools.ExtractLines(text, "2-4");

        // Assert
        Assert.Contains("Line 2: Line 2", result);
        Assert.Contains("Line 3: Line 3", result);
        Assert.Contains("Line 4: Line 4", result);
        Assert.DoesNotContain("Line 1: Line 1", result);
        Assert.DoesNotContain("Line 5: Line 5", result);
    }

    [Theory]
    [InlineData("Hello world\nThis is a test", 2, 6, 26, 21)]
    [InlineData("", 1, 0, 0, 0)] // Empty string still results in 1 line
    [InlineData("Hello", 1, 1, 5, 5)]
    [InlineData("  spaced  text  \n\n", 3, 2, 18, 10)] // Leading/trailing spaces in words get filtered out
    public void WordCount_ShouldCountCorrectly(string text, int expectedLines, int expectedWords, int expectedChars, int expectedCharsNoSpaces)
    {
        // Act
        var result = TextTools.WordCount(text);

        // Assert
        Assert.Contains($"Lines: {expectedLines}", result);
        Assert.Contains($"Words: {expectedWords}", result);
        Assert.Contains($"Characters (with spaces): {expectedChars}", result);
        Assert.Contains($"Characters (without spaces): {expectedCharsNoSpaces}", result);
    }

    [Theory]
    [InlineData("hello world", "uppercase", "HELLO WORLD")]
    [InlineData("HELLO WORLD", "lowercase", "hello world")]
    [InlineData("hello world", "titlecase", "Hello World")]
    [InlineData("  hello world  ", "trim", "hello world")]
    [InlineData("hello    world    test", "remove_extra_spaces", "hello world test")]
    public void FormatText_ShouldApplyCorrectFormat(string text, string format, string expectedResult)
    {
        // Act
        var result = TextTools.FormatText(text, format);

        // Assert
        Assert.Contains(expectedResult, result);
        Assert.Contains($"Applied '{format}' formatting", result);
    }

    [Fact]
    public void FormatText_ShouldThrowForInvalidFormat()
    {
        // Act
        var result = TextTools.FormatText("test", "invalid_format");

        // Assert
        Assert.Contains("Error formatting text", result);
        Assert.Contains("Unsupported format", result);
    }

    [Theory]
    [InlineData("apple,banana,cherry", ",", 0, 3)]
    [InlineData("apple,banana,cherry", ",", 2, 2)]
    [InlineData("apple|banana|cherry", "|", 0, 3)]
    [InlineData("no delimiter here", ",", 0, 1)]
    public void SplitText_ShouldSplitCorrectly(string text, string delimiter, int maxParts, int expectedParts)
    {
        // Act
        var result = TextTools.SplitText(text, delimiter, maxParts);

        // Assert
        Assert.Contains($"Split text into {expectedParts} parts", result);
    }

    [Fact]
    public void SplitText_ShouldShowParts()
    {
        // Arrange
        var text = "apple,banana,cherry";

        // Act
        var result = TextTools.SplitText(text, ",");

        // Assert
        Assert.Contains("Part 1: apple", result);
        Assert.Contains("Part 2: banana", result);
        Assert.Contains("Part 3: cherry", result);
    }

    [Fact]
    public void SearchText_ShouldHandleExceptions()
    {
        // This test verifies error handling exists, though specific exceptions are hard to trigger
        var result = TextTools.SearchText("normal text", "pattern");
        Assert.DoesNotContain("Error searching text", result);
    }

    [Fact]
    public void WordCount_EmptyString_ShouldHandleGracefully()
    {
        // Act
        var result = TextTools.WordCount("");

        // Assert
        Assert.Contains("Lines: 1", result); // Empty string still results in 1 line from Split
        Assert.Contains("Words: 0", result);
        Assert.Contains("Characters (with spaces): 0", result);
        Assert.Contains("Characters (without spaces): 0", result);
    }

    [Fact]
    public void ExtractLines_InvalidLineNumbers_ShouldHandleGracefully()
    {
        // Act
        var result = TextTools.ExtractLines("Line 1\nLine 2", "abc,xyz");

        // Assert
        Assert.Contains("No valid lines found", result);
    }

    [Theory]
    [InlineData("hello world", "title")]
    [InlineData("hello world", "upper")]
    [InlineData("hello world", "lower")]
    public void FormatText_ShouldAcceptAliases(string text, string format)
    {
        // Act
        var result = TextTools.FormatText(text, format);

        // Assert
        Assert.DoesNotContain("Error formatting text", result);
        Assert.Contains($"Applied '{format}' formatting", result);
    }
}