# What Real People Think About Life Agents, Personal AI Assistants, and Proactive AI

> **Research compiled from 11 academic papers and a curated knowledge base document in the agents workspace. All quotes, statistics, and findings are extracted directly from paper content — not synthesized from memory.**

---

## 1. What Users Actually Said

Direct quotes from user study participants across multiple papers:

### On Wanting Control

> **P92** (Choose Your Agent, N=243): *"I love the advisor. It helps when you get into the weeds of the game when the strategies become less obvious. I also like that I still have full control."*

> **P2** (ProMemAssist, N=12): *"It felt like I was more in control [in ProMemAssist], even when it reminded me of things. It was helpful, not pushy."*

> **P96** (Choose Your Agent): *"I don't trust AI bots; I feel I can make better decisions on my own."*

### On Wanting Delegation

> **P47** (Choose Your Agent): *"Delegate helped with ease of decision making and made it easiest for me."*

> **P99** (Choose Your Agent): *"I prefer that someone else make the decision."*

### On Being Coached

> **P266** (Choose Your Agent): *"Coach helped me see things that I didn't see myself like a real coach."*

### On Timing and Interruptions

> **P5** (ProMemAssist): *"I felt like if I'm currently working on some task and then I have, like, some cognitive load, you shouldn't tell me too much, unless it's important."*

> **P9** (ProMemAssist): *"I liked that it wasn't always talking to me when I was in the middle of something. It felt like it waited until I was done."*

> **P3** (ProMemAssist): *"[It intervened when] you're almost done and you don't have as much on your mind — definitely yeah, [mental capacity] definitely matters."*

> **P6** (ProMemAssist): *"There were moments where I wanted help and moments where I didn't want anything. It kind of depends where you are in the task."*

### On Learning and Personalization

> **P4** (ProMemAssist): *"It should learn from me over time. Like, I always forget my keys, tell me that automatically."*

### On Feedback Loops

> **P7** (ProMemAssist): *"There's a lack of feedback to this... the feedback loop is kind of not there. It is helping me but it's like we are walking parallelly."*

### On Trust Fragility

> **P5** (ProMemAssist): Researchers noted that this participant indicated *a single unhelpful suggestion could break trust* with the entire system.

---

## 2. Empirical Numbers

### User Studies with Real Participants

| Study | Participants | Key Finding |
|-------|-------------|-------------|
| **Choose Your Agent** (2026) | N=243 (81 groups × 3), 6,561 trading decisions | 44% preferred Advisor, only 19.3% preferred Delegate — but Delegate yielded highest economic surplus (β=0.084, p=.034) |
| **ProMemAssist** (UIST 2025) | N=12, within-subject, 4 tabletop tasks | 24.6% positive engagement rate vs baseline's 9.34% — **2.6× higher engagement** with fewer messages |
| **ContextAgent/ProAgent** (2025) | N=20 (12M/8F, avg age 24.3), 9 scenarios | 33.4% higher proactive prediction accuracy, 38.9% user satisfaction improvement |
| **Egocentric Co-Pilot** (WWW 2026) | N=4 raters, 9 systems compared | 4.70/5.0 mean Likert rating, beating all commercial smart glasses devices |
| **ProPerSim** (ICLR 2026) | 32 simulated personas + 353 MTurk evaluators | Satisfaction scores rose from 2.2/4 to 3.3/4 over 14 days of adaptation |
| **ProAgentBench** (2026) | Real users, 500+ hours continuous sessions | 28,000+ events captured; human interaction burstiness B=0.787 |

### Hard Performance Numbers

- **ProMemAssist** delivered 130 messages vs baseline's 332 — yet achieved **2.6× the positive engagement rate** and statistically significantly lower frustration (NASA-TLX frustration: 2.32 vs 3.14, p<.05)
- **BAO** (CMU/Salesforce/MIT) achieved 43% reduction in user burden (User Involvement Rate: 0.2148) compared to baseline RL (0.3758). Without behavior regularization, UR *shoots to 0.9064* — agents pester users constantly.
- **PROPER** (U. Washington): up to 84% quality gains in single-turn proactive assistance; small LLAMA-8B model beats GPT-4 in Medical (70.26% win rate) and PWAB (76.39% win rate) when augmented with proactivity
- **ProAgentBench**: Best model (Deepseek-V3.2) achieves only **64.4% accuracy** on timing prediction; semantic similarity for generated assistance content maxes out at 0.305 — even frontier LLMs struggle to predict what users actually need
- **Proactive Agent** (2024): Fine-tuned models achieve only **66.47% F1** in proactive assistance — meaning even the best anticipation models fail ~1/3 of the time
- **ProPerSim**: Recommendation frequency naturally dropped from **24/hour to ~6/hour** as the system learned — less is more

### The Preference-Performance Gap (Choose Your Agent)

This is perhaps the most striking finding across all papers: **users systematically prefer the mode that gives them worse outcomes.**

- 44% preferred Advisor (see AI suggestion, decide yourself)
- 15.2% preferred Coach (AI shapes your thinking)  
- 19.3% preferred Delegate (AI decides for you)
- **21.4% preferred NO AI at all**
- Yet Delegate produced the best economic outcomes (β=0.084, p=.034)
- In Advisor mode, users only followed AI's recommendation 70.6% of the time
- In Coach mode, users retained their initial decision **69.5% of the time** even when AI recommended differently
- Users who preferred any AI mode reported **20% higher mental effort** than autonomy-seekers (p<.01) — they willingly accepted more cognitive load for perceived control

---

## 3. What Works

### Less Is More: Selective Intervention Beats Flooding

ProMemAssist proved this definitively: by modeling the user's working memory load in real-time and deferring messages when cognitive capacity was low, it delivered **60% fewer messages** while achieving **2.6× the positive engagement rate**. The key insight: *"The cost of a false positive (unnecessary interruption) may outweigh the cost of a false negative (missed opportunity to assist)."*

### Narrow Scope + High Reliability > Broad Ambitions + Frequent Failures

From the knowledge base: **Lindy.ai** reached 400K+ users by focusing on narrow, well-defined automations rather than trying to be a general-purpose life agent. The pattern holds across all successful deployments.

### Progressive Personalization Through Feedback

ProPerSim demonstrated that assistants improve dramatically when they learn from explicit user feedback over time. Scores rose from 2.2/4 to 3.3/4 over 14 simulated days. Crucially, **explicit feedback** (reward signals) massively outperformed implicit cues — providing action-recommendation history alone, without associated rewards, offered "limited benefit."

### Behavior Regularization Prevents Pestering

BAO's behavior regularization is critical: Information-Seeking Regularization penalizes consecutive user interactions without information gain; Over-Thinking Regularization prevents premature token budget exhaustion. Without these guardrails, agents become unbearable — UR rose from 0.21 to **0.91** without regularization.

### Modular Tool-Based Architectures

Egocentric Co-Pilot's neuro-symbolic framework achieved a 98.5% task completion rate on foundational tasks and outperformed all commercial smart glasses devices (4.70/5.0 vs next best commercial at ~3.5). The key: instead of a monolithic model, it uses an LLM orchestrator coordinating specialized tools via MCP protocol.

### Advisor Mode: The Sweet Spot for User Satisfaction

While Delegate produces better outcomes, Advisor mode provides the highest *perceived* satisfaction because users maintain control. The design implication: start with Advisor, build trust, then offer delegation options progressively.

---

## 4. What Fails

### AutoGPT/BabyAGI: The Autonomous Agent Fantasy

From the knowledge base: *"Rapid context degradation after 10-20 steps. No principled stopping criteria. Cost explosion. Compounding errors."* These early autonomous agents demonstrated that unbounded autonomy without grounding leads to failure cascades. Referenced in AgentBench and MetaGPT papers as cautionary examples.

### Synthetic Training Data Misses Real Human Behavior

ProAgentBench proved this quantitatively: real human interaction patterns show strong burstiness (B=0.787) — many short gaps and a few long gaps, following a power law. When LLMs simulate the same interactions, burstiness drops to B=0.166 (essentially random). The power-law log-likelihood ratio strongly favors human data (2951.48 vs -59.36 for synthetic). **LLMs fundamentally cannot replicate the bursty, unpredictable nature of real human behavior.**

### Chain-of-Thought Prompting Hurts Proactive Prediction

Counter-intuitively, CoT prompting *degrades* performance on proactive timing prediction for smaller models. ProAgentBench found that "CoT amplifies models' inherent behavioral tendencies" and "tends to overthink simple scenarios, imagining future problems rather than assessing what the user actually needs in the present." In Qwen3-VL-8B, CoT induced excessive conservatism (recall dropped from 94.4% to 17.1%).

### All-at-Once Proactivity Without Personalization

ProPerSim showed that proactivity without personalization leads to misaligned suggestions — like recommending a steakhouse to a vegetarian. And personalization without proactivity leaves users constantly initiating. Both capabilities must be integrated.

### Low Semantic Similarity Across All Models

Even the best frontier models achieve only 0.275-0.305 semantic similarity between generated assistance and what users actually wanted (ProAgentBench). This means AI systems fundamentally struggle to predict *what* help users need, even when they can somewhat predict *when* they need it.

### The 80% Problem

From the knowledge base: agents that work well 80% of the time create worse outcomes than reactive-only systems because the 20% failure cases destroy trust and create cleanup work that exceeds the benefit of the 80% successes.

---

## 5. Real Product Stories

### Rabbit R1: The Egocentric Co-Pilot Comparison

The Egocentric Co-Pilot paper (WWW 2026) included Rabbit R1 in a direct comparison across 9 systems. When asked "What's the weather like today?", Rabbit R1 responded: *"Checking weather forecast for today. The weather in Brunswick County, NC [wrong location] today will be mostly cloudy, with 34 degrees Celsius, 10% precipitation."* — it failed to determine the user's actual location.

When asked "Book me a restaurant nearby," Rabbit R1 went into a confused loop: *"Looking for places? Accepting cookie preferences before searching for La Cucina, D.C... Searching for La Cucina, D.M.R... Completing a CAPTCHA verification... I recommend making a reservation at a local restaurant known for its excellent cuisine..."* — it couldn't complete the task.

### Smart Glasses Landscape (from Egocentric Co-Pilot)

Multiple commercial smart glasses were tested in the same study (5-point Likert scale):
- **Human baseline**: 4.92
- **Egocentric Co-Pilot** (research prototype): 4.70
- **Ray-Ban Meta**: Could determine correct location for weather but couldn't book restaurants
- **RayNeo X3 Pro / V3 / X2**: Two models responded "Sorry, I can't find weather info"; one gave wrong location (Beijing instead of actual location)
- **Even G1**: Gave local weather correctly but couldn't book restaurants
- **Rabbit R1**: Wrong location, failed restaurant booking
- **Apple Vision Pro**: Included in comparison but interaction pattern differs from conversational AI

### Lindy.ai: The Success Story

From the knowledge base: 400K+ users, built on the principle that *"narrow scope + high reliability > broad ambitions + frequent failures."* This is the clearest commercial validation that focused, reliable AI assistance works.

### Replit Agent: The Reliability Wall

Michele Catasta (Replit): *"It's easy to build the prototype of a coding agent, but deceptively hard to improve its reliability."* This captures the universal challenge — the gap between demo and production is enormous.

### Platform-Centric vs User-Centric (Next Paradigm paper)

The Next Paradigm paper (USTC + Huawei, 2026) argued that current platforms like recommendation systems are fundamentally misaligned: *"A platform-centric model cannot simultaneously function as a user's trusted agent and a system optimized for platform profit. A more capable model is not a more benevolent model. It is a stronger amplifier of its objective."* They proposed user-centric agents running on-device with minimal cloud dependency.

### RayNeo X2 Pro Battery Reality

From Egocentric Co-Pilot deployment: *"In continuous streaming mode without external power, the device battery sustains operation for approximately 20 minutes."* — Hardware remains a fundamental bottleneck for always-on assistants.

---

## 6. The Trust Problem

### The Fundamental Paradox: Users Want Control Over Better Outcomes

Choose Your Agent's N=243 study revealed the core tension: **users systematically prefer the interaction mode that gives them worse outcomes.** 44% chose Advisor (see suggestion, decide yourself) over Delegate (19.3%), even though Delegate produced statistically significantly better results. This isn't irrational — users are trading economic efficiency for perceived autonomy.

Higher pre-game trust in AI predicted greater AI usage (r~.25, p<.01), but this trust was fragile. **Algorithm aversion** — the tendency to abandon AI assistance after observing even small errors — was documented as a significant factor reducing adoption rates.

### A Single Bad Suggestion Breaks Everything

ProMemAssist P5 noted that *a single unhelpful suggestion could break trust* with the entire system. This asymmetry — where one failure outweighs many successes — means proactive agents must be calibrated toward high precision over high recall.

### Trust Influenced More by Quality Than Timing

ProMemAssist found that *"trust was influenced more by the quality of the assistance than the timing of delivery."* Getting the content right matters more than getting the moment right — though bad timing still damages the relationship.

### The 21.4% Who Want No AI At All

In Choose Your Agent, over one-fifth of participants (21.4%) preferred no AI assistance whatsoever. These users represent a fundamental challenge: not everyone wants an AI life agent, and designing systems assumes they do.

### Poorly Calibrated Proactivity Undermines Trust, Agency, and Interaction Quality

PROPER defined this clearly: *"Poorly calibrated proactivity can undermine user trust, agency, and interaction quality when assistance is mistimed or misaligned."* The risk isn't just annoyance — it's actively harmful to the user relationship.

### The Knowledge Base Trust Hierarchy

The knowledge base synthesized a trust escalation ladder that reflects empirical findings:
1. **Never autonomous** — user does everything
2. **Ask & act** — agent proposes, user approves every action
3. **Notify & act** — agent acts and tells user afterward
4. **Full autonomy** — agent acts silently

Users should start at level 2 and only advance with demonstrated reliability.

---

## 7. Surprising Findings

### 1. Delegation Benefits Non-Users More Than Users

The most unexpected finding in Choose Your Agent: **non-users in Delegate groups had 21.6% higher surplus than human baseline groups.** Approximately 48.8% of the Delegate's total benefit came from indirect spillover effects (market-making), not from direct user gains. AI agents can improve outcomes for people who don't even use them.

### 2. Introverts Benefit More from Personalized AI

ProPerSim (32 personas, Big Five traits): low-extraversion personas showed the *largest* improvement from personalized assistance, likely because the home-based interaction environment favored solitary activities matching their preferences. Also surprising: **low-openness personas benefited more than high-openness ones** — personalization reinforced existing preferences rather than encouraging novelty.

### 3. The System Taught Itself to Shut Up

ProPerSim's ProPerAssistant started at 24 recommendations per hour but naturally converged to ~6 per hour through preference learning. The system discovered on its own that less frequent, more targeted suggestions achieve higher satisfaction scores. This "learning to be quiet" is an emergent behavior.

### 4. Real Humans Are Bursty; LLMs Are Not

ProAgentBench quantified a fundamental gap: human interaction timing follows a power law with burstiness B=0.787 (many rapid interactions, then long pauses). LLM-simulated interactions show B=0.166 — essentially uniform. The log-likelihood ratio overwhelmingly favors the power-law model (2951.48, p=7.83×10⁻¹⁰⁰). Any training data generated by LLMs will miss this pattern entirely.

### 5. Chain-of-Thought Makes Agents Worse at Knowing When to Help

CoT prompting, universally helpful for reasoning tasks, actually *hurts* proactive timing prediction. ProAgentBench found CoT "tends to overthink simple scenarios, imagining future problems rather than assessing what the user actually needs in the present." For Qwen3-VL-8B, CoT reduced recall from 94.4% to 17.1%.

### 6. The False Positive Asymmetry

ProMemAssist established that *"the cost of a false positive (unnecessary interruption) may outweigh the cost of a false negative (missed opportunity to assist)."* This is the opposite of most ML system design, which typically prioritizes recall. For life agents, **missing an opportunity to help is less damaging than interrupting unnecessarily.**

### 7. BAO Without Guardrails Creates Monsters

Without behavior regularization, BAO agents' User Involvement Rate shoots from 0.21 to **0.91** — meaning agents demand user attention for 91% of their actions. The paper showed that removing regularization creates agents that "rely more heavily on user verification and feedback on final answers," essentially becoming needy and counterproductive.

### 8. The When-How Gap

ProAgentBench found that knowing *when* to help (64.4% accuracy for best model) is fundamentally easier than knowing *how* to help (37.1% intention accuracy, 0.305 max semantic similarity). This suggests a staged approach: first solve timing, then content — rather than trying to solve both simultaneously.

### 9. 20-Minute Battery Life Reality

Always-on smart glasses assistance hits a hard physical wall: the Egocentric Co-Pilot deployed on RayNeo X2 Pro sustained only ~20 minutes of continuous operation. The vision of "always-on contextual AI" is bottlenecked by power consumption, not by AI capability.

---

## Sources

Papers read in full or substantial sections:

1. **Choose Your Agent: Advisors, Coaches, and Delegates** (2026) — N=243 behavioral experiment
2. **BAO: Behavioral Agentic Optimization** (2026) — CMU/Salesforce/MIT, proactive agent RL
3. **ProMemAssist** (UIST 2025) — Meta Reality Labs, working memory modeling for wearables
4. **ContextAgent / ProAgent Sensory** (2025) — AR glasses proactive agent, N=20 user study
5. **PROPER** (2026) — U. Washington, proactivity benchmark
6. **Egocentric Co-Pilot** (WWW 2026) — Smart glasses agent, comparison with Rabbit R1 & 8 other devices
7. **The Next Paradigm Is User-Centric Agent** (2026) — USTC + Huawei, platform vs user-centric vision
8. **Toward Personalized LLM-Powered Agents** (2026) — Comprehensive survey, 863 lines
9. **ProPerSim** (ICLR 2026) — 32 personas, Big Five traits, proactive + personalized
10. **ProAgentBench** (2026) — 28,000+ events, 500+ hours real user data
11. **Long-Running Life Augmentation Agents** — Knowledge base synthesis document (562 lines)
