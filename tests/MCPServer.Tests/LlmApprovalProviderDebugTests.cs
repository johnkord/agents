using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using MCPServer.ToolApproval;
using MCPServer.ToolApproval.LlmApproval;

namespace MCPServer.Tests.ToolApproval;

/// <summary>
/// Simple debugging tests for LLM approval provider
/// </summary>
public class LlmApprovalProviderDebugTests
{
    private readonly ITestOutputHelper _output;

    public LlmApprovalProviderDebugTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Debug_DangerousOperationWithMockFallback()
    {
        // Arrange
        var llmService = new MockLlmService();
        var policy = new LlmApprovalPolicy
        {
            AutoApprovalMinConfidence = 0.85,
            HumanRequiredMaxConfidence = 0.50
        };
        var mockFallbackProvider = new DebugMockApprovalProvider(approveAll: false);
        var provider = new LlmApprovalProvider(llmService, policy, fallbackProvider: mockFallbackProvider);

        var token = new ApprovalInvocationToken(
            Guid.NewGuid(),
            "delete_file",
            new Dictionary<string, object?> { ["path"] = "/important/system/file.exe" },
            DateTimeOffset.UtcNow);

        _output.WriteLine($"Testing token: {token.ToolName} with args: {string.Join(", ", token.Arguments)}");

        // First, let's see what the LLM service returns directly
        var llmDecision = await llmService.EvaluateToolCallAsync(
            token.ToolName, 
            token.Arguments, 
            new LlmApprovalContext());

        _output.WriteLine($"LLM Decision: {llmDecision.Result}, Confidence: {llmDecision.Confidence:F2}");

        // Now test the full provider
        var result = await provider.RequestApprovalAsync(token);

        _output.WriteLine($"Final Result: {result}");
        _output.WriteLine($"Fallback Called: {mockFallbackProvider.WasCalled}");
        _output.WriteLine($"Fallback Decision: {mockFallbackProvider.LastDecision}");

        // Assert
        Assert.False(result, "Dangerous operation should be denied when fallback provider denies");
        Assert.True(mockFallbackProvider.WasCalled, "Fallback provider should have been called");
    }

    [Fact]
    public async Task Debug_ToolSpecificPolicy()
    {
        // Arrange
        var llmService = new MockLlmService();
        var policy = new LlmApprovalPolicy
        {
            AutoApprovalMinConfidence = 0.50, // Low global threshold
            ToolPolicies = new Dictionary<string, ToolPolicy>
            {
                ["write_file"] = new ToolPolicy
                {
                    AllowAutoApproval = true,
                    MinConfidenceOverride = 0.96 // Very high threshold for this tool
                }
            }
        };
        var mockFallbackProvider = new DebugMockApprovalProvider(approveAll: true);
        var provider = new LlmApprovalProvider(llmService, policy, fallbackProvider: mockFallbackProvider);

        var token = new ApprovalInvocationToken(
            Guid.NewGuid(),
            "write_file",
            new Dictionary<string, object?> { ["path"] = "/test.txt", ["content"] = "Hello World" },
            DateTimeOffset.UtcNow);

        _output.WriteLine($"Testing token: {token.ToolName} with args: {string.Join(", ", token.Arguments)}");

        // First, let's see what the LLM service returns directly
        var llmDecision = await llmService.EvaluateToolCallAsync(
            token.ToolName, 
            token.Arguments, 
            new LlmApprovalContext());

        _output.WriteLine($"LLM Decision: {llmDecision.Result}, Confidence: {llmDecision.Confidence:F2}");

        // Now test the full provider
        var result = await provider.RequestApprovalAsync(token);

        _output.WriteLine($"Final Result: {result}");
        _output.WriteLine($"Fallback Called: {mockFallbackProvider.WasCalled}");
        _output.WriteLine($"Fallback Decision: {mockFallbackProvider.LastDecision}");

        // Assert
        Assert.True(result);
        Assert.True(mockFallbackProvider.WasCalled, "Should require human approval due to high tool-specific confidence threshold");
    }

    /// <summary>
    /// Debug mock approval provider that tracks calls
    /// </summary>
    private class DebugMockApprovalProvider : IApprovalProvider
    {
        private readonly bool _approveAll;
        public bool WasCalled { get; private set; }
        public bool LastDecision { get; private set; }
        public string ProviderName => "DebugMock";

        public DebugMockApprovalProvider(bool approveAll)
        {
            _approveAll = approveAll;
        }

        public Task<bool> RequestApprovalAsync(ApprovalInvocationToken token, System.Threading.CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            LastDecision = _approveAll;
            return Task.FromResult(_approveAll);
        }
    }
}