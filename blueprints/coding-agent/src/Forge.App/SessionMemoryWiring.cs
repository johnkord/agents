using Forge.Core;
using Forge.Core.SessionMemory;
using Microsoft.Extensions.AI;
using OpenAI.Responses;
using Serilog;

namespace Forge.App;

/// <summary>
/// Wires the default OpenAI-Responses-backed session-memory extractor (P2.1).
/// Lives in Forge.App so <c>Forge.Core</c> remains provider-agnostic.
/// </summary>
#pragma warning disable OPENAI001 // Responses API is in preview
internal static class SessionMemoryWiring
{
    public static SessionMemoryExtractor Build(
        ResponsesClient responsesClient,
        string model,
        AgentOptions options,
        ILogger logger)
    {
        return async (request, ct) =>
        {
            // Spin up a throwaway LLM client with NO tools for this extraction.
            // A fresh client = fresh conversation state = no cross-contamination
            // with the main agent loop's history.
            IReadOnlyList<AITool> noTools = [];
            await using var client = new OpenAIResponsesLlmClient(
                responsesClient, model, options, noTools, logger);

            client.AddSystemMessage(SessionMemoryPrompts.SystemPrompt);
            client.AddUserMessage(SessionMemoryPrompts.BuildUserPrompt(request));

            var response = await client.GetStreamingResponseAsync(onToken: null, cancellationToken: ct);

            var raw = response.Text ?? string.Empty;
            var lastStep = request.Steps.Count > 0 ? request.Steps[^1].StepNumber : (request.FromStepIndex ?? 0);
            var previousCount = request.PreviousSummary?.ExtractionCount ?? 0;

            var snapshot = SessionMemoryPrompts.ParseSnapshot(
                raw,
                lastExtractedStep: lastStep,
                extractionCount: previousCount + 1,
                updatedAt: DateTimeOffset.UtcNow);

            return new SessionMemoryExtractionResult
            {
                Snapshot = snapshot,
                PromptTokens = response.PromptTokens,
                CompletionTokens = response.CompletionTokens,
                RawResponse = raw,
            };
        };
    }
}
#pragma warning restore OPENAI001

