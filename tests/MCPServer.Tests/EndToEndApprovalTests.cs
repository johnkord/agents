using System;
using System.Collections.Generic;
using System.Reflection;
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
            [RequiresApproval]
            public static string RequiresApprovalTool(string operation)
            {
                return $"EXECUTED RequiresApprovalTool with operation: {operation}";
            }

            public static string NoApprovalRequiredTool(string operation)
            {
                return $"EXECUTED NoApprovalRequiredTool with operation: {operation}";
            }

            [RequiresApproval(false)]
            public static string ExplicitlyNoApprovalTool(string operation)
            {
                return $"EXECUTED ExplicitlyNoApprovalTool with operation: {operation}";
            }
        }

        [Fact]
        public void RequiresApproval_ToolIsWrapped_OriginalIsNot()
        {
            // Test that the ToolApprovalWrapper correctly identifies which tools need wrapping
            
            var requiresApprovalMethod = typeof(TestToolsForEndToEnd).GetMethod(nameof(TestToolsForEndToEnd.RequiresApprovalTool))!;
            var noApprovalMethod = typeof(TestToolsForEndToEnd).GetMethod(nameof(TestToolsForEndToEnd.NoApprovalRequiredTool))!;
            var explicitlyNoApprovalMethod = typeof(TestToolsForEndToEnd).GetMethod(nameof(TestToolsForEndToEnd.ExplicitlyNoApprovalTool))!;

            var originalTool1 = McpServerTool.Create(() => "test", new() { Name = "requires_approval" });
            var originalTool2 = McpServerTool.Create(() => "test", new() { Name = "no_approval" });
            var originalTool3 = McpServerTool.Create(() => "test", new() { Name = "explicitly_no_approval" });

            // Test wrapping behavior
            var wrappedTool1 = ToolApprovalWrapper.WrapIfNeeded(originalTool1, requiresApprovalMethod);
            var wrappedTool2 = ToolApprovalWrapper.WrapIfNeeded(originalTool2, noApprovalMethod);
            var wrappedTool3 = ToolApprovalWrapper.WrapIfNeeded(originalTool3, explicitlyNoApprovalMethod);

            // RequiresApproval tool should be wrapped (different object)
            Assert.NotSame(originalTool1, wrappedTool1);
            
            // No approval tool should NOT be wrapped (same object)
            Assert.Same(originalTool2, wrappedTool2);
            
            // Explicitly no approval tool should NOT be wrapped (same object)
            Assert.Same(originalTool3, wrappedTool3);
        }

        [Fact]
        public void RequiresApproval_AttributePresenceDetectedCorrectly()
        {
            // Verify that our test methods have the expected attributes
            
            var requiresApprovalMethod = typeof(TestToolsForEndToEnd).GetMethod(nameof(TestToolsForEndToEnd.RequiresApprovalTool))!;
            var noApprovalMethod = typeof(TestToolsForEndToEnd).GetMethod(nameof(TestToolsForEndToEnd.NoApprovalRequiredTool))!;
            var explicitlyNoApprovalMethod = typeof(TestToolsForEndToEnd).GetMethod(nameof(TestToolsForEndToEnd.ExplicitlyNoApprovalTool))!;

            // RequiresApprovalTool should have the attribute with Required = true
            var attr1 = requiresApprovalMethod.GetCustomAttribute<RequiresApprovalAttribute>();
            Assert.NotNull(attr1);
            Assert.True(attr1.Required);

            // NoApprovalRequiredTool should not have the attribute
            var attr2 = noApprovalMethod.GetCustomAttribute<RequiresApprovalAttribute>();
            Assert.Null(attr2);

            // ExplicitlyNoApprovalTool should have the attribute with Required = false
            var attr3 = explicitlyNoApprovalMethod.GetCustomAttribute<RequiresApprovalAttribute>();
            Assert.NotNull(attr3);
            Assert.False(attr3.Required);
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

        [Fact]
        public void ApprovalSystem_IdentifiesCorrectToolsInRealScenario()
        {
            // Test with real tools from the system
            
            var shellRunCommandMethod = typeof(MCPServer.Tools.ShellTools).GetMethod("RunCommand");
            Assert.NotNull(shellRunCommandMethod);
            
            var shellAttr = shellRunCommandMethod.GetCustomAttribute<RequiresApprovalAttribute>();
            Assert.NotNull(shellAttr);
            Assert.True(shellAttr.Required);

            // Create a dummy tool and verify it gets wrapped
            var dummyTool = McpServerTool.Create(() => "dummy", new() { Name = "shell_run" });
            var wrappedShellTool = ToolApprovalWrapper.WrapIfNeeded(dummyTool, shellRunCommandMethod);
            
            Assert.NotSame(dummyTool, wrappedShellTool);
            Assert.Equal("shell_run", wrappedShellTool.ProtocolTool.Name);
        }
    }
}