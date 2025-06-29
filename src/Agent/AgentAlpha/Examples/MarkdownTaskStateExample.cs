using Microsoft.Extensions.Logging;
using Common.Interfaces.Session;
using Common.Services.Session;
using OpenAIIntegration;

namespace AgentAlpha.Examples;

/// <summary>
/// Example usage of the new markdown-based task state management
/// </summary>
public class MarkdownTaskStateExample
{
    private readonly IMarkdownTaskStateManager _markdownTaskManager;
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<MarkdownTaskStateExample> _logger;

    public MarkdownTaskStateExample(
        IMarkdownTaskStateManager markdownTaskManager,
        ISessionManager sessionManager,
        ILogger<MarkdownTaskStateExample> logger)
    {
        _markdownTaskManager = markdownTaskManager;
        _sessionManager = sessionManager;
        _logger = logger;
    }

    /// <summary>
    /// Demonstrates a complete task execution workflow using markdown-based state management
    /// </summary>
    public async Task RunCompleteWorkflowExample()
    {
        // Create a new session
        var session = await _sessionManager.CreateSessionAsync("Markdown Task Example");
        _logger.LogInformation("Created session: {SessionId}", session.SessionId);

        try
        {
            // 1. Initialize task with markdown
            var taskDescription = "Create a data analysis report for Q4 sales performance";
            var initialMarkdown = await _markdownTaskManager.InitializeTaskMarkdownAsync(
                session.SessionId, 
                taskDescription);
            
            _logger.LogInformation("Initialized task markdown:\n{Markdown}", initialMarkdown);

            // 2. Execute first subtask
            var currentSubtask = await _markdownTaskManager.GetCurrentSubtaskFromMarkdownAsync(session.SessionId);
            if (currentSubtask != null)
            {
                _logger.LogInformation("Current subtask: {Description}", currentSubtask.Description);
                
                // Simulate task execution
                await SimulateTaskExecution("Gathering sales data from CRM system");
                
                // Update markdown with results
                var updatedMarkdown = await _markdownTaskManager.UpdateTaskMarkdownAsync(
                    session.SessionId,
                    "Completed data gathering from CRM",
                    "Successfully extracted Q4 sales data: 1,250 transactions, $2.3M revenue",
                    "Discovered that October had unusually high sales due to promotional campaign");
                
                _logger.LogInformation("Updated markdown after data gathering:\n{Markdown}", updatedMarkdown);
            }

            // 3. Complete a specific subtask
            await _markdownTaskManager.CompleteSubtaskInMarkdownAsync(
                session.SessionId,
                "Gather sales data",
                "Q4 sales data collection completed successfully - found 1,250 transactions totaling $2.3M");

            // 4. Dynamic planning - add new subtask based on findings
            await _markdownTaskManager.AddSubtaskToMarkdownAsync(
                session.SessionId,
                "Need to analyze the October promotional campaign impact since it had unusually high sales",
                "Promotional campaign analysis required for complete Q4 understanding");

            // 5. Continue with next subtask
            var nextSubtask = await _markdownTaskManager.GetCurrentSubtaskFromMarkdownAsync(session.SessionId);
            if (nextSubtask != null)
            {
                _logger.LogInformation("Next subtask: {Description}", nextSubtask.Description);
                
                // Simulate another task execution
                await SimulateTaskExecution("Analyzing sales trends and patterns");
                
                // Update with analysis results
                await _markdownTaskManager.UpdateTaskMarkdownAsync(
                    session.SessionId,
                    "Completed sales trend analysis",
                    "Identified key trends: 35% increase in Q4 vs Q3, peak sales in October (+45%), strong performance in electronics category",
                    "October promotional campaign contributed 15% of total Q4 revenue");
            }

            // 6. Get final task state
            var finalMarkdown = await _markdownTaskManager.GetTaskMarkdownAsync(session.SessionId);
            _logger.LogInformation("Final task state:\n{Markdown}", finalMarkdown);

            _logger.LogInformation("Markdown task workflow completed successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during markdown task workflow");
            throw;
        }
    }

    /// <summary>
    /// Example of migrating from traditional subtask lists to markdown
    /// </summary>
    public async Task RunMigrationExample()
    {
        var session = await _sessionManager.CreateSessionAsync("Migration Example");
        
        // Simulate having existing traditional task state
        _logger.LogInformation("Starting with traditional subtask list approach...");
        
        // Convert to markdown-based approach
        var taskDescription = "Migrate legacy system to cloud infrastructure";
        await _markdownTaskManager.InitializeTaskMarkdownAsync(session.SessionId, taskDescription);
        
        _logger.LogInformation("Successfully migrated to markdown-based task management");
        
        // Show how both systems can work together during transition
        var markdownState = await _markdownTaskManager.GetTaskMarkdownAsync(session.SessionId);
        _logger.LogInformation("Current markdown state:\n{Markdown}", markdownState);
    }

    /// <summary>
    /// Example of error handling and fallback scenarios
    /// </summary>
    public async Task RunErrorHandlingExample()
    {
        var session = await _sessionManager.CreateSessionAsync("Error Handling Example");
        
        try
        {
            // Initialize task
            await _markdownTaskManager.InitializeTaskMarkdownAsync(
                session.SessionId, 
                "Test error handling scenarios");
            
            // Demonstrate graceful handling of OpenAI API failures
            // (The implementation includes fallback templates when API calls fail)
            
            _logger.LogInformation("Error handling mechanisms are built into the markdown manager");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Demonstrated error handling");
        }
    }

    private async Task SimulateTaskExecution(string taskDescription)
    {
        _logger.LogInformation("Executing: {Task}", taskDescription);
        
        // Simulate some work
        await Task.Delay(1000);
        
        _logger.LogInformation("Completed: {Task}", taskDescription);
    }
}