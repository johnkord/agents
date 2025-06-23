using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MCPServer.ToolApproval;

/// <summary>
/// Console-based approval provider that prompts the user at the console.
/// This maintains the current behavior for local development scenarios.
/// </summary>
public class ConsoleApprovalProvider : IApprovalProvider
{
    public string ProviderName => "Console";

    public Task<bool> RequestApprovalAsync(ApprovalInvocationToken token, CancellationToken cancellationToken = default)
    {
        // Maintain current synchronous console behavior
        Console.Error.WriteLine(
            $"Tool '{token.ToolName}' requested with args: {JsonSerializer.Serialize(token.Arguments)}");
        Console.Error.Write("Approve? [y/N] ");
        
        var answer = Console.ReadLine();
        var approved = string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase);
        
        return Task.FromResult(approved);
    }
}