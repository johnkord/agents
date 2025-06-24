using System;
using System.Text.Json;
using Xunit;

namespace AgentAlpha.Tests
{
    public class ProblemStatementScenarioTests
    {
        [Fact]
        public void ExtractTextFromContent_WithOpenAILikeResponse_ShouldWork()
        {
            // This simulates the actual content structure from the OpenAI response in the problem statement
            var contentJson = @"[
                {
                    ""type"": ""output_text"",
                    ""annotations"": [],
                    ""text"": ""I can help list all the files in your current directory by using the file operations. Let's proceed with that.""
                }
            ]";

            var contentElement = JsonSerializer.Deserialize<JsonElement>(contentJson);
            
            // Test the helper method
            var extractedText = ExtractTextFromContent(contentElement);
            
            Assert.Equal("I can help list all the files in your current directory by using the file operations. Let's proceed with that.", extractedText);
        }
        
        [Fact]
        public void ExtractTextFromContent_WithToolSelectionResponse_ShouldWork()
        {
            // This simulates what the LLM should return for tool selection
            var contentJson = @"[
                {
                    ""type"": ""output_text"",
                    ""annotations"": [],
                    ""text"": ""[\""list_directory\""]""
                }
            ]";

            var contentElement = JsonSerializer.Deserialize<JsonElement>(contentJson);
            var extractedText = ExtractTextFromContent(contentElement);
            
            Assert.Equal("[\"list_directory\"]", extractedText);
            
            // Verify this can be deserialized as a string array
            var toolNames = JsonSerializer.Deserialize<string[]>(extractedText);
            Assert.NotNull(toolNames);
            Assert.Single(toolNames);
            Assert.Equal("list_directory", toolNames[0]);
        }
        
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
    }
}