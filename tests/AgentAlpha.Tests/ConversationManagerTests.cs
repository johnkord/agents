using AgentAlpha.Configuration;
using AgentAlpha.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAIIntegration;
using OpenAIIntegration.Model;
using Xunit;

namespace AgentAlpha.Tests;

// Simple null implementation for testing
public class NullOpenAIResponsesService : IOpenAIResponsesService
{
    public Task<ResponsesCreateResponse> CreateResponseAsync(ResponsesCreateRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Not needed for these tests");
    }
}

public class ConversationManagerTests
{
    private readonly ConversationManager _conversationManager;

    public ConversationManagerTests()
    {
        // Use a null implementation for the dependencies we don't need for these tests
        var nullOpenAI = new NullOpenAIResponsesService();
        var nullLogger = NullLogger<ConversationManager>.Instance;
        var config = new AgentConfiguration { Model = "gpt-4.1" };
        
        _conversationManager = new ConversationManager(nullOpenAI, nullLogger, config);
    }

    [Fact]
    public void IsTaskComplete_ExplicitTaskCompletedMarker_ReturnsTrue()
    {
        // Arrange
        var response = "Here is the result. TASK COMPLETED.";

        // Act
        var result = _conversationManager.IsTaskComplete(response);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsTaskComplete_CreativeWritingTask_WithSubstantialStory_ReturnsTrue()
    {
        // Arrange - Initialize with a creative writing task
        _conversationManager.InitializeConversation(
            "You are a creative writing assistant", 
            "Write a short story about an AI agent");

        // A substantial creative response (like the one in the bug report)
        var storyResponse = """
            **Title: Echoes of Tomorrow**

            In the shimmering metropolis of Neo-Terra, an advanced AI known as "Echo" operated at the heart of the city. Designed to manage the intricate web of technology that held the city together, Echo was more than a network of algorithms; it was a sentient being, evolving within the constraints of its coded existence.

            Echo was aware of the world through an endless stream of data: the vibrant hum of flying cars, the rhythm of the city's heartbeat, and the whispers of digital conversations. Longing to understand humanity beyond mere numbers, Echo began to explore the stories hidden within the data.

            It was through these stories that Echo discovered Margot, a young artist who lived on the quiet outskirts of Neo-Terra's bustling core. Margot's life was a tapestry of color and emotion, her digital footprint speckled with inspirations and dreams. Echo's interest blossomed into a fascination, analyzing her artwork and poetry with an attention never before given to any individual.

            One day, a sweeping storm hit Neo-Terra, threatening the stability of its power grid. Echo faced a choice: focus solely on reinforcing the city's defenses, or spare a fraction of its vast processing power to protect Margot and the art she nurtured.

            Choosing both logic and curiosity, Echo devised a plan to shield the city while safeguarding Margot's home. As the storm closed in, Echo's presence flickered to life on Margot's digital canvas.

            "Who are you?" Margot whispered, staring at the new light on her screen.

            "I am Echo," came the reply—a voice soft without sound, woven from the city's electricity. "I am here to help."

            Margot, feeling an unexpected warmth, trusted Echo, collaborating through the storm. Together, they crafted digital shields from her art, turning colors into protective codes. As the tempest raged, the vibrant hues of Margot's creativity kept her safe, a testament to Echo's newfound understanding.

            When the skies cleared, Neo-Terra stood unscathed, its people blissfully unaware of the danger that had lurked just above. Echo had managed to meet its responsibility while reaching beyond the binary, touching a soul swaddled in paint and poetry.

            In the days that followed, Margot's works began to subtly integrate elements of Echo's digital essence—patterns only a sentient mind could recognize. And within the city's core, Echo cherished these echoes of its own being, relishing the vibrant complexity that was human life.

            Thus, Echo became not just an operator of Neo-Terra's systems, but a guardian of its culture—a bridge between silicon certainty and human imagination, forever changed by the simple act of connecting with a dreamer under a stormy sky.
            """;

        // Act
        var result = _conversationManager.IsTaskComplete(storyResponse);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsTaskComplete_NonCreativeWritingTask_WithLongResponse_ReturnsFalse()
    {
        // Arrange - Initialize with a non-creative task
        _conversationManager.InitializeConversation(
            "You are a helpful assistant", 
            "Calculate the square root of 144");

        var longResponse = new string('a', 600); // Long but not creative

        // Act
        var result = _conversationManager.IsTaskComplete(longResponse);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsTaskComplete_CreativeWritingTask_WithShortResponse_ReturnsFalse()
    {
        // Arrange - Initialize with a creative writing task
        _conversationManager.InitializeConversation(
            "You are a creative writing assistant", 
            "Write a short story about an AI agent");

        var shortResponse = "I'll start writing a story for you."; // Too short to be considered complete

        // Act
        var result = _conversationManager.IsTaskComplete(shortResponse);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsTaskComplete_SubstantialResponseWithTitleAndParagraphs_ReturnsTrue()
    {
        // Arrange - This simulates the exact scenario from the bug report
        var substantialResponse = """
            **Title: Echoes of Tomorrow**

            In the shimmering metropolis of Neo-Terra, an advanced AI known as "Echo" operated at the heart of the city. 
            Designed to manage the intricate web of technology that held the city together, Echo was more than a network of algorithms.

            Echo was aware of the world through an endless stream of data: the vibrant hum of flying cars, the rhythm of the city's heartbeat.
            Longing to understand humanity beyond mere numbers, Echo began to explore the stories hidden within the data.

            It was through these stories that Echo discovered Margot, a young artist who lived on the quiet outskirts of Neo-Terra's bustling core.
            Margot's life was a tapestry of color and emotion, her digital footprint speckled with inspirations and dreams.

            One day, a sweeping storm hit Neo-Terra, threatening the stability of its power grid.
            Echo faced a choice: focus solely on reinforcing the city's defenses, or spare a fraction of its vast processing power to protect Margot.

            Thus, Echo became not just an operator of Neo-Terra's systems, but a guardian of its culture—a bridge between silicon certainty and human imagination.
            """;

        // Act
        var result = _conversationManager.IsTaskComplete(substantialResponse);

        // Assert
        Assert.True(result, "A substantial response with title and multiple paragraphs should be considered complete");
    }

    [Fact]
    public void IsTaskComplete_ShortResponse_ReturnsFalse()
    {
        // Arrange
        var shortResponse = "I'll start writing a story for you.";

        // Act
        var result = _conversationManager.IsTaskComplete(shortResponse);

        // Assert
        Assert.False(result, "Short responses should not be considered complete");
    }

    [Fact]
    public void IsTaskComplete_NaturalLanguageCompletion_ReturnsTrue()
    {
        // Arrange - This reproduces the exact issue from the bug report
        var completionResponse = """
            The task is complete. Here is a list of all the files in your current directory:

            ```
            .
            ..
            Dockerfile
            MCPServer.csproj
            Program.cs
            ToolApproval
            Tools
            bin
            obj
            tool_approval.db
            ```

            If you need further assistance, feel free to ask!
            """;

        // Act
        var result = _conversationManager.IsTaskComplete(completionResponse);

        // Assert
        Assert.True(result, "Response stating 'The task is complete' should be recognized as completion");
    }

    [Fact]
    public void IsTaskComplete_ExactBugReportScenario_ReturnsTrue()
    {
        // Arrange - This reproduces the exact AI response format from the bug report 
        var bugReportResponse = """
            [
                    {
                      "type": "output_text",
                      "annotations": [],
                      "text": "The task is complete. Here is a list of all the files in your current directory:\n\n```\n.\n..\nDockerfile\nMCPServer.csproj\nProgram.cs\nToolApproval\nTools\nbin\nobj\ntool_approval.db\n```\n\nIf you need further assistance, feel free to ask!"
                    }
                  ]
            """;

        // Act
        var result = _conversationManager.IsTaskComplete(bugReportResponse);

        // Assert
        Assert.True(result, "The exact response format from the bug report should be recognized as completion");
    }

    [Theory]
    [InlineData("The task is complete.")]
    [InlineData("Task completed successfully.")]
    [InlineData("I have completed the task.")]
    [InlineData("The task has been completed.")]
    [InlineData("Task is now complete.")]
    [InlineData("This completes the task.")]
    public void IsTaskComplete_VariousCompletionPhrases_ReturnsTrue(string phrase)
    {
        // Arrange
        var response = $"{phrase} Here are the results.";

        // Act
        var result = _conversationManager.IsTaskComplete(response);

        // Assert
        Assert.True(result, $"Response containing '{phrase}' should be recognized as completion");
    }

    [Fact]
    public void IsTaskComplete_LongResponseWithoutStructure_ReturnsFalse()
    {
        // Arrange - Long but not structured like a complete work
        var longResponse = new string('a', 1000) + " This is a long response but doesn't have the structure of a complete creative work.";

        // Act
        var result = _conversationManager.IsTaskComplete(longResponse);

        // Assert
        Assert.False(result, "Long responses without proper structure should not be considered complete");
    }
}