using System;
using System.Text.Json;
using Xunit;

namespace AgentAlpha.Tests
{
    public class ContentExtractionTests
    {
        [Fact]
        public void ExtractTextFromContent_ShouldWork()
        {
            // Test JSON content similar to what OpenAI returns
            var contentJson = @"[
                {
                    ""type"": ""output_text"",
                    ""annotations"": [],
                    ""text"": ""[\""list_directory\""]""
                }
            ]";

            var contentElement = JsonSerializer.Deserialize<JsonElement>(contentJson);
            
            // Test the helper method we'll create
            var extractedText = ExtractTextFromContent(contentElement);
            
            Assert.Equal("[\"list_directory\"]", extractedText);
        }
        
        [Fact]
        public void ExtractTextFromContent_WithMultipleItems_ShouldReturnFirstText()
        {
            var contentJson = @"[
                {
                    ""type"": ""output_text"",
                    ""annotations"": [],
                    ""text"": ""[\""list_directory\"", \""read_file\""]""
                },
                {
                    ""type"": ""image"",
                    ""url"": ""http://example.com/image.jpg""
                }
            ]";

            var contentElement = JsonSerializer.Deserialize<JsonElement>(contentJson);
            var extractedText = ExtractTextFromContent(contentElement);
            
            Assert.Equal("[\"list_directory\", \"read_file\"]", extractedText);
        }
        
        [Fact]
        public void ExtractTextFromContent_WithNoTextType_ShouldReturnEmpty()
        {
            var contentJson = @"[
                {
                    ""type"": ""image"",
                    ""url"": ""http://example.com/image.jpg""
                }
            ]";

            var contentElement = JsonSerializer.Deserialize<JsonElement>(contentJson);
            var extractedText = ExtractTextFromContent(contentElement);
            
            Assert.Equal("", extractedText);
        }
        
        [Fact]
        public void ExtractTextFromContent_WithEmptyArray_ShouldReturnEmpty()
        {
            var contentJson = @"[]";

            var contentElement = JsonSerializer.Deserialize<JsonElement>(contentJson);
            var extractedText = ExtractTextFromContent(contentElement);
            
            Assert.Equal("", extractedText);
        }
        
        [Fact]
        public void ExtractTextFromContent_WithNullContent_ShouldReturnEmpty()
        {
            var extractedText = ExtractTextFromContent(null);
            Assert.Equal("", extractedText);
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