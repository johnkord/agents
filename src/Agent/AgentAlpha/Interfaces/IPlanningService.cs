using AgentAlpha.Models;
using Common.Interfaces.Session;
using ModelContextProtocol.Client;

namespace AgentAlpha.Interfaces;

/// <summary>
/// Service responsible for creating and managing task execution using markdown-based planning
/// </summary>
public interface IPlanningService
{
    /// <summary>
    /// Initialize task planning directly into markdown format
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="task">The task to plan for</param>
    /// <param name="availableTools">Tools available to the agent</param>
    /// <param name="context">Optional context from previous conversations or sessions</param>
    /// <returns>The initialized markdown task document</returns>
    Task<string> InitializeTaskPlanningAsync(string sessionId, string task, IList<IUnifiedTool> availableTools, string? context = null);
    
    /// <summary>
    /// Initialize task planning with current state analysis directly into markdown format
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="task">The task to plan for</param>
    /// <param name="availableTools">Tools available to the agent</param>
    /// <param name="currentState">Current state of the environment and context</param>
    /// <param name="context">Optional additional context</param>
    /// <returns>The initialized markdown task document</returns>
    Task<string> InitializeTaskPlanningWithStateAsync(string sessionId, string task, IList<IUnifiedTool> availableTools, CurrentState currentState, string? context = null);
    
    /// <summary>
    /// Set the session activity logger for automatic OpenAI request logging
    /// </summary>
    /// <param name="activityLogger">The activity logger to use for this session</param>
    void SetActivityLogger(ISessionActivityLogger? activityLogger);
}