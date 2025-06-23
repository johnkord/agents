using System.Collections.Generic;                    // + new
using System.Linq;
using System.Reflection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace MCPServer.ToolApproval;

public static class ToolApprovalWrapper
{
    public static McpServerTool WrapIfNeeded(McpServerTool original, MethodInfo methodInfo)
    {
        // Check attribute – if not present or not required, just return the original tool
        var attr = methodInfo.GetCustomAttributes(typeof(RequiresApprovalAttribute), false)
                             .Cast<RequiresApprovalAttribute>()
                             .FirstOrDefault();

        if (attr is null || !attr.Required)
            return original;

        // Create a proxy tool that asks for approval and then delegates
        return McpServerTool.Create(
            async (RequestContext<CallToolRequestParams> ctx) =>
            {
                var args = ctx.Params?.Arguments?
                               .ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value)
                               ?? new Dictionary<string, object?>();

                if (!await ToolApprovalManager.Instance.EnsureApprovedAsync(original.ProtocolTool.Name, args))
                    return ErrorResult($"Invocation of '{original.ProtocolTool.Name}' was denied.");

                // approved – forward to the original tool
                return await original.InvokeAsync(ctx);      // ← fixed delegation
            },
            new() { Name = original.ProtocolTool.Name });
    }

    static CallToolResult ErrorResult(string message) => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = message }]
    };
}
