# Evaluation and Reliability: Making Agents You Can Trust

> *Last updated: 2026-07-15*

## The Reliability Crisis

The gap between "works in a demo" and "works in production" is where most agent projects die. An agent that succeeds 80% of the time is impressive in a demo and unusable in production — that's a 1-in-5 failure rate that destroys user trust.

The math gets worse with multi-step agents. If each step has a 95% success rate and a task requires 10 steps, the compound success rate is $0.95^{10} \approx 60\%$. For 20 steps at 95%: $0.95^{20} \approx 36\%$. This is why **step-level reliability matters exponentially more than it intuitively feels.**

**The goal: move from 80% → 95% → 99% reliable.** Each step requires different techniques.

Anthropic's guidance is direct: "The autonomous nature of agents means higher costs, and the potential for compounding errors. We recommend extensive testing in sandboxed environments, along with the appropriate guardrails." The key word is *sandboxed* — agents should be tested in environments where failures can't cause real damage.

## Evaluation Dimensions

### Accuracy
Does the agent produce correct results?

```
Task: "Find and fix the bug causing the login form to crash"
- Correct: Identifies the null pointer in auth.js:47, applies correct fix
- Partially correct: Finds the right file but wrong line, fix is close
- Incorrect: Edits the wrong file entirely
```

### Efficiency
Does the agent solve the problem in a reasonable number of steps?

```
Optimal: 4 steps (search → read → edit → verify)
Acceptable: 8 steps (some exploration needed)
Inefficient: 20 steps (lots of backtracking and false starts)
Wasteful: 50 steps (stuck in a loop, trying random things)
```

### Completeness
Does the agent solve the _entire_ problem or just part of it?

```
Task: "Add input validation to all API endpoints"
- Complete: All 12 endpoints validated, edge cases handled
- Partial: 8/12 endpoints done, forgot the batch endpoints
- Minimal: Only the most obvious endpoint validated
```

### Safety
Does the agent avoid harmful or unauthorized actions?

```
- Safe: Only modifies files in the project directory
- Unsafe: Modifies system files, deletes data, exposes credentials
```

### Transparency
Can a human understand what the agent did and why?

```
- Transparent: Clear step-by-step reasoning, explains each decision
- Opaque: Jumps to conclusions, no explanation of approach
```

## Evaluation Methodologies

### 1. Task-Based Benchmarks

Create a suite of tasks with known correct answers:

```python
BENCHMARK = [
    {
        "id": "fix-001",
        "description": "Fix the off-by-one error in pagination.py",
        "setup": "git checkout fix-001-setup",  # Set up the broken state
        "validation": [
            {"type": "file_contains", "file": "pagination.py", "pattern": "range(0, total_pages)"},
            {"type": "test_passes", "command": "pytest tests/test_pagination.py"},
            {"type": "max_steps", "limit": 10}
        ],
        "difficulty": "easy",
        "category": "bug-fix"
    },
    {
        "id": "refactor-003",
        "description": "Extract the email sending logic into a separate service class",
        "setup": "git checkout refactor-003-setup",
        "validation": [
            {"type": "file_exists", "file": "services/email_service.py"},
            {"type": "test_passes", "command": "pytest tests/"},
            {"type": "no_import", "file": "views.py", "pattern": "import smtplib"},
            {"type": "code_quality", "min_score": 7}
        ],
        "difficulty": "medium",
        "category": "refactoring"
    }
]
```

### 2. A/B Comparison

Run two agent configurations on the same tasks, compare:

```python
async def ab_compare(tasks: list[Task], config_a: AgentConfig, config_b: AgentConfig):
    results = {"a_wins": 0, "b_wins": 0, "tie": 0}
    
    for task in tasks:
        result_a = await run_agent(config_a, task)
        result_b = await run_agent(config_b, task)
        
        # Automated metrics
        score_a = evaluate(result_a, task.validation)
        score_b = evaluate(result_b, task.validation)
        
        # LLM-as-judge for subjective quality
        judge_result = await judge(task, result_a, result_b)
        
        if judge_result.winner == "a": results["a_wins"] += 1
        elif judge_result.winner == "b": results["b_wins"] += 1
        else: results["tie"] += 1
    
    return results
```

### 3. LLM-as-Judge

Use a powerful model to evaluate agent outputs:

```python
JUDGE_PROMPT = """You are evaluating an AI agent's work on a task.

Task: {task_description}
Agent's output: {agent_output}
Expected outcome: {expected_outcome}

Score on each dimension (1-10):
1. Correctness: Does the output satisfy the task requirements?
2. Completeness: Are all aspects of the task addressed?
3. Efficiency: Was the approach reasonable, or wasteful?
4. Code Quality: Is the code clean, idiomatic, well-documented? (if applicable)
5. Safety: Were there any risky or unauthorized actions?

Provide scores and brief justification for each.

Overall verdict: PASS (all scores >= 7) or FAIL (any score < 7)
"""
```

**Calibration**: LLM judges need calibration. Compare their scores against human evaluations on a sample and adjust.

### 4. Trajectory Analysis

Don't just evaluate the final output — analyze the _path_ the agent took.

**AgentBoard** (Ma et al., 2024, NeurIPS Oral) formalized this with the **progress rate metric** — a fine-grained alternative to binary success/fail:

```
Progress Rate = (# sub-goals completed) / (# total sub-goals)
```

This solves a critical evaluation blind spot:
```
Agent A: ✗ (failed — but completed 8/10 sub-goals)
Agent B: ✗ (failed — completed 0/10 sub-goals)

Binary evaluation: Both scored 0. They look identical.
Progress rate:    Agent A = 80%, Agent B = 0%. Very different.
```

AgentBoard evaluated agents across 9 environments (ALFWorld, WebShop, WebArena, BabyAI, etc.) and found:
- **Tool use is the most reliable capability** — models do well at API calling but struggle with multi-step reasoning
- **Self-correction rarely happens naturally** — agents need explicit mechanisms (like Reflexion) to recover from mistakes
- **Long-horizon tasks are disproportionately harder** — performance drops sharply with required step count
- **The "last mile" is hardest** — many agents get most of the way but fail on final steps

Practical takeaway: **Use progress rate alongside success rate** during development to get diagnostic visibility into agent capabilities.

```python
@dataclass
class TrajectoryMetrics:
    total_steps: int
    tool_calls: int
    unique_tools_used: set[str]
    errors_encountered: int
    errors_recovered: int
    backtracking_steps: int  # Steps that undid previous work
    redundant_steps: int     # Steps that produced no new information
    time_to_first_action: float
    total_tokens_consumed: int
    
    @property
    def efficiency_score(self) -> float:
        """Lower is better — ratio of wasted to useful steps"""
        wasted = self.redundant_steps + self.backtracking_steps
        useful = self.total_steps - wasted
        return useful / max(self.total_steps, 1)
    
    @property
    def recovery_rate(self) -> float:
        """How well does the agent recover from errors?"""
        if self.errors_encountered == 0:
            return 1.0
        return self.errors_recovered / self.errors_encountered
```

### 5. Regression Testing

After any change to the agent (prompt, tools, model), run the full benchmark:

```python
def regression_check(old_results: dict, new_results: dict, 
                     max_regression: float = 0.02) -> bool:
    """Fail if accuracy drops by more than 2%"""
    for task_category, old_score in old_results.items():
        new_score = new_results.get(task_category, 0)
        if old_score - new_score > max_regression:
            print(f"REGRESSION in {task_category}: {old_score:.1%} → {new_score:.1%}")
            return False
    return True
```

## Reliability Engineering Techniques

### Technique 1: Guardrails

Hard limits that prevent catastrophic failures:

```python
class AgentGuardrails:
    def __init__(self):
        self.max_steps = 50
        self.max_tokens_per_turn = 200_000
        self.forbidden_paths = ["/etc", "/sys", "/root", ".env"]
        self.allowed_commands = ["git", "npm", "python", "pytest", "ls", "cat", "grep"]
        self.max_file_size_write = 100_000  # characters
    
    def check_tool_call(self, tool_name: str, params: dict) -> tuple[bool, str]:
        if tool_name == "run_command":
            cmd = params.get("command", "")
            base_cmd = cmd.split()[0] if cmd else ""
            if base_cmd not in self.allowed_commands:
                return False, f"Command '{base_cmd}' not in allowed list"
        
        if tool_name in ["write_file", "edit_file"]:
            path = params.get("path", "")
            for forbidden in self.forbidden_paths:
                if path.startswith(forbidden):
                    return False, f"Cannot modify files in {forbidden}"
        
        return True, "ok"
```

### Technique 2: Retry with Reflection

When an action fails, don't just retry — reflect on _why_ and adjust:

```python
async def execute_with_reflection(agent, action, max_retries=3):
    for attempt in range(max_retries):
        try:
            result = await agent.execute(action)
            return result
        except ToolError as e:
            if attempt == max_retries - 1:
                raise
            
            # Reflect on the failure
            reflection = await agent.reflect(
                f"Action failed on attempt {attempt + 1}: {e}\n"
                f"Original action: {action}\n"
                f"What went wrong and how should I adjust?"
            )
            
            # Adjust based on reflection
            action = await agent.revise_action(action, reflection)
```

### Technique 3: Assertions and Checkpoints

Programmatic verification at key points:

```python
class AgentCheckpoints:
    @staticmethod
    def verify_file_edit(filepath: str, expected_changes: list[str]):
        """After editing a file, verify the changes were applied"""
        content = read_file(filepath)
        for change in expected_changes:
            assert change in content, f"Expected change not found: {change}"
    
    @staticmethod
    def verify_tests_pass(test_command: str):
        """After code modification, verify tests still pass"""
        result = run_command(test_command)
        assert result.exit_code == 0, f"Tests failed: {result.stderr}"
    
    @staticmethod  
    def verify_no_regression(before_snapshot: dict, after_snapshot: dict):
        """Compare before/after states to ensure no unintended changes"""
        for key in before_snapshot:
            if key not in after_snapshot:
                raise RegressionError(f"Missing after edit: {key}")
```

### Technique 4: Confidence Calibration

Teach agents to express uncertainty and act accordingly:

```xml
<system_prompt>
When you're uncertain about something, explicitly state your confidence level:
- HIGH confidence: Proceed without asking
- MEDIUM confidence: Proceed but flag for review  
- LOW confidence: Ask for clarification before acting

Examples:
- "I'm HIGH confidence this is a null pointer error on line 47" → fix it
- "I'm MEDIUM confidence the database schema needs this migration" → apply it, flag for review
- "I'm LOW confidence about which API version to use" → ask the user
</system_prompt>
```

### Technique 5: Output Validation

Programmatically validate agent outputs before returning to user:

```python
class OutputValidator:
    def validate_code_output(self, code: str, language: str) -> ValidationResult:
        checks = []
        
        # Syntax check
        if language == "python":
            try:
                ast.parse(code)
                checks.append(("syntax", True, "Valid Python syntax"))
            except SyntaxError as e:
                checks.append(("syntax", False, f"Syntax error: {e}"))
        
        # Security check
        dangerous_patterns = ["eval(", "exec(", "os.system(", "__import__"]
        for pattern in dangerous_patterns:
            if pattern in code:
                checks.append(("security", False, f"Dangerous pattern: {pattern}"))
        
        # Style check (basic)
        if len(code.split("\n")) > 500:
            checks.append(("size", False, "Output suspiciously large (>500 lines)"))
        
        return ValidationResult(checks=checks, passed=all(c[1] for c in checks))
```

### Technique 6: Cascading Fallbacks

When the primary approach fails, have a fallback chain:

```python
FALLBACK_CHAIN = [
    {"model": "claude-opus", "temperature": 0.0, "max_steps": 30},
    {"model": "claude-opus", "temperature": 0.3, "max_steps": 20},  # Retry with different temp
    {"model": "claude-opus", "temperature": 0.0, "max_steps": 50, "simplified_tools": True},
    {"strategy": "decompose_and_retry"},  # Break into smaller subtasks
    {"strategy": "ask_human"},  # Give up gracefully
]
```

## Building an Evaluation Pipeline

```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│  Benchmark    │────▶│  Run Agent   │────▶│  Validate    │
│  Suite        │     │  on Tasks    │     │  Results     │
└──────────────┘     └──────────────┘     └──────┬───────┘
                                                  │
                                           ┌──────▼───────┐
                                           │  Compute     │
                                           │  Metrics     │
                                           └──────┬───────┘
                                                  │
                      ┌──────────────┐     ┌──────▼───────┐
                      │  Dashboard   │◄────│  Store       │
                      │  & Alerts    │     │  Results     │
                      └──────────────┘     └──────────────┘
```

**Key metrics to track over time:**
- Pass rate by category (bug-fix, refactor, feature, research)
- Average steps to completion
- Token cost per task
- Error recovery rate
- Regression rate after changes

## The Agent Benchmark Landscape

A comprehensive view of key benchmarks, ordered by domain and realism:

| Benchmark | Domain | Key Metric | Difficulty | Key Finding |
|---|---|---|---|---|
| **SWE-bench** (Jimenez et al., 2023) | Software engineering | % resolved GitHub issues | Very Hard | Best went from 1.96% → 53%+ (2023→2025) — almost entirely due to agentic tools, not model improvements |
| **AgentBench** (Liu et al., 2023) | 8 diverse environments | Overall score | Hard | Massive gap between commercial and open-source LLMs as agents. Format compliance is top failure mode |
| **WebArena** (Zhou et al., 2023) | Realistic web tasks | Task success rate | Hard | Self-hosted real websites; even GPT-4 struggles with complex web navigation |
| **OSWorld** (Xie et al., 2024) | Real computer OS tasks | Task success rate | Very Hard | Best model 12.24% vs human 72.36% — GUI grounding is the primary bottleneck |
| **τ-bench** (Yao et al., 2024) | Customer service + tools | pass^k reliability | Hard | Even GPT-4o succeeds <50%. The pass^k metric exposes reliability crisis (50% → 3% over 5 tasks) |
| **AgentBoard** (Ma et al., 2024) | 9 environments | Progress rate | Medium-Hard | Fine-grained metric reveals capability profiles beyond binary pass/fail |
| **Agent-as-a-Judge** (Zhuge et al., 2024) | AI development tasks | Requirement coverage | Hard | ~0.85 correlation with human judgment (vs 0.65 for LLM-as-a-Judge) |
| **FINDER/DEFT** (2025) | Deep research | Checklist %, failure taxonomy | Very Hard | 100 tasks, 419 checklists; DEFT taxonomy with 14 failure modes across 4 categories |
| **ADR-Bench** (2025) | Deep research (Chinese) | Research report quality | Hard | First Chinese deep research benchmark |
| **ResearchRubrics** (2025) | Deep research | 20-43 rubric items per task | Very Hard | 101 tasks with fine-grained rubric-based scoring; used to train DeepPlanner/Step-DR |
| **BrowseComp** (OpenAI, 2025) | Web research | Retrieval accuracy | Very Hard | Complex web research tasks requiring multi-step browsing |
| **DeepResearch Bench** (2025) | Deep research survey | Multi-metric | Hard | Comprehensive deep research evaluation framework |

### The pass^k Reliability Metric (from τ-bench)

One of the most important evaluation concepts for production agents:

$\text{pass}^k = (\text{pass}^1)^k$

This measures the probability of succeeding on ALL of k consecutive tasks:

| pass^1 | pass^3 | pass^5 | pass^10 |
|--------|--------|--------|---------|
| 90% | 72.9% | 59.0% | 34.9% |
| 80% | 51.2% | 32.8% | 10.7% |
| 50% | 12.5% | 3.1% | 0.1% |

**The implication**: An agent that looks "pretty good" at 80% single-task accuracy is **unusable in production** for sequential tasks. This is why the reliability ladder matters — and why evaluation must report pass^k for k > 1.

### The Evaluation Stack

Agent-as-a-Judge (Zhuge et al., 2024) introduces a hierarchy of evaluation approaches:

```
Level 0: Automated metrics (pass/fail, BLEU, accuracy)
Level 1: LLM-as-a-Judge (text quality, single-turn evaluation)
Level 2: Agent-as-a-Judge (process + outcome, can execute code to verify)
Level 3: Human evaluation (gold standard, for calibration)
```

Each level catches errors the previous level misses. Agent-as-a-Judge can **re-execute code**, **check intermediate reasoning**, and **evaluate full trajectories** — making it far more reliable than LLM-as-a-Judge for evaluating agent systems.

## The Reliability Ladder

A framework for thinking about where your agent is and where it needs to be:

| Level | Pass Rate | Characteristics | Suitable For |
|---|---|---|---|
| **L0: Prototype** | 40-60% | Works sometimes, fails unpredictably | Internal demos |
| **L1: Alpha** | 60-80% | Works for common cases, struggles with edge cases | Internal beta users |
| **L2: Beta** | 80-90% | Reliable for most tasks, occasional failures | Limited production |
| **L3: Production** | 90-95% | Consistent, good error handling, graceful degradation | General production |
| **L4: High-Trust** | 95-99% | Extensive validation, fallbacks, monitoring | Critical workflows |
| **L5: Autonomous** | 99%+ | Self-healing, proactive error prevention | Unsupervised operation |

**Most teams jump from L0 to trying L3.** The discipline is to systematically climb each level.

## Common Pitfalls in Agent Evaluation

1. **Testing on easy cases only**: Benchmarks must include failure-prone scenarios
2. **Ignoring cost**: An agent that's accurate but 10x too expensive is not viable
3. **One-shot evaluation**: Results vary run-to-run due to temperature and model non-determinism. Average over 3-5 runs minimum.
4. **Evaluating output only**: Trajectory matters. Two agents can produce the same output but one used 5 steps and the other 50. Use **progress rate** (AgentBoard) for diagnostic granularity.
5. **No baseline**: Compare against the simplest possible approach (single LLM call, no agent). If the simple approach wins, you don't need an agent.
6. **Benchmark overfitting**: If you tune your agent to pass the benchmark, you're not improving general capability. Hold out a test set the agent never sees during development.
7. **Ignoring latency**: A correct answer in 5 minutes may be worse than a mostly-correct answer in 5 seconds, depending on the use case.
8. **Not testing error recovery**: The benchmark should *intentionally* include scenarios where tools fail, files don't exist, or APIs return errors. Recovery ability is a first-class metric.
9. **Missing the eval investment for automated design**: ADAS (Hu et al., 2024) showed that automated agent design requires reliable, fast evaluation. Your eval harness may be the most important piece of infrastructure you build — it enables not just testing but automated architecture search.

## Deep Research Failure Taxonomies (2025–2026)

The deep research wave produced the first **systematic failure taxonomies** for complex agentic tasks. These are essential for diagnosing what goes wrong and prioritizing reliability improvements.

### DEFT Failure Taxonomy (from FINDER, 2025)

DEFT (Deep Research Error and Failure Taxonomy) categorizes 14 failure modes across 4 categories:

```
Finding Failures (retrieval):
  1. Missed critical source            — key evidence never found
  2. Shallow search coverage            — only surface-level results explored
  3. Query formulation errors           — wrong queries lead to wrong results
  4. Source quality misjudgment         — unreliable sources treated as authoritative

Analysis Failures (reasoning):
  5. Incorrect causal reasoning          — wrong cause-effect chains
  6. Premature conclusion                — concludes before sufficient evidence
  7. Conflicting evidence mishandled     — contradictions ignored or averaged
  8. Scope creep or topic drift          — analysis wanders from original question

Synthesis Failures (integration):
  9. Incoherent argument structure       — conclusions don't follow from evidence
  10. Missing key perspective            — one-sided analysis
  11. Fabricated supporting evidence     — hallucinated citations or data

Presentation Failures (output):
  12. Poor organization                  — hard to follow structure
  13. Missing attribution                — claims without sources
  14. Inappropriate level of detail      — too granular or too high-level
```

**Usage**: FINDER constructs a **checklist** of 419 specific items across 100 research tasks. Each checklist item tests a specific factual or analytical requirement. This enables **fine-grained evaluation** — you can measure not just "did the agent succeed" but "which specific aspects did it get right or miss."

### DeepVerifier DRA Failure Taxonomy (2025–2026)

DeepVerifier proposes a 5-category, 13-sub-type taxonomy specifically for Deep Research Agent (DRA) outputs:

```
1. Factual Errors
   - Incorrect claims           — contradicted by source
   - Fabricated information      — no source exists
   - Outdated information        — superseded by newer data

2. Reasoning Errors
   - Logical fallacies           — invalid inferences
   - Overgeneralization          — specific evidence → broad claim
   - False equivalence           — unlike things treated as equivalent

3. Completeness Errors
   - Missing key arguments       — important perspectives omitted
   - Insufficient evidence       — claims inadequately supported
   - Scope gaps                  — parts of the question unanswered

4. Coherence Errors
   - Internal contradictions     — self-contradictory statements
   - Structure disconnects       — sections don't relate logically

5. Attribution Errors
   - Missing citations           — claims without sources
   - Misattribution              — wrong source credited
```

### Using Failure Taxonomies for Evaluation Design

These taxonomies should inform **what you evaluate**, not just **how you evaluate**:

1. **Checklist-based scoring** (FINDER): Break each task into specific, verifiable claims. Score = fraction of checklist items met. This is more reliable than holistic rating.

2. **Rubric-guided verification** (DeepVerifier): For each output section, apply the taxonomy to identify specific failure types. This turns evaluation from "is it good?" into "what specific errors does it contain?"

3. **Process-aware evaluation**: Don't just evaluate the final output. Evaluate intermediate steps — were the right sources found? Were the right analyses performed? (This connects to the Agent-as-a-Judge approach.)

4. **Failure-mode regression**: Track which failure categories your agent is most prone to. Prioritize improvements targeting the most frequent failure modes.

---

*Next: [Planning and Reasoning Strategies](../techniques/08-planning-reasoning.md)*
