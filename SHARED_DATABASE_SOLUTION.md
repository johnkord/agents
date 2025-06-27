# Shared Session Database Solution

## Problem
The Approval Service and AgentAlpha services needed to share access to the `agent_sessions.db` database file, but each service was creating the database in its own directory, making it impossible for them to access each other's session data when deployed independently.

## Solution Overview
Implemented a shared database approach where both services use the same database file through:
1. **Environment Variable Configuration**: `AGENT_SESSION_DB_PATH` points both services to the same database location
2. **Shared Default Path**: Changed from service-specific `./app/data/` to shared `./data/` location
3. **Explicit Configuration**: ApprovalService explicitly configures SessionManager with shared database path
4. **Container Shared Volumes**: Docker and Kubernetes configurations mount shared storage

## Changes Made

### Code Changes
1. **SessionManager.cs**: Updated default database path from `./app/data/agent_sessions.db` to `./data/agent_sessions.db`
2. **ApprovalService Program.cs**: Added explicit SessionManager configuration with shared database path
3. **ApprovalStore.cs**: Updated default path to use shared `./data/` directory for consistency

### Infrastructure Changes
1. **Docker Files**: Both services create shared `/app/data` directory with proper permissions
2. **Helm Charts**: 
   - Created shared persistent volume claim with `ReadWriteMany` access mode
   - Both services mount the shared volume instead of separate volumes
   - Added environment variable configuration for database path
3. **Values.yaml**: Added `sharedData` configuration section

### Documentation Updates
1. **agent-session-management.md**: 
   - Added database sharing section explaining the architecture
   - Added deployment considerations with Docker Compose and Kubernetes examples
   - Updated troubleshooting section with shared database issues

## Configuration

### Environment Variables
- `AGENT_SESSION_DB_PATH`: Path to shared session database (default: `./data/agent_sessions.db`)
- `APPROVAL_SERVICE_DB_PATH`: Path to approval service database (default: `./data/approval_service.db`)

### Docker Deployment
```yaml
version: '3.8'
services:
  agent-alpha:
    environment:
      - AGENT_SESSION_DB_PATH=/app/data/agent_sessions.db
    volumes:
      - shared_data:/app/data
  
  approval-service:
    environment:
      - AGENT_SESSION_DB_PATH=/app/data/agent_sessions.db
    volumes:
      - shared_data:/app/data

volumes:
  shared_data:
```

### Kubernetes Deployment
- Shared persistent volume claim with `ReadWriteMany` access
- Both services mount the same volume at `/app/data`
- Environment variables configure the shared database path

## Testing
- Created `SharedDatabaseTests.cs` to verify two SessionManager instances can access the same database
- All existing tests continue to pass
- Helm chart validation confirms shared volume configuration

## Benefits
1. **Independent Deployment**: Services can be deployed separately while sharing session data
2. **Data Consistency**: Single source of truth for session information
3. **Scalability**: Multiple instances can access the same session database
4. **Backwards Compatibility**: Existing code continues to work with environment variable configuration

## Migration Path
1. Set `AGENT_SESSION_DB_PATH` environment variable to shared location
2. Deploy with shared volume configuration
3. Existing databases can be copied to the shared location during migration