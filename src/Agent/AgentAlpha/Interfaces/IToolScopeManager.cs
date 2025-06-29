namespace AgentAlpha.Interfaces;

/// <summary>
/// Stores per-session “required tool” information so that any service can
/// query or update the current scope without plumbing arrays around.
/// </summary>
public interface IToolScopeManager
{
    void   SetRequiredTools(string sessionId, IReadOnlyCollection<string> tools);
    string[] GetRequiredTools(string sessionId);
    void   Clear(string sessionId);
}
