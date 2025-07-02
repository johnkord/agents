using Common.Interfaces.Session;
using Common.Models.Session;
using OpenAIIntegration.Model;
using OpenAIIntegration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text;

namespace Common.Services.Session;

/// <summary>
/// Implementation of session summary generation using LLM analysis
/// </summary>
public class SessionSummaryService : ISessionSummaryService
{
    private readonly ISessionManager _sessionManager;
    private readonly ISessionAwareOpenAIService _openAiService;
    private readonly ILogger<SessionSummaryService> _logger;

    public SessionSummaryService(
        ISessionManager sessionManager,
        ISessionAwareOpenAIService openAiService,
        ILogger<SessionSummaryService> logger)
    {
        _sessionManager = sessionManager;
        _openAiService = openAiService;
        _logger = logger;
    }

    public async Task<SessionSummary> GenerateSessionSummaryAsync(string sessionId, SummaryOptions? options = null)
    {
        options ??= new SummaryOptions();
        _logger.LogInformation("Generating session summary for session {SessionId}", sessionId);

        var startTime = DateTime.UtcNow;
        try
        {
            // Get session and activities
            var session = await _sessionManager.GetSessionAsync(sessionId);
            if (session == null)
            {
                throw new ArgumentException($"Session {sessionId} not found", nameof(sessionId));
            }

            var activities = await _sessionManager.GetSessionActivitiesAsync(sessionId);
            
            // Generate summary using LLM
            var summary = await GenerateSummaryFromActivitiesAsync(session, activities, options);
            
            // Log the summary generation activity
            await LogSummaryActivityAsync(sessionId, summary, DateTime.UtcNow.Subtract(startTime));
            
            _logger.LogInformation("Successfully generated session summary for session {SessionId}", sessionId);
            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate session summary for session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<SessionSummary> GeneratePartialSummaryAsync(string sessionId, DateTime fromTime, DateTime toTime, SummaryOptions? options = null)
    {
        options ??= new SummaryOptions();
        _logger.LogInformation("Generating partial session summary for session {SessionId} from {FromTime} to {ToTime}", 
            sessionId, fromTime, toTime);

        // Get session and filtered activities
        var session = await _sessionManager.GetSessionAsync(sessionId);
        if (session == null)
        {
            throw new ArgumentException($"Session {sessionId} not found", nameof(sessionId));
        }

        var allActivities = await _sessionManager.GetSessionActivitiesAsync(sessionId);
        var filteredActivities = allActivities
            .Where(a => a.Timestamp >= fromTime && a.Timestamp <= toTime)
            .ToList();

        return await GenerateSummaryFromActivitiesAsync(session, filteredActivities, options, isPartial: true);
    }

    public async Task<SessionSummary> GenerateFinalSummaryAsync(string sessionId, string? userQuestions = null, SummaryOptions? options = null)
    {
        options ??= new SummaryOptions();
        _logger.LogInformation("Generating final session summary for session {SessionId}", sessionId);

        // Generate comprehensive summary with focus on completion
        options.FocusAreas = new[] { SummaryFocus.TaskCompletion, SummaryFocus.UserQuestions };
        
        var summary = await GenerateSessionSummaryAsync(sessionId, options);
        
        // If user questions provided, enhance the summary
        if (!string.IsNullOrEmpty(userQuestions))
        {
            summary = await EnhanceSummaryWithUserQuestionsAsync(summary, userQuestions);
        }

        return summary;
    }

    private async Task<SessionSummary> GenerateSummaryFromActivitiesAsync(
        AgentSession session, 
        List<SessionActivity> activities, 
        SummaryOptions options,
        bool isPartial = false)
    {
        // Analyze activities
        var statistics = AnalyzeActivities(activities);
        var errors = ExtractErrors(activities);
        var taskCompletion = AnalyzeTaskCompletion(session, activities);

        // Build context for LLM
        var context = BuildLLMContext(session, activities, options);
        
        // Generate markdown summary using LLM
        var markdownSummary = await GenerateMarkdownSummaryAsync(context, options);
        
        // Extract executive summary from markdown
        var executiveSummary = ExtractExecutiveSummary(markdownSummary);

        return new SessionSummary
        {
            SessionId = session.SessionId,
            SessionName = session.Name,
            SessionStartTime = session.CreatedAt,
            SessionEndTime = isPartial ? null : GetSessionEndTime(activities),
            Status = session.Status,
            MarkdownSummary = markdownSummary,
            ExecutiveSummary = executiveSummary,
            TaskCompletion = taskCompletion,
            Statistics = statistics,
            Errors = errors,
            SummaryOptionsSnapshot = JsonSerializer.Serialize(options)
        };
    }

    private SessionStatistics AnalyzeActivities(List<SessionActivity> activities)
    {
        var toolCalls = activities.Where(a => a.ActivityType == ActivityTypes.ToolCall).ToList();
        var successfulToolCalls = toolCalls.Where(a => a.Success).ToList();
        var openAiRequests = activities.Where(a => a.ActivityType == ActivityTypes.OpenAIRequest).ToList();
        var errors = activities.Where(a => !a.Success || a.ActivityType == ActivityTypes.Error).ToList();

        var toolUsage = toolCalls
            .GroupBy(a => ExtractToolNameFromActivity(a))
            .Where(g => !string.IsNullOrEmpty(g.Key))
            .ToDictionary(g => g.Key!, g => g.Count());

        return new SessionStatistics
        {
            TotalActivities = activities.Count,
            ToolCalls = toolCalls.Count,
            SuccessfulToolCalls = successfulToolCalls.Count,
            OpenAIRequests = openAiRequests.Count,
            ErrorCount = errors.Count,
            EstimatedTokens = EstimateTokenUsage(activities),
            ToolUsage = toolUsage
        };
    }

    private List<ErrorSummary> ExtractErrors(List<SessionActivity> activities)
    {
        return activities
            .Where(a => !a.Success || a.ActivityType == ActivityTypes.Error)
            .Select(a => new ErrorSummary
            {
                Timestamp = a.Timestamp,
                ErrorType = DetermineErrorType(a),
                Description = a.ErrorMessage ?? a.Description,
                RootCause = AnalyzeRootCause(a),
                Resolution = DetermineResolution(a),
                Impact = AssessErrorImpact(a)
            })
            .ToList();
    }

    private TaskCompletionAssessment AnalyzeTaskCompletion(AgentSession session, List<SessionActivity> activities)
    {
        var completedSubtasks = new List<string>();
        var failedSubtasks = new List<string>();
        var evidence = new List<string>();

        // Analyze task markdown if available
        if (!string.IsNullOrEmpty(session.TaskStateMarkdown))
        {
            (completedSubtasks, failedSubtasks) = ParseTaskMarkdown(session.TaskStateMarkdown);
        }

        // Look for completion evidence in activities
        evidence.AddRange(ExtractCompletionEvidence(activities));

        var completionPercentage = CalculateCompletionPercentage(completedSubtasks, failedSubtasks, activities);
        var taskCompleted = completionPercentage >= 90; // Consider 90%+ as completed

        return new TaskCompletionAssessment
        {
            TaskCompleted = taskCompleted,
            CompletionPercentage = completionPercentage,
            Evidence = evidence,
            CompletedSubtasks = completedSubtasks,
            FailedSubtasks = failedSubtasks
        };
    }

    private string BuildLLMContext(AgentSession session, List<SessionActivity> activities, SummaryOptions options)
    {
        var context = new StringBuilder();
        
        context.AppendLine($"Session Analysis Request");
        context.AppendLine($"Session ID: {session.SessionId}");
        context.AppendLine($"Session Name: {session.Name}");
        context.AppendLine($"Session Duration: {session.CreatedAt:yyyy-MM-dd HH:mm:ss} - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
        context.AppendLine($"Total Activities: {activities.Count}");
        context.AppendLine();
        
        // Add task markdown if available
        if (!string.IsNullOrEmpty(session.TaskStateMarkdown))
        {
            context.AppendLine("Task State Markdown:");
            context.AppendLine("```markdown");
            context.AppendLine(session.TaskStateMarkdown);
            context.AppendLine("```");
            context.AppendLine();
        }

        // Add activity summary
        context.AppendLine("Activity Summary:");
        foreach (var activity in activities.Take(options.IncludeDetailedLogs ? int.MaxValue : 50))
        {
            var description = activity.Description.Length > options.MaxActivityDescriptionLength 
                ? activity.Description.Substring(0, options.MaxActivityDescriptionLength) + "..."
                : activity.Description;
                
            context.AppendLine($"- [{activity.Timestamp:HH:mm:ss}] {activity.ActivityType}: {description}");
            if (!activity.Success && !string.IsNullOrEmpty(activity.ErrorMessage))
            {
                context.AppendLine($"  ERROR: {activity.ErrorMessage}");
            }
        }

        return context.ToString();
    }

    private async Task<string> GenerateMarkdownSummaryAsync(string context, SummaryOptions options)
    {
        var focusAreasText = string.Join(", ", options.FocusAreas);
        
        var prompt = $"""
            You are a session analysis expert. Create a comprehensive markdown summary of this agent session.
            
            Focus Areas: {focusAreasText}
            Include Performance Analysis: {options.IncludePerformanceAnalysis}
            
            Session Data:
            {context}
            
            Generate a complete markdown document following this structure:
            
            # Session Summary: [Session Name]
            
            **Session ID:** [SessionId]
            **Duration:** [Duration]
            **Status:** [Status]
            
            ## Executive Summary
            [High-level overview of accomplishments]
            
            ## Task Analysis
            ### Primary Task
            **Description:** [Task description]
            **Status:** [Completion status]
            **Evidence:** [Supporting evidence]
            
            ### Subtasks Completed
            [List of completed subtasks with evidence]
            
            ## Actions Taken
            ### Tool Executions
            [Summary table of tools used]
            
            ### Key Activities
            [Most important activities with results]
            
            ## Error Analysis
            [Analysis of any errors encountered]
            
            ## Session Statistics
            [Performance metrics and usage statistics]
            
            ## Completion Assessment
            [Final assessment of task accomplishment with evidence]
            
            Provide specific evidence for all claims and focus on demonstrating task completion.
            """;

        var request = new ResponsesCreateRequest
        {
            Model = "gpt-4",
            Input = prompt,
            MaxOutputTokens = 4000
        };

        var response = await _openAiService.CreateResponseAsync(request);
        return ExtractTextFromResponse(response) ?? "Error generating summary";
    }

    private async Task<SessionSummary> EnhanceSummaryWithUserQuestionsAsync(SessionSummary summary, string userQuestions)
    {
        var prompt = $"""
            Enhance the following session summary by adding a detailed "User Questions & Responses" section.
            
            User Questions:
            {userQuestions}
            
            Current Summary:
            {summary.MarkdownSummary}
            
            Add a section that directly answers each user question with:
            - Clear, direct answers
            - Supporting evidence from the session activities
            - References to specific activities or timestamps
            
            Return the complete enhanced markdown summary.
            """;

        var request = new ResponsesCreateRequest
        {
            Model = "gpt-4",
            Input = prompt,
            MaxOutputTokens = 5000
        };

        var response = await _openAiService.CreateResponseAsync(request);
        var enhancedMarkdown = ExtractTextFromResponse(response) ?? summary.MarkdownSummary;
        
        summary.MarkdownSummary = enhancedMarkdown;
        return summary;
    }

    private async Task LogSummaryActivityAsync(string sessionId, SessionSummary summary, TimeSpan duration)
    {
        var activity = SessionActivity.Create(
            ActivityTypes.SessionSummary,
            $"Generated session summary with {summary.Statistics.TotalActivities} activities analyzed",
            new
            {
                SummaryLength = summary.MarkdownSummary.Length,
                CompletionPercentage = summary.TaskCompletion.CompletionPercentage,
                ErrorCount = summary.Statistics.ErrorCount,
                ToolCalls = summary.Statistics.ToolCalls
            }
        );
        
        activity.SessionId = sessionId;
        activity.DurationMs = (long)duration.TotalMilliseconds;
        
        await _sessionManager.AddSessionActivityAsync(sessionId, activity);
    }

    // Helper methods
    private string ExtractToolNameFromActivity(SessionActivity activity)
    {
        try
        {
            if (string.IsNullOrEmpty(activity.Data)) return string.Empty;
            
            using var doc = JsonDocument.Parse(activity.Data);
            if (doc.RootElement.TryGetProperty("ToolName", out var toolNameElement))
            {
                return toolNameElement.GetString() ?? string.Empty;
            }
        }
        catch { /* Ignore parsing errors */ }
        
        return string.Empty;
    }

    private int EstimateTokenUsage(List<SessionActivity> activities)
    {
        // Rough estimation: 4 characters per token
        var totalCharacters = activities.Sum(a => a.Description.Length + (a.Data?.Length ?? 0));
        return totalCharacters / 4;
    }

    private string DetermineErrorType(SessionActivity activity)
    {
        if (activity.ActivityType == ActivityTypes.ToolCall && !activity.Success)
            return "Tool Execution Error";
        if (activity.ActivityType == ActivityTypes.OpenAIRequest && !activity.Success)
            return "OpenAI API Error";
        return "General Error";
    }

    private string AnalyzeRootCause(SessionActivity activity)
    {
        return activity.ErrorMessage ?? "Unknown cause";
    }

    private string DetermineResolution(SessionActivity activity)
    {
        return activity.Success ? "Resolved" : "Unresolved";
    }

    private ErrorImpact AssessErrorImpact(SessionActivity activity)
    {
        if (activity.ActivityType == ActivityTypes.ToolCall && !activity.Success)
            return ErrorImpact.Medium;
        return ErrorImpact.Low;
    }

    private DateTime? GetSessionEndTime(List<SessionActivity> activities)
    {
        return activities.Any() ? activities.Max(a => a.Timestamp) : null;
    }

    private (List<string> completed, List<string> failed) ParseTaskMarkdown(string markdown)
    {
        var completed = new List<string>();
        var failed = new List<string>();
        
        var lines = markdown.Split('\n');
        foreach (var line in lines)
        {
            if (line.Trim().StartsWith("- [x]"))
            {
                completed.Add(line.Trim().Substring(5).Trim());
            }
            else if (line.Trim().StartsWith("- [ ]"))
            {
                failed.Add(line.Trim().Substring(5).Trim());
            }
        }
        
        return (completed, failed);
    }

    private List<string> ExtractCompletionEvidence(List<SessionActivity> activities)
    {
        return activities
            .Where(a => a.Success && a.ActivityType == ActivityTypes.TaskComplete)
            .Select(a => a.Description)
            .ToList();
    }

    private int CalculateCompletionPercentage(List<string> completed, List<string> failed, List<SessionActivity> activities)
    {
        var totalTasks = completed.Count + failed.Count;
        if (totalTasks == 0) return 100; // No specific tasks defined
        
        return (int)((double)completed.Count / totalTasks * 100);
    }

    private string ExtractExecutiveSummary(string markdown)
    {
        var lines = markdown.Split('\n');
        var inExecutiveSection = false;
        var summary = new StringBuilder();
        
        foreach (var line in lines)
        {
            if (line.StartsWith("## Executive Summary"))
            {
                inExecutiveSection = true;
                continue;
            }
            
            if (inExecutiveSection && line.StartsWith("##"))
            {
                break; // Next section started
            }
            
            if (inExecutiveSection && !string.IsNullOrWhiteSpace(line))
            {
                summary.AppendLine(line);
            }
        }
        
        return summary.ToString().Trim();
    }

    private string? ExtractTextFromResponse(ResponsesCreateResponse response)
    {
        var outputMessage = response.Output?.OfType<OutputMessage>().FirstOrDefault();
        if (outputMessage?.Content?.ValueKind == JsonValueKind.String)
        {
            return outputMessage.Content.Value.GetString();
        }
        return null;
    }
}