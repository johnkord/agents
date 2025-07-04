using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AgentAlpha.Configuration;
using AgentAlpha.Interfaces;
using AgentAlpha.Services;
using OpenAIIntegration;
using Common.Interfaces.Session;
using Common.Services.Session;
using Common.Interfaces.Tools;

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
        
        // Register HTTP client for Session Service
        services.AddHttpClient();
        services.AddSingleton<ISessionManager>(provider =>
        {
            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
            var logger = provider.GetRequiredService<ILogger<SessionServiceClient>>();
            var httpClient = httpClientFactory.CreateClient();
            var sessionClient = new SessionServiceClient(httpClient, logger);
            
            // Configure Session Service URL from environment or default
            var sessionServiceUrl = Environment.GetEnvironmentVariable("SESSION_SERVICE_URL") ?? "http://localhost:5001";
            sessionClient.SetBaseUrl(sessionServiceUrl);
            
            return sessionClient;
        });
        
        services.AddSingleton<ISessionActivityLogger, SessionActivityLogger>();
        
        // Simplified architecture - removed excessive abstractions
        services.AddSingleton<SimpleToolManager>();
        services.AddSingleton<IConversationManager, ConversationManager>();
        services.AddSingleton<SimpleTaskExecutor>();
        
        // Keep TaskExecutor interface for backward compatibility but register simplified implementation
        services.AddSingleton<ITaskExecutor>(provider => provider.GetRequiredService<SimpleTaskExecutor>());
        
        // Register OpenAI services
        services.AddSingleton<IOpenAIResponsesService>(provider => new OpenAIResponsesService(configuration.OpenAiApiKey));
        services.AddSingleton<ISessionAwareOpenAIService>(provider =>
        {
            var innerService = provider.GetRequiredService<IOpenAIResponsesService>();
            var logger = provider.GetRequiredService<ILogger<SessionAwareOpenAIService>>();
            return new SessionAwareOpenAIService(innerService, logger);
        });
        
        // Register planning service
        services.AddSingleton<PlanningService>();
        
        // Register routing & fast-path (deduplicated)
        services.AddSingleton<ITaskRouter, TaskRouter>();
        services.AddSingleton<IFastPathExecutor, FastPathExecutor>();

        return services;
    }
}