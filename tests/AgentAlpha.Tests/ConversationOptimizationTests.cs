using AgentAlpha.Configuration;
using AgentAlpha.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AgentAlpha.Tests;

public class ConversationOptimizationTests
{
    [Fact]
    public void ConversationManager_WithMaxMessages_OptimizesLength()
    {
        // Arrange
        var nullOpenAI = new NullOpenAIResponsesService();
        var nullLogger = NullLogger<ConversationManager>.Instance;
        var config = new AgentConfiguration 
        { 
            Model = "gpt-4.1",
            MaxConversationMessages = 5 // Small limit for testing
        };
        var nullActivityLogger = new NullSessionActivityLogger();
        
        var conversationManager = new ConversationManager(nullOpenAI, nullLogger, config, nullActivityLogger);
        
        // Act
        conversationManager.InitializeConversation("You are a helpful assistant", "Task 1");
        conversationManager.AddAssistantMessage("Response 1");
        conversationManager.AddToolResults(new[] { "Tool result 1" });
        conversationManager.AddAssistantMessage("Response 2");
        conversationManager.AddToolResults(new[] { "Tool result 2" });
        conversationManager.AddAssistantMessage("Response 3");
        
        // Assert
        var messages = conversationManager.GetCurrentMessages().ToList();
        Assert.True(messages.Count <= 5, $"Expected <= 5 messages, got {messages.Count}");
        
        var stats = conversationManager.GetConversationStatistics();
        Assert.True(stats.TotalMessages <= 5);
        Assert.True(stats.SystemMessages >= 1); // Should keep system message
    }

    [Fact] 
    public void ConversationManager_WithoutMaxMessages_DoesNotOptimize()
    {
        // Arrange
        var nullOpenAI = new NullOpenAIResponsesService();
        var nullLogger = NullLogger<ConversationManager>.Instance;
        var config = new AgentConfiguration 
        { 
            Model = "gpt-4.1",
            MaxConversationMessages = 0 // No limit
        };
        var nullActivityLogger = new NullSessionActivityLogger();
        
        var conversationManager = new ConversationManager(nullOpenAI, nullLogger, config, nullActivityLogger);
        
        // Act
        conversationManager.InitializeConversation("You are a helpful assistant", "Task 1");
        conversationManager.AddAssistantMessage("Response 1");
        conversationManager.AddToolResults(new[] { "Tool result 1" });
        conversationManager.AddAssistantMessage("Response 2");
        conversationManager.AddToolResults(new[] { "Tool result 2" });
        
        // Assert
        var messages = conversationManager.GetCurrentMessages().ToList();
        Assert.True(messages.Count > 5); // Should have all messages
        
        var stats = conversationManager.GetConversationStatistics();
        Assert.True(stats.TotalMessages > 5);
    }

    [Fact]
    public void ConversationStatistics_CalculatesCorrectly()
    {
        // Arrange
        var nullOpenAI = new NullOpenAIResponsesService();
        var nullLogger = NullLogger<ConversationManager>.Instance;
        var config = new AgentConfiguration { Model = "gpt-4.1" };
        var nullActivityLogger = new NullSessionActivityLogger();
        
        var conversationManager = new ConversationManager(nullOpenAI, nullLogger, config, nullActivityLogger);
        
        // Act
        conversationManager.InitializeConversation("System prompt", "User task");
        conversationManager.AddAssistantMessage("Assistant response");
        
        var stats = conversationManager.GetConversationStatistics();
        
        // Assert
        Assert.Equal(3, stats.TotalMessages);
        Assert.Equal(1, stats.SystemMessages);
        Assert.Equal(1, stats.UserMessages);
        Assert.Equal(1, stats.AssistantMessages);
        Assert.True(stats.EstimatedTokens > 0);
    }

    [Fact]
    public void AddToolResults_UsesImprovedFormat()
    {
        // Arrange
        var nullOpenAI = new NullOpenAIResponsesService();
        var nullLogger = NullLogger<ConversationManager>.Instance;
        var config = new AgentConfiguration { Model = "gpt-4.1" };
        var nullActivityLogger = new NullSessionActivityLogger();
        
        var conversationManager = new ConversationManager(nullOpenAI, nullLogger, config, nullActivityLogger);
        conversationManager.InitializeConversation("System prompt", "User task");
        
        // Act
        conversationManager.AddToolResults(new[] { "Tool executed successfully", "File created" });
        
        var messages = conversationManager.GetCurrentMessages().ToList();
        
        // Assert - Should have system, user, assistant (tool results), user (continuation)
        Assert.Equal(4, messages.Count);
        
        // Check that tool results are formatted as "Tool results:" prefix
        var toolResultMessage = messages[2];
        var contentProperty = toolResultMessage.GetType().GetProperty("content");
        var content = contentProperty?.GetValue(toolResultMessage)?.ToString() ?? "";
        
        Assert.StartsWith("Tool results:", content);
        Assert.Contains("Tool executed successfully", content);
        Assert.Contains("File created", content);
        
        // Check continuation message
        var continuationMessage = messages[3];
        var continuationContent = continuationMessage.GetType().GetProperty("content")?.GetValue(continuationMessage)?.ToString() ?? "";
        Assert.Contains("continue", continuationContent.ToLowerInvariant());
    }
}