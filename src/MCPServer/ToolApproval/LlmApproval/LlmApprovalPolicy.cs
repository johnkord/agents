using System;
using System.Collections.Generic;

namespace MCPServer.ToolApproval.LlmApproval;

/// <summary>
/// Configuration for LLM-based approval policies
/// </summary>
public class LlmApprovalPolicy
{
    /// <summary>
    /// Minimum confidence required for automatic approval (default: 0.85)
    /// </summary>
    public double AutoApprovalMinConfidence { get; set; } = 0.85;

    /// <summary>
    /// Maximum confidence for requiring human approval (default: 0.50)
    /// Below this threshold, human approval is required
    /// </summary>
    public double HumanRequiredMaxConfidence { get; set; } = 0.50;

    /// <summary>
    /// Tool-specific policies that override default behavior
    /// </summary>
    public Dictionary<string, ToolPolicy> ToolPolicies { get; set; } = new();

    /// <summary>
    /// Tools that always require human approval regardless of LLM decision
    /// </summary>
    public List<string> AlwaysRequireHuman { get; set; } = new();

    /// <summary>
    /// Tools that are never auto-approved, must be explicitly approved by human
    /// </summary>
    public List<string> NeverAutoApprove { get; set; } = new();

    /// <summary>
    /// Risk category-based policies
    /// </summary>
    public Dictionary<RiskCategory, RiskPolicy> RiskPolicies { get; set; } = new()
    {
        [RiskCategory.Safe] = new RiskPolicy { AllowAutoApproval = true, MinConfidence = 0.70 },
        [RiskCategory.Moderate] = new RiskPolicy { AllowAutoApproval = true, MinConfidence = 0.80 },
        [RiskCategory.High] = new RiskPolicy { AllowAutoApproval = true, MinConfidence = 0.90 },
        [RiskCategory.Critical] = new RiskPolicy { AllowAutoApproval = false, MinConfidence = 1.0 }
    };

    /// <summary>
    /// Enable caching of LLM decisions (default: true)
    /// </summary>
    public bool CacheEnabled { get; set; } = true;

    /// <summary>
    /// Cache time-to-live for decisions (default: 1 hour)
    /// </summary>
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Maximum time to wait for LLM response (default: 30 seconds)
    /// </summary>
    public TimeSpan LlmTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Fallback to human approval when LLM is unavailable (default: true)
    /// </summary>
    public bool FallbackToHuman { get; set; } = true;
}

/// <summary>
/// Policy configuration for a specific tool
/// </summary>
public class ToolPolicy
{
    /// <summary>
    /// Whether this tool can be auto-approved by LLM
    /// </summary>
    public bool AllowAutoApproval { get; set; } = true;

    /// <summary>
    /// Minimum confidence override for this specific tool
    /// </summary>
    public double? MinConfidenceOverride { get; set; }

    /// <summary>
    /// Argument-specific policies
    /// </summary>
    public List<ArgumentPolicy> ArgumentPolicies { get; set; } = new();

    /// <summary>
    /// Maximum number of calls per hour for this tool (-1 for unlimited)
    /// </summary>
    public int MaxCallsPerHour { get; set; } = -1;

    /// <summary>
    /// Custom risk category override for this tool
    /// </summary>
    public RiskCategory? RiskCategoryOverride { get; set; }
}

/// <summary>
/// Policy for specific tool arguments
/// </summary>
public class ArgumentPolicy
{
    /// <summary>
    /// Name of the argument this policy applies to
    /// </summary>
    public string ArgumentName { get; set; } = string.Empty;

    /// <summary>
    /// Patterns that are always denied
    /// </summary>
    public List<string> DeniedPatterns { get; set; } = new();

    /// <summary>
    /// Patterns that require human approval
    /// </summary>
    public List<string> HumanRequiredPatterns { get; set; } = new();

    /// <summary>
    /// Patterns that are considered safe for auto-approval
    /// </summary>
    public List<string> SafePatterns { get; set; } = new();

    /// <summary>
    /// Maximum length for this argument (-1 for unlimited)
    /// </summary>
    public int MaxLength { get; set; } = -1;

    /// <summary>
    /// Whether this argument can contain sensitive data
    /// </summary>
    public bool MayContainSensitiveData { get; set; } = false;
}

/// <summary>
/// Risk-based policy configuration
/// </summary>
public class RiskPolicy
{
    /// <summary>
    /// Whether tools in this risk category can be auto-approved
    /// </summary>
    public bool AllowAutoApproval { get; set; } = true;

    /// <summary>
    /// Minimum confidence required for this risk category
    /// </summary>
    public double MinConfidence { get; set; } = 0.85;

    /// <summary>
    /// Maximum number of auto-approvals per hour for this risk category
    /// </summary>
    public int MaxAutoApprovalsPerHour { get; set; } = -1;

    /// <summary>
    /// Additional human review required for this risk category
    /// </summary>
    public bool RequireAdditionalReview { get; set; } = false;
}

/// <summary>
/// Attribute to configure LLM approval policies for specific tools
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class LlmApprovalPolicyAttribute : Attribute
{
    /// <summary>
    /// Whether this tool can be auto-approved by LLM (default: true)
    /// </summary>
    public bool AllowAutoApproval { get; set; } = true;

    /// <summary>
    /// Minimum confidence required for auto-approval (overrides global setting)
    /// </summary>
    public double? MinConfidence { get; set; }

    /// <summary>
    /// Risk category for this tool
    /// </summary>
    public RiskCategory? RiskCategory { get; set; }

    /// <summary>
    /// Maximum calls per hour for this tool
    /// </summary>
    public int MaxCallsPerHour { get; set; } = -1;
}