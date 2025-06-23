using System;

namespace MCPServer.ToolApproval;

/// <summary>
/// Marks an MCP tool as “dangerous” so the runtime asks for human approval
/// before execution.  Usage:
/// [McpServerTool, RequiresApproval]   // shorthand (Required = true)
/// or
/// [McpServerTool, RequiresApproval(false)] // disable
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class RequiresApprovalAttribute : Attribute
{
    public RequiresApprovalAttribute(bool required = true) => Required = required;
    public bool Required { get; }
}
