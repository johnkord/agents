using Xunit;
using AgentAlpha.Configuration;

namespace AgentAlpha.Tests.Configuration;

public class AgentConfigurationTests
{
    [Fact]
    public void GetPlanningModel_ReturnsMainModel_WhenPlanningModelIsNull()
    {
        // Arrange
        var config = new AgentConfiguration
        {
            Model = "gpt-4.1",
            PlanningModel = null
        };

        // Act
        var result = config.GetPlanningModel();

        // Assert
        Assert.Equal("gpt-4.1", result);
    }

    [Fact]
    public void GetPlanningModel_ReturnsPlanningModel_WhenSet()
    {
        // Arrange
        var config = new AgentConfiguration
        {
            Model = "gpt-4.1",
            PlanningModel = "o1-preview"
        };

        // Act
        var result = config.GetPlanningModel();

        // Assert
        Assert.Equal("o1-preview", result);
    }

    [Fact]
    public void ValidateModel_AcceptsReasoningModels()
    {
        // Arrange & Act & Assert
        var config1 = AgentConfiguration.FromEnvironment();
        Environment.SetEnvironmentVariable("AGENT_MODEL", "o1");
        Environment.SetEnvironmentVariable("PLANNING_MODEL", "o3");
        
        var config2 = AgentConfiguration.FromEnvironment();
        
        // Should not throw
        Assert.Equal("o1", config2.Model);
        Assert.Equal("o3", config2.PlanningModel);
        
        // Cleanup
        Environment.SetEnvironmentVariable("AGENT_MODEL", null);
        Environment.SetEnvironmentVariable("PLANNING_MODEL", null);
    }

    [Fact]
    public void ValidateModel_ThrowsForInvalidModel()
    {
        // Arrange
        Environment.SetEnvironmentVariable("AGENT_MODEL", "invalid-model");

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => AgentConfiguration.FromEnvironment());
        Assert.Contains("Invalid model value: 'invalid-model'", exception.Message);
        
        // Cleanup
        Environment.SetEnvironmentVariable("AGENT_MODEL", null);
    }

    [Fact]
    public void FromEnvironment_SetsPlanningModel_WhenEnvironmentVariableExists()
    {
        // Arrange
        Environment.SetEnvironmentVariable("PLANNING_MODEL", "o1-mini");

        // Act
        var config = AgentConfiguration.FromEnvironment();

        // Assert
        Assert.Equal("o1-mini", config.PlanningModel);
        
        // Cleanup
        Environment.SetEnvironmentVariable("PLANNING_MODEL", null);
    }

    [Fact]
    public void FromEnvironment_KeepsPlanningModelNull_WhenEnvironmentVariableNotSet()
    {
        // Arrange
        Environment.SetEnvironmentVariable("PLANNING_MODEL", null);

        // Act
        var config = AgentConfiguration.FromEnvironment();

        // Assert
        Assert.Null(config.PlanningModel);
    }

    [Theory]
    [InlineData("o1")]
    [InlineData("o1-mini")]
    [InlineData("o1-preview")]
    [InlineData("o3")]
    [InlineData("o3-mini")]
    [InlineData("gpt-4.1")]
    [InlineData("gpt-4")]
    [InlineData("gpt-4-turbo")]
    public void ValidateModel_AcceptsValidModels(string modelName)
    {
        // Arrange
        Environment.SetEnvironmentVariable("AGENT_MODEL", modelName);

        // Act & Assert - Should not throw
        var config = AgentConfiguration.FromEnvironment();
        Assert.Equal(modelName, config.Model);
        
        // Cleanup
        Environment.SetEnvironmentVariable("AGENT_MODEL", null);
    }
}