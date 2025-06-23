using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MCPServer.ToolApproval;

/// <summary>
/// Console-based approval backend that prompts the user at the console.
/// Suitable for local development and testing scenarios.
/// </summary>
public class ConsoleApprovalBackend : IApprovalBackend
{
    public string Name => "Console";

    public Task<bool> RequestApprovalAsync(ApprovalInvocationToken token, CancellationToken cancellationToken = default)
    {
        // Run on task to avoid blocking the current thread completely
        return Task.Run(() =>
        {
            Console.Error.WriteLine(
                $"Tool '{token.ToolName}' requested with args: {JsonSerializer.Serialize(token.Arguments)}");
            Console.Error.Write("Approve? [y/N] ");
            
            var answer = Console.ReadLine();
            var approved = string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase);
            
            return approved;
        }, cancellationToken);
    }
}