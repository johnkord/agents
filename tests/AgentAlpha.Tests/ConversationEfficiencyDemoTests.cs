using AgentAlpha.Configuration;
using AgentAlpha.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace AgentAlpha.Tests;

public class ConversationEfficiencyDemoTests
{
    private readonly ITestOutputHelper _output;

    public ConversationEfficiencyDemoTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void DemonstrateConversationOptimization()
    {
        // Arrange
        var nullOpenAI = new NullOpenAIResponsesService();
        var nullLogger = NullLogger<ConversationManager>.Instance;
        var nullActivityLogger = new NullSessionActivityLogger();
        
        // Test without optimization (unlimited messages)
        var unlimitedConfig = new AgentConfiguration { Model = "gpt-4.1", MaxConversationMessages = 0 };
        var unlimitedManager = new ConversationManager(nullOpenAI, nullLogger, unlimitedConfig, nullActivityLogger);
        
        // Test with optimization (limit to 8 messages)
        var optimizedConfig = new AgentConfiguration { Model = "gpt-4.1", MaxConversationMessages = 8 };
        var optimizedManager = new ConversationManager(nullOpenAI, nullLogger, optimizedConfig, nullActivityLogger);

        // Simulate the same conversation in both managers
        var interactions = new[]
        {
            ("list files", "Here are the files..."),
            ("copy file", "File copied successfully"),
            ("check status", "Status is active"),
            ("run command", "Command executed"),
            ("verify result", "Result verified"),
            ("cleanup", "Cleanup completed")
        };

        // Act - Run conversations
        foreach (var manager in new[] { unlimitedManager, optimizedManager })
        {
            manager.InitializeConversation("You are a helpful assistant", "Help me with file operations");
            
            foreach (var (userTask, assistantResponse) in interactions)
            {
                manager.AddAssistantMessage(assistantResponse);
                manager.AddToolResults(new[] { $"Tool executed for: {userTask}" });
            }
        }

        // Assert and demonstrate efficiency gains
        var unlimitedStats = unlimitedManager.GetConversationStatistics();
        var optimizedStats = optimizedManager.GetConversationStatistics();

        _output.WriteLine("=== Conversation Efficiency Demonstration ===");
        _output.WriteLine($"Unlimited Messages: {unlimitedStats.TotalMessages} messages, ~{unlimitedStats.EstimatedTokens} tokens");
        _output.WriteLine($"Optimized (Max 8):  {optimizedStats.TotalMessages} messages, ~{optimizedStats.EstimatedTokens} tokens");
        
        var tokenReduction = ((double)(unlimitedStats.EstimatedTokens - optimizedStats.EstimatedTokens) / unlimitedStats.EstimatedTokens) * 100;
        _output.WriteLine($"Token Reduction: {tokenReduction:F1}%");
        
        var messageReduction = ((double)(unlimitedStats.TotalMessages - optimizedStats.TotalMessages) / unlimitedStats.TotalMessages) * 100;
        _output.WriteLine($"Message Reduction: {messageReduction:F1}%");

        // Verify optimization worked
        Assert.True(optimizedStats.TotalMessages <= 8, "Optimized conversation should not exceed 8 messages");
        Assert.True(optimizedStats.EstimatedTokens < unlimitedStats.EstimatedTokens, "Optimized conversation should use fewer tokens");
        Assert.True(optimizedStats.SystemMessages >= 1, "System messages should be preserved");
        
        // Show that we still maintained recent context
        var optimizedMessages = optimizedManager.GetCurrentMessages().ToList();
        Assert.True(optimizedMessages.Count > 2, "Should maintain some conversation context");
    }

    [Fact]
    public void DemonstrateImprovedToolResultFormat()
    {
        // Arrange
        var nullOpenAI = new NullOpenAIResponsesService();
        var nullLogger = NullLogger<ConversationManager>.Instance;
        var config = new AgentConfiguration { Model = "gpt-4.1" };
        var nullActivityLogger = new NullSessionActivityLogger();
        var manager = new ConversationManager(nullOpenAI, nullLogger, config, nullActivityLogger);

        // Act
        manager.InitializeConversation("You are a helpful assistant", "Execute some tools");
        manager.AddToolResults(new[] { 
            "Tool 'run_command' called with args {\"script\":\"ls -al\"}. Result: Exit code: 0",
            "Tool 'run_command' called with args {\"script\":\"pwd\"}. Result: /home/user"
        });

        var messages = manager.GetCurrentMessages().ToList();
        var stats = manager.GetConversationStatistics();

        // Assert improved format
        _output.WriteLine("=== Improved Tool Result Format ===");
        _output.WriteLine($"Total messages: {messages.Count}");
        _output.WriteLine($"Estimated tokens: {stats.EstimatedTokens}");
        
        // Find the tool result message
        var toolResultMessage = messages.FirstOrDefault(m => 
        {
            var content = m.GetType().GetProperty("content")?.GetValue(m)?.ToString() ?? "";
            return content.StartsWith("Tool results:");
        });
        
        Assert.NotNull(toolResultMessage);
        var toolContent = toolResultMessage.GetType().GetProperty("content")?.GetValue(toolResultMessage)?.ToString() ?? "";
        
        _output.WriteLine($"Tool result format: {toolContent.Split('\n')[0]}...");
        
        // Verify cleaner format
        Assert.StartsWith("Tool results:", toolContent);
        Assert.DoesNotContain("I executed the requested tools", toolContent); // Old redundant format
    }
}