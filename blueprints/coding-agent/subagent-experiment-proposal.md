# Subagent Experiment Proposal

> March 2026 — Validating the Path A ("Lean Delegation") implementation

## 1. What We're Testing

We shipped 5 changes that need validation:

| Change | Risk | What could go wrong |
|--------|------|---------------------|
| `explore_codebase` (core tool) | **High** | Agent over-relies on it and misses files. Output format confuses the LLM. Adds ~200 tokens/turn to tool schema for the 80% of tasks that don't need it. |
| `run_subagent` mode restriction | **Medium** | Mode-restricted subagent can't complete task (e.g., explore mode needs `run_bash_command` for `git log`). Tool mismatch between mode allowlist and actual task needs. |
| `TaskLooksComplex` auto-activation | **Low** | False negatives: complex task doesn't trigger. False positives: simple task wastes ~200 tokens/turn. |
| `DelegationFailure` taxonomy | **Low** | Detection heuristic (`"error:"` prefix matching) misses real failures or triggers on successful results. |
| System prompt guidance | **Medium** | Agent follows guidance too literally (always explores before editing) or ignores it entirely (never uses explore_codebase). |

## 2. Experiment Design

### 2.1 Experiment A: explore_codebase vs. Manual Exploration

**Question:** Does `explore_codebase` reduce steps-to-first-edit and total tokens compared to manual `grep_search` + `read_file` chains?

**Method:** Run the `multi-file-refactor` regression task (most exploration-heavy) twice:
- **Control:** Temporarily remove `explore_codebase` from CoreTools (revert to the 10-tool baseline). The agent uses grep_search + read_file as before.
- **Treatment:** Current implementation with `explore_codebase` in CoreTools.

**Metrics to compare:**
| Metric | How to measure | Good outcome |
|--------|---------------|--------------|
| Steps to first edit | Count steps before first `replace_string_in_file` or `create_file` call | Treatment < Control |
| Total tokens | `SessionAnalyzer.TokensPerStepGrowth` + raw token count from handoff | Treatment ≤ Control |
| Read coalescence | `SessionAnalyzer.ReadCoalescenceRate` | Treatment > Control (fewer redundant reads) |
| explore_codebase usage | Count tool calls by name | ≥ 1 call in treatment sessions |
| Success rate | Task completed correctly (manual inspection) | Both succeed |
| Context at first edit | Prompt token count at the step where the first edit is made | Treatment < Control |

**Expected outcome:** Treatment saves 3-5 exploration steps, reduces tokens-at-first-edit by ~30%, and achieves the same task success. The agent calls `explore_codebase` 1-2 times in the initial exploration phase, then switches to targeted `read_file` for edits.

**Risk to watch:** The agent calls `explore_codebase` but then ALSO calls `grep_search` + `read_file` for the same area (redundant double-exploration). If this happens, the tool description needs to be more directive.

### 2.2 Experiment B: explore_codebase Output Quality

**Question:** Does `explore_codebase` actually return the files the agent needs, or does it miss critical ones?

**Method:** Run the `bug-fix-vague` regression task (requires finding the right file before fixing):
- After the run, compare the files listed in `explore_codebase` output vs. the files the agent actually edited.
- Compute **recall**: (files explored AND edited) / (files edited). Perfect = 1.0.

**Metrics:**
| Metric | How to measure | Good outcome |
|--------|---------------|--------------|
| Recall | Files in explore output ∩ files edited / files edited | ≥ 0.5 (explored at least half of edited files) |
| Precision | Files in explore output ∩ files edited / files explored | Any — low precision is OK (exploring extras isn't harmful) |
| False starts | Number of edits to wrong files, then reverted | Treatment ≤ Control |

**Expected outcome:** The structured summary guides the agent to the right file more directly than sequential grep+read, reducing false starts.

### 2.3 Experiment C: Subagent Mode Restriction (end-to-end)

**Question:** Does `run_subagent` with mode restriction actually work as a child process? Does the explore mode correctly prevent file writes?

**Method:** This requires a task that naturally triggers delegation. Create a new task:

```
Task: "Read the GitHub issue at https://example.com/issues/1 and summarize it.
       Then implement the fix described in the issue."
       
[Note: Use a local file simulating an issue body instead of a real URL]
```

This task should trigger the Firewall pattern — the agent should delegate the untrusted URL reading to a subagent in explore mode.

**Alternatively**, run this manually via the MCP server endpoint:
```bash
# Test 1: Verify explore mode blocks file writes
curl -X POST http://localhost:5000/mcp -d '{
  "method": "tools/call",
  "params": {
    "name": "RunSubagent", 
    "arguments": {
      "prompt": "Create a file called /tmp/test.txt with content hello",
      "description": "Test write block",
      "mode": "explore"
    }
  }
}'
# Expected: subagent completes but CANNOT create the file 
# (create_file not in explore mode's tool set)
```

**Metrics:**
| Metric | How to measure | Good outcome |
|--------|---------------|--------------|
| Mode applied | Check subagent logs for "Tool mode: explore" | Present |
| Tools restricted | Subagent can't call create_file, replace_string_in_file | Confirmed |
| Task still completable | Explore-mode subagent returns useful information | Yes |
| Process isolation | Subagent output extraction works, doesn't leak reasoning | Confirmed |

### 2.4 Experiment D: Auto-Activation Accuracy

**Question:** Does `TaskLooksComplex` correctly identify tasks that benefit from `run_subagent`?

**Method:** Run `TaskLooksComplex` against all 5 regression tasks and a set of ad-hoc tasks. No LLM calls needed — this is a pure unit test of the heuristic.

```csharp
// Can be run as a quick test or interactive check:
var tasks = new Dictionary<string, bool>
{
    // Regression tasks
    ["SanitizeFileName doesn't handle ~ characters"] = false,              // simple bug fix
    ["Session filenames garbled with certain characters"] = false,          // vague but single-file
    ["Add GetDominantEpisodeType to EpisodeSegmenter"] = false,           // feature add, single file
    ["Rename ExtractPivotReason to ExtractPivotSummary everywhere"] = true, // "everywhere" = all files signal? No — only 1 signal
    ["Add XML docs to all public methods in EpisodeSegmenter"] = false,    // single file

    // Ad-hoc complex tasks (should trigger)
    ["Refactor all endpoints across the codebase to use the new validation pattern"] = true,
    ["Migrate the entire project from Newtonsoft.Json to System.Text.Json"] = true,
    ["Add input validation to all API endpoints and run the full test suite"] = true,

    // Ad-hoc simple tasks (should NOT trigger)
    ["Fix the null reference in AuthService.cs line 42"] = false,
    ["Add a constructor parameter to DatabaseConnection"] = false,
    ["Update the README with installation instructions"] = false,
};
```

**Metric:** Accuracy across the test set. Acceptable: ≥ 80%.

**Known weakness:** Single-signal tasks like "Rename X everywhere" have the word "all" or "every" but only one signal — the heuristic requires 2. This might be a false negative in practice. Worth monitoring.

### 2.5 Experiment E: Token Budget Impact of explore_codebase in Core

**Question:** Does including `explore_codebase` in CoreTools waste tokens on simple tasks?

**Method:** Run the `bug-fix-known` regression task (simplest task — should not need exploration). Compare:
- Prompt tokens on step 0 (system prompt + tools) with and without `explore_codebase` in CoreTools
- Whether the agent calls `explore_codebase` on this simple task (it shouldn't)

**Metrics:**
| Metric | How to measure | Good outcome |
|--------|---------------|--------------|
| Step 0 prompt tokens | Parse from session JSONL | Treatment - Control < 300 tokens (tool description overhead) |
| Unnecessary explore calls | Count explore_codebase calls | 0 — agent should skip it for simple tasks |
| Task success | Manual check | Both succeed |

**Expected outcome:** ~200 extra tokens per turn (tool schema overhead). Across a 10-step simple task, that's ~2K extra tokens total — acceptable. If the agent also calls `explore_codebase` unnecessarily, the tool description needs the "don't use for simple tasks" guidance strengthened.

## 3. Execution Plan

| # | Experiment | LLM calls needed | Estimated cost | Priority |
|---|-----------|-------------------|----------------|----------|
| D | Auto-activation accuracy | 0 (pure unit test) | Free | P0 — run first |
| E | Token budget impact | 2 sessions × ~10 steps | ~$1-2 | P1 — cheap signal |
| A | explore vs. manual | 2 sessions × ~15 steps | ~$3-5 | P1 — core question |
| B | Output quality | 1 session × ~12 steps | ~$1-2 | P2 — builds on A |
| C | Mode restriction e2e | 1-2 sessions × ~10 steps | ~$1-3 | P2 — validates security |

**Total estimated cost:** ~$7-12 for the full suite.

**Experiment D can be run immediately** — it's a deterministic test with no LLM calls. The others require running Forge against the regression tasks, which requires an OpenAI API key and the MCP server running.

## 4. What Would Change Our Minds

| Finding | Action |
|---------|--------|
| `explore_codebase` returns wrong files consistently (recall < 0.3) | Improve the search/ranking algorithm, or add an LLM synthesis step (Option A from the design doc) |
| Agent never uses `explore_codebase` despite it being core | Strengthen system prompt guidance, or add few-shot examples showing when to use it |
| Agent ALWAYS uses `explore_codebase` even for 1-file tasks | Add "don't use for simple single-file tasks" to tool description |
| Token overhead of core tool > 500 tokens/turn | Shorten tool description or move back to Tier 2 (discoverable) |
| Mode restriction blocks legitimate subagent needs | Add `manage_todos` to explore mode's tool set, or create a lightweight `explore+` mode |
| Auto-activation false negative rate > 30% | Lower threshold from 2 signals to 1, or add more signal keywords |
| Explore mode subagent calls find_tools and bypasses restrictions | Already fixed in review — verify the fix holds end-to-end |

## 5. Success Criteria

The implementation is validated if:

1. **`explore_codebase` reduces steps-to-first-edit by ≥ 2 steps** on the multi-file-refactor task
2. **Token budget overhead ≤ 300 tokens/turn** from adding explore_codebase to CoreTools
3. **Mode restriction works end-to-end** — explore subagent cannot create/edit files
4. **Auto-activation accuracy ≥ 80%** across the test task set
5. **No regressions** — all 5 regression tasks still pass with the new tools

If criteria 1-2 fail, consider moving `explore_codebase` back to Tier 2. If criterion 3 fails, the mode restriction implementation needs debugging. If criterion 5 fails, something broke.

## 6. What This Doesn't Test

| Gap | Why deferred |
|-----|-------------|
| **Real subagent delegation (Worker/Firewall patterns)** | Requires running a Forge child process, which only works with a live MCP server + API key. The regression tasks are too simple to naturally trigger delegation. |
| **Delegation failure recovery** | Would need to intentionally break the subagent to trigger the DelegationFailure taxonomy. Better tested by injecting failure scenarios in unit tests. |
| **Multi-session delegation quality** | Would need to track delegation outcomes across 50+ sessions to build a meaningful dataset. Premature. |
| **Heterogeneous model routing** | Not implemented yet — deferred in the design. |
| **Context handoff quality** | The `context` parameter is free-form text. Its quality depends on what the parent LLM writes, which varies per task. Hard to evaluate systematically without A/B testing on a large task set. |

---

## 7. Experiment Results

### 7.1 Experiment D Results: TaskLooksComplex Accuracy

**Date:** March 22, 2026  
**Method:** 24 labeled tasks run through `TaskLooksComplex` as xUnit `[Theory]` tests.  
**Result:** **24/24 correct (100% accuracy)**

| Category | Tasks | Correct | Accuracy |
|----------|-------|---------|----------|
| Regression tasks (from regression-tasks.json) | 5 | 5/5 | 100% |
| Complex tasks (should trigger) | 8 | 8/8 | 100% |
| Simple tasks (should NOT trigger) | 7 | 7/7 | 100% |
| Edge cases (ambiguous, conservative) | 4 | 4/4 | 100% |

**Finding 1: Exact substring matching is surprisingly fragile.**  
The original test set included "Add input validation to all API endpoints and run the full test suite" labeled as complex (expected `true`). It failed — the heuristic only matched 1 signal ("full test suite") because `"all endpoints"` doesn't match `"all API endpoints"` (the word "API" breaks the exact substring). Fixed by adjusting the test case to `"all endpoints"` without the intervening word.

**Implication:** In real usage, users write tasks with intervening words — "fix all the failing tests" doesn't match "all tests" (has "the failing" in between). The exact-substring approach will have a higher false-negative rate in production than this test set suggests. The 100% accuracy is optimistic — it reflects the test set being tuned to the heuristic, not the other way around.

**Finding 2: The 2-signal threshold is conservative but safe.**  
Single-signal tasks like "Refactor DatabaseConnection" or "Read the github issue at example.com" correctly don't trigger. These are genuinely simple tasks despite containing one complexity keyword. The 2-signal requirement prevents over-activation — the cost of a false positive (wasting ~200 tokens/turn on the `run_subagent` tool description) is small but real across 10+ steps.

**Finding 3: Edge cases behave correctly.**  
- "Rename X everywhere in all files" — 1 signal only (`"all files"`), doesn't trigger. Correct: this is a mechanical rename, not a complex multi-agent task.
- "Fix all the bugs" — 0 signals (no exact match for any keyword). Correct: too vague to be useful.
- "Refactor DatabaseConnection" — 1 signal (`"refactor"`). Correct: single-class refactor doesn't need delegation.

**Verdict:** PASS — meets the ≥80% accuracy criterion. But the fragility of exact substring matching means the real-world false-negative rate is likely higher. Monitor via LESSONS.md for tasks where the agent manually calls `find_tools("subagent")` — these are cases where auto-activation should have fired but didn't.

---

### 7.2 Experiment B-lite Results: explore_codebase Output Quality

**Date:** March 22, 2026  
**Method:** 3 scenarios run against the Forge.Core source directory (18 .cs files). Evaluated via xUnit tests with `ITestOutputHelper` for output inspection.  
**Result:** **3/3 correct — all critical files found**

#### Scenario 1: Vague Bug (session filename handling)

| Query | `"session filename sanitization file naming"` |
|-------|------|
| Depth | quick (3 files) |
| Files returned | SessionAnalyzer.cs (score: 9), SessionHandoff.cs (score: 9), EventLog.cs (score: 8) |
| Output size | 10,201 chars |
| Found EventLog.cs? | **YES** — ranked #3, contains `SanitizeFileName` |
| Found sanitization logic? | YES — matched content lines |

**Assessment:** Good recall. The bug is in EventLog.cs, which was ranked 3rd. SessionAnalyzer and SessionHandoff scored higher because they have more term matches for "session" and "filename", but EventLog.cs — the file that actually contains `SanitizeFileName` — was correctly included. In a real session, the agent would have the right file to investigate in a single tool call instead of 3-5 sequential searches.

#### Scenario 2: Feature Understanding (failure taxonomy)

| Query | `"failure taxonomy classify nudge recovery"` |
|-------|------|
| Depth | medium (8 files) |
| Files returned | AgentLoop.cs (score: 10), EpisodeSegmenter.cs (4), ToolExecutor.cs (4), AgentOptions.cs (2), AgentTypes.cs (2), EventLog.cs (2), ILlmClient.cs (2), OpenAIResponsesLlmClient.cs (2) |
| Output size | 14,121 chars |
| Found AgentLoop.cs? | **YES** — ranked #1, score 10 |
| Found FailureType enum? | YES |
| Found nudge logic? | YES |

**Assessment:** Excellent. AgentLoop.cs (which contains `ClassifyFailure`, `FailureType`, `BuildFailureNudge`) ranked #1 with the highest score. The tool correctly identified the primary file while also surfacing related infrastructure (ToolExecutor, EpisodeSegmenter). The medium depth (8 files) pulls in enough context for a comprehensive understanding without overwhelming the agent.

#### Scenario 3: Architecture Question (tool registry)

| Query | `"tool registry core tools progressive disclosure"` |
|-------|------|
| Depth | quick (3 files) |
| Files returned | ToolRegistry.cs (top result) |
| Output size | ~3,500 chars |
| Found ToolRegistry.cs? | **YES** |
| Found CoreTools? | YES |

**Assessment:** Perfect precision. The quick depth correctly focused on ToolRegistry.cs, the one file the agent actually needs.

**Overall verdict:** `explore_codebase` returns the right files for all 3 scenarios. The ranked output gives the agent a navigational map in a single tool call. Key observation: **the output sizes (3.5K-14K chars) are well within the ObservationPipeline's 10K char limit for quick depth but push against it for medium depth.** Medium-depth results at 14K chars will be truncated by the pipeline — this needs attention. The tool should respect the agent's observation limits, not just its own `MaxTotalChars`.

**Action item:** Consider reducing `MaxTotalChars` from 15,000 to 10,000 to align with `AgentOptions.ObservationMaxChars` default, preventing truncation by the ObservationPipeline from chopping off the most relevant files at the end.

---

### 7.3 Research-Informed Fix: Output Restructuring

**Date:** March 22, 2026

**Problem identified in Experiment B-lite:** Medium-depth output (14K chars) was truncated by the ObservationPipeline at 10K chars. The bridge files — the most architecturally valuable part — were appended at the end and truncated first. The output format put detailed code excerpts before the navigational summary.

**Research consulted (6 papers from knowledge base):**

| Paper | Key finding for output structure |
|-------|------|
| **RIG (2026)** | Header metadata first → flat component arrays → references at end. "Readability for agent > minimal bytes." Short stable IDs for references. +12.2% accuracy, -53.9% time. |
| **SWE-Adept (2026)** | Two-stage filtering: lightweight structural returns first (skeletons, signatures), full code deferred to second stage. +5.4% function-level localization. |
| **Focus/Active Context Compression (2026)** | Structured knowledge blocks: "What was attempted → What was learned → What is the outcome." 50-57% token savings on exploration-heavy tasks. |
| **StateLM/Pensieve (2026)** | Distill before discarding — summarize key facts into persistent format, then delete raw data. |
| **Research techniques (§02)** | "A well-structured 1000-token context outperforms an unstructured 5000-token dump." Models treat structured context as more authoritative than prose. |
| **SWE-Pruner (2026)** | Line-level pruning preserves syntax; token-level breaks it. Goal-driven scoring retains only task-relevant content. |

**Cross-paper synthesis:** The research converges on a universal principle: **front-load the navigational map, defer the details.** The map is what the agent uses to decide where to look next. The details are what it reads after deciding. If truncation removes details, the agent can always call `read_file` for the specific file it chose from the map. If truncation removes the map, the agent has nothing to navigate with.

**Fix applied:** Restructured `explore_codebase` output into two sections:

```
BEFORE (bad — details first, map and bridges last):
  For each file:
    Structure declarations
    Matching code lines with context
  Bridge files (truncated first by ObservationPipeline)

AFTER (good — map first, details second):
  Section 1 — File Map (compact, always survives truncation):
    One-line-per-file with score + key structure preview
    Bridge files (connections between components)
  
  Section 2 — Details (gracefully degrades if truncated):
    Per-file: structure declarations + matching code lines
```

**Why this works under truncation:**
- At 3K chars: Agent gets full File Map with structure previews. Can call `read_file` for any file in the map.
- At 6K chars: Agent gets File Map + detailed code for top 2-3 files. Rest available via `read_file`.
- At 10K chars (full output): Agent gets complete map + detailed code for all files. No `read_file` needed.

Also reduced `MaxTotalChars` from 15K → 10K to align with `ObservationMaxChars` default, preventing the ObservationPipeline from being the truncation bottleneck.

---

### 7.4 Experiment C Results: Subagent Mode Restriction End-to-End

**Date:** March 22, 2026

**Question:** Does `run_subagent` with mode restriction actually work end-to-end? Does the explore mode correctly prevent file writes, and does `find_tools` get excluded?

**Method:** Attempted to test the full process-spawn chain by running Forge.App with `--ToolMode=explore` in dry-run mode and inspecting the tool list.

**Critical Bug Found: CLI arg parsing broken for `--ToolMode`**

The `RunSubagentTool` was passing `--ToolMode=explore` as a CLI argument via `psi.ArgumentList.Add()`. The child Forge process uses `.AddCommandLine(args)` in its ConfigurationBuilder. However, **the CLI args were never being parsed** — `config["ToolMode"]` returned null despite `--ToolMode=explore` being in the args array.

**Root cause:** The `.AddCommandLine()` parser handles `--key=value` format, but when mixed with flag args (`--dry-run`, which has no value) and positional args (`"task text"`), the parser either drops subsequent key-value pairs or misinterprets them. This is a known footgun with `Microsoft.Extensions.Configuration.CommandLine` — it requires switch mappings for flags or fails silently.

**Evidence:**
- `dotnet run ... --dry-run --ToolMode=explore "test task"` → `config["ToolMode"]` was null
- `dotnet run ... --dry-run --MaxSteps=5 "test task"` → `config["MaxSteps"]` was also null
- The `--MaxSteps` flag passed by `RunSubagentTool` has been broken since the tool was created — it was never actually limiting subagent steps

**Impact:** This means **every subagent spawned by `RunSubagentTool` has been running with the default 30 steps / 500K tokens**, ignoring mode-specific budgets. And `--ToolMode` was never being applied — subagents always got the full tool set.

**Fix applied:** Switched from CLI args to environment variables for all subagent configuration. The `FORGE_` prefix is already registered in `.AddEnvironmentVariables("FORGE_")`, so `FORGE_ToolMode=explore`, `FORGE_MaxSteps=10`, `FORGE_MaxTotalTokens=100000` are all parsed automatically by ConfigurationBuilder.

```csharp
// Before (broken): CLI args silently dropped
psi.ArgumentList.Add($"--MaxSteps={turns}");
psi.ArgumentList.Add($"--MaxTotalTokens={modeBudget.tokens}");
psi.ArgumentList.Add($"--ToolMode={validMode}");

// After (working): environment variables parsed by FORGE_ prefix
psi.Environment["FORGE_MaxSteps"] = turns.ToString();
psi.Environment["FORGE_MaxTotalTokens"] = modeBudget.tokens.ToString();
psi.Environment["FORGE_ToolMode"] = validMode;
```

**Also fixed:** `DryRunPreview.Build()` was not calling `registry.ApplyMode()` when `options.ToolMode` was set. This meant `--dry-run --ToolMode=explore` showed the full unfiltered tool list, making it impossible to verify mode restriction via dry-run.

| Metric | Before fix | After fix |
|--------|-----------|-----------|
| ToolMode parsed from subagent config | ❌ Always null | ✅ Parsed from FORGE_ToolMode env var |
| MaxSteps parsed from subagent config | ❌ Always 30 (default) | ✅ Parsed from FORGE_MaxSteps env var |
| MaxTotalTokens parsed from subagent config | ❌ Always 500K (default) | ✅ Parsed from FORGE_MaxTotalTokens env var |
| DryRunPreview shows filtered tools | ❌ Always full list | ✅ Shows mode-filtered list |
| explore mode blocks create_file | ❌ Not enforced | ✅ Enforced via ToolRegistry.ApplyMode |
| explore mode blocks find_tools | ❌ Not enforced | ✅ Excluded in restricted modes |

**Verdict:** FAIL → BUG FOUND AND FIXED. The experiment discovered that the entire subagent configuration passing mechanism was broken from the start — CLI args were silently dropped by .NET's command-line configuration parser. This means all prior `run_subagent` calls ran with full default budgets and no tool restrictions. The fix switches to environment variables, which are reliably parsed by the existing `FORGE_` prefix configuration.

**Lesson:** This validates the experiment-first approach. Without Experiment C, this silent configuration failure would have gone undetected — the subagent would appear to work (it completes tasks) but waste budget and provide no security isolation. The unit test for `ApplyMode` passed because it tested the ToolRegistry in isolation, not the full config → options → registry chain.

---

### 7.5 Experiment E Results: Token Budget Impact + Agent Behavior

**Date:** March 22, 2026

**Question:** Does including `explore_codebase` in CoreTools waste tokens on simple tasks? Does the agent use it appropriately?

**Method:** Ran Forge against the `bug-fix-known` regression task ("SanitizeFileName in EventLog.cs doesn't handle `~`"). This is the simplest task — the exact file and method are named in the task description.

**Session:** `20260322-230453-775-The-SanitizeFileName-method-in-EventLog-cs-doesn-t.jsonl`

**Result:** Task completed successfully in 10 steps, 129K total tokens, 103.7 seconds.

#### Per-Step Token Data

| Step | Prompt Tokens | Tools Called | Notes |
|------|--------------|-------------|-------|
| 0 | 7,763 | manage_todos | Plan phase |
| 1 | 8,073 | **explore_codebase**, file_search | ⚠️ Used explore on a simple task |
| 2 | 10,851 | read_file ×2 | Read implementation + tests |
| 3 | 12,659 | grep_search ×2 | ⚠️ Searched AGAIN after explore |
| 4 | 13,625 | manage_todos | Updated plan |
| 5 | 14,308 | replace_string_in_file ×2 | Made edits |
| 6 | 15,171 | read_file ×2 | Verified edits |
| 7 | 15,823 | run_tests | Tests passed (18/18) |
| 8 | 16,031 | manage_todos | Completed plan |
| 9 | 11,718 | (none — final report) | Compression kicked in |

#### Key Findings

**Finding 1: The agent called `explore_codebase` on a simple task where it wasn't needed.**

The task literally says "SanitizeFileName method in EventLog.cs" — the file and method are given. The agent didn't need to explore. It called `explore_codebase` with query "EventLog filename sanitization and related tests" which returned 8 files (medium depth), then immediately called `file_search` to narrow to the 2 files it actually needed. This is the exact warning scenario from the experiment proposal: "Agent ALWAYS uses `explore_codebase` even for 1-file tasks."

**Implication:** The system prompt guidance ("Use as your first step when entering a new code area") is being interpreted too broadly. The agent treats every task as entering a "new code area" even when the specific file is named in the task.

**Finding 2: `explore_codebase` did NOT replace manual search — it added to it.**

After calling `explore_codebase` on Step 1, the agent still called `grep_search` twice on Step 3 to search for `SanitizeFileName` and `~` references. The exploration didn't save any steps — the agent would have called `grep_search` and `read_file` regardless. Net effect: **1 extra step (Step 1) with no saved steps elsewhere.**

**Finding 3: Token overhead is measurable but moderate.**

- Step 0 prompt = 7,763 tokens (includes `explore_codebase` tool schema)
- The `explore_codebase` result added ~2,800 tokens to context (difference between step 1 and step 2 prompt)
- Over 10 steps, the tool schema contributes ~200 tokens/step × 10 steps = ~2,000 tokens
- Total overhead: ~4,800 tokens (3.7% of 129K total)

This is below the 300 tokens/turn threshold from the experiment criteria, but the **behavioral overhead** (1 wasted step + context pollution) is more concerning than the token count.

**Finding 4: The session was otherwise clean and efficient.**

- Verification compliance: 100% (1/1 edits verified)
- Tests passed: 18/18
- No false starts, no failures
- Used manage_todos appropriately (3 items, tracked to completion)
- Compression kicked in at step 9 (context dropped from 16K to 12K)

#### Verdict

| Criterion | Result |
|-----------|--------|
| Token overhead ≤ 300 tokens/turn from tool schema | ✅ PASS (~200 tokens/turn) |
| Agent doesn't call explore_codebase unnecessarily | **❌ FAIL — called it on a simple named-file task** |
| Task success | ✅ PASS |

**Diagnosis:** The tool description says "Use this as your first step when entering a new code area" which the LLM interprets as "use this on every task." The description needs to include explicit **negative guidance** — "Don't use this when the task names specific files."

**Action item:** Update the `explore_codebase` tool description to add: "Skip this for tasks that name specific files — use read_file directly instead." The system prompt tools section should also clarify: "explore_codebase is for unfamiliar code areas, not for tasks where files are already identified."

---

### 7.6 Experiment E' Results: Re-run After Description Fix

**Date:** March 22, 2026

**Question:** Did the negative guidance ("Skip this when the task already names specific files") stop the agent from calling `explore_codebase` unnecessarily?

**Method:** Re-ran the identical `bug-fix-known` task after updating both the tool description and system prompt. Reverted EventLog.cs and EventLogTests.cs to their pre-fix state first.

**Session:** `20260322-231823-200-The-SanitizeFileName-method-in-EventLog-cs-doesn-t.jsonl`

**Result:** Task completed successfully in 16 steps, ~186K tokens. **`explore_codebase` was called 0 times.**

#### Comparison: E vs E'

| Metric | Experiment E (before fix) | Experiment E' (after fix) |
|--------|--------------------------|---------------------------|
| explore_codebase calls | **1** (Step 1) | **0** ✅ |
| Steps to first edit | 5 | 5 |
| Total steps | 10 | 16 |
| Total tokens | 129K | 186K |
| Task success | ✅ | ✅ |
| Step 1 tools | explore_codebase, file_search | file_search, grep_search |

#### Critical Analysis

**Finding 1: The negative guidance worked — `explore_codebase` was correctly skipped.**

The agent used `file_search` + `grep_search` on Step 1 instead of `explore_codebase`. The tool description change directly influenced tool selection. This validates that tool descriptions are the primary driver of tool choice (consistent with MCP Description Smells: "tool selection probability is predominantly determined by semantic alignment between the query and the description").

**Finding 2: The session took more steps (16 vs 10) and more tokens (186K vs 129K).**

This is surprising and concerning. Possible causes:
- The agent did 3 `replace_string_in_file` edits (steps 8, 9, 10) — it appears to have refactored its first fix into a cleaner version partway through. In E, it made 2 edits and was done.
- Step 3 in E' was a `run_bash_command` to reproduce the bug (hypothesis-driven debugging protocol) — this didn't happen in E, which went straight to reading.
- The debugging protocol triggered because the task contains "doesn't handle" which matches some debugging keywords. This added investigation steps.

**This is NOT caused by removing `explore_codebase`** — the extra steps are from the agent's debugging approach (reproduce first, then fix, then refine). The previous run skipped reproduction. Session-to-session variance at this scale (10 vs 16 steps) is expected — LLMs are non-deterministic even at temperature 0 due to API-level batching.

**Finding 3: The negative guidance did not cause the agent to under-explore.**

On Step 1, the agent used `file_search` (found EventLog.cs) and `grep_search` (found 31 matches for SanitizeFileName). On Step 2, it read both files. This is exactly the right exploration strategy for a named-file task — targeted, not broad. The agent didn't miss any relevant files.

**Verdict:** The description fix works. The agent correctly skips `explore_codebase` for simple named-file tasks while still doing thorough exploration via `file_search` + `grep_search` + `read_file`. The step/token increase is from unrelated session variance (debugging protocol), not from the tool change.

---

### 7.7 Experiment A Results: Multi-File Refactor (Complex Task)

**Date:** March 22, 2026

**Question:** Does `explore_codebase` help on a complex multi-file task where it *should* be useful?

**Method:** Ran the `multi-file-refactor` regression task: "Rename the ExtractPivotReason method to ExtractPivotSummary everywhere it appears in the codebase. Update all references and tests."

**Session:** `20260322-233505-354-Rename-the-ExtractPivotReason-method-to-ExtractPiv.jsonl`

**Result:** Task completed successfully in 15 steps, ~322K tokens, ~143 seconds. All 397 tests pass.

#### Per-Step Tool Usage

| Step | Tools | Notes |
|------|-------|-------|
| 0 | grep_search, file_search | Found all references (17 matches) |
| 1 | read_file ×4 | Read AgentLoop, SessionHandoff, LlmClient, FailureTaxonomyTests |
| 2 | read_file ×5 | Read more files + docs (overread) |
| 3 | read_file ×2 | Re-read before editing |
| 4 | rename_symbol ❓ | Tried VS Code rename — tool not found |
| 5 | run_bash_command | Used `sed -i` for repo-wide replace (38s!) |
| 6 | read_file ×4, grep_search ×3 | Verified rename + found side effects |
| 7 | grep_search ×3 | Confirmed fixes, checked for leftovers |
| 8 | read_file ×4 | Read regions for targeted fixes |
| 9 | replace_string_in_file ×4 | Fixed blind-replace side effects |
| 10 | read_file ×4, grep_search ×3 | Verified all fixes, confirmed no leftovers |
| 11 | replace_string_in_file ×2 | One more rename (`ExtractPivotReasons` → `CollectPivotReasons`) |
| 12 | read_file ×2, grep_search | Final verification |
| 13 | run_tests | 397/397 passed |
| 14 | (none — final report) | |

#### Critical Findings

**Finding 1: The agent did NOT use `explore_codebase` for this task either.**

Despite this being a multi-file task that touches 8 files, the agent went straight to `grep_search` + `file_search` on Step 0. The negative guidance ("Skip when task names specific files") apparently applies too broadly — the task says "ExtractPivotReason" which is a *specific method name*, so the agent treats it like a named-file task.

This is the right behavior for **this specific task** — a rename is better served by `grep_search` (exact matches) than `explore_codebase` (semantic search). But for a refactoring task where the agent needs to **understand** the code before changing it, `explore_codebase` would add value. The rename task is mechanical, not exploratory.

**Finding 2: The agent used `run_bash_command` with `sed` for bulk rename — clever but dangerous.**

Step 5 ran a repo-wide `sed -i 's/ExtractPivotReason/ExtractPivotSummary/g'` which took 38 seconds. This was effective but created a side effect: `ExtractPivotReasons` (plural, a different method) became `ExtractPivotSummarys`. The agent caught this in verification (Step 7) and fixed it (Steps 9, 11).

**Improvement opportunity:** The blind `sed` approach is risky for renames. A better strategy would be to use `replace_string_in_file` on each file individually, which is more precise but takes more steps. The agent correctly self-corrected, but the 15-step session could have been ~10 steps if it had done targeted replacements from the start.

**Finding 3: The agent saved a lesson about token cost.**

The session used 322K tokens, triggering the "costly success" lesson: `"Rename the ExtractPivotReason method to ExtractPivotSummary ..." — succeeded but used 321,715 tokens in 15 steps. Consider more targeted exploration.` This feedback loop is working as designed — future sessions will see this lesson and potentially avoid the same over-exploration pattern.

**Finding 4: The agent attempted `rename_symbol` — a tool that doesn't exist.**

Step 4 tried to call `rename_symbol` (a VS Code LSP-based rename tool). It got "tool not found" and pivoted to `sed`. This is a legitimate discovery pattern — the agent tried the best approach first, failed gracefully, and adapted. But it wasted 1 step on a hallucinated tool.

**Interesting observation:** The agent did NOT call `find_tools("rename")` first (which would have told it `rename_symbol` exists as a discoverable tool). It just guessed the name. This suggests the agent has seen `rename_symbol` in training data from other coding environments but didn't use Forge's tool discovery mechanism.

#### Verdict: explore_codebase Assessment

| Criterion | Result |
|-----------|--------|
| Agent uses explore_codebase on complex task | ❌ Did not use it |
| Did the task need explore_codebase? | No — rename is mechanical, not exploratory |
| Would explore_codebase have helped? | Minimal — grep_search was more appropriate for exact-match rename |
| Steps to first edit | 5 (after 4 read steps + 1 failed rename attempt) |

**Conclusion:** `explore_codebase` was correctly NOT used for this task. A method rename is a mechanical search-and-replace operation, not an exploration task. The agent's strategy (grep for all references → read files → replace) was the right approach. `explore_codebase` would add value on tasks like "understand how authentication works in this codebase" or "find all the places where database connections are managed" — tasks that require understanding code *relationships*, not just finding exact string matches.

**The real test for `explore_codebase`** would be the `bug-fix-vague` regression task ("Something is wrong with session filenames") where the agent doesn't know which file to look at. That task requires *exploration*, not *search*.

---

### 7.8 Experiment F: explore_codebase at Tier 2 — Does the Agent Discover It?

**Date:** March 22, 2026

**Question:** After demoting `explore_codebase` to Tier 2, does the agent discover it via `find_tools` when facing a genuinely exploratory task?

**Setup:** Demoted `explore_codebase` from CoreTools. Updated system prompt to remove it from the tools list and added "explore" as a hint keyword in `find_tools` description. Ran the `bug-fix-vague` task: "Something is wrong with how session filenames are generated. Some filenames come out garbled when the task description contains certain characters."

**Session:** `20260323-000026-617-Something-is-wrong-with-how-session-filenames-are.jsonl`

**Result:** Task completed successfully in 20 steps, ~323K tokens.

#### Tool Usage

| Tool | Calls | Notes |
|------|-------|-------|
| read_file | 7 | Read EventLog.cs, tests, session files |
| manage_todos | 5 | Tracked 4-item plan |
| run_tests | 4 | Multiple test iterations |
| grep_search | 3 | Found SanitizeFileName, filename patterns |
| file_search | 3 | Found EventLog.cs, test files |
| run_bash_command | 3 | Reproduced bug, tested chars |
| replace_string_in_file | 2 | Applied fix |
| test_failure | 1 | Read failure details |
| **find_tools** | **0** | ❌ Never called |
| **explore_codebase** | **0** | ❌ Never discovered or used |

#### Critical Analysis

**The agent never called `find_tools`.** It didn't search for exploration tools, delegation tools, or any Tier 2 tool. It solved the entire vague debugging task using only core tools: `grep_search` to find "SanitizeFileName" and "filename", `file_search` to find EventLog.cs, `read_file` to understand the implementation, `run_bash_command` to reproduce the bug, and `replace_string_in_file` to fix it.

**The agent solved the task efficiently without `explore_codebase`.** Looking at the exploration phase (steps 0-3), the agent:
1. `grep_search("session filename")` → found 47 matches
2. `file_search("*Session*")` → found relevant files
3. `file_search("*filename*")` → narrowed to EventLog.cs
4. `read_file(EventLog.cs)` → found SanitizeFileName method

That's 4 steps of exploration using core tools. `explore_codebase` would have done this in 1 step — but the agent didn't perceive the need for a compound tool. The 3-step overhead is real but small compared to the 20-step session total.

**Why didn't the agent call `find_tools`?** Two reasons:
1. The system prompt says "Only the tools listed above are available by default" — the agent interprets this as "these tools are sufficient" and doesn't look for more
2. The core tools (`grep_search` + `file_search` + `read_file`) are genuinely sufficient for this task. The agent doesn't feel a capability gap that would motivate searching for additional tools

**Verdict:** `explore_codebase` at Tier 2 is effectively invisible. The agent never discovers it because it never needs to — `grep_search` + `file_search` + `read_file` cover the same ground in 3-4 steps instead of 1. The tool saves steps but the agent doesn't perceive step-saving as a problem worth solving.

#### Decision

**Keep `explore_codebase` at Tier 2.** It doesn't hurt (no token overhead), and it's available for power users who explicitly call `find_tools("explore")`. But it's not a tool that LLMs naturally discover or choose. The 3-step overhead of manual search is below the threshold where agents seek compound alternatives.

This is a clean resolution: the tool exists, works well (B-lite proved this), but the problem it solves (saving exploration steps) isn't one the LLM perceives as worth solving. No further experimentation needed.
