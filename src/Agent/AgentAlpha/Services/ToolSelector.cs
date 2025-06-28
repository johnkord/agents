using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Diagnostics;
using ModelContextProtocol.Client;
using OpenAIIntegration;
using OpenAIIntegration.Model;
using AgentAlpha.Interfaces;
using AgentAlpha.Models;
using AgentAlpha.Configuration;
using Common.Interfaces.Session;
using Common.Models.Session;

namespace AgentAlpha.Services;

/// <summary>
/// Implementation of intelligent tool selection to reduce OpenAI context costs
/// </summary>
public class ToolSelector : IToolSelector
{
    private readonly ISessionAwareOpenAIService _openAi;
    private readonly IToolManager _toolManager;
    private readonly ILogger<ToolSelector> _logger;
    private readonly ToolSelectionConfig _config;
    private readonly AgentConfiguration _agentConfig;
    private ISessionActivityLogger? _activityLogger;

    public ToolSelector(
        ISessionAwareOpenAIService openAi,
        IToolManager toolManager,
        ILogger<ToolSelector> logger,
        AgentConfiguration agentConfig,
        ToolSelectionConfig? config = null)
    {
        _openAi = openAi;
        _toolManager = toolManager;
        _logger = logger;
        _agentConfig = agentConfig;
        _config = config ?? ToolSelectionConfig.Default;
    }

    /// <summary>
    /// Set the session activity logger for automatic OpenAI request logging
    /// </summary>
    public void SetActivityLogger(ISessionActivityLogger? activityLogger)
    {
        _activityLogger = activityLogger;
        _openAi.SetActivityLogger(activityLogger);
        _logger.LogDebug("Activity logger {Status} for ToolSelector", 
            activityLogger != null ? "set" : "cleared");
    }

    public async Task<ToolDefinition[]> SelectToolsForTaskAsync(string task, IList<McpClientTool> availableTools, int? maxTools = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var maxToolCount = maxTools ?? _config.MaxToolsPerRequest;
        
        try
        {
            // Start with essential tools
            var essentialTools = await GetEssentialToolsAsync(availableTools);
            var selectedTools = new List<ToolDefinition>(essentialTools);
            
            // If we're at or over the limit with just essential tools, return them
            if (selectedTools.Count >= maxToolCount)
            {
                _logger.LogInformation("Using only essential tools ({Count}) for task due to tool limit", selectedTools.Count);
                return selectedTools.Take(maxToolCount).ToArray();
            }
            
            // Use LLM to select additional relevant tools (including built-in tools)
            if (_config.UseLLMSelection && availableTools.Count > 0)
            {
                var remainingSlots = maxToolCount - selectedTools.Count;
                var additionalTools = await SelectToolsUsingLLMAsync(task, availableTools, selectedTools, remainingSlots);
                selectedTools.AddRange(additionalTools);
            }
            else
            {
                // Fallback: use simple heuristics if LLM selection is disabled
                var additionalTools = SelectToolsUsingHeuristics(task, availableTools, selectedTools, maxToolCount - selectedTools.Count);
                selectedTools.AddRange(additionalTools);
                
                // Add web search tool if task requires it and model supports it (only when not using LLM selection)
                if (ShouldIncludeWebSearch(task) && selectedTools.Count < maxToolCount && 
                    !selectedTools.Any(t => t.Name == "web_search"))
                {
                    var webSearchTool = _agentConfig.WebSearch.ToToolDefinition();
                    selectedTools.Add(webSearchTool);
                    _logger.LogInformation("Added web search tool for task requiring current information");
                }
            }
            
            stopwatch.Stop();
            
            // Log tool selection reasoning for better activity tracking
            await LogToolSelectionReasoningAsync(task, availableTools, selectedTools, stopwatch.ElapsedMilliseconds);
            
            _logger.LogInformation("Selected {Count} tools for task in {Duration}ms", 
                selectedTools.Count, stopwatch.ElapsedMilliseconds);
            
            return selectedTools.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to select tools for task, falling back to all tools");
            // Fallback: return all tools converted to OpenAI format
            return availableTools.Select(t => _toolManager.CreateOpenAiToolDefinition(t)).ToArray();
        }
    }

    public async Task<ToolDefinition[]> SelectAdditionalToolsAsync(
        IEnumerable<object> conversationContext, 
        IList<McpClientTool> availableTools, 
        ToolDefinition[] currentlySelectedTools,
        int maxAdditionalTools = 3)
    {
        if (!_config.AllowDynamicExpansion || maxAdditionalTools <= 0)
        {
            return Array.Empty<ToolDefinition>();
        }

        try
        {
            // Extract recent conversation context for analysis
            var recentContext = ExtractRecentContext(conversationContext);
            
            if (string.IsNullOrEmpty(recentContext))
            {
                return Array.Empty<ToolDefinition>();
            }
            
            // Find tools not currently selected
            var currentToolNames = currentlySelectedTools.Select(t => t.Name).ToHashSet();
            var remainingTools = availableTools.Where(t => !currentToolNames.Contains(t.Name)).ToList();
            
            if (remainingTools.Count == 0)
            {
                return Array.Empty<ToolDefinition>();
            }
            
            // Use LLM to suggest additional tools based on conversation context
            var additionalTools = await SelectAdditionalToolsUsingLLMAsync(recentContext, remainingTools, maxAdditionalTools);
            
            _logger.LogInformation("Selected {Count} additional tools for conversation iteration", additionalTools.Length);
            return additionalTools;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to select additional tools");
            return Array.Empty<ToolDefinition>();
        }
    }

    public Task<ToolDefinition[]> GetEssentialToolsAsync(IList<McpClientTool> availableTools)
    {
        var essentialTools = new List<ToolDefinition>();
        
        // Find essential tools by name
        foreach (var toolName in _config.EssentialTools)
        {
            var tool = availableTools.FirstOrDefault(t => 
                string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase));
            
            if (tool != null)
            {
                essentialTools.Add(_toolManager.CreateOpenAiToolDefinition(tool));
            }
        }
        
        _logger.LogDebug("Found {Count} essential tools: {Tools}", 
            essentialTools.Count, string.Join(", ", essentialTools.Select(t => t.Name)));
        
        return Task.FromResult(essentialTools.ToArray());
    }

    private async Task<ToolDefinition[]> SelectToolsUsingLLMAsync(
        string task, 
        IList<McpClientTool> availableTools, 
        List<ToolDefinition> alreadySelectedTools,
        int maxAdditionalTools)
    {
        // Create a list of all available tools including both MCP tools and built-in OpenAI tools
        var allAvailableToolDescriptions = new List<string>();
        
        // Add MCP tools
        var mcpToolDescriptions = availableTools
            .Where(t => !alreadySelectedTools.Any(s => s.Name == t.Name))
            .Select(t => $"- {t.Name}: {t.Description ?? "No description available"}")
            .ToList();
        allAvailableToolDescriptions.AddRange(mcpToolDescriptions);
        
        // Add built-in OpenAI tools that could be relevant
        var builtInTools = GetBuiltInOpenAIToolDescriptions(task, alreadySelectedTools);
        allAvailableToolDescriptions.AddRange(builtInTools);
        
        if (allAvailableToolDescriptions.Count == 0)
        {
            return Array.Empty<ToolDefinition>();
        }
        
        var prompt = $"""
            You are a tool selection assistant. Given a task and a list of available tools, select the most relevant tools that would be needed to complete the task.

            Task: {task}

            Available tools:
            {string.Join("\n", allAvailableToolDescriptions)}

            Instructions:
            1. Analyze the task to understand what operations might be needed
            2. Select up to {maxAdditionalTools} tools that are most relevant
            3. Prefer tools that are directly related to the task requirements
            4. Consider both immediate needs and potential follow-up operations
            5. Return your response as a JSON array of tool names only

            Example response: ["tool1", "tool2", "tool3"]

            Selected tools:
            """;

        try
        {
            var request = new ResponsesCreateRequest
            {
                Model = _config.SelectionModel,
                Input = new[]
                {
                    new { role = "user", content = prompt }
                },
                ToolChoice = "none" // We don't want to use tools for tool selection
            };

            var response = await _openAi.CreateResponseAsync(request);
            var outputMessage = response.Output?
                .OfType<OutputMessage>()
                .FirstOrDefault(m => string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase));
                
            var content = ExtractTextFromContent(outputMessage?.Content);

            // Parse the JSON response with better error handling
            string[] selectedToolNames;
            try
            {
                selectedToolNames = JsonSerializer.Deserialize<string[]>(content.Trim()) ?? Array.Empty<string>();
            }
            catch (JsonException jsonEx)
            {
                // Log the full request and response for debugging
                await LogToolSelectionErrorAsync(request, response, content, jsonEx, "JSON parsing failed");
                throw new InvalidOperationException($"Failed to parse LLM response as JSON array. Content: '{content.Trim()}'", jsonEx);
            }
            
            // Convert selected tool names back to ToolDefinitions
            var selectedTools = new List<ToolDefinition>();
            foreach (var toolName in selectedToolNames.Take(maxAdditionalTools))
            {
                // First try to find among MCP tools
                var mcpTool = availableTools.FirstOrDefault(t => 
                    string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase));
                
                if (mcpTool != null)
                {
                    selectedTools.Add(_toolManager.CreateOpenAiToolDefinition(mcpTool));
                }
                else
                {
                    // Then try to find among built-in tools
                    var builtInTool = GetBuiltInOpenAIToolDefinition(toolName);
                    if (builtInTool != null)
                    {
                        selectedTools.Add(builtInTool);
                    }
                    else
                    {
                        _logger.LogWarning("LLM selected tool '{ToolName}' but it was not found in available tools", toolName);
                    }
                }
            }
            
            _logger.LogDebug("LLM selected tools: {Tools}", string.Join(", ", selectedTools.Select(t => t.Name)));
            return selectedTools.ToArray();
        }
        catch (Exception ex)
        {
            // Log detailed error information for debugging (without request details since it may not be available)
            await LogToolSelectionErrorAsync(null, null, null, ex, "LLM tool selection failed");
            _logger.LogError(ex, "Failed to use LLM for tool selection, falling back to heuristics");
            return SelectToolsUsingHeuristics(task, availableTools, alreadySelectedTools, maxAdditionalTools);
        }
    }

    private ToolDefinition[] SelectToolsUsingHeuristics(
        string task, 
        IList<McpClientTool> availableTools, 
        List<ToolDefinition> alreadySelectedTools,
        int maxAdditionalTools)
    {
        var taskLower = task.ToLowerInvariant();
        var selectedTools = new List<ToolDefinition>();
        var alreadySelectedNames = alreadySelectedTools.Select(t => t.Name).ToHashSet();
        
        // Simple keyword-based heuristics
        var keywordMappings = new Dictionary<string[], string[]>
        {
            [new[] { "math", "calculate", "add", "subtract", "multiply", "divide", "number" }] = 
                new[] { "add", "subtract", "multiply", "divide" },
            [new[] { "file", "files", "read", "write", "directory", "folder", "save", "load", "list" }] = 
                new[] { "read_file", "write_file", "list_directory", "file_info" },
            [new[] { "text", "search", "replace", "word", "count", "format" }] = 
                new[] { "search_in_file", "replace_in_file", "word_count" },
            [new[] { "time", "date", "current", "now" }] = 
                new[] { "current_time" },
            [new[] { "system", "environment", "variable", "info" }] = 
                new[] { "get_env_var", "system_info" },
            [new[] { "web", "search", "internet", "online", "news", "current", "latest", "recent", "today", "real-time", "live", "browse", "website", "url", "google", "find", "available", "which", "what", "list", "models", "options", "versions", "supported", "offerings", "plans", "pricing", "features", "capabilities", "services", "apis", "endpoints", "status", "working", "active" }] = 
                new[] { "web_search" }
        };
        
        foreach (var (keywords, toolNames) in keywordMappings)
        {
            if (keywords.Any(keyword => taskLower.Contains(keyword)))
            {
                foreach (var toolName in toolNames)
                {
                    if (selectedTools.Count >= maxAdditionalTools) break;
                    
                    // Skip web_search as it's handled separately as a built-in OpenAI tool
                    if (string.Equals(toolName, "web_search", StringComparison.OrdinalIgnoreCase))
                        continue;
                    
                    var tool = availableTools.FirstOrDefault(t => 
                        string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase) &&
                        !alreadySelectedNames.Contains(t.Name));
                    
                    if (tool != null)
                    {
                        selectedTools.Add(_toolManager.CreateOpenAiToolDefinition(tool));
                        alreadySelectedNames.Add(tool.Name);
                    }
                }
            }
        }
        
        // If we still have slots, add some general-purpose tools
        if (selectedTools.Count < maxAdditionalTools)
        {
            var generalTools = new[] { "file_info", "list_directory", "current_time" };
            foreach (var toolName in generalTools)
            {
                if (selectedTools.Count >= maxAdditionalTools) break;
                
                var tool = availableTools.FirstOrDefault(t => 
                    string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase) &&
                    !alreadySelectedNames.Contains(t.Name));
                
                if (tool != null)
                {
                    selectedTools.Add(_toolManager.CreateOpenAiToolDefinition(tool));
                }
            }
        }
        
        _logger.LogDebug("Heuristic selected tools: {Tools}", string.Join(", ", selectedTools.Select(t => t.Name)));
        return selectedTools.ToArray();
    }

    private async Task<ToolDefinition[]> SelectAdditionalToolsUsingLLMAsync(
        string conversationContext, 
        IList<McpClientTool> remainingTools, 
        int maxAdditionalTools)
    {
        var toolDescriptions = remainingTools
            .Select(t => $"- {t.Name}: {t.Description ?? "No description available"}")
            .ToList();
        
        var prompt = $"""
            You are analyzing a conversation to suggest additional tools that might be helpful.
            
            Recent conversation context:
            {conversationContext}
            
            Available additional tools:
            {string.Join("\n", toolDescriptions)}
            
            Based on the conversation context, select up to {maxAdditionalTools} additional tools that might be helpful for continuing this conversation.
            Only suggest tools if they seem genuinely useful based on the conversation flow.
            
            Return your response as a JSON array of tool names only, or an empty array if no additional tools are needed.
            
            Selected additional tools:
            """;

        try
        {
            var request = new ResponsesCreateRequest
            {
                Model = _config.SelectionModel,
                Input = new[]
                {
                    new { role = "user", content = prompt }
                },
                ToolChoice = "none"
            };

            var response = await _openAi.CreateResponseAsync(request);
            var outputMessage = response.Output?
                .OfType<OutputMessage>()
                .FirstOrDefault(m => string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase));
                
            var content = ExtractTextFromContent(outputMessage?.Content);

            var selectedToolNames = JsonSerializer.Deserialize<string[]>(content.Trim()) ?? Array.Empty<string>();
            
            var selectedTools = new List<ToolDefinition>();
            foreach (var toolName in selectedToolNames.Take(maxAdditionalTools))
            {
                var tool = remainingTools.FirstOrDefault(t => 
                    string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase));
                
                if (tool != null)
                {
                    selectedTools.Add(_toolManager.CreateOpenAiToolDefinition(tool));
                }
            }
            
            return selectedTools.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to use LLM for additional tool selection");
            return Array.Empty<ToolDefinition>();
        }
    }

    private string ExtractRecentContext(IEnumerable<object> conversationContext)
    {
        // Extract the last few messages for context
        var recentMessages = conversationContext
            .TakeLast(4) // Last 4 messages should be enough context
            .Select(msg =>
            {
                var roleProperty = msg.GetType().GetProperty("role");
                var contentProperty = msg.GetType().GetProperty("content");
                
                if (roleProperty != null && contentProperty != null)
                {
                    var role = roleProperty.GetValue(msg)?.ToString() ?? "";
                    var content = contentProperty.GetValue(msg)?.ToString() ?? "";
                    return $"{role}: {content}";
                }
                return "";
            })
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
        
        return string.Join("\n", recentMessages);
    }
    
    /// <summary>
    /// Extracts text content from OpenAI response content JsonElement
    /// </summary>
    private static string ExtractTextFromContent(JsonElement? content)
    {
        if (!content.HasValue || content.Value.ValueKind != JsonValueKind.Array)
            return "";

        foreach (var item in content.Value.EnumerateArray())
        {
            if (item.TryGetProperty("type", out var typeElement) && 
                typeElement.GetString() == "output_text" &&
                item.TryGetProperty("text", out var textElement))
            {
                return textElement.GetString() ?? "";
            }
        }
        
        return "";
    }

    public bool ShouldIncludeWebSearch(string task)
    {
        if (string.IsNullOrWhiteSpace(task))
            return false;

        var taskLower = task.ToLowerInvariant();

        // Keywords that explicitly imply web search
        var webSearchKeywords = new[] {
            "web", "search", "internet", "online", "news", "current", "latest",
            "recent", "today", "real-time", "live", "browse", "website", "url",
            "google", "find", "what's happening", "breaking", "update", "trending"
        };

        // Keywords that suggest need for up-to-date information
        var currentInfoKeywords = new[] {
            "available", "which", "what", "list", "models", "options", "versions",
            "supported", "offerings", "plans", "pricing", "features", "capabilities",
            "services", "apis", "endpoints", "status", "working", "active"
        };

        // NEW – keywords indicating local file / directory operations.
        var fileOperationKeywords = new[] {
            "file", "files", "directory", "folder", "path", "contents", "content",
            "read", "write", "save", "load"
        };

        // If it looks like a local file-system task and lacks explicit web-search terms, skip web search
        if (fileOperationKeywords.Any(k => taskLower.Contains(k)) &&
            !webSearchKeywords.Any(k => taskLower.Contains(k)))
        {
            return false;
        }

        return webSearchKeywords.Any(k => taskLower.Contains(k)) ||
               currentInfoKeywords.Any(k => taskLower.Contains(k));
    }

    /// <summary>
    /// Log detailed tool selection reasoning for better activity tracking and debugging
    /// </summary>
    private async Task LogToolSelectionReasoningAsync(
        string task, 
        IList<McpClientTool> availableTools, 
        List<ToolDefinition> selectedTools, 
        long durationMs)
    {
        if (_activityLogger == null) return;

        var availableToolNames = availableTools.Select(t => t.Name).ToList();
        var selectedToolNames = selectedTools.Select(t => t.Name).ToList();
        var rejectedToolNames = availableToolNames.Except(selectedToolNames).ToList();

        // Analyze task to determine reasoning
        var taskAnalysis = AnalyzeTaskForToolSelection(task);
        
        var selectionReasoning = new
        {
            Task = task,
            TaskAnalysis = taskAnalysis,
            SelectionDurationMs = durationMs,
            AvailableToolsCount = availableTools.Count,
            SelectedToolsCount = selectedTools.Count,
            MaxToolsAllowed = _config.MaxToolsPerRequest,
            
            SelectedTools = selectedTools.Select(t => new 
            { 
                Name = t.Name, 
                Description = t.Description,
                SelectionReason = DetermineSelectionReason(t.Name, task, taskAnalysis)
            }).ToList(),
            
            RejectedTools = rejectedToolNames.Take(10).Select(name => new 
            { 
                Name = name,
                RejectionReason = DetermineRejectionReason(name, task, taskAnalysis)
            }).ToList(),
            
            SelectionMethod = _config.UseLLMSelection ? "LLM-based" : "Heuristic-based",
            WebSearchIncluded = ShouldIncludeWebSearch(task),
            WebSearchReasoning = GetWebSearchReasoning(task),
            
            TaskCategories = taskAnalysis.Categories,
            RelevanceFiltering = new
            {
                TaskKeywords = taskAnalysis.Keywords,
                ToolMatchingCriteria = "Keywords, categories, and context-based relevance"
            }
        };

        await _activityLogger.LogActivityAsync(
            ActivityTypes.ToolSelectionReasoning,
            $"Selected {selectedTools.Count} tools from {availableTools.Count} available for task",
            selectionReasoning
        );
    }

    /// <summary>
    /// Analyze task to categorize and extract keywords for tool selection reasoning
    /// </summary>
    private TaskAnalysis AnalyzeTaskForToolSelection(string task)
    {
        var taskLower = task.ToLowerInvariant();
        var categories = new List<string>();
        var keywords = new List<string>();

        // Extract keywords and categorize
        if (taskLower.Contains("math") || taskLower.Contains("calculate") || taskLower.Contains("number"))
        {
            categories.Add("Mathematical");
            keywords.AddRange(new[] { "math", "calculate", "number" });
        }
        
        if (taskLower.Contains("file") || taskLower.Contains("read") || taskLower.Contains("write"))
        {
            categories.Add("File Operations");
            keywords.AddRange(new[] { "file", "read", "write" });
        }
        
        if (taskLower.Contains("openai") || taskLower.Contains("model") || taskLower.Contains("ai"))
        {
            categories.Add("AI/OpenAI");
            keywords.AddRange(new[] { "openai", "model", "ai" });
            
            // If asking about available models, pricing, features, etc. - likely needs current info
            if (taskLower.Contains("available") || taskLower.Contains("models") || 
                taskLower.Contains("which") || taskLower.Contains("list") ||
                taskLower.Contains("pricing") || taskLower.Contains("features") ||
                taskLower.Contains("capabilities") || taskLower.Contains("options"))
            {
                keywords.AddRange(new[] { "available", "current", "latest" });
            }
        }
        
        if (taskLower.Contains("github") || taskLower.Contains("repository") || taskLower.Contains("pull request"))
        {
            categories.Add("GitHub/Repository");
            keywords.AddRange(new[] { "github", "repository", "pull request" });
        }
        
        if (taskLower.Contains("web") || taskLower.Contains("search") || taskLower.Contains("current") || taskLower.Contains("latest"))
        {
            categories.Add("Web/Search");
            keywords.AddRange(new[] { "web", "search", "current", "latest" });
        }

        if (categories.Count == 0)
        {
            categories.Add("General");
        }

        return new TaskAnalysis
        {
            Categories = categories,
            Keywords = keywords.Distinct().ToList(),
            RequiresCurrentInfo = ShouldIncludeWebSearch(task),
            ComplexityLevel = DetermineTaskComplexity(task)
        };
    }

    /// <summary>
    /// Determine why a tool was selected
    /// </summary>
    private string DetermineSelectionReason(string toolName, string task, TaskAnalysis analysis)
    {
        if (_config.EssentialTools.Contains(toolName, StringComparer.OrdinalIgnoreCase))
        {
            return "Essential tool always included";
        }

        if (toolName.Equals("web_search", StringComparison.OrdinalIgnoreCase) && analysis.RequiresCurrentInfo)
        {
            return "Task requires current/real-time information";
        }

        // Check for keyword matches
        var toolLower = toolName.ToLowerInvariant();
        var matchingKeywords = analysis.Keywords.Where(k => toolLower.Contains(k) || task.ToLowerInvariant().Contains(toolLower)).ToList();
        
        if (matchingKeywords.Any())
        {
            return $"Matches task keywords: {string.Join(", ", matchingKeywords)}";
        }

        // Check for category matches
        foreach (var category in analysis.Categories)
        {
            if (IsToolRelevantToCategory(toolName, category))
            {
                return $"Relevant to task category: {category}";
            }
        }

        return "Selected by LLM analysis";
    }

    /// <summary>
    /// Determine why a tool was rejected
    /// </summary>
    private string DetermineRejectionReason(string toolName, string task, TaskAnalysis analysis)
    {
        var toolLower = toolName.ToLowerInvariant();
        
        // If task requires current info but this isn't web search, prioritize web search
        if (analysis.RequiresCurrentInfo && !toolLower.Contains("web_search") && !toolLower.Contains("search"))
        {
            return "Task requires current information - web search prioritized";
        }
        
        // Check if tool is completely unrelated to task categories
        var isRelevant = analysis.Categories.Any(category => IsToolRelevantToCategory(toolName, category));
        
        if (!isRelevant)
        {
            return $"Not relevant to task categories: {string.Join(", ", analysis.Categories)}";
        }

        if (toolLower.Contains("github") && !analysis.Categories.Contains("GitHub/Repository"))
        {
            return "GitHub tool not needed for non-repository task";
        }

        if (toolLower.Contains("vector") && !task.ToLowerInvariant().Contains("vector") && !task.ToLowerInvariant().Contains("search"))
        {
            return "Vector store operations not required";
        }

        return "Lower priority or space limitations";
    }

    /// <summary>
    /// Get detailed reasoning for why web search was or wasn't included
    /// </summary>
    private string GetWebSearchReasoning(string task)
    {
        if (string.IsNullOrWhiteSpace(task))
            return "No task provided";

        var taskLower = task.ToLowerInvariant();
        
        var webSearchKeywords = new[] { 
            "web", "search", "internet", "online", "news", "current", "latest", 
            "recent", "today", "real-time", "live", "browse", "website", "url", 
            "google", "find", "what's happening", "breaking", "update", "trending"
        };
        
        var currentInfoKeywords = new[] {
            "available", "which", "what", "list", "models", "options", "versions",
            "supported", "offerings", "plans", "pricing", "features", "capabilities",
            "services", "apis", "endpoints", "status", "working", "active"
        };
        
        var foundWebKeywords = webSearchKeywords.Where(k => taskLower.Contains(k)).ToList();
        var foundCurrentInfoKeywords = currentInfoKeywords.Where(k => taskLower.Contains(k)).ToList();
        
        if (foundWebKeywords.Any() || foundCurrentInfoKeywords.Any())
        {
            var reasons = new List<string>();
            if (foundWebKeywords.Any())
                reasons.Add($"Web search keywords: {string.Join(", ", foundWebKeywords)}");
            if (foundCurrentInfoKeywords.Any())
                reasons.Add($"Current info keywords: {string.Join(", ", foundCurrentInfoKeywords)}");
            return string.Join("; ", reasons);
        }
        
        return "No keywords indicating need for current information or web search";
    }

    /// <summary>
    /// Check if a tool is relevant to a specific category
    /// </summary>
    private bool IsToolRelevantToCategory(string toolName, string category)
    {
        var toolLower = toolName.ToLowerInvariant();
        
        return category switch
        {
            "Mathematical" => toolLower.Contains("add") || toolLower.Contains("subtract") || 
                             toolLower.Contains("multiply") || toolLower.Contains("divide") || 
                             toolLower.Contains("math"),
            "File Operations" => toolLower.Contains("file") || toolLower.Contains("read") || 
                               toolLower.Contains("write") || toolLower.Contains("directory"),
            "AI/OpenAI" => toolLower.Contains("openai") || toolLower.Contains("gpt") || 
                          toolLower.Contains("complete"),
            "GitHub/Repository" => toolLower.Contains("github") || toolLower.Contains("pull") || 
                                  toolLower.Contains("repository"),
            "Web/Search" => toolLower.Contains("web") || toolLower.Contains("search"),
            _ => false
        };
    }

    /// <summary>
    /// Determine task complexity based on content
    /// </summary>
    private string DetermineTaskComplexity(string task)
    {
        var wordCount = task.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var hasMultipleVerbs = task.ToLowerInvariant().Split(' ').Count(w => 
            new[] { "analyze", "create", "generate", "build", "develop", "implement", "design" }.Contains(w)) > 1;
        
        if (wordCount > 20 || hasMultipleVerbs)
            return "High";
        else if (wordCount > 10)
            return "Medium";
        else
            return "Low";
    }

    /// <summary>
    /// Task analysis result for tool selection reasoning
    /// </summary>
    private class TaskAnalysis
    {
        public List<string> Categories { get; set; } = new();
        public List<string> Keywords { get; set; } = new();
        public bool RequiresCurrentInfo { get; set; }
        public string ComplexityLevel { get; set; } = "Medium";
    }

    /// <summary>
    /// Log detailed error information when tool selection fails
    /// </summary>
    private async Task LogToolSelectionErrorAsync(
        ResponsesCreateRequest? request, 
        ResponsesCreateResponse? response, 
        string? extractedContent, 
        Exception exception, 
        string errorContext)
    {
        if (_activityLogger == null) return;

        var errorDetails = new
        {
            ErrorContext = errorContext,
            Exception = new
            {
                Type = exception.GetType().Name,
                Message = exception.Message,
                StackTrace = exception.StackTrace
            },
            Request = request != null ? new
            {
                Model = request.Model,
                InputType = request.Input?.GetType().Name ?? "null",
                ToolChoice = request.ToolChoice,
                // Include partial request content for debugging (truncated for safety)
                InputPreview = request.Input?.ToString()?.Substring(0, Math.Min(500, request.Input?.ToString()?.Length ?? 0))
            } : null,
            Response = response != null ? new
            {
                OutputItemCount = response.Output?.Count() ?? 0,
                Usage = response.Usage,
                // Include partial response for debugging
                OutputTypes = response.Output?.Select(o => o.GetType().Name).ToArray()
            } : null,
            ExtractedContent = extractedContent != null ? new
            {
                Length = extractedContent.Length,
                Content = extractedContent.Length > 1000 ? extractedContent.Substring(0, 1000) + "..." : extractedContent,
                IsValidJson = IsValidJson(extractedContent)
            } : null
        };

        await _activityLogger.LogActivityAsync(
            ActivityTypes.Error,
            $"Tool selection error: {errorContext}",
            errorDetails
        );
    }

    /// <summary>
    /// Check if a string is valid JSON
    /// </summary>
    private bool IsValidJson(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return false;
        
        try
        {
            JsonDocument.Parse(content);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get descriptions of built-in OpenAI tools that could be relevant for the task
    /// </summary>
    private List<string> GetBuiltInOpenAIToolDescriptions(string task, List<ToolDefinition> alreadySelectedTools)
    {
        var descriptions = new List<string>();
        var alreadySelectedNames = alreadySelectedTools.Select(t => t.Name).ToHashSet();

        // Add web search tool if it's not already selected and could be relevant
        if (!alreadySelectedNames.Contains("web_search") && ShouldIncludeWebSearch(task))
        {
            descriptions.Add("- web_search: Search the web for current information and real-time data");
        }

        // Add other built-in tools as they become available
        // Future: code_interpreter, file_search, etc.

        return descriptions;
    }

    /// <summary>
    /// Get a ToolDefinition for a built-in OpenAI tool by name
    /// </summary>
    private ToolDefinition? GetBuiltInOpenAIToolDefinition(string toolName)
    {
        return toolName.ToLowerInvariant() switch
        {
            "web_search" => _agentConfig.WebSearch.ToToolDefinition(),
            // Future: add other built-in tools
            _ => null
        };
    }
}