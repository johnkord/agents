using AgentAlpha.Configuration;
using Xunit;

namespace AgentAlpha.Tests.Configuration;

public class AgentConfigurationTests
{
    [Fact]
    public void FromEnvironment_WithValidConfiguration_ShouldCreateValidConfig()
    {
        // Arrange
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-key");
        Environment.SetEnvironmentVariable("MCP_TRANSPORT", "stdio");
        Environment.SetEnvironmentVariable("AGENT_MODEL", "gpt-4.1");
        Environment.SetEnvironmentVariable("MAX_ITERATIONS", "15");
        
        try
        {
            // Act
            var config = AgentConfiguration.FromEnvironment();
            
            // Assert
            Assert.Equal("test-key", config.OpenAiApiKey);
            Assert.Equal(McpTransportType.Stdio, config.Transport);
            Assert.Equal("gpt-4.1", config.Model);
            Assert.Equal(15, config.MaxIterations);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
            Environment.SetEnvironmentVariable("MCP_TRANSPORT", null);
            Environment.SetEnvironmentVariable("AGENT_MODEL", null);
            Environment.SetEnvironmentVariable("MAX_ITERATIONS", null);
        }
    }
    
    [Theory]
    [InlineData("invalid-transport", "Invalid MCP_TRANSPORT value")]
    [InlineData("tcp", "Invalid MCP_TRANSPORT value")]
    public void FromEnvironment_WithInvalidTransport_ShouldThrow(string transport, string expectedMessage)
    {
        // Arrange
        Environment.SetEnvironmentVariable("MCP_TRANSPORT", transport);
        
        try
        {
            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => AgentConfiguration.FromEnvironment());
            Assert.Contains(expectedMessage, ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MCP_TRANSPORT", null);
        }
    }
    
    [Theory]
    [InlineData("gpt-5", "Invalid model value")]
    [InlineData("claude-3", "Invalid model value")]
    [InlineData("", null)] // Empty should not throw, uses default
    public void FromEnvironment_WithInvalidModel_ShouldHandleAppropriately(string model, string expectedMessage)
    {
        // Arrange
        Environment.SetEnvironmentVariable("AGENT_MODEL", model);
        
        try
        {
            if (expectedMessage != null)
            {
                // Act & Assert - Should throw
                var ex = Assert.Throws<InvalidOperationException>(() => AgentConfiguration.FromEnvironment());
                Assert.Contains(expectedMessage, ex.Message);
            }
            else
            {
                // Act & Assert - Should use default
                var config = AgentConfiguration.FromEnvironment();
                Assert.Equal("gpt-4.1", config.Model); // Default value
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("AGENT_MODEL", null);
        }
    }
    
    [Theory]
    [InlineData("-1", "Invalid MAX_ITERATIONS value")]
    [InlineData("0", "Invalid MAX_ITERATIONS value")]
    [InlineData("abc", "Invalid MAX_ITERATIONS value")]
    public void FromEnvironment_WithInvalidMaxIterations_ShouldThrow(string value, string expectedMessage)
    {
        // Arrange
        Environment.SetEnvironmentVariable("MAX_ITERATIONS", value);
        
        try
        {
            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => AgentConfiguration.FromEnvironment());
            Assert.Contains(expectedMessage, ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MAX_ITERATIONS", null);
        }
    }
    
    [Fact]
    public void FromEnvironment_WithHttpTransportNoUrl_ShouldThrow()
    {
        // Arrange
        Environment.SetEnvironmentVariable("MCP_TRANSPORT", "http");
        Environment.SetEnvironmentVariable("MCP_SERVER_URL", "");
        
        try
        {
            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => AgentConfiguration.FromEnvironment());
            Assert.Contains("MCP_SERVER_URL is required when using HTTP transport", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MCP_TRANSPORT", null);
            Environment.SetEnvironmentVariable("MCP_SERVER_URL", null);
        }
    }
    
    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("FALSE", false)]
    [InlineData("", false)] // Default
    public void FromEnvironment_UseChainedPlanner_ShouldParseCorrectly(string value, bool expected)
    {
        // Arrange
        Environment.SetEnvironmentVariable("USE_CHAINED_PLANNER", value);
        
        try
        {
            // Act
            var config = AgentConfiguration.FromEnvironment();
            
            // Assert
            Assert.Equal(expected, config.UseChainedPlanner);
        }
        finally
        {
            Environment.SetEnvironmentVariable("USE_CHAINED_PLANNER", null);
        }
    }
    
    [Theory]
    [InlineData("0.5", 0.5)]
    [InlineData("0", 0.0)]
    [InlineData("1", 1.0)]
    [InlineData("", 0.8)] // Default
    public void FromEnvironment_PlanQualityTarget_ShouldParseCorrectly(string value, double expected)
    {
        // Arrange
        Environment.SetEnvironmentVariable("PLAN_QUALITY_TARGET", value);
        
        try
        {
            // Act
            var config = AgentConfiguration.FromEnvironment();
            
            // Assert
            Assert.Equal(expected, config.PlanQualityTarget);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PLAN_QUALITY_TARGET", null);
        }
    }
    
    [Theory]
    [InlineData("-0.1")]
    [InlineData("1.1")]
    [InlineData("2")]
    public void FromEnvironment_InvalidPlanQualityTarget_ShouldThrow(string value)
    {
        // Arrange
        Environment.SetEnvironmentVariable("PLAN_QUALITY_TARGET", value);
        
        try
        {
            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => AgentConfiguration.FromEnvironment());
            Assert.Contains("PLAN_QUALITY_TARGET must be between 0 and 1", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PLAN_QUALITY_TARGET", null);
        }
    }
    
    [Fact]
    public void GetPlanningModel_WithPlanningModelSet_ShouldReturnPlanningModel()
    {
        // Arrange
        var config = new AgentConfiguration
        {
            Model = "gpt-4.1",
            PlanningModel = "o1"
        };
        
        // Act
        var result = config.GetPlanningModel();
        
        // Assert
        Assert.Equal("o1", result);
    }
    
    [Fact]
    public void GetPlanningModel_WithoutPlanningModel_ShouldReturnDefaultModel()
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
}
