using MCPServer.ToolApproval;
using ApprovalService.Controllers;
using System;
using System.IO;

namespace ApprovalService;

public interface IApprovalStore
{
    Task StoreApprovalRequestAsync(ApprovalRequest request);
    Task<ApprovalRequest?> GetApprovalRequestAsync(Guid id);
    Task<bool> UpdateApprovalStatusAsync(Guid id, ApprovalStatus status);
    Task<IEnumerable<ApprovalRequest>> GetPendingApprovalsAsync();
}

public class SqliteApprovalStore : IApprovalStore
{
    private readonly string _connectionString;

    public SqliteApprovalStore()
    {
        // Use environment variable for database path with fallback to shared data directory
        var databasePath = Environment.GetEnvironmentVariable("APPROVAL_SERVICE_DB_PATH") ?? "./data/approval_service.db";
        _connectionString = $"Data Source={databasePath}";
        
        // Ensure directory exists
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        EnsureDatabase();
    }

    private void EnsureDatabase()
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS ApprovalRequests (
                Id         TEXT PRIMARY KEY,
                ToolName   TEXT NOT NULL,
                Arguments  TEXT NOT NULL,
                CreatedAt  TEXT NOT NULL,
                Status     INTEGER NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public async Task StoreApprovalRequestAsync(ApprovalRequest request)
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ApprovalRequests (Id, ToolName, Arguments, CreatedAt, Status)
            VALUES ($id, $tool, $args, $created, $status);
            """;
        cmd.Parameters.AddWithValue("$id", request.Id.ToString());
        cmd.Parameters.AddWithValue("$tool", request.ToolName);
        cmd.Parameters.AddWithValue("$args", System.Text.Json.JsonSerializer.Serialize(request.Arguments));
        cmd.Parameters.AddWithValue("$created", request.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$status", (int)request.Status);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<ApprovalRequest?> GetApprovalRequestAsync(Guid id)
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id, ToolName, Arguments, CreatedAt, Status
              FROM ApprovalRequests
             WHERE Id = $id;
            """;
        cmd.Parameters.AddWithValue("$id", id.ToString());

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var argsJson = reader.GetString(2);
            var args = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(argsJson)
                      ?? new Dictionary<string, object?>();

            return new ApprovalRequest
            {
                Id = Guid.Parse(reader.GetString(0)),
                ToolName = reader.GetString(1),
                Arguments = args,
                CreatedAt = DateTimeOffset.Parse(reader.GetString(3)),
                Status = (ApprovalStatus)reader.GetInt32(4)
            };
        }

        return null;
    }

    public async Task<bool> UpdateApprovalStatusAsync(Guid id, ApprovalStatus status)
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE ApprovalRequests
               SET Status = $status
             WHERE Id = $id;
            """;
        cmd.Parameters.AddWithValue("$status", (int)status);
        cmd.Parameters.AddWithValue("$id", id.ToString());
        
        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    public async Task<IEnumerable<ApprovalRequest>> GetPendingApprovalsAsync()
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id, ToolName, Arguments, CreatedAt, Status
              FROM ApprovalRequests
             WHERE Status = 0
             ORDER BY CreatedAt;
            """;

        using var reader = await cmd.ExecuteReaderAsync();
        var requests = new List<ApprovalRequest>();
        
        while (await reader.ReadAsync())
        {
            var argsJson = reader.GetString(2);
            var args = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(argsJson)
                      ?? new Dictionary<string, object?>();

            requests.Add(new ApprovalRequest
            {
                Id = Guid.Parse(reader.GetString(0)),
                ToolName = reader.GetString(1),
                Arguments = args,
                CreatedAt = DateTimeOffset.Parse(reader.GetString(3)),
                Status = (ApprovalStatus)reader.GetInt32(4)
            });
        }

        return requests;
    }
}