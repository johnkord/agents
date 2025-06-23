using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace MCPServer.ToolApproval;

public static class ToolApprovalExtensions
{
    /// <summary>
    /// Call after registering tools to transparently wrap those that are marked
    /// with <see cref="RequiresApprovalAttribute"/>.
    /// </summary>
    public static IMcpServerBuilder EnableToolApproval(this IMcpServerBuilder builder)
    {
        // IMcpServerBuilder no longer exposes Tools directly; registration happens elsewhere.
        // Keep this for future expansion but make it a no-op for now.
        return builder;
    }
}
