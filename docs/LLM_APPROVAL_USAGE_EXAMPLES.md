# LLM Tool Approval Usage Examples

This document provides examples of how to use the LLM-driven tool approval system.

## Basic Configuration

### Environment Variables

Set these environment variables to enable LLM approval:

```bash
# Enable LLM approval provider
APPROVAL_PROVIDER_TYPE=llm

# LLM service configuration
LLM_SERVICE_TYPE=mock  # Use mock service for testing
LLM_AUTO_APPROVAL_MIN_CONFIDENCE=0.85
LLM_HUMAN_FALLBACK_MAX_CONFIDENCE=0.50
LLM_CACHE_ENABLED=true
LLM_CACHE_TTL=01:00:00  # 1 hour
LLM_TIMEOUT=00:00:30   # 30 seconds
LLM_FALLBACK_TO_HUMAN=true
```

### Programmatic Configuration

```csharp
using MCPServer.ToolApproval;
using MCPServer.ToolApproval.LlmApproval;

// Create LLM approval configuration
var config = new ApprovalProviderConfiguration
{
    ProviderType = ApprovalProviderType.Llm,
    LlmProvider = new LlmProviderConfig
    {
        ServiceType = LlmServiceType.Mock,
        AutoApprovalMinConfidence = 0.85,
        HumanFallbackMaxConfidence = 0.50,
        CacheEnabled = true,
        CacheTtl = TimeSpan.FromHours(1),
        Timeout = TimeSpan.FromSeconds(30),
        FallbackToHuman = true
    }
};

// Create the approval manager
var approvalManager = new ToolApprovalManager(config);
```

## Tool Examples

### Safe Operations (Auto-Approved)

```csharp
[McpServerTool(Name = "read_file"), RequiresApproval]
public static string ReadFile(string path)
{
    // Safe read operations typically get auto-approved
    // LLM assesses as Safe risk with high confidence
    return File.ReadAllText(path);
}

[McpServerTool(Name = "get_current_time"), RequiresApproval]
public static string GetCurrentTime()
{
    // Very safe operations get auto-approved
    return DateTime.Now.ToString();
}
```

### Moderate Risk Operations (Context-Dependent)

```csharp
[McpServerTool(Name = "write_file"), RequiresApproval]
public static void WriteFile(string path, string content)
{
    // LLM evaluates based on path and content
    // Safe paths like /tmp/test.txt typically auto-approved
    // System paths or executable files require human approval
    File.WriteAllText(path, content);
}

[McpServerTool(Name = "create_directory"), RequiresApproval]
public static void CreateDirectory(string path)
{
    // Evaluated based on path safety
    Directory.CreateDirectory(path);
}
```

### High Risk Operations (Often Require Human)

```csharp
[McpServerTool(Name = "delete_file"), RequiresApproval]
public static void DeleteFile(string path)
{
    // High risk - LLM checks for dangerous patterns
    // .exe files, system paths → require human approval
    // User documents, temp files → may auto-approve
    File.Delete(path);
}

[McpServerTool(Name = "execute_command"), RequiresApproval]
public static string ExecuteCommand(string command)
{
    // High risk - LLM analyzes command for safety
    // Simple commands like "ls" → may auto-approve
    // "rm -rf", "sudo" commands → require human approval
    return Process.Start(command).StandardOutput.ReadToEnd();
}
```

### Critical Operations (Always Require Human)

```csharp
[McpServerTool(Name = "format_disk"), RequiresApproval]
public static void FormatDisk(string drive)
{
    // Critical operations require human approval regardless
    // LLM categorizes as Critical risk
    // Always falls back to human approval
    FormatDrive(drive);
}

[McpServerTool(Name = "delete_database"), RequiresApproval]
public static void DeleteDatabase(string connectionString)
{
    // Critical database operations require human oversight
    DropDatabase(connectionString);
}
```

## Custom Tool Policies

### Tool-Specific Configuration

```csharp
[McpServerTool(Name = "backup_database"), RequiresApproval]
[LlmApprovalPolicy(AllowAutoApproval = false)] // Always require human
public static void BackupDatabase(string connectionString)
{
    CreateBackup(connectionString);
}

[McpServerTool(Name = "read_log_file"), RequiresApproval]
[LlmApprovalPolicy(MinConfidence = 0.70, RiskCategory = RiskCategory.Safe)]
public static string ReadLogFile(string path)
{
    // Lower confidence threshold for log files
    return File.ReadAllText(path);
}
```

### Policy Configuration

```csharp
var policy = new LlmApprovalPolicy
{
    AutoApprovalMinConfidence = 0.85,
    HumanRequiredMaxConfidence = 0.50,
    
    // Tool-specific policies
    ToolPolicies = new Dictionary<string, ToolPolicy>
    {
        ["backup_database"] = new ToolPolicy 
        { 
            AllowAutoApproval = false // Always require human
        },
        ["read_config"] = new ToolPolicy 
        { 
            MinConfidenceOverride = 0.70 // Lower threshold
        }
    },
    
    // Never auto-approve these tools
    NeverAutoApprove = new List<string> 
    { 
        "delete_database", 
        "format_disk",
        "modify_security_settings"
    },
    
    // Risk-based policies
    RiskPolicies = new Dictionary<RiskCategory, RiskPolicy>
    {
        [RiskCategory.Safe] = new RiskPolicy { MinConfidence = 0.70 },
        [RiskCategory.Critical] = new RiskPolicy { AllowAutoApproval = false }
    }
};
```

## LLM Decision Examples

### Safe Operations

```
Tool: read_file
Arguments: { "path": "/home/user/document.txt" }
LLM Decision: Approve (Confidence: 0.90, Risk: Safe)
Reasoning: Safe file read operation in user directory with .txt extension
Final Result: ✅ Auto-Approved
```

### Dangerous Patterns Detected

```
Tool: delete_file  
Arguments: { "path": "/system/important.exe" }
LLM Decision: RequireHuman (Confidence: 0.30, Risk: High)
Reasoning: High risk operation with dangerous pattern (.exe file in system path)
Concerns: ["Dangerous pattern detected in path: \.exe$"]
Final Result: 🔄 Human Approval Required
```

### Policy Override

```
Tool: write_file
Arguments: { "path": "/app/config.json", "content": "..." }
LLM Decision: Approve (Confidence: 0.85, Risk: Moderate)
Policy Override: RequireHuman (Tool confidence threshold: 0.90)
Final Result: 🔄 Human Approval Required (Policy)
```

### Critical Operation

```
Tool: format_disk
Arguments: { "drive": "C:\\" }
LLM Decision: RequireHuman (Confidence: 0.10, Risk: Critical)
Reasoning: Critical operation that should have human approval
Final Result: 🔄 Human Approval Required (Critical Risk)
```

## Monitoring and Debugging

### Cache Statistics

```csharp
var provider = (LlmApprovalProvider)approvalManager.Provider;
var stats = provider.CacheStatistics;
Console.WriteLine($"Cache Hit Rate: {stats.HitRate:P2}");
Console.WriteLine($"Total Requests: {stats.TotalRequests}");
```

### Decision Logging

The system automatically logs all LLM decisions:

```
[INFO] LLM Decision for 'delete_file': RequireHuman (Confidence: 0.30, Risk: High) - 
       High risk operation with dangerous pattern detected
[INFO] Concerns identified: Dangerous pattern detected in path: \.exe$
[INFO] Fallback to human approval required
```

## Best Practices

1. **Start Conservative**: Begin with high confidence thresholds and gradually lower them as you gain confidence in the system.

2. **Monitor Decisions**: Regularly review LLM decisions and human overrides to tune your policies.

3. **Use Tool-Specific Policies**: Configure different thresholds for different tools based on their risk profiles.

4. **Test Thoroughly**: Use the MockLlmService to test various scenarios before deploying with real LLM services.

5. **Always Have Fallback**: Ensure human approval is always available as a fallback option.

6. **Cache Configuration**: Enable caching to reduce LLM API costs and improve performance for repeated operations.

This LLM-driven approval system provides intelligent automation while maintaining safety through human oversight when needed.