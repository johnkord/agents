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

## Change Plan (AgentAlpha v1.x → v1.1)

### 1. New Code Artifacts
| Layer | File | Brief |
|-------|------|-------|
| Interfaces | `Interfaces/IPlanEvaluator.cs` | Contract for evaluating plans. |
| Services   | `Services/PlanEvaluator.cs` | OpenAI-backed implementation. |
| Helpers    | `Services/PlanRefinementLoop.cs` | Iterative loop driver (wrapper around `IPlanner`). |
| Config     | `Configuration/PlanQualityConfig.cs` | Threshold + limits, bound from env. |
| Tests      | `tests/AgentAlpha.Tests/PlanEvaluatorTests.cs` | Unit tests (mock OpenAI). |
|            | `tests/AgentAlpha.Tests/PlanRefinementLoopTests.cs` | Loop termination & score increase. |

### 2. Modified Artifacts
| File | Change |
|------|--------|
| `SimpleTaskExecutor.cs` | After initial `CreatePlanAsync`, call `PlanRefinementLoop.RefinePlanAsync`. |
| `ServiceCollectionExtensions.cs` | Register `PlanEvaluator`, `PlanRefinementLoop`, and `PlanQualityConfig`. |
| `AgentConfiguration.cs` | Add `PlanQualityTarget`, `MaxPlanRefinements` with env parsing (`PLAN_QUALITY_TARGET`, `MAX_PLAN_REFINEMENTS`). |
| `README-AI-ARCHITECTURE.md` | Document new interface, env-vars, and test coverage. |

### 3. Runtime Flow (updated)
```
plan = _planner.CreatePlanAsync()
plan = _planRefiner.RefinePlanAsync(plan, task) // NEW
conversationManager.InitializeConversation(plan)
```
The loop short-circuits if:
* score ≥ `PlanQualityTarget`
* score stagnates for 2 consecutive iterations
* attempts ≥ `MaxPlanRefinements`

### 4. Configuration (env → defaults)
| Var | Default | Description |
|-----|---------|-------------|
| `PLAN_QUALITY_TARGET` | `0.80` | Pass threshold, 0–1. |
| `MAX_PLAN_REFINEMENTS` | `3`    | Hard cap on iterations. |
| `EVALUATOR_MODEL` | `gpt-4.1-nano` | Compact model for cheap scoring. |

### 5. Telemetry
Adds `Session.Metadata.EvaluatorStats`  
```json
{ "Attempts": 2, "FinalScore": 0.83 }
```
and logs each evaluation at **Information** level.

### 6. Test Surface
- **Unit**  
  - `PlanEvaluatorTests` – JSON parsing, fallback on malformed JSON.  
  - `PlanRefinementLoopTests` – loop exits on target / max iterations / stagnation.  
- **Integration**  
  - `SimpleTaskExecutorPlanQualityTests` – verify that executor calls refiner and emits final plan to session log.  

### 7. Implementation Checklist
- [x] Define `IPlanEvaluator` interface.
- [x] Implement `PlanEvaluator` with OpenAI call & JSON parse.
- [x] Add `PlanQualityTarget` & `MaxPlanRefinements` env parsing to config.
- [x] Register evaluator & loop in DI.
- [x] Extend `IPlanner` with `RefinePlanAsync` + implement in `ChainedPlanner`.
- [x] Extend tests (PlanEvaluator/RefinementLoop).   <!-- toggled -->
- [x] Verify build & all unit tests pass.