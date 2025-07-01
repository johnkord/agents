# Tool Approval System Redesign

## Overview

The tool approval system has been redesigned to support remote approval workflows while maintaining a simple local development experience. This allows agents to run in cloud environments where console-based approval is not feasible.

## Key Features

- **Multiple Approval Providers**: Console, File-based, and REST providers
- **Backwards Compatibility**: Existing code continues to work unchanged
- **Async Support**: Non-blocking approval workflows
- **Web Interface**: Beautiful dashboard for managing approvals
- **Configuration-driven**: Easy to switch between providers
- **Audit Trail**: Complete history of all approval decisions

## Quick Start

### Local Development (Default)
No changes needed – simply call `EnsureApproved` at the start of a dangerous tool:

```csharp
public static string DangerousTool(string param)
{
    if (!ToolApprovalManager.Instance.EnsureApproved(
            "dangerous_tool",
            new() { ["param"] = param }))
        return "Denied";

    // ...actual work...
}
```

### Cloud Deployment with REST Approval

1. **Start the Approval Service**:
   ```bash
   cd src/ApprovalService
   dotnet run
   ```
   The service will start at `http://localhost:5000` with a web dashboard.

2. **Configure the Tool Approval Manager**:
   ```csharp
   var config = new ApprovalProviderConfiguration
   {
       ProviderType = ApprovalProviderType.Rest,
       RestProvider = new RestProviderConfig
       {
           BaseUrl = "http://localhost:5000"
       }
   };
   var manager = new ToolApprovalManager(config);
   ```

3. **Use the Web Dashboard**:
   - Open `http://localhost:5000` in your browser
   - View pending approval requests
   - Click "Approve" or "Deny" buttons
   - Auto-refreshes every 5 seconds

### File-based Development Workflow

For development with external tools or scripts:

```csharp
var config = new ApprovalProviderConfiguration
{
    ProviderType = ApprovalProviderType.File,
    FileProvider = new FileProviderConfig
    {
        ApprovalDirectory = "./approvals",
        Timeout = TimeSpan.FromMinutes(5)
    }
};
```

The system will write JSON request files and poll for response files.

## Architecture

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Tool Call     │    │  Approval        │    │  Approval       │
│   [Requires     │───▶│  Manager         │───▶│  Provider       │
│    Approval]    │    │                  │    │  (Console/File/ │
└─────────────────┘    └──────────────────┘    │  REST)          │
                                               └─────────────────┘
                                                        │
                                               ┌─────────────────┐
                                               │  Human          │
                                               │  Decision       │
                                               │  (Web UI/File/  │
                                               │   Console)      │
                                               └─────────────────┘
```

## Benefits

### For Local Development
- **Console Provider**: Immediate y/n prompts (existing behavior)
- **File Provider**: Integration with external approval tools
- **No Configuration**: Works out of the box

### For Cloud Deployment
- **REST Provider**: Web-based approval interface
- **Async Processing**: Non-blocking approval workflows
- **Remote Access**: Approve requests from anywhere
- **Audit Dashboard**: Complete approval history

### For Both
- **Backwards Compatible**: No code changes required
- **Flexible Configuration**: Switch providers easily
- **Audit Trail**: Complete history in SQLite database
- **Type Safety**: Strong typing with C# interfaces

## Components

### Core Interfaces
- `IApprovalProvider`: Abstraction for approval mechanisms
- `ApprovalProviderConfiguration`: Configuration for all providers
- `ApprovalProviderFactory`: Creates providers from configuration

### Providers
- `ConsoleApprovalProvider`: Console-based approval (default)
- `FileApprovalProvider`: File-based approval for automation
- `RestApprovalProvider`: REST API-based approval for cloud

### Approval Service
- ASP.NET Core web service
- REST API for approval management
- Web dashboard for human approvers
- SQLite database for persistence

## Migration

### Minimal Changes Required
The system is designed for minimal disruption:

```csharp
// Before (still works):
var approved = ToolApprovalManager.Instance.EnsureApproved(toolName, args);

// After (optional, for better performance):
var approved = await ToolApprovalManager.Instance.EnsureApprovedAsync(toolName, args);
```

### Configuration Examples

**Environment-based configuration**:
```csharp
var providerType = Environment.GetEnvironmentVariable("APPROVAL_PROVIDER") switch
{
    "file" => ApprovalProviderType.File,
    "rest" => ApprovalProviderType.Rest,
    _ => ApprovalProviderType.Console
};

var config = new ApprovalProviderConfiguration { ProviderType = providerType };
```

**Docker deployment**:
```yaml
version: '3.8'
services:
  approval-service:
    build: src/ApprovalService
    ports:
      - "5000:5000"
  
  agent:
    build: src/Agent
    environment:
      - APPROVAL_SERVICE_URL=http://approval-service:5000
    depends_on:
      - approval-service
```

## Security Considerations

- **Local Development**: Trust-based (console/file)
- **Production**: Future authentication/authorization planned
- **Audit Trail**: All decisions logged with timestamps
- **Input Validation**: Arguments validated and sanitized

## Future Enhancements

- Authentication & authorization for REST provider
- Message queue transport for high-scale scenarios
- Mobile push notifications
- Advanced approval policies and rules
- Multi-tenant support

## Testing

All providers are fully tested:
```bash
dotnet test
```

Tests cover:
- Provider factory creation
- Configuration validation
- Backwards compatibility
- Error handling

## Support

This redesign maintains full backwards compatibility while adding powerful new capabilities for cloud deployment scenarios.

For issues or questions, see the updated design documentation in `docs/TOOL_APPROVAL_DESIGN_UPDATED.md`.