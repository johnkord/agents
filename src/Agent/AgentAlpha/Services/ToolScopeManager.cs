using System.Collections.Concurrent;
using AgentAlpha.Interfaces;
using Common.Interfaces.Tools;

namespace AgentAlpha.Services;

public sealed class ToolScopeManager : IToolScopeManager
{
    private readonly ConcurrentDictionary<string, string[]> _map = new();

    // signature now matches interface exactly
    public void SetRequiredTools(string sessionId, IEnumerable<string> toolNames) =>
        _map[sessionId] = toolNames?.ToArray() ?? Array.Empty<string>();

    // explicit IEnumerable<string> return type
    public IEnumerable<string> GetRequiredTools(string sessionId) =>
        _map.TryGetValue(sessionId, out var tools) ? tools : Array.Empty<string>();

    public void Clear(string sessionId) => _map.TryRemove(sessionId, out _);
}
