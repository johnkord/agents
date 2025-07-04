using Xunit;
using AgentAlpha.Services;
using AgentAlpha.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAIIntegration.Model;
using System.Threading.Tasks;
using Common.Interfaces.Session;
using OpenAIIntegration;
using System.Threading;
using System.Text.Json;

namespace AgentAlpha.Tests.Services;

internal sealed class FakeOpenAIService : ISessionAwareOpenAIService
{
    private readonly string _content;
    public FakeOpenAIService(string content) => _content = content;

    private OutputMessage CreateOutput()
    {
        // Try to parse the content as JSON object/array first; fall back to JSON string
        JsonElement elem;
        try
        {
            elem = JsonDocument.Parse(_content).RootElement.Clone();
        }
        catch
        {
            elem = JsonDocument.Parse(JsonSerializer.Serialize(_content)).RootElement.Clone();
        }

        return new OutputMessage
        {
            Role    = "assistant",
            Content = elem
        };
    }

    // Original helper (session-aware version)
    public Task<ResponsesCreateResponse> CreateResponseAsync(ResponsesCreateRequest _, string? __ = null)
        => Task.FromResult(new ResponsesCreateResponse
        {
            Output = new ResponseOutputItem[] { CreateOutput() }
        });

    // IOpenAIResponsesService overload (with CancellationToken)
    public Task<ResponsesCreateResponse> CreateResponseAsync(ResponsesCreateRequest req, CancellationToken _)
        => CreateResponseAsync(req, (string?)null);

    // Session activity logger hook – no-op for tests
    public void SetActivityLogger(ISessionActivityLogger? _) { }
}

public class PlanEvaluatorTests
{
    [Fact]
    public async Task EvaluateAsync_ParsesValidJson()
    {
        // Arrange
        var json = """{ "score": 0.9, "feedback": "Looks good" }""";
        var svc  = new PlanEvaluator(new FakeOpenAIService(json), NullLogger<PlanEvaluator>.Instance);

        // Act
        var res = await svc.EvaluateAsync("plan", "task");

        // Assert
        Assert.Equal(0.9, res.Score, 3);
        Assert.Equal("Looks good", res.Feedback);
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsDefaultOnMalformedJson()
    {
        // Arrange
        var svc = new PlanEvaluator(new FakeOpenAIService("not json"), NullLogger<PlanEvaluator>.Instance);

        // Act
        var res = await svc.EvaluateAsync("plan", "task");

        // Assert
        Assert.Equal(0.0, res.Score);
        Assert.Contains("error", res.Feedback, StringComparison.OrdinalIgnoreCase);
    }
}
