# AgentAlpha V2 Configuration Examples

This document provides configuration examples for different use cases of AgentAlpha V2.

## Basic Configuration

Minimal configuration for standard operation:

```bash
# Required
export OPENAI_API_KEY="your-api-key-here"

# Optional (with defaults shown)
export MCP_TRANSPORT="stdio"              # or "http", "sse"
export AGENT_MODEL="gpt-4.1"             # Main model for conversations
export MAX_ITERATIONS="10"               # Maximum conversation iterations
```

## Fast-Path Optimized Configuration

For workloads with many simple tasks:

```bash
export OPENAI_API_KEY="your-api-key-here"
export AGENT_MODEL="gpt-4.1-mini"        # Lighter model for fast responses
export AGENT_LIGHT_MODEL="gpt-4.1-nano"  # Ultra-light for routing decisions
export MAX_ITERATIONS="5"                # Reduce iterations for simple tasks
```

## Complex Task Configuration with Chained Planner

For complex, multi-step tasks requiring sophisticated planning:

```bash
export OPENAI_API_KEY="your-api-key-here"
export AGENT_MODEL="gpt-4.1"
export PLANNING_MODEL="o1"               # Use reasoning model for planning

# Enable chained planner
export USE_CHAINED_PLANNER="true"
export CHAINED_PLANNER_MODEL="gpt-4.1-nano"      # Fast model for analysis/outline
export CHAINED_PLANNER_DETAIL_MODEL="gpt-4.1"    # Powerful model for detailed planning
export CHAINED_PLANNER_MAX_TOKENS="2048"

# Plan quality settings
export PLAN_QUALITY_TARGET="0.9"         # High quality requirement (0.0-1.0)
export MAX_PLAN_REFINEMENTS="5"          # Allow more refinement iterations
```

## Development/Testing Configuration

For development and testing environments:

```bash
export OPENAI_API_KEY="your-api-key-here"
export AGENT_MODEL="gpt-4.1-mini"        # Use cheaper model for testing
export MAX_ITERATIONS="3"                # Limit iterations to save costs

# Verbose logging
export LOG_LEVEL="Debug"
export VERBOSE_OPENAI="true"
export VERBOSE_TOOLS="true"

# Disable production features
export USE_CHAINED_PLANNER="false"
export PLAN_QUALITY_TARGET="0.5"         # Lower quality acceptable for testing
```

## Production Configuration

Recommended settings for production deployments:

```bash
export OPENAI_API_KEY="${OPENAI_API_KEY_FROM_SECRETS}"
export AGENT_MODEL="gpt-4.1"
export PLANNING_MODEL="o1-mini"          # Balance performance and cost

# Chained planner for complex tasks only
export USE_CHAINED_PLANNER="true"
export CHAINED_PLANNER_MODEL="gpt-4.1-mini"
export CHAINED_PLANNER_DETAIL_MODEL="gpt-4.1"
export PLAN_QUALITY_TARGET="0.8"
export MAX_PLAN_REFINEMENTS="3"

# Conservative iteration limits
export MAX_ITERATIONS="10"
export MAX_CONVERSATION_MESSAGES="100"   # Limit context size

# Tool filtering for security
export MCP_TOOL_WHITELIST="read_file,write_file,list_directory,get_current_time"
export MCP_TOOL_BLACKLIST="run_command,delete_file"

# Session service
export SESSION_SERVICE_URL="http://session-service:5001"

# Logging
export LOG_LEVEL="Information"
export VERBOSE_OPENAI="false"           # Reduce log volume in production
export VERBOSE_TOOLS="false"
```

## HTTP Transport Configuration

When using HTTP/SSE transport for MCP:

```bash
export OPENAI_API_KEY="your-api-key-here"
export MCP_TRANSPORT="http"
export MCP_SERVER_URL="http://localhost:8080"
export AGENT_MODEL="gpt-4.1"
```

## Tool Filtering Examples

### Whitelist Only Safe Tools
```bash
# Only allow specific tools
export MCP_TOOL_WHITELIST="read_file,search_text,word_count,get_current_time"
```

### Blacklist Dangerous Tools
```bash
# Block specific tools but allow all others
export MCP_TOOL_BLACKLIST="run_command,delete_file,write_file"
```

### Combined Filtering
```bash
# Whitelist takes precedence, then blacklist filters the whitelist
export MCP_TOOL_WHITELIST="read_file,write_file,run_command,list_directory"
export MCP_TOOL_BLACKLIST="run_command"  # This removes run_command from whitelist
```

## Environment-Specific Examples

### Docker Compose
```yaml
version: '3.8'
services:
  agentalpha:
    image: agentalpha:v2
    environment:
      - OPENAI_API_KEY=${OPENAI_API_KEY}
      - AGENT_MODEL=gpt-4.1
      - USE_CHAINED_PLANNER=true
      - PLAN_QUALITY_TARGET=0.8
      - SESSION_SERVICE_URL=http://session-service:5001
```

### Kubernetes ConfigMap
```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: agentalpha-config
data:
  AGENT_MODEL: "gpt-4.1"
  PLANNING_MODEL: "o1-mini"
  USE_CHAINED_PLANNER: "true"
  PLAN_QUALITY_TARGET: "0.8"
  MAX_PLAN_REFINEMENTS: "3"
  MAX_ITERATIONS: "10"
```

### .env File for Local Development
```bash
# .env file (remember to add to .gitignore!)
OPENAI_API_KEY=sk-...your-key-here...
AGENT_MODEL=gpt-4.1-mini
USE_CHAINED_PLANNER=false
MAX_ITERATIONS=5
LOG_LEVEL=Debug
VERBOSE_OPENAI=true
```

## Performance Tuning Guide

### For Latency-Sensitive Applications
- Use `gpt-4.1-mini` or `gpt-4.1-nano` as AGENT_MODEL
- Set `MAX_ITERATIONS=3-5`
- Disable chained planner: `USE_CHAINED_PLANNER=false`
- Lower plan quality: `PLAN_QUALITY_TARGET=0.6`

### For Accuracy-Critical Applications
- Use `gpt-4.1` as AGENT_MODEL
- Use `o1` or `o3` as PLANNING_MODEL
- Enable chained planner: `USE_CHAINED_PLANNER=true`
- Higher plan quality: `PLAN_QUALITY_TARGET=0.9`
- More refinements: `MAX_PLAN_REFINEMENTS=5`

### For Cost Optimization
- Use lighter models where possible
- Limit iterations: `MAX_ITERATIONS=5`
- Limit conversation history: `MAX_CONVERSATION_MESSAGES=50`
- Use tool whitelisting to prevent unnecessary tool calls
- Monitor token usage with verbose logging in dev/staging

## Troubleshooting Common Issues

### "Invalid model value" Error
Ensure AGENT_MODEL is one of:
- gpt-4.1, gpt-4, gpt-4-turbo, gpt-4.1-nano, gpt-4.1-mini
- o1, o1-mini, o1-preview, o3, o3-mini

### "MCP_SERVER_URL is required" Error
When using `MCP_TRANSPORT=http`, you must also set:
```bash
export MCP_SERVER_URL="http://your-mcp-server:port"
```

### Plan Quality Not Meeting Target
- Increase `MAX_PLAN_REFINEMENTS`
- Use more powerful models for planning
- Adjust `PLAN_QUALITY_TARGET` to realistic levels

### Fast-Path Not Being Used
- Check task router confidence with verbose logging
- Ensure simple tasks are truly simple (single tool or query)
- Consider adjusting routing model or prompts
