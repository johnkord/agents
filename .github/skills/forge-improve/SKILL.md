---
name: forge-improve
description: >
  Analyzes a Forge coding agent session log to identify performance issues,
  inefficiencies, and improvement opportunities. Reviews the full timeline of
  LLM calls, tool executions, failure patterns, and token usage to produce
  actionable recommendations for improving tools, the agent loop, system prompt,
  and infrastructure. Use when you have a session JSONL file and want to
  understand what happened and how to make the agent better.
---

# Forge Session Analysis & Improvement

Analyze a Forge coding agent session log and produce a critical assessment with actionable improvement recommendations.

## Input

The user provides a session identifier — either:
- A full path to a `.jsonl` session file
- A partial filename or timestamp (search in `sessions/` directory)
- "latest" or "last" (use the most recent session file)

## Step 1: Locate the Session File

```bash
# If "latest" or "last":
ls -t sessions/*.jsonl | head -1

# If partial match:
ls -t sessions/*PARTIAL*.jsonl | head -1

# The session directory can also be at:
# - sessions/
# - blueprints/coding-agent/sessions/
```

If the file cannot be found, list available sessions and ask the user to pick one.

## Step 2: Read the Full Session Log

Read the entire JSONL file. Each line is a JSON event with structure:

```json
{"event": "session_start|step|session_end|session_handoff", "ts": "ISO8601", "data": {...}}
```

### Event types:

**session_start**: `{task, model, workspace, startedAt}`
**step**: `{stepNumber, timestamp, thought, toolCalls: [{toolName, arguments, resultSummary, resultLength, isError, durationMs}], promptTokens, completionTokens, durationMs}`
**session_end**: `{success, totalSteps, totalPromptTokens, totalCompletionTokens, totalDurationMs, failureReason}`
**session_handoff**: `{task, status, stepsCompleted, maxSteps, tokensUsed, summary, filesModified, failedApproaches, lastTestOutput, nextSteps}`

## Step 3: Build the Session Timeline

Construct a chronological narrative of what happened. For each step, note:

1. **What the agent thought** (the `thought` field — this is the agent's reasoning)
2. **What tools it called** and the results (success/failure, duration, result size)
3. **Token cost** (prompt + completion tokens for that step)
4. **Cumulative budget usage** (running total vs MaxTotalTokens)
5. **Time elapsed** since session start

## Step 4: Critical Analysis

Analyze the session across these dimensions. Be specific — cite step numbers and exact data.

### 4a. Efficiency Analysis

- **Token waste**: Which steps consumed the most tokens? Were any reads or searches redundant (same file read multiple times, same query run twice)?
- **Step waste**: Did the agent take unnecessary steps? Could fewer steps have achieved the same result?
- **Observation pipeline**: Were any tool results truncated? Did the agent adapt well to truncation (e.g., used startLine/endLine)?
- **Context window**: Track prompt token growth across steps. Is the sawtooth compression working? Does it compress at the right time?

### 4b. Tool Usage Analysis

- **Tool selection**: Did the agent choose the right tool for each subtask? Were there cases where a different tool would have been more efficient?
- **Tool descriptions**: Did any tool's description mislead the agent? Did the agent fail to discover a useful tool via find_tools?
- **Tool errors**: Catalog every IsError=true tool call. What went wrong? Was it the tool's fault or the agent's usage?
- **Missing tools**: Was there a capability the agent needed but didn't have?

### 4c. Planning & Verification Quality

- **Plan quality**: Did the agent's initial plan (in the first step's thought) match what actually happened? Was it overly detailed or too vague?
- **Grounded thinking**: Did the agent predict outcomes before edits? Were predictions accurate?
- **Verification compliance**: After file-modifying tool calls, did the agent follow the verification checklist (read_file → get_errors → run_tests)?
- **RETHINK/ALTERNATIVE usage**: If the agent hit failures, did it correctly identify when to rethink vs try an alternative?

### 4d. Failure Recovery

- **Failure taxonomy accuracy**: Were failures classified correctly (StaleContext, SyntaxError, TestFailure, etc.)?
- **Progressive deepening**: If reasoning was escalated (Medium→High), did it help?
- **Consecutive failure nudges**: Were nudge messages appropriate? Did the agent respond well to them?

### 4e. Session Boundaries

- **Proactive boundary detection**: If the session approached limits, was the consolidation prompt injected? Did it help?
- **Handoff note quality**: Does the session_handoff event accurately summarize what happened? Would a resumed session have enough context to continue?
- **Lessons generated**: If the session failed, was a useful lesson generated?

## Step 5: Produce Recommendations

Based on the analysis, produce concrete, actionable recommendations. Categorize by type:

### Tool Improvements
- Specific tool bugs or description improvements
- New tools that should be added
- Tools that should be removed or merged

### System Prompt Improvements
- Prompt sections that didn't influence behavior
- Missing guidance for observed failure patterns
- Verification checklist gaps

### Agent Loop Improvements
- AgentLoop.cs changes (e.g., new nudge conditions, better boundary detection)
- Context management changes (compression timing, relevance weighting)
- Progressive deepening tuning

### Infrastructure Improvements
- EventLog improvements (missing data that would aid analysis)
- MCP server changes
- Configuration changes

## Step 6: Write the Investigation Document

Write the full analysis as a Markdown file at:

```
blueprints/coding-agent/sessions/investigations/{session-filename}-investigation.md
```

Where `{session-filename}` is the base name of the JSONL file (without `.jsonl`). Create the `investigations/` directory if it doesn't exist.

The document must follow this structure:

```markdown
# Forge Session Investigation: [short task description]

> Session: [filename]
> Date: [timestamp from session_start]
> Result: [success/failure] in [N] steps, [N] tokens, [N]s

## Summary

**Efficiency**: [grade A-F] — [one sentence justification]
**Tool Usage**: [grade A-F] — [one sentence justification]
**Verification**: [grade A-F] — [one sentence justification]

## Session Timeline

[Chronological narrative of every step — what the agent thought,
what tools it called, results, token cost, elapsed time.
Annotate with observations inline.]

## Analysis

### Efficiency
[Token waste, redundant reads, unnecessary steps — with specific step citations]

### Tool Usage
[Tool selection quality, errors, missing capabilities]

### Planning & Verification
[Plan quality, prediction accuracy, checklist compliance]

### Failure Recovery
[Failure classification accuracy, progressive deepening, nudge effectiveness]

### Session Boundaries
[Handoff note quality, proactive consolidation effectiveness]

## Recommendations

### Tool Improvements
[Specific tool changes with file paths]

### System Prompt Improvements
[Prompt changes with rationale]

### Agent Loop Improvements
[AgentLoop.cs / context management changes]

### Infrastructure Improvements
[EventLog, MCP server, config changes]

## Key Takeaways
1. [most important finding]
2. [second most important]
3. [third most important]
```

After writing the file, tell the user the path to the investigation document.

## Important Rules

- **Read the ENTIRE session log.** Do not sample or skip events. Every step matters.
- **Cite specific step numbers** when discussing issues (e.g., "Step 3 was redundant because Step 1 already read this file").
- **Be critical, not charitable.** The purpose is to find problems, not validate the design.
- **Quantify everything.** "The agent wasted tokens" is useless. "Step 4's read_file call cost 2,100 prompt tokens but the content was already in context from Step 1's read" is useful.
- **Think creatively.** Don't just report what happened — imagine how the agent *should* have behaved and what architectural changes would make that happen.
- **Reference the source code.** When recommending changes, name the specific file and method (e.g., "AgentLoop.cs Finish method should also record...").
- **Consider the research.** The design is informed by papers in `blueprints/coding-agent/research-review-2026-03.md`. Reference relevant research when it supports a recommendation.

## Source Code Reference

Key files for understanding the agent architecture:
- `blueprints/coding-agent/src/Forge.Core/AgentLoop.cs` — Main loop, failure taxonomy, lessons, progressive deepening
- `blueprints/coding-agent/src/Forge.Core/OpenAIResponsesLlmClient.cs` — Context management, sawtooth compression, adaptive window
- `blueprints/coding-agent/src/Forge.Core/SystemPrompt.cs` — System prompt with verification checklists
- `blueprints/coding-agent/src/Forge.Core/ToolExecutor.cs` — Tool execution, guardrails, observation pipeline
- `blueprints/coding-agent/src/Forge.Core/ToolRegistry.cs` — Progressive tool disclosure
- `blueprints/coding-agent/src/Forge.Core/SessionHandoff.cs` — Handoff note generation
- `blueprints/coding-agent/src/Forge.Core/RepoMapGenerator.cs` — REPO.md generation
- `blueprints/coding-agent/research-review-2026-03.md` — Research basis for design decisions
- `blueprints/coding-agent/implementation-plan.md` — Implementation status and roadmap
