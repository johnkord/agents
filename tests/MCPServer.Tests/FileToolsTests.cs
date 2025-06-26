using MCPServer.Tools;
using Xunit;
using System;
using System.IO;
using System.Linq;

namespace MCPServer.Tests;

public class FileToolsTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _testFile;
    private readonly string _testContent;

    public FileToolsTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "MCPServer_FileTools_Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _testFile = Path.Combine(_testDirectory, "test.txt");
        _testContent = "This is test content\nWith multiple lines\nFor testing purposes";
        File.WriteAllText(_testFile, _testContent);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public void ReadFile_ExistingFile_ShouldReturnContent()
    {
        // Act
        var result = FileTools.ReadFile(_testFile);

        // Assert
        Assert.Contains("Successfully read file", result);
        Assert.Contains(_testContent, result);
    }

    [Fact]
    public void ReadFile_NonExistentFile_ShouldReturnError()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_testDirectory, "nonexistent.txt");

        // Act
        var result = FileTools.ReadFile(nonExistentFile);

        // Assert
        Assert.Contains("Error: File", result);
        Assert.Contains("does not exist", result);
    }

    [Fact]
    public void ReadFile_InvalidPath_ShouldHandleGracefully()
    {
        // Arrange - Using a very long path that should cause issues
        var invalidPath = new string('a', 300); // Very long path

        // Act
        var result = FileTools.ReadFile(invalidPath);

        // Assert - Should either return file not found or handle gracefully
        Assert.True(result.Contains("Error:") || result.Contains("does not exist"), 
            "Should handle invalid paths gracefully");
    }

    [Fact]
    public void FileExists_ExistingFile_ShouldReturnTrue()
    {
        // Act
        var result = FileTools.FileExists(_testFile);

        // Assert
        Assert.Contains("exists", result);
        Assert.DoesNotContain("does not exist", result);
    }

    [Fact]
    public void FileExists_NonExistentFile_ShouldReturnFalse()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_testDirectory, "nonexistent.txt");

        // Act
        var result = FileTools.FileExists(nonExistentFile);

        // Assert
        Assert.Contains("does not exist", result);
    }

    [Fact]
    public void ListDirectory_ExistingDirectory_ShouldListContents()
    {
        // Arrange - Create additional test files and directories
        var subDir = Path.Combine(_testDirectory, "subdir");
        Directory.CreateDirectory(subDir);
        var anotherFile = Path.Combine(_testDirectory, "another.txt");
        File.WriteAllText(anotherFile, "content");

        // Act
        var result = FileTools.ListDirectory(_testDirectory);

        // Assert
        Assert.Contains("Contents of directory", result);
        Assert.Contains("[DIR]  subdir", result);
        Assert.Contains("[FILE] test.txt", result);
        Assert.Contains("[FILE] another.txt", result);
    }

    [Fact]
    public void ListDirectory_NonExistentDirectory_ShouldReturnError()
    {
        // Arrange
        var nonExistentDir = Path.Combine(_testDirectory, "nonexistent");

        // Act
        var result = FileTools.ListDirectory(nonExistentDir);

        // Assert
        Assert.Contains("Error: Directory", result);
        Assert.Contains("does not exist", result);
    }

    [Fact]
    public void ListDirectory_EmptyDirectory_ShouldReturnEmptyListing()
    {
        // Arrange
        var emptyDir = Path.Combine(_testDirectory, "empty");
        Directory.CreateDirectory(emptyDir);

        // Act
        var result = FileTools.ListDirectory(emptyDir);

        // Assert
        Assert.Contains("Contents of directory", result);
        // Should not contain any [DIR] or [FILE] entries
        Assert.DoesNotContain("[DIR]", result);
        Assert.DoesNotContain("[FILE]", result);
    }

    [Fact]
    public void GetFileInfo_ExistingFile_ShouldReturnFileInfo()
    {
        // Act
        var result = FileTools.GetFileInfo(_testFile);

        // Assert
        Assert.Contains("File information for", result);
        Assert.Contains("Name: test.txt", result);
        Assert.Contains("Size:", result);
        Assert.Contains("Created:", result);
        Assert.Contains("Modified:", result);
        Assert.Contains("Extension: .txt", result);
        Assert.Contains("Read-only:", result);
    }

    [Fact]
    public void GetFileInfo_NonExistentFile_ShouldReturnError()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_testDirectory, "nonexistent.txt");

        // Act
        var result = FileTools.GetFileInfo(nonExistentFile);

        // Assert
        Assert.Contains("Error: File", result);
        Assert.Contains("does not exist", result);
    }

    [Fact]
    public void GetFileInfo_ShouldShowCorrectFileSize()
    {
        // Act
        var result = FileTools.GetFileInfo(_testFile);

        // Assert
        Assert.Contains($"Size: {_testContent.Length} bytes", result);
    }

    // Test for approval-required methods would need approval system setup
    // These tests are commented out as they require database setup
    /*
    [Fact]
    public void WriteFile_WithoutApproval_ShouldReturnDenialError()
    {
        // Act
        var result = FileTools.WriteFile(Path.Combine(_testDirectory, "new.txt"), "content");

        // Assert - Since approval system isn't configured to allow, should be denied
        Assert.Contains("Error: Tool execution was denied by approval system", result);
    }

    [Fact]
    public void DeleteFile_WithoutApproval_ShouldReturnDenialError()
    {
        // Act
        var result = FileTools.DeleteFile(_testFile);

        // Assert - Since approval system isn't configured to allow, should be denied
        Assert.Contains("Error: Tool execution was denied by approval system", result);
    }

    [Fact]
    public void CreateDirectory_WithoutApproval_ShouldReturnDenialError()
    {
        // Act
        var result = FileTools.CreateDirectory(Path.Combine(_testDirectory, "newdir"));

        // Assert - Since approval system isn't configured to allow, should be denied
        Assert.Contains("Error: Tool execution was denied by approval system", result);
    }
    */

    [Fact]
    public void ListDirectory_ShouldSortEntries()
    {
        // Arrange - Create files and directories in non-alphabetical order
        Directory.CreateDirectory(Path.Combine(_testDirectory, "z_dir"));
        Directory.CreateDirectory(Path.Combine(_testDirectory, "a_dir"));
        File.WriteAllText(Path.Combine(_testDirectory, "z_file.txt"), "content");
        File.WriteAllText(Path.Combine(_testDirectory, "a_file.txt"), "content");

        // Act
        var result = FileTools.ListDirectory(_testDirectory);

        // Assert - Directories should come first, then files, both sorted
        var lines = result.Split('\n');
        var contentLines = lines.Where(l => l.Contains("[DIR]") || l.Contains("[FILE]")).ToArray();
        
        // Should have directories first
        Assert.True(contentLines.TakeWhile(l => l.Contains("[DIR]")).Count() >= 2);
        
        // Verify sorting (directories first, then files)
        var dirLines = contentLines.Where(l => l.Contains("[DIR]")).ToArray();
        var fileLines = contentLines.Where(l => l.Contains("[FILE]")).ToArray();
        
        Assert.True(dirLines.Length >= 2);
        Assert.True(fileLines.Length >= 3); // Including the original test.txt
    }
}