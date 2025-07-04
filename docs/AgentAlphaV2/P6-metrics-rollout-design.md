# P6 – Metrics & Roll-out - Deferred until later, not planned for implementation until future

## Objectives
Collect latency, token usage, success-rate metrics and implement gradual enablement flags for P1-P5 features.

## Metrics
| Metric | Source | Export |
|--------|--------|--------|
| `conversation_tokens_total` | ConversationManager.Usage | Prometheus |
| `tool_latency_seconds` | ParallelToolRunner | Prometheus |
| `plan_quality_score` | PlanEvaluator | Logs/Prom |

### Implementation
- Add `IMetricsCollector` abstraction with Prometheus.Net default impl.  
- Inject into services (optional dependency).  
- Each new component records measurements.

## Feature Flags
Environment vars: `ENABLE_ROUTER`, `ENABLE_CHAINED_PLANNER`, … default `false`.

## Roll-out Plan
1. Deploy with metrics only (`flags=false`).  
2. Observe baselines for 1 week.  
3. Enable P1 in canary (10 %).  
4. Gradually enable others conditioned on KPIs.  
