using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using MCPServer.ToolApproval.LlmApproval;

namespace MCPServer.Tests.ToolApproval;

/// <summary>
/// Tests to understand how the MockLlmService behaves
/// </summary>
public class MockLlmServiceBehaviorTests
{
    private readonly ITestOutputHelper _output;

    public MockLlmServiceBehaviorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task MockLlmService_DeleteFileWithExecutable_Analysis()
    {
        // Arrange
        var llmService = new MockLlmService();
        var context = new LlmApprovalContext();

        // Act
        var decision = await llmService.EvaluateToolCallAsync(
            "delete_file",
            new Dictionary<string, object?> { ["path"] = "/important/system/file.exe" },
            context);

        // Assert
        _output.WriteLine($"Tool: delete_file");
        _output.WriteLine($"Arguments: /important/system/file.exe");
        _output.WriteLine($"Result: {decision.Result}");
        _output.WriteLine($"Confidence: {decision.Confidence:F2}");
        _output.WriteLine($"Risk Category: {decision.RiskCategory}");
        _output.WriteLine($"Reasoning: {decision.Reasoning}");
        _output.WriteLine($"Concerns: {string.Join(", ", decision.Concerns)}");
    }

    [Fact]
    public async Task MockLlmService_FormatDisk_Analysis()
    {
        // Arrange
        var llmService = new MockLlmService();
        var context = new LlmApprovalContext();

        // Act
        var decision = await llmService.EvaluateToolCallAsync(
            "format_disk",
            new Dictionary<string, object?> { ["drive"] = "C:\\" },
            context);

        // Assert
        _output.WriteLine($"Tool: format_disk");
        _output.WriteLine($"Arguments: C:\\");
        _output.WriteLine($"Result: {decision.Result}");
        _output.WriteLine($"Confidence: {decision.Confidence:F2}");
        _output.WriteLine($"Risk Category: {decision.RiskCategory}");
        _output.WriteLine($"Reasoning: {decision.Reasoning}");
        _output.WriteLine($"Concerns: {string.Join(", ", decision.Concerns)}");
    }

    [Fact]
    public async Task MockLlmService_WriteFile_Analysis()
    {
        // Arrange
        var llmService = new MockLlmService();
        var context = new LlmApprovalContext();

        // Act
        var decision = await llmService.EvaluateToolCallAsync(
            "write_file",
            new Dictionary<string, object?> { ["path"] = "/test.txt", ["content"] = "Hello World" },
            context);

        // Assert
        _output.WriteLine($"Tool: write_file");
        _output.WriteLine($"Arguments: /test.txt, 'Hello World'");
        _output.WriteLine($"Result: {decision.Result}");
        _output.WriteLine($"Confidence: {decision.Confidence:F2}");
        _output.WriteLine($"Risk Category: {decision.RiskCategory}");
        _output.WriteLine($"Reasoning: {decision.Reasoning}");
        _output.WriteLine($"Concerns: {string.Join(", ", decision.Concerns)}");
    }
}