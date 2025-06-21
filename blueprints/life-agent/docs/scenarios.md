# Life Agent — Scenarios

> Concrete use cases the Life Agent should handle, organized by category and implementation phase.
>
> **Research validation key**: Scenarios are annotated with empirical evidence from user studies where applicable. See [user-sentiment-research.md](user-sentiment-research.md) for full findings.

---

## Daily Automation

> **Research validation**: Morning briefings are the ideal first proactive action — low risk, high perceived value. ProPerSim showed satisfaction scores rise from 2.2/4 to 3.3/4 when agents learn timing preferences. This category should ship first to build trust before higher-stakes scenarios (§12.5 of design doc).

### S1: Morning Briefing
**Phase**: 2 · **Workers**: SummaryAgent · **Trust**: FullAuto · **Trigger**: Cron (7am weekdays)

Every weekday morning, a Discord DM arrives:

> **Good morning.** Here's your day:
>
> **Calendar** — 3 meetings today. The 2pm with Sarah was rescheduled to 3pm. You have a 90-minute gap from 10:30–12:00.
>
> **Email** — 2 messages need replies (landlord re: lease renewal, Alex re: project timeline). 4 newsletters archived.
>
> **Weather** — Rain expected at 5pm. Leave early or bring an umbrella.
>
> **Tasks** — "Research NAS solutions" completed overnight (report attached). Reminder: car registration due in 12 days.

### S2: End-of-Week Review
**Phase**: 2 · **Workers**: SummaryAgent · **Trust**: FullAuto · **Trigger**: Cron (Friday 6pm)

> **Week in review:**
>
> - 12 tasks completed, 3 still pending
> - LLM cost this week: $4.20
> - Research on "Rust async patterns" is 80% done — want me to finish over the weekend?
> - You missed your Wednesday deep-work block (meeting conflict). Want me to reschedule?

---

## Background Research

### S3: On-Demand Deep Research
**Phase**: 1 · **Workers**: ResearchAgent · **Trust**: FullAuto · **Trigger**: User message

User says in Discord: *"Research the best NAS solutions for home use under $500"*

Agent acknowledges immediately, delegates to ResearchAgent, runs for ~5 minutes in background. When done, posts the full structured report to Discord (or uploads as a file if too long).

### S4: Ongoing Topic Monitoring
**Phase**: 2 · **Workers**: MonitorAgent · **Trust**: FullAuto · **Trigger**: Cron (weekly)

User: *"Keep an eye on news about the .NET 11 release"*

MonitorAgent checks weekly for new articles, blog posts, and release announcements. Sends a digest only when there's something new:

> **📰 .NET 11 Update** (3 new items since last week):
> - Microsoft announced .NET 11 Preview 3 with native AOT improvements
> - Stephen Toub's blog: "Performance improvements in .NET 11"
> - Breaking change: `System.Text.Json` source generator API updated

### S5: Travel Research (Multi-Step)
**Phase**: 1 · **Workers**: ResearchAgent (multiple invocations) · **Trust**: FullAuto · **Trigger**: User message

User: *"I'm thinking about visiting Japan in October — what should I know?"*

Orchestrator decomposes into sub-tasks:
1. Visa requirements for US citizens → ResearchAgent
2. October weather and best regions → ResearchAgent
3. Flight price range (West Coast departure) → ResearchAgent
4. Cultural tips and etiquette → ResearchAgent
5. Must-see recommendations → ResearchAgent

Results synthesized into a single comprehensive report, delivered ~10 minutes later.

---

## Price & Event Monitoring

### S6: Price Drop Tracking
**Phase**: 2 · **Workers**: MonitorAgent · **Trust**: FullAuto · **Trigger**: Cron (every 6 hours)

User: *"Track the price of RTX 5080 on Amazon and Newegg"*

MonitorAgent polls every 6 hours. Only sends Discord DM when price drops >5%:

> **💰 Price Alert: RTX 5080**
> - Amazon: $649 → **$599** (-7.7%)
> - Newegg: $659 (unchanged)
> - Lowest tracked: $599 (now). Want me to keep watching or stop tracking?

### S7: Release Monitoring
**Phase**: 2 · **Workers**: MonitorAgent · **Trust**: FullAuto · **Trigger**: Cron (daily)

User: *"Remind me when Kubernetes 1.31 is released"*

Monitors the Kubernetes releases page and GitHub tags. Alerts on release day:

> **🚀 Kubernetes 1.31 Released!**
> - Released today (March 15, 2026)
> - Codename: "Elli"
> - Key changes: [summary from changelog]
> - Want me to research the upgrade path for your AKS cluster?

### S8: Ticket/Event Availability
**Phase**: 2 · **Workers**: MonitorAgent · **Trust**: FullAuto · **Trigger**: Cron (every 6 hours)

User: *"Watch for concert tickets for Band X in Portland"*

Monitors Ticketmaster, StubHub, AXS. Alerts when tickets appear or prices drop significantly.

---

## Reminders & Follow-ups

> **Research validation**: ProMemAssist (N=12) proved that timing matters — intervene when cognitive load is low, not when the user is in flow state. User P5: *"If I'm currently working on some task and then I have cognitive load, you shouldn't tell me too much, unless it's important."* The cost-of-interruption model (§10.3) applies directly here.

### S9: Deadline Reminders
**Phase**: 1 · **Workers**: ReminderAgent · **Trust**: NotifyAndAct · **Trigger**: Scheduled

User: *"Remind me to renew my car registration before April 15"*

Sends reminders at 2 weeks, 1 week, and 2 days before deadline:

> **⏰ Reminder: Car Registration**
> Due in 2 days (April 15). Oregon DMV online renewal: [link]
> React ✅ when done, or I'll remind you tomorrow.

### S10: Email Follow-up Tracking
**Phase**: 3 (requires Gmail) · **Workers**: EmailTriageAgent + ReminderAgent · **Trust**: NotifyAndAct · **Trigger**: Event (5 days no reply)

User: *"I emailed the landlord about the leak 3 days ago"*

Agent tracks the thread. After 5 days with no reply:

> **📧 No response from landlord re: leak**
> Sent 5 days ago, no reply. Want me to draft a follow-up?
> [Draft Follow-up] [Remind Me Later] [Dismiss]

---

## Calendar Intelligence (Phase 3)

### S11: Week Overview
**Phase**: 3 · **Workers**: ScheduleAgent · **Trust**: FullAuto · **Trigger**: User message

User: *"What does my week look like?"*

Reads Google Calendar, analyzes:

> **Next week:**
> - Monday: 4 meetings (heavy day). No focus time.
> - Tuesday: 2 meetings, 3hr gap 1-4pm.
> - Wednesday: Clear — suggest blocking for deep work?
> - Thursday: All-hands 10am, 1:1 with manager 3pm.
> - Friday: Light — 1 meeting.
>
> Suggestions: Block Monday 8-9am for prep. Move Tuesday standup to async?

### S12: Focus Time Scheduling
**Phase**: 3 · **Workers**: ScheduleAgent · **Trust**: AskAndAct · **Trigger**: User message

User: *"Schedule a 1-hour block for deep work every morning this week"*

Finds available slots, requests approval via Discord button:

> **📅 Proposed deep work blocks:**
> - Mon 8:00–9:00am ✅
> - Tue 8:00–9:00am ✅
> - Wed 8:00–9:00am ✅
> - Thu 9:00–10:00am (8am conflicts with standup)
> - Fri 8:00–9:00am ✅
>
> [Approve All] [Edit] [Cancel]

---

## Proactive Behaviors

> **Research validation**: This is the highest-risk category. ProAgentBench found even SOTA models achieve only 64.4% accuracy on timing prediction and 30.5% semantic similarity on content prediction. BAO showed that without behavior regularization, proactive agents demand user attention 91% of the time. Each scanner here must clear the 80% reliability bar (§14.4) or it's worse than not shipping. False-positive asymmetry applies: interrupting unnecessarily is worse than missing a chance to help.

### S13: Meeting Context Preparation
**Phase**: 3 · **Workers**: ResearchAgent + EmailTriageAgent · **Trust**: FullAuto · **Trigger**: Event (15 min before meeting)

Meeting in 15 minutes with someone you haven't met:

> **📋 Upcoming: 2pm with Alex Chen (VP Eng @ Acme)**
> - Context from your last 2 email threads: discussing API partnership + SLA terms
> - Alex's LinkedIn: VP Eng, previously at Stripe, 15 years experience
> - Last interaction: March 1 (you sent the draft proposal)
> - Suggested prep: Review the SLA doc Alex mentioned

### S14: Travel Day Intelligence
**Phase**: 2–3 · **Workers**: MonitorAgent + ScheduleAgent · **Trust**: FullAuto · **Trigger**: Event (day before flight)

You have a flight tomorrow:

> **✈️ Travel Day Tomorrow**
> - Flight: UA 1234 to SFO, departs 7:15am Terminal B
> - Weather in SF: 62°F, clear
> - Uber to airport: ~35 min at that hour (suggested departure: 5:45am)
> - Set alarm for 5:15am? [Yes] [No]
> - Your first meeting in SF is at 2pm — plenty of time after landing (10:30am)

### S15: PR/Code Monitoring
**Phase**: 2 · **Workers**: MonitorAgent · **Trust**: FullAuto · **Trigger**: Webhook or Cron

A PR you're watching has been merged:

> **🔀 PR Merged: `async-runtime` → main**
> - 47 files changed, +1,204 / -387 lines
> - Author: @teammate
> - Key changes: Replaced tokio runtime with custom scheduler
> - Want a detailed summary? [Yes] [No]

---

## Compound Multi-Agent Tasks

### S16: Event Planning
**Phase**: 3 · **Workers**: ScheduleAgent + ResearchAgent + ReminderAgent · **Trust**: Mixed · **Trigger**: User message

User: *"Plan my birthday dinner for 8 people next Saturday"*

Orchestrator decomposes:
1. Check calendar — Saturday 6pm–10pm is free ✅ (ScheduleAgent, FullAuto)
2. Research restaurants with group seating, good reviews, your cuisine preferences (ResearchAgent, FullAuto)
3. Present top 3 options → **user picks** (AskAndAct)
4. Create calendar event and share with invitees → **user approves guest list** (AskAndAct)
5. Set reminder: confirm reservation Thursday (ReminderAgent, NotifyAndAct)
6. Day-of reminder with directions, parking info, reservation confirmation (ReminderAgent, FullAuto)

### S17: Learning Goal
**Phase**: 2–3 · **Workers**: ResearchAgent + ScheduleAgent + ReminderAgent · **Trust**: Mixed · **Trigger**: User message

User: *"I want to learn Rust over the next 3 months"*

1. Research best learning resources for your experience level (ResearchAgent)
2. Create a 12-week study plan with milestones
3. Schedule weekly 2-hour study blocks on your calendar (ScheduleAgent, AskAndAct)
4. Weekly check-in reminders: "Week 3: How's the Rust study going? This week's topic: ownership & borrowing"
5. Monthly progress assessment: "You've completed 4/12 weeks. On track. Next milestone: build a CLI tool"

---

---

## Research-Backed Design Constraints

These constraints, derived from user sentiment research across 6 user studies (N=243+12+20+4+32+500hrs), should govern all scenario implementation:

| Constraint | Evidence | Impact on Scenarios |
|-----------|----------|--------------------|
| **Less is more** | ProMemAssist: 60% fewer messages → 2.6× engagement | Batch low-urgency notifications; never flood |
| **Trust is fragile** | ProMemAssist P5: single bad suggestion breaks trust | Every scenario must have high precision before shipping |
| **False positives > false negatives** | ProMemAssist, PROPER | Missing an opportunity to help < interrupting unnecessarily |
| **Users want control** | Choose Your Agent: 44% prefer Advisor | Default all scenarios to AskAndAct or NotifyAndAct; earn FullAuto |
| **Personalization requires explicit feedback** | ProPerSim: implicit signals alone offer "limited benefit" | Every scenario output must include feedback mechanisms (👍/👎, "too much", "wrong timing") |
| **System should learn to be quieter** | ProPerSim: 24/hour → 6/hour natural convergence | Rate-limit all scanners; decrease proactivity on non-engagement |
| **80% reliability minimum** | User sentiment: 80% reliable = worse than no agent | Block scenario launch until per-worker success rate >95% |

---

*See also: [scenarios-new.md](scenarios-new.md) for 65 additional scenarios (S18–S82), including 19 wellness & human flourishing scenarios. See [human-wellness-research.md](../../knowledge-base/human-wellness-research.md) for the evidence base. See [user-sentiment-research.md](user-sentiment-research.md) for the full empirical basis.*
