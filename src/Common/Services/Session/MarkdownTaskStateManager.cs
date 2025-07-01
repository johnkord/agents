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
    
    private string[] _lastRequiredTools = Array.Empty<string>();
    public string[] LastRequiredTools => _lastRequiredTools;
    
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

    /// <summary>
    /// Initialize task planning directly into markdown format
    /// </summary>
    public async Task<string> InitializeTaskPlanningAsync(
        string sessionId,
        string task,
        IList<IUnifiedTool> availableTools)
    {
        _logger.LogInformation("Initializing task planning in markdown format for session {SessionId}: {Task}", sessionId, task);

        try
        {
            // Create a basic current state if none provided
            var basicState = new CurrentState
            {
                CapturedAt = DateTime.UtcNow
            };

            return await InitializeTaskPlanningWithStateAsync(
                sessionId,
                task,
                availableTools,
                basicState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize task planning for session {SessionId}", sessionId);
            _lastRequiredTools = Array.Empty<string>();
            return CreateFallbackMarkdownPlan(task, availableTools);
        }
    }

    /// <summary>
    /// Initialize task planning with current state analysis directly into markdown format
    /// </summary>
    public async Task<string> InitializeTaskPlanningWithStateAsync(
        string sessionId,
        string task,
        IList<IUnifiedTool> availableTools,
        CurrentState currentState)
    {
        _logger.LogInformation("Initializing state-aware task planning in markdown format for session {SessionId}: {Task}", sessionId, task);

        try
        {
            // Create markdown-based planning prompt
            var markdownPlanPrompt = CreateMarkdownPlanningPrompt(
                task,
                availableTools,
                currentState);
            
            var request = new ResponsesCreateRequest
            {
                Model  = "gpt-4.1",
                Input  = new[]
                {
                    new { role = "system", content = "You are an expert task planning assistant." },
                    new { role = "user",   content = markdownPlanPrompt }
                },
                Tools       = new[] { SaveMarkdownPlanTool },
                ToolChoice  = "auto",
                MaxOutputTokens = 2000
            };

            // Log the planning activity
            if (_activityLogger != null)
            {
                await _activityLogger.LogActivityAsync(
                    ActivityTypes.TaskPlanning,
                    $"Creating markdown task plan for: {task}",
                    new { SessionId = sessionId, Task = task, AvailableToolsCount = availableTools.Count }
                );
            }

            var response = await _openAiService.CreateResponseAsync(request);

            // Look for the function call output
            var fnCall = response.Output?
                             .OfType<FunctionToolCall>()
                             .FirstOrDefault(fc => fc.Name == SaveMarkdownPlanTool.Name);

            var (markdownPlan, requiredTools) = ParseFunctionCall(fnCall);

            _toolScope.SetRequiredTools(sessionId, requiredTools);
            _lastRequiredTools = requiredTools;
            
            if (!string.IsNullOrEmpty(markdownPlan))
            {
                if (_activityLogger != null)
                {
                    await _activityLogger!.LogActivityAsync(
                        ActivityTypes.TaskMarkdownUpdate,
                        "Initial markdown task plan",
                        markdownPlan);

                    await _activityLogger!.LogActivityAsync(
                        ActivityTypes.TaskPlanning,
                        "Required tools extracted",
                        requiredTools);
                }

                // Save to session
                await SaveTaskMarkdownToSessionAsync(sessionId, markdownPlan);

                _logger.LogInformation("Successfully created markdown task plan for session {SessionId}", sessionId);
                return markdownPlan;
            }
            else
            {
                _toolScope.Clear(sessionId);
                _lastRequiredTools = Array.Empty<string>();
                _logger.LogWarning("Failed to get valid markdown plan response for session {SessionId}", sessionId);
                var fallback = CreateFallbackMarkdownPlan(task, availableTools);
                await SaveTaskMarkdownToSessionAsync(sessionId, fallback);
                return fallback;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize state-aware task planning for session {SessionId}", sessionId);
            var fallback = CreateFallbackMarkdownPlan(task, availableTools);

            // Still log the fallback plan
            if (_activityLogger != null)
            {
                await _activityLogger.LogActivityAsync(
                    ActivityTypes.TaskPlanning,
                    "Fallback markdown plan created",
                    new { SessionId = sessionId, Task = task, MarkdownPlan = fallback });

                // Log the raw fallback markdown plan
                await _activityLogger!.LogActivityAsync(
                    ActivityTypes.TaskMarkdownUpdate,
                    "Initial markdown task plan (fallback)",
                    fallback);
            }

            _toolScope.Clear(sessionId);
            _lastRequiredTools = Array.Empty<string>();

            await SaveTaskMarkdownToSessionAsync(sessionId, fallback);
            return fallback;
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
                _lastRequiredTools = requiredTools;
                
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
                _lastRequiredTools = requiredTools;
                
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
                _lastRequiredTools = requiredTools;
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

    /// <summary>
    /// Creates a markdown planning prompt for LLM task planning
    /// </summary>
    private string CreateMarkdownPlanningPrompt(
        string task,
        IList<IUnifiedTool> availableTools,
        CurrentState currentState)
    {
        var prompt = new List<string>
        {
            $"*Prompt generated on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC*",
            "",
            "Create a comprehensive markdown-based execution plan for the following task:",
            $"\n**TASK:** {task}\n"
        };
        
        // Add state analysis
        var stateAnalysis = AnalyzeCurrentState(currentState, availableTools);
        if (!string.IsNullOrEmpty(stateAnalysis))
        {
            prompt.Add("**CURRENT STATE ANALYSIS:**");
            prompt.Add(stateAnalysis);
            prompt.Add("");
        }
        
        // Add available tools
        prompt.Add("**AVAILABLE TOOLS:**");
        foreach (var tool in availableTools)
        {
            prompt.Add($"- {tool.Name}: {tool.Description}");
        }
        prompt.Add("");
        
        prompt.Add("**INSTRUCTIONS:**");
        prompt.Add("Create a markdown document with the following structure:");
        prompt.Add("1. Task Overview - Brief summary of what needs to be accomplished");
        prompt.Add("2. Strategy - High-level approach to complete the task");
        prompt.Add("3. Execution Steps - Numbered checklist with specific, actionable items");
        prompt.Add("4. Required Tools - Full list of tools needed for execution");
        prompt.Add("5. Success Criteria - How to determine if the task is complete");
        prompt.Add("");
        prompt.Add("Format the execution steps as a numbered checklist using markdown checkboxes:");
        prompt.Add("- [ ] Step description");
        prompt.Add("");
        prompt.Add("Each step should be:");
        prompt.Add("- Specific and actionable");
        prompt.Add("- Include the tool to use if applicable");
        prompt.Add("- Have clear success criteria");
        prompt.Add("- Be granular enough to track progress");
        prompt.Add("");
        prompt.Add("When the plan is complete, call the tool " +
                   $"**{SaveMarkdownPlanTool.Name}** with two arguments:");
        prompt.Add("1. `markdown_plan`  – the full markdown document");
        prompt.Add("2. `required_tools` – an array with the tool names that appear in the \"Required Tools\" section.");
        prompt.Add("Return **nothing else**.");
        
        return string.Join("\n", prompt);
    }

    /// <summary>
    /// Creates a fallback markdown plan when LLM planning fails
    /// </summary>
    private string CreateFallbackMarkdownPlan(string task, IList<IUnifiedTool> availableTools)
    {
        var plan = new List<string>();

        // HEADER – tests look for "# Task:" explicitly
        plan.Add($"# Task: {task}");
        plan.Add("");

        // Strategy identical to the LLM-driven format used elsewhere
        plan.Add("**Strategy:** Execute the task using available tools in a systematic approach.");
        plan.Add("");

        // Rename section so the tests find "## Subtasks"
        plan.Add("## Subtasks");
        plan.Add("- [ ] Analyze the task requirements");
        plan.Add("- [ ] Identify necessary tools and resources");
        plan.Add("- [ ] Execute the task using appropriate tools");
        plan.Add("- [ ] Verify the results");
        plan.Add("- [ ] Complete the task");
        plan.Add("");

        plan.Add("## Required Tools");
        foreach (var tool in availableTools.Take(5)) // Limit to first 5 tools for fallback
        {
            plan.Add($"- {tool.Name}");
        }
        plan.Add("");

        plan.Add("## Success Criteria");
        plan.Add("- Task completed successfully");
        plan.Add("- All requirements met");
        plan.Add("- Results verified");

        _logger.LogInformation("Created fallback markdown plan for task: {Task}", task);
        return string.Join("\n", plan);
    }

    /// <summary>
    /// Very lightweight fallback analysis – just summarises counts.
    /// </summary>
    private static string AnalyzeCurrentState(CurrentState state, IList<IUnifiedTool> tools)
    {
        // Very lightweight summary – keeps build fast and avoids the heavy analyser previously removed
        if (state == null) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine($"• Context provided: {(!string.IsNullOrWhiteSpace(state.SessionContext))}");
        sb.AppendLine($"• Previous results: {state.PreviousResults?.Count ?? 0}");
        sb.AppendLine($"• User prefs set : {state.UserPreferences != null}");
        sb.AppendLine($"• Available tools: {tools.Count}");
        return sb.ToString();
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