# Enhanced Activity Logging

This document describes the enhanced activity logging system that provides comprehensive audit trails for all OpenAI and tool interactions.

## Overview

The enhanced activity logging system captures detailed information about every step in the agent's operation, providing the comprehensive audit trail requested in issue #67. Users can now see virtually every step in the operation and the data that was gathered/sent in each step.

## Key Features

### 1. Comprehensive OpenAI Request/Response Logging

**Before (Basic Logging):**
```json
{
  "Model": "gpt-4o",
  "MessageCount": 3,
  "ToolCount": 10,
  "ToolNames": ["github_get_pull_request_diff", "github_get_pull_request_files", ...]
}
```

**After (Enhanced Logging):**
```json
{
  "Model": "gpt-4o",
  "MessageCount": 3,
  "ToolCount": 10,
  "ToolNames": ["github_get_pull_request_diff", "github_get_pull_request_files", ...],
  "ToolChoice": "auto",
  "FullRequest": {
    "Model": "gpt-4o",
    "Messages": [
      {"role": "system", "content": "You are a helpful assistant..."},
      {"role": "user", "content": "Please review this PR..."},
      {"role": "assistant", "content": "I'll help you review..."}
    ],
    "Tools": [
      {"Name": "github_get_pull_request_diff", "Description": "Get diff...", "ParameterCount": 156},
      {"Name": "github_get_pull_request_files", "Description": "Get files...", "ParameterCount": 142}
    ],
    "ToolChoice": "auto"
  }
}
```

### 2. Detailed Tool Input/Output Logging

**Before (Basic Logging):**
```json
{
  "ToolName": "github_get_pull_request_files",
  "Arguments": {"owner": "microsoft", "repo": "mssql-python", "pullNumber": 104}
}
```

**After (Enhanced Logging):**
```json
{
  "ToolName": "github_get_pull_request_files",
  "Arguments": {"owner": "microsoft", "repo": "mssql-python", "pullNumber": 104},
  "FullInput": {
    "ToolName": "github_get_pull_request_files",
    "ArgumentCount": 3,
    "ArgumentKeys": ["owner", "repo", "pullNumber"],
    "ArgumentValues": {
      "owner": "microsoft",
      "repo": "mssql-python", 
      "pullNumber": "104"
    }
  }
}
```

**Tool Result Enhanced Logging:**
```json
{
  "ToolName": "github_get_pull_request_files",
  "Success": true,
  "ResultLength": 337,
  "HasContent": true,
  "FullOutput": {
    "ResultText": "Files changed in PR: src/main.py, tests/test_main.py, README.md",
    "ContentBlocks": [
      {"Type": "TextContentBlock", "Content": "File list retrieved successfully"}
    ],
    "IsError": false,
    "Metadata": "{\"files_count\": 3, \"additions\": 25, \"deletions\": 12}"
  }
}
```

### 3. Comprehensive Error Logging

Enhanced error logging now includes:
- Full stack traces (truncated for size)
- Error context and debugging information
- Failure timestamps and environment details

```json
{
  "ToolName": "github_get_pull_request_files",
  "Arguments": {"owner": "microsoft", "repo": "mssql-python", "pullNumber": 104},
  "ErrorType": "HttpRequestException",
  "ErrorMessage": "API rate limit exceeded",
  "StackTrace": "at HttpClient.SendAsync...",
  "FailureContext": {
    "ToolName": "github_get_pull_request_files",
    "ArgumentCount": 3,
    "ArgumentKeys": ["owner", "repo", "pullNumber"],
    "Timestamp": "2025-06-25T13:07:42.123Z"
  }
}
```

## Configuration Options

The enhanced logging is configurable through the `ActivityLoggingConfig` class:

```csharp
public class ActivityLoggingConfig
{
    /// <summary>
    /// Enable verbose logging that includes full OpenAI request/response data
    /// Default: true
    /// </summary>
    public bool VerboseOpenAI { get; set; } = true;
    
    /// <summary>
    /// Enable verbose logging that includes full tool input/output data
    /// Default: true
    /// </summary>
    public bool VerboseTools { get; set; } = true;
    
    /// <summary>
    /// Maximum size of data to log before truncation (in characters)
    /// Default: 50000
    /// </summary>
    public int MaxDataSize { get; set; } = 50000;
    
    /// <summary>
    /// Maximum size for individual string fields before truncation
    /// Default: 5000
    /// </summary>
    public int MaxStringSize { get; set; } = 5000;
    
    /// <summary>
    /// Maximum number of messages to include in OpenAI request logging
    /// Default: 50
    /// </summary>
    public int MaxMessagesInLog { get; set; } = 50;
}
```

### Usage Examples

**Basic Configuration (Environment Variable):**
```bash
# Enable verbose logging (default)
export VERBOSE_OPENAI=true
export VERBOSE_TOOLS=true

# Reduce data size limits for production
export MAX_DATA_SIZE=10000
export MAX_STRING_SIZE=1000
```

**Programmatic Configuration:**
```csharp
var config = new AgentConfiguration();
config.ActivityLogging.VerboseOpenAI = true;
config.ActivityLogging.VerboseTools = true;
config.ActivityLogging.MaxDataSize = 25000;
```

**Disable Verbose Logging for Production:**
```csharp
var config = new AgentConfiguration();
config.ActivityLogging.VerboseOpenAI = false;  // Basic OpenAI logging only
config.ActivityLogging.VerboseTools = false;   // Basic tool logging only
```

## Data Management Features

### Automatic Truncation

Large data payloads are automatically truncated to prevent excessive log sizes:

```json
{
  "truncated": true,
  "originalSize": 125000,
  "maxSize": 50000,
  "data": "Original data content... [TRUNCATED]"
}
```

### String Truncation

Individual string fields are truncated with clear indicators:

```
"This is a very long string that exceeds the maximum... [TRUNCATED]"
```

## Backward Compatibility

The enhanced logging system maintains full backward compatibility:

- Existing code continues to work without changes
- Basic logging is still available when verbose options are disabled
- All existing activity types and interfaces remain unchanged
- Enhanced data is additive - no existing fields are modified

## Benefits

1. **Comprehensive Audit Trail**: See every step of the operation with full context
2. **Debugging Support**: Detailed error information with stack traces and context
3. **Compliance**: Complete record of all AI and tool interactions
4. **Performance Monitoring**: Token usage and timing information
5. **Configurable Verbosity**: Adjust detail level for different environments
6. **Size Management**: Automatic truncation prevents oversized logs

## Activity Log Example

A typical enhanced activity log for a PR review task now includes:

```json
[
  {
    "ActivityType": "OpenAI_Request",
    "Description": "Sending request to OpenAI API",
    "Data": {
      "Model": "gpt-4o",
      "MessageCount": 3,
      "ToolCount": 10,
      "FullRequest": {
        "Model": "gpt-4o",
        "Messages": [...],
        "Tools": [...],
        "ToolChoice": "auto"
      }
    }
  },
  {
    "ActivityType": "OpenAI_Response", 
    "Description": "Received response from OpenAI API",
    "Data": {
      "Model": "gpt-4o",
      "OutputItemCount": 2,
      "HasToolCalls": true,
      "FullResponse": {
        "Output": [...],
        "Usage": {
          "InputTokens": 1245,
          "OutputTokens": 387,
          "TotalTokens": 1632
        }
      }
    }
  },
  {
    "ActivityType": "Tool_Call",
    "Description": "Executing MCP tool: github_get_pull_request_files",
    "Data": {
      "ToolName": "github_get_pull_request_files",
      "Arguments": {...},
      "FullInput": {
        "ArgumentCount": 3,
        "ArgumentKeys": [...],
        "ArgumentValues": {...}
      }
    }
  },
  {
    "ActivityType": "Tool_Result",
    "Description": "MCP tool result: github_get_pull_request_files", 
    "Data": {
      "ToolName": "github_get_pull_request_files",
      "Success": true,
      "ResultLength": 337,
      "FullOutput": {
        "ResultText": "...",
        "ContentBlocks": [...],
        "Metadata": "..."
      }
    }
  }
]
```

This enhanced logging provides the comprehensive audit trail requested in issue #67, allowing users to see virtually every step in the operation and all data exchanged.