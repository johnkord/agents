using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using MCPServer.ToolApproval;
using MCPServer.ToolApproval.LlmApproval;

namespace MCPServer.Tests.ToolApproval;

/// <summary>
/// Tests for LLM-based approval provider
/// </summary>
public class LlmApprovalProviderTests
{
    [Fact]
    public async Task LlmApprovalProvider_SafeOperation_AutoApproves()
    {
        // Arrange
        var llmService = new MockLlmService();
        var policy = new LlmApprovalPolicy
        {
            AutoApprovalMinConfidence = 0.80,
            HumanRequiredMaxConfidence = 0.50
        };
        var provider = new LlmApprovalProvider(llmService, policy);

        var token = new ApprovalInvocationToken(
            Guid.NewGuid(),
            "read_file",
            new Dictionary<string, object?> { ["path"] = "/home/user/document.txt" },
            DateTimeOffset.UtcNow);

        // Act
        var result = await provider.RequestApprovalAsync(token);

        // Assert
        Assert.True(result, "Safe file read operation should be auto-approved");
    }

    [Fact]
    public async Task LlmApprovalProvider_DangerousOperation_RequiresHumanApproval()
    {
        // Arrange
        var llmService = new MockLlmService();
        var policy = new LlmApprovalPolicy
        {
            AutoApprovalMinConfidence = 0.85,
            HumanRequiredMaxConfidence = 0.50
        };
        var mockFallbackProvider = new MockApprovalProvider(approveAll: false);
        var provider = new LlmApprovalProvider(llmService, policy, fallbackProvider: mockFallbackProvider);

        var token = new ApprovalInvocationToken(
            Guid.NewGuid(),
            "delete_file",
            new Dictionary<string, object?> { ["path"] = "/important/system/file.exe" },
            DateTimeOffset.UtcNow);

        // Act
        var result = await provider.RequestApprovalAsync(token);

        // Assert
        Assert.False(result, "Dangerous operation should be denied when fallback provider denies");
        Assert.True(mockFallbackProvider.WasCalled, "Fallback provider should have been called");
    }

    [Fact]
    public async Task LlmApprovalProvider_CriticalOperation_AlwaysRequiresHuman()
    {
        // Arrange
        var llmService = new MockLlmService();
        var policy = new LlmApprovalPolicy
        {
            AutoApprovalMinConfidence = 0.50, // Very low threshold
            HumanRequiredMaxConfidence = 0.10
        };
        var mockFallbackProvider = new MockApprovalProvider(approveAll: true);
        var provider = new LlmApprovalProvider(llmService, policy, fallbackProvider: mockFallbackProvider);

        var token = new ApprovalInvocationToken(
            Guid.NewGuid(),
            "format_disk",
            new Dictionary<string, object?> { ["drive"] = "C:\\" },
            DateTimeOffset.UtcNow);

        // Act
        var result = await provider.RequestApprovalAsync(token);

        // Assert
        Assert.True(result, "Critical operation should be approved when human approves");
        Assert.True(mockFallbackProvider.WasCalled, "Fallback provider should have been called for critical operation");
    }

    [Fact]
    public async Task LlmApprovalProvider_PolicyPreventsAutoApproval_RequiresHuman()
    {
        // Arrange
        var llmService = new MockLlmService();
        var policy = new LlmApprovalPolicy
        {
            AutoApprovalMinConfidence = 0.50,
            AlwaysRequireHuman = new List<string> { "read_file" }
        };
        var mockFallbackProvider = new MockApprovalProvider(approveAll: true);
        var provider = new LlmApprovalProvider(llmService, policy, fallbackProvider: mockFallbackProvider);

        var token = new ApprovalInvocationToken(
            Guid.NewGuid(),
            "read_file",
            new Dictionary<string, object?> { ["path"] = "/safe/file.txt" },
            DateTimeOffset.UtcNow);

        // Act
        var result = await provider.RequestApprovalAsync(token);

        // Assert
        Assert.True(result, "Should be approved when human approves");
        Assert.True(mockFallbackProvider.WasCalled, "Should require human approval due to policy");
    }

    [Fact]
    public async Task LlmApprovalProvider_CacheEnabled_UsesCachedDecision()
    {
        // Arrange
        var llmService = new TrackingMockLlmService();
        var policy = new LlmApprovalPolicy
        {
            CacheEnabled = true,
            CacheTtl = TimeSpan.FromMinutes(5),
            AutoApprovalMinConfidence = 0.80
        };
        var provider = new LlmApprovalProvider(llmService, policy);

        var token = new ApprovalInvocationToken(
            Guid.NewGuid(),
            "read_file",
            new Dictionary<string, object?> { ["path"] = "/test.txt" },
            DateTimeOffset.UtcNow);

        // Act - First call
        var result1 = await provider.RequestApprovalAsync(token);
        var result2 = await provider.RequestApprovalAsync(token);

        // Assert
        Assert.True(result1);
        Assert.True(result2);
        Assert.Equal(1, llmService.CallCount);
    }

    [Fact]
    public async Task LlmApprovalProvider_ToolSpecificPolicy_OverridesGlobalPolicy()
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
        var mockFallbackProvider = new MockApprovalProvider(approveAll: true);
        var provider = new LlmApprovalProvider(llmService, policy, fallbackProvider: mockFallbackProvider);

        var token = new ApprovalInvocationToken(
            Guid.NewGuid(),
            "write_file",
            new Dictionary<string, object?> { ["path"] = "/test.txt", ["content"] = "Hello World" },
            DateTimeOffset.UtcNow);

        // Act
        var result = await provider.RequestApprovalAsync(token);

        // Assert
        Assert.True(result);
        Assert.True(mockFallbackProvider.WasCalled, "Should require human approval due to high tool-specific confidence threshold");
    }

    /// <summary>
    /// Mock approval provider for testing
    /// </summary>
    private class MockApprovalProvider : IApprovalProvider
    {
        private readonly bool _approveAll;
        public bool WasCalled { get; private set; }
        public string ProviderName => "Mock";

        public MockApprovalProvider(bool approveAll)
        {
            _approveAll = approveAll;
        }

        public Task<bool> RequestApprovalAsync(ApprovalInvocationToken token, System.Threading.CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(_approveAll);
        }
    }

    /// <summary>
    /// Mock LLM service that tracks the number of calls
    /// </summary>
    private class TrackingMockLlmService : MockLlmService
    {
        public int CallCount { get; private set; }

        public override async Task<LlmApprovalDecision> EvaluateToolCallAsync(
            string toolName,
            IReadOnlyDictionary<string, object?> arguments,
            LlmApprovalContext context,
            System.Threading.CancellationToken cancellationToken = default)
        {
            CallCount++;
            return await base.EvaluateToolCallAsync(toolName, arguments, context, cancellationToken);
        }
    }
}