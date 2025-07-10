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
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

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
        
        // Core execution services
        services.AddSingleton<SimpleToolManager>();
        services.AddSingleton<IConversationManager, ConversationManager>();
        services.AddTransient<WorkerConversation>();            // P5 – new instance per worker
        services.AddSingleton<SimpleTaskExecutor>();
        
        // Keep TaskExecutor interface for backward compatibility but register simplified implementation
        services.AddSingleton<ITaskExecutor>(provider => provider.GetRequiredService<SimpleTaskExecutor>());
        
        // Register OpenAI services
        services.AddSingleton<IOpenAIResponsesService>(provider => 
        {
            var apiKey = configuration.OpenAiApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("OpenAI API key is not configured. Please set OPENAI_API_KEY environment variable.");
            }
            return new OpenAIResponsesService(apiKey);
        });
        
        services.AddSingleton<ISessionAwareOpenAIService>(provider =>
        {
            var innerService = provider.GetRequiredService<IOpenAIResponsesService>();
            var logger = provider.GetRequiredService<ILogger<SessionAwareOpenAIService>>();
            return new SessionAwareOpenAIService(innerService, logger);
        });
        
        // Register planning services
        services.AddSingleton<PlanningService>();
        services.AddSingleton<ChainedPlanner>();
        services.AddSingleton<IPlanner>(sp =>
        {
            var cfg = sp.GetRequiredService<AgentConfiguration>();
            var logger = sp.GetRequiredService<ILogger<IPlanner>>();

            try
            {
                return sp.GetRequiredService<PlanningService>();
                // Disable ChainedPlanner for now
                /* 
                return cfg.UseChainedPlanner
                    ? sp.GetRequiredService<ChainedPlanner>()
                    : sp.GetRequiredService<PlanningService>();
                    */
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to resolve planner implementation");
                throw;
            }
        });
        
        // Register routing & fast-path services (V2 features)
        services.AddSingleton<ITaskRouter, TaskRouter>();
        services.AddSingleton<IFastPathExecutor, FastPathExecutor>();
        services.AddSingleton<IPlanEvaluator, PlanEvaluator>();
        services.AddSingleton<PlanRefinementLoop>();

        // Validate service registration
        ValidateServiceRegistration(services);

        return services;
    }
    
    /// <summary>
    /// Validates that all required services are properly registered
    /// </summary>
    private static void ValidateServiceRegistration(IServiceCollection services)
    {
        var requiredServices = new[]
        {
            typeof(AgentConfiguration),
            typeof(IConnectionManager),
            typeof(ISessionManager),
            typeof(ISessionActivityLogger),
            typeof(SimpleToolManager),
            typeof(IConversationManager),
            typeof(ITaskExecutor),
            typeof(IOpenAIResponsesService),
            typeof(ISessionAwareOpenAIService),
            typeof(IPlanner),
            typeof(ITaskRouter),
            typeof(IFastPathExecutor),
            typeof(IPlanEvaluator),
            typeof(PlanRefinementLoop)
        };
        
        var registeredTypes = services.Select(s => s.ServiceType).ToHashSet();
        var missingServices = requiredServices.Where(t => !registeredTypes.Contains(t)).ToList();
        
        if (missingServices.Any())
        {
            throw new InvalidOperationException(
                $"The following required services are not registered: {string.Join(", ", missingServices.Select(t => t.Name))}");
        }
    }
}