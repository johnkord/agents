# P3 – Plan Evaluation Loop

## Overview
Iteratively refine plans until evaluation score ≥ threshold.

## New Elements
- `PlanEvaluator` (service) – returns numeric score + feedback.  
- `PlanRefinementLoop` (helper) – loop `Planner → Evaluator` max _N_ iterations.

## API
```csharp
public record EvaluationResult(double Score, string Feedback);
public interface IPlanEvaluator
{
    Task<EvaluationResult> EvaluateAsync(string plan, string task);
}
```

## Implementation

### 1. PlanEvaluator Service

#### Purpose
Evaluates the quality of a plan and provides a score with feedback for refinement.

#### Responsibilities
- Analyze the plan structure and content
- Execute the plan using a dry-run mode if applicable
- Compare actual vs. expected outcomes
- Generate a numeric score and feedback for improvement

#### Example Implementation
```csharp
public class PlanEvaluator : IPlanEvaluator
{
    public async Task<EvaluationResult> EvaluateAsync(string plan, string task)
    {
        // Analyze plan structure
        var structureScore = await EvaluateStructureAsync(plan);
        
        // Execute plan and compare results
        var executionScore = await EvaluateExecutionAsync(plan, task);
        
        // Combine scores for final evaluation
        var finalScore = (structureScore + executionScore) / 2;
        
        // Generate feedback
        var feedback = GenerateFeedback(finalScore, structureScore, executionScore);
        
        return new EvaluationResult(finalScore, feedback);
    }
}
```

### 2. PlanRefinementLoop Helper

#### Purpose
Facilitates the iterative refinement of plans based on evaluation feedback.

#### Responsibilities
- Execute the planning and evaluation in a loop
- Apply feedback to improve the plan
- Limit the number of refinement attempts

#### Example Implementation
```csharp
public class PlanRefinementLoop
{
    private readonly IPlanner _planner;
    private readonly IPlanEvaluator _evaluator;
    private readonly AgentConfiguration _config;

    public PlanRefinementLoop(IPlanner planner, IPlanEvaluator evaluator, AgentConfiguration config)
    {
        _planner = planner;
        _evaluator = evaluator;
        _config = config;
    }

    public async Task<string> RefinePlanAsync(string initialPlan, string task)
    {
        var plan = initialPlan;
        var attempts = 0;
        
        // Initial evaluation
        var evaluation = await _evaluator.EvaluateAsync(plan, task);
        
        while (evaluation.Score < _config.PlanQualityTarget && attempts < _config.MaxRefinements)
        {
            attempts++;
            plan = await _planner.RefinePlanAsync(plan, evaluation.Feedback);
            evaluation = await _evaluator.EvaluateAsync(plan, task);
        }
        
        return plan;
    }
}
```

## Flow
`TaskExecutor`
1. `plan = planner.CreatePlanAsync()`
2. `eval = evaluator.EvaluateAsync(plan)`
3. `while (eval.Score < cfg.PlanQualityTarget && attempts < cfg.MaxRefinements) planner.RefinePlanAsync(plan, eval.Feedback)`

## Config
```
PLAN_QUALITY_TARGET=0.8
MAX_PLAN_REFINEMENTS=3
```

## Change Plan (AgentAlpha v0.x → v1.x)

### 1. New Code Artifacts
| Layer | File | Brief |
|-------|------|-------|
| Interfaces | `Interfaces/IPlanEvaluator.cs` | Contract for evaluating plans. |
| Services | `Services/PlanEvaluator.cs` | OpenAI-backed implementation. |
| Helpers  | `Helpers/PlanRefinementLoop.cs` | Iterative loop driver. |
| Config   | `Configuration/PlanQualityConfig.cs` | Thresholds + limits. |
| Tests    | `tests/PlanEvaluatorTests.cs` | Unit tests with mocked OpenAI. |

### 2. Modified Artifacts
| File | Change |
|------|--------|
| `TaskExecutor.cs` | Insert evaluation loop after planner call. |
| `ServiceCollectionExtensions.cs` | Register `PlanEvaluator`, `PlanRefinementLoop`, config. |
| `AgentConfiguration.cs` | Add `PlanQualityTarget`, `MaxPlanRefinements` with env parsing. |
| `README-AI-ARCHITECTURE.md` | Document new interface & config vars. |
| `Program.cs` | No change – DI already centralised. |

### 3. OpenAI Request Patterns
1. `PlanEvaluator` sends the **plan + task** to a compact “evaluation” model (`gpt-4.1-nano`) with system prompt:
   > “You are a strict plan critic. Return `{{\"score\":double, \"feedback\":string}}` JSON only.”
2. Parse JSON → `EvaluationResult`.
3. Scores range 0-1; feedback ≤ 512 tokens.

### 4. Loop Logic (in `PlanRefinementLoop`)
```
attempts = 0
eval = Evaluate(plan)
while eval.Score < target && attempts < max:
    plan = Planner.RefinePlan(plan, eval.Feedback)
    eval  = Evaluate(plan)
    attempts++
return plan
```
If refinement fails to improve score for two consecutive iterations, **break early** to avoid infinite loops.

### 5. Configuration
```
PLAN_QUALITY_TARGET    default=0.8   // double 0-1
MAX_PLAN_REFINEMENTS   default=3     // int ≥0
EVALUATOR_MODEL        default=gpt-4.1-nano
```

### 6. Telemetry
- Increment `Session.Metadata.EvaluatorStats.{Attempts,FinalScore}`.
- Log each evaluation with `{Score,Attempts}` at **Information** level.

### 7. Failure Modes & Mitigations
| Failure | Mitigation |
|---------|------------|
| Evaluator returns non-JSON | Fallback to score 0 + generic feedback. |
| Plan never reaches target  | After `MaxPlanRefinements`, proceed with best plan, log **Warning**. |
| Token exhaustion           | Use `MaxOutputTokens=300` for evaluator prompts. |

## Implementation Checklist
- [ ] Define `IPlanEvaluator` interface.
- [ ] Implement `PlanEvaluator` with OpenAI call & JSON parse.
- [ ] Add `PlanQualityConfig` section to `AgentConfiguration` + `FromEnvironment`.
- [ ] Register evaluator & loop in `ServiceCollectionExtensions`.
- [ ] Extend `TaskExecutor.ExecuteAsync` to call refinement loop.
- [ ] Unit test evaluator parsing & loop termination logic.
- [ ] Update `README-AI-ARCHITECTURE.md` and env-var docs.
- [ ] Verify DI graph (`dotnet build`) and run `dotnet test`.