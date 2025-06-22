using System;                         // NEW
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenAIIntegration.Model;   // ← for ToolDefinition

namespace OpenAIIntegration.Model;

public sealed class ResponsesCreateResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("object")]
    public string? Object { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("background")]
    public bool? Background { get; set; }

    [JsonPropertyName("created_at")]
    public long? CreatedAt { get; set; }

    [JsonPropertyName("error")]
    public ResponseError? Error { get; set; }

    [JsonPropertyName("incomplete_details")]
    public Dictionary<string, object>? IncompleteDetails { get; set; }

    [JsonPropertyName("instructions")]
    public object? Instructions { get; set; }

    [JsonPropertyName("max_output_tokens")]
    public int? MaxOutputTokens { get; set; }

    /* rename `items` → `output` */
    [JsonPropertyName("output")]
    public ResponseOutputItem[]? Output { get; set; }   // CHANGED – now polymorphic

    // NEW – whether the response will be persisted on the server
    [JsonPropertyName("store")]
    public bool? Store { get; set; }

    // NEW – arbitrary user metadata
    [JsonPropertyName("metadata")]
    public Dictionary<string, object?>? Metadata { get; set; }

    // NEW – token accounting
    [JsonPropertyName("usage")]
    public ResponseUsage? Usage { get; set; }

    [JsonPropertyName("parallel_tool_calls")]
    public bool? ParallelToolCalls { get; set; }

    [JsonPropertyName("previous_response_id")]
    public string? PreviousResponseId { get; set; }

    [JsonPropertyName("prompt")]
    public object? Prompt { get; set; }

    [JsonPropertyName("reasoning")]
    public object? Reasoning { get; set; }

    [JsonPropertyName("service_tier")]
    public string? ServiceTier { get; set; }

    // status already present

    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    [JsonPropertyName("text")]
    public object? Text { get; set; }

    [JsonPropertyName("tool_choice")]
    public object? ToolChoice { get; set; }

    [JsonPropertyName("tools")]
    public ToolDefinition[]? Tools { get; set; }

    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }

    [JsonPropertyName("truncation")]
    public object? Truncation { get; set; }

    // usage already present – token names updated below

    [JsonPropertyName("user")]
    public string? User { get; set; }

    // --- removed fields ---
    // public object[]? Items  (renamed)
    // public string? Content  (renamed)
    // public ToolCall[]? ToolCalls (not in spec)
}

public sealed class ResponseUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }

    [JsonPropertyName("output_tokens_details")]
    public OutputTokensDetails? OutputTokensDetails { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

public sealed class OutputTokensDetails
{
    [JsonPropertyName("reasoning_tokens")]
    public int ReasoningTokens { get; set; }
}

public sealed class ResponseError
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

// ---------- NEW helper types for the “output” array ----------

// Base item
[JsonConverter(typeof(ResponseOutputItemConverter))]
public abstract record ResponseOutputItem
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "message";
}

// 1. Normal assistant message
public sealed record OutputMessage : ResponseOutputItem
{
    [JsonPropertyName("id")]     public string? Id      { get; init; }
    [JsonPropertyName("role")]   public string? Role    { get; init; }
    [JsonPropertyName("content")]public JsonElement? Content { get; init; }
    // ...add more message-specific fields if needed...
}

// 2. File-search tool call
public sealed record FileSearchToolCall : ResponseOutputItem
{
    [JsonPropertyName("id")]      public string?   Id      { get; init; }
    [JsonPropertyName("queries")] public string[]? Queries { get; init; }
    [JsonPropertyName("status")]  public string?   Status  { get; init; }
}

// 3. Function tool call
public sealed record FunctionToolCall : ResponseOutputItem
{
    [JsonPropertyName("id")]        public string?    Id        { get; init; }
    [JsonPropertyName("name")]      public string?    Name      { get; init; }
    [JsonPropertyName("arguments")] public JsonElement? Arguments{ get; init; }
    [JsonPropertyName("status")]    public string?    Status    { get; init; }
}

// 4. Web-search tool call
public sealed record WebSearchToolCall : ResponseOutputItem
{
    [JsonPropertyName("id")]      public string?   Id      { get; init; }
    [JsonPropertyName("queries")] public string[]? Queries { get; init; }
    [JsonPropertyName("status")]  public string?   Status  { get; init; }
}

// 5. Computer tool call
public sealed record ComputerToolCall : ResponseOutputItem
{
    [JsonPropertyName("id")]     public string? Id  { get; init; }
    [JsonPropertyName("action")] public JsonElement? Action { get; init; }
    [JsonPropertyName("pending_safety_checks")]
    public JsonElement[]? PendingSafetyChecks { get; init; }
    [JsonPropertyName("status")] public string? Status { get; init; }
}

// 6. Reasoning item
public sealed record ReasoningItem : ResponseOutputItem
{
    [JsonPropertyName("id")]               public string? Id               { get; init; }
    [JsonPropertyName("summary")]          public JsonElement? Summary     { get; init; }
    [JsonPropertyName("encrypted_content")]public string? EncryptedContent { get; init; }
    [JsonPropertyName("status")]           public string? Status           { get; init; }
}

// 7. Image generation call
public sealed record ImageGenerationCall : ResponseOutputItem
{
    [JsonPropertyName("id")]     public string? Id     { get; init; }
    [JsonPropertyName("result")] public string? Result { get; init; }
    [JsonPropertyName("status")] public string? Status { get; init; }
}

// 8. Code interpreter tool call
public sealed record CodeInterpreterToolCall : ResponseOutputItem
{
    [JsonPropertyName("id")]          public string? Id          { get; init; }
    [JsonPropertyName("code")]        public string? Code        { get; init; }
    [JsonPropertyName("results")]     public JsonElement? Results{ get; init; }
    [JsonPropertyName("status")]      public string? Status      { get; init; }
    [JsonPropertyName("container_id")]public string? ContainerId { get; init; }
}

// 9. Local shell tool call
public sealed record LocalShellToolCall : ResponseOutputItem
{
    [JsonPropertyName("id")]      public string? Id       { get; init; }
    [JsonPropertyName("call_id")] public string? CallId   { get; init; }
    [JsonPropertyName("action")]  public JsonElement? Action { get; init; }
    [JsonPropertyName("status")]  public string? Status   { get; init; }
}

// 10. MCP tool call
public sealed record McpToolCall : ResponseOutputItem
{
    [JsonPropertyName("id")]          public string? Id          { get; init; }
    [JsonPropertyName("name")]        public string? Name        { get; init; }
    [JsonPropertyName("arguments")]   public string? Arguments   { get; init; }
    [JsonPropertyName("server_label")]public string? ServerLabel { get; init; }
    [JsonPropertyName("error")]       public string? Error       { get; init; }
    [JsonPropertyName("output")]      public string? Output      { get; init; }
}

// 11. MCP list tools
public sealed record McpListTools : ResponseOutputItem
{
    [JsonPropertyName("id")]          public string? Id          { get; init; }
    [JsonPropertyName("server_label")]public string? ServerLabel { get; init; }
    [JsonPropertyName("tools")]       public JsonElement? Tools  { get; init; }
    [JsonPropertyName("error")]       public string? Error       { get; init; }
}

// 12. MCP approval request
public sealed record McpApprovalRequest : ResponseOutputItem
{
    [JsonPropertyName("id")]          public string? Id          { get; init; }
    [JsonPropertyName("name")]        public string? Name        { get; init; }
    [JsonPropertyName("arguments")]   public string? Arguments   { get; init; }
    [JsonPropertyName("server_label")]public string? ServerLabel { get; init; }
}

// ---------- Polymorphic deserialiser ----------
internal sealed class ResponseOutputItemConverter : JsonConverter<ResponseOutputItem>
{
    public override ResponseOutputItem? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc  = JsonDocument.ParseValue(ref reader);
        var root       = doc.RootElement;
        var typeString = root.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String
                         ? t.GetString()!
                         : "message";

        Type target = typeString switch
        {
            "file_search_call"      => typeof(FileSearchToolCall),
            "function_call"         => typeof(FunctionToolCall),
            "web_search_call"       => typeof(WebSearchToolCall),
            "computer_call"         => typeof(ComputerToolCall),
            "reasoning"             => typeof(ReasoningItem),
            "image_generation_call" => typeof(ImageGenerationCall),
            "code_interpreter_call" => typeof(CodeInterpreterToolCall),
            "local_shell_call"      => typeof(LocalShellToolCall),   // NEW
            "mcp_call"             => typeof(McpToolCall),           // NEW
            "mcp_list_tools"       => typeof(McpListTools),          // NEW
            "mcp_approval_request" => typeof(McpApprovalRequest),    // NEW
            _                       => typeof(OutputMessage) // covers "message" & default
        };

        return (ResponseOutputItem?)JsonSerializer.Deserialize(root.GetRawText(), target, options);
    }

    public override void Write(Utf8JsonWriter writer, ResponseOutputItem value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, (object)value, value.GetType(), options);
}
