using System;
using System.Collections.Generic;

namespace MCPServer.ToolApproval;

public sealed record ApprovalInvocationToken(
    Guid             Id,
    string           ToolName,
    IReadOnlyDictionary<string, object?> Arguments,
    DateTimeOffset   CreatedAt,
    ApprovalStatus   Status = ApprovalStatus.Pending);
