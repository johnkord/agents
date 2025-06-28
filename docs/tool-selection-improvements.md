# Tool Selection and Activity Logging Improvements

## Problem Analysis

The agent failed on the task "which models are available through openai?" because:

1. **Poor Web Search Detection**: The `ShouldIncludeWebSearch` method only looked for explicit web/search keywords
2. **Incorrect Tool Matching**: Selected `openai_list_vector_stores` instead of recognizing need for current information
3. **Insufficient Activity Logging**: Limited debugging information about tool selection reasoning

## Improvements Made

### 1. Enhanced Web Search Detection

**Before**: Only checked for explicit web search keywords:
```csharp
var webSearchKeywords = new[] { 
    "web", "search", "internet", "online", "news", "current", "latest", 
    "recent", "today", "real-time", "live", "browse", "website", "url", 
    "google", "find", "what's happening", "breaking", "update", "trending"
};
```

**After**: Added keywords that indicate need for current information:
```csharp
// Keywords that indicate need for current/up-to-date information
var currentInfoKeywords = new[] {
    "available", "which", "what", "list", "models", "options", "versions",
    "supported", "offerings", "plans", "pricing", "features", "capabilities",
    "services", "apis", "endpoints", "status", "working", "active"
};
```

### 2. Improved Tool Selection Reasoning

Enhanced rejection reasoning to prioritize web search when current information is needed:

```csharp
// If task requires current info but this isn't web search, prioritize web search
if (analysis.RequiresCurrentInfo && !toolLower.Contains("web_search_preview") && !toolLower.Contains("search"))
{
    return "Task requires current information - web search prioritized";
}
```

### 3. Enhanced Activity Logging

Added detailed web search reasoning to activity logs:

```csharp
WebSearchReasoning = GetWebSearchReasoning(task),
```

This provides specific details about which keywords triggered web search inclusion.

### 4. Better Task Analysis for AI/OpenAI Queries

Enhanced detection of queries asking about available models/features:

```csharp
// If asking about available models, pricing, features, etc. - likely needs current info
if (taskLower.Contains("available") || taskLower.Contains("models") || 
    taskLower.Contains("which") || taskLower.Contains("list") ||
    taskLower.Contains("pricing") || taskLower.Contains("features") ||
    taskLower.Contains("capabilities") || taskLower.Contains("options"))
{
    keywords.AddRange(new[] { "available", "current", "latest" });
}
```

## Test Results

The original problematic task now correctly identifies the need for web search:

```
Task: 'which models are available through openai?'
✅ Web search SHOULD be included
Found keywords: available which models
```

## Test Coverage

Added test cases to `WebSearchToolTests.cs`:
- `"which models are available through openai?"` → `true`
- `"what pricing options are available?"` → `true`  
- `"list all supported versions"` → `true`
- `"which apis are active?"` → `true`
- `"what features does the service offer?"` → `true`

## Impact

These changes ensure that:
1. **Current Information Queries** are properly detected and routed to web search
2. **Activity Logs** provide clear reasoning for debugging tool selection failures
3. **OpenAI Model Queries** specifically are handled correctly
4. **Backward Compatibility** is maintained for existing functionality

The original task would now correctly include web search instead of incorrectly selecting vector store tools.