using System.Text.Json;

namespace Common.Models.Session;

/// <summary>
/// Represents a persistent agent session that can be saved and restored
/// </summary>
public class AgentSession
{
    /// <summary>
    /// Unique identifier for the session
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
    
    /// <summary>
    /// Human-readable name or description for the session
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// When the session was created
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// When the session was last updated
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }
    
    /// <summary>
    /// Serialized conversation messages
    /// </summary>
    public string ConversationState { get; set; } = string.Empty;
    
    /// <summary>
    /// Configuration used for this session
    /// </summary>
    public string ConfigurationSnapshot { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional metadata for the session
    /// </summary>
    public string Metadata { get; set; } = string.Empty;
    
    /// <summary>
    /// Status of the session
    /// </summary>
    public SessionStatus Status { get; set; } = SessionStatus.Active;
    
    /// <summary>
    /// Serialized current task plan for the session
    /// </summary>
    public string CurrentPlan { get; set; } = string.Empty;
    
    /// <summary>
    /// Serialized activity log for session audit trail
    /// </summary>
    public string ActivityLog { get; set; } = string.Empty;
    
    /// <summary>
    /// Title/name of the current task being executed
    /// </summary>
    public string TaskTitle { get; set; } = string.Empty;
    
    /// <summary>
    /// Current status of the task execution
    /// </summary>
    public TaskExecutionStatus TaskStatus { get; set; } = TaskExecutionStatus.NotStarted;
    
    /// <summary>
    /// Current step number being executed (0 if not started)
    /// </summary>
    public int CurrentStep { get; set; } = 0;
    
    /// <summary>
    /// Total number of steps in the current task plan
    /// </summary>
    public int TotalSteps { get; set; } = 0;
    
    /// <summary>
    /// Number of completed steps
    /// </summary>
    public int CompletedSteps { get; set; } = 0;
    
    /// <summary>
    /// Progress percentage (0.0 to 1.0)
    /// </summary>
    public double ProgressPercentage { get; set; } = 0.0;
    
    /// <summary>
    /// When task execution started
    /// </summary>
    public DateTime? TaskStartedAt { get; set; }
    
    /// <summary>
    /// When task execution completed
    /// </summary>
    public DateTime? TaskCompletedAt { get; set; }
    
    /// <summary>
    /// Category/type of the current task
    /// </summary>
    public string TaskCategory { get; set; } = string.Empty;
    
    /// <summary>
    /// Priority level of the task (1=Low, 2=Normal, 3=High, 4=Critical, 5=Emergency)
    /// </summary>
    public int TaskPriority { get; set; } = 2;
    
    /// <summary>
    /// Comma-separated tags for task categorization
    /// </summary>
    public string TaskTags { get; set; } = string.Empty;
    
    /// <summary>
    /// Estimated duration in minutes
    /// </summary>
    public int? EstimatedDuration { get; set; }
    
    /// <summary>
    /// Actual duration in minutes (calculated when task completes)
    /// </summary>
    public int? ActualDuration { get; set; }
    
    /// <summary>
    /// Create a new session with a generated ID
    /// </summary>
    public static AgentSession CreateNew(string name = "")
    {
        var now = DateTime.UtcNow;
        return new AgentSession
        {
            SessionId = Guid.NewGuid().ToString(),
            Name = string.IsNullOrEmpty(name) ? $"Session {now:yyyy-MM-dd HH:mm}" : name,
            CreatedAt = now,
            LastUpdatedAt = now,
            Status = SessionStatus.Active
        };
    }
    
    /// <summary>
    /// Deserialize conversation messages from stored state
    /// </summary>
    public List<object> GetConversationMessages()
    {
        if (string.IsNullOrEmpty(ConversationState))
            return new List<object>();
            
        try
        {
            return JsonSerializer.Deserialize<List<object>>(ConversationState) ?? new List<object>();
        }
        catch
        {
            return new List<object>();
        }
    }
    
    /// <summary>
    /// Serialize conversation messages to storable state
    /// </summary>
    public void SetConversationMessages(IEnumerable<object> messages)
    {
        try
        {
            ConversationState = JsonSerializer.Serialize(messages);
            LastUpdatedAt = DateTime.UtcNow;
        }
        catch
        {
            ConversationState = string.Empty;
        }
    }
    
    /// <summary>
    /// Get the current task plan for this session
    /// </summary>
    public TaskPlan? GetCurrentPlan()
    {
        if (string.IsNullOrEmpty(CurrentPlan))
            return null;
            
        try
        {
            return JsonSerializer.Deserialize<TaskPlan>(CurrentPlan);
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// Set the current task plan for this session
    /// </summary>
    public void SetCurrentPlan(TaskPlan? plan)
    {
        try
        {
            CurrentPlan = plan != null ? JsonSerializer.Serialize(plan) : string.Empty;
            LastUpdatedAt = DateTime.UtcNow;
        }
        catch
        {
            CurrentPlan = string.Empty;
        }
    }
    
    /// <summary>
    /// Get the activity log for this session
    /// </summary>
    public List<SessionActivity> GetActivityLog()
    {
        if (string.IsNullOrEmpty(ActivityLog))
            return new List<SessionActivity>();
            
        try
        {
            return JsonSerializer.Deserialize<List<SessionActivity>>(ActivityLog) ?? new List<SessionActivity>();
        }
        catch
        {
            return new List<SessionActivity>();
        }
    }
    
    /// <summary>
    /// Add an activity to the session log
    /// </summary>
    public void AddActivity(SessionActivity activity)
    {
        var activities = GetActivityLog();
        activities.Add(activity);
        SetActivityLog(activities);
    }
    
    /// <summary>
    /// Set the complete activity log for this session
    /// </summary>
    public void SetActivityLog(List<SessionActivity> activities)
    {
        try
        {
            ActivityLog = JsonSerializer.Serialize(activities);
            LastUpdatedAt = DateTime.UtcNow;
        }
        catch
        {
            ActivityLog = string.Empty;
        }
    }
    
    /// <summary>
    /// Update task execution information based on a task plan
    /// </summary>
    public void UpdateTaskInfo(TaskPlan taskPlan)
    {
        TaskTitle = taskPlan.Task;
        TotalSteps = taskPlan.Steps.Count;
        TaskCategory = taskPlan.Complexity.ToString();
        
        if (TaskStatus == TaskExecutionStatus.NotStarted)
        {
            TaskStatus = TaskExecutionStatus.InProgress;
            TaskStartedAt = DateTime.UtcNow;
            CurrentStep = 1; // Start with first step
        }
        
        // Extract tags from additional context if available
        if (taskPlan.AdditionalContext?.ContainsKey("tags") == true)
        {
            TaskTags = taskPlan.AdditionalContext["tags"].ToString() ?? string.Empty;
        }
        
        // Extract priority if available
        if (taskPlan.AdditionalContext?.ContainsKey("priority") == true)
        {
            if (int.TryParse(taskPlan.AdditionalContext["priority"].ToString(), out int priority))
            {
                TaskPriority = Math.Max(1, Math.Min(5, priority)); // Ensure 1-5 range
            }
        }
        
        UpdateProgressPercentage();
        LastUpdatedAt = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Update progress when a subtask is completed
    /// </summary>
    public void UpdateSubtaskProgress(int completedStepNumber, bool isTaskComplete = false)
    {
        CompletedSteps = Math.Max(CompletedSteps, completedStepNumber);
        CurrentStep = isTaskComplete ? TotalSteps : Math.Min(TotalSteps, completedStepNumber + 1);
        
        if (isTaskComplete)
        {
            TaskStatus = TaskExecutionStatus.Completed;
            TaskCompletedAt = DateTime.UtcNow;
            ProgressPercentage = 1.0;
            
            // Calculate actual duration
            if (TaskStartedAt.HasValue)
            {
                ActualDuration = (int)(DateTime.UtcNow - TaskStartedAt.Value).TotalMinutes;
            }
        }
        else
        {
            UpdateProgressPercentage();
        }
        
        LastUpdatedAt = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Update progress percentage based on completed steps
    /// </summary>
    private void UpdateProgressPercentage()
    {
        if (TotalSteps > 0)
        {
            ProgressPercentage = Math.Round((double)CompletedSteps / TotalSteps, 3);
        }
        else
        {
            ProgressPercentage = 0.0;
        }
    }
    
    /// <summary>
    /// Mark task as failed
    /// </summary>
    public void MarkTaskFailed(string reason = "")
    {
        TaskStatus = TaskExecutionStatus.Failed;
        TaskCompletedAt = DateTime.UtcNow;
        
        if (TaskStartedAt.HasValue)
        {
            ActualDuration = (int)(DateTime.UtcNow - TaskStartedAt.Value).TotalMinutes;
        }
        
        // Add failure reason to activity log
        if (!string.IsNullOrEmpty(reason))
        {
            var activities = GetActivityLog();
            activities.Add(SessionActivity.Create(ActivityTypes.Error, $"Task failed: {reason}"));
            SetActivityLog(activities);
        }
        
        LastUpdatedAt = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Get task tags as a list
    /// </summary>
    public List<string> GetTaskTagsList()
    {
        return string.IsNullOrEmpty(TaskTags) 
            ? new List<string>() 
            : TaskTags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                     .Select(tag => tag.Trim())
                     .Where(tag => !string.IsNullOrEmpty(tag))
                     .ToList();
    }
    
    /// <summary>
    /// Set task tags from a list
    /// </summary>
    public void SetTaskTags(IEnumerable<string> tags)
    {
        TaskTags = string.Join(", ", tags.Where(tag => !string.IsNullOrWhiteSpace(tag)));
        LastUpdatedAt = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Get a summary of task progress
    /// </summary>
    public string GetTaskProgressSummary()
    {
        if (TaskStatus == TaskExecutionStatus.NotStarted)
            return "Task not started";
            
        var progress = $"{CompletedSteps}/{TotalSteps} steps completed ({ProgressPercentage:P1})";
        
        return TaskStatus switch
        {
            TaskExecutionStatus.InProgress => $"In Progress: {progress}",
            TaskExecutionStatus.Completed => $"Completed: {progress}",
            TaskExecutionStatus.Failed => $"Failed: {progress}",
            TaskExecutionStatus.Cancelled => $"Cancelled: {progress}",
            _ => progress
        };
    }
}

/// <summary>
/// Status of an agent session
/// </summary>
public enum SessionStatus
{
    Active,
    Completed,
    Archived,
    Error
}

/// <summary>
/// Status of task execution within a session
/// </summary>
public enum TaskExecutionStatus
{
    /// <summary>
    /// No task has been started in this session
    /// </summary>
    NotStarted,
    
    /// <summary>
    /// Task is currently being executed
    /// </summary>
    InProgress,
    
    /// <summary>
    /// Task has been completed successfully
    /// </summary>
    Completed,
    
    /// <summary>
    /// Task execution failed
    /// </summary>
    Failed,
    
    /// <summary>
    /// Task execution was cancelled
    /// </summary>
    Cancelled
}