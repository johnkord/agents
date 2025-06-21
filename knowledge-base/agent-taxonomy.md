# Agent & Copilot Taxonomy

> *Last updated: 2026-07-15*

A comprehensive reference of agent types, their characteristics, and design considerations.

> **Key distinction (Anthropic, 2024):** *"Workflows"* are systems where LLMs and tools are orchestrated through predefined code paths. *"Agents"* are systems where LLMs dynamically direct their own processes and tool usage. Most production "agents" are actually **workflows** — and that's fine. Levels 0-2 below are predominantly workflows; true agency begins at Level 3.

---

## By Autonomy Level

### Level 0: Completion Assistants
**No loop. Single inference call.**

| Aspect | Details |
|---|---|
| Examples | GitHub Copilot inline suggestions, autocomplete |
| Architecture | Single LLM call per interaction |
| Loop | None |
| Tools | None (or implicit, like code context) |
| Human involvement | Human accepts/rejects each suggestion |
| Reliability | High (bounded output) |
| Cost | Low |

### Level 1: Copilots (Conversational Tool Users)
**Human-in-the-loop per turn. Multi-turn conversation with tool use.**

| Aspect | Details |
|---|---|
| Examples | ChatGPT with plugins, Copilot Chat, cursor tab |
| Architecture | ReAct or simple tool loop |
| Loop | 1-5 tool calls per turn, then wait for human |
| Tools | Search, read files, simple edits |
| Human involvement | Human reviews every response, guides direction |
| Reliability | Medium-High |
| Cost | Medium |

### Level 2: Semi-Autonomous Agents
**Execute multi-step plans, human approves checkpoints.**

| Aspect | Details |
|---|---|
| Examples | Copilot agent mode, Cursor Composer |
| Architecture | Plan-and-Execute or Autonomous with checkpoints |
| Loop | 5-30 tool calls, pausing at critical decisions |
| Tools | Full toolbelt (files, terminal, search, git) |
| Human involvement | Reviews plan, approves destructive actions |
| Reliability | Medium |
| Cost | Medium-High |

### Level 3: Autonomous Agents
**Given a goal, execute to completion with minimal intervention.**

| Aspect | Details |
|---|---|
| Examples | Devin, SWE-Agent, Claude Code (autonomous mode) |
| Architecture | Autonomous agent with tool belt, self-evaluation |
| Loop | 10-100+ tool calls |
| Tools | Extensive (files, terminal, browser, APIs, git) |
| Human involvement | Sets goal, reviews final output |
| Reliability | Variable (60-90%) — $0.95^{20}$ per-step accuracy ≈ 36% end-to-end |
| Cost | High |

> **Design insight (12-Factor Agents):** Even L3 agents should expose a `contact_human` tool — not as a failure state, but as a *smart* tool call. The agent decides when the cost of guessing exceeds the cost of asking.

### Level 4: Multi-Agent Systems
**Teams of agents coordinating on complex goals.**

| Aspect | Details |
|---|---|
| Examples | AutoGen teams, CrewAI crews, custom orchestrations |
| Architecture | Orchestrator-Workers, Peer-to-Peer, Hierarchical |
| Loop | Distributed across agents |
| Tools | Per-agent specialization |
| Human involvement | Defines problem, sets constraints |
| Reliability | Variable (errors compound across agents) |
| Cost | Very High |

> **Reality check:** Most teams that attempt L4 discover that a single well-designed L3 agent with good tools outperforms a poorly coordinated multi-agent system. Start with small, focused agents (12-Factor Factor 10) before reaching for multi-agent orchestration.

---

## By Domain

### Coding Agents

| Agent Type | Primary Task | Key Tools | Key Challenges |
|---|---|---|---|
| **Bug Fixer** | Diagnose and fix bugs | Debugger, test runner, grep, file editor | Reproducing the bug, not introducing regressions |
| **Feature Builder** | Implement new features from specs | All coding tools + documentation | Understanding requirements, architectural fit |
| **Refactoring Agent** | Improve code structure | AST tools, test runner, linter | Maintaining behavior while changing structure |
| **Code Reviewer** | Review PRs for quality | Git diff, linter, security scanner | Balancing strictness with pragmatism |
| **Test Writer** | Generate comprehensive tests | Test frameworks, coverage tools | Edge cases, meaningful assertions (not just coverage) |
| **Migration Agent** | Upgrade dependencies, frameworks | Package manager, codemods | Breaking changes, implicit dependencies |
| **Documentation Agent** | Generate/update docs | Code reader, doc templates | Accuracy, appropriate detail level |

### Research & Knowledge Agents

| Agent Type | Primary Task | Key Tools | Key Challenges |
|---|---|---|---|
| **Deep Research** | Comprehensive, analyst-level research on complex topics | Web search, web browse, code interpreter, PDF parser, citation tools | Evidence integration, verification, avoiding content fabrication, long-horizon planning |
| **Literature Review** | Survey academic papers | Academic search, paper analyzer | Comprehensiveness, synthesis quality |
| **Competitive Analysis** | Analyze competitors/alternatives | Web search, data extraction | Currency of information, objectivity |
| **Knowledge Base Builder** | Organize information into structured KB | File system, search, summarizer | Taxonomy design, keeping updated |
| **Fact Checker** | Verify claims against sources | Web search, document analyzer | Source reliability, handling ambiguity |
| **Self-Correcting Agent** | Any task with built-in verification loops | Same as base task + confidence signals, verification rubrics | When to stop refining, avoiding over-correction, verifier blind spots |

> **Deep Research — expanded view**: Deep research agents represent the most complex agent type, requiring *all* atomic capabilities: planning & task decomposition, deep information seeking, reflection & verification, and structured report writing. The 2025 deep research papers establish two viable architectures: (1) **Single-agent autonomous** (SFR-DR, Step-DeepResearch) — a single reasoning model with minimal tools (search, browse, code) that self-manages its memory and decides its own next action; and (2) **Meta-multi-agent** (MAS², ResearStudio) — specialized teams where generator/implementer/rectifier agents construct and adapt the research workflow dynamically. Single-agent systems generalize better to unseen tasks; multi-agent systems excel at specific, well-defined workflows.
>
> Key insight from failure analysis (FINDER/DEFT): DRAs fail primarily at **evidence integration** (insufficient/contradictory evidence) and **verification** (strategic content fabrication, fact-checking failures), not at task comprehension. The implication: invest in verification capabilities, not just retrieval capabilities.
>
> The training recipe (emerging consensus): Start from a reasoning-optimized model → agentic mid-training → SFT on atomic capabilities → RL with AI-feedback rewards (RLOO > GRPO). Use error-tolerant inference that lets agents recover from mistakes rather than terminating.

### Data & Analytics Agents

| Agent Type | Primary Task | Key Tools | Key Challenges |
|---|---|---|---|
| **Data Analyst** | Answer questions from data | SQL, Python/pandas, charts | Correct queries, meaningful visualizations |
| **ETL Agent** | Transform data between formats | File I/O, parsers, validators | Edge cases, data quality |
| **Anomaly Detector** | Find unusual patterns | Stats tools, time series analysis | False positives, explaining findings |
| **Report Generator** | Create data-driven reports | Query tools, chart libs, templates | Narrative coherence, accuracy |

### Operations & DevOps Agents

| Agent Type | Primary Task | Key Tools | Key Challenges |
|---|---|---|---|
| **Deployment Agent** | Deploy applications | CI/CD tools, cloud APIs, health checks | Safety, rollback capability |
| **Incident Responder** | Diagnose and mitigate issues | Log analyzer, monitoring APIs, runbooks | Speed, not making things worse |
| **Infrastructure Agent** | Manage cloud resources | Terraform, cloud CLIs, config managers | Cost, security, drift |
| **Monitoring Agent** | Continuous health observation | Metrics APIs, alerting systems | Alert fatigue, false positives |

### Creative & Content Agents

| Agent Type | Primary Task | Key Tools | Key Challenges |
|---|---|---|---|
| **Writing Assistant** | Draft and refine content | Editor, style checker, research | Voice consistency, originality |
| **Design System Agent** | Maintain design system consistency | Component library, style validators | Aesthetic judgment, accessibility |
| **Content Pipeline** | Process content through stages | Parsers, formatters, validators | Format preservation, quality gates |

---

## By Architecture Pattern (Cross-Reference)

| Architecture | Best Agent Types | Why |
|---|---|---|
| **ReAct** | Simple Q&A, single-tool tasks | Low overhead, direct |
| **Plan-Execute** | Feature building, migrations, complex fixes | Need for forethought |
| **Plan-Reflect-Verify** | Deep research, complex retrieval, code generation | Closed-loop self-correction; RE-Searcher and Reflection-Driven Control demonstrate the pattern |
| **Router** | Multi-domain copilots, customer service | Different tools per domain |
| **Evaluator-Optimizer** | Code generation, writing, analysis | Iterative improvement |
| **Generator-Verifier** | Math reasoning, research synthesis, any high-accuracy task | Separate lightweight verifier catches generator errors; CoRefine, SPOC, DeepVerifier |
| **Orchestrator-Workers** | Research, code review across files | Parallelizable subtasks |
| **Meta-MAS (MAS²)** | Complex deep research, adaptive workflows | Generator-implementer-rectifier team that constructs and adapts agent systems per-task |
| **Pipeline** | ETL, content processing, report generation | Known workflow structure |
| **Autonomous + Self-Managed Memory** | Deep research, long-horizon tasks | SFR-DR's memory buffer; agents that clean their own context |
| **Autonomous** | General-purpose, exploratory tasks | Maximum flexibility |

---

## Design Decision Matrix

When building a new agent, answer these questions:

```
1. AUTONOMY: How much should it do without asking?
   □ One suggestion at a time (L0-L1)
   □ Multi-step with checkpoints (L2)
   □ Full task completion (L3)
   □ Team coordination (L4)

2. DOMAIN: How specialized is it?
   □ Single domain (e.g., only Python code)
   □ Multi-domain (e.g., code + docs + tests)
   □ General purpose

3. RELIABILITY REQUIREMENT:
   □ Best-effort (internal tool, occasional failures OK)
   □ Production (95%+ success rate needed)
   □ Critical (99%+ with formal verification)

4. LATENCY TOLERANCE:
   □ Real-time (<2 seconds)
   □ Interactive (<30 seconds)
   □ Background (minutes OK)
   □ Async (hours OK)

5. COST SENSITIVITY:
   □ Minimal (use cheapest model that works)
   □ Balanced (pay more for critical steps)
   □ Quality-first (use best model always)

6. USER TECHNICAL LEVEL:
   □ Developers (can interpret technical output)
   □ Technical non-developers (need some translation)
   □ Non-technical (need fully polished output)

7. HUMAN CONTACT STRATEGY:
   □ Every turn (copilot — human approves each step)
   □ Checkpoints (pause at destructive or ambiguous actions)
   □ Tool-based (agent decides when to ask via contact_human tool)
   □ Review-only (human reviews final output)
```

These answers map to specific architectural choices:

| Answer Combo | Recommended Architecture |
|---|---|
| L1 + Single domain + Real-time | ReAct with small model |
| L2 + Multi-domain + Interactive | Router → Plan-Execute |
| L3 + Code + Background | Autonomous with Evaluator |
| L4 + Research + Async | Orchestrator-Workers with debate |
| L2 + General + Critical | Plan-Execute with human checkpoints |

---

## Anti-Taxonomy: What NOT to Build

| Don't Build | Why | Build Instead |
|---|---|---|
| Universal agent that does everything | Jack of all trades, master of none | Router over specialized agents |
| Fully autonomous agent for prod on day 1 | Trust must be earned | Start as copilot, increase autonomy |
| Agent without an escape hatch | It will get stuck and spiral | Agent with "I'm stuck" detection + `contact_human` tool |
| Agent that only talks | Users want results, not conversation | Agent with working tool belt |
| Agent that never explains | Users can't trust what they can't audit | Agent with transparent reasoning |
| Framework-locked agent | Trapped behind abstractions you can't debug | Own your prompts, own your context window, own your control flow |
| Agent with vague tool descriptions | LLM won't know when or how to use tools | Invest more in ACI (Agent-Computer Interface) than in prompts |

---

*Back to [Index](../../README.md)*
