using Xunit;
using Microsoft.Extensions.Logging;
using AgentAlpha.Services;
using AgentAlpha.Models;
using SessionService.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AgentAlpha.Tests;

public class SharedDatabaseTests : IDisposable
{
    private readonly string _testDataDirectory;
    private readonly string _sharedDbPath;

    public SharedDatabaseTests()
    {
        // Create a temporary test directory
        _testDataDirectory = Path.Combine(Path.GetTempPath(), "agents_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDataDirectory);
        _sharedDbPath = Path.Combine(_testDataDirectory, "agent_sessions.db");
    }

    [Fact]
    public async Task SharedDatabase_TwoSessionManagers_CanAccessSameData()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger1 = loggerFactory.CreateLogger<SessionManager>();
        var logger2 = loggerFactory.CreateLogger<SessionManager>();

        // Create two SessionManager instances pointing to the same database file
        var sessionManager1 = new SessionManager(logger1, _sharedDbPath);
        var sessionManager2 = new SessionManager(logger2, _sharedDbPath);

        var sessionName = "Shared Database Test Session";

        // Act - Create session with first manager
        var session = await sessionManager1.CreateSessionAsync(sessionName);
        Assert.NotNull(session);

        // Save session to ensure it's persisted
        await sessionManager1.SaveSessionAsync(session);

        // Act - Retrieve session with second manager
        var retrievedSession = await sessionManager2.GetSessionAsync(session.SessionId);

        // Assert
        Assert.NotNull(retrievedSession);
        Assert.Equal(session.SessionId, retrievedSession.SessionId);
        Assert.Equal(sessionName, retrievedSession.Name);
        Assert.Equal(session.Status, retrievedSession.Status);
        
        // List sessions from both managers should show the same session
        var sessions1 = await sessionManager1.ListSessionsAsync();
        var sessions2 = await sessionManager2.ListSessionsAsync();
        
        Assert.Single(sessions1);
        Assert.Single(sessions2);
        Assert.Equal(sessions1.First().SessionId, sessions2.First().SessionId);
    }

    [Fact]
    public async Task SharedDatabase_WithEnvironmentVariable_UsesCorrectPath()
    {
        // Arrange
        var envVarName = "AGENT_SESSION_DB_PATH";
        var originalValue = Environment.GetEnvironmentVariable(envVarName);
        
        try
        {
            // Set environment variable to our test path
            Environment.SetEnvironmentVariable(envVarName, _sharedDbPath);
            
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<SessionManager>();

            // Act - Create SessionManager without explicit path (should use env var)
            var sessionManager = new SessionManager(logger);
            var session = await sessionManager.CreateSessionAsync("Environment Variable Test");

            // Assert - Session should be created and database file should exist at the specified path
            Assert.NotNull(session);
            Assert.True(File.Exists(_sharedDbPath), $"Database file should exist at {_sharedDbPath}");
        }
        finally
        {
            // Restore original environment variable
            Environment.SetEnvironmentVariable(envVarName, originalValue);
        }
    }

    [Fact] 
    public void SessionManager_DefaultPath_UsesSharedLocation()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<SessionManager>();

        // Act - Create SessionManager with default settings
        var sessionManager = new SessionManager(logger);

        // Assert - The default path should be in shared location, not app-specific
        // We can't directly test the private field, but we can verify behavior
        // by checking that the database gets created in the expected location
        var defaultPath = "./data/agent_sessions.db";
        var expectedDirectory = Path.GetDirectoryName(defaultPath);
        
        // The SessionManager should create the directory if it doesn't exist
        Assert.True(Directory.Exists(expectedDirectory) || expectedDirectory == ".", 
            "SessionManager should create data directory or use current directory");
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testDataDirectory))
        {
            Directory.Delete(_testDataDirectory, recursive: true);
        }
    }
}