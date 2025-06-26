using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MCPServer.ToolApproval.LlmApproval;

/// <summary>
/// Approval provider that uses LLM intelligence to make approval decisions
/// </summary>
public class LlmApprovalProvider : IApprovalProvider
{
    private readonly ILlmService _llmService;
    private readonly LlmApprovalPolicy _policy;
    private readonly ILlmDecisionCache _decisionCache;
    private readonly IApprovalProvider _fallbackProvider;
    private readonly ILogger<LlmApprovalProvider> _logger;

    public string ProviderName => $"LLM ({_llmService.ServiceName})";

    public LlmApprovalProvider(
        ILlmService llmService, 
        LlmApprovalPolicy policy, 
        ILlmDecisionCache? decisionCache = null,
        IApprovalProvider? fallbackProvider = null,
        ILogger<LlmApprovalProvider>? logger = null)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _decisionCache = decisionCache ?? new InMemoryLlmDecisionCache();
        _fallbackProvider = fallbackProvider ?? new ConsoleApprovalProvider();
        _logger = logger ?? NullLogger<LlmApprovalProvider>.Instance;
    }

    public async Task<bool> RequestApprovalAsync(ApprovalInvocationToken token, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing approval request for tool '{ToolName}' with LLM provider", token.ToolName);

            // 1. Check policy for always-require-human tools
            if (_policy.AlwaysRequireHuman.Contains(token.ToolName))
            {
                _logger.LogInformation("Tool '{ToolName}' is configured to always require human approval", token.ToolName);
                return await _fallbackProvider.RequestApprovalAsync(token, cancellationToken);
            }

            // 2. Check policy for never-auto-approve tools
            if (_policy.NeverAutoApprove.Contains(token.ToolName))
            {
                _logger.LogInformation("Tool '{ToolName}' is configured to never auto-approve", token.ToolName);
                return await _fallbackProvider.RequestApprovalAsync(token, cancellationToken);
            }

            // 3. Check cache for previous decisions
            if (_policy.CacheEnabled)
            {
                var cacheKey = GenerateCacheKey(token);
                var cachedDecision = await _decisionCache.GetDecisionAsync(cacheKey, cancellationToken);
                
                if (cachedDecision != null)
                {
                    _logger.LogInformation("Using cached decision for tool '{ToolName}': {Result}", 
                        token.ToolName, cachedDecision.Result);
                    return await ProcessDecisionResult(cachedDecision, token, cancellationToken);
                }
            }

            // 4. Get LLM decision
            var context = new LlmApprovalContext(
                Timestamp: token.CreatedAt,
                AdditionalContext: new Dictionary<string, object?>
                {
                    ["token_id"] = token.Id.ToString(),
                    ["created_at"] = token.CreatedAt.ToString("O")
                });

            var llmDecision = await GetLlmDecisionWithTimeout(token, context, cancellationToken);

            // 5. Apply policy-based decision logic
            var finalDecision = ApplyPolicyToDecision(llmDecision, token);

            // 6. Cache the decision
            if (_policy.CacheEnabled && llmDecision != null)
            {
                var cacheKey = GenerateCacheKey(token);
                await _decisionCache.CacheDecisionAsync(cacheKey, finalDecision, _policy.CacheTtl, cancellationToken);
            }

            // 7. Log the decision
            LogDecision(token, finalDecision);

            // 8. Process the result
            return await ProcessDecisionResult(finalDecision, token, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing LLM approval for tool '{ToolName}'. Falling back to human approval.", token.ToolName);
            
            if (_policy.FallbackToHuman)
            {
                return await _fallbackProvider.RequestApprovalAsync(token, cancellationToken);
            }
            
            return false; // Fail safe - deny if no fallback
        }
    }

    private async Task<LlmApprovalDecision?> GetLlmDecisionWithTimeout(
        ApprovalInvocationToken token, 
        LlmApprovalContext context, 
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = new CancellationTokenSource(_policy.LlmTimeout);
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            return await _llmService.EvaluateToolCallAsync(token.ToolName, token.Arguments, context, combinedCts.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("LLM approval request was cancelled for tool '{ToolName}'", token.ToolName);
            return null;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("LLM approval request timed out for tool '{ToolName}' after {Timeout}", 
                token.ToolName, _policy.LlmTimeout);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM service failed for tool '{ToolName}'", token.ToolName);
            return null;
        }
    }

    private LlmApprovalDecision ApplyPolicyToDecision(LlmApprovalDecision? llmDecision, ApprovalInvocationToken token)
    {
        // If LLM failed, create a decision that requires human approval
        if (llmDecision == null)
        {
            return new LlmApprovalDecision(
                ApprovalResult.RequireHuman,
                0.0,
                "LLM service unavailable or timed out",
                RiskCategory.High,
                new[] { "LLM service unavailable" },
                TimeSpan.Zero);
        }

        // Apply tool-specific policies
        if (_policy.ToolPolicies.TryGetValue(token.ToolName, out var toolPolicy))
        {
            if (!toolPolicy.AllowAutoApproval && llmDecision.Result == ApprovalResult.Approve)
            {
                return llmDecision with 
                { 
                    Result = ApprovalResult.RequireHuman,
                    Reasoning = $"Tool policy prevents auto-approval. Original: {llmDecision.Reasoning}"
                };
            }

            // Apply tool-specific confidence threshold
            if (toolPolicy.MinConfidenceOverride.HasValue)
            {
                var minConfidence = toolPolicy.MinConfidenceOverride.Value;
                if (llmDecision.Result == ApprovalResult.Approve && llmDecision.Confidence < minConfidence)
                {
                    return llmDecision with 
                    { 
                        Result = ApprovalResult.RequireHuman,
                        Reasoning = $"Confidence {llmDecision.Confidence:F2} below tool threshold {minConfidence:F2}. {llmDecision.Reasoning}"
                    };
                }
            }
        }

        // Apply risk-based policies
        if (_policy.RiskPolicies.TryGetValue(llmDecision.RiskCategory, out var riskPolicy))
        {
            if (!riskPolicy.AllowAutoApproval && llmDecision.Result == ApprovalResult.Approve)
            {
                return llmDecision with 
                { 
                    Result = ApprovalResult.RequireHuman,
                    Reasoning = $"Risk policy prevents auto-approval for {llmDecision.RiskCategory} risk. {llmDecision.Reasoning}"
                };
            }

            if (llmDecision.Result == ApprovalResult.Approve && llmDecision.Confidence < riskPolicy.MinConfidence)
            {
                return llmDecision with 
                { 
                    Result = ApprovalResult.RequireHuman,
                    Reasoning = $"Confidence {llmDecision.Confidence:F2} below risk threshold {riskPolicy.MinConfidence:F2}. {llmDecision.Reasoning}"
                };
            }
        }

        // Apply global confidence thresholds
        if (llmDecision.Result == ApprovalResult.Approve && llmDecision.Confidence < _policy.AutoApprovalMinConfidence)
        {
            return llmDecision with 
            { 
                Result = ApprovalResult.RequireHuman,
                Reasoning = $"Confidence {llmDecision.Confidence:F2} below auto-approval threshold {_policy.AutoApprovalMinConfidence:F2}. {llmDecision.Reasoning}"
            };
        }

        if (llmDecision.Result == ApprovalResult.RequireHuman && llmDecision.Confidence <= _policy.HumanRequiredMaxConfidence)
        {
            // Very low confidence might warrant denial
            return llmDecision with 
            { 
                Result = ApprovalResult.Deny,
                Reasoning = $"Very low confidence {llmDecision.Confidence:F2} suggests denial. {llmDecision.Reasoning}"
            };
        }

        return llmDecision;
    }

    private async Task<bool> ProcessDecisionResult(
        LlmApprovalDecision decision, 
        ApprovalInvocationToken token, 
        CancellationToken cancellationToken)
    {
        return decision.Result switch
        {
            ApprovalResult.Approve => true,
            ApprovalResult.Deny => false,
            ApprovalResult.RequireHuman => await _fallbackProvider.RequestApprovalAsync(token, cancellationToken),
            _ => false
        };
    }

    private string GenerateCacheKey(ApprovalInvocationToken token)
    {
        var keyData = new { token.ToolName, token.Arguments };
        var json = JsonSerializer.Serialize(keyData);
        return $"llm_approval:{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json))}";
    }

    private void LogDecision(ApprovalInvocationToken token, LlmApprovalDecision decision)
    {
        _logger.LogInformation(
            "LLM Decision for '{ToolName}': {Result} (Confidence: {Confidence:F2}, Risk: {Risk}) - {Reasoning}",
            token.ToolName,
            decision.Result,
            decision.Confidence,
            decision.RiskCategory,
            decision.Reasoning);

        if (decision.Concerns.Count > 0)
        {
            _logger.LogInformation("Concerns identified: {Concerns}", string.Join(", ", decision.Concerns));
        }
    }
}