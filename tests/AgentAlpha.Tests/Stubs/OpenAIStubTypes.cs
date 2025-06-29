#nullable disable
namespace OpenAIIntegration
{
    public class OpenAIConfig
    {
        public string ApiKey { get; set; } = "";
        public string Model  { get; set; } = "";
    }

    public interface IOpenAIService
    {
        Task<OpenAIIntegration.Model.CompletionResponse> GetCompletionAsync(
            OpenAIIntegration.Model.CompletionRequest request,
            CancellationToken cancellationToken = default);
    }

    // Dummy implementation used only by unit-tests
    public class SessionAwareOpenAIService
    {
        public SessionAwareOpenAIService(OpenAIConfig cfg,
                                         Common.Interfaces.Session.ISessionActivityLogger _)
        { /* no-op */ }
    }
}

namespace OpenAIIntegration.Model
{
    public class Message
    {
        public string Role    { get; set; } = "";
        public string Content { get; set; } = "";
    }

    public class Choice
    {
        public Message Message      { get; set; } = new();
        public string  FinishReason { get; set; } = "";
    }

    public class CompletionRequest
    {
        public string   Model    { get; set; } = "";
        public Message[] Messages { get; set; } = Array.Empty<Message>();
    }

    public class CompletionResponse
    {
        public string  Id     { get; set; } = "";
        public string  Model  { get; set; } = "";
        public Choice[] Choices { get; set; } = Array.Empty<Choice>();
    }
}
