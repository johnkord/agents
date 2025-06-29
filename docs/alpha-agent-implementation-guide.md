# TaskExecutor Refactoring Implementation Guide

## Overview

This document provides concrete implementation examples and step-by-step guidance for refactoring the TaskExecutor. It serves as a practical companion to the main refactoring plan and interface design documents.

## Phase 1 Implementation: SessionCoordinator

### Step 1: Create the SessionCoordinator Interface and Models

First, create the necessary models in `src/Agent/AgentAlpha/Models/`:

```csharp
// SessionContext.cs
using Common.Models.Session;

namespace AgentAlpha.Models;

public class SessionContext
{
    public AgentSession? Session { get; set; }
    public bool IsResuming { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

// ActivityLoggingServices.cs
namespace AgentAlpha.Models;

public class ActivityLoggingServices
{
    public IToolSelector? ToolSelector { get; set; }
    public IPlanningService? PlanningService { get; set; }
}
```

### Step 2: Implement SessionCoordinator

Create `src/Agent/AgentAlpha/Services/SessionCoordinator.cs`:

```csharp
using Microsoft.Extensions.Logging;
using AgentAlpha.Interfaces;
using AgentAlpha.Models;
using Common.Interfaces.Session;
using Common.Models.Session;

namespace AgentAlpha.Services;

public class SessionCoordinator : ISessionCoordinator
{
    private readonly ISessionManager _sessionManager;
    private readonly ISessionActivityLogger _activityLogger;
    private readonly ILogger<SessionCoordinator> _logger;

    public SessionCoordinator(
        ISessionManager sessionManager,
        ISessionActivityLogger activityLogger,
        ILogger<SessionCoordinator> logger)
    {
        _sessionManager = sessionManager;
        _activityLogger = activityLogger;
        _logger = logger;
    }

    public async Task<SessionContext> SetupSessionAsync(TaskExecutionRequest request)
    {
        _logger.LogInformation("Setting up session for task: {Task}", request.Task);

        // Handle existing session ID
        if (!string.IsNullOrEmpty(request.SessionId))
        {
            return await ResumeExistingSessionAsync(request);
        }
        
        // Handle session name (create new session)
        if (!string.IsNullOrEmpty(request.SessionName))
        {
            return await CreateNamedSessionAsync(request);
        }
        
        // Create default temporary session
        return await CreateDefaultSessionAsync(request);
    }

    public async Task ConfigureActivityLoggingAsync(
        SessionContext context, 
        ActivityLoggingServices services)
    {
        if (context.Session == null)
        {
            _logger.LogWarning("Cannot configure activity logging - no session in context");
            return;
        }

        _activityLogger.SetCurrentSession(context.Session);

        // Configure activity logging for services that need it
        services.ToolSelector?.SetActivityLogger(_activityLogger);
        services.PlanningService?.SetActivityLogger(_activityLogger);

        await _activityLogger.LogActivityAsync(
            ActivityTypes.SessionStart,
            context.IsResuming ? $"Resumed session for task" : $"Started new session for task",
            new { 
                SessionId = context.SessionId,
                IsResuming = context.IsResuming,
                CreatedAt = context.CreatedAt
            });

        _logger.LogInformation("Activity logging configured for session {SessionId}", context.SessionId);
    }

    public async Task SaveSessionAsync(SessionContext context)
    {
        if (context.Session != null)
        {
            await _sessionManager.SaveSessionAsync(context.Session);
            _logger.LogDebug("Session {SessionId} saved successfully", context.SessionId);
        }
    }

    private async Task<SessionContext> ResumeExistingSessionAsync(TaskExecutionRequest request)
    {
        var session = await _sessionManager.GetSessionAsync(request.SessionId!);
        if (session != null)
        {
            _logger.LogInformation("Resuming existing session {SessionId}", request.SessionId);
            return new SessionContext
            {
                Session = session,
                SessionId = session.SessionId,
                IsResuming = true
            };
        }

        _logger.LogWarning("Session {SessionId} not found, creating new session", request.SessionId);
        return await CreateDefaultSessionAsync(request);
    }

    private async Task<SessionContext> CreateNamedSessionAsync(TaskExecutionRequest request)
    {
        var session = await _sessionManager.CreateSessionAsync(request.SessionName!);
        _logger.LogInformation("Created new named session {SessionId} with name '{Name}'", 
            session.SessionId, request.SessionName);
        
        request.SessionId = session.SessionId; // Update request with new session ID
        
        return new SessionContext
        {
            Session = session,
            SessionId = session.SessionId,
            IsResuming = false
        };
    }

    private async Task<SessionContext> CreateDefaultSessionAsync(TaskExecutionRequest request)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");
        var defaultSessionName = $"Session {timestamp}";
        
        var session = await _sessionManager.CreateSessionAsync(defaultSessionName);
        _logger.LogInformation("Created default session {SessionId} with name '{Name}'", 
            session.SessionId, defaultSessionName);
        
        request.SessionId = session.SessionId; // Update request with new session ID
        
        return new SessionContext
        {
            Session = session,
            SessionId = session.SessionId,
            IsResuming = false
        };
    }
}
```

### Step 3: Update TaskExecutor to Use SessionCoordinator

Modify the TaskExecutor's constructor and ExecuteAsync method:

```csharp
// In TaskExecutor constructor - replace complex session logic
private readonly ISessionCoordinator _sessionCoordinator;

public TaskExecutor(
    IConnectionManager connectionManager,
    IToolManager toolManager,
    // ... other existing dependencies
    ISessionCoordinator sessionCoordinator, // NEW
    ILogger<TaskExecutor> logger)
{
    // ... existing assignments
    _sessionCoordinator = sessionCoordinator;
}

// Replace the complex session setup logic in ExecuteAsync
public async Task ExecuteAsync(TaskExecutionRequest request)
{
    _logger.LogInformation("Starting task execution: {Task}", request.Task);

    var effectiveConfig = ApplyRequestOverrides(request);

    try
    {
        // Step 1: Connect to MCP Server (unchanged)
        await ConnectToMcpServerAsync();

        // Step 2: Setup session (simplified)
        var sessionContext = await _sessionCoordinator.SetupSessionAsync(request);
        
        // Step 3: Configure activity logging
        var loggingServices = new ActivityLoggingServices
        {
            ToolSelector = _toolSelector,
            PlanningService = _planningService
        };
        await _sessionCoordinator.ConfigureActivityLoggingAsync(sessionContext, loggingServices);

        // Step 4: Initialize conversation (unchanged)
        var isResumingSession = await InitializeConversationAsync(request);

        // ... rest of the method remains the same for now
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Task execution failed");
        throw;
    }
}
```

### Step 4: Update Dependency Injection

Add the SessionCoordinator to the DI container in `Program.cs`:

```csharp
// In Program.cs or wherever DI is configured
services.AddScoped<ISessionCoordinator, SessionCoordinator>();
```

### Step 5: Create Unit Tests

Create `tests/AgentAlpha.Tests/SessionCoordinatorTests.cs`:

```csharp
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using AgentAlpha.Services;
using AgentAlpha.Models;
using Common.Interfaces.Session;
using Common.Models.Session;

namespace AgentAlpha.Tests;

public class SessionCoordinatorTests
{
    private readonly Mock<ISessionManager> _mockSessionManager;
    private readonly Mock<ISessionActivityLogger> _mockActivityLogger;
    private readonly Mock<ILogger<SessionCoordinator>> _mockLogger;
    private readonly SessionCoordinator _sessionCoordinator;

    public SessionCoordinatorTests()
    {
        _mockSessionManager = new Mock<ISessionManager>();
        _mockActivityLogger = new Mock<ISessionActivityLogger>();
        _mockLogger = new Mock<ILogger<SessionCoordinator>>();
        
        _sessionCoordinator = new SessionCoordinator(
            _mockSessionManager.Object,
            _mockActivityLogger.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task SetupSessionAsync_WithExistingSessionId_ReturnsResumingContext()
    {
        // Arrange
        var existingSession = new AgentSession 
        { 
            SessionId = "existing-session-id", 
            Name = "Existing Session" 
        };
        var request = new TaskExecutionRequest 
        { 
            Task = "Test task", 
            SessionId = "existing-session-id" 
        };

        _mockSessionManager
            .Setup(x => x.GetSessionAsync("existing-session-id"))
            .ReturnsAsync(existingSession);

        // Act
        var result = await _sessionCoordinator.SetupSessionAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("existing-session-id", result.SessionId);
        Assert.True(result.IsResuming);
        Assert.Equal(existingSession, result.Session);
    }

    [Fact]
    public async Task SetupSessionAsync_WithSessionName_CreatesNewSession()
    {
        // Arrange
        var newSession = new AgentSession 
        { 
            SessionId = "new-session-id", 
            Name = "New Session" 
        };
        var request = new TaskExecutionRequest 
        { 
            Task = "Test task", 
            SessionName = "New Session" 
        };

        _mockSessionManager
            .Setup(x => x.CreateSessionAsync("New Session"))
            .ReturnsAsync(newSession);

        // Act
        var result = await _sessionCoordinator.SetupSessionAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("new-session-id", result.SessionId);
        Assert.False(result.IsResuming);
        Assert.Equal(newSession, result.Session);
        Assert.Equal("new-session-id", request.SessionId); // Verify request was updated
    }

    [Fact]
    public async Task SetupSessionAsync_WithoutSessionInfo_CreatesDefaultSession()
    {
        // Arrange
        var defaultSession = new AgentSession 
        { 
            SessionId = "default-session-id", 
            Name = "Session 2024-01-01 10:00" 
        };
        var request = new TaskExecutionRequest { Task = "Test task" };

        _mockSessionManager
            .Setup(x => x.CreateSessionAsync(It.IsAny<string>()))
            .ReturnsAsync(defaultSession);

        // Act
        var result = await _sessionCoordinator.SetupSessionAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("default-session-id", result.SessionId);
        Assert.False(result.IsResuming);
        Assert.Equal(defaultSession, result.Session);
    }

    [Fact]
    public async Task ConfigureActivityLoggingAsync_ConfiguresServicesCorrectly()
    {
        // Arrange
        var context = new SessionContext
        {
            Session = new AgentSession { SessionId = "test-session" },
            SessionId = "test-session"
        };
        var mockToolSelector = new Mock<IToolSelector>();
        var mockPlanningService = new Mock<IPlanningService>();
        var services = new ActivityLoggingServices
        {
            ToolSelector = mockToolSelector.Object,
            PlanningService = mockPlanningService.Object
        };

        // Act
        await _sessionCoordinator.ConfigureActivityLoggingAsync(context, services);

        // Assert
        _mockActivityLogger.Verify(x => x.SetCurrentSession(context.Session), Times.Once);
        mockToolSelector.Verify(x => x.SetActivityLogger(_mockActivityLogger.Object), Times.Once);
        mockPlanningService.Verify(x => x.SetActivityLogger(_mockActivityLogger.Object), Times.Once);
        _mockActivityLogger.Verify(x => x.LogActivityAsync(
            ActivityTypes.SessionStart,
            It.IsAny<string>(),
            It.IsAny<object>()), Times.Once);
    }
}
```

## Validation Steps

After implementing Phase 1:

1. **Build the project**: `dotnet build --configuration Release`
2. **Run all tests**: `dotnet test --configuration Release`
3. **Run integration tests**: Verify that existing TaskExecutor functionality still works
4. **Performance test**: Ensure no performance regressions
5. **Code review**: Review the extracted code for quality and maintainability

## Next Steps

Once Phase 1 is complete and validated:

1. **Phase 2**: Extract ToolOrchestrator using similar patterns
2. **Phase 3**: Extract ConversationExecutor
3. **Phase 4**: Extract ExecutionStrategyManager
4. **Phase 5**: Final cleanup and optimization

## Common Pitfalls to Avoid

1. **Breaking existing interfaces**: Maintain backward compatibility
2. **Introducing circular dependencies**: Keep dependency flow unidirectional
3. **Over-engineering**: Start simple and add complexity only when needed
4. **Insufficient testing**: Each component needs comprehensive unit tests
5. **Performance regressions**: Monitor performance throughout the refactoring

This implementation guide provides a concrete starting point for the TaskExecutor refactoring while maintaining system stability and functionality.