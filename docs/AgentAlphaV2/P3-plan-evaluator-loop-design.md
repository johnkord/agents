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

## Testing
- Mock evaluator to force low score → ensure loop runs.