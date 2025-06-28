using Xunit;
using AgentAlpha.Models;
using AgentAlpha.Configuration;

namespace AgentAlpha.Tests;

/// <summary>
/// Tests for the tool selection configuration and basic functionality
/// </summary>
public class ToolSelectionTests
{
    [Fact]
    public void ToolSelectionConfig_Default_HasCorrectValues()
    {
        // Arrange & Act
        var config = ToolSelectionConfig.Default;

        // Assert
        Assert.Equal(10, config.MaxToolsPerRequest);
        Assert.True(config.UseLLMSelection);
        Assert.Equal("gpt-4.1-nano", config.SelectionModel);
        Assert.Equal(0.1, config.SelectionTemperature);
        Assert.True(config.AllowDynamicExpansion);
        Assert.Equal(3, config.MaxAdditionalToolsPerIteration);
        Assert.Contains("complete_task", config.EssentialTools);
    }

    [Fact]
    public void ToolSelectionConfig_Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var config = new ToolSelectionConfig
        {
            MaxToolsPerRequest = 5,
            UseLLMSelection = false,
            SelectionModel = "gpt-4",
            SelectionTemperature = 0.2,
            AllowDynamicExpansion = false,
            MaxAdditionalToolsPerIteration = 1
        };

        // Assert
        Assert.Equal(5, config.MaxToolsPerRequest);
        Assert.False(config.UseLLMSelection);
        Assert.Equal("gpt-4", config.SelectionModel);
        Assert.Equal(0.2, config.SelectionTemperature);
        Assert.False(config.AllowDynamicExpansion);
        Assert.Equal(1, config.MaxAdditionalToolsPerIteration);
    }

    [Fact]
    public void ToolRelevanceScore_PropertiesSetCorrectly()
    {
        // Arrange & Act
        var score = new ToolRelevanceScore
        {
            ToolName = "test_tool",
            RelevanceScore = 0.85,
            Reasoning = "Highly relevant for the task",
            Category = "math"
        };

        // Assert
        Assert.Equal("test_tool", score.ToolName);
        Assert.Equal(0.85, score.RelevanceScore);
        Assert.Equal("Highly relevant for the task", score.Reasoning);
        Assert.Equal("math", score.Category);
    }

    [Fact]
    public void ToolSelectionResult_PropertiesSetCorrectly()
    {
        // Arrange
        var scores = new List<ToolRelevanceScore>
        {
            new() { ToolName = "tool1", RelevanceScore = 0.9 },
            new() { ToolName = "tool2", RelevanceScore = 0.7 }
        };

        // Act
        var result = new ToolSelectionResult
        {
            ScoredTools = scores,
            SelectedToolNames = new[] { "tool1", "tool2" },
            SelectionReasoning = "Selected based on relevance",
            SelectionTime = TimeSpan.FromMilliseconds(150)
        };

        // Assert
        Assert.Equal(2, result.ScoredTools.Count);
        Assert.Equal(2, result.SelectedToolNames.Length);
        Assert.Equal("Selected based on relevance", result.SelectionReasoning);
        Assert.Equal(150, result.SelectionTime.TotalMilliseconds);
    }

    [Fact]
    public void ToolSelectionConfig_EssentialTools_CanBeModified()
    {
        // Arrange
        var config = new ToolSelectionConfig();
        
        // Act
        config.EssentialTools.Add("custom_essential_tool");
        
        // Assert
        Assert.Contains("complete_task", config.EssentialTools);
        Assert.Contains("custom_essential_tool", config.EssentialTools);
        Assert.Equal(2, config.EssentialTools.Count);
    }

    [Fact]
    public void ToolFilterConfig_CompleteTask_CannotBeBlacklisted()
    {
        // Arrange
        var filter = new ToolFilterConfig();
        filter.Blacklist.Add("complete_task");
        filter.Blacklist.Add("other_tool");
        
        // Act & Assert
        Assert.True(filter.ShouldIncludeTool("complete_task"));
        Assert.False(filter.ShouldIncludeTool("other_tool"));
    }

    [Fact]
    public void ToolFilterConfig_CompleteTask_AlwaysIncluded_CaseInsensitive()
    {
        // Arrange
        var filter = new ToolFilterConfig();
        filter.Blacklist.Add("COMPLETE_TASK"); // Different case
        filter.Blacklist.Add("Complete_Task"); // Mixed case
        
        // Act & Assert
        Assert.True(filter.ShouldIncludeTool("complete_task"));
        Assert.True(filter.ShouldIncludeTool("COMPLETE_TASK"));
        Assert.True(filter.ShouldIncludeTool("Complete_Task"));
    }

    [Fact]
    public void ToolFilterConfig_CompleteTask_IncludedEvenWithWhitelist()
    {
        // Arrange
        var filter = new ToolFilterConfig();
        filter.Whitelist.Add("some_other_tool");
        // Note: complete_task is NOT in the whitelist
        
        // Act & Assert
        Assert.True(filter.ShouldIncludeTool("complete_task"));
        Assert.True(filter.ShouldIncludeTool("some_other_tool"));
        Assert.False(filter.ShouldIncludeTool("not_in_whitelist"));
    }

    [Fact]
    public void ToolFilterConfig_CompleteTask_IncludedEvenInBlacklistAndNotInWhitelist()
    {
        // Arrange
        var filter = new ToolFilterConfig();
        filter.Whitelist.Add("allowed_tool");
        filter.Blacklist.Add("complete_task");
        filter.Blacklist.Add("blocked_tool");
        
        // Act & Assert
        Assert.True(filter.ShouldIncludeTool("complete_task")); // Always included despite being blacklisted
        Assert.True(filter.ShouldIncludeTool("allowed_tool"));   // In whitelist
        Assert.False(filter.ShouldIncludeTool("blocked_tool"));  // In blacklist
        Assert.False(filter.ShouldIncludeTool("random_tool"));   // Not in whitelist
    }

    [Fact]
    public void ToolFilterConfig_NormalFiltering_StillWorks()
    {
        // Arrange
        var filter = new ToolFilterConfig();
        filter.Blacklist.Add("blocked_tool");
        
        // Act & Assert
        Assert.False(filter.ShouldIncludeTool("blocked_tool"));
        Assert.True(filter.ShouldIncludeTool("allowed_tool"));
    }

    [Fact]
    public void ToolFilterConfig_CompleteTask_NeverBlacklisted_IssueScenario()
    {
        // Arrange - Simulate the exact scenario from the issue
        var filter = new ToolFilterConfig();
        
        // Act - Try to blacklist complete_task
        filter.Blacklist.Add("complete_task");
        filter.Blacklist.Add("dangerous_tool");
        filter.Blacklist.Add("unwanted_tool");
        
        // Assert - complete_task should always be included despite being blacklisted
        Assert.True(filter.ShouldIncludeTool("complete_task"), 
            "complete_task tool should NEVER be blacklisted - it's essential for task completion signaling");
        Assert.False(filter.ShouldIncludeTool("dangerous_tool"));
        Assert.False(filter.ShouldIncludeTool("unwanted_tool"));
    }
}