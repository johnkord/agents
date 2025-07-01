using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text;
using Common.Models.Session;
using Common.Interfaces.Session;
using Common.Interfaces.Tools;                 // NEW – tool scope
using OpenAIIntegration;
using OpenAIIntegration.Model;

namespace Common.Services.Session;

/// <summary>
/// Implementation of markdown-based task state management with LLM-driven planning
/// Each time the task state is updated, re-planning occurs based on what has been accomplished
/// </summary>
public class MarkdownTaskStateManager : IMarkdownTaskStateManager
{
    private readonly ISessionManager _sessionManager;
    private readonly ISessionAwareOpenAIService _openAiService;
    private readonly IToolScopeManager _toolScope;            // NEW
    private readonly ILogger<MarkdownTaskStateManager> _logger;
    private ISessionActivityLogger? _activityLogger;          // NEW - for activity logging
    
    public MarkdownTaskStateManager(
        ISessionManager sessionManager,
        ISessionAwareOpenAIService openAiService,
        IToolScopeManager toolScope,                          // NEW
        ILogger<MarkdownTaskStateManager> logger)
    {
        _sessionManager = sessionManager;
        _openAiService = openAiService;
        _toolScope     = toolScope;                           // NEW
        _logger        = logger;
    }

    /// <summary>
    /// Set the session activity logger for automatic OpenAI request logging
    /// </summary>
    public void SetActivityLogger(ISessionActivityLogger? activityLogger)
    {
        _activityLogger = activityLogger;
        _openAiService.SetActivityLogger(activityLogger);
        _logger.LogDebug("Activity logger {Status} for MarkdownTaskStateManager", 
            activityLogger != null ? "set" : "cleared");
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
                Model = "gpt-4.1",
                Input = prompt,
                Instructions = "You are a task planning assistant. Create clear, actionable markdown documents for task execution.",
                MaxOutputTokens = 2000
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
    

    
    // NEW – single tool definition identical to PlanningService
    private static readonly ToolDefinition SaveMarkdownPlanTool = new()
    {
        Type        = "function",
        Name        = "save_markdown_plan",
        Description = "Return the updated markdown plan and the list of required tools.",
        Parameters  = new
        {
            type       = "object",
            properties = new
            {
                markdown_plan  = new { type = "string", description = "The markdown task plan." },
                required_tools = new
                {
                    type  = "array",
                    items = new { type = "string" },
                    description = "Names of tools required for executing the next steps."
                }
            },
            required = new[] { "markdown_plan", "required_tools" }
        }
    };

    // NEW – helper copied from PlanningService (fixed variable scope)
    private static (string markdown, string[] tools) ParseFunctionCall(FunctionToolCall? fnCall)
    {
        if (fnCall == null)                         // no function call
            return ("", Array.Empty<string>());

        if (!fnCall.Arguments.HasValue)             // no arguments at all
            return ("", Array.Empty<string>());

        JsonElement argElement = fnCall.Arguments.Value;
        JsonElement root;

        if (argElement.ValueKind == JsonValueKind.Object)
        {
            root = argElement;
        }
        else if (argElement.ValueKind == JsonValueKind.String)
        {
            var jsonText = argElement.GetString();
            if (string.IsNullOrWhiteSpace(jsonText))
                return ("", Array.Empty<string>());

            using var doc = JsonDocument.Parse(jsonText);
            root = doc.RootElement.Clone();         // clone to use outside `using`
        }
        else
        {
            return ("", Array.Empty<string>());
        }

        var markdown = root.GetProperty("markdown_plan").GetString() ?? "";

        var toolsArr = root.TryGetProperty("required_tools", out var arr) && arr.ValueKind == JsonValueKind.Array
            ? arr.EnumerateArray()
                  .Where(e => e.ValueKind == JsonValueKind.String)
                  .Select(e => e.GetString()!)
                  .ToArray()
            : Array.Empty<string>();

        return (markdown.Trim(), toolsArr);
    }

    public async Task<string> UpdateTaskMarkdownAsync(string sessionId, string actionDescription, string actionResult, string? observations = null)
    {
        try
        {
            var currentMarkdown = await GetTaskMarkdownAsync(sessionId);
            if (string.IsNullOrEmpty(currentMarkdown))
            {
                // If no markdown exists, initialize it with a basic structure
                _logger.LogWarning("No task markdown found for session {SessionId}, initializing with action result", sessionId);
                
                var basicMarkdown = $"""
                    # Task: Session {sessionId}
                    
                    **Strategy:** Task execution in progress
                    
                    **Status:** In Progress
                    
                    ## Progress Notes
                    
                    *Session started without initial task plan*
                    
                    ### Action: {actionDescription}
                    Result: {actionResult}
                    {(string.IsNullOrEmpty(observations) ? "" : $"\nObservations: {observations}")}
                    
                    ## Context
                    
                    Session executing without predefined task structure.
                    """;
                
                await SaveTaskMarkdownToSessionAsync(sessionId, basicMarkdown);
                return basicMarkdown;
            }

            _logger.LogInformation("Updating task markdown for session {SessionId} with action: {Action}", sessionId, actionDescription);

            var timestampUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");   // NEW
            
            // KEY CHANGE: Re-plan based on what has been accomplished so far
            var prompt = $"""
                Update the markdown plan below based on the recent action/result, and re-plan based on what has been accomplished so far.
                (Timestamp UTC: {timestampUtc})

                Maintain structure, mark completed subtasks, add new ones as needed, update context, and adjust the plan based on progress.
                
                ```markdown
                {currentMarkdown}
                ```

                • Action: {actionDescription}
                • Result: {actionResult}
                {(string.IsNullOrEmpty(observations) ? "" : $"• Observations: {observations}")}

                Please re-plan by:
                1. Analyzing what has been accomplished based on the action result
                2. Marking completed subtasks as complete [x] if applicable
                3. Adjusting remaining subtasks based on new information
                4. Adding new subtasks if the action revealed additional requirements
                5. Updating the strategy if needed based on progress
                6. Identifying tools needed for the next steps

                When done, call the tool {SaveMarkdownPlanTool.Name} with:
                  - markdown_plan  : the updated markdown with re-planned tasks
                  - required_tools : array of tools needed for the upcoming subtasks
                Return nothing else.
                """;

            var request = new ResponsesCreateRequest
            {
                Model          = "gpt-4.1",
                Input          = new[] { new { role = "user", content = prompt } },
                Tools          = new[] { SaveMarkdownPlanTool },
                ToolChoice     = "auto",
                MaxOutputTokens = 2000
            };

            var response = await _openAiService.CreateResponseAsync(request);

            var fnCall = response.Output?
                               .OfType<FunctionToolCall>()
                               .FirstOrDefault(fc => fc.Name == SaveMarkdownPlanTool.Name);

            var (updatedMarkdown, requiredTools) = ParseFunctionCall(fnCall);

            if (!string.IsNullOrWhiteSpace(updatedMarkdown))
            {
                await SaveTaskMarkdownToSessionAsync(sessionId, updatedMarkdown);
                _toolScope.SetRequiredTools(sessionId, requiredTools);   // NEW
                
                _logger.LogInformation("Re-planned task based on progress for session {SessionId}", sessionId);
                return updatedMarkdown;
            }

            _logger.LogWarning("Function call did not return markdown – falling back to existing extractor");
            // ...existing ExtractContentFromResponse fallback...
            var extracted = ExtractContentFromResponse(response) ?? currentMarkdown;
            await SaveTaskMarkdownToSessionAsync(sessionId, extracted);
            return extracted;
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
                _logger.LogWarning("No task markdown found for session {SessionId}, cannot complete subtask", sessionId);
                
                // Initialize with the completed subtask
                var initialMarkdown = $"""
                    # Task: Session {sessionId}
                    
                    **Strategy:** Task execution in progress
                    
                    **Status:** In Progress
                    
                    ## Subtasks
                    
                    - [x] {subtaskDescription} - {completionResult}
                    
                    ## Progress Notes
                    
                    *Subtask completed on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC*
                    
                    ## Context
                    
                    {completionResult}
                    """;
                
                await SaveTaskMarkdownToSessionAsync(sessionId, initialMarkdown);
                return initialMarkdown;
            }
            
            _logger.LogInformation("Completing subtask in markdown for session {SessionId}: {Subtask}", sessionId, subtaskDescription);
            
            var timestampUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");   // NEW
            
            // KEY CHANGE: Re-plan based on subtask completion
            var prompt = $"""
                Update the following task markdown document to mark a subtask as completed and re-plan based on what was accomplished.
                (Timestamp UTC: {timestampUtc})
                
                Current markdown document:
                ```markdown
                {currentMarkdown}
                ```
                
                Subtask to mark as completed: {subtaskDescription}
                Completion result: {completionResult}
                
                Please:
                1. Find the subtask that matches the description and mark it as completed (change - [ ] to - [x])
                2. Add a brief completion note under the completed item if appropriate
                3. Re-plan the remaining tasks based on what was learned from completing this subtask
                4. Add new subtasks if the completion revealed additional requirements
                5. Adjust the strategy if needed based on the completion result
                6. Update the context section with relevant information from the completion
                7. Update progress notes with timestamp
                8. Identify tools needed for the next steps

                When done, call the tool {SaveMarkdownPlanTool.Name} with:
                  - markdown_plan  : the updated markdown with re-planned tasks
                  - required_tools : array of tools needed for the upcoming subtasks
                Return nothing else.
                """;
                
            var request = new ResponsesCreateRequest
            {
                Model          = "gpt-4.1",
                Input          = new[] { new { role = "user", content = prompt } },
                Tools          = new[] { SaveMarkdownPlanTool },
                ToolChoice     = "auto",
                MaxOutputTokens = 2000
            };
            
            var response = await _openAiService.CreateResponseAsync(request);

            var fnCall = response.Output?
                               .OfType<FunctionToolCall>()
                               .FirstOrDefault(fc => fc.Name == SaveMarkdownPlanTool.Name);

            var (updatedMarkdown, requiredTools) = ParseFunctionCall(fnCall);

            if (!string.IsNullOrWhiteSpace(updatedMarkdown))
            {
                await SaveTaskMarkdownToSessionAsync(sessionId, updatedMarkdown);
                _toolScope.SetRequiredTools(sessionId, requiredTools);   // NEW
                
                _logger.LogInformation("Re-planned task based on subtask completion for session {SessionId}", sessionId);
                return updatedMarkdown;
            }

            _logger.LogWarning("Function call did not return markdown – falling back to basic update");
            // Fallback using existing method without re-planning
            var fallbackRequest = new ResponsesCreateRequest
            {
                Model = "gpt-4.1",
                Input = $"""
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
                    """,
                Instructions = "You are a task planning assistant. Update markdown documents to accurately reflect completed subtasks.",
                MaxOutputTokens = 2000
            };
            
            var fallbackResponse = await _openAiService.CreateResponseAsync(fallbackRequest);
            var fallbackMarkdown = ExtractContentFromResponse(fallbackResponse) ?? currentMarkdown;
            
            // Save updated markdown
            await SaveTaskMarkdownToSessionAsync(sessionId, fallbackMarkdown);
            
            return fallbackMarkdown;
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
            
            var timestampUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");   // NEW
            
            var prompt = $"""
                Update the following task markdown document to add new subtasks based on emerging needs.
                (Timestamp UTC: {timestampUtc})
                
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
                Model = "gpt-4.1",
                Input = prompt,
                Instructions = "You are a task planning assistant. Add relevant subtasks to markdown documents based on evolving requirements.",
                MaxOutputTokens = 2000
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
            
            var timestampUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");   // NEW
            
            var prompt = $"""
                Update the following task plan markdown document based on execution feedback and current progress.
                (Timestamp UTC: {timestampUtc})
                
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
                
                When done, call the tool {SaveMarkdownPlanTool.Name} with:
                  - markdown_plan  : the updated markdown
                  - required_tools : array of tools needed for the upcoming subtasks
                Return nothing else.
                """;
                
            var request = new ResponsesCreateRequest
            {
                Model          = "gpt-4.1",
                Input          = new[] { new { role = "user", content = prompt } },
                Tools          = new[] { SaveMarkdownPlanTool },
                ToolChoice     = "auto",
                MaxOutputTokens = 2000
            };
            
            var response = await _openAiService.CreateResponseAsync(request);

            var fnCall = response.Output?
                               .OfType<FunctionToolCall>()
                               .FirstOrDefault(fc => fc.Name == SaveMarkdownPlanTool.Name);

            var (updatedMarkdown, requiredTools) = ParseFunctionCall(fnCall);

            if (!string.IsNullOrWhiteSpace(updatedMarkdown))
            {
                await SaveTaskMarkdownToSessionAsync(sessionId, updatedMarkdown);
                _toolScope.SetRequiredTools(sessionId, requiredTools);   // NEW
                return updatedMarkdown;
            }

            _logger.LogWarning("Function call did not return markdown – falling back to basic content");
            // Fallback using existing method
            var fallbackRequest = new ResponsesCreateRequest
            {
                Model = "gpt-4.1",
                Input = prompt,
                Instructions = "You are an iterative planning assistant. Refine task plans based on execution feedback to improve effectiveness.",
                MaxOutputTokens = 2000
            };
            
            var fallbackResponse = await _openAiService.CreateResponseAsync(fallbackRequest);
            var fallbackMarkdown = ExtractContentFromResponse(fallbackResponse) ?? currentMarkdown;
            
            await SaveTaskMarkdownToSessionAsync(sessionId, fallbackMarkdown);
            
            _logger.LogInformation("Updated plan iteratively for session {SessionId}", sessionId);
            return fallbackMarkdown;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update plan iteratively for session {SessionId}", sessionId);
            throw;
        }
    }
    
    private async Task SaveTaskMarkdownToSessionAsync(string sessionId, string markdown)
    {
        // Always fetch the latest session state first
        var session = await _sessionManager.GetSessionAsync(sessionId);
        if (session == null)
            throw new InvalidOperationException($"Session {sessionId} not found");

        // Check if markdown actually changed
        if (!string.Equals(session.TaskStateMarkdown, markdown, StringComparison.Ordinal))
        {
            // Update the session
            session.TaskStateMarkdown = markdown;
            session.LastUpdatedAt = DateTime.UtcNow;
            
            // Save the updated session first
            await _sessionManager.SaveSessionAsync(session);
            
            // Then add the activity separately
            var activity = new SessionActivity
            {
                ActivityId = Guid.NewGuid().ToString(),
                SessionId = sessionId,
                Timestamp = DateTime.UtcNow,
                ActivityType = ActivityTypes.TaskMarkdownUpdate,
                Description = "Task state markdown updated",
                Success = true,
                Data = SessionActivity.TruncateString(markdown, 50000)
            };
            
            await _sessionManager.AddSessionActivityAsync(sessionId, activity);
            
            _logger.LogDebug("Saved task markdown for session {SessionId}: {Length} characters", 
                             sessionId, markdown.Length);
        }
        else
        {
            _logger.LogDebug("Task markdown unchanged for session {SessionId}, skipping save", sessionId);
        }
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