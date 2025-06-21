namespace LifeAgent.Core;

/// <summary>
/// Abstraction over LLM chat completion. Replaces raw Func delegates
/// with a testable, DI-friendly interface. Supports tiered model routing.
/// </summary>
public interface IChatCompletionService
{
    /// <summary>
    /// Sends a system + user message pair to the configured LLM and returns the text response.
    /// </summary>
    Task<string> CompleteAsync(
        string systemPrompt,
        string userMessage,
        ModelTier tier = ModelTier.Fast,
        float temperature = 0.3f,
        int maxTokens = 1000,
        CancellationToken ct = default);

    /// <summary>
    /// Sends a structured JSON-mode request. The LLM is instructed to return valid JSON.
    /// </summary>
    Task<T?> CompleteJsonAsync<T>(
        string systemPrompt,
        string userMessage,
        ModelTier tier = ModelTier.Fast,
        CancellationToken ct = default) where T : class;
}

/// <summary>
/// Tiered model routing: cheap models for triage, expensive for reasoning.
/// Maps to concrete model names in configuration.
/// </summary>
public enum ModelTier
{
    /// <summary>Fast and cheap — task classification, notification generation, simple extraction.</summary>
    Fast,
    /// <summary>Capable — task decomposition, proactive opportunity evaluation, structuring.</summary>
    Standard,
    /// <summary>Best available — research synthesis, complex analysis, multi-step reasoning.</summary>
    Deep,
}
