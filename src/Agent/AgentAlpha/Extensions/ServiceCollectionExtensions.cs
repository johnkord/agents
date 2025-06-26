using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AgentAlpha.Configuration;
using AgentAlpha.Interfaces;
using AgentAlpha.Services;
using OpenAIIntegration;

namespace AgentAlpha.Extensions;

/// <summary>
/// Extension methods for configuring services in the dependency injection container
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all AgentAlpha services with the dependency injection container
    /// </summary>
    /// <param name="services">The service collection to register services with</param>
    /// <param name="configuration">The agent configuration to use</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddAgentAlphaServices(this IServiceCollection services, AgentConfiguration configuration)
    {
        // Register configuration
        services.AddSingleton(configuration);
        
        // Register core services
        services.AddSingleton<IConnectionManager, ConnectionManager>();
        services.AddSingleton<ISessionManager, SessionManager>();
        services.AddSingleton<ISessionActivityLogger, SessionActivityLogger>();
        services.AddSingleton<IToolManager, ToolManager>();
        services.AddSingleton<IPlanningService, PlanningService>();
        services.AddSingleton<IToolSelector, ToolSelector>();
        services.AddSingleton<IConversationManager, ConversationManager>();
        services.AddSingleton<ITaskExecutor, TaskExecutor>();
        
        // Register OpenAI service
        services.AddSingleton(provider => new OpenAIResponsesService(configuration.OpenAiApiKey));
        
        return services;
    }
}