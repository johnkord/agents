using Common.Models.Session;

namespace Common.Interfaces.Session;

/// <summary>
/// Interface for generating comprehensive session summaries
/// </summary>
public interface ISessionSummaryService
{
    /// <summary>
    /// Generate a comprehensive session summary
    /// </summary>
    Task<SessionSummary> GenerateSessionSummaryAsync(string sessionId, SummaryOptions? options = null);
    
    /// <summary>
    /// Generate summary for specific time range
    /// </summary>
    Task<SessionSummary> GeneratePartialSummaryAsync(string sessionId, DateTime fromTime, DateTime toTime, SummaryOptions? options = null);
    
    /// <summary>
    /// Generate final session completion summary with user questions
    /// </summary>
    Task<SessionSummary> GenerateFinalSummaryAsync(string sessionId, string? userQuestions = null, SummaryOptions? options = null);
}

/// <summary>
/// Options for controlling session summary generation
/// </summary>
public class SummaryOptions
{
    /// <summary>
    /// Include detailed activity logs in summary
    /// </summary>
    public bool IncludeDetailedLogs { get; set; } = true;
    
    /// <summary>
    /// Include raw activity data section
    /// </summary>
    public bool IncludeRawData { get; set; } = false;
    
    /// <summary>
    /// Maximum length for activity descriptions
    /// </summary>
    public int MaxActivityDescriptionLength { get; set; } = 500;
    
    /// <summary>
    /// Focus areas for analysis
    /// </summary>
    public SummaryFocus[] FocusAreas { get; set; } = { SummaryFocus.TaskCompletion };
    
    /// <summary>
    /// Include performance analysis
    /// </summary>
    public bool IncludePerformanceAnalysis { get; set; } = true;
}

/// <summary>
/// Focus areas for summary analysis
/// </summary>
public enum SummaryFocus
{
    TaskCompletion,
    ErrorAnalysis,
    PerformanceMetrics,
    UserQuestions,
    ToolUsage
}