# Tool Approval System Deployment Guide

This guide explains how to deploy and configure the redesigned tool approval system for different environments.

## Quick Migration Guide

### For Existing Users
No changes required! The system maintains backward compatibility:
- Console approval continues to work exactly as before
- Existing code using `[RequiresApproval]` attribute needs no modifications
- Database and audit logging remain unchanged

### For Cloud Deployments
Replace console approval with remote approval:

```csharp
// Before (blocks in cloud environments)
// System used console approval automatically

// After (cloud-friendly)
var options = new ToolApprovalOptions
{
    BackendType = ApprovalBackendType.Remote,
    RemoteConfig = new RemoteApprovalConfig
    {
        BaseUrl = "https://your-approval-service.com",
        ApiKey = Environment.GetEnvironmentVariable("APPROVAL_API_KEY"),
        ApprovalTimeout = TimeSpan.FromMinutes(5)
    }
};

ToolApprovalManager.Instance.SetApprovalBackend(options.CreateBackend());
```

## Deployment Scenarios

### 1. Local Development
**Use case**: Developer testing, debugging  
**Configuration**: Console approval (default)

```csharp
// No configuration needed - console approval is the default
// Or explicitly:
var options = new ToolApprovalOptions
{
    BackendType = ApprovalBackendType.Console
};
ToolApprovalManager.Instance.SetApprovalBackend(options.CreateBackend());
```

### 2. Cloud/Production Deployment
**Use case**: Agent running in cloud without console access  
**Configuration**: Remote approval service

```csharp
var options = new ToolApprovalOptions
{
    BackendType = ApprovalBackendType.Remote,
    RemoteConfig = new RemoteApprovalConfig
    {
        BaseUrl = "https://approval.company.com",
        ApiKey = Environment.GetEnvironmentVariable("APPROVAL_API_KEY"),
        ApprovalTimeout = TimeSpan.FromMinutes(10), // How long to wait for human approval
        PollInterval = TimeSpan.FromSeconds(3),     // How often to check for approval
        RequestTimeout = TimeSpan.FromSeconds(30)   // HTTP request timeout
    }
};
ToolApprovalManager.Instance.SetApprovalBackend(options.CreateBackend());
```

### 3. Enterprise Integration
**Use case**: Integration with existing approval workflows  
**Configuration**: Custom backend implementation

```csharp
// Implement your own approval backend
public class EnterpriseApprovalBackend : IApprovalBackend
{
    public string Name => "Enterprise";
    
    public async Task<bool> RequestApprovalAsync(ApprovalInvocationToken token, CancellationToken cancellationToken)
    {
        // Integrate with your enterprise systems:
        // - ServiceNow tickets
        // - JIRA workflows  
        // - Teams/Slack notifications
        // - Custom approval processes
        return await YourEnterpriseApprovalSystem.RequestApprovalAsync(token);
    }
}

// Use your custom backend
ToolApprovalManager.Instance.SetApprovalBackend(new EnterpriseApprovalBackend());
```

## Environment Configuration

### Environment Variables
```bash
# For remote approval backend
export APPROVAL_SERVICE_URL="https://approval-service.example.com"
export APPROVAL_API_KEY="your-secret-api-key"

# Optional: Override timeouts
export APPROVAL_TIMEOUT_MINUTES="5"
export APPROVAL_POLL_INTERVAL_SECONDS="2"
```

### Configuration Files
```json
{
  "ToolApproval": {
    "BackendType": "Remote",
    "RemoteConfig": {
      "BaseUrl": "https://approval-service.example.com",
      "ApiKey": "your-api-key",
      "ApprovalTimeout": "00:05:00",
      "PollInterval": "00:00:02",
      "RequestTimeout": "00:00:30"
    }
  }
}
```

## Security Considerations

### API Security
- Always use HTTPS for remote approval services
- Implement proper API key management (rotate regularly)
- Consider using OAuth 2.0 or JWT tokens for authentication
- Validate and sanitize all tool parameters before displaying to approvers

### Network Security
- Whitelist approval service IPs if possible
- Use VPN or private networks for sensitive environments
- Implement request signing to prevent tampering
- Log all approval requests and responses for audit

### Access Control
- Implement role-based access control (RBAC) in your approval service
- Different approval levels for different risk categories
- Time-based access controls (business hours only, etc.)
- Emergency override procedures with enhanced logging

## Monitoring and Observability

### Metrics to Track
- Approval request rate
- Approval/denial ratios
- Average approval time
- Timeout rates
- Backend availability

### Logging
The system automatically logs:
- All approval requests with tool name and parameters
- Approval decisions (approved/denied/timeout)
- Backend errors and failures
- Audit trail in SQLite database

### Alerting
Consider alerts for:
- High denial rates (possible security issues)
- Backend unavailability
- Approval timeouts
- Unusual approval patterns

## Troubleshooting

### Common Issues

**"Remote approval backend not responding"**
- Check network connectivity to approval service
- Verify API key and authentication
- Check approval service logs
- Ensure service is running and healthy

**"Approval requests timing out"**
- Increase `ApprovalTimeout` if approvers need more time
- Check if approval service is processing requests
- Verify polling interval isn't too aggressive

**"Console approval still prompting in cloud"**
- Ensure you've configured and set the remote backend
- Check that configuration is being applied at startup
- Verify environment variables are set correctly

### Fallback Strategies
```csharp
// Implement fallback logic for backend failures
try 
{
    ToolApprovalManager.Instance.SetApprovalBackend(remoteBackend);
}
catch (Exception ex)
{
    Console.WriteLine($"Remote backend failed: {ex.Message}");
    Console.WriteLine("Falling back to console approval");
    ToolApprovalManager.Instance.SetApprovalBackend(new ConsoleApprovalBackend());
}
```

## Future Enhancements

The system is designed to be extensible. Planned enhancements include:

- **Webhook Support**: Push notifications instead of polling
- **Message Queue Integration**: For high-throughput scenarios
- **Approval Policies**: Auto-approve based on risk assessment
- **Mobile Apps**: Native mobile approval applications
- **Integration SDKs**: Pre-built integrations for common platforms

## Support

For questions and issues:
1. Check the design document: `docs/TOOL_APPROVAL_DESIGN.md`
2. Review example code: `examples/approval-service/`
3. Run tests to verify setup: `dotnet test tests/MCPServer.Tests/`
4. Check logs and audit trail in `tool_approval.db`