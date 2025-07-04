# Conversation Management Improvements

This document describes the conversation management optimizations implemented to address token usage and context preservation issues in multi-turn agent sessions.

## Issues Addressed

### 1. Growing Token Usage
- **Problem**: Conversation history grows linearly with each interaction, causing token usage to increase from 255 to 1121+ tokens across just 4 requests
- **Solution**: Added configurable conversation length management via `MaxConversationMessages` setting

### 2. Redundant Tool Result Formatting  
- **Problem**: Tool results were being added twice to conversation (once as assistant message, once embedded in user message)
- **Solution**: Simplified tool result formatting to use a single "Tool results:" message with cleaner continuation prompt

### 3. Context Loss in Long Sessions
- **Problem**: Very long conversation histories can cause the model to lose focus on earlier context
- **Solution**: Implemented conversation optimization that keeps system messages while truncating older conversation messages

## Configuration

Add to your environment or configuration:

```bash
# Set maximum conversation messages (0 = unlimited, default)
export MAX_CONVERSATION_MESSAGES=20
```

Or in `AgentConfiguration`:

```csharp
var config = new AgentConfiguration
{
    MaxConversationMessages = 20  // Keep last 20 messages + system messages
};
```

## Features

### Conversation Length Management
- Automatically truncates older messages when conversation exceeds `MaxConversationMessages`
- Always preserves system messages
- Keeps most recent conversation messages for context continuity

### Improved Tool Result Formatting
- Cleaner "Tool results:" prefix instead of redundant formatting
- Simplified continuation prompts
- Reduced message duplication

### Conversation Statistics
- New `GetConversationStatistics()` method provides monitoring data:
  - Total message count
  - Messages by role (system/user/assistant)
  - Estimated token usage

### Enhanced Logging
- Debug logging for conversation optimization events
- Token usage monitoring in task execution

## Usage Examples

### Basic Usage (No Changes Required)
```csharp
// Existing code continues to work unchanged
conversationManager.AddToolResults(toolSummaries);
```

### With Conversation Limits
```csharp
var config = new AgentConfiguration 
{ 
    MaxConversationMessages = 15  // Limit to 15 messages
};
var conversationManager = new ConversationManager(openAI, logger, config);
```

### Monitoring Conversation Health
```csharp
var stats = conversationManager.GetConversationStatistics();
logger.LogInformation("Conversation: {Messages} messages, ~{Tokens} tokens", 
    stats.TotalMessages, stats.EstimatedTokens);
```

## Impact

These changes provide:
- **Reduced token usage** for long sessions
- **Better context preservation** through intelligent message management  
- **Cleaner conversation flow** with improved tool result formatting
- **Monitoring capabilities** for conversation health
- **Backward compatibility** - existing code continues to work unchanged

## Testing

New test suite `ConversationOptimizationTests` validates:
- Conversation length optimization
- Proper statistics calculation
- Improved tool result formatting
- Backward compatibility