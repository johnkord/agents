using Microsoft.Extensions.Logging;
using System.Text.Json;
using OpenAIIntegration;
using OpenAIIntegration.Model;
using AgentAlpha.Configuration;
using AgentAlpha.Interfaces;
using AgentAlpha.Models;

namespace AgentAlpha.Services;

/// <summary>
/// Implementation of OpenAI conversation and message flow management
/// </summary>
public class ConversationManager : IConversationManager
{
    private readonly IOpenAIResponsesService _openAi;
    private readonly ILogger<ConversationManager> _logger;
    private readonly AgentConfiguration _config;
    private readonly List<object> _messages;

    public ConversationManager(IOpenAIResponsesService openAi, ILogger<ConversationManager> logger, AgentConfiguration config)
    {
        _openAi = openAi;
        _logger = logger;
        _config = config;
        _messages = new List<object>();
    }

    public void InitializeConversation(string systemPrompt, string userTask)
    {
        _messages.Clear();
        _messages.Add(new { role = "system", content = systemPrompt });
        _messages.Add(new { role = "user", content = userTask });
        
        _logger.LogInformation("Initialized conversation with task: {Task}", userTask);
    }

    public async Task<ConversationResponse> ProcessIterationAsync(OpenAIIntegration.Model.ToolDefinition[] availableTools)
    {
        var request = new ResponsesCreateRequest
        {
            Model = _config.Model,
            Input = _messages.ToArray(),
            Tools = availableTools,
            ToolChoice = "auto"
        };

        try
        {
            var response = await _openAi.CreateResponseAsync(request);
            
            // Log the full response for debugging
            _logger.LogDebug("OpenAI Response: {Response}", JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }));

            // Extract assistant text
            var assistantText = response.Output?
                .OfType<OutputMessage>()
                .FirstOrDefault(m => string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                ?.Content?.ToString() ?? "";

            // Extract tool calls
            var toolCalls = ExtractToolCalls(response);

            return new ConversationResponse(assistantText, toolCalls, toolCalls.Count > 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process conversation iteration");
            throw;
        }
    }

    public void AddAssistantMessage(string content)
    {
        _messages.Add(new { role = "assistant", content });
        _logger.LogDebug("Added assistant message to conversation");
    }

    public void AddToolResults(IEnumerable<string> toolSummaries)
    {
        var summary = string.Join("\n", toolSummaries);
        _messages.Add(new { role = "assistant", content = summary });
        _messages.Add(new { role = "user", content = $"I executed the requested tools.\n{summary}\n\nIs the task complete?" });
        
        _logger.LogDebug("Added tool results to conversation: {Summary}", summary);
    }

    public bool IsTaskComplete(string assistantResponse)
    {
        return assistantResponse.Contains("TASK COMPLETED", StringComparison.OrdinalIgnoreCase);
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
}