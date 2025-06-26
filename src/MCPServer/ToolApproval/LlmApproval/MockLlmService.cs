using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MCPServer.ToolApproval.LlmApproval;

/// <summary>
/// Mock LLM service for testing and demonstration purposes
/// This service uses rule-based logic to simulate LLM decision making
/// </summary>
public class MockLlmService : ILlmService
{
    public string ServiceName => "Mock LLM";
    public string ModelName => "mock-gpt-4";

    private static readonly Dictionary<string, (RiskCategory Risk, double BaseConfidence)> ToolRiskMap = new Dictionary<string, (RiskCategory Risk, double BaseConfidence)>()
    {
        // Safe operations
        ["read_file"] = (RiskCategory.Safe, 0.90),
        ["get_current_time"] = (RiskCategory.Safe, 0.95),
        ["calculate"] = (RiskCategory.Safe, 0.95),
        ["list_files"] = (RiskCategory.Safe, 0.85),
        ["get_file_info"] = (RiskCategory.Safe, 0.90),
        
        // Moderate risk operations
        ["write_file"] = (RiskCategory.Moderate, 0.75),
        ["create_directory"] = (RiskCategory.Moderate, 0.80),
        ["copy_file"] = (RiskCategory.Moderate, 0.70),
        ["move_file"] = (RiskCategory.Moderate, 0.65),
        
        // High risk operations
        ["delete_file"] = (RiskCategory.High, 0.60),
        ["execute_command"] = (RiskCategory.High, 0.50),
        ["network_request"] = (RiskCategory.High, 0.55),
        ["install_package"] = (RiskCategory.High, 0.45),
        
        // Critical operations
        ["delete_database"] = (RiskCategory.Critical, 0.20),
        ["modify_security_settings"] = (RiskCategory.Critical, 0.15),
        ["execute_shell"] = (RiskCategory.Critical, 0.30),
        ["format_disk"] = (RiskCategory.Critical, 0.10)
    };

    private static readonly Dictionary<string, double> DangerousPatterns = new Dictionary<string, double>()
    {
        [@"\.exe$"] = -0.30,           // Executable files
        [@"\.bat$"] = -0.30,           // Batch files
        [@"\.sh$"] = -0.25,            // Shell scripts
        [@"\.ps1$"] = -0.25,           // PowerShell scripts
        [@"^/etc/"] = -0.40,           // System configuration
        [@"^C:\\Windows\\"] = -0.40,   // Windows system files
        [@"^C:\\Program Files\\"] = -0.35, // Program files
        [@"passwd"] = -0.50,           // Password files
        [@"shadow"] = -0.50,           // Shadow files
        [@"^sudo "] = -0.45,           // Sudo commands
        [@"rm -rf"] = -0.60,           // Dangerous rm commands
        [@"DROP TABLE"] = -0.70,       // SQL drop commands
        [@"DELETE FROM"] = -0.50,      // SQL delete commands
        [@"UPDATE.*SET"] = -0.30,      // SQL update commands
    };

    private static readonly Dictionary<string, double> SafePatterns = new Dictionary<string, double>()
    {
        [@"\.txt$"] = 0.20,            // Text files
        [@"\.json$"] = 0.25,           // JSON files
        [@"\.csv$"] = 0.20,            // CSV files
        [@"\.log$"] = 0.30,            // Log files
        [@"^/tmp/"] = 0.15,            // Temporary files
        [@"^/home/.*/(Documents|Downloads)"] = 0.25, // User directories
        [@"SELECT.*FROM"] = 0.30,      // SQL select commands
        [@"^ls "] = 0.25,              // List commands
        [@"^cat "] = 0.30,             // Cat commands
        [@"^head "] = 0.25,            // Head commands
        [@"^tail "] = 0.25,            // Tail commands
    };

    public virtual async Task<LlmApprovalDecision> EvaluateToolCallAsync(
        string toolName,
        IReadOnlyDictionary<string, object?> arguments,
        LlmApprovalContext context,
        CancellationToken cancellationToken = default)
    {
        // Simulate processing time
        await Task.Delay(TimeSpan.FromMilliseconds(100 + Random.Shared.Next(0, 400)), cancellationToken);

        var startTime = DateTime.UtcNow;

        // Get base risk assessment
        var (riskCategory, baseConfidence) = GetToolRiskAssessment(toolName);

        // Analyze arguments
        var argumentAnalysis = AnalyzeArguments(arguments);
        var adjustedConfidence = Math.Max(0.0, Math.Min(1.0, baseConfidence + argumentAnalysis.ConfidenceAdjustment));

        // Determine result based on confidence and risk
        var result = DetermineApprovalResult(adjustedConfidence, riskCategory, argumentAnalysis);

        // Generate reasoning
        var reasoning = GenerateReasoning(toolName, riskCategory, adjustedConfidence, argumentAnalysis);

        var processingTime = DateTime.UtcNow - startTime;

        return new LlmApprovalDecision(
            result,
            adjustedConfidence,
            reasoning,
            riskCategory,
            argumentAnalysis.Concerns,
            processingTime);
    }

    private (RiskCategory Risk, double BaseConfidence) GetToolRiskAssessment(string toolName)
    {
        if (ToolRiskMap.TryGetValue(toolName, out var assessment))
        {
            return assessment;
        }

        // Default assessment for unknown tools
        var riskCategory = toolName.ToLower() switch
        {
            var name when name.Contains("delete") || name.Contains("remove") => RiskCategory.High,
            var name when name.Contains("execute") || name.Contains("run") => RiskCategory.High,
            var name when name.Contains("write") || name.Contains("create") => RiskCategory.Moderate,
            var name when name.Contains("read") || name.Contains("get") || name.Contains("list") => RiskCategory.Safe,
            _ => RiskCategory.Moderate
        };

        var baseConfidence = riskCategory switch
        {
            RiskCategory.Safe => 0.80,
            RiskCategory.Moderate => 0.65,
            RiskCategory.High => 0.45,
            RiskCategory.Critical => 0.25,
            _ => 0.50
        };

        return (riskCategory, baseConfidence);
    }

    private ArgumentAnalysis AnalyzeArguments(IReadOnlyDictionary<string, object?> arguments)
    {
        var concerns = new List<string>();
        var confidenceAdjustment = 0.0;

        foreach (var arg in arguments)
        {
            var value = arg.Value?.ToString() ?? "";

            // Check for dangerous patterns
            foreach (var pattern in DangerousPatterns)
            {
                if (Regex.IsMatch(value, pattern.Key, RegexOptions.IgnoreCase))
                {
                    confidenceAdjustment += pattern.Value;
                    concerns.Add($"Dangerous pattern detected in {arg.Key}: {pattern.Key}");
                }
            }

            // Check for safe patterns
            foreach (var pattern in SafePatterns)
            {
                if (Regex.IsMatch(value, pattern.Key, RegexOptions.IgnoreCase))
                {
                    confidenceAdjustment += pattern.Value;
                    break; // Only apply one safe pattern bonus per argument
                }
            }

            // Check for other concerning patterns
            if (value.Length > 1000)
            {
                confidenceAdjustment -= 0.10;
                concerns.Add($"Very long argument {arg.Key} ({value.Length} chars)");
            }

            if (ContainsSensitiveData(value))
            {
                confidenceAdjustment -= 0.20;
                concerns.Add($"Potential sensitive data in {arg.Key}");
            }
        }

        return new ArgumentAnalysis(confidenceAdjustment, concerns);
    }

    private bool ContainsSensitiveData(string value)
    {
        var sensitivePatterns = new[]
        {
            @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", // Email addresses
            @"\b\d{3}-\d{2}-\d{4}\b",                                 // SSN format
            @"\b\d{4}[- ]?\d{4}[- ]?\d{4}[- ]?\d{4}\b",               // Credit card format
            @"password|passwd|secret|key|token",                       // Common secret keywords
            @"bearer [a-zA-Z0-9_-]+",                                  // Bearer tokens
            @"api[_-]?key",                                            // API keys
        };

        return sensitivePatterns.Any(pattern => 
            Regex.IsMatch(value, pattern, RegexOptions.IgnoreCase));
    }

    private ApprovalResult DetermineApprovalResult(double confidence, RiskCategory riskCategory, ArgumentAnalysis analysis)
    {
        // Critical operations should generally require human approval (prioritize over low confidence denial)
        if (riskCategory == RiskCategory.Critical)
        {
            // Only deny critical operations if they have specific concerns AND very low confidence
            if (analysis.Concerns.Count > 0 && confidence < 0.20)
            {
                return ApprovalResult.Deny;
            }
            return ApprovalResult.RequireHuman;
        }

        // Very low confidence should be denied (but not for critical operations)
        if (confidence < 0.30)
        {
            return ApprovalResult.Deny;
        }

        // Low confidence should require human approval
        if (confidence < 0.50)
        {
            return ApprovalResult.RequireHuman;
        }

        // High-risk operations with low-medium confidence should require human approval
        if (riskCategory >= RiskCategory.High && confidence < 0.70)
        {
            return ApprovalResult.RequireHuman;
        }

        // Operations with many concerns should require human approval
        if (analysis.Concerns.Count >= 3)
        {
            return ApprovalResult.RequireHuman;
        }

        // Otherwise, approve
        return ApprovalResult.Approve;
    }

    private string GenerateReasoning(string toolName, RiskCategory riskCategory, double confidence, ArgumentAnalysis analysis)
    {
        var reasoning = $"Tool '{toolName}' assessed as {riskCategory} risk with {confidence:F2} confidence. ";

        if (analysis.Concerns.Count == 0)
        {
            reasoning += "No specific concerns identified. ";
        }
        else
        {
            reasoning += $"{analysis.Concerns.Count} concern(s) identified: {string.Join(", ", analysis.Concerns.Take(3))}. ";
        }

        reasoning += riskCategory switch
        {
            RiskCategory.Safe => "Safe operation that can typically be auto-approved.",
            RiskCategory.Moderate => "Moderate risk operation that requires careful consideration.",
            RiskCategory.High => "High risk operation that may require human oversight.",
            RiskCategory.Critical => "Critical operation that should have human approval.",
            _ => "Unknown risk level."
        };

        return reasoning;
    }

    private record ArgumentAnalysis(double ConfidenceAdjustment, List<string> Concerns);
}