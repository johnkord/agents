using AgentAlpha.Configuration;
using AgentAlpha.Services;
using Microsoft.Extensions.Logging;
using Common.Interfaces.Session;
using OpenAIIntegration;

namespace AgentAlpha.Tests;

/// <summary>
/// Helper methods for tests
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// Create a mock simple tool manager for testing
    /// </summary>
    public static SimpleToolManager CreateMockToolManager(ILogger<SimpleToolManager>? logger = null)
    {
        logger ??= new Microsoft.Extensions.Logging.Abstractions.NullLogger<SimpleToolManager>();
        var config = new AgentConfiguration();
        var mockOpenAI = new MockSessionAwareOpenAIService();
        return new SimpleToolManager(logger, config, mockOpenAI);
    }

    /// <summary>
    /// Mock implementation of ISessionAwareOpenAIService for tests
    /// </summary>
    private class MockSessionAwareOpenAIService : ISessionAwareOpenAIService
    {
        public void SetActivityLogger(ISessionActivityLogger? activityLogger) { }
        public Task<OpenAIIntegration.Model.ResponsesCreateResponse> CreateResponseAsync(OpenAIIntegration.Model.ResponsesCreateRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}