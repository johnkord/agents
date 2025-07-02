using Microsoft.Extensions.Logging;
using System.Text.Json;
using OpenAIIntegration;
using OpenAIIntegration.Model;
using AgentAlpha.Configuration;
using AgentAlpha.Interfaces;
using AgentAlpha.Models;
using Common.Models.Session;
using Common.Interfaces.Session;
using Common.Interfaces.Tools;
namespace AgentAlpha.Services;

/// <summary>
/// Simplified implementation of OpenAI conversation and message flow management
/// </summary>
public class ConversationManager : IConversationManager
{
    private readonly IOpenAIResponsesService _openAi;
    private readonly ILogger<ConversationManager> _logger;
    private readonly AgentConfiguration _config;
    private readonly ISessionActivityLogger? _activityLogger;
    private readonly List<object> _messages;
    private readonly List<string> _recentAssistantResponses;
    private string _taskMarkdown = "";             // <-- new
    private const int MaxMarkdownTokens = 400; // ~1600 chars
    private const string RevisionToolName = "revise_plan"; // NEW

    public ConversationManager(
        IOpenAIResponsesService openAi,
        ILogger<ConversationManager> logger,
        AgentConfiguration config,
        ISessionActivityLogger? activityLogger = null)
    {
        _openAi      = openAi;
        _logger      = logger;
        _config      = config;
        _activityLogger = activityLogger;
        _messages    = new List<object>();
        _recentAssistantResponses = new List<string>();
    }

    public void InitializeConversation(string systemPrompt, string userTask)
    {
        _messages.Clear();
        _messages.Add(new { role = "system", content = systemPrompt });
        
        // Structure the initial user message to prompt ReAct pattern
        var structuredTask = $"""
            Task: {userTask}
            
            Please solve this task using the ReAct (Reasoning and Acting) methodology:
            
            Start by **REASONING** about this task:
            - What do you need to accomplish?
            - What information or capabilities might you need?
            - What would be a logical first step?
            
            Then proceed with your planned **ACTION**.
            """;
        
        _messages.Add(new { role = "user", content = structuredTask });
        
        _logger.LogInformation("Initialized ReAct conversation with task: {Task}", userTask);

        // Bootstrap markdown document with ReAct structure
        _taskMarkdown = $"""
            # Task
            {userTask}

            ## ReAct Methodology Progress
            
            ### Reasoning Phase
            *(LLM reasoning about the task will be captured here)*
            
            ### Actions Taken
            *(Tool calls and their results will be documented here)*
            
            ### Observations
            *(Analysis of results and insights gained)*
            
            ### Next Steps
            *(Planned future actions based on current state)*
            
            ### Final Results
            *(Completed when task is finished)*
            """;
    }

    public void InitializeFromSession(AgentSession session, string newUserTask)
    {
        _messages.Clear();
        
        // Load existing conversation messages from session
        var existingMessages = session.GetConversationMessages();
        _messages.AddRange(existingMessages);
        
        // Add the new user task
        if (!string.IsNullOrEmpty(newUserTask))
        {
            _messages.Add(new { role = "user", content = newUserTask });
        }
        
        _logger.LogInformation("Initialized conversation from session {SessionId} with new task: {Task}", 
            session.SessionId, newUserTask);
    }

    public IEnumerable<object> GetCurrentMessages()
    {
        return _messages.ToList(); // Return a copy to avoid external modification
    }

    private string GetCondensedMarkdown()
    {
        if (string.IsNullOrWhiteSpace(_taskMarkdown)) return "";
        // crude “token” trim (≈4 chars per token)
        var maxLen = MaxMarkdownTokens * 4;
        return _taskMarkdown.Length <= maxLen
            ? _taskMarkdown
            : _taskMarkdown[..maxLen] + "\n\n<!-- trimmed -->";
    }

    public async Task<ConversationResponse> ProcessIterationAsync(
        OpenAIIntegration.Model.ToolDefinition[] availableTools)
    {
        // --- inject markdown context ---------------------------
        var mdSnapshot = GetCondensedMarkdown();
        var contextMessages = new List<object>(_messages);
        if (!string.IsNullOrWhiteSpace(mdSnapshot))
        {
            contextMessages.Add(new
            {
                role    = "system",
                content = $"Current task markdown (trimmed):\n\n{mdSnapshot}"
            });
        }
        // --------------------------------------------------------
        
        var toolList = (availableTools ?? Array.Empty<ToolDefinition>()).ToList();

        var request = new ResponsesCreateRequest
        {
            Model      = _config.Model,
            Input      = contextMessages.ToArray(),
            Tools      = toolList.ToArray(),
            ToolChoice = "auto"
        };

        string? requestActivityId = null;
        if (_activityLogger != null)
        {
            if (_config.ActivityLogging.VerboseOpenAI)
            {
                // Create detailed request data for comprehensive logging
                var requestData = new 
                { 
                    Model = request.Model,
                    MessageCount = _messages.Count,
                    ToolCount = toolList.Count,
                    ToolNames = toolList.Select(t => 
                        string.IsNullOrEmpty(t.Name) ? $"[{t.Type}]" : t.Name).ToArray(),
                    ToolChoice = request.ToolChoice,
                    // Include full request details for comprehensive audit trail
                    FullRequest = new
                    {
                        Model = request.Model,
                        Messages = _messages.Take(_config.ActivityLogging.MaxMessagesInLog).ToArray(),
                        Tools = toolList.Select(t => new { 
                            Name = string.IsNullOrEmpty(t.Name) ? $"[{t.Type}]" : t.Name, 
                            t.Description, 
                            ParameterCount = t.Parameters?.ToString()?.Length ?? 0 
                        }).ToArray(),
                        ToolChoice = request.ToolChoice
                    }
                };
                
                requestActivityId = _activityLogger.StartActivity(
                    ActivityTypes.OpenAIRequest,
                    "Sending request to OpenAI API",
                    requestData);
            }
            else
            {
                // Basic logging without full request details
                requestActivityId = _activityLogger.StartActivity(
                    ActivityTypes.OpenAIRequest,
                    "Sending request to OpenAI API",
                    new 
                    { 
                        Model = request.Model,
                        MessageCount = _messages.Count,
                        ToolCount = toolList.Count,
                        ToolNames = toolList.Select(t => 
                            string.IsNullOrEmpty(t.Name) ? $"[{t.Type}]" : t.Name).ToArray()
                    });
            }
        }

        try
        {
            var response = await _openAi.CreateResponseAsync(request);
            
            if (_activityLogger != null && requestActivityId != null)
            {
                await _activityLogger.CompleteActivityAsync(requestActivityId, new 
                { 
                    ResponseReceived = true,
                    OutputItemCount = response.Output?.Count() ?? 0
                });
                
                if (_config.ActivityLogging.VerboseOpenAI)
                {
                    // Log the response as a separate activity with comprehensive data
                    var responseData = new
                    {
                        Model = request.Model,
                        OutputItemCount = response.Output?.Count() ?? 0,
                        HasToolCalls = response.Output?.Any(o => o is FunctionToolCall || o is McpToolCall) ?? false,
                        // Include detailed response data for comprehensive audit trail
                        FullResponse = new
                        {
                            Output = response.Output?.Select(output => new
                            {
                                Type = output.GetType().Name,
                                Content = output switch
                                {
                                    OutputMessage msg => (object)new { msg.Role, Content = SessionActivity.TruncateString(msg.Content?.ToString(), _config.ActivityLogging.MaxStringSize) },
                                    FunctionToolCall ftc => (object)new { ftc.Name, Arguments = SessionActivity.TruncateString(JsonSerializer.Serialize(ftc.Arguments), _config.ActivityLogging.MaxStringSize) },
                                    McpToolCall mtc => (object)new { mtc.Name, Arguments = SessionActivity.TruncateString(JsonSerializer.Serialize(mtc.Arguments), _config.ActivityLogging.MaxStringSize) },
                                    _ => (object)new { Raw = SessionActivity.TruncateString(output.ToString(), _config.ActivityLogging.MaxStringSize) }
                                }
                            }).ToArray(),
                            Usage = response.Usage != null ? new
                            {
                                response.Usage.InputTokens,
                                response.Usage.OutputTokens,
                                response.Usage.TotalTokens
                            } : null
                        }
                    };
                    
                    await _activityLogger.LogActivityAsync(
                        ActivityTypes.OpenAIResponse,
                        "Received response from OpenAI API",
                        responseData);
                }
                else
                {
                    // Basic response logging
                    await _activityLogger.LogActivityAsync(
                        ActivityTypes.OpenAIResponse,
                        "Received response from OpenAI API",
                        new
                        {
                            Model = request.Model,
                            OutputItemCount = response.Output?.Count() ?? 0,
                            HasToolCalls = response.Output?.Any(o => o is FunctionToolCall || o is McpToolCall) ?? false
                        });
                }
            }
            
            // Extract assistant text
            var assistantText = response.Output?
                .OfType<OutputMessage>()
                .FirstOrDefault(m => string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                ?.Content?.ToString() ?? "";

            // Extract tool calls
            var toolCalls = ExtractToolCalls(response);

            // ------- MARKDOWN UPDATE HOOK ‑----
            var summaryLines = new List<string>();
            if (!string.IsNullOrWhiteSpace(assistantText))
                summaryLines.Add($"Assistant said:\n{assistantText}");
            if (toolCalls.Count > 0)
                summaryLines.Add($"Planned tool calls:\n{string.Join(", ", toolCalls.Select(c => c.Name))}");
            var summary = string.Join("\n\n", summaryLines);

            var taskCompleted = IsTaskComplete(assistantText);
            await UpdateMarkdownAsync(summary, taskCompleted);
            // ----------------------------------

            return new ConversationResponse(assistantText, toolCalls, toolCalls.Count > 0);
        }
        catch (Exception ex)
        {
            if (_activityLogger != null && requestActivityId != null)
            {
                await _activityLogger.FailActivityAsync(requestActivityId, ex.Message);
            }
            
            _logger.LogError(ex, "Failed to process conversation iteration");
            throw;
        }
    }

    public async Task<ConversationResponse> ProcessIterationWithExpansionAsync(
        OpenAIIntegration.Model.ToolDefinition[] currentTools,
        Func<Task<OpenAIIntegration.Model.ToolDefinition[]>> getAdditionalTools)
    {
        // First, try with current tools
        var response = await ProcessIterationAsync(currentTools);
        
        // If the assistant's response suggests it needs more tools, try expanding
        if (!response.HasToolCalls && ContainsToolExpansionRequest(response.AssistantText))
        {
            _logger.LogInformation("Detected tool expansion request, getting additional tools");
            
            try
            {
                var additionalTools = await getAdditionalTools();
                if (additionalTools.Length > 0)
                {
                    var expandedTools = currentTools.Concat(additionalTools).ToArray();
                    _logger.LogInformation("Retrying with {Count} additional tools: {Tools}", 
                        additionalTools.Length, string.Join(", ", additionalTools.Select(t => t.Name)));
                    
                    // Add a message about tool expansion
                    _messages.Add(new { role = "system", content = $"Additional tools are now available: {string.Join(", ", additionalTools.Select(t => t.Name))}" });
                    
                    // Retry the request with expanded tools
                    response = await ProcessIterationAsync(expandedTools);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to expand tools, continuing with original response");
            }
        }
        
        return response;
    }

    public void AddAssistantMessage(string content)
    {
        _messages.Add(new { role = "assistant", content });
        _logger.LogDebug("Added assistant message to conversation");
        
        // Track assistant responses for repetition detection
        _recentAssistantResponses.Add(content);
        
        // Keep only the last 5 responses to detect repetition
        if (_recentAssistantResponses.Count > 5)
        {
            _recentAssistantResponses.RemoveAt(0);
        }
        
        // Optimize conversation length after adding messages
        OptimizeConversationLength();
    }

    public void AddToolResults(IEnumerable<string> toolSummaries)
    {
        var summary = string.Join("\n", toolSummaries);
        
        // Add tool results as observation data
        _messages.Add(new { role = "assistant", content = $"Tool results:\n{summary}" });
        
        // Add ReAct-structured continuation prompt
        if (toolSummaries.Any())
        {
            _messages.Add(new { role = "user", content = """
                Now that you have the results from your action, please continue following the ReAct pattern:
                
                **OBSERVE**: Analyze the results you just received. What do they tell you? Did you get what you expected?
                
                **REASON**: Based on these observations, what should be your next step? Are you closer to completing the task?
                
                **ACT**: If more work is needed, choose your next action. If the task is complete, clearly state completion.
                """ });
        }
        
        _logger.LogDebug("Added tool results to conversation: {Summary}", summary);
        
        // Optimize conversation length if configured
        OptimizeConversationLength();
    }

    public bool IsTaskComplete(string assistantResponse)
    {
        // Primary completion detection: Check for explicit completion marker from complete_task tool
        if (assistantResponse.Contains("TASK COMPLETED", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Fallback for backward compatibility: Check for ReAct pattern completion indicators
        var lowerResponse = assistantResponse.ToLowerInvariant();
        var reactCompletionPhrases = new[]
        {
            "the task is complete",
            "task completed",
            "task has been completed",
            "i have completed the task",
            "task is now complete",
            "this completes the task",
            "task successfully completed",
            "all required steps completed",
            "objective accomplished",
            "goal achieved"
        };

        foreach (var phrase in reactCompletionPhrases)
        {
            if (lowerResponse.Contains(phrase))
            {
                return true;
            }
        }

        // Check for natural language completion indicators
        if (IsNaturalLanguageCompletion(assistantResponse))
        {
            _logger.LogDebug("Detected completion based on natural language completion phrase");
            return true;
        }

        // For very substantial responses that appear to be complete creative content,
        // consider the task done to prevent infinite repetition
        if (IsSubstantialCompleteResponse(assistantResponse))
        {
            _logger.LogDebug("Detected completion based on substantial response content");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if the response contains a completion tool call
    /// </summary>
    public bool IsTaskComplete(ConversationResponse response)
    {
        // Check if the response contains a call to the complete_task tool
        if (response.HasToolCalls)
        {
            var hasCompletionTool = response.ToolCalls.Any(tc => 
                tc.Name.Equals("complete_task", StringComparison.OrdinalIgnoreCase));
            
            if (hasCompletionTool)
            {
                _logger.LogInformation("Task completion detected via complete_task tool call");
                return true;
            }
        }

        // Fallback to text-based detection
        return IsTaskComplete(response.AssistantText);
    }

    public ConversationStatistics GetConversationStatistics()
    {
        var systemCount = 0;
        var userCount = 0;
        var assistantCount = 0;
        var estimatedTokens = 0;

        foreach (var message in _messages)
        {
            var roleProperty = message.GetType().GetProperty("role");
            var contentProperty = message.GetType().GetProperty("content");
            
            if (roleProperty != null && contentProperty != null)
            {
                var role = roleProperty.GetValue(message)?.ToString() ?? "";
                var content = contentProperty.GetValue(message)?.ToString() ?? "";
                
                // Count messages by role
                switch (role.ToLowerInvariant())
                {
                    case "system":
                        systemCount++;
                        break;
                    case "user":
                        userCount++;
                        break;
                    case "assistant":
                        assistantCount++;
                        break;
                }
                
                // Rough token estimation (4 characters per token on average)
                estimatedTokens += content.Length / 4;
            }
        }

        return new ConversationStatistics(
            TotalMessages: _messages.Count,
            SystemMessages: systemCount,
            UserMessages: userCount,
            AssistantMessages: assistantCount,
            EstimatedTokens: estimatedTokens
        );
    }

    private bool IsSubstantialCompleteResponse(string response)
    {
        // This is a conservative check for responses that are clearly complete creative works
        // Characteristics of a complete response:
        // - Very long (indicates substantial content)
        // - Has structural elements (titles, paragraphs, proper formatting)
        // - Appears to tell a complete story or creative work
        
        if (response.Length < 800) // Must be substantial
            return false;

        // Look for indicators of structured, complete content
        var hasTitle = response.Contains("**Title:", StringComparison.OrdinalIgnoreCase) || 
                      response.Contains("# ") ||
                      response.StartsWith("**") && response.Contains("**", StringComparison.OrdinalIgnoreCase);
        
        var hasMultipleParagraphs = response.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length >= 5;
        
        var hasEndingIndicators = response.Contains("Thus,") || response.Contains("In the end,") || 
                                 response.Contains("Finally,") || response.Contains("conclusion");

        // If it has a title and multiple paragraphs, it's likely a complete creative work
        return hasTitle && hasMultipleParagraphs;
    }

    /// <summary>
    /// Check if the response contains natural language completion indicators
    /// </summary>
    private bool IsNaturalLanguageCompletion(string response)
    {
        // Common phrases that indicate task completion in natural language
        var completionPhrases = new[]
        {
            "the task is complete",
            "task completed",
            "task has been completed",
            "i have completed the task",
            "task is now complete",
            "this completes the task"
        };

        foreach (var phrase in completionPhrases)
        {
            if (response.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Optimize conversation length by removing older messages if conversation gets too long
    /// </summary>
    private void OptimizeConversationLength()
    {
        if (_config.MaxConversationMessages <= 0 || _messages.Count <= _config.MaxConversationMessages)
        {
            return; // No optimization needed
        }

        var systemMessages = new List<object>();
        var conversationMessages = new List<object>();

        // Separate system messages from conversation messages
        foreach (var message in _messages)
        {
            if (IsSystemMessage(message))
            {
                systemMessages.Add(message);
            }
            else
            {
                conversationMessages.Add(message);
            }
        }

        // Keep system messages and recent conversation messages
        var keepCount = _config.MaxConversationMessages - systemMessages.Count;
        if (keepCount > 0 && conversationMessages.Count > keepCount)
        {
            // Keep the most recent messages
            conversationMessages = conversationMessages.Skip(conversationMessages.Count - keepCount).ToList();
            
            _logger.LogDebug("Optimized conversation: keeping {SystemCount} system messages and {ConversationCount} recent conversation messages", 
                systemMessages.Count, conversationMessages.Count);
        }

        // Rebuild messages list
        _messages.Clear();
        _messages.AddRange(systemMessages);
        _messages.AddRange(conversationMessages);
    }

    /// <summary>
    /// Check if a message is a system message
    /// </summary>
    private static bool IsSystemMessage(object message)
    {
        if (message == null) return false;
        
        // Use reflection to check the role property
        var roleProperty = message.GetType().GetProperty("role");
        if (roleProperty != null)
        {
            var role = roleProperty.GetValue(message)?.ToString();
            return "system".Equals(role, StringComparison.OrdinalIgnoreCase);
        }
        
        return false;
    }

    private List<Models.ToolCall> ExtractToolCalls(ResponsesCreateResponse response)
    {
        var toolCalls = new List<Models.ToolCall>();

        foreach (var item in response.Output ?? Array.Empty<ResponseOutputItem>())
        {
            switch (item)
            {
                case FunctionToolCall funcCall when !string.IsNullOrEmpty(funcCall.Name):
                    var args = ExtractArguments(funcCall.Arguments);
                    toolCalls.Add(new Models.ToolCall(funcCall.Name, args));
                    break;

                case McpToolCall mcpCall when !string.IsNullOrEmpty(mcpCall.Name):
                    var mcpArgs = ExtractMcpArguments(mcpCall.Arguments);
                    toolCalls.Add(new Models.ToolCall(mcpCall.Name, mcpArgs));
                    break;

                // Handle other tool call types as needed
                case FileSearchToolCall fileSearch:
                    // Log but don't execute as these are handled differently
                    _logger.LogDebug("File search tool call detected: {Queries}", string.Join(", ", fileSearch.Queries ?? Array.Empty<string>()));
                    break;

                case WebSearchToolCall webSearch:
                    _logger.LogDebug("Web search tool call detected: {Queries}", string.Join(", ", webSearch.Queries ?? Array.Empty<string>()));
                    break;

                case ComputerToolCall computerCall:
                    _logger.LogDebug("Computer tool call detected: {Status}", computerCall.Status);
                    break;

                case ReasoningItem reasoning:
                    _logger.LogDebug("Reasoning step detected: {Status}", reasoning.Status);
                    break;

                case ImageGenerationCall imageGen:
                    _logger.LogDebug("Image generation call detected: {Status}", imageGen.Status);
                    break;

                case CodeInterpreterToolCall codeInterpreter:
                    _logger.LogDebug("Code interpreter call detected: {Status}", codeInterpreter.Status);
                    break;

                case LocalShellToolCall shellCall:
                    _logger.LogDebug("Local shell call detected: {Status}", shellCall.Status);
                    break;

                case OutputMessage:
                    // Already handled above
                    break;

                default:
                    _logger.LogDebug("Unhandled response item type: {Type}", item.GetType().Name);
                    break;
            }
        }

        return toolCalls;
    }

    private Dictionary<string, object?> ExtractArguments(JsonElement? arguments)
    {
        if (arguments == null) return new Dictionary<string, object?>();

        return arguments.Value.ValueKind switch
        {
            JsonValueKind.Object => DeserializeArguments(arguments.Value),
            JsonValueKind.String => ParseStringArguments(arguments.Value.GetString()),
            _ => new Dictionary<string, object?>()
        };
    }

    private Dictionary<string, object?> ExtractMcpArguments(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments)) return new Dictionary<string, object?>();

        try
        {
            return DeserializeArguments(JsonDocument.Parse(arguments).RootElement);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse MCP arguments: {Arguments}", arguments);
            return new Dictionary<string, object?>();
        }
    }

    private static Dictionary<string, object?> DeserializeArguments(JsonElement jsonObj)
        => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonObj.GetRawText())!
           .ToDictionary(kvp => kvp.Key, kvp => ConvertJsonElement(kvp.Value));

    private static object? ConvertJsonElement(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String => e.GetString(),
        JsonValueKind.Number => e.TryGetInt64(out var l) ? l : e.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => e.GetRawText() // fall back to raw JSON for objects/arrays
    };

    private static Dictionary<string, object?> ParseStringArguments(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new Dictionary<string, object?>();

        raw = raw.Trim();

        // If it looks like JSON, try to parse it
        if (raw.StartsWith("{"))
        {
            try
            {
                return DeserializeArguments(JsonDocument.Parse(raw).RootElement);
            }
            catch
            {
                // fall through – treat as plain string below
            }
        }

        // Fallback: single "command" parameter
        return new Dictionary<string, object?> { ["command"] = raw };
    }

    private bool ContainsToolExpansionRequest(string assistantText)
    {
        if (string.IsNullOrEmpty(assistantText))
            return false;
        
        var lowerText = assistantText.ToLowerInvariant();
        
        // Look for phrases that indicate the assistant needs additional tools
        var expansionIndicators = new[]
        {
            "i don't have",
            "i need",
            "would need",
            "require",
            "missing",
            "unavailable",
            "not available",
            "no tool",
            "can't find",
            "cannot find",
            "additional tool",
            "more tool"
        };
        
        return expansionIndicators.Any(indicator => lowerText.Contains(indicator));
    }

    /// <summary>
    /// Check if the assistant is providing repetitive responses that indicate being stuck
    /// </summary>
    private bool IsProvidingRepetitiveResponses(string currentResponse)
    {
        if (_recentAssistantResponses.Count < 3)
            return false;

        // Check if the current response is very similar to recent responses
        var similarResponseCount = 0;
        var currentWords = currentResponse.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var recentResponse in _recentAssistantResponses.TakeLast(3))
        {
            var recentWords = recentResponse.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var commonWords = currentWords.Intersect(recentWords).Count();
            var totalWords = currentWords.Union(recentWords).Count();
            
            // If more than 80% of words are the same, consider it repetitive
            if (totalWords > 0 && (double)commonWords / totalWords > 0.8)
            {
                similarResponseCount++;
            }
        }

        // If 2 or more of the last 3 responses are very similar to current response
        var isRepetitive = similarResponseCount >= 2;
        
        if (isRepetitive)
        {
            _logger.LogWarning("Detected repetitive assistant responses - agent may be stuck");
        }
        
        return isRepetitive;
    }

    /// <summary>
    /// Check if a response would be repetitive before adding it to conversation
    /// </summary>
    public bool WouldBeRepetitive(string response)
    {
        return IsProvidingRepetitiveResponses(response);
    }

    public string GetTaskMarkdown() => _taskMarkdown;

    public async Task UpdateMarkdownAsync(string iterationSummary, bool taskCompleted = false)
    {
        // Build a short prompt so that the LLM updates the markdown doc
        var prompt = $"""
            You are maintaining a markdown document that tracks a complex task.
            Current markdown is enclosed in <doc></doc>. Update it so that it
            reflects the latest information in <summary></summary>.
            Preserve existing content, mark any finished sub-tasks with [x] and
            add new sub-tasks if needed. When `complete=true`, finalise the
            document by adding all evidence, your reasoning and detailed results.

            <doc>
            {_taskMarkdown}
            </doc>

            <summary complete="{taskCompleted}">
            {iterationSummary}
            </summary>

            Return ONLY the updated markdown.
            """;

        // ----------- NEW: define expected tool -----------------------------
        var revisionTool = new ToolDefinition
        {
            Type = "function",
            Name = RevisionToolName,
            Description = "Updates the task markdown and proposes useful tool calls.",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    markdown = new { type = "string", description = "Full revised markdown document." },
                    tools    = new
                    {
                        type  = "array",
                        description = "List of tool names that would be useful next.",
                        items = new { type = "string" }
                    }
                },
                required = new[] { "markdown", "tools" }
            }
        };
        // -------------------------------------------------------------------

        var mdRequest = new ResponsesCreateRequest
        {
            Model      = _config.Model,
            Input      = new[] { new { role = "user", content = prompt } },
            Tools      = new[] { revisionTool },    // NEW
            ToolChoice = "auto"                     // NEW
        };

        try
        {
            var mdResponse = await _openAi.CreateResponseAsync(mdRequest);

            // ----------- NEW: extract tool call result ---------------------
            var planCall = mdResponse.Output?
                .OfType<FunctionToolCall>()
                .FirstOrDefault(fc => fc.Name == RevisionToolName);

            if (planCall != null && planCall.Arguments.HasValue)
            {
                var args = planCall.Arguments.Value;
                if (args.TryGetProperty("markdown", out var mdElem) &&
                    mdElem.ValueKind == JsonValueKind.String)
                {
                    _taskMarkdown = mdElem.GetString() ?? _taskMarkdown;
                }

                if (args.TryGetProperty("tools", out var toolsElem) &&
                    toolsElem.ValueKind == JsonValueKind.Array)
                {
                    var recommended = toolsElem.EnumerateArray()
                                               .Where(e => e.ValueKind == JsonValueKind.String)
                                               .Select(e => e.GetString())
                                               .Where(s => !string.IsNullOrWhiteSpace(s))
                                               .Distinct()
                                               .ToArray();
                    _logger.LogInformation("LLM recommends tools: {Tools}",
                        string.Join(", ", recommended));
                    // (optional) persist for later selection
                }
            }
            // ----------------------------------------------------------------
            else
            {
                // fallback: old behaviour (assistant returned plain markdown)
                var newMd = mdResponse.Output?
                    .OfType<OutputMessage>()
                    .FirstOrDefault()?.Content?.ToString();

                if (!string.IsNullOrWhiteSpace(newMd))
                    _taskMarkdown = newMd!;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update markdown, keeping previous version");
        }
    }
}