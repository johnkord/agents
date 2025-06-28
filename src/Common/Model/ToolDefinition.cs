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
    public override ToolDefinition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Create a new options instance without the converter to avoid recursion
        var optionsWithoutConverter = new JsonSerializerOptions(options);
        optionsWithoutConverter.Converters.Clear();
        
        var jsonElement = JsonElement.ParseValue(ref reader);
        return JsonSerializer.Deserialize<ToolDefinition>(jsonElement.GetRawText(), optionsWithoutConverter) ?? new ToolDefinition();
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
