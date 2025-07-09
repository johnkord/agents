namespace Common.Models.Session;

/// <summary>
/// Defines the types of activities that can be logged during session execution
/// </summary>
public static class ActivityTypes
{
    /// <summary>
    /// Activity type for general information messages
    /// </summary>
    public const string Info = "Info";

    /// <summary>
    /// Activity type for warning messages
    /// </summary>
    public const string Warning = "Warning";

    /// <summary>
    /// Activity type for error messages
    /// </summary>
    public const string Error = "Error";

    /// <summary>
    /// Activity type for successful task completion
    /// </summary>
    public const string TaskComplete = "TaskComplete";

    /// <summary>
    /// Activity type for task failure
    /// </summary>
    public const string TaskFailed = "TaskFailed";

    /// <summary>
    /// Activity type for OpenAI API requests
    /// </summary>
    public const string OpenAIRequest = "OpenAIRequest";

    /// <summary>
    /// Activity type for OpenAI API responses
    /// </summary>
    public const string OpenAIResponse = "OpenAIResponse";

    /// <summary>
    /// Activity type for tool execution requests
    /// </summary>
    public const string ToolExecution = "ToolExecution";

    /// <summary>
    /// Activity type for session start events
    /// </summary>
    public const string SessionStart = "SessionStart";

    /// <summary>
    /// Activity type for session end events
    /// </summary>
    public const string SessionEnd = "SessionEnd";

    /// <summary>
    /// Activity type for task planning events
    /// </summary>
    public const string TaskPlanning = "TaskPlanning";

    /// <summary>
    /// Activity type for task state updates in markdown
    /// </summary>
    public const string TaskMarkdownUpdate = "TaskMarkdownUpdate";

    // ---------- added to reconcile duplicate definitions ----------
    public const string ToolSelection = "Tool_Selection";
    public const string ToolCall = "Tool_Call";
    public const string ToolResult = "Tool_Result";
    public const string ConversationIteration = "Conversation_Iteration";
    public const string PlanDetails = "Plan_Details";
    public const string ToolSelectionReasoning = "Tool_Selection_Reasoning";
    public const string ResponseQualityAssessment = "Response_Quality_Assessment";
    public const string TaskCompletionEvaluation = "Task_Completion_Evaluation";
    public const string Planning = "Planning";

    // ---------- added from AgentAlpha ActivityTypes ----------
    /// <summary>
    /// Activity type for fast-path execution results
    /// </summary>
    public const string FastPathResult = "FastPathResult";

    /// <summary>
    /// Activity type for plan creation events
    /// </summary>
    public const string PlanCreated = "PlanCreated";

    /// <summary>
    /// Activity type for plan refinement events
    /// </summary>
    public const string PlanRefined = "PlanRefined";

    /// <summary>
    /// Activity type for worker spawning events
    /// </summary>
    public const string WorkerSpawned = "WorkerSpawned";

    /// <summary>
    /// Activity type for worker completion events
    /// </summary>
    public const string WorkerCompleted = "WorkerCompleted";

    /// <summary>
    /// Activity type for routing decision events
    /// </summary>
    public const string RouteDecision = "RouteDecision";

    /// <summary>
    /// Activity type for conversation step events
    /// </summary>
    public const string ConversationStep = "ConversationStep";

    /// <summary>
    /// Activity type for planner stage events (P2 feature)
    /// </summary>
    public const string PlannerStage = "PlannerStage";
    
    /// <summary>
    /// Activity type for final results when execution has finished
    /// </summary>
    public const string Result = "Result";
}