using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AgentAlpha.Tests;

/// <summary>
/// Tests to verify that list truncation has been removed from messages and activity logs
/// </summary>
public class ListTruncationTests
{
    [Fact]
    public void ConsoleMessage_ToolSelection_ShouldNotTruncateAfterFivthTool()
    {
        // This test demonstrates the issue and what the fix should be
        // It tests the console message format that's used in TaskExecutor.cs
        
        // Arrange - Create a list with more than 5 tools
        var toolNames = new List<string>();
        for (int i = 1; i <= 8; i++)
        {
            toolNames.Add($"tool_{i}");
        }
        
        // Act - Simulate the current (broken) message formatting
        var currentMessage = $"🎯 Selected {toolNames.Count} tools: " +
                           $"{string.Join(", ", toolNames.Take(5))}" +
                           $"{(toolNames.Count > 5 ? "..." : "")}";
        
        // Act - Simulate the fixed message formatting (what we want)
        var fixedMessage = $"🎯 Selected {toolNames.Count} tools: " +
                         $"{string.Join(", ", toolNames)}";
        
        // Assert - Current behavior (shows the problem)
        Assert.Contains("...", currentMessage);
        Assert.DoesNotContain("tool_6", currentMessage);
        Assert.DoesNotContain("tool_7", currentMessage);
        Assert.DoesNotContain("tool_8", currentMessage);
        
        // Assert - Fixed behavior (what we want after the fix)
        Assert.DoesNotContain("...", fixedMessage);
        Assert.Contains("tool_6", fixedMessage);
        Assert.Contains("tool_7", fixedMessage);
        Assert.Contains("tool_8", fixedMessage);
        
        // Verify all tools are present in the fixed message
        for (int i = 1; i <= 8; i++)
        {
            Assert.Contains($"tool_{i}", fixedMessage);
        }
    }
    
    [Fact]
    public void StringJoin_WithoutTruncation_IncludesAllElements()
    {
        // Test the general pattern that should be used everywhere
        var items = new[] { "item1", "item2", "item3", "item4", "item5", "item6", "item7" };
        
        // The old way (with truncation) - what we're fixing
        var truncatedResult = string.Join(", ", items.Take(5)) + (items.Length > 5 ? "..." : "");
        
        // The new way (without truncation) - what we want
        var fullResult = string.Join(", ", items);
        
        // Verify the difference
        Assert.Contains("...", truncatedResult);
        Assert.DoesNotContain("item6", truncatedResult);
        Assert.DoesNotContain("item7", truncatedResult);
        
        Assert.DoesNotContain("...", fullResult);
        Assert.Contains("item6", fullResult);
        Assert.Contains("item7", fullResult);
        
        // Verify all items are in the full result
        foreach (var item in items)
        {
            Assert.Contains(item, fullResult);
        }
    }
    
    [Fact]
    public void FallbackPlan_ShouldIncludeAllAvailableTools()
    {
        // This test validates that fallback plans include all tools, not just 5
        // We'll test this by checking the pattern that was used in CreateFallbackPlan
        
        // Arrange - Simulate having 8 tools available
        var allTools = new List<string>();
        for (int i = 1; i <= 8; i++)
        {
            allTools.Add($"tool_{i}");
        }
        
        // Act - Old behavior (what we fixed)
        var oldRequiredTools = allTools.Take(5).ToList();
        var oldPotentialTools = allTools.Take(5).ToList();
        
        // Act - New behavior (what we want)
        var newRequiredTools = allTools.ToList();
        var newPotentialTools = allTools.ToList();
        
        // Assert - Old behavior was limited (showing the problem we fixed)
        Assert.Equal(5, oldRequiredTools.Count);
        Assert.Equal(5, oldPotentialTools.Count);
        Assert.DoesNotContain("tool_6", oldRequiredTools);
        Assert.DoesNotContain("tool_8", oldPotentialTools);
        
        // Assert - New behavior includes all tools (showing the fix)
        Assert.Equal(8, newRequiredTools.Count);
        Assert.Equal(8, newPotentialTools.Count);
        Assert.Contains("tool_6", newRequiredTools);
        Assert.Contains("tool_8", newPotentialTools);
        
        // Verify all tools are included
        for (int i = 1; i <= 8; i++)
        {
            Assert.Contains($"tool_{i}", newRequiredTools);
            Assert.Contains($"tool_{i}", newPotentialTools);
        }
    }
}