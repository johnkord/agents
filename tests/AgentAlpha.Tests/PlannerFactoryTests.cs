using AgentAlpha.Configuration;
using AgentAlpha.Extensions;
using AgentAlpha.Interfaces;
using AgentAlpha.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class PlannerFactoryTests
{
    [Fact]
    public void Factory_Returns_ChainedPlanner_When_Flag_True()
    {
        // Arrange – minimal config with flag enabled
        var cfg = new AgentConfiguration
        {
            OpenAiApiKey    = "dummy",
            UseChainedPlanner = true
        };

        var services = new ServiceCollection();
        services.AddLogging();                 // required by AddAgentAlphaServices
        services.AddAgentAlphaServices(cfg);
        var sp = services.BuildServiceProvider();

        // Act
        var planner = sp.GetRequiredService<IPlanner>();

        // Assert
        Assert.IsType<ChainedPlanner>(planner);
    }
}
