# Session Summary Stage Design

## Overview

This document describes the design for a final LLM/conversation stage that summarizes the actions taken throughout an agent session. This stage provides comprehensive session analysis, task completion evidence, error analysis, and user question responses in a clear markdown format.

## Problem Statement

Agent sessions generate extensive activity logs including tool calls, responses, task state changes, and errors. Users need:

- **Comprehensive Session Summary**: Clear overview of what was accomplished
- **Task Completion Evidence**: Proof that requested tasks were successfully completed
- **Error Analysis**: Summary of encountered errors for troubleshooting
- **Question Responses**: Answers to user questions with supporting evidence
- **Markdown Presentation**: Human-readable format suitable for UI display

The current system tracks individual activities but lacks a holistic view that demonstrates task accomplishment and provides actionable insights.

## Solution Architecture

### Core Components

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Session       │    │  Session Summary │    │  LLM Analysis   │
│   Activities    │───▶│  Service         │───▶│  Engine         │
│   Repository    │    │                  │    │  (OpenAI)       │
└─────────────────┘    └──────────────────┘    └─────────────────┘
                                │                        │
                                ▼                        ▼
                       ┌─────────────────┐    ┌─────────────────┐
                       │  Activity       │    │  Markdown       │
                       │  Analyzer       │    │  Summary        │
                       │                 │    │  Generator      │
                       └─────────────────┘    └─────────────────┘
                                │                        │
                                ▼                        ▼
                       ┌─────────────────┐    ┌─────────────────┐
                       │  Summary        │    │  Final Summary  │
                       │  Templates      │    │  Document       │
                       └─────────────────┘    └─────────────────┘
```

### Session Summary Service Interface

```csharp
public interface ISessionSummaryService
{
    /// <summary>
    /// Generate a comprehensive session summary
    /// </summary>
    Task<SessionSummary> GenerateSessionSummaryAsync(string sessionId, 
        SummaryOptions? options = null);
    
    /// <summary>
    /// Generate summary for specific time range
    /// </summary>
    Task<SessionSummary> GeneratePartialSummaryAsync(string sessionId, 
        DateTime fromTime, DateTime toTime, SummaryOptions? options = null);
    
    /// <summary>
    /// Generate final session completion summary
    /// </summary>
    Task<SessionSummary> GenerateFinalSummaryAsync(string sessionId, 
        string? userQuestions = null, SummaryOptions? options = null);
}
```

### Summary Document Structure

The generated summary follows this markdown structure:

```markdown
# Session Summary: [Session Name]

**Session ID:** [SessionId]  
**Duration:** [StartTime] - [EndTime] ([TotalDuration])  
**Status:** [Completed/Failed/Partial]  

## Executive Summary

[High-level overview of what was accomplished]

## Task Analysis

### Primary Task
**Description:** [Original task description]  
**Status:** [Completed/Failed/Partial]  
**Evidence:** [Links to specific activities/results]

### Subtasks Completed
- [x] [Subtask 1] - [Completion evidence]
- [x] [Subtask 2] - [Completion evidence]
- [ ] [Subtask 3] - [Failure reason if applicable]

## Actions Taken

### Tool Executions
| Tool | Calls | Success Rate | Key Results |
|------|-------|--------------|-------------|
| github_get_pull_request | 3 | 100% | Retrieved PR data |
| file_editor | 5 | 80% | Modified 4/5 files |

### Detailed Action Log
#### [Timestamp] - [Action Type]
**Description:** [What was done]  
**Input:** [Tool arguments/parameters]  
**Result:** [Success/failure and key outputs]  
**Impact:** [How this contributed to task completion]

## Error Analysis

### Errors Encountered
1. **[Error Type]** at [Timestamp]
   - **Description:** [What went wrong]
   - **Root Cause:** [Analysis of why it happened]
   - **Resolution:** [How it was handled]
   - **Prevention:** [Recommendations for future]

### Performance Issues
- [Any timeouts, rate limits, or performance concerns]

## User Questions & Responses

### Question: [User's question]
**Answer:** [Direct response]  
**Evidence:** [Supporting data from session activities]  
**References:** [Activity IDs or timestamps for verification]

## Session Statistics

- **Total Activities:** [Count]
- **Tool Calls:** [Count] ([Success rate]%)
- **OpenAI Requests:** [Count]
- **Errors:** [Count]
- **Duration:** [Total session time]
- **Token Usage:** [Estimated tokens consumed]

## Completion Assessment

### Task Accomplishment
[LLM analysis of whether the original task was successfully completed, with evidence]

### Quality Metrics
- **Objective Achievement:** [Percentage/assessment]
- **Error Rate:** [Percentage]
- **Efficiency:** [Assessment of resource usage]

### Recommendations
[Suggestions for improvement or follow-up actions]

## Raw Activity Data

[Optional: Link to detailed activity log for technical analysis]
```

## Implementation Details

### SessionSummaryService Implementation

The service operates in several phases:

1. **Activity Collection**: Retrieve all session activities from the database
2. **Activity Analysis**: Categorize and analyze activities by type
3. **Context Building**: Prepare comprehensive context for LLM analysis
4. **LLM Processing**: Use OpenAI to generate intelligent summary
5. **Template Population**: Structure the response into markdown format
6. **Validation**: Ensure summary completeness and accuracy

### LLM Prompt Strategy

The service uses structured prompts to guide the LLM analysis:

```
You are a session analysis expert. Analyze the following agent session data and create a comprehensive summary.

Session Data:
- Session ID: {sessionId}
- Duration: {duration}
- Total Activities: {activityCount}
- Task Description: {originalTask}

Activities:
{detailedActivityList}

Please create a markdown summary that includes:
1. Executive summary of accomplishments
2. Detailed task analysis with evidence
3. Complete action log with results
4. Error analysis and recommendations
5. Answers to user questions: {userQuestions}
6. Assessment of task completion

Focus on providing clear evidence for task completion and actionable insights for any issues encountered.
```

### Activity Analysis Engine

The analyzer categorizes activities into:

- **Task Management**: Planning, markdown updates, subtask completion
- **Tool Executions**: All tool calls with inputs/outputs
- **OpenAI Interactions**: API requests and responses
- **Error Events**: Failures, timeouts, validation issues
- **Session Control**: Start, end, status changes

### Integration Points

#### Existing Session Management
- Integrates with `ISessionManager` for activity retrieval
- Uses `ISessionActivityLogger` patterns for consistency
- Leverages existing session models and interfaces

#### Task State Management
- Coordinates with `MarkdownTaskStateManager` for task context
- Incorporates markdown task documents into summary analysis
- Maintains consistency with existing task tracking

#### Error Handling
- Processes all logged errors for comprehensive analysis
- Provides actionable recommendations based on error patterns
- Supports debugging and troubleshooting workflows

## Configuration Options

### SummaryOptions

```csharp
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
```

## Activity Type Integration

### New Activity Type

```csharp
public static class ActivityTypes
{
    // ... existing types ...
    
    /// <summary>
    /// Activity type for session summary generation
    /// </summary>
    public const string SessionSummary = "Session_Summary";
}
```

### Summary Activity Logging

When a summary is generated, the service logs a `Session_Summary` activity containing:
- Summary generation timestamp
- Summary options used
- Summary length and key metrics
- Any errors during generation

## Error Handling and Resilience

### Graceful Degradation
- If LLM analysis fails, provide template-based summary
- Handle missing activity data with appropriate fallbacks
- Support partial summaries for incomplete sessions

### Performance Considerations
- Batch process large activity sets
- Implement caching for repeated summary requests
- Use activity sampling for very long sessions

### Data Privacy
- Filter sensitive information before LLM processing
- Provide option to exclude specific activity types
- Support local-only processing for confidential sessions

## Testing Strategy

### Unit Tests
- Test activity collection and categorization
- Verify markdown generation and formatting
- Validate error handling scenarios
- Test configuration option processing

### Integration Tests
- End-to-end summary generation with real sessions
- LLM interaction testing with mock responses
- Performance testing with large activity sets
- Cross-service integration validation

### User Acceptance Tests
- Summary accuracy and completeness validation
- User question response quality assessment
- Markdown rendering and readability verification
- Error analysis usefulness evaluation

## Security Considerations

### Data Protection
- Sanitize sensitive data before LLM processing
- Implement activity filtering for confidential information
- Audit trail for summary access and generation

### API Security
- Rate limiting for summary generation requests
- Authentication requirements for session access
- Validation of summary generation permissions

## Future Enhancements

### Advanced Analysis
- Machine learning for error pattern detection
- Automated performance optimization suggestions
- Cross-session analysis and trends
- Predictive failure analysis

### User Experience
- Interactive summary exploration
- Custom summary templates
- Real-time summary updates during session
- Export capabilities (PDF, HTML, etc.)

### Integration Capabilities
- Webhook notifications for summary completion
- API endpoints for external system integration
- Summary comparison and diff capabilities
- Automated reporting and dashboards

## Conclusion

The Session Summary Stage provides essential closure to agent sessions by delivering comprehensive analysis, task completion evidence, and actionable insights. The design leverages existing infrastructure while introducing powerful new capabilities for session understanding and improvement.

The implementation maintains consistency with current patterns while providing the flexibility needed for diverse use cases and future enhancements. The markdown-based output ensures compatibility with UI rendering and user workflows.