using Xunit;
using Microsoft.Extensions.Logging;
using AgentAlpha.Services;
using AgentAlpha.Models;
using System.IO;

namespace AgentAlpha.Tests;

public class SessionManagerTests : IDisposable
{
    private readonly SessionManager _sessionManager;
    private readonly ILogger<SessionManager> _logger;
    private readonly string _testDbPath;

    public SessionManagerTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<SessionManager>();
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_agent_sessions_{Guid.NewGuid()}.db");
        _sessionManager = new SessionManager(_logger, _testDbPath);
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
    public async Task CreateSessionAsync_ShouldCreateNewSession()
    {
        // Act
        var session = await _sessionManager.CreateSessionAsync("Test Session");

        // Assert
        Assert.NotNull(session);
        Assert.NotEmpty(session.SessionId);
        Assert.Equal("Test Session", session.Name);
        Assert.Equal(SessionStatus.Active, session.Status);
        Assert.True(session.CreatedAt <= DateTime.UtcNow);
        Assert.Equal(session.CreatedAt, session.LastUpdatedAt);
    }

    [Fact]
    public async Task CreateSessionAsync_WithEmptyName_ShouldGenerateDefaultName()
    {
        // Act
        var session = await _sessionManager.CreateSessionAsync();

        // Assert
        Assert.NotNull(session);
        Assert.NotEmpty(session.SessionId);
        Assert.StartsWith("Session ", session.Name);
        Assert.Equal(SessionStatus.Active, session.Status);
    }

    [Fact]
    public async Task GetSessionAsync_ExistingSession_ShouldReturnSession()
    {
        // Arrange
        var originalSession = await _sessionManager.CreateSessionAsync("Test Session");

        // Act
        var retrievedSession = await _sessionManager.GetSessionAsync(originalSession.SessionId);

        // Assert
        Assert.NotNull(retrievedSession);
        Assert.Equal(originalSession.SessionId, retrievedSession.SessionId);
        Assert.Equal(originalSession.Name, retrievedSession.Name);
        Assert.Equal(originalSession.Status, retrievedSession.Status);
    }

    [Fact]
    public async Task GetSessionAsync_NonExistentSession_ShouldReturnNull()
    {
        // Act
        var session = await _sessionManager.GetSessionAsync("non-existent-id");

        // Assert
        Assert.Null(session);
    }

    [Fact]
    public async Task SaveSessionAsync_ShouldUpdateExistingSession()
    {
        // Arrange
        var session = await _sessionManager.CreateSessionAsync("Original Name");
        session.Name = "Updated Name";
        session.Status = SessionStatus.Completed;
        var testMessages = new List<object>
        {
            new { role = "system", content = "System prompt" },
            new { role = "user", content = "User message" }
        };
        session.SetConversationMessages(testMessages);

        // Act
        await _sessionManager.SaveSessionAsync(session);
        var retrievedSession = await _sessionManager.GetSessionAsync(session.SessionId);

        // Assert
        Assert.NotNull(retrievedSession);
        Assert.Equal("Updated Name", retrievedSession.Name);
        Assert.Equal(SessionStatus.Completed, retrievedSession.Status);
        Assert.NotEmpty(retrievedSession.ConversationState);
        
        var retrievedMessages = retrievedSession.GetConversationMessages();
        Assert.Equal(2, retrievedMessages.Count);
    }

    [Fact]
    public async Task ListSessionsAsync_ShouldReturnAllSessions()
    {
        // Arrange
        var session1 = await _sessionManager.CreateSessionAsync("Session 1");
        var session2 = await _sessionManager.CreateSessionAsync("Session 2");

        // Act
        var sessions = await _sessionManager.ListSessionsAsync();

        // Assert
        Assert.True(sessions.Count >= 2);
        Assert.Contains(sessions, s => s.SessionId == session1.SessionId);
        Assert.Contains(sessions, s => s.SessionId == session2.SessionId);
    }

    [Fact]
    public async Task DeleteSessionAsync_ExistingSession_ShouldReturnTrue()
    {
        // Arrange
        var session = await _sessionManager.CreateSessionAsync("Test Session");

        // Act
        var result = await _sessionManager.DeleteSessionAsync(session.SessionId);

        // Assert
        Assert.True(result);
        
        // Verify deletion
        var retrievedSession = await _sessionManager.GetSessionAsync(session.SessionId);
        Assert.Null(retrievedSession);
    }

    [Fact]
    public async Task DeleteSessionAsync_NonExistentSession_ShouldReturnFalse()
    {
        // Act
        var result = await _sessionManager.DeleteSessionAsync("non-existent-id");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ArchiveSessionAsync_ExistingSession_ShouldReturnTrue()
    {
        // Arrange
        var session = await _sessionManager.CreateSessionAsync("Test Session");

        // Act
        var result = await _sessionManager.ArchiveSessionAsync(session.SessionId);

        // Assert
        Assert.True(result);
        
        // Verify archival
        var retrievedSession = await _sessionManager.GetSessionAsync(session.SessionId);
        Assert.NotNull(retrievedSession);
        Assert.Equal(SessionStatus.Archived, retrievedSession.Status);
    }

    [Fact]
    public async Task GetSessionByNameAsync_ExistingSession_ShouldReturnSession()
    {
        // Arrange
        var session = await _sessionManager.CreateSessionAsync("Test Session Name");

        // Act
        var retrievedSession = await _sessionManager.GetSessionByNameAsync("Test Session Name");

        // Assert
        Assert.NotNull(retrievedSession);
        Assert.Equal(session.SessionId, retrievedSession.SessionId);
        Assert.Equal("Test Session Name", retrievedSession.Name);
        Assert.Equal(session.Status, retrievedSession.Status);
    }

    [Fact]
    public async Task GetSessionByNameAsync_NonExistentSession_ShouldReturnNull()
    {
        // Act
        var retrievedSession = await _sessionManager.GetSessionByNameAsync("Non-existent Session");

        // Assert
        Assert.Null(retrievedSession);
    }

    [Fact]
    public async Task GetSessionByNameAsync_MultipleSessionsWithSameName_ShouldReturnMostRecent()
    {
        // Arrange
        var session1 = await _sessionManager.CreateSessionAsync("Same Name");
        await Task.Delay(10); // Ensure different timestamps
        var session2 = await _sessionManager.CreateSessionAsync("Same Name");

        // Act
        var retrievedSession = await _sessionManager.GetSessionByNameAsync("Same Name");

        // Assert
        Assert.NotNull(retrievedSession);
        Assert.Equal(session2.SessionId, retrievedSession.SessionId);
        Assert.True(retrievedSession.LastUpdatedAt >= session1.LastUpdatedAt);
    }

    [Fact]
    public async Task ArchiveSessionAsync_NonExistentSession_ShouldReturnFalse()
    {
        // Act
        var result = await _sessionManager.ArchiveSessionAsync("non-existent-id");

        // Assert
        Assert.False(result);
    }
}

public class AgentSessionTests
{
    [Fact]
    public void CreateNew_ShouldCreateValidSession()
    {
        // Act
        var session = AgentSession.CreateNew("Test Session");

        // Assert
        Assert.NotNull(session);
        Assert.NotEmpty(session.SessionId);
        Assert.Equal("Test Session", session.Name);
        Assert.Equal(SessionStatus.Active, session.Status);
        Assert.True(session.CreatedAt <= DateTime.UtcNow);
        Assert.Equal(session.CreatedAt, session.LastUpdatedAt);
        Assert.Empty(session.ConversationState);
    }

    [Fact]
    public void CreateNew_WithEmptyName_ShouldGenerateDefaultName()
    {
        // Act
        var session = AgentSession.CreateNew();

        // Assert
        Assert.NotNull(session);
        Assert.StartsWith("Session ", session.Name);
    }

    [Fact]
    public void GetConversationMessages_EmptyState_ShouldReturnEmptyList()
    {
        // Arrange
        var session = AgentSession.CreateNew();

        // Act
        var messages = session.GetConversationMessages();

        // Assert
        Assert.NotNull(messages);
        Assert.Empty(messages);
    }

    [Fact]
    public void SetConversationMessages_ShouldSerializeAndDeserialize()
    {
        // Arrange
        var session = AgentSession.CreateNew();
        var testMessages = new List<object>
        {
            new { role = "system", content = "System prompt" },
            new { role = "user", content = "User message" },
            new { role = "assistant", content = "Assistant response" }
        };

        // Act
        session.SetConversationMessages(testMessages);
        var retrievedMessages = session.GetConversationMessages();

        // Assert
        Assert.NotEmpty(session.ConversationState);
        Assert.Equal(3, retrievedMessages.Count);
        Assert.True(session.LastUpdatedAt > session.CreatedAt);
    }

    [Fact]
    public void GetConversationMessages_InvalidJson_ShouldReturnEmptyList()
    {
        // Arrange
        var session = AgentSession.CreateNew();
        session.ConversationState = "invalid json";

        // Act
        var messages = session.GetConversationMessages();

        // Assert
        Assert.NotNull(messages);
        Assert.Empty(messages);
    }
}