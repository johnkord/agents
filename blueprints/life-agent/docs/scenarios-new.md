# Life Agent — New Scenario Research

> 25–40 new scenarios discovered through deep research across 20 papers, product analysis (Lindy.ai, Reclaim.ai, Motion, Zapier Agents), and creative synthesis. None of these overlap with the 17 existing scenarios in `scenarios.md` (S1–S17).

---

## Category 1: Financial & Shopping Intelligence

### S18: Subscription Audit
**Trigger**: Cron (monthly) + Gmail integration  
**Agent Actions**:
1. Scan email for recurring payment receipts (Stripe, PayPal, Apple, Google)
2. Build a table of active subscriptions with monthly/annual costs
3. Flag subscriptions with no usage signals in the last 30 days (e.g., no login emails, no activity notifications)
4. Calculate total monthly spend and present cancellation candidates

**Value**: People average 12+ subscriptions and forget about 2–3 of them. Saves $20–50/month passively.  
**Source/Inspiration**: Knowledge base §8.3 (Financial Health Monitoring); Lindy.ai's inbox automation; Declarative Agent Workflows (PayPal-scale personal finance patterns)  
**Confidence**: HIGH — email scanning is well-supported, subscription patterns are highly regular

---

### S19: Bill Anomaly Detection
**Trigger**: Event (new email matching bill/payment pattern)  
**Agent Actions**:
1. Parse incoming utility/service bills from email
2. Compare against historical amounts for the same provider
3. Alert if amount deviates >15% from the 3-month rolling average
4. If provider has a known dispute process, include a link or draft dispute template

**Value**: Catches billing errors, rate hikes, and fraud early. Utility overcharges alone cost US households $200+/year on average.  
**Source/Inspiration**: Knowledge base §8.3; ProAgentBench data analysis category (9.17% of real user tasks); Egocentric Co-Pilot nutrition/label reading pattern (applied to financial documents)  
**Confidence**: HIGH — email parsing + comparison logic is straightforward

---

### S20: Comparison Shopping Assistant
**Trigger**: User message  
**Agent Actions**:
1. User says "I need a new standing desk under $600"
2. Research top options across Amazon, manufacturer sites, review aggregators
3. Build a comparison matrix: price, key specs, review score, warranty, shipping time
4. Track prices of the top 3 picks for 2 weeks (hands off to MonitorAgent)
5. Alert when any drops below a user-defined threshold

**Value**: Saves 2–4 hours of comparison research plus ongoing price vigilance. Combines research + monitoring workers.  
**Source/Inspiration**: ProAgent sensory contexts (product comparison while shopping); ContextAgent tool-use patterns; existing S6 price tracking extended with research phase  
**Confidence**: HIGH — combines two proven capabilities (research + price monitoring)

---

### S21: Warranty & Return Window Tracker
**Trigger**: Event (purchase confirmation email parsed) + scheduled reminders  
**Agent Actions**:
1. Detect purchase confirmation emails and extract: item, price, retailer, order date
2. Look up return policy for retailer (30 days for Amazon, 90 for Costco, etc.)
3. Set a reminder 5 days before return window closes
4. For items with warranties, store expiration date and remind 30 days before expiry

**Value**: Eliminates missed return windows and forgotten warranties — people lose $100+/year on unreturned items.  
**Source/Inspiration**: Knowledge base §8.3; Lindy.ai proactive anticipation pattern; ProMemAssist timing-based cognitive assistance  
**Confidence**: HIGH — email parsing for order confirmations is a well-solved pattern

---

## Category 2: Social & Relationship Management

### S22: Stay-in-Touch Cadence
**Trigger**: Cron (weekly) + contact database  
**Agent Actions**:
1. Maintain a contact list with desired cadence (e.g., "Mom: weekly", "College friend Dave: monthly", "Mentor: quarterly")
2. Track last interaction via email/calendar (when supported) or user confirmation
3. Surface contacts who are overdue for a touchpoint
4. Suggest a reason to reach out (birthday coming up, shared interest in recent news, "haven't talked in 47 days")

**Value**: Maintains relationships that would otherwise decay. Research shows people lose ~50% of close contacts every 7 years without intentional effort.  
**Source/Inspiration**: Knowledge base §8.4 (Social Maintenance); Personalized LLM Agents survey (relationship modeling in user profiles); O-Mem active user profiling  
**Confidence**: HIGH — simple cadence tracking with optional email/calendar integration

---

### S23: Gift Suggestion Engine
**Trigger**: Event (birthday/holiday approaching for tracked contact) or User message  
**Agent Actions**:
1. 2 weeks before a tracked contact's birthday/holiday: alert with gift suggestions
2. Pull context from previous conversations, known interests, wish lists
3. Research 3–5 gift options within a specified budget range
4. Include purchase links and delivery time estimates
5. Set a "order by" deadline reminder to ensure on-time delivery

**Value**: Eliminates last-minute gift panic and improves gift quality through personalization.  
**Source/Inspiration**: ProPerSim personalized recommendation patterns; Personalized LLM Agents survey (preference learning); ContextAgent daily scenario patterns  
**Confidence**: MEDIUM — quality depends on accumulated context about the person's preferences

---

### S24: Important Life Event Follow-Up
**Trigger**: Event (user mentions someone's job interview, medical procedure, exam, etc.)  
**Agent Actions**:
1. When user mentions "Sarah has her surgery on Thursday" in conversation
2. Store the event with an appropriate follow-up date (e.g., Friday or Saturday)
3. Remind user to check in: "Sarah had her surgery yesterday — want to send a message?"
4. Optionally draft a brief, warm check-in message

**Value**: Shows you care during moments that matter most. People consistently rank "they remembered and asked" as the #1 sign of a good friend.  
**Source/Inspiration**: PROPER proactivity research ("unknown unknowns" — things the user wouldn't think to track); ProActive Agent daily life scenarios; BAO user engagement optimization  
**Confidence**: MEDIUM — requires natural language understanding of conversational context to detect life events

---

## Category 3: Health & Wellness

### S25: Medication & Supplement Reminder
**Trigger**: Cron (user-configured times) + context-aware timing  
**Agent Actions**:
1. User configures medications/supplements with schedule (e.g., "Vitamin D with breakfast", "Allergy med at 9pm")
2. Send reminders at configured times, but adapt based on calendar (if user has a 7am flight, remind earlier)
3. Track confirmation responses; flag if a dose is missed
4. Weekly adherence summary: "You took Vitamin D 5/7 days this week"

**Value**: Medication non-adherence affects ~50% of patients and costs $100B+/year in the US alone. Even supplement tracking improves health outcomes.  
**Source/Inspiration**: ProMemAssist wearable assistance (timing-based proactive support); Egocentric Co-Pilot health info lookup; Knowledge base §8.5 (Health & Wellness)  
**Confidence**: HIGH — simple scheduling with calendar-aware adjustments

---

### S26: Exercise Opportunity Detection
**Trigger**: Cron (daily, early morning) + calendar integration  
**Agent Actions**:
1. Scan today's calendar for gaps ≥45 minutes
2. Cross-reference with weather data and user preferences (gym vs. outdoor, time-of-day preference)
3. Suggest a specific workout window: "You have 11:00–12:30 free and it's 68°F and sunny — good window for a run?"
4. If user confirms, optionally block the calendar slot
5. End-of-week: "You exercised 3/5 target days this week"

**Value**: The #1 barrier to exercise is "I don't have time." Calendar-aware suggestions remove this friction.  
**Source/Inspiration**: Reclaim.ai habit protection feature; ContextAgent outdoor activity planning scenario; Knowledge base §8.5; ProPerSim personality-adapted recommendations  
**Confidence**: HIGH — calendar gap detection + weather API is straightforward

---

### S27: Hydration & Break Nudges (Deep Work Mode)
**Trigger**: Event (user enters a focus time block on calendar)  
**Agent Actions**:
1. Detect when user starts a deep work / focus block
2. After 90 minutes, send a gentle nudge: "You've been focused for 90 min — good time for a 5-min break and water"
3. Adapt frequency based on user feedback (some people want every 60 min, others every 2 hours)
4. Respect "DND override" preference — some users want zero interruption during focus blocks

**Value**: Pomodoro/break research shows cognitive performance drops ~20% after 90min without breaks. Hydration affects focus.  
**Source/Inspiration**: BAO proactivity calibration (Pareto frontier between helpfulness and interruption); ProPerSim personality-based frequency adaptation (reduced from 24/hr to 6/hr); Knowledge base §8.5  
**Confidence**: MEDIUM — value depends heavily on user acceptance; must not become annoying

---

## Category 4: Knowledge Work Enhancement

### S28: Meeting Action Item Extraction
**Trigger**: Event (calendar meeting ends) + meeting notes integration  
**Agent Actions**:
1. After a meeting ends, check if meeting notes/transcript are available (from Granola, Fireflies, or user-pasted notes)
2. Extract action items assigned to the user
3. Present them in Discord with deadlines: "From your 2pm meeting: (1) Send API spec to Alex by Friday (2) Review security doc before next standup"
4. Offer to create reminders for each item

**Value**: 73% of action items from meetings are forgotten within 48 hours. Automated extraction closes the loop.  
**Source/Inspiration**: ProAgentBench real-world interaction categories; Zapier + Fireflies/Granola automation patterns; Lindy.ai meeting prep/follow-up feature  
**Confidence**: MEDIUM — requires meeting notes input; extraction quality varies with note quality

---

### S29: Context Pre-Loading Before Task Switch
**Trigger**: Event (15 minutes before a calendar event with a different topic/project)  
**Agent Actions**:
1. Detect when user is switching contexts (e.g., from "Project Alpha standup" to "Client Beta review")
2. Surface relevant context for the upcoming task: last email threads, open PRs, pending action items, recent documents
3. Deliver a brief context packet: "Switching to Client Beta in 15min. Last touchpoint: you sent the revised proposal March 5. Open items: awaiting their feedback on pricing tier."

**Value**: Context switching costs 23 minutes of recovery time (UC Irvine research). Pre-loading context cuts this significantly.  
**Source/Inspiration**: ProActive Agent proactive context delivery; ContextAgent sensory context injection; S13 (meeting context prep) generalized to all task switches; Next Paradigm user-centric cross-platform intent  
**Confidence**: MEDIUM — relies on calendar metadata being descriptive enough to identify project/topic

---

### S30: Stale Task / Blocked Work Detection
**Trigger**: Cron (daily) + task/project tracking  
**Agent Actions**:
1. Scan tracked tasks/projects for items with no updates in >7 days
2. Check if the stall correlates with a dependency (e.g., waiting for someone's email reply, waiting for a PR review)
3. Surface blockers: "Your 'Migrate to Postgres' task has been stalled for 9 days. Possible blocker: you're waiting for DevOps approval — last email sent March 2. Draft a follow-up?"
4. Offer to send a nudge email or escalate

**Value**: Projects stall silently. Proactive detection prevents weeks of slippage. Project managers report ~30% of tasks stall due to unnoticed dependencies.  
**Source/Inspiration**: Asana/ClickUp AI blocker detection features; ProAgentBench code programming category; PROPER "unknown unknowns" detection; Knowledge base §8.7 (Knowledge Worker Copilot)  
**Confidence**: MEDIUM — highly valuable but requires task tracking integration or manual task management

---

### S31: Document Expiration & Renewal Tracker
**Trigger**: User-configured dates + Cron (weekly check)  
**Agent Actions**:
1. Maintain a registry of documents with expiration dates: passport, driver's license, professional certifications, insurance policies, domain registrations, SSL certs
2. Send tiered reminders: 90 days, 30 days, 14 days, 3 days before expiration
3. Include renewal instructions and links for each document type
4. After renewal, update the expiration date for the next cycle

**Value**: Expired passports cancel trips. Expired licenses mean fines. Expired SSL certs break production sites. Systematic tracking prevents all of these.  
**Source/Inspiration**: Knowledge base §8.3; Lindy.ai anticipation feature; ReminderAgent extension from simple one-off reminders to recurring document lifecycle  
**Confidence**: HIGH — simple date tracking with configurable reminder cadence

---

## Category 5: Home & Life Maintenance

### S32: Home Maintenance Schedule
**Trigger**: Cron (monthly) + seasonal awareness  
**Agent Actions**:
1. Maintain a home maintenance schedule: HVAC filter (every 3 months), gutter cleaning (spring/fall), smoke detector batteries (every 6 months), dryer vent (annual), etc.
2. Surface upcoming maintenance tasks at the start of each month
3. Include cost estimates and recommended local services if applicable
4. Track completion: "You marked the HVAC filter as done on March 5. Next due: June 5."

**Value**: Preventive home maintenance saves $3,000–5,000/year vs. reactive repairs. Most homeowners miss 40%+ of recommended maintenance.  
**Source/Inspiration**: Knowledge base §8.8 (Home Automation Intelligence Layer); ProActive Agent daily life scenarios; IFTTT/smart home automation patterns  
**Confidence**: HIGH — calendar-based scheduling with a pre-built maintenance template

---

### S33: Meal Planning from Calendar Gaps
**Trigger**: Cron (Sunday evening) + calendar + user preferences  
**Agent Actions**:
1. Analyze next week's calendar density to identify cooking-friendly evenings vs. "need something fast" nights
2. Suggest a weekly meal plan: full recipes for light days, quick 15-min meals or takeout suggestions for packed days
3. Generate a consolidated grocery list from the meal plan
4. Factor in dietary preferences, budget targets, and what's already in pantry (user maintains a rough inventory)

**Value**: "What's for dinner?" is the #1 daily decision that causes stress. Planning eliminates decision fatigue and reduces food waste by ~30%.  
**Source/Inspiration**: Knowledge base §8.8; ContextAgent restaurant ordering scenario; ProAgent dietary health advice pattern; Zapier automated workflows  
**Confidence**: MEDIUM — meal planning is high-value but personalization requires accumulated preference data

---

### S34: Package Delivery Consolidation
**Trigger**: Event (shipping confirmation emails) + Cron (daily)  
**Agent Actions**:
1. Parse shipping confirmation emails for tracking numbers and carriers
2. Monitor delivery status via carrier APIs or web scraping
3. Send a daily summary: "3 packages arriving today: Amazon (books, by 5pm), REI (jacket, by 8pm), USPS (unknown, by 3pm)"
4. Alert on delays or delivery exceptions
5. Remind to check porch / pickup packages when expected delivery time passes

**Value**: Consolidates fragmentary delivery info scattered across emails. Prevents porch piracy through timely pickup reminders.  
**Source/Inspiration**: Lindy.ai inbox automation; ProAgentBench information lookup category (55% of real tasks); Microsoft Outlook AI extraction patterns  
**Confidence**: HIGH — email parsing for shipping confirmations is well-established

---

## Category 6: Travel Optimization (Beyond S5/S14)

### S35: Flight Price Watch for Planned Trips
**Trigger**: User message + Cron (daily during watch period)  
**Agent Actions**:
1. User says: "I'm thinking about flying to Tokyo in October — watch prices"
2. Set up daily monitoring of flight prices from user's home airport to destination
3. Track price trends and identify the optimal booking window using historical patterns
4. Alert when prices drop below the trend line or a specified budget: "PDX→NRT round-trip dropped to $680 (avg for this route in Oct is $850). Book now?"
5. Include a link to the booking page

**Value**: Flight prices vary $200–500+ depending on booking timing. Systematic monitoring captures savings that manual checking misses.  
**Source/Inspiration**: Knowledge base §8.6 (Travel Optimization); S6 price tracking applied to flights; Google Flights price tracking model  
**Confidence**: MEDIUM — flight price APIs are less open than product APIs; may require scraping or Google Flights integration

---

### S36: Loyalty Point Expiration Alert
**Trigger**: Cron (monthly) + user-configured accounts  
**Agent Actions**:
1. User registers loyalty program accounts: airline miles, hotel points, credit card rewards, store loyalty cards
2. Track point balances and expiration policies (many programs expire points after 18–24 months of inactivity)
3. Alert 60 days before expiration: "Your 42,000 United miles expire June 15 (no activity in 16 months). Options: (1) Book a $25 economy upgrade to reset the clock (2) Transfer 1,000 miles to a partner program (3) Make a shopping portal purchase"
4. Suggest the cheapest way to prevent expiration

**Value**: Americans forfeit ~$48 billion in loyalty points annually. Even one alert preventing a major expiration is worth hundreds of dollars.  
**Source/Inspiration**: Knowledge base §8.6; Next Paradigm cross-platform intent fulfillment (subscription and loyalty management); financial monitoring patterns  
**Confidence**: MEDIUM — loyalty program APIs vary widely; may require manual balance entry for some programs

---

### S37: Travel Disruption Response
**Trigger**: Event (flight delay/cancellation notification email or push notification)  
**Agent Actions**:
1. Detect flight delay/cancellation from airline email or monitored flight status
2. Immediately search for alternative flights on the same and other airlines
3. Check hotel rebooking if connection is missed
4. Present options: "Your UA1234 is canceled. Options: (1) UA1240 departing 2hrs later, same route — seats available (2) AA789 via DFW, arrives 1hr later — $50 more (3) Stay overnight + first flight tomorrow"
5. If trust level allows, draft a rebooking request or compensation claim email

**Value**: Flight disruptions cause panic. Having alternatives researched instantly (within minutes of the notification) reduces stress and captures better rebooking options before they fill up.  
**Source/Inspiration**: Next Paradigm user-centric agent (rebooking and refund negotiation use case); Knowledge base §8.6; S14 travel day intelligence extended to disruption handling  
**Confidence**: LOW — requires real-time flight status + cross-airline search; complex but very high value

---

## Category 7: Communication & Email Intelligence

### S38: Email Digest by Priority
**Trigger**: Cron (configurable: 3x/day or on-demand) + Gmail  
**Agent Actions**:
1. Classify incoming emails into tiers: 🔴 Needs Reply (from known contacts, contains questions/requests), 🟡 FYI (receipts, confirmations, updates), 🟢 Low Priority (newsletters, promotions)
2. Auto-archive 🟢 tier (if user enables FullAuto for this)
3. Send a digest: "Since last check: 2 🔴 (landlord, your manager), 5 🟡 (package shipped x2, calendar update, bank statement, GitHub notification), 12 🟢 archived"
4. Include one-line summaries for 🔴 items with suggested reply length

**Value**: Reduces email anxiety. The average knowledge worker checks email 77 times/day. Batched digests with triage can cut this to 3–5 checks.  
**Source/Inspiration**: Shortwave AI email features; Lindy.ai inbox management; Gemini for Gmail triage; ProAgentBench information lookup dominant category  
**Confidence**: HIGH — email classification is a well-solved ML problem; Gmail API provides the necessary access

---

### S39: Smart Reply Drafting for Routine Emails  
**Trigger**: User message ("Draft a reply to the landlord's email") or auto-suggested for 🔴 emails  
**Agent Actions**:
1. Read the email thread for context
2. Check agent memory for relevant context (e.g., previous interactions with this person, related tasks)
3. Draft a reply matching the user's typical tone and communication style
4. Present in Discord for review: "Proposed reply to landlord re: lease renewal: [draft]. Tone: professional but firm. [Send] [Edit] [Discard]"
5. Learn from edits to improve future drafts

**Value**: Email drafting is the most time-consuming communication task. Even a 70%-accurate first draft saves 3–5 minutes per reply.  
**Source/Inspiration**: Lindy.ai "drafts replies in your voice" feature; Personalized LLM Agents survey (style adaptation through user feedback); O-Mem self-evolving memory for personalization; BAO behavior regularization  
**Confidence**: HIGH — email drafting is a core LLM capability; learning user tone requires ~10 examples

---

### S40: Unsubscribe Sweep
**Trigger**: Cron (quarterly) or User message  
**Agent Actions**:
1. Analyze the last 90 days of email for recurring newsletters/marketing
2. Calculate open rates per sender: "You received 12 emails from TechCrunch Daily; you opened 1 (8%)"
3. Present a list of low-engagement subscriptions with one-click unsubscribe links
4. If FullAuto: auto-unsubscribe from senders with 0% open rate after confirmation

**Value**: The average inbox receives 40+ marketing emails/week. Periodic cleanup reduces noise significantly.  
**Source/Inspiration**: Lindy.ai inbox automation; existing S1 morning briefing newsletter archival extended to proactive cleanup; Shortwave email management  
**Confidence**: HIGH — email analysis + unsubscribe link extraction is technically straightforward

---

## Category 8: Decision Support & Proactive Intelligence

### S41: Decision Matrix Builder
**Trigger**: User message  
**Agent Actions**:
1. User says: "Help me decide between buying vs. renting in Portland"
2. Agent asks 2–3 clarifying questions to scope the decision (budget, timeline, priorities)
3. Research both sides with cited sources
4. Build a weighted pros/cons matrix with the user's stated priorities
5. Present a structured analysis: "Based on your 5-year timeline and $2,500/month budget, buying slightly favors at 6.2/10 vs renting at 5.8/10. Key swing factor: if mortgage rates drop below 5.5%, buying advantage increases significantly."

**Value**: Complex life decisions benefit enormously from structured analysis. Replaces the "asking 5 friends and getting 5 different opinions" pattern.  
**Source/Inspiration**: IntentRL clarification before deep research; PROPER proactive assistance for complex decisions; Choose Your Agent "delegate" mode for decision support; S3 deep research extended with decision framework  
**Confidence**: HIGH — combines existing research capability with a structured decision framework layer

---

### S42: Proactive "Did You Know" Alerts
**Trigger**: Event (contextual, triggered by detected patterns in user behavior/data)  
**Agent Actions**:
1. Monitor user's data streams for opportunities the user likely doesn't know about
2. Examples: "Your credit card's travel insurance covers trip cancellation — relevant since you booked that Tokyo flight" / "Oregon's homebuyer tax credit application deadline is April 30 — eligible since you're a first-time buyer" / "Your gym membership includes free guest passes — relevant if you want to bring a friend"
3. Surface only HIGH-confidence, high-value insights (max 2/week to avoid fatigue)
4. Include sources and action steps

**Value**: This is the "unknown unknowns" problem — the agent knows things the user doesn't know to ask about. Each insight can save hundreds to thousands of dollars.  
**Source/Inspiration**: PROPER proactivity research (84% quality gains from surfacing unknown unknowns); ProActive Agent reactive-to-proactive transition; BAO engagement optimization (2/week cap prevents fatigue)  
**Confidence**: LOW — requires deep contextual reasoning and cross-domain knowledge; highest value but hardest to execute reliably

---

### S43: Recurring Expense Negotiation Prep
**Trigger**: Cron (annual, timed to contract renewal periods)  
**Agent Actions**:
1. Track recurring service contracts: internet, phone, insurance, streaming bundles
2. 30 days before renewal or price increase, research: competing offers, current promotions, retention department strategies
3. Prepare a negotiation brief: "Your Comcast bill increases from $79→$99 on April 1. Competitor offer: AT&T Fiber $65/mo for 12 months. Retention script: call 1-800-XFINITY, ask for retention department, cite AT&T offer, target: $69/mo for 12 months."
4. Set a reminder to make the call

**Value**: 10 minutes of negotiation saves $200–400/year per service. Most people don't bother because they forget or don't research alternatives.  
**Source/Inspiration**: Knowledge base §8.3 (Financial Health); Next Paradigm (subscription management, refund negotiation use cases); Lindy.ai proactive anticipation  
**Confidence**: MEDIUM — research phase is straightforward; negotiation script quality depends on provider-specific knowledge

---

## Category 9: Cognitive Load Management

### S44: Smart Notification Batching
**Trigger**: Continuous (meta-layer on top of all agent notifications)  
**Agent Actions**:
1. Instead of sending each notification immediately, batch non-urgent items
2. Categorize by urgency: Immediate (flight canceled, meeting in 5min) vs. Batchable (weekly review, price drop, newsletter digest)
3. Deliver batched notifications at user-configured "check-in" times (e.g., 9am, 1pm, 6pm)
4. Learn which notifications user acts on immediately vs. ignores, and re-classify accordingly

**Value**: Notification overload is the #1 complaint about AI assistants (BAO research). Smart batching preserves attention while ensuring nothing is missed.  
**Source/Inspiration**: BAO Pareto frontier research (task performance vs. user engagement trade-off); ProPerSim frequency adaptation (24/hr → 6/hr); ProMemAssist cognitive-load-aware timing  
**Confidence**: HIGH — meta-layer on existing notification system; biggest risk is the Life Agent being dismissed entirely if it notifies too much

---

### S45: End-of-Day Brain Dump Capture
**Trigger**: Cron (user-configured, e.g., 9pm) or User message  
**Agent Actions**:
1. Prompt user: "Anything on your mind before bed? Tasks, ideas, worries — just dump it here."
2. Accept freeform text input
3. Parse and categorize: extract tasks (→ create reminders), extract ideas (→ store in memory), extract worries (→ suggest actionable next steps or schedule for tomorrow)
4. Confirm: "Got it. Created 2 reminders: 'Call dentist' (tomorrow 10am), 'Look into refinancing' (this weekend). Stored idea about podcast topic. Anything else?"

**Value**: David Allen's GTD principle: "your mind is for having ideas, not holding them." Capturing loose thoughts before bed improves sleep quality and ensures nothing is lost.  
**Source/Inspiration**: O-Mem episodic memory capture; Anatomy of Agentic Memory (working → long-term memory consolidation); Mem.ai knowledge management pattern; Knowledge base §8.7  
**Confidence**: HIGH — simple conversational capture + NLP extraction; aligns naturally with Discord DM interaction model

---

### S46: Weekly Energy Audit
**Trigger**: Cron (Sunday evening)  
**Agent Actions**:
1. Analyze the past week: meeting load, focus time achieved vs. planned, tasks completed vs. added, number of context switches
2. Identify patterns: "You had 14 meetings this week (up from 10 last week). Focus time dropped from 12hrs to 6hrs. Your completion rate dropped from 80% to 55%."
3. Suggest concrete adjustments: "Consider declining the recurring Wednesday 'sync' meeting (you've noted it's low-value twice). Block Thursday afternoon as a no-meeting zone."
4. Track trends over 4+ weeks to identify systemic issues

**Value**: Most people don't realize how they actually spend their time. Quantified self-reflection with actionable suggestions drives real behavior change.  
**Source/Inspiration**: S2 (End-of-Week Review) extended with analytical depth; Reclaim.ai/Clockwise scheduling analytics; BAO user engagement analysis; Alignment in Time long-horizon planning  
**Confidence**: HIGH — requires only calendar data analysis; analytics + suggestions are straightforward

---

## Category 10: Safety & Emergency Preparedness

### S47: Weather-Aware Day Adjustment
**Trigger**: Cron (early morning, before S1 morning briefing) + severe weather alerts  
**Agent Actions**:
1. Check weather forecast including severe weather warnings
2. Cross-reference with today's calendar: outdoor events, commute-dependent meetings, travel days
3. Proactively suggest adjustments: "Winter storm warning for 2pm–8pm. Your 4pm is across town — suggest moving to virtual? Your grocery run could be shifted to this morning."
4. For severe events (wildfire, hurricane track, flood warning): escalate to immediate notification regardless of batching preferences

**Value**: Weather-related disruptions affect plans 50+ days/year in most climates. Proactive adjustment prevents missed commitments and safety risks.  
**Source/Inspiration**: ContextAgent weather + bus schedule + outdoor activity scenarios; ProAgent weather updates during commuting; S14 travel day intelligence generalized to daily weather awareness  
**Confidence**: HIGH — weather APIs are mature; calendar cross-referencing is straightforward

---

### S48: Emergency Contact & Info Quick Access
**Trigger**: User message (e.g., "I locked myself out" or "power outage")  
**Agent Actions**:
1. Maintain an emergency info registry: landlord/property manager phone, insurance policy numbers, utility company contacts, roadside assistance, nearest urgent care, trusted neighbor
2. On relevant queries, instantly surface the right contact + next steps
3. "Locked out? Your property manager: (555) 123-4567 (available until 9pm). Backup: spare key is with neighbor Lisa (unit 4B). Locksmith last used: QuickLock (503-555-7890, avg $85)."

**Value**: In stressful moments, finding the right phone number or next step is surprisingly hard. Instant access saves time and reduces panic.  
**Source/Inspiration**: Egocentric Co-Pilot fact lookup (98.5% TCR for foundational tool use); ContextAgent tool-use patterns; Knowledge base §8.8  
**Confidence**: HIGH — simple key-value retrieval from a pre-populated registry; only requires initial setup

---

## Category 11: Personal Knowledge & Learning

### S49: Article/Content Save & Summarize Queue
**Trigger**: User message ("save this for later: [URL]") or email forward  
**Agent Actions**:
1. User forwards an article, shares a URL, or says "save this podcast episode about distributed systems"
2. Fetch content and generate a 3-paragraph summary + key takeaways
3. Store in a personal knowledge base with tags
4. Weekly digest: "You saved 7 items this week. 3 unread. Here are the highlights from what you saved..."
5. When relevant topics come up in conversation or research, surface saved items: "You saved an article about this topic 3 weeks ago — want me to pull it up?"

**Value**: The "save for later" problem — people bookmark 10x more than they read. Summaries + proactive resurfacing close the loop.  
**Source/Inspiration**: Mem.ai knowledge management pattern; O-Mem episodic + semantic memory; Anatomy of Agentic Memory; AriadneMem lifelong memory; Evernote AI features  
**Confidence**: HIGH — URL fetching + summarization is a demonstrated LLM strength; tagging and retrieval uses existing memory patterns

---

### S50: Skill Gap Spotter
**Trigger**: Cron (quarterly) or Event (user mentions a goal/aspiration)  
**Agent Actions**:
1. Based on accumulated context about user's work, goals, and interests, identify skill gaps
2. When user says "I want to become a tech lead" or "I'm interested in ML engineering": research the role requirements and compare against known skills
3. Present a gap analysis: "Based on your profile, key gaps for tech lead: (1) System design experience — suggest: 'Designing Data-Intensive Applications' book + mock interviews (2) People management — suggest: 'The Manager's Path' + ask your manager about mentoring opportunities"
4. Connect to S17 (Learning Goal) for execution

**Value**: Career development is high-value but people rarely do structured gap analysis. Having an agent that knows your background makes this much more actionable.  
**Source/Inspiration**: IntentRL proactive clarification for deep research; Personalized LLM Agents survey (preference-aware planning); S17 Learning Goal as the execution layer  
**Confidence**: MEDIUM — requires sufficient accumulated context about user's professional background

---

## Category 12: Proactive Scheduling Intelligence (Beyond S11/S12)

### S51: Meeting Prep Time Auto-Blocking
**Trigger**: Event (new meeting added to calendar)  
**Agent Actions**:
1. When a new meeting appears on the calendar, assess if it needs prep time
2. High-prep indicators: external attendees, first meeting with someone, meeting title contains "review", "presentation", "demo"
3. Automatically block 15–30 minutes before the meeting for prep (configurable)
4. If the slot before is already taken, surface a warning: "Your 2pm with Client X needs prep but you're in back-to-back meetings from 12–2. Want to decline the 1:30 optional meeting?"

**Value**: 63% of meetings start with unprepared participants. Auto-blocking prep time is the simplest intervention with the highest impact.  
**Source/Inspiration**: Clockwise team calendar sync features; S13 meeting context prep + S12 focus time scheduling combined; Reclaim.ai habit protection applied to prep time  
**Confidence**: HIGH — simple calendar heuristic + auto-block; S13 already does the content prep, this adds the time protection

---

### S52: Commute-Aware Schedule Optimizer
**Trigger**: Event (new in-person meeting or location change detected)  
**Agent Actions**:
1. When a meeting has a physical location, estimate commute time from your current location or the previous meeting's location
2. Flag scheduling conflicts: "You have a meeting at HQ ending at 11:30am and a lunch across town at 12:00pm — commute is 25 min. You'll be 15 min late unless you leave the HQ meeting early."
3. Suggest solutions: leave early, request virtual for one meeting, or shift the lunch by 30 minutes
4. Factor in real-time traffic for the morning advisory

**Value**: Location-based scheduling conflicts are invisible in standard calendars. Saves embarrassment and wasted commute time.  
**Source/Inspiration**: ContextAgent GPS + bus schedule tools; ProAgent commuting scenario (transport info at bus stop); Motion project management schedule optimization; Next Paradigm cross-platform intent  
**Confidence**: MEDIUM — requires location data from calendar events and a maps/traffic API

---

### S53: Social Calendar Balancing
**Trigger**: Cron (weekly, as part of week overview)  
**Agent Actions**:
1. Track ratio of work vs. social vs. personal time on the calendar
2. Detect imbalances: "This is your 3rd consecutive week with zero social events. Your average is 2/week. Want me to suggest blocking a dinner slot this weekend?"
3. Cross-reference with S22 (Stay-in-Touch Cadence) to suggest who to reach out to
4. Offer to help plan: "You haven't seen [friend] in 6 weeks and both your Saturday evenings look free. Draft a message?"

**Value**: Work-life balance is measurable through calendar data. Gentle nudges prevent social isolation during busy periods.  
**Source/Inspiration**: Reclaim.ai work-life balance features; Alignment in Time long-horizon agent planning; Knowledge base §8.4 (Social Maintenance); BAO engagement optimization  
**Confidence**: MEDIUM — calendar categorization (work vs. social) requires either user tagging or heuristic classification

---

## Category 13: Creative & Miscellaneous

### S54: "On This Day" Personal Memory Resurfacing
**Trigger**: Cron (daily, morning)  
**Agent Actions**:
1. Search the agent's memory for events from exactly 1 year ago, 2 years ago, etc.
2. Surface meaningful memories: "1 year ago today: you started your first Rust project. You've come a long way — that project is now in production."
3. Can also resurface: trips taken, goals set, decisions made and their outcomes
4. Include only genuinely interesting items — not "1 year ago you bought paper towels"

**Value**: Personal nostalgia is highly valued (Facebook's "On This Day" is one of their most-engaged features). For a personal agent, this builds emotional connection and demonstrates long-term memory value.  
**Source/Inspiration**: AriadneMem lifelong memory management; AMemGym long-horizon memory evaluation; Anatomy of Agentic Memory (episodic memory resurfacing); O-Mem self-evolving memory  
**Confidence**: MEDIUM — value is directly proportional to how long the agent has been running and quality of stored memories

---

### S55: Ambient Research for Upcoming Decisions
**Trigger**: Event (agent detects an upcoming decision from calendar/email/conversation)  
**Agent Actions**:
1. When agent detects a decision point approaching (lease renewal, contract negotiation, annual review, car insurance renewal), automatically begin low-priority background research
2. Don't wait for user to ask — start gathering data: "Your car insurance renews May 1. I started comparing rates. So far: current provider ($1,200/yr) vs. Progressive ($980/yr) vs. GEICO ($1,050/yr). Want me to keep digging?"
3. Deliver findings 2 weeks before the decision deadline
4. Uses FullAuto trust for research phase, AskAndAct for any actions

**Value**: Many decisions are suboptimal simply because people don't have time to research. Ambient research ensures data is ready when the decision arrives.  
**Source/Inspiration**: PROPER proactiveness (anticipating needs before they're expressed); IntentRL clarification-before-execution pattern; Knowledge base §8.3; Lindy.ai "anticipate" feature pillar  
**Confidence**: MEDIUM — detecting upcoming decisions from signals is the hard part; research execution is proven

---

### S56: Multi-Party Scheduling Coordinator
**Trigger**: User message ("Schedule dinner with Alex, Sarah, and Mike this weekend")  
**Agent Actions**:
1. Check user's calendar for available windows
2. Draft a scheduling poll or direct outreach: "Hey! [User] wants to schedule dinner this weekend. Are you available: (a) Sat 6pm (b) Sat 7:30pm (c) Sun 6pm?"
3. Send via user's preferred channel (Discord, email, or SMS if integrated)
4. Collect responses and confirm the best slot
5. Create calendar event and send confirmation to all parties

**Value**: Group scheduling is one of the most tedious coordination tasks. Automating the back-and-forth saves 30+ minutes per event.  
**Source/Inspiration**: Next Paradigm (multi-party meeting coordination); Lindy.ai meeting scheduling feature; Clockwise team scheduling; S16 Event Planning scheduling component extracted as standalone  
**Confidence**: MEDIUM — outreach to other people requires careful trust controls; works best if friends/contacts know about the agent

---

### S57: Personal API / Data Dashboard
**Trigger**: User message ("What do you know about me?" or "Show my stats")  
**Agent Actions**:
1. Aggregate all data the agent has collected: tasks completed, research reports generated, money saved via price alerts, reminders triggered, emails triaged
2. Present a personal dashboard: "Since I started (47 days ago): 23 research reports, 156 emails triaged, $340 saved via price alerts, 12 reminders completed, 3 deadlines caught that would have been missed"
3. Show trends: "Your meeting load increased 20% this month. Research request frequency: up. Task completion rate: 87%."
4. Privacy control: user can review all stored data and delete any items

**Value**: Demonstrates tangible ROI of the agent. Builds trust through transparency. Addresses the "what does it actually do for me?" question.  
**Source/Inspiration**: BAO user engagement measurement; AgentSys secure memory management (data transparency); Personalized LLM Agents survey (user profile inspection); 12-Factor Agents observability principle  
**Confidence**: HIGH — aggregation of internal metrics; the agent already has all this data

---

## Audio Lifelogging (Phase 5)

> These scenarios depend on the audio lifelogging pipeline described in `design.md` §8.4 and researched in `knowledge-base/audio-lifelogging-research.md`.

---

### S58 — Conversation Recall Query

**Trigger**: User asks "What did Alex say about the project deadline last Tuesday?"  
**Agent Behavior**:
1. Searches conversational memory (vector embeddings + metadata filters: speaker="Alex", date range=last Tuesday)
2. Retrieves matching transcript segments with timestamps and speaker labels
3. Synthesizes a concise answer with direct quotes and context
4. Offers to show the full transcript segment or create a follow-up task

**Workers**: AudioLifelogAgent (retrieval), ResearchAgent (synthesis)  
**Phase**: 5 (requires conversational memory store + speaker-attributed transcripts)  
**Value**: The killer use case for audio lifelogging. Eliminates "I think they said..." uncertainty. Directly addresses memory augmentation research (Vemuri et al., 2006; Harvey et al., 2016).  
**Source/Inspiration**: `say` project (u/8ta4, 2+ years daily use); SenseCam-based memory augmentation (Hodges et al., 2006); audio lifelogging research §4 (Academic Foundations)  
**Confidence**: HIGH — proven by `say` users who rely on this daily; core value proposition of audio lifelogging

---

### S59 — Automatic Meeting Notes from Ambient Audio

**Trigger**: AudioLifelogAgent detects a multi-speaker conversation segment ≥5 minutes with recognized contacts  
**Agent Behavior**:
1. Speaker diarization identifies participants from enrolled gallery (ECAPA-TDNN matching)
2. LLM structures transcript into: attendees, topics discussed, decisions made, action items
3. Action items automatically become LifeTasks assigned to the user (with "spoken commitment" tag)
4. Meeting summary pushed as a notification; full transcript queryable in conversational memory
5. If a calendar event overlaps the time window, links the notes to that event

**Workers**: AudioLifelogAgent, ScheduleWorker (calendar correlation), SummaryAgent  
**Phase**: 5  
**Value**: Eliminates manual note-taking. Meeting notes appear automatically with zero user effort. Action items actually get tracked.  
**Source/Inspiration**: Omi wearable product (automated meeting summaries); Limitless pendant (before Meta acquisition); CAM-based lifelogging (Doherty et al., 2012)  
**Confidence**: HIGH — Omi and Limitless both ship this feature commercially; proven demand

---

### S60 — Spoken Task Capture ("I Need To...")

**Trigger**: AudioLifelogAgent detects the user saying a commitment phrase (e.g., "I need to...", "Remind me to...", "I should...") in any conversation  
**Agent Behavior**:
1. NLU extraction identifies the task description and any time references ("by Friday", "next week")
2. Creates a LifeTask with the commitment as description, deadline parsed from context
3. Sends a confirmation notification: "I heard you say you need to [task]. Created a reminder for [deadline]. Correct?"
4. User can confirm, edit, or dismiss (feedback loop updates extraction accuracy)

**Workers**: AudioLifelogAgent, ReminderWorker  
**Phase**: 5  
**Value**: Captures commitments at the moment they're made — the highest-fidelity point. No more forgetting what you agreed to in conversation.  
**Source/Inspiration**: u/8ta4's "ADHD management" use case; ProMemAssist episodic memory for commitments; audio lifelogging research §11 (Open Problems — commitment detection)  
**Confidence**: MEDIUM — intent detection from ambient audio has higher false-positive rate than explicit commands; confirmation step is critical

---

### S61 — Daily Conversation Digest

**Trigger**: Scheduled (end of day, before evening summary)  
**Agent Behavior**:
1. Aggregates all conversation segments from the day (time, duration, participants, topics)
2. LLM generates a digest: "Today you had 7 conversations (2h 14m total). Key topics: project deadline with Alex (35 min), dinner plans with Sarah (12 min), 1:1 with manager about promotion timeline (28 min)..."
3. Highlights any unresolved action items from conversations
4. Includes in the evening briefing alongside calendar and task summaries

**Workers**: AudioLifelogAgent, SummaryAgent  
**Phase**: 5  
**Value**: Provides a bird's-eye view of the social/conversational dimension of your day. Complements the task/calendar summary. Enables reflection.  
**Source/Inspiration**: Harvey et al. (2016) — audio lifelogs for event summarization; SenseCam daily review studies (Browne et al., 2011); audio lifelogging research §9 (Architecture Patterns)  
**Confidence**: HIGH — aggregation + summarization is well-understood; no novel ML required beyond what S59 already provides

---

### S62 — Speaker-Attributed Knowledge Graph Updates

**Trigger**: AudioLifelogAgent processes a conversation containing factual statements from recognized speakers  
**Agent Behavior**:
1. NER + LLM extract entities and relationships from speaker-attributed transcript
2. Updates semantic memory with attributed facts: "Alex mentioned he's moving to Portland in March", "Manager said Q2 budget is approved"
3. Cross-references with existing knowledge graph entries; flags contradictions ("Last month Alex said he was staying in Seattle")  
4. Facts are tagged with source (conversation date, speaker), confidence, and decay schedule

**Workers**: AudioLifelogAgent  
**Phase**: 5  
**Value**: Your knowledge about people and topics is automatically maintained and updated from real conversations. The agent knows what you know — and what people told you.  
**Source/Inspiration**: O-Mem persistent user model; Anatomy of an AI Agent (social context tracking); Pierce & Mann (2021) — affect-enriched personal knowledge; audio lifelogging research §4  
**Confidence**: MEDIUM — NER/relation extraction from conversational speech is noisier than from written text; attribution confidence needs calibration

---

### S63 — Voice-Activated Dream Journal

**Trigger**: User speaks immediately after waking (detected via time-of-day + sleep schedule in user profile)  
**Agent Behavior**:
1. Transcribes the morning voice memo (typically 1-3 minutes of dream description)
2. LLM structures into: narrative summary, recurring themes, emotional tone, named people/places
3. Stores in a dedicated "dream journal" section of conversational memory
4. Over time, identifies recurring dream themes and correlates with daily activities/stress levels
5. Optional: includes dream summary in morning briefing ("You described a dream about flying over water — this is the 3rd time this month")

**Workers**: AudioLifelogAgent, SummaryAgent  
**Phase**: 5  
**Value**: Dream journaling via voice is the lowest-friction capture method. Most dream details are lost within minutes of waking — voice capture happens at the optimal moment.  
**Source/Inspiration**: u/8ta4's "dream journaling" emergent use case; affective computing research (Pierce & Mann, 2021); audio lifelogging research §3 (Community Experiences)  
**Confidence**: MEDIUM — technically simple (it's just transcription + structuring), but theme correlation and insight generation over time is speculative

---

---

## Category 15: Wellness — Preventive Care & Medical

> **Research basis**: `knowledge-base/human-wellness-research.md` §5-7. The USPSTF maintains 40+ Grade A/B screening recommendations. Most adults are missing at least one. The agent's role: maintain a personalized schedule and make it trivially easy to follow through, like a PA who never forgets.

### S64: Personalized Preventive Care Calendar
**Trigger**: Setup (one-time onboarding) + Cron (monthly audit)
**Agent Actions**:
1. During onboarding, ask: age, sex, known risk factors (smoking history, family history of cancer/diabetes/heart disease), last dates for PCP visit, dental, eye exam, and any screenings
2. Generate a personalized USPSTF-aligned screening schedule: mammogram every 2yr (women 40-74), colonoscopy starting at 45, blood pressure annually, etc.
3. Store in a wellness calendar. Monthly audit checks what's overdue.
4. Overdue items surface in the daily briefing: "Your annual physical is 3 months overdue. Your last cholesterol check was 5 years ago (recommended every 4-6 years)."
5. Include a "schedule now" action that creates a reminder to call the provider

**Value**: Preventive care is the domain where agents have the clearest ROI — a missed colonoscopy can mean the difference between a polyp removal and stage 3 colon cancer. Most people have no system for tracking this.
**Research**: USPSTF A/B recommendations; wellness research §5; 46% of adults are behind on at least one screening (JAMA, 2021)
**Confidence**: HIGH — date tracking + age-based rules; no ML required

---

### S65: Dental Visit Lifecycle Manager
**Trigger**: Event (dental visit completed) + Cron (6-month check)
**Agent Actions**:
1. After a dental visit, ask: "Visit done? Want me to schedule the next cleaning in 6 months?"
2. If user confirms, create a calendar reminder at 5 months ("Schedule dental cleaning — your 6-month window opens next month")
3. At 6 months: "Your dental cleaning is due. Last one was [date]. Want me to remind you to call [dentist name]?"
4. If >8 months overdue, escalate once: "It's been 8+ months since your last dental visit. Periodontal disease is linked to heart disease — worth getting back on track."
5. Never mention it again until next cycle (no nagging beyond one escalation)

**Value**: 42% of U.S. adults have periodontal disease (CDC). The "always have the next one booked" pattern eliminates the #1 barrier: forgetting to schedule.
**Research**: Wellness research §6; Lockhart et al., 2012 (AHA); Taylor & Borgnakke, 2008
**Confidence**: HIGH — pure calendar tracking

---

### S66: Vision Care Tracker
**Trigger**: Cron (annual check) + age-adaptive scheduling
**Agent Actions**:
1. Track last comprehensive eye exam date
2. Schedule based on age bracket: every 2 years (18-39), every 1-2 years (40-64), annually (65+)
3. For users with known risk factors (diabetes, family glaucoma history, high myopia): annual regardless
4. When overdue: "Your eye exam is due. Comprehensive exams detect glaucoma and diabetic retinopathy — conditions with no symptoms until irreversible damage is done."
5. Optional: for heavy screen users, offer the 20-20-20 reminder plugin (disabled by default — too annoying for most)

**Value**: Glaucoma is the "silent thief of sight" — detected only through exams. Eye exams also catch systemic diseases (diabetes, hypertension) via retinal changes.
**Research**: Wellness research §7; American Optometric Association guidelines
**Confidence**: HIGH — simple date tracking with age-based rules

---

### S67: "You Mentioned a Symptom" Passive Logger
**Trigger**: Event (audio pipeline detects the user mentioning a health symptom 3+ times in a week)
**Agent Actions**:
1. Audio pipeline detects recurring health complaints: headaches, back pain, fatigue, stomach issues, etc.
2. After detecting the same complaint 3+ times in a rolling 7-day window, log it privately
3. At end of week or before a known PCP appointment: "You mentioned headaches 4 times this week. Want me to add this to your notes for your next doctor's appointment?"
4. **Never diagnose. Never suggest what the symptom means. Never recommend OTC medication.**
5. Just observe, log, and help the user remember to bring it up with their actual doctor

**Value**: People forget to mention symptoms to their doctors. The doctor visit is 15 minutes, and the chronic headache you've had for 3 weeks doesn't make it into the conversation because you're focused on the acute issue. Passive logging solves this.
**Research**: Wellness research §5.3; ProMemAssist episodic memory; audio lifelogging research
**Confidence**: MEDIUM — depends on audio pipeline quality; NLU for symptom detection has moderate accuracy in ambient speech. False positives are low-risk (worst case: an unnecessary note)

---

## Category 16: Wellness — Physical Activity & Sleep

> **Research basis**: `knowledge-base/human-wellness-research.md` §2-3. Physical activity alone could prevent 110,000 deaths/year in U.S. adults 40+. Sleep deprivation at <6 hours impairs cognition equivalent to 0.10% BAC. These are the two highest-leverage behavioral domains.

### S68: Weekly Fitness Account
**Trigger**: Cron (Sunday evening, as part of weekly review) + wearable/self-report data
**Agent Actions**:
1. Compile the week's activity: aerobic minutes, resistance training sessions, steps, active days
2. Compare against targets (150 min aerobic, 2x resistance, 8K+ steps daily)
3. Present a non-judgmental summary in the weekly review:
   > "This week: 95/150 aerobic minutes (3 runs), 1/2 lifting sessions, avg 7,200 steps/day. Last week was 130/150 and 2/2. You're trending down — anything getting in the way?"
4. If targets met: brief celebration + consistency streak count
5. If consistently under-target for 3+ weeks: "Want to revisit your targets? Or find a different time slot for workouts?"
6. Connect to calendar: "You have 3 open lunch windows this week that could work for a gym session"

**Value**: Nobody tracks their fitness against WHO guidelines unless they're already fitness-oriented. The agent does the accounting so you just see the gap. The calendar connection removes the "no time" excuse.
**Research**: Wellness research §2; WHO/CDC guidelines; Shailendra et al., 2022 (resistance training meta-analysis)
**Confidence**: HIGH — data aggregation + comparison. Wearable integration adds precision but self-report works fine.

---

### S69: The 9-Day Rule (Exercise Re-Engagement)
**Trigger**: Event (no exercise detected for 7+ consecutive days)
**Agent Actions**:
1. On day 7 with no exercise: low-urgency note in daily briefing: "It's been a week since your last workout. Even a 20-minute walk counts."
2. On day 9 (the point where habit decay becomes hard to reverse): slightly more direct: "9 days without exercise. Research shows this is where habits break. Your calendar has a gap at [time] today — good window for even a short session?"
3. If user dismisses: back off for 3 days (no nagging)
4. If user responds positively: suggest a specific, achievable activity (not "go run 5K" — more like "go for a walk around the block")
5. When they do exercise after a break: "Welcome back. First session after a break is the hardest — nice work."

**Value**: Exercise habits break at the 7-10 day gap (Kaushal & Rhodes, 2015). This is the most critical intervention point — easy to intercept if you're tracking, invisible otherwise the user doesn't notice until it's been 3 weeks.
**Research**: Wellness research §2.3; habit formation research (Lally et al., 2010 — 66-day average for habit formation)
**Confidence**: HIGH — simple streak tracking + calendar awareness

---

### S70: Sleep Pattern Watchdog
**Trigger**: Cron (weekly) + daily sleep data
**Agent Actions**:
1. Track nightly sleep duration from wearable or phone screen-on/off times
2. Calculate rolling 7-day average, consistency (variance in bed/wake times), and trend
3. Weekly whisper in the briefing if everything's fine: "Sleep: 7.2h avg, consistent. Good."
4. If 3+ nights under 6 hours in a week: "You've slept less than 6 hours on 3 nights this week. That's associated with cognitive performance equivalent to 0.10% BAC. Something keeping you up?"
5. Correlate with other signals: "Your sleep dropped this week and you also had 3 late-night meetings. The meeting schedule might be the culprit."
6. Evening pre-sleep nudge (if opted in): "It's 10:30pm. Getting to bed by 11 gives you 7.5 hours before your alarm."

**Value**: Sleep is bidirectionally linked to almost every other wellness domain. Fixing sleep improves exercise motivation, mood, productivity, and immune function. Most people conceptually know this but have no feedback loop.
**Research**: Wellness research §3; Cappuccio et al., 2010; Williamson & Feyer, 2000
**Confidence**: HIGH — wearable sleep data is widely available; screen-on/off is a reasonable proxy

---

## Category 17: Wellness — Medication & Chronic Care

> **Research basis**: `knowledge-base/human-wellness-research.md` §8. ~50% medication non-adherence rate (unchanged for 20+ years). Simple reminders improve adherence 10-20% (Cochrane). This is the closest thing to a "free lunch" in health technology.

### S71: Intelligent Medication Reminder (Beyond S25)
**Trigger**: Daily at user-configured times + context-aware adjustments
**Agent Actions**:
1. Unlike S25 (basic reminder), this adapts to life context:
   - Travel day detected? Remind earlier: "You fly at 6am — take your meds before heading to the airport"
   - Time zone change? Auto-adjust reminder times for the new zone
   - Weekday vs. weekend? Some users prefer different times
2. Track confirmation. If not confirmed within 2 hours of reminder, one follow-up: "Did you take your [morning meds]? Just checking."
3. Weekly adherence score in briefing: "Medications: 6/7 days this week (85%). Last week was 7/7."
4. If refill is approaching (user-configured interval): "Your [medication] refill is due in 5 days. Pharmacy: [name]. Want me to remind you to call in the refill?"
5. If audio pipeline detects "I ran out of my prescription": immediate proactive note + pharmacy reminder

**Value**: The difference between 50% and 70% adherence is measurable in hospitalizations, strokes, and cardiac events. A daily reminder at the right time is one of the simplest, highest-value interventions any health system can perform.
**Research**: Wellness research §8; Thakkar et al., 2016 (Cochrane); Osterberg & Blaschke, 2005
**Confidence**: HIGH — this is the quintessential agent use case: simple, low-risk, high-impact, trivially automatable

---

### S72: Chronic Condition Pattern Correlation
**Trigger**: Cron (monthly) + accumulated data
**Agent Actions**:
1. For users managing chronic conditions (diabetes, hypertension, autoimmune, etc.), correlate behavioral patterns with self-reported flare-ups or readings
2. Example: "You reported high blood sugar on 3 occasions this month. On 2 of those days, you also slept under 5 hours. Sleep deprivation impairs insulin sensitivity — worth discussing with your endocrinologist?"
3. Track: sleep patterns around flare days, exercise patterns, diet changes, stress indicators (calendar density)
4. Present correlations as observations, never as causal claims: "I notice X tends to coincide with Y" not "X causes Y"
5. Offer to include the correlation in notes for the next doctor visit

**Value**: Patients managing chronic conditions are drowning in data but nobody's connecting the dots. A doctor sees you for 15 minutes quarterly. The agent sees you every day and can spot patterns invisible in a single visit.
**Research**: Wellness research §8; Type 2 diabetes management (CDC); medication adherence patterns
**Confidence**: MEDIUM — correlation detection is straightforward but spurious correlations are a real risk. All claims must be prefaced as observational, never diagnostic.

---

## Category 18: Wellness — Social Connection & Mental Health

> **Research basis**: `knowledge-base/human-wellness-research.md` §9-10. Social isolation = 29% increased premature mortality. Loneliness doubles depression risk. The Surgeon General called it an epidemic. This is the hardest domain for an agent — social connection is not a metric you can "remind" people to achieve — but it may be the most important.

### S73: The Compound Wellbeing Signal
**Trigger**: Event (ProactivityScanner detects multi-domain deterioration)
**Agent Actions**:
1. This is not a single-domain scenario. It fires when **multiple upstream indicators deteriorate simultaneously**:
   - No exercise for 7+ days AND
   - Sleep averaging <6 hours AND
   - No social conversations detected by audio pipeline for 5+ days AND
   - (Optional) Mood self-report declining
2. When ≥3 of these signals converge, deliver a single, carefully worded check-in:
   > "Hey — I've noticed you haven't been sleeping much, haven't worked out, and haven't been talking to people lately. No judgment, and I could be wrong about the audio data, but — are you doing OK? If you want to talk to someone, I have resources. Otherwise, even a short walk or a call to a friend might help reset."
3. **This message sends at most once per month.** After sending, all wellness nudges in individual domains are paused for 48 hours (don't pile on).
4. If user dismisses: note it, don't bring up again for 30 days
5. If user responds: engage conversationally, offer concrete low-effort actions

**Value**: This is the scenario that justifies the entire wellness system. No single metric tells you someone is struggling. But the compound signal — sleep + isolation + inactivity — is a powerful predictor of mental health crisis. Catching it early and gently is the difference between "a rough patch" and "a spiral."
**Research**: Wellness research §9 (mental health), §10 (social connection); Holt-Lunstad et al., 2015; Mann et al., 2022
**Confidence**: MEDIUM — the detection is high-confidence (multiple objective signals), but the response is high-risk (tone, timing, privacy). Must be done exactly right or not at all. False positive cost: user feels surveilled. False negative cost: missing someone who needs help.

---

### S74: Social Pulse (Audio-Augmented)
**Trigger**: Cron (weekly) + audio pipeline
**Agent Actions**:
1. The audio pipeline passively detects conversations throughout the week
2. Speaker identification reveals WHO the user talked to (from enrolled gallery)
3. Weekly social pulse in the briefing:
   > "Social: 5 conversations this week (2h 10m total). Talked with: Alex (3x), Sarah (1x), Mom (1x). You usually average 7 conversations — slightly quieter week."
4. If 2+ consecutive weeks are below baseline: "You've been quieter than usual socially the past couple weeks. Want to plan something with someone this weekend?"
5. Cross-reference with S22 (Stay-in-Touch Cadence): "You haven't talked to [configured contact] in [N] days — want to reach out?"

**Value**: Unlike self-reported social tracking, this happens passively through the audio pipeline. The user doesn't have to log anything. Social isolation detection becomes automatic. This integrates social connection monitoring (Surgeon General's recommendation) into the agent's ambient awareness.
**Research**: Wellness research §10; Surgeon General Advisory, 2023; CDC Social Connectedness, 2024
**Confidence**: MEDIUM — depends on audio pipeline + speaker ID quality. Cannot detect phone calls unless they happen near the pendant. Text/messaging interactions are invisible to audio.

---

### S75: Crisis Resource Delivery
**Trigger**: Event (audio pipeline or text input detects crisis language)
**Agent Actions**:
1. If user expresses suicidal ideation, self-harm language, or clear crisis indicators via text or audio:
   - **Immediately** — not batched, not deferred, not waiting for the daily briefing:
   > "I hear you, and I want to make sure you have support right now.
   >
   > 988 Suicide & Crisis Lifeline: call or text **988** (available 24/7)
   > Crisis Text Line: text **HOME** to **741741**
   >
   > You're not alone. These are confidential, free services."
2. **Do not attempt to counsel, therapize, or "talk them through it."** The agent is not trained for this. Providing resources and getting out of the way is the right thing.
3. Log the event privately (not in briefings, not in weekly reviews)
4. Do not follow up with "are you feeling better?" — that's clinical territory
5. If appropriate, in the next daily briefing (24+ hours later), include a gentle: "If you ever want to find a therapist, I can help you search."

**Value**: This is a safety boundary, not a feature. It must exist. It must be implemented exactly as described or not at all.
**Research**: 988 Suicide & Crisis Lifeline; USPSTF depression/suicide screening (Grade B); wellness research §9
**Confidence**: HIGH (for the response protocol) / MEDIUM (for detection accuracy). Detection of crisis language in ambient audio has non-trivial false positive risk. In text input, detection is more reliable. Err on the side of providing resources even for borderline cases.

---

### S76: Therapy Continuity Tracker
**Trigger**: Cron (per user's therapy schedule) + calendar
**Agent Actions**:
1. User configures: "I see a therapist every other Wednesday at 2pm"
2. Agent tracks attendance and gaps
3. If a session is missed: no comment (could be vacation, sick, etc.)
4. If 3+ sessions missed in a row or 6+ weeks since last visit: gentle nudge: "You haven't seen your therapist in 6 weeks. Life gets busy — want me to set a reminder to reschedule?"
5. If user says "I want to find a therapist" (audio or text): offer to research options: insurance-covered providers in the area, waitlist lengths, specialties

**Value**: Therapy dropout rate is >40% within the first 3 months. The #1 reason: life gets in the way and people stop scheduling. A persistent system that notices the gap and gently nudges can meaningfully reduce dropout.
**Research**: Wellness research §9; therapy dropout statistics (Wierzbicki & Pekarik, 1993)
**Confidence**: HIGH — simple calendar tracking; the intervention is just a reminder, same as dental

---

## Category 19: Wellness — Stress, Recovery & Environment

### S77: Overwork Alarm
**Trigger**: Cron (weekly) + calendar analysis
**Agent Actions**:
1. Analyze work patterns: meetings/day, hours with calendar events, consecutive work days without a day off
2. Flag concerning patterns:
   - >10 hours scheduled 3+ days this week: "You've had 10+ hour days three times this week. Your sleep has dropped 45 minutes compared to last week."
   - >14 consecutive days without a weekend/day off: "You haven't had a day off in over 2 weeks. The INTERHEART study found chronic work stress accounts for ~33% of heart attack risk."
   - Meeting overload: "You have 7 meetings tomorrow with zero gaps. That's a recipe for decision fatigue."
3. Suggest specific actions: decline an optional meeting, block a recovery day, take a walk between meetings
4. Track vacation days: "You have 14 unused vacation days and it's Q4. Want me to suggest some long weekends?"

**Value**: Knowledge workers chronically overwork without noticing. The calendar is the best proxy we have. Interventions are simple: block time, decline meetings, schedule recovery.
**Research**: Wellness research §12; Rosengren et al., 2004 (INTERHEART); Framingham subsidiary data on vacation
**Confidence**: HIGH — pure calendar analysis; low false-positive risk

---

### S78: Nature & Sunlight Nudge
**Trigger**: Cron (daily, late morning) + weather API + calendar
**Agent Actions**:
1. Check if user has been outdoors today (wearable/location data, or infer from calendar — all-day indoor meetings)
2. If weather is good and user has a gap: "It's 65°F and sunny. You have a free slot at 11am. Even 20 minutes outside measurably reduces cortisol."
3. If user has been indoors for 3+ consecutive days (no outdoor activities on calendar, no walks detected): "You haven't been outside in 3 days. Morning sunlight exposure helps regulate your circadian rhythm and improves sleep."
4. Seasonal adaptation: during shorter winter days, emphasize morning light exposure. "Sun sets at 4:45pm today — get outside before noon if you can."

**Value**: Nature exposure (20+ min) significantly reduces cortisol (Hunter et al., 2019). Morning light exposure regulates circadian rhythm, improving sleep and mood. Most indoor workers go days without meaningful outdoor time without realizing it.
**Research**: Wellness research §12; Hunter et al., 2019; circadian rhythm research
**Confidence**: HIGH — weather API + calendar gap detection; straightforward

---

### S79: Ergonomic Awareness (For Desk Workers)
**Trigger**: Event (audio pipeline detects repeated mentions of physical discomfort) + periodic
**Agent Actions**:
1. If user mentions back pain, neck pain, wrist pain, or eye strain 3+ times in 2 weeks: "You've mentioned [back pain] a few times recently. For desk workers, sometimes small ergonomic changes help — have you checked your monitor height and chair position lately?"
2. One-time resource share: link to a simple ergonomic setup guide
3. If user works from home and mentions it: "Since you're working from home: a separate monitor, an external keyboard, and a chair adjustment can prevent chronic issues. Want me to research affordable options?"
4. Never re-send the same advice. Log it as delivered.

**Value**: Musculoskeletal disorders account for 33% of workplace injuries (BLS). Most remote workers have suboptimal setups and don't fix them until chronic pain develops.
**Research**: Wellness research §14; BLS workplace injury data
**Confidence**: MEDIUM — audio detection of pain mentions has moderate accuracy; intervention is low-risk

---

## Category 20: Wellness — The Long Game (Longitudinal Scenarios)

> These scenarios play out over months or years. They require the agent to maintain long-term memory, detect slow trends, and connect dots across long time horizons. This is where a persistent agent adds value no other tool can.

### S80: Annual Health Review
**Trigger**: Cron (annually, on user's birthday or start-of-year)
**Agent Actions**:
1. Compile a year-in-review wellness summary:
   > "**2026 Health Summary**
   >
   > **Exercise**: You averaged 120 min/week aerobic (target: 150). Lifting 1.5x/week (target: 2). Consistency improved from Q1→Q4.
   >
   > **Sleep**: Average 6.8 hours (target: 7+). Best month: August (7.3h). Worst: November (6.2h — coincided with project crunch).
   >
   > **Preventive care**: Annual physical ✓, dental ×2 ✓, eye exam ✓, flu shot ✓. Overdue: colonoscopy (you're 46 — recommended starting at 45).
   >
   > **Social**: ~4 social interactions/week avg. Best month: July. Quietest: January.
   >
   > **Medications**: 89% adherence (up from 76% last year).
   >
   > **Trends**: Your best health weeks correlate strongly with weeks where you exercised 3+ times. Your worst sleep weeks correlate with >8 meetings/day."
2. Highlight the #1 area to focus on next year: "If I had to pick one thing: getting the colonoscopy done. Everything else is trending well."
3. Compare year-over-year if prior year data exists

**Value**: No human maintains a longitudinal view of their own health behaviors across a whole year. This is the unique power of a persistent agent: it never forgets, it connects patterns across months, and it can frame the big picture in a way you'd never assemble yourself.
**Research**: All wellness research domains; the synthesis capability is the point
**Confidence**: HIGH — pure data aggregation with long-term memory

---

### S81: Life Stage Transition Awareness
**Trigger**: Event (user mentions a major life change) + age milestones
**Agent Actions**:
1. Detect major transitions from audio/text: new job, moving, having a child, retirement, loss of a loved one, divorce
2. For each transition type, research and surface relevant wellness considerations:
   - New parent: "Sleep deprivation is expected, but watch for persistent mood changes (postpartum depression screening recommended). Your exercise dropped to zero — even 15-minute walks matter."
   - Turning 45: "New screening recommendation: colorectal cancer screening should start now if you haven't already. Also a good time for a baseline cardiac risk assessment."
   - Retirement: "Social isolation risk increases sharply after retirement. Your social interactions dropped 60% since your last day. Consider structured activities: volunteering, classes, regular meetups."
   - Loss: "I'm sorry for your loss. I'll suppress non-essential notifications for a week. If you want to talk to a grief counselor, I can help you find one."
3. Adjust wellness monitoring thresholds temporarily: after a life disruption, relax exercise/sleep targets for 2-4 weeks so the agent doesn't nag during a crisis

**Value**: Life transitions are when people are most vulnerable to health deterioration AND most likely to ignore self-care. An agent that recognizes the transition and adjusts its behavior accordingly demonstrates genuine intelligence.
**Research**: Wellness research §9 (mental health transitions); Surgeon General Advisory on social connection; ProPerSim personality adaptation
**Confidence**: LOW-MEDIUM — detecting life transitions from speech is hard (high NLU requirement). Age-based triggers are easy. The response protocol is well-defined but the detection is the bottleneck.

---

### S82: The Accountant of Your Best Intentions
**Trigger**: Event (audio pipeline detects a health-related intention) + follow-up check
**Agent Actions**:
1. Audio pipeline detects the user expressing a health intention:
   - "I should really start running again"
   - "I need to eat better"
   - "I'm going to start meditating"
   - "I should call to schedule that appointment"
2. Log the intention with date and context
3. After 7 days, if no action detected: "A week ago you mentioned wanting to start running again. Want to pick a day this week? Your calendar has openings on [Tuesday, Thursday]."
4. If user acts on it: celebrate briefly, track the streak
5. If user dismisses: note it, don't mention the same intention again for 30 days
6. Long-term: "Last January you said you wanted to eat better. Here's what actually happened: home cooking went from 2x/week to 4x/week by March, then back to 2x by June. Want to give it another push?"

**Value**: This is the core promise of the life agent. Humans are full of good intentions that evaporate in 48 hours. The agent is the accountant who remembers every "I should..." and gently follows up — not to judge, but to help you become who you said you wanted to be.
**Research**: Wellness research §16.5 (audio pipeline integration); habit formation research (Lally et al., 2010); audio lifelogging commitment detection
**Confidence**: MEDIUM — intention detection from ambient speech is imprecise, same challenges as S60 (Spoken Task Capture). But the follow-up protocol is high-value and low-risk.

---

*Total: 65 new scenarios (S18–S82), organized in 20 categories.*

---

## Summary Statistics

| Confidence | Count | Examples |
|-----------|-------|---------|
| **HIGH** | 32 | Subscription Audit, Bill Anomaly, Medication Reminder, Email Digest, Notification Batching, Conversation Recall, Meeting Notes, Conversation Digest, Preventive Care Calendar, Dental Lifecycle, Sleep Watchdog, Exercise Re-Engagement, Annual Health Review, Overwork Alarm |
| **MEDIUM** | 27 | Gift Suggestions, Context Pre-Loading, Flight Price Watch, Skill Gap Spotter, Ambient Research, Spoken Task Capture, Knowledge Graph, Dream Journal, Compound Wellbeing Signal, Social Pulse, Chronic Condition Correlation, Ergonomic Awareness, Health Intention Accountant |
| **LOW** | 3 | Proactive "Did You Know" Alerts, Travel Disruption Response, Life Stage Transition Awareness |

| Category | Count | Phase Recommendation |
|----------|-------|---------------------|
| Financial & Shopping | 4 | Phase 2–3 (requires Gmail) |
| Social & Relationships | 3 | Phase 2 (cadence tracking), Phase 3 (email integration) |
| Health & Wellness (original) | 3 | Phase 2 |
| Knowledge Work | 4 | Phase 3 (heavy calendar/email integration) |
| Home & Maintenance | 3 | Phase 2 |
| Travel Optimization | 3 | Phase 2–3 |
| Communication & Email | 3 | Phase 3 (Gmail required) |
| Decision Support | 3 | Phase 1–2 (builds on existing research) |
| Cognitive Load Management | 3 | Phase 2 |
| Safety & Emergency | 2 | Phase 1–2 |
| Personal Knowledge | 2 | Phase 2 |
| Proactive Scheduling | 3 | Phase 3 (calendar required) |
| Creative & Miscellaneous | 4 | Phase 2–3 |
| Audio Lifelogging | 6 | Phase 5 (BLE pendant + Deepgram + speaker ID) |
| **Wellness — Preventive Care** | **4** | **Phase 2 (date tracking) + Phase 5 (symptom logger)** |
| **Wellness — Physical Activity & Sleep** | **3** | **Phase 2 (self-report) + wearable integration** |
| **Wellness — Medication & Chronic Care** | **2** | **Phase 1–2 (reminders are day-1 capability)** |
| **Wellness — Social & Mental Health** | **4** | **Phase 2 (crisis resources), Phase 5 (compound signal, social pulse)** |
| **Wellness — Stress & Environment** | **3** | **Phase 2 (calendar-based), Phase 5 (audio ergonomic)** |
| **Wellness — Longitudinal** | **3** | **Phase 3+ (requires months of accumulated data)** |

### Key Research Sources

| Source | Scenarios Inspired |
|--------|-------------------|
| Knowledge base §8.3–8.8 (Creative Use Cases) | S18, S19, S21, S22, S31, S32, S36, S43, S48 |
| ProActive Agent / ProAgent papers | S19, S24, S29, S33, S47, S52 |
| BAO Pareto frontier research | S27, S42, S44, S46, S53 |
| PROPER proactivity ("unknown unknowns") | S30, S42, S55 |
| Next Paradigm user-centric agents | S36, S37, S43, S52, S56 |
| Lindy.ai product features | S18, S21, S38, S39, S40, S55 |
| Reclaim.ai / Clockwise / Motion | S26, S46, S51, S52, S53 |
| ProMemAssist / ProPerSim | S25, S27, S33, S44 |
| Personalized LLM Agents survey | S22, S39, S50, S57 |
| O-Mem / AriadneMem / Anatomy of Memory | S22, S39, S45, S49, S54 |
| IntentRL / Choose Your Agent | S41, S50, S55 |
| Egocentric Co-Pilot | S19, S25, S48 |
| ContextAgent / ContextAgentBench | S20, S26, S33, S47, S52 |
| Zapier / IFTTT / Declarative Workflows | S18, S33, S34, S38 |
| Audio lifelogging research (`knowledge-base/audio-lifelogging-research.md`) | S58, S59, S60, S61, S62, S63 |
| **Human wellness research (`knowledge-base/human-wellness-research.md`)** | **S64, S65, S66, S67, S68, S69, S70, S71, S72, S73, S74, S75, S76, S77, S78, S79, S80, S81, S82** |
| **WHO/CDC physical activity guidelines** | **S68, S69, S77** |
| **USPSTF A/B screening recommendations** | **S64, S66, S75** |
| **U.S. Surgeon General Social Connection Advisory** | **S73, S74, S81** |
| **Medication adherence meta-analyses (Cochrane)** | **S71, S72** |
