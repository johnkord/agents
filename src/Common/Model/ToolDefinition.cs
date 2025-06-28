using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenAIIntegration.Model;

[JsonConverter(typeof(ToolDefinitionConverter))]
public sealed class ToolDefinition
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    // Function tool properties
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /* Keep the parameters bag untyped so callers can pass an
       arbitrary JSON-serialisable object graph. */
    [JsonPropertyName("parameters")]
    public object? Parameters { get; set; }

    [JsonPropertyName("strict")]
    public bool? Strict { get; set; }

    // Built-in tool properties
    [JsonPropertyName("user_location")]
    public object? UserLocation { get; set; }

    [JsonPropertyName("search_context_size")]
    public string? SearchContextSize { get; set; }

    // Helper method to determine if this is a built-in tool
    [JsonIgnore]
    public bool IsBuiltInTool => Type != "function";
}

public class ToolDefinitionConverter : JsonConverter<ToolDefinition>
{
    /* DTO without the [JsonConverter] attribute –
       used to bypass the converter during nested deserialisation */
    private sealed class ToolDefinitionDto
    {
        public string           Type              { get; set; } = "function";
        public string           Name              { get; set; } = "";
        public string?          Description       { get; set; }
        public object?          Parameters        { get; set; }
        public bool?            Strict            { get; set; }
        public object?          UserLocation      { get; set; }
        public string?          SearchContextSize { get; set; }
    }

    public override ToolDefinition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Clone options but keep converters list; no need to clear it now
        var safeOptions = new JsonSerializerOptions(options);

        var jsonElement = JsonElement.ParseValue(ref reader);

        // Deserialize into DTO (no converter attribute → no recursion)
        var dto = JsonSerializer.Deserialize<ToolDefinitionDto>(jsonElement.GetRawText(), safeOptions)
                  ?? new ToolDefinitionDto();

        // Map DTO → actual instance
        return new ToolDefinition
        {
            Type              = dto.Type,
            Name              = dto.Name,
            Description       = dto.Description,
            Parameters        = dto.Parameters,
            Strict            = dto.Strict,
            UserLocation      = dto.UserLocation,
            SearchContextSize = dto.SearchContextSize
        };
    }

    public override void Write(Utf8JsonWriter writer, ToolDefinition value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        
        // Always write type
        writer.WriteString("type", value.Type);
        
        if (value.IsBuiltInTool)
        {
            // For built-in tools, only write the relevant properties
            if (value.UserLocation != null)
            {
                writer.WritePropertyName("user_location");
                JsonSerializer.Serialize(writer, value.UserLocation, options);
            }
            
            if (!string.IsNullOrEmpty(value.SearchContextSize))
            {
                writer.WriteString("search_context_size", value.SearchContextSize);
            }
        }
        else
        {
            // For function tools, write the standard properties
            if (!string.IsNullOrEmpty(value.Name))
            {
                writer.WriteString("name", value.Name);
            }
            
            if (!string.IsNullOrEmpty(value.Description))
            {
                writer.WriteString("description", value.Description);
            }
            
            if (value.Parameters != null)
            {
                writer.WritePropertyName("parameters");
                JsonSerializer.Serialize(writer, value.Parameters, options);
            }
            
            if (value.Strict.HasValue)
            {
                writer.WriteBoolean("strict", value.Strict.Value);
            }
        }
        
        writer.WriteEndObject();
    }
}
