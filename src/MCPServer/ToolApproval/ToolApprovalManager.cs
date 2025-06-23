using System;
using System.Collections.Generic;                    // + new
using System.Text.Json;                              // + new
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;                         // + new
using ModelContextProtocol.Protocol;

namespace MCPServer.ToolApproval;

public sealed class ToolApprovalManager
{
    public static ToolApprovalManager Instance { get; } = new();

    private const string ConnectionString = "Data Source=tool_approval.db";
    private IApprovalBackend _approvalBackend;

    private ToolApprovalManager()
    {
        EnsureDatabase();
        // Default to console approval backend for backward compatibility
        _approvalBackend = new ConsoleApprovalBackend();
    }

    /// <summary>
    /// Sets the approval backend to use for processing approval requests.
    /// </summary>
    /// <param name="backend">The approval backend to use</param>
    public void SetApprovalBackend(IApprovalBackend backend)
    {
        _approvalBackend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    private static void EnsureDatabase()
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            CREATE TABLE IF NOT EXISTS ApprovalInvocations (
                Id         TEXT PRIMARY KEY,
                ToolName   TEXT NOT NULL,
                Arguments  TEXT NOT NULL,
                CreatedAt  TEXT NOT NULL,
                Status     INTEGER NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Legacy synchronous method for backward compatibility.
    /// Use EnsureApprovedAsync for new code.
    /// </summary>
    [Obsolete("Use EnsureApprovedAsync instead")]
    public bool EnsureApproved(string toolName, IReadOnlyDictionary<string, object?> args)
    {
        return EnsureApprovedAsync(toolName, args).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Asynchronously requests approval for a tool invocation.
    /// </summary>
    /// <param name="toolName">Name of the tool being invoked</param>
    /// <param name="args">Arguments passed to the tool</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if approved, false if denied</returns>
    public async Task<bool> EnsureApprovedAsync(string toolName, IReadOnlyDictionary<string, object?> args, CancellationToken cancellationToken = default)
    {
        var token = new ApprovalInvocationToken(
            Guid.NewGuid(), toolName, args, DateTimeOffset.UtcNow);

        InsertToken(token);

        try
        {
            var approved = await _approvalBackend.RequestApprovalAsync(token, cancellationToken);
            UpdateStatus(token.Id, approved ? ApprovalStatus.Approved : ApprovalStatus.Denied);
            return approved;
        }
        catch (OperationCanceledException)
        {
            UpdateStatus(token.Id, ApprovalStatus.Denied);
            throw;
        }
        catch (Exception)
        {
            // For safety, deny approval on any error
            UpdateStatus(token.Id, ApprovalStatus.Denied);
            throw;
        }
    }

    // ---------------------- persistence helpers --------------------------
    private static void InsertToken(ApprovalInvocationToken t)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO ApprovalInvocations (Id, ToolName, Arguments, CreatedAt, Status)
            VALUES ($id, $tool, $args, $created, $status);
            """;
        cmd.Parameters.AddWithValue("$id",      t.Id.ToString());
        cmd.Parameters.AddWithValue("$tool",    t.ToolName);
        cmd.Parameters.AddWithValue("$args",    JsonSerializer.Serialize(t.Arguments));
        cmd.Parameters.AddWithValue("$created", t.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$status",  (int)t.Status);
        cmd.ExecuteNonQuery();
    }

    private static void UpdateStatus(Guid id, ApprovalStatus status)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            UPDATE ApprovalInvocations
               SET Status = $status
             WHERE Id = $id;
            """;
        cmd.Parameters.AddWithValue("$status", (int)status);
        cmd.Parameters.AddWithValue("$id",     id.ToString());
        cmd.ExecuteNonQuery();
    }

    private static IReadOnlyCollection<ApprovalInvocationToken> QueryTokens(string whereClause)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $"""
            SELECT Id, ToolName, Arguments, CreatedAt, Status
              FROM ApprovalInvocations
              {whereClause};
            """;

        using var reader = cmd.ExecuteReader();
        var list = new List<ApprovalInvocationToken>();
        while (reader.Read())
        {
            var id        = Guid.Parse(reader.GetString(0));
            var tool      = reader.GetString(1);
            var argsJson  = reader.GetString(2);
            var created   = DateTimeOffset.Parse(reader.GetString(3));
            var status    = (ApprovalStatus)reader.GetInt32(4);
            var args      = JsonSerializer.Deserialize<Dictionary<string, object?>>(argsJson)
                            ?? new Dictionary<string, object?>();
            list.Add(new ApprovalInvocationToken(id, tool, args, created, status));
        }
        return list.AsReadOnly();
    }

    public IReadOnlyCollection<ApprovalInvocationToken> AuditTrail
        => QueryTokens(string.Empty);

    public IReadOnlyCollection<ApprovalInvocationToken> PendingInvocations
        => QueryTokens("WHERE Status = 0");   // 0 == ApprovalStatus.Pending
}
