using Xunit;
using Microsoft.Extensions.Logging;
using AgentAlpha.Services;
using AgentAlpha.Models;
using System.IO;

namespace AgentAlpha.Tests;

public class SessionResumeIntegrationTests : IDisposable
{
    private readonly SessionManager _sessionManager;
    private readonly ILogger<SessionManager> _sessionLogger;
    private readonly string _testDbPath;

    public SessionResumeIntegrationTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _sessionLogger = loggerFactory.CreateLogger<SessionManager>();
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_session_resume_{Guid.NewGuid()}.db");
        _sessionManager = new SessionManager(_sessionLogger, _testDbPath);
    }

    public void Dispose()
    {
        // Clean up test database
        if (File.Exists(_testDbPath))
        {
            File.Delete(_testDbPath);
        }
    }

    [Fact]
    public async Task SessionManager_GetSessionByName_ReturnsCorrectSession()
    {
        // Arrange
        var session1 = await _sessionManager.CreateSessionAsync("duplicate-name");
        await Task.Delay(10); // Ensure different timestamps
        var session2 = await _sessionManager.CreateSessionAsync("duplicate-name");

        // Act
        var result = await _sessionManager.GetSessionByNameAsync("duplicate-name");

        // Assert - Should return the most recent session
        Assert.NotNull(result);
        Assert.Equal(session2.SessionId, result.SessionId);
        Assert.Equal("duplicate-name", result.Name);
    }

    [Fact]
    public async Task SessionManager_GetSessionByName_NonExistent_ReturnsNull()
    {
        // Act
        var result = await _sessionManager.GetSessionByNameAsync("non-existent-session");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SessionName_Workflow_SimulatesRealUsageScenario()
    {
        // This test simulates the exact scenario from the issue:
        // 1. First run with --session-name "2025-06-23-s1" should create new session
        // 2. Second run with --session-name "2025-06-23-s1" should find and reuse existing session

        var sessionName = "2025-06-23-s1";

        // First run simulation: check if session exists, it doesn't, so create new one
        var existingSession = await _sessionManager.GetSessionByNameAsync(sessionName);
        Assert.Null(existingSession); // Should not exist yet

        var newSession = await _sessionManager.CreateSessionAsync(sessionName);
        Assert.NotNull(newSession);
        Assert.Equal(sessionName, newSession.Name);
        Assert.NotEmpty(newSession.SessionId);

        var firstRunSessionId = newSession.SessionId;

        // Second run simulation: check if session exists, it should, so reuse it
        var resumedSession = await _sessionManager.GetSessionByNameAsync(sessionName);
        Assert.NotNull(resumedSession);
        Assert.Equal(sessionName, resumedSession.Name);
        Assert.Equal(firstRunSessionId, resumedSession.SessionId);

        // Verify we're getting the same session, not a new one
        Assert.Equal(newSession.SessionId, resumedSession.SessionId);
        Assert.Equal(newSession.CreatedAt, resumedSession.CreatedAt);
    }

    [Fact]
    public async Task SessionName_MultipleRuns_AlwaysReuseSameSession()
    {
        var sessionName = "persistent-session";

        // Create initial session
        var session1 = await _sessionManager.CreateSessionAsync(sessionName);
        
        // Simulate multiple subsequent runs
        for (int i = 0; i < 5; i++)
        {
            var foundSession = await _sessionManager.GetSessionByNameAsync(sessionName);
            Assert.NotNull(foundSession);
            Assert.Equal(session1.SessionId, foundSession.SessionId);
            Assert.Equal(sessionName, foundSession.Name);
            
            // Update session (simulate saving conversation state)
            foundSession.ConversationState = $"Updated state {i}";
            await _sessionManager.SaveSessionAsync(foundSession);
        }

        // Verify final state
        var finalSession = await _sessionManager.GetSessionByNameAsync(sessionName);
        Assert.NotNull(finalSession);
        Assert.Equal(session1.SessionId, finalSession.SessionId);
        Assert.Equal("Updated state 4", finalSession.ConversationState);
    }
}