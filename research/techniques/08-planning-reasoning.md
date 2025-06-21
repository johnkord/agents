# Planning and Reasoning Strategies for Agents

> *Last updated: 2026-07-15*

## The Central Problem

LLMs are next-token predictors. They don't inherently "plan" or "reason" — they simulate planning and reasoning by generating tokens that follow patterns seen in training data. The challenge is scaffolding these capabilities into reliable, multi-step behavior.

**The gap**: Humans plan, then execute. LLMs generate sequentially. Agent architectures must bridge this gap.

A crucial insight from Anthropic's agent guide: during execution, it's critical for agents to gain **"ground truth" from the environment at each step** (tool call results, code execution output, test results) to assess progress. Agents that plan extensively but don't validate against reality drift quickly. The environment is the oracle — always prefer checking reality over reasoning about it.

## Reasoning Strategies

### Chain-of-Thought (CoT)

The foundational technique. Prompt the model to think step-by-step before producing an answer.

```
Without CoT: "The answer is 42"
With CoT: "Let me think through this step by step:
1. First, I need to identify the variables...
2. Then, calculate the intermediate result...
3. Finally, combine them to get... 42"
```

**Why it works**: Generating reasoning tokens gives the model more "compute" on the problem. Each token is a forward pass through the network — more tokens = more computation.

**Foundational paper**: Wei et al. (2022, NeurIPS) demonstrated that CoT prompting enables LLMs to solve complex reasoning tasks that they fail at with standard prompting. Key findings:
- CoT is an **emergent ability** — it only helps with models above ~100B parameters. Smaller models can produce reasoning traces but they don't improve accuracy.
- CoT is fundamentally **context engineering**: the reasoning traces are context that enables the model to solve problems it otherwise couldn't.
- CoT improves most on tasks requiring multi-step reasoning (math, logic, commonsense physics), with less benefit on simple recall.

**Variants:**
- **Zero-shot CoT**: Just add "Let's think step by step" (Kojima et al., 2022)
- **Few-shot CoT**: Provide examples of worked-through problems
- **Structured CoT**: Force a specific reasoning template
- **Self-Consistency**: Sample multiple CoT paths and vote on the answer (Wang et al., 2022)
- **Auto-CoT**: Automatically construct CoT demonstrations

### Tree-of-Thought (ToT)

Instead of a single reasoning path, explore multiple branches (Yao et al., NeurIPS 2023):

```
                Problem
               /   |   \
          Path A  Path B  Path C
         /    \     |      |   \
       A1    A2    B1    C1    C2
        ✗     ✓     ✗     ✓     ✗
              ↓           ↓
         Evaluate    Evaluate
              ↓           ↓
         Continue    Continue
```

ToT requires four design choices:
1. **Thought decomposition:** How to break the problem into steps
2. **Thought generator:** Sample (diversity) or propose (sequential refinement)
3. **State evaluator:** LLM rates each partial solution's promise (1-10) or votes pass/fail
4. **Search algorithm:** BFS (broad comparison) or DFS (deep exploration with backtracking)

**Key results from the paper:**
- Game of 24: **4% → 74%** success rate (vs standard prompting)
- Creative Writing: Expert judges preferred ToT outputs significantly
- Mini Crosswords: 60% word-level success vs 16% for CoT

**Implementation:**

```python
async def tree_of_thought(problem: str, breadth: int = 3, depth: int = 3):
    """Explore multiple reasoning paths, prune weak branches"""
    
    # Generate initial reasoning branches
    branches = await generate_branches(problem, n=breadth)
    
    for level in range(depth):
        # Evaluate each branch
        scored_branches = []
        for branch in branches:
            score = await evaluate_branch(branch, problem)
            scored_branches.append((score, branch))
        
        # Keep the best branches
        scored_branches.sort(reverse=True)
        survivors = [b for _, b in scored_branches[:breadth]]
        
        # Extend surviving branches
        branches = []
        for branch in survivors:
            extensions = await extend_branch(branch, n=breadth)
            branches.extend(extensions)
    
    # Select the best final branch
    final_scores = [(await evaluate_branch(b, problem), b) for b in branches]
    best = max(final_scores, key=lambda x: x[0])
    return best[1]
```

**When to use**: Complex problems with multiple valid approaches where the first approach tried isn't always best. Math, code architecture decisions, research strategy.

### Self-Consistency

Generate multiple independent solutions, take the majority answer:

```python
async def self_consistency(problem: str, n_samples: int = 5) -> str:
    solutions = await asyncio.gather(*[
        llm.generate(problem, temperature=0.7)
        for _ in range(n_samples)
    ])
    
    # Extract final answers
    answers = [extract_answer(s) for s in solutions]
    
    # Majority vote
    counter = Counter(answers)
    return counter.most_common(1)[0][0]
```

**Why it works**: Random variation in generation means different runs make different mistakes. The correct answer is more frequently generated than any specific incorrect answer.

### SELF-DISCOVER: Self-Composed Reasoning Structures

SELF-DISCOVER (Zhou et al., 2024) enables LLMs to **self-compose task-specific reasoning structures** from a library of atomic reasoning modules:

```
Stage 1 (per task class — offline):
  SELECT  → Pick relevant modules from library of 39 (e.g., "break into subtasks", "think critically")
  ADAPT   → Rephrase modules for the specific task
  IMPLEMENT → Operationalize into JSON reasoning scaffold

Stage 2 (per instance — online):
  Follow the discovered structure, fill in instance-specific reasoning
```

**Key results:**
- +5-32% over CoT on BBH, T4D, MATH benchmarks
- **10-40x fewer inference calls** than CoT-Self-Consistency (for comparable accuracy)
- Discovered structures **transfer across model families** (PaLM 2 → GPT-4 and vice versa)

**Why this matters for agent design:**
- The best reasoning strategy depends on the task — SELF-DISCOVER finds it automatically
- JSON reasoning scaffolds give models **structured slots to fill**, improving reliability
- The discovery step is amortizable (once per task class), making it practical
- This is meta-reasoning: reasoning about how to reason

### Reflexion

Agent reflects on its own performance and explicitly learns from mistakes (Shinn et al., NeurIPS 2023). The full architecture has three components:

1. **Actor:** Generates actions using ReAct or chain-of-thought
2. **Evaluator:** Scores the trajectory (binary pass/fail, or heuristic)
3. **Self-Reflection:** Generates verbal feedback on failures, stored as episodic memory

```
┌─────────┐     ┌───────────┐     ┌───────────────┐
│  Actor  │────▶│ Evaluator │────▶│ Self-Reflection │
│(execute)│     │ (pass/   │     │ ("what went    │
└─────────┘     │  fail?)  │     │  wrong?")      │
     ▲          └───────────┘     └───────┬───────┘
     │                                   │
     │          ┌───────────────┐      │
     └──────────┤ Episodic Memory │◀─────┘
                │ (reflections)  │
                └───────────────┘
```

**Key results:**
- HumanEval: **91%** (vs 80% GPT-4 baseline) — through self-reflection alone, no weight updates
- Ablation: Both self-reflection AND test generation needed together; either alone is insufficient
- The reflections act as a form of **verbal reinforcement learning** — learning from mistakes without gradient updates

```python
async def reflexion_loop(task: str, max_trials: int = 3):
    reflections = []
    
    for trial in range(max_trials):
        # Attempt the task
        result = await agent.execute(task, prior_reflections=reflections)
        
        # Evaluate
        evaluation = await evaluate(result, task)
        
        if evaluation.passed:
            return result
        
        # Reflect on failure
        reflection = await agent.reflect(
            f"Attempt {trial + 1} failed.\n"
            f"Task: {task}\n"
            f"What I did: {result.trajectory}\n"
            f"Why it failed: {evaluation.feedback}\n"
            f"Prior reflections: {reflections}\n\n"
            f"What should I do differently next time?"
        )
        reflections.append(reflection)
    
    return None  # Failed after all trials
```

**Key insight**: Reflections become a form of learned, task-specific memory. The agent builds up a map of "don't do X because Y."

### Critical Analysis: Brittle Foundations of ReAct

Verma et al. (2024, arXiv:2405.13966) present an important corrective to the widespread adoption of ReAct-style reasoning:

**Core finding**: ReAct's performance gains come primarily from **exemplar-query similarity**, not from the interleaved Thought-Act-Observation format.

| Condition | Performance |
|---|---|
| ReAct with well-matched exemplars | High |
| ReAct with random exemplars | Significantly lower |
| Direct prompting with matched exemplars | Comparable to ReAct |
| ReAct without Thought traces | Similar to full ReAct (when exemplars match) |

**Implications for agent builders:**
1. **Don't cargo-cult ReAct** — test whether simpler approaches work first
2. **Invest in exemplar engineering** — dynamic few-shot selection may be higher-leverage than reasoning scaffolds
3. **Reasoning traces still have value** for observability, debugging, and human oversight — even if they don't drive accuracy
4. **The primary lever is context quality** (what's in the prompt), not reasoning format (how it's structured)

This aligns with the broader context engineering thesis: the most impactful intervention is putting the right information in context.

## Planning Strategies

### Hierarchical Task Decomposition

Break complex goals into sub-goals, sub-goals into tasks:

```
Goal: "Build a user authentication system"
├── Sub-goal: Design the data model
│   ├── Task: Define User schema
│   ├── Task: Define Session schema
│   └── Task: Define Permission schema
├── Sub-goal: Implement backend
│   ├── Task: Create registration endpoint
│   ├── Task: Create login endpoint
│   ├── Task: Create password reset flow
│   └── Task: Add session management middleware
├── Sub-goal: Add security measures
│   ├── Task: Implement rate limiting
│   ├── Task: Add password hashing
│   └── Task: Set up CSRF protection
└── Sub-goal: Test and validate
    ├── Task: Write unit tests
    ├── Task: Write integration tests
    └── Task: Security audit
```

**Implementation:**

```python
async def hierarchical_plan(goal: str, max_depth: int = 3) -> TaskTree:
    """Recursively decompose a goal into executable tasks"""
    
    plan = await llm.generate(f"""
    Decompose this goal into 3-5 sub-goals:
    Goal: {goal}
    
    For each sub-goal, indicate:
    - Description
    - Dependencies (which other sub-goals must complete first)
    - Estimated complexity (low/medium/high)
    """)
    
    tree = parse_plan(plan)
    
    for node in tree.nodes:
        if node.complexity in ["medium", "high"] and tree.depth < max_depth:
            # Recursively decompose complex sub-goals
            subtree = await hierarchical_plan(node.description, max_depth - 1)
            node.children = subtree.nodes
    
    return tree
```

### Dependency-Aware Scheduling

Once decomposed, schedule tasks respecting dependencies:

```python
def topological_schedule(tasks: list[Task]) -> list[list[Task]]:
    """Return tasks grouped into parallelizable waves"""
    remaining = set(t.id for t in tasks)
    completed = set()
    waves = []
    
    while remaining:
        # Find tasks whose dependencies are all completed
        ready = [
            t for t in tasks 
            if t.id in remaining and all(d in completed for d in t.dependencies)
        ]
        
        if not ready:
            raise DeadlockError("Circular dependency detected")
        
        waves.append(ready)
        for t in ready:
            remaining.remove(t.id)
            completed.add(t.id)
    
    return waves  # Each wave can be executed in parallel

# Example output:
# Wave 1: [Define User schema, Define Session schema]  ← parallel
# Wave 2: [Create registration endpoint]  ← depends on Wave 1
# Wave 3: [Write unit tests]  ← depends on Wave 2
```

### Adaptive Re-Planning

Plans inevitably need adjustment. Build re-planning into the architecture:

```python
class AdaptivePlanner:
    def __init__(self, agent):
        self.agent = agent
        self.original_plan = None
        self.current_plan = None
        self.execution_log = []
    
    async def execute_with_replanning(self, goal: str):
        self.original_plan = await self.create_plan(goal)
        self.current_plan = self.original_plan.copy()
        
        while self.current_plan.has_remaining_steps():
            step = self.current_plan.next_step()
            
            result = await self.agent.execute_step(step)
            self.execution_log.append(result)
            
            if result.success:
                self.current_plan.mark_complete(step)
            else:
                # Re-plan: maybe the remaining steps need to change
                revised = await self.replan(
                    original_goal=goal,
                    completed_steps=self.current_plan.completed_steps,
                    failed_step=step,
                    failure_reason=result.error,
                    remaining_steps=self.current_plan.remaining_steps
                )
                self.current_plan = revised
    
    async def replan(self, **context) -> Plan:
        return await self.agent.generate_plan(f"""
        The original plan needs revision.
        
        Goal: {context['original_goal']}
        Completed so far: {context['completed_steps']}
        Failed step: {context['failed_step']}
        Failure reason: {context['failure_reason']}
        Original remaining steps: {context['remaining_steps']}
        
        Create a revised plan for the remaining work, accounting for what we've 
        learned from the failure. You can keep, modify, or replace remaining steps.
        """)
```

### Plan Verification

Before executing a plan, verify it's sound:

```python
async def verify_plan(plan: Plan, goal: str) -> PlanVerification:
    """Use LLM to check plan quality before execution"""
    
    verification = await llm.generate(f"""
    Verify this plan for solving the goal:
    
    Goal: {goal}
    Plan:
    {plan.format()}
    
    Check for:
    1. COMPLETENESS: Does the plan cover all aspects of the goal?
    2. ORDERING: Are dependencies respected?
    3. FEASIBILITY: Can each step actually be accomplished with available tools?
    4. EFFICIENCY: Are there unnecessary steps or missing parallelization opportunities?
    5. RISK: What could go wrong? What's the fallback?
    
    Return: APPROVED, NEEDS_REVISION, or REJECTED with explanation.
    """)
    
    return parse_verification(verification)
```

## Reasoning Under Uncertainty

### When the Agent Doesn't Know

The most dangerous failure is false confidence. Strategies for handling uncertainty:

```python
UNCERTAINTY_PROMPT = """
When you encounter uncertainty, classify it:

1. RESOLVABLE UNCERTAINTY: "I don't know X, but I can find out using tool Y"
   → Use the appropriate tool to resolve it

2. BOUNDED UNCERTAINTY: "X could be A or B, and the choice matters"
   → State both options and their implications, ask the user

3. IRREDUCIBLE UNCERTAINTY: "I can't determine X with available tools"
   → Explicitly state this limitation, suggest alternatives

4. RISKY UNCERTAINTY: "I think X is true but if I'm wrong, the consequences are serious"
   → Verify before acting, or apply the safer default

NEVER pretend to know something you don't. Your credibility depends on accurately 
representing what you know vs. what you're uncertain about.
"""
```

### Reasoning About Time and State

Agents often struggle with temporal reasoning — understanding that the codebase changes as they work:

```python
# Anti-pattern: Agent reads file, plans based on reading, 
# executes plan much later when file may have changed

# Better: Re-verify assumptions before critical actions
class StateAwareAgent:
    async def execute_step(self, step):
        # Verify prerequisites still hold
        for assumption in step.assumptions:
            if not await self.verify(assumption):
                return StepResult(
                    success=False, 
                    error=f"Assumption no longer holds: {assumption}"
                )
        
        # Execute with fresh state
        result = await self.do(step.action)
        
        # Verify postconditions
        for postcondition in step.expected_postconditions:
            if not await self.verify(postcondition):
                return StepResult(
                    success=False,
                    error=f"Postcondition not met: {postcondition}"
                )
        
        return StepResult(success=True, output=result)
```

## The Reasoning-Action Balance

```
Pure Reasoning ◄──────────────────────────► Pure Action
(Overthink,      (Balanced: Think     (Act randomly,
 never act)       just enough,         no strategy)
                  then execute)
```

**Failure mode 1: Analysis paralysis** — The agent spends 20 steps reasoning and planning but never acts. Common with overly verbose system prompts that emphasize caution.

**Failure mode 2: Impulsive action** — The agent acts immediately without thinking. Common with low-temperature settings and action-oriented prompts.

**The sweet spot depends on task type:**
- **Quick fixes**: Bias toward action (identify → fix → verify)
- **Architecture decisions**: Bias toward reasoning (analyze → compare → decide → implement)
- **Debugging**: Balanced (hypothesize → test → refine → fix)

## Compound Reasoning Patterns

### Hypothesis-Driven Development (for debugging agents)

```
1. OBSERVE: Gather symptoms (error messages, failing tests, logs)
2. HYPOTHESIZE: Form 2-3 possible explanations
3. RANK: Order hypotheses by likelihood
4. TEST: Design a minimal test for the top hypothesis
5. DIAGNOSE: Based on test result, confirm or reject hypothesis
6. FIX: If confirmed, implement fix. If rejected, test next hypothesis.
7. VERIFY: Confirm the fix resolves the original symptoms
```

### Analogical Reasoning

"This problem is similar to X, and for X the solution was Y":

```xml
<system_prompt>
When approaching unfamiliar problems, consider:
- Have you seen a similar problem before? What was the approach?
- Can this problem be transformed into a known problem type?
- What would a senior engineer who has seen this pattern many times do?

Draw on analogies from your training data, but verify they apply to the current context.
</system_prompt>
```

### Constraint Propagation

When given constraints, propagate their implications before planning:

```
Constraints:
- Must use Python 3.9+
- Cannot install new packages (use stdlib only)
- Must be backward compatible with existing API

Propagated implications:
- Cannot use match/case (Python 3.10+)
- Cannot use tomllib (Python 3.11+)
- HTTP parsing: use urllib, not requests
- JSON handling: use json (stdlib)
- Must keep existing function signatures unchanged
- New features must be additive
```

## Metacognition: Thinking About Thinking

The highest-leverage improvement is giving agents the ability to monitor their own reasoning.

### LATS: Tree Search as Planning (Zhou et al., ICML 2024)

LATS (Language Agent Tree Search) unifies reasoning, acting, and planning by applying **Monte Carlo Tree Search** to agent execution:

1. **Selection:** Choose the most promising action path using UCT (exploration-exploitation balance)
2. **Expansion:** Generate n possible next actions
3. **Evaluation:** Use environment feedback (not just self-assessment) to score states
4. **Backpropagation:** Update value estimates up the tree
5. **Reflection:** Generate self-reflections from failed paths (recycled as context)

**Key results:**
- HumanEval: **80% → 92.7%** (same GPT-4, just adding tree search)
- WebShop: **50% → 75.9%** (comparable to fine-tuned models)
- The key advantage over ToT: LATS uses **actual environment feedback** rather than LLM self-evaluation

**When to use LATS vs simpler approaches:**

| Approach | Cost | Best For |
|---|---|---|
| ReAct (linear) | Low | Simple tasks, real-time |
| Reflexion (retry) | Medium | Tasks with clear success/fail signals |
| ToT (tree search) | High | Large search spaces, no environment |
| LATS (MCTS) | Very High | Complex tasks where accuracy justifies cost |

```xml
<metacognition_prompt>
After every 3-5 steps, pause and ask yourself:
1. Am I making progress toward the goal?
2. Am I going in circles?
3. Is my current approach working, or should I try something different?
4. What's the most important thing I don't know right now?
5. Am I being efficient with my tool calls?

If you answer "no" or "I don't know" to questions 1-3, STOP and re-plan before continuing.
</metacognition_prompt>
```

This turns the agent from a reactive system into a self-monitoring system — a qualitative leap in reliability.

## Self-Reflection & Verification (2025–2026 Research)

A wave of 2025–2026 papers establishes self-reflection and verification as **trainable capabilities** rather than just prompting strategies. This represents a paradigm shift from Reflexion (2023), which relied on prompted verbal feedback.

### The Three Eras of Self-Correction

```
Era 1: PROMPTED (2023)
  Reflexion: "What went wrong?" → store in episodic memory → try again
  Limitation: Reflection quality depends entirely on model prompting
  Problem: LLMs fail to correct errors >50% of the time without external guidance

Era 2: STRUCTURED (2025)
  RE-Searcher: Explicit goal → search → reflect(goal met?) → continue/revise
  Structured Reflection: Trainable Reflect→Call→Final with DAPO+GSPO
  Reflection-Driven Control: Plan-Reflect-Verify with pluggable Reflex Module
  Advance: Reflection becomes a distinct, trainable action with RL rewards

Era 3: INTERNALIZED (2025-2026)
  SPOC: Interleaved solution+verification in single pass (no external trigger)
  CoRefine: 211K-param controller using confidence to decide HALT/RETHINK/ALTERNATIVE
  Dyna-Think: World model simulation embedded in thinking process
  Advance: Verification becomes part of generation, not a separate step
```

### Plan-Reflect-Verify Pattern

The Plan-Reflect-Verify loop (from Reflection-Driven Control, AAAI 2026) adds a pluggable **Reflex Module** to any agent:

```
┌──────────┐     ┌──────────┐     ┌──────────┐
│  Plan    │────▶│  Execute │────▶│  Reflect  │
│(strategy)│     │ (action) │     │(self-check)│
└──────────┘     └──────────┘     └─────┬─────┘
     ▲                                   │
     │           ┌──────────────┐        │
     └───────────┤ Reflective   │◄───────┘
                 │ Memory Repo  │
                 └──────────────┘
```

The Reflex Module has three components:
1. **Lightweight Self-Checker**: Quick assessment — did the action achieve its goal?
2. **Reflective Prompt Engine**: If self-check fails, generates targeted reflection
3. **Reflective Memory Repository**: Stores past reflections for future reference

**Key insight**: The Reflex Module is *pluggable* — it can attach to any existing agent without modifying the base agent's architecture.

### Confidence-Guided Self-Refinement (CoRefine)

CoRefine (2026) treats refinement as an **exploration-exploitation tradeoff** controlled by confidence:

```python
# Conceptual model of CoRefine's controller
class ConfidenceController:
    """211K-parameter Conv1D controller on frozen LLM"""
    
    def decide(self, confidence_trace: list[float]) -> Action:
        """
        confidence_trace: token-level confidence from each reasoning step
        Returns: HALT | RETHINK | ALTERNATIVE
        """
        # HALT: Answer is likely correct (high, stable confidence)
        # RETHINK: Re-examine same approach (dip in confidence)  
        # ALTERNATIVE: Try completely different approach (persistent low confidence)
        features = extract_features(confidence_trace)  # early/mid/late phases
        return self.controller(features)
```

**Results**: 
- 190x token reduction vs 512-sample majority voting
- 92.6% precision when controller confidently halts
- Average ~2.7 refinement steps per problem

**Why this matters**: The controller is tiny (211K params), frozen-LLM-compatible, and requires no backbone fine-tuning. It's a modular primitive that can be added to any reasoning agent.

### Spontaneous Self-Correction (SPOC)

SPOC (2025) enables **single-pass interleaved solution and verification**:

```
Generate solution₁ → Verify₁ → (if wrong) Generate solution₂ → Verify₂ → ...
                      ↓ (if correct)
                    Output
```

The model assigns itself dual roles (proposer + verifier) with shared parameters. Training:
1. **PairSFT**: Bootstrap multi-turn generation style from the initial model's correct/incorrect outputs
2. **Message-wise Online RL**: RLOO with process rewards for each solution or verification step

**Key finding**: Data balancing is critical — reweighting correct/incorrect subsets to equal scale leads to higher verification accuracy and more stable RL training.

**Results**: +8.8-20% on math benchmarks (MATH500, AMC23, AIME24) across model sizes.

### World Model Simulation (Dyna-Think)

Dyna-Think (2025, Microsoft Research + Columbia) integrates **compressed world model simulation** into the agent's thinking process:

```
Standard reasoning:  Think → Act → Observe → Think → Act ...
Dyna-Think:         Think → [Simulate what will happen] → Act → Observe → ...
```

Two training stages:
1. **DIT (Imitation Learning)**: Reconstruct R1's thinking to focus on action-relevant world simulation
2. **DDT (Dyna Training)**: Two-stage online training — world model training (predicting next states, generating critiques) then policy training

Three world model objectives:
- **Next-state prediction**: What will the screen look like after this action?
- **State-change prediction**: What specifically changes?  
- **Critique generation**: What could go wrong? (Most effective for policy improvement)

**Results**: 32B Dyna-Think model achieves similar best-of-n performance to 685B R1 with 2x fewer tokens.

### Planning Tokens Need Special Treatment (DeepPlanner)

DeepPlanner (2025, Amazon + HKUST) discovered that **planning tokens exhibit 2.4x higher entropy than execution tokens** during RL training. This means vanilla GRPO under-optimizes planning:

```
Token entropy during RL training:
  Planning tokens: 0.78 average entropy
  Execution tokens: 0.32 average entropy
```

**Solution**: Entropy-based advantage shaping — append an entropy-shaped term to token-level advantages, amplifying gradients on high-entropy (planning) tokens. Plus selective upweighting of efficient rollouts.

**Result**: SOTA on deep research benchmarks with 10x fewer training samples than previous SOTA.

**Practical implication**: When training agents with RL, treat planning and execution as distinct phases with different optimization priorities.

### Goal-Oriented Reflection (RE-Searcher)

RE-Searcher (2025) quantifies a critical fragility: a single-word change in a search query can cause cosine similarity of results to drop below 0.6. Their solution — force explicit goal articulation and reflection:

```xml
<search>
  <query>QUERY</query>
  <goal>GOAL: What specific information am I trying to find?</goal>
</search>
<!-- After receiving results -->
<think>Analyze whether results meet the goal</think>
<reflect>True/False</reflect>  <!-- True = goal met, False = refine query -->
```

**Key insight**: The combination of goal-oriented planning (what am I looking for?) and self-reflection (did I find it?) enables the agent to **resist spurious cues** from noisy search environments.

### The Ultimate Metacognitive Tool: Stopping

The highest form of agent metacognition is knowing when to **stop and ask for help**. The 12-Factor Agents framework (Factor 7) models this as a tool call:

```python
def request_clarification(question: str, options: list[str] = None) -> str:
    """Stop execution and ask the human for guidance.
    Use when: the task is ambiguous, risks are high, or you've tried 
    2+ approaches and none worked."""
    ...
```

Agents that spiral for 50 steps on an impossible task waste time and tokens. Agents that recognize impasses after 3-5 attempts and escalate are dramatically more useful in practice. Build the escape hatch early.

---

*Next: [Agent Implementation Blueprint](../../blueprints/generic-agent/09-implementation-blueprint.md)*
