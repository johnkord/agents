# Remote Tool Approval Service Example

This directory contains an example REST API service that can be used with the `RemoteApprovalBackend`.

## Quick Start

```bash
# Build and run the approval service
cd examples/approval-service
dotnet run

# The service will start on http://localhost:5000
# Web UI available at http://localhost:5000/approvals
```

## API Endpoints

### Submit Approval Request
```http
POST /api/approvals
Content-Type: application/json

{
  "id": "12345678-1234-1234-1234-123456789abc",
  "toolName": "delete_file",
  "arguments": { "path": "/tmp/test.txt" },
  "createdAt": "2024-01-01T12:00:00Z",
  "timeout": "00:05:00"
}
```

Response:
```json
{
  "requestId": "12345678-1234-1234-1234-123456789abc",
  "status": "pending"
}
```

### Check Approval Status
```http
GET /api/approvals/status?requestId=12345678-1234-1234-1234-123456789abc
```

Response:
```json
{
  "requestId": "12345678-1234-1234-1234-123456789abc",
  "status": "pending|approved|denied",
  "approvedBy": "user@example.com",
  "timestamp": "2024-01-01T12:01:00Z"
}
```

## Configuration

Configure the MCP Server to use the remote approval backend:

```csharp
var approvalOptions = new ToolApprovalOptions
{
    BackendType = ApprovalBackendType.Remote,
    RemoteConfig = new RemoteApprovalConfig
    {
        BaseUrl = "http://localhost:5000",
        ApiKey = "your-api-key-here",
        ApprovalTimeout = TimeSpan.FromMinutes(5),
        PollInterval = TimeSpan.FromSeconds(2)
    }
};

ToolApprovalManager.Instance.SetApprovalBackend(approvalOptions.CreateBackend());
```

## Security Considerations

- Always use HTTPS in production
- Implement proper authentication (API keys, OAuth, etc.)
- Consider rate limiting and request validation
- Log all approval decisions for audit purposes
- Implement request signing to prevent tampering