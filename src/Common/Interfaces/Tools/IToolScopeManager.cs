using System.Collections.Generic;
using Common.Models.Session;

namespace Common.Interfaces.Tools
{
    /// <summary>
    /// Defines methods for managing tool scopes in a session-aware manner.
    /// This includes setting, clearing, and retrieving required tools for task execution.
    /// </summary>
    public interface IToolScopeManager
    {
        /// <summary>
        /// Sets the required tools for the specified session.
        /// This overrides any previously set tools.
        /// </summary>
        /// <param name="sessionId">The ID of the session.</param>
        /// <param name="toolNames">The names of the tools to be set as required.</param>
        void SetRequiredTools(string sessionId, IEnumerable<string> toolNames);

        /// <summary>
        /// Clears the required tools for the specified session.
        /// </summary>
        /// <param name="sessionId">The ID of the session.</param>
        void Clear(string sessionId);

        /// <summary>
        /// Gets the currently required tools for the specified session.
        /// </summary>
        /// <param name="sessionId">The ID of the session.</param>
        /// <returns>A collection of tool names that are currently required for the session.</returns>
        IEnumerable<string> GetRequiredTools(string sessionId);
    }
}