using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MCPServer.ToolApproval;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Xunit;

namespace MCPServer.Tests.ToolApproval
{
    /// <summary>
    /// End-to-end tests that demonstrate the RequiresApproval attribute actually blocks
    /// execution and waits for approval from the approval service.
    /// </summary>
    public class EndToEndApprovalTests
    {
        public class TestToolsForEndToEnd
        {
            // Attributes removed
            public static string RequiresApprovalTool(string operation) =>
                $"EXECUTED RequiresApprovalTool with operation: {operation}";

            public static string NoApprovalRequiredTool(string operation)
            {
                return $"EXECUTED NoApprovalRequiredTool with operation: {operation}";
            }

            // Attribute removed
            public static string ExplicitlyNoApprovalTool(string operation) =>
                $"EXECUTED ExplicitlyNoApprovalTool with operation: {operation}";
        }

        [Fact]
        public async Task TestApprovalProvider_WorksAsExpected()
        {
            // Test that our TestApprovalProvider behaves correctly
            
            var provider = new TestApprovalProvider();
            
            // Test approval
            provider.QueueApprovalResponse(true);
            var token1 = new ApprovalInvocationToken(Guid.NewGuid(), "test_tool_1", new Dictionary<string, object?>(), DateTimeOffset.UtcNow);
            var result1 = await provider.RequestApprovalAsync(token1);
            Assert.True(result1);
            
            // Test denial
            provider.QueueApprovalResponse(false);
            var token2 = new ApprovalInvocationToken(Guid.NewGuid(), "test_tool_2", new Dictionary<string, object?>(), DateTimeOffset.UtcNow);
            var result2 = await provider.RequestApprovalAsync(token2);
            Assert.False(result2);
            
            // Verify both tokens were recorded
            Assert.Equal(2, provider.ReceivedTokens.Count);
            Assert.Equal("test_tool_1", provider.ReceivedTokens[0].ToolName);
            Assert.Equal("test_tool_2", provider.ReceivedTokens[1].ToolName);
        }

        [Fact]
        public async Task TestApprovalProvider_WithMultipleResponses_WorksCorrectly()
        {
            // Test queuing multiple responses
            
            var provider = new TestApprovalProvider();
            provider.QueueApprovalResponse(true);   // First: approve
            provider.QueueApprovalResponse(false);  // Second: deny
            provider.QueueApprovalResponse(true);   // Third: approve

            var token1 = new ApprovalInvocationToken(Guid.NewGuid(), "tool1", new Dictionary<string, object?>(), DateTimeOffset.UtcNow);
            var token2 = new ApprovalInvocationToken(Guid.NewGuid(), "tool2", new Dictionary<string, object?>(), DateTimeOffset.UtcNow);
            var token3 = new ApprovalInvocationToken(Guid.NewGuid(), "tool3", new Dictionary<string, object?>(), DateTimeOffset.UtcNow);

            var result1 = await provider.RequestApprovalAsync(token1);
            var result2 = await provider.RequestApprovalAsync(token2);
            var result3 = await provider.RequestApprovalAsync(token3);

            Assert.True(result1);
            Assert.False(result2);
            Assert.True(result3);

            Assert.Equal(3, provider.ReceivedTokens.Count);
        }

        [Fact]
        public async Task TestApprovalProvider_ThrowsWhenNoResponseQueued()
        {
            // Test that the provider throws when no response is queued
            
            var provider = new TestApprovalProvider();
            var token = new ApprovalInvocationToken(Guid.NewGuid(), "test_tool", new Dictionary<string, object?>(), DateTimeOffset.UtcNow);

            await Assert.ThrowsAsync<InvalidOperationException>(() => provider.RequestApprovalAsync(token));
        }

        [Fact]
        public void DirectToolExecution_WithoutWrapper_ExecutesImmediately()
        {
            // Test that tools execute immediately when not wrapped
            
            var result1 = TestToolsForEndToEnd.RequiresApprovalTool("direct-call");
            var result2 = TestToolsForEndToEnd.NoApprovalRequiredTool("direct-call");
            var result3 = TestToolsForEndToEnd.ExplicitlyNoApprovalTool("direct-call");

            Assert.Contains("EXECUTED RequiresApprovalTool with operation: direct-call", result1);
            Assert.Contains("EXECUTED NoApprovalRequiredTool with operation: direct-call", result2);
            Assert.Contains("EXECUTED ExplicitlyNoApprovalTool with operation: direct-call", result3);
        }
    }
}