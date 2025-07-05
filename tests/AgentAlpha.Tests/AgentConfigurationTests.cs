using Xunit;
using AgentAlpha.Configuration;
using System;

namespace AgentAlpha.Tests;

public class AgentConfigurationTests
{
    [Fact]
    public void FromEnvironment_WithValidValues_ShouldSucceed()
    {
        // Arrange - Set valid environment variables
        Environment.SetEnvironmentVariable("MCP_TRANSPORT", "stdio");
        Environment.SetEnvironmentVariable("MAX_ITERATIONS", "5");
        Environment.SetEnvironmentVariable("MAX_CONVERSATION_MESSAGES", "100");
        
        // Act
        var config = AgentConfiguration.FromEnvironment();
        
        // Assert
        Assert.NotNull(config);
        Assert.Equal(5, config.MaxIterations);
        Assert.Equal(100, config.MaxConversationMessages);
        
        // Cleanup
        Environment.SetEnvironmentVariable("MCP_TRANSPORT", null);
        Environment.SetEnvironmentVariable("MAX_ITERATIONS", null);
        Environment.SetEnvironmentVariable("MAX_CONVERSATION_MESSAGES", null);
    }

    [Fact]
    public void FromEnvironment_WithInvalidTransport_ShouldThrow()
    {
        // Arrange
        Environment.SetEnvironmentVariable("MCP_TRANSPORT", "invalid-transport");
        
        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => AgentConfiguration.FromEnvironment());
        Assert.Contains("Invalid MCP_TRANSPORT value", exception.Message);
        
        // Cleanup
        Environment.SetEnvironmentVariable("MCP_TRANSPORT", null);
    }

    [Fact]
    public void FromEnvironment_WithInvalidMaxIterations_ShouldThrow()
    {
        // Arrange
        Environment.SetEnvironmentVariable("MAX_ITERATIONS", "-1");
        
        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => AgentConfiguration.FromEnvironment());
        Assert.Contains("Invalid MAX_ITERATIONS", exception.Message);
        
        // Cleanup
        Environment.SetEnvironmentVariable("MAX_ITERATIONS", null);
    }

    [Fact]
    public void FromEnvironment_WithHttpTransportAndUrl_ShouldSucceed()
    {
        // Arrange
        Environment.SetEnvironmentVariable("MCP_TRANSPORT", "http");
        Environment.SetEnvironmentVariable("MCP_SERVER_URL", "http://localhost:3000");
        
        // Act
        var config = AgentConfiguration.FromEnvironment();
        
        // Assert
        Assert.NotNull(config);
        Assert.Equal("http://localhost:3000", config.ServerUrl);
        
        // Cleanup
        Environment.SetEnvironmentVariable("MCP_TRANSPORT", null);
        Environment.SetEnvironmentVariable("MCP_SERVER_URL", null);
    }
}