# Forge Phase 1 — Checkpoint 1 Review

> Smoke test results from 5 tasks run on 2026-03-18
> Model: gpt-5.4 | MCP tools: 40

## Results Summary

| # | Task | Result | Steps | Tokens | Duration | Verdict |
|---|------|--------|-------|--------|----------|---------|
| 1 | Read README.md and summarize it | ✅ Pass | 2 | 11,202 | 8.9s | Clean. Efficient. |
| 2 | Create a hello world Python script | ✅ Pass | 3 | 10,697 | 3.8s | Good — verified its work. |
| 3 | Find all TODO comments | ⚠️ Pass (wasteful) | 7 | 27,831 | 14.3s | 3 wasted bash attempts before grep. |
| 4 | Fix syntax error in /tmp/buggy.py | ✅ Pass | 4 | 14,365 | 5.3s | Read→fix→verify pattern. Guardrail bypass. |
| 5 | Run dotnet build and report | ❌ Fail | 3 | 10,921 | 4.9s | Bash quoting bug — no command ever executed. |

**Checkpoint criteria: 4/5 pass.** Result: 4/5 (task 5 failed due to infrastructure bug, not model error).

---

## Critical Bugs

### BUG-1: `run_bash_command` is completely broken (blocks all commands)

**Severity:** Critical — renders the tool useless for ALL commands.

**Root cause:** `ProcessStartInfo.Arguments` on Linux splits the argument string into an argv array. Our `EscapeForBash` wraps the command in single quotes (`bash -c 'cd /path && dotnet build'`), but .NET splits this on whitespace before passing to bash, so bash receives a fragmented argv where `-c` gets `'cd` as its argument — an unterminated single quote.

**Evidence:** All 5 bash invocations across tasks 3 and 5 failed with the same error:
```
-c: line 1: unexpected EOF while looking for matching `''
```

**Fix:** Use `ProcessStartInfo.ArgumentList` instead of `Arguments`. This passes each argument as a discrete argv entry without any splitting:
```csharp
psi.ArgumentList.Add("-c");
psi.ArgumentList.Add(command);  // no escaping needed
```

**Impact:** Task 5 would have passed. Task 3 would have been 3- 4 steps shorter (10K fewer tokens).

---

### BUG-2: Guardrails path restriction is completely ineffective

**Severity:** Critical — security control doesn't work.

**Root cause:** `IsFileOperation()` checks PascalCase tool names (`"ReadFile"`, `"CreateFile"`) but MCP registers them as snake_case (`"read_file"`, `"create_file"`). The condition never matches, so path restrictions never fire.

**Evidence:** Task 4 successfully read and edited `/tmp/buggy.py` despite the workspace being the project root. No "BLOCKED" message appeared.

**Fix:** Update `IsFileOperation` to use snake_case names:
```csharp
private static bool IsFileOperation(string toolName) =>
    toolName is "read_file" or "create_file" or "list_directory" 
        or "grep_search" or "replace_string_in_file" or "file_search";
```

And update the `RunBashCommand` check:
```csharp
if (string.Equals(toolName, "run_bash_command", StringComparison.OrdinalIgnoreCase))
```

---

## Efficiency Issues

### EFF-1: grep_search runs from MCP server CWD, not the workspace

**Problem:** `grep_search` uses `Directory.GetCurrentDirectory()` which is the MCP server's working directory. The agent has no way to tell it where to search. Results return paths relative to the MCP server CWD (e.g., `Tools/ManageTodosTool.cs`), not the agent's workspace.

**Impact:** Search results are correct only when the MCP server happens to be running from the right directory. No way for the agent to search a specific subtree.

**Fix:** Add an optional `rootPath` parameter to `grep_search`. Default to CWD for backward compat.

### EFF-2: Agent retries failing bash commands without changing strategy

**Problem:** In task 3, the agent tried the exact same `grep` bash command 3 times (steps 2, 3, 4), getting the same error each time. The system prompt says "try a different strategy after 2 failed attempts" but model didn't follow this. It wasn't until step 5 that it fell back to `grep_search`.

**Impact:** 3 wasted steps = ~10K tokens burned, ~6 seconds of latency for zero gain.

**Possible fixes:**
- Detect repeated identical tool calls in the agent loop and inject a nudge: "You've tried this exact tool call before and it failed. Try a different approach."
- More prominently surface the error in the tool result. Currently the bash error is cryptic (`unexpected EOF while looking for matching...`) — the agent doesn't realize it's an infrastructure bug vs. a command issue.

### EFF-3: run_bash_command lacks a working directory parameter

**Problem:** Commands always run from the MCP server's CWD. The agent compensates by prepending `cd /workspace &&` to every command, which wastes tokens and (combined with BUG-1) creates fragile multi-command strings.

**Fix:** Add a `workingDirectory` parameter to `run_bash_command`.

### EFF-4: No running token count visible during execution

**Problem:** Token usage per step appears only in debug logs. During a long run, the user has no visibility into budget consumption until the final summary.

**Fix:** Show a running total in the step info line: `Step 3: 1 tool call, 4,010 tokens (cumulative: 15,403 / 200,000)`

---

## Console Output Issues

### OUT-1: Final answer bleeds into log line

**Problem:** The model's final text streams directly to stdout, immediately followed by the Serilog `[INF] Agent completed...` line with no separator.

Example:
```
...the `ManageTodos` tool name/description, not TODO comments.[19:44:51 INF] Agent completed in 7 steps
```

**Fix:** Emit a newline before the "Agent completed" log and before the result summary block.

### OUT-2: Streaming shows nothing during tool-call-only steps

**Problem:** When the model emits tool calls but no text (which is most steps in practice), the user sees only `⚡ tool_name ✓` with no indication of what the model is thinking. Steps 0-5 of task 3 have `thought=""` — the model produced zero text.

**Possible fixes:**
- Show a brief step header: `[Step 2]` before tool call indicators
- Optionally display the tool arguments in compact form to show what's happening

---

## Observations (Not Bugs)

### OBS-1: The model is well-behaved on verification

Tasks 2 and 4 both show the agent reading back the file after creating/editing it. This matches the "verify after modify" principle without us forcing it. The system prompt instruction is working.

### OBS-2: Token cost is dominated by prompt tokens (95%+)

| Task | Prompt Tokens | Completion Tokens | Ratio |
|------|--------------|-------------------|-------|
| 1 | 10,741 | 461 | 96% prompt |
| 2 | 10,590 | 107 | 99% prompt |
| 3 | 27,256 | 575 | 98% prompt |
| 4 | 14,213 | 152 | 99% prompt |
| 5 | 10,704 | 217 | 98% prompt |

The context (system prompt + 40 tool descriptions + conversation history) is the dominant cost. This validates the design's emphasis on context engineering and future work on observation pruning. The 40 tool descriptions alone are probably ~3K tokens of overhead per step.

### OBS-3: Most steps have empty `thought` field

The model goes straight to tool calls without emitting reasoning text. This is token-efficient but makes debugging harder — you can't see why it chose a particular tool or approach. Future phases should consider requiring brief reasoning before tool calls for complex tasks (adaptive depth from the design).

### OBS-4: grep_search only found matches in MCP server code, not the workspace

It searched `Tools/ManageTodosTool.cs` — a file in the MCP server project, not the main codebase. This is EFF-1 in action. The agent's actual workspace code was never searched.

---

## Priority Order for Fixes

1. **BUG-1** (bash broken) — blocks task 5, causes waste in task 3. Fix immediately. ✅ FIXED
2. **BUG-2** (guardrails bypass) — security control is nonfunctional. Fix immediately. ✅ FIXED
3. **EFF-3** (bash working directory) — eliminates the `cd && cmd` pattern that complicates commands. ✅ FIXED
4. **EFF-1** (grep root path) — makes search actually useful across the workspace. ✅ FIXED
5. **OUT-1** (output formatting) — quick cosmetic fix. ✅ FIXED
6. **EFF-2** (retry detection) — duplicate tool call detection added. ✅ FIXED
7. **EFF-4** (running token count) — cumulative token display per step. ✅ FIXED
8. **OUT-2** (step headers) — deferred (not blocking).

---

## Post-Checkpoint: What's Next?

### Data from Phase 2 runs (after fixes)

| Task | Steps | Tokens | Duration | Notes |
|------|-------|--------|----------|-------|
| Error handling (40 tools) | 15 | 118,509 | 65.4s | Pre-tool-registry baseline |
| CreateDirectory (6 core + find_tools) | 8 | 60,904 | 31.3s | **49% fewer tokens, 52% faster** |

### Three candidate next steps

**Option A: REPO.md generation (Phase 3).** Eliminates 2-4 exploration steps / 6-10K tokens per task. Requires building a build-file parser. High value for unfamiliar repos, lower value for the current self-referential testing pattern.

**Option B: Harder smoke tests.** Stress-test Plan→Act→Verify, history compression, and re-plan nudging on multi-file refactoring, debugging, and ambiguous tasks.

**Option C: Self-improvement loop (CHOSEN).** Point Forge at its own codebase. Design progressively harder tasks where the agent improves itself. Generates data about complex task handling while producing immediately useful improvements. Validated by research: Voyager's skill library, Reflexion's verbal reinforcement, Tool-R0's self-evolving curricula.
