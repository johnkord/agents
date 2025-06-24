using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Diagnostics;
using ModelContextProtocol.Client;
using OpenAIIntegration;
using OpenAIIntegration.Model;
using AgentAlpha.Interfaces;
using AgentAlpha.Models;
using AgentAlpha.Configuration;

namespace AgentAlpha.Services;

/// <summary>
/// Implementation of intelligent tool selection to reduce OpenAI context costs
/// </summary>
public class ToolSelector : IToolSelector
{
    private readonly IOpenAIResponsesService _openAi;
    private readonly IToolManager _toolManager;
    private readonly ILogger<ToolSelector> _logger;
    private readonly ToolSelectionConfig _config;

    public ToolSelector(
        IOpenAIResponsesService openAi,
        IToolManager toolManager,
        ILogger<ToolSelector> logger,
        ToolSelectionConfig? config = null)
    {
        _openAi = openAi;
        _toolManager = toolManager;
        _logger = logger;
        _config = config ?? ToolSelectionConfig.Default;
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
            
            // Use LLM to select additional relevant tools
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
            }
            
            stopwatch.Stop();
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
        // Create a prompt for tool selection
        var toolDescriptions = availableTools
            .Where(t => !alreadySelectedTools.Any(s => s.Name == t.Name))
            .Select(t => $"- {t.Name}: {t.Description ?? "No description available"}")
            .ToList();
        
        if (toolDescriptions.Count == 0)
        {
            return Array.Empty<ToolDefinition>();
        }
        
        var prompt = $"""
            You are a tool selection assistant. Given a task and a list of available tools, select the most relevant tools that would be needed to complete the task.

            Task: {task}

            Available tools:
            {string.Join("\n", toolDescriptions)}

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
            var content = response.Output?
                .OfType<OutputMessage>()
                .FirstOrDefault(m => string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                ?.Content?.ToString() ?? "";

            // Parse the JSON response
            var selectedToolNames = JsonSerializer.Deserialize<string[]>(content.Trim()) ?? Array.Empty<string>();
            
            // Convert selected tool names back to ToolDefinitions
            var selectedTools = new List<ToolDefinition>();
            foreach (var toolName in selectedToolNames.Take(maxAdditionalTools))
            {
                var tool = availableTools.FirstOrDefault(t => 
                    string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase));
                
                if (tool != null)
                {
                    selectedTools.Add(_toolManager.CreateOpenAiToolDefinition(tool));
                }
            }
            
            _logger.LogDebug("LLM selected tools: {Tools}", string.Join(", ", selectedTools.Select(t => t.Name)));
            return selectedTools.ToArray();
        }
        catch (Exception ex)
        {
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
            [new[] { "file", "read", "write", "directory", "folder", "save", "load" }] = 
                new[] { "read_file", "write_file", "list_directory", "file_info" },
            [new[] { "text", "search", "replace", "word", "count", "format" }] = 
                new[] { "search_in_file", "replace_in_file", "word_count" },
            [new[] { "time", "date", "current", "now" }] = 
                new[] { "current_time" },
            [new[] { "system", "environment", "variable", "info" }] = 
                new[] { "get_env_var", "system_info" }
        };
        
        foreach (var (keywords, toolNames) in keywordMappings)
        {
            if (keywords.Any(keyword => taskLower.Contains(keyword)))
            {
                foreach (var toolName in toolNames)
                {
                    if (selectedTools.Count >= maxAdditionalTools) break;
                    
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
            var content = response.Output?
                .OfType<OutputMessage>()
                .FirstOrDefault(m => string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                ?.Content?.ToString() ?? "";

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
}