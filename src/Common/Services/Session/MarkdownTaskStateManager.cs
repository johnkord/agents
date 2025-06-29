using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Linq;
using Common.Models.Session;
using Common.Interfaces.Session;
using OpenAIIntegration;
using OpenAIIntegration.Model;

namespace Common.Services.Session;

/// <summary>
/// Implementation of markdown-based task state management with LLM-driven planning
/// </summary>
public class MarkdownTaskStateManager : IMarkdownTaskStateManager
{
    private readonly ISessionManager _sessionManager;
    private readonly ISessionAwareOpenAIService _openAiService;
    private readonly ILogger<MarkdownTaskStateManager> _logger;
    
    public MarkdownTaskStateManager(
        ISessionManager sessionManager,
        ISessionAwareOpenAIService openAiService,
        ILogger<MarkdownTaskStateManager> logger)
    {
        _sessionManager = sessionManager;
        _openAiService = openAiService;
        _logger = logger;
    }
    
    public async Task<string> InitializeTaskMarkdownAsync(string sessionId, string taskDescription)
    {
        try
        {
            _logger.LogInformation("Initializing task markdown for session {SessionId}: {Task}", sessionId, taskDescription);
            
            var prompt = $"""
                Create a markdown document for task execution and planning. The task is:
                "{taskDescription}"
                
                Please create a well-structured markdown document that includes:
                1. A clear task title
                2. A brief strategy section
                3. A list of initial subtasks with checkboxes (- [ ] format)
                4. A context section for tracking progress
                
                The document should be professional and organized. Use this format:
                
                # Task: [task title]
                
                **Strategy:** [brief description of approach]
                
                **Status:** In Progress
                
                ## Subtasks
                
                - [ ] [subtask 1 description]
                - [ ] [subtask 2 description]
                - [ ] [additional subtasks as needed]
                
                ## Progress Notes
                
                *Task initialized on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC*
                
                ## Context
                
                [Context will be added as subtasks are completed]
                
                Provide only the markdown content, no additional explanation.
                """;
                
            var request = new ResponsesCreateRequest
            {
                Model = "gpt-4o",
                Input = prompt,
                Instructions = "You are a task planning assistant. Create clear, actionable markdown documents for task execution.",
                MaxOutputTokens = 1000
            };
            
            var response = await _openAiService.CreateResponseAsync(request);
            var markdown = ExtractContentFromResponse(response) ?? "";
            
            if (string.IsNullOrEmpty(markdown))
            {
                // Fallback to a basic template
                markdown = $"""
                    # Task: {taskDescription}
                    
                    **Strategy:** Execute the task step by step
                    
                    **Status:** In Progress
                    
                    ## Subtasks
                    
                    - [ ] Analyze the task requirements
                    - [ ] Plan the implementation approach
                    - [ ] Execute the main task
                    - [ ] Verify completion
                    
                    ## Progress Notes
                    
                    *Task initialized on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC*
                    
                    ## Context
                    
                    Initial task setup completed.
                    """;
            }
            
            // Save to session
            await SaveTaskMarkdownToSessionAsync(sessionId, markdown);
            
            return markdown;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize task markdown for session {SessionId}", sessionId);
            throw;
        }
    }
    
    public async Task<string> InitializeTaskMarkdownFromPlanAsync(string sessionId, TaskPlan taskPlan)
    {
        try
        {
            _logger.LogInformation("Initializing task markdown from plan for session {SessionId}: {Task}", sessionId, taskPlan.Task);
            
            // Create markdown representation of the task plan
            var markdown = $"""
                # Task: {taskPlan.Task}
                
                **Strategy:** {taskPlan.Strategy}
                
                **Status:** In Progress
                
                **Complexity:** {taskPlan.Complexity}
                
                **Confidence:** {taskPlan.Confidence:P0}
                
                **Required Tools:** {string.Join(", ", taskPlan.RequiredTools)}
                
                ## Subtasks
                
                {string.Join("\n", taskPlan.Steps.Select(step => 
                    $"- [ ] **Step {step.StepNumber}:** {step.Description}" +
                    (step.PotentialTools.Any() ? $"\n  - *Tools:* {string.Join(", ", step.PotentialTools)}" : "") +
                    (!string.IsNullOrEmpty(step.ExpectedOutput) ? $"\n  - *Expected Output:* {step.ExpectedOutput}" : "")
                ))}
                
                ## Progress Notes
                
                *Task plan initialized on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC*
                
                ## Context
                
                Created from execution plan with {taskPlan.Steps.Count} steps.
                """;
            
            // Save to session
            await SaveTaskMarkdownToSessionAsync(sessionId, markdown);
            
            return markdown;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize task markdown from plan for session {SessionId}", sessionId);
            throw;
        }
    }
    
    public async Task<string> UpdateTaskMarkdownAsync(string sessionId, string actionDescription, string actionResult, string? observations = null)
    {
        try
        {
            var currentMarkdown = await GetTaskMarkdownAsync(sessionId);
            if (string.IsNullOrEmpty(currentMarkdown))
            {
                throw new InvalidOperationException($"No task markdown found for session {sessionId}");
            }
            
            _logger.LogInformation("Updating task markdown for session {SessionId} with action: {Action}", sessionId, actionDescription);
            
            var prompt = $"""
                Update the following task markdown document based on a recent action and its result.
                
                Current markdown document:
                ```markdown
                {currentMarkdown}
                ```
                
                Recent action: {actionDescription}
                Action result: {actionResult}
                {(string.IsNullOrEmpty(observations) ? "" : $"Additional observations: {observations}")}
                
                Please update the markdown document to reflect:
                1. Progress made from this action
                2. Update relevant subtasks (mark as completed with checkboxes if appropriate)
                3. Add new subtasks if the action revealed additional work needed
                4. Update the context section with relevant information from the action
                5. Update progress notes with timestamp
                
                Keep the same structure and format. Return only the updated markdown content.
                """;
                
            var request = new ResponsesCreateRequest
            {
                Model = "gpt-4o",
                Input = prompt,
                Instructions = "You are a task planning assistant. Update markdown documents to reflect progress accurately.",
                MaxOutputTokens = 1500
            };
            
            var response = await _openAiService.CreateResponseAsync(request);
            var updatedMarkdown = ExtractContentFromResponse(response) ?? currentMarkdown;
            
            // Save updated markdown
            await SaveTaskMarkdownToSessionAsync(sessionId, updatedMarkdown);
            
            return updatedMarkdown;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update task markdown for session {SessionId}", sessionId);
            throw;
        }
    }
    
    public async Task<string> GetTaskMarkdownAsync(string sessionId)
    {
        try
        {
            var session = await _sessionManager.GetSessionAsync(sessionId);
            return session?.TaskStateMarkdown ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get task markdown for session {SessionId}", sessionId);
            return string.Empty;
        }
    }
    
    public async Task<SubtaskInfo?> GetCurrentSubtaskFromMarkdownAsync(string sessionId)
    {
        try
        {
            var markdown = await GetTaskMarkdownAsync(sessionId);
            if (string.IsNullOrEmpty(markdown))
            {
                return null;
            }
            
            // Parse markdown to find the first unchecked subtask
            var lines = markdown.Split('\n');
            int priority = 1;
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Look for unchecked checkbox items
                if (trimmedLine.StartsWith("- [ ]"))
                {
                    var description = trimmedLine.Substring(5).Trim();
                    if (!string.IsNullOrEmpty(description))
                    {
                        return new SubtaskInfo
                        {
                            Description = description,
                            IsCompleted = false,
                            Priority = priority
                        };
                    }
                }
                
                // Count all checkbox items for priority ordering
                if (trimmedLine.StartsWith("- ["))
                {
                    priority++;
                }
            }
            
            return null; // No unchecked subtasks found
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get current subtask from markdown for session {SessionId}", sessionId);
            return null;
        }
    }
    
    public async Task<string> CompleteSubtaskInMarkdownAsync(string sessionId, string subtaskDescription, string completionResult)
    {
        try
        {
            var currentMarkdown = await GetTaskMarkdownAsync(sessionId);
            if (string.IsNullOrEmpty(currentMarkdown))
            {
                throw new InvalidOperationException($"No task markdown found for session {sessionId}");
            }
            
            _logger.LogInformation("Completing subtask in markdown for session {SessionId}: {Subtask}", sessionId, subtaskDescription);
            
            var prompt = $"""
                Update the following task markdown document to mark a subtask as completed.
                
                Current markdown document:
                ```markdown
                {currentMarkdown}
                ```
                
                Subtask to mark as completed: {subtaskDescription}
                Completion result: {completionResult}
                
                Please:
                1. Find the subtask that matches the description and mark it as completed (change - [ ] to - [x])
                2. Add a brief completion note under the completed item if appropriate
                3. Update the context section with any relevant information from the completion
                4. Update progress notes with timestamp
                
                Return only the updated markdown content.
                """;
                
            var request = new ResponsesCreateRequest
            {
                Model = "gpt-4o",
                Input = prompt,
                Instructions = "You are a task planning assistant. Update markdown documents to accurately reflect completed subtasks.",
                MaxOutputTokens = 1500
            };
            
            var response = await _openAiService.CreateResponseAsync(request);
            var updatedMarkdown = ExtractContentFromResponse(response) ?? currentMarkdown;
            
            // Save updated markdown
            await SaveTaskMarkdownToSessionAsync(sessionId, updatedMarkdown);
            
            return updatedMarkdown;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete subtask in markdown for session {SessionId}", sessionId);
            throw;
        }
    }
    
    public async Task<string> AddSubtaskToMarkdownAsync(string sessionId, string reason, string? context = null)
    {
        try
        {
            var currentMarkdown = await GetTaskMarkdownAsync(sessionId);
            if (string.IsNullOrEmpty(currentMarkdown))
            {
                throw new InvalidOperationException($"No task markdown found for session {sessionId}");
            }
            
            _logger.LogInformation("Adding subtask to markdown for session {SessionId}: {Reason}", sessionId, reason);
            
            var prompt = $"""
                Update the following task markdown document to add new subtasks based on emerging needs.
                
                Current markdown document:
                ```markdown
                {currentMarkdown}
                ```
                
                Reason for adding subtasks: {reason}
                {(string.IsNullOrEmpty(context) ? "" : $"Additional context: {context}")}
                
                Please:
                1. Analyze the current state and the reason for adding subtasks
                2. Add appropriate new subtasks to the list with - [ ] format
                3. Ensure the new subtasks are logically ordered with existing ones
                4. Update the context section if needed
                5. Update progress notes with timestamp
                
                Return only the updated markdown content.
                """;
                
            var request = new ResponsesCreateRequest
            {
                Model = "gpt-4o",
                Input = prompt,
                Instructions = "You are a task planning assistant. Add relevant subtasks to markdown documents based on evolving requirements.",
                MaxOutputTokens = 1500
            };
            
            var response = await _openAiService.CreateResponseAsync(request);
            var updatedMarkdown = ExtractContentFromResponse(response) ?? currentMarkdown;
            
            // Save updated markdown
            await SaveTaskMarkdownToSessionAsync(sessionId, updatedMarkdown);
            
            return updatedMarkdown;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add subtask to markdown for session {SessionId}", sessionId);
            throw;
        }
    }
    
    public async Task<string> UpdatePlanIterativelyAsync(string sessionId, string executionFeedback, string? currentContext = null)
    {
        try
        {
            var currentMarkdown = await GetTaskMarkdownAsync(sessionId);
            if (string.IsNullOrEmpty(currentMarkdown))
            {
                throw new InvalidOperationException($"No task markdown found for session {sessionId}");
            }
            
            _logger.LogInformation("Updating plan iteratively for session {SessionId} based on execution feedback", sessionId);
            
            var prompt = $"""
                Update the following task plan markdown document based on execution feedback and current progress.
                This is an iterative planning update to refine the approach based on what has been learned during execution.
                
                Current markdown document:
                ```markdown
                {currentMarkdown}
                ```
                
                Execution feedback: {executionFeedback}
                {(string.IsNullOrEmpty(currentContext) ? "" : $"Current execution context: {currentContext}")}
                
                Please iteratively refine the plan by:
                1. Analyzing what has been completed and what remains
                2. Adjusting the strategy based on execution feedback
                3. Modifying, adding, or reordering subtasks as needed
                4. Updating progress notes with insights from execution
                5. Refining the context section with lessons learned
                6. Maintaining completed subtasks as [x] and uncompleted as [ ]
                
                Focus on making the plan more effective based on real execution experience.
                Return only the updated markdown content.
                """;
                
            var request = new ResponsesCreateRequest
            {
                Model = "gpt-4o",
                Input = prompt,
                Instructions = "You are an iterative planning assistant. Refine task plans based on execution feedback to improve effectiveness.",
                MaxOutputTokens = 2000
            };
            
            var response = await _openAiService.CreateResponseAsync(request);
            var updatedMarkdown = ExtractContentFromResponse(response) ?? currentMarkdown;
            
            // Save updated markdown
            await SaveTaskMarkdownToSessionAsync(sessionId, updatedMarkdown);
            
            _logger.LogInformation("Updated plan iteratively for session {SessionId}", sessionId);
            return updatedMarkdown;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update plan iteratively for session {SessionId}", sessionId);
            throw;
        }
    }
    
    private async Task SaveTaskMarkdownToSessionAsync(string sessionId, string markdown)
    {
        var session = await _sessionManager.GetSessionAsync(sessionId);
        if (session == null)
        {
            throw new InvalidOperationException($"Session {sessionId} not found");
        }
        
        session.TaskStateMarkdown = markdown;
        session.LastUpdatedAt = DateTime.UtcNow;
        
        await _sessionManager.SaveSessionAsync(session);
        
        _logger.LogDebug("Saved task markdown for session {SessionId}: {Length} characters", sessionId, markdown.Length);
    }
    
    private string? ExtractContentFromResponse(ResponsesCreateResponse response)
    {
        // Extract content from the new API response format
        var outputMessage = response.Output?.OfType<OutputMessage>().FirstOrDefault();
        if (outputMessage?.Content?.ValueKind == JsonValueKind.String)
        {
            return outputMessage.Content.Value.GetString();
        }
        
        return null;
    }
}