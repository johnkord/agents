using System.Collections.Concurrent;
using AgentAlpha.Interfaces;

namespace AgentAlpha.Services;

public sealed class ToolScopeManager : IToolScopeManager
{
    private readonly ConcurrentDictionary<string,string[]> _map = new();

    public void SetRequiredTools(string sessionId, IReadOnlyCollection<string> tools) =>
        _map[sessionId] = tools.ToArray();

    public string[] GetRequiredTools(string sessionId) =>
        _map.TryGetValue(sessionId, out var tools) ? tools : Array.Empty<string>();

    public void Clear(string sessionId) => _map.TryRemove(sessionId, out _);
}
