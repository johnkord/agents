# Forge Self-Improvement Plan

> March 2026 — Pointing the agent at itself

## Reasoning Effort

**Current state: no reasoning effort or temperature is set.** Forge sends `ChatOptions` to the LLM with tools but no `Temperature`, `TopP`, or reasoning-effort parameter. This means the model runs at its default settings (likely temperature 1.0 for GPT-5.4).

**Recommendation:** Add `Temperature = 0` to `ChatOptions` for coding tasks. Deterministic output is more reproducible and makes trajectory comparison meaningful. If reasoning-effort becomes available via `Microsoft.Extensions.AI` (OpenAI's `reasoning_effort` parameter), set it to `"high"` for planning steps and `"medium"` for routine tool-calling steps — this maps to the CogRouter adaptive depth concept without requiring custom training.

This is a one-line change in `AgentLoop.cs` but should be configurable via `AgentOptions` for experimentation.

## Why Self-Improvement?

The most creative use of a coding agent is to improve itself. Six research threads converge on this being tractable:

1. **Voyager (2023):** An ever-growing skill library compounds capabilities. Mastered skills are stored as executable code, indexed, and composed for harder tasks. *Forge's event logs are the raw material for this.*

2. **Reflexion (2023):** Verbal self-reflection ("what went wrong and what to do differently") outperforms blind retry by 8% and simple replay by a larger margin. *Forge's session logs contain the exact failure trajectories needed for reflection.*

3. **Tool-R0 (2026):** Self-play with difficulty-aware curricula outperforms 210K human-annotated examples. Tasks at the agent's competence frontier yield the most learning. *We can generate tasks calibrated to Forge's current ability.*

4. **DeepVerifier (2026):** Structured failure taxonomy drives targeted feedback. *Forge's event logs can be classified into failure modes and fed back as learning signal.*

5. **BAO (2026):** Explicit retrospective + prospective inter-turn behaviors are what baseline RL agents lack. *The Plan→Act→Verify prompt already has this structure; session-over-session memory would extend it across tasks.*

6. **CoRefine (2026):** Sequential refinement with halt/rethink/alternative beats parallel sampling at 190× fewer tokens. *Forge already has re-plan nudging; session-level "what went wrong last time" would add the cross-session dimension.*

## Approach: Reflexion-Style Session Memory

The simplest approach that captures the most value from the research:

**After each session, Forge writes a reflection note.** If the session had failures, wasted steps, or high token consumption, the agent (or a post-session analysis prompt) generates a 2-3 sentence lesson learned. These accumulate in a `LESSONS.md` file in the workspace.

**Before each session, Forge reads `LESSONS.md`.** Past lessons are injected into context, biasing the agent away from repeating mistakes. This is Reflexion's "verbal reinforcement" applied at the session level.

**Implementation:**
1. Post-session: if session had errors or >50K tokens, ask the LLM to generate a lesson from the event log summary
2. Append to `LESSONS.md` (human-readable, version-controllable)
3. Pre-session: if `LESSONS.md` exists, inject its contents after the system prompt
4. Cap at ~50 lessons (Reflection-Driven Control: saturation at ~50 patterns)

**What this does NOT do:** It doesn't modify Forge's own code. It modifies Forge's *behavior* through accumulated context. Code self-modification is a later, riskier step.

## Progressive Task Ladder

Five levels of self-referential tasks, each harder than the last:

### Level 1: Read and Understand
Tasks that only require reading Forge's own code and reporting.
- "Read AgentLoop.cs and explain the streaming + tool execution flow"
- "List all the files in the Forge project and describe the architecture"
- "Find all the places where tokens are counted and explain the budget tracking"

**Purpose:** Tests whether the agent can navigate and understand its own codebase. Low risk (read-only). Validates REPO.md-free navigation.

### Level 2: Targeted Single-File Improvements
Tasks that modify one file with clear success criteria.
- "Add a `Temperature` property to `AgentOptions` with a default of 0, and wire it into `ChatOptions` in `AgentLoop.cs`"
- "The `ObservationPipeline.MaxChars` constant is 10,000. Make it configurable via `AgentOptions` instead of hardcoded"
- "Add a test for the duplicate tool call detection in `AgentLoop` — mock the scenario where the same grep_search is called twice"

**Purpose:** Tests the edit→verify loop on its own code. Success is measurable (build + tests pass).

### Level 3: Multi-File Refactoring
Tasks requiring changes across multiple files with dependency understanding.
- "Extract the tool execution logic from AgentLoop.cs into a separate ToolExecutor class. All 87 existing tests must still pass."
- "The Guardrails class uses string matching to extract paths from JSON. Refactor it to use System.Text.Json deserialization instead."
- "Add a `--verbose` flag to Program.cs that sets Serilog minimum level to Debug (currently always Debug)"

**Purpose:** Tests Plan→Act→Verify under real complexity. Multi-file changes require the agent to understand dependencies between files.

### Level 4: Debugging and Diagnosis
Tasks where the agent must understand a failure and fix it.
- Pre-plant a bug: change `MaxLines` in ObservationPipeline from 200 to 2, then: "Tests are failing. Diagnose and fix the issue."
- Pre-plant a bug: rename a tool in the MCP server, then: "The agent can't find the grep_search tool. Figure out why and fix it."
- "Run the full test suite. If any tests fail, diagnose the root cause and fix them."

**Purpose:** Tests hypothesis-driven debugging. The agent must observe → hypothesize → test → fix. This is where Plan→Act→Verify earns its keep.

### Level 5: Design and Implement from Spec
Tasks requiring the agent to read a design doc and implement from it.
- "Read `design.md` section 7.4 (Evolving Reflective Memory). Implement a minimal version: after each session, write a one-line lesson to `LESSONS.md` if there were errors."
- "Read `design.md` section 5.2 (Verify-Before-Commit Protocol). Implement the reversible/irreversible classification in Guardrails.cs."
- "Read `implementation-plan.md` Phase 3A (Repository Intelligence). Implement a minimal REPO.md generator that parses `*.csproj` files and writes a structural snapshot."

**Purpose:** Tests whether the agent can translate design intent into working code. The highest-value tasks — the agent literally implements its own roadmap.

## Execution Protocol

For each task:

1. **Commit clean state** before running (`git add -A && git commit`)
2. **Run the task** and capture the full output
3. **Review the diff** (`git diff`) — inspect what the agent actually changed
4. **Run tests** (`dotnet test`) — verify correctness
5. **Record metrics** — steps, tokens, duration, success/failure
6. **Review the session log** — identify wasted steps, interesting decisions
7. **If failed:** roll back (`git checkout .`), analyze why, consider whether a system prompt or tool description change would help
8. **If succeeded:** commit the improvement, move to next task

## Success Criteria

- Level 1-2: 80%+ pass rate (these are straightforward)
- Level 3: 60%+ pass rate (multi-file is harder)
- Level 4: 40%+ pass rate (debugging is genuinely difficult for current agents)
- Level 5: 30%+ pass rate (design-to-implementation is the hardest)

Any task the agent solves is a real improvement to the codebase. Any task it fails on reveals what to build next.
