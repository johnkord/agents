# Agent Session Management

This document describes the persistent session management feature in AgentAlpha, which allows conversations to be saved and resumed across multiple task executions.

## Overview

AgentAlpha now supports persistent sessions that maintain conversation history and context across multiple task executions. Sessions are stored locally in a SQLite database and provide continuity for complex, multi-step workflows.

## Key Features

### 🔄 Session Persistence
- Conversations are automatically saved to `agent_sessions.db`
- Full conversation history is preserved including system prompts, user messages, and assistant responses
- Session state is updated after each task completion

### 📝 Session Creation
- Create named sessions for easy identification
- Automatic session ID generation (UUID format)
- Default naming with timestamp for unnamed sessions

### 🎯 Session Resumption
- Resume any previous session by ID
- Full conversation context is restored
- Continue from exactly where you left off

### 📊 Session Status Tracking
- **Active**: Currently in use or available for use
- **Completed**: Marked as finished
- **Archived**: Long-term storage, can still be resumed
- **Error**: Session encountered an error state

## Usage Examples

### Creating a New Session

```bash
# Create a named session for a specific project
dotnet run --session-name "Code Review Session" "Help me review this C# project"

# Output example:
💾 Created new session: Code Review Session (a1b2c3d4-e5f6-7890-abcd-ef1234567890)
📝 Task: Help me review this C# project
```

### Resuming an Existing Session

```bash
# Resume using session ID
dotnet run --session "a1b2c3d4-e5f6-7890-abcd-ef1234567890" "Continue with the security analysis"

# Output example:
🔄 Resuming session: Code Review Session (a1b2c3d4-e5f6-7890-abcd-ef1234567890)
📝 New task: Continue with the security analysis
```

### Without Sessions (Traditional Mode)

```bash
# Standard one-time execution (no persistence)
dotnet run "What time is it?"
```

## Command Line Parameters

| Parameter | Description | Example |
|-----------|-------------|---------|
| `--session-name "Name"` | Create a new named session | `--session-name "My Project"` |
| `--session "ID"` | Resume session by ID | `--session "a1b2c3d4-..."` |
| `--session-id "ID"` | Alternative syntax for session resume | `--session-id "a1b2c3d4-..."` |

## Technical Architecture

### Data Storage
- **Database**: SQLite (`agent_sessions.db`)
- **Location**: Same directory as the AgentAlpha executable
- **Schema**: Sessions table with JSON conversation state

### Session Model
```csharp
public class AgentSession
{
    public string SessionId { get; set; }           // UUID
    public string Name { get; set; }                // Human-readable name
    public DateTime CreatedAt { get; set; }         // Creation timestamp
    public DateTime LastUpdatedAt { get; set; }     // Last modification
    public string ConversationState { get; set; }   // JSON serialized messages
    public SessionStatus Status { get; set; }       // Active, Completed, Archived, Error
    // ... additional metadata fields
}
```

### Conversation State
Sessions store the complete conversation history as JSON:
```json
[
    {"role": "system", "content": "You are AgentAlpha..."},
    {"role": "user", "content": "Help me with my project"},
    {"role": "assistant", "content": "I'll help you..."},
    {"role": "user", "content": "Continue with next step"}
]
```

## Integration with Existing Features

### Conversation Manager
- Extended `IConversationManager` interface with session support
- `InitializeFromSession()` method loads conversation history
- `GetCurrentMessages()` provides state for session saving

### Task Executor
- Session loading and creation logic
- Automatic session state saving after task completion
- Backwards compatibility with non-session usage

### Error Handling
- Graceful degradation if session not found
- SQLite connection and permission error handling
- Session corruption recovery

## Best Practices

### Session Naming
- Use descriptive names for easy identification
- Include project or topic information
- Consider using timestamps for organization

### Session Management
- Sessions are automatically saved, no manual intervention needed
- Session IDs are displayed for future reference
- Consider archiving completed sessions periodically

### Multi-Step Workflows
Sessions are ideal for:
- **Code Reviews**: Multi-file analysis with ongoing discussion
- **Project Planning**: Iterative planning and refinement
- **Learning Sessions**: Building knowledge across multiple topics
- **Debugging**: Step-by-step problem analysis

## Limitations and Considerations

### Current Limitations
- Manual session listing not yet implemented (planned for future CLI enhancement)
- No session export/import functionality
- Database is local only (no cloud sync)

### Performance Considerations
- Session size grows with conversation length
- SQLite performance is suitable for typical usage
- Consider session archival for very long conversations

### Security Considerations
- Sessions are stored locally in plain text
- No encryption of session data
- Database file should be protected with file system permissions

## Future Enhancements

### Planned Features
- Session listing and management CLI commands
- Session export/import functionality
- Session search and filtering
- Session metadata and tagging
- Session sharing capabilities
- Cloud storage integration

### API Extensions
- REST API for session management
- Session templates and cloning
- Bulk session operations
- Session analytics and reporting

## Troubleshooting

### Common Issues

**"Session not found" Error**
- Verify the session ID is correct
- Check if the database file exists
- Ensure proper file permissions

**Database Permission Errors**
- Check write permissions in the application directory
- Ensure the database file is not locked by another process
- Verify SQLite is properly installed

**Session State Corruption**
- Session will gracefully fall back to new conversation
- Check application logs for detailed error information
- Database file can be safely deleted to reset all sessions

### Debug Information
- Enable verbose logging with `--verbose` flag
- Check `agent_sessions.db` with SQLite browser tools
- Review conversation state JSON for structure issues

## Examples and Use Cases

### Software Development Workflow
```bash
# Start a new development session
dotnet run --session-name "Feature Development" "Help me plan a new authentication feature"

# Later - continue with implementation
dotnet run --session "session-id" "Let's implement the login functionality"

# Later - add testing
dotnet run --session "session-id" "Now help me write unit tests for this feature"
```

### Document Analysis Project
```bash
# Begin document review
dotnet run --session-name "Contract Review" "Analyze this contract for key terms"

# Follow up analysis
dotnet run --session "session-id" "Check for any compliance issues"

# Final summary
dotnet run --session "session-id" "Create a summary report of findings"
```

This persistent session capability transforms AgentAlpha from a single-task assistant into a continuous collaboration partner that maintains context and builds on previous conversations.