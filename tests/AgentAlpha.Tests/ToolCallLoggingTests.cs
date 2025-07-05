using Microsoft.Extensions.Logging;
using Xunit;
using AgentAlpha.Services;
using AgentAlpha.Configuration;
using Common.Models.Session;
using Common.Interfaces.Session;
using SessionService.Services;
using OpenAIIntegration;
using Moq;
using AgentAlpha.Models;
using System.Text.Json;
using ModelContextProtocol.Client;

namespace AgentAlpha.Tests
{
    /*
    public class ToolCallLoggingTests
    {
        [Fact]
        public async Task SimpleToolManager_HasActivityLoggerConfigured_AndCanLogActivities()
        {
            // Arrange
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var sessionManager = new SessionManager(loggerFactory.CreateLogger<SessionManager>());
            var activityLogger = new SessionActivityLogger(sessionManager, loggerFactory.CreateLogger<SessionActivityLogger>());

            var session = await sessionManager.CreateSessionAsync("Test Tool Manager Logging Setup");
            activityLogger.SetCurrentSession(session);

            var config = new AgentConfiguration
            {
                Model = "test-model",
                ToolFilter = new ToolFilterConfig(),
                ActivityLogging = new ActivityLoggingConfig()
            };

            // Mock the OpenAI service
            var mockOpenAi = new Mock<ISessionAwareOpenAIService>();

            var toolManager = new SimpleToolManager(
                loggerFactory.CreateLogger<SimpleToolManager>(),
                config,
                mockOpenAi.Object);

            var testTool = new Mock<McpClientTool>();
            testTool.Setup(t => t.Name).Returns("test_tool");
            testTool.Setup(t => t.ExecuteAsync(It.IsAny<JsonElement>())).ReturnsAsync("{\"Success\":true}");
            toolManager.AddTool(testTool.Object);

            // Act - Set the activity logger (this is what should happen during setup)
            toolManager.SetActivityLogger(activityLogger);

            // Manually verify that we can log activities
            var toolCall = new ToolCall("test_tool", JsonDocument.Parse("{\"arg\":\"value\"}").RootElement);
            await toolManager.ExecuteToolAsync(toolCall);

            // Assert
            var activities = await sessionManager.GetSessionActivitiesAsync(session.SessionId);

            Assert.Equal(2, activities.Count);

            var toolCallActivity = activities.FirstOrDefault(a => a.ActivityType == ActivityTypes.ToolCall);
            Assert.NotNull(toolCallActivity);
            Assert.Equal("Executing tool: test_tool", toolCallActivity.Description);
            Assert.Contains("\"toolName\":\"test_tool\"", toolCallActivity.Data);

            var toolResultActivity = activities.FirstOrDefault(a => a.ActivityType == ActivityTypes.ToolResult);
            Assert.NotNull(toolResultActivity);
            Assert.Equal("Tool execution result for: test_tool", toolResultActivity.Description);
            Assert.Contains("\"toolName\":\"test_tool\"", toolResultActivity.Data);
            Assert.Contains("\"result\":\"{\\\"Success\\\":true}\"", toolResultActivity.Data);
        }

        [Fact]
        public void SimpleToolManager_SetActivityLogger_PropagatesLoggerToOpenAIService()
        {
            // Arrange
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var config = new AgentConfiguration
            {
                Model = "test-model",
                ToolFilter = new ToolFilterConfig(),
                ActivityLogging = new ActivityLoggingConfig()
            };

            var mockOpenAi = new Mock<ISessionAwareOpenAIService>();
            var mockActivityLogger = new Mock<ISessionActivityLogger>();

            var toolManager = new SimpleToolManager(
                loggerFactory.CreateLogger<SimpleToolManager>(),
                config,
                mockOpenAi.Object);

            // Act
            toolManager.SetActivityLogger(mockActivityLogger.Object);

            // Assert - Verify that the activity logger was also set on the OpenAI service
            mockOpenAi.Verify(m => m.SetActivityLogger(mockActivityLogger.Object), Times.Once);
        }
    }
    */
}