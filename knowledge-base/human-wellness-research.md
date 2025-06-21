# Human Wellness & Longevity: What a Life Agent Should Track

> **Research Date:** March 2026
> **Purpose:** Evidence-based research on how a proactive AI life agent can monitor and improve the physical health, mental health, social connectedness, and preventive care of its human partner. Every domain includes hard numbers from peer-reviewed research, WHO/CDC guidelines, and real-world studies — because an agent that nudges without evidence is just noise.

---

## Table of Contents

1. [The Case for Proactive Health Monitoring](#1-the-case-for-proactive-health-monitoring)
2. [Physical Activity](#2-physical-activity)
3. [Sleep](#3-sleep)
4. [Nutrition & Hydration](#4-nutrition--hydration)
5. [Preventive Medical Care](#5-preventive-medical-care)
6. [Dental Health](#6-dental-health)
7. [Vision Care](#7-vision-care)
8. [Medication Adherence](#8-medication-adherence)
9. [Mental Health](#9-mental-health)
10. [Social Connection](#10-social-connection)
11. [Substance Use & Harm Reduction](#11-substance-use--harm-reduction)
12. [Stress & Recovery](#12-stress--recovery)
13. [Cognitive Health & Lifelong Learning](#13-cognitive-health--lifelong-learning)
14. [Environmental & Ergonomic Factors](#14-environmental--ergonomic-factors)
15. [Recommended Tracking Schedule for a Life Agent](#15-recommended-tracking-schedule-for-a-life-agent)
16. [Implementation Considerations](#16-implementation-considerations)
17. [References](#17-references)

---

## 1. The Case for Proactive Health Monitoring

The leading causes of death globally are noncommunicable diseases (NCDs): cardiovascular disease, cancer, chronic respiratory disease, and diabetes. The WHO estimates that **physical inactivity alone will cost public health systems US$300 billion between 2020-2030** (WHO, 2024). The U.S. Surgeon General has declared loneliness and social isolation a public health epidemic, with effects comparable to smoking 15 cigarettes per day (Holt-Lunstad et al., 2010).

The critical insight: **most of these risks are modifiable through behavior change, and the failure mode is not lack of knowledge but lack of consistent follow-through.** People know they should exercise, sleep well, see a dentist, and call their mother. They don't do it. Not because they're lazy, but because:

1. **No one is tracking it** — there's no system watching whether you actually went to the gym or just thought about it
2. **No one is nudging at the right moment** — generic reminders are ignored; contextually aware nudges work (ProMemAssist: 2.6× engagement with 60% fewer messages)
3. **Consequences are delayed** — the feedback loop between "skipping a workout" and "cardiovascular disease" spans decades
4. **Life gets in the way** — acute demands always crowd out preventive behaviors

A life agent is uniquely positioned to close these gaps because it:
- **Persists** — it doesn't forget that you haven't worked out in 9 days
- **Correlates** — it can connect your poor sleep to your skipped workout to your irritable mood
- **Nudges contextually** — it knows your schedule, your patterns, and your receptivity
- **Bridges the immediate and the longitudinal** — it can frame "go for a walk" in terms of the 27% mortality reduction you're leaving on the table

### The Numbers That Should Alarm Everyone

| Risk Factor | Impact | Source |
|---|---|---|
| Physical inactivity | 20-30% increased risk of death | WHO, 2024 |
| Social isolation | 29% increased risk of premature mortality | Holt-Lunstad et al., 2015 |
| Poor sleep (<7 hours) | 13% higher all-cause mortality | Cappuccio et al., 2010 |
| Medication non-adherence | ~125,000 deaths/year in U.S. | Viswanathan et al., 2012 (AHRQ) |
| Never seeing a dentist | 2× risk of oral cancer undetected; linked to cardiovascular disease | ADA; Lockhart et al., 2012 |
| Chronic loneliness | 26% increased mortality (comparable to 15 cigarettes/day) | Holt-Lunstad et al., 2010 |
| Sedentary >8 hours/day | 59% increased risk of all-cause death (when combined with low activity) | Ekelund et al., 2016 (Lancet) |

---

## 2. Physical Activity

### 2.1 What the Evidence Says

Physical activity is the single most impactful modifiable health behavior. The evidence is overwhelming and consistent across every major health organization:

**Aerobic exercise (cardio):**
- 150 minutes/week of moderate-intensity or 75 minutes/week of vigorous-intensity aerobic activity (WHO/CDC guideline)
- Reduces cardiovascular disease risk, the #1 killer globally
- **An estimated 110,000 deaths per year could be prevented** in U.S. adults 40+ if they increased moderate-to-vigorous activity by even 10 minutes/day (JAMA Internal Medicine, 2022)
- Reduces risk of at least 8 types of cancer (CDC, 2024)
- Reduces risk of type 2 diabetes and metabolic syndrome
- 6,000-10,000 steps/day reduces risk of premature death (Lancet Public Health, 2022): 8,000-10,000 for adults <60, 6,000-8,000 for adults 60+

**Resistance training (lifting):**
- **Any resistance training reduces all-cause mortality by 15%**, cardiovascular mortality by 19%, cancer mortality by 14% (Shailendra et al., 2022 — meta-analysis of 10 studies)
- **Maximum risk reduction of 27% at ~60 minutes/week** of resistance training; benefits diminish at higher volumes
- Critical for sarcopenia prevention (age-related muscle loss): adults lose 3-8% of muscle mass per decade after 30
- Maintains bone density, preventing osteoporosis and fractures
- CDC guideline: muscle-strengthening activities involving all major muscle groups on 2+ days/week

**Combined effect:**
- Adults meeting both aerobic AND strength guidelines were **~50% less likely to die from flu/pneumonia** (British Journal of Sports Medicine, 2023)
- Physical activity is associated with reduced COVID-19 hospitalizations and deaths (CDC, 2023)
- Regular activity reduces depression risk by 20-30% (Schuch et al., 2018 — meta-analysis)

### 2.2 What a Life Agent Should Track

| Metric | Target | Frequency | Data Source |
|---|---|---|---|
| Aerobic minutes/week | ≥150 min moderate or ≥75 min vigorous | Weekly | Apple Health / Google Fit / self-report |
| Resistance training sessions | ≥2x/week | Weekly | Self-report or gym check-in |
| Daily steps | 8,000-10,000 (age <60), 6,000-8,000 (60+) | Daily | Wearable / phone |
| Consecutive sedentary hours | Alert if >2 hours without movement | Real-time | Wearable |
| Streak tracking | Days since last workout | Daily | Derived |

### 2.3 Agent Behavior

- Track a rolling 7-day activity window rather than daily targets (evidence shows weekly accumulation matters more than daily consistency)
- If no exercise for 3+ days, escalate nudge urgency gradually — not nagging, but "you usually feel better after a run"
- Connect activity to outcomes the user cares about: mood, sleep quality, energy
- Respect recovery days — don't push someone who reports soreness or illness
- Use the audio lifelogging pipeline to detect mentions of planning workouts ("I should go to the gym tomorrow") and follow up

---

## 3. Sleep

### 3.1 What the Evidence Says

Sleep is the second pillar. Adults need 7-9 hours per night (CDC, National Sleep Foundation).

**Health benefits of adequate sleep (CDC, 2024):**
- Reduced risk of type 2 diabetes, heart disease, high blood pressure, stroke
- Better immune function (get sick less often)
- Healthy weight maintenance
- Improved attention, memory, and daily performance
- Reduced risk of motor vehicle crashes
- Lower stress and better mood

**Consequences of chronic sleep debt:**
- <6 hours/night associated with 12% higher mortality risk (Cappuccio et al., 2010 — meta-analysis of 1.3M people)
- Chronic sleep deprivation impairs insulin sensitivity, appetite regulation, immune function
- Single night of poor sleep measurably impairs cognitive performance comparable to 0.10% BAC (Williamson & Feyer, 2000)
- 1 in 3 U.S. adults don't get enough sleep (CDC MMWR)

**Sleep quality markers:**
- Consistent sleep/wake times (even on weekends)
- Sleep latency <20 minutes
- <1 extended awakening per night
- Feeling refreshed upon waking

### 3.2 What a Life Agent Should Track

| Metric | Target | Frequency | Data Source |
|---|---|---|---|
| Sleep duration | 7-9 hours | Daily | Wearable / phone screen time |
| Sleep consistency | <60 min variation in bed/wake times | Weekly | Derived |
| Late-night screen time | Alert if screen use >30 min before target bedtime | Daily | Phone usage data |
| Caffeine/alcohol timing | Track if consuming caffeine after 2pm or alcohol near bedtime | Daily | Self-report / audio detection |

### 3.3 Agent Behavior

- Gentle evening wind-down reminder ("It's 10pm — you usually feel better with 7+ hours")
- Don't wake users up or disturb during sleep hours (respect quiet hours: a life agent that buzzes you at 2am is worse than no agent)
- If detecting a pattern of <6 hours for 3+ consecutive nights, escalate concern
- Connect sleep data to mood and productivity patterns: "Your reported energy levels are 30% lower on days after <6 hours of sleep"

---

## 4. Nutrition & Hydration

### 4.1 What the Evidence Says

Diet quality is among the top 3 modifiable risk factors for premature death globally (GBD Study, 2019). The evidence converges on patterns, not specific foods:

**Dietary patterns with strongest evidence:**
- **Mediterranean diet**: 25% reduction in cardiovascular events (PREDIMED trial, N=7,447, NEJM 2018)
- **DASH diet**: clinically significant blood pressure reduction (8-14 mmHg systolic)
- **Common elements**: high vegetables/fruits, whole grains, lean protein, healthy fats (olive oil, nuts, fish), limited processed food, limited added sugar

**Hydration:**
- Adequate water intake: ~3.7L/day for men, ~2.7L/day for women (including food sources) (National Academies, 2004)
- Chronic mild dehydration associated with kidney stones, UTIs, impaired cognition, and constipation
- Most people don't drink enough water — not dangerously, but sub-optimally

**What actually kills people (dietary risk factors, GBD 2019):**
1. High sodium intake
2. Low whole grains intake
3. Low fruit intake
4. Low nuts and seeds
5. Low vegetables
6. Low omega-3 fatty acids

### 4.2 What a Life Agent Should Track

| Metric | Target | Frequency | Data Source |
|---|---|---|---|
| Meal regularity | 2-3 meals/day, roughly consistent timing | Daily | Self-report / audio mentions |
| Fruit/vegetable intake | ≥5 servings/day | Daily | Self-report |
| Water intake | ≥8 glasses/day (simplified target) | Daily | Self-report |
| Processed food frequency | Awareness, not judgment | Weekly | Self-report |
| Days since cooking a meal | Encourage home cooking | Weekly | Audio / self-report |

### 4.3 Agent Behavior

- Don't count calories or moralize about food choices — this is a wellness agent, not a diet coach
- Track patterns, not individual meals: "You've eaten out for every meal this week — do you want me to find a simple recipe?"
- Detect via audio lifelogging: "I'm starving, I haven't eaten all day" → gentle check-in
- Hydration reminders only if user opts in (hydration nagging is the archetypal example of annoying proactivity)

---

## 5. Preventive Medical Care

### 5.1 What the Evidence Says

The U.S. Preventive Services Task Force (USPSTF) maintains evidence-graded recommendations. Grade A/B recommendations represent services with high or moderate net benefit. Key screenings for adults:

**Universal screenings (all adults):**

| Screening | Population | Frequency | USPSTF Grade |
|---|---|---|---|
| Blood pressure | Adults ≥18 | Annual (or per provider) | A |
| Depression screening | All adults, including pregnant/postpartum | At least once; periodic | B |
| Anxiety screening | Adults ≤64 | At least once | B |
| Cholesterol/lipids | Varies by risk; typically ≥35 (men), ≥45 (women) | Every 4-6 years if normal | B |
| Type 2 diabetes | Adults 35-70 with overweight/obesity | Every 3 years | B |
| Hepatitis C | Adults 18-79 | At least once | B |
| HIV | Adults 15-65 | At least once; periodic if at risk | A |
| Colorectal cancer | Adults 45-75 | Every 1-10 years depending on method | A/B |
| Breast cancer (mammogram) | Women 40-74 | Every 2 years | B |
| Cervical cancer (Pap/HPV) | Women 21-65 | Every 3-5 years | A |
| Lung cancer (LDCT) | Adults 50-80, 20+ pack-year smoking history | Annual | B |

**The annual physical exam:**
- Not a USPSTF recommendation per se (they focus on specific screenings), but serves as the vehicle for delivering most preventive care
- At minimum: adults should have a primary care provider and see them at least annually
- Visit includes vitals, medication review, screening schedule, lifestyle counseling

### 5.2 What a Life Agent Should Track

| Metric | Target | Frequency | Data Source |
|---|---|---|---|
| Last PCP visit | Within last 12 months | Annual check | Self-report |
| Age-appropriate screenings completed | Per USPSTF schedule | Per schedule | Self-report / calendar |
| Annual flu/COVID vaccination | Current season | Annual (autumn) | Self-report |

### 5.3 Agent Behavior

- Maintain a personalized preventive care calendar based on age, sex, and risk factors
- Prompt gently: "It's been 14 months since your last checkup — want me to remind you to schedule one?"
- Don't play doctor — never interpret symptoms or suggest diagnoses
- If the user mentions health symptoms in conversation (audio pipeline), the agent should note but NOT diagnose: "You've mentioned headaches three times this week — might be worth bringing up at your next appointment"

---

## 6. Dental Health

### 6.1 What the Evidence Says

Oral health is systematically undervalued yet has outsized effects:

- **Periodontal (gum) disease is linked to cardiovascular disease**: bacteria from infected gums enter the bloodstream, contributing to arterial plaque (Lockhart et al., 2012 — AHA Scientific Statement)
- Oral infections increase risk of endocarditis
- Gum disease is bidirectionally linked with diabetes: each worsens the other (Taylor & Borgnakke, 2008)
- Undetected oral cancers have a 5-year survival rate of ~66%, dramatically worse when caught late
- **42% of U.S. adults have some form of periodontal disease** (CDC NHANES)
- Preventive dental visits catch early-stage caries, oral cancer, and gum disease before they become costly and painful

**Recommended schedule:**
- Professional cleaning and exam: every 6 months (standard), or every 3-4 months if history of gum disease
- Daily brushing (2x/day) and flossing (1x/day) — the most impactful home care

### 6.2 What a Life Agent Should Track

| Metric | Target | Frequency | Data Source |
|---|---|---|---|
| Last dental visit | Within last 6 months | Every 6 months | Self-report / calendar |
| Next dental appointment scheduled | Always have next one booked | After each visit | Calendar |

### 6.3 Agent Behavior

- After a dental visit: "Do you want to schedule your next cleaning in 6 months?"
- If >6 months since last visit: gentle nudge, escalating only once
- Don't track brushing/flossing — that level of monitoring crosses the creepy line

---

## 7. Vision Care

### 7.1 What the Evidence Says

- **Uncorrected refractive errors** are the #1 cause of preventable visual impairment globally (WHO)
- Comprehensive eye exams detect not just vision problems but also glaucoma, macular degeneration, diabetic retinopathy, and even systemic diseases (hypertension, diabetes can be detected via retinal examination)
- **Glaucoma is called the "silent thief of sight"** — no symptoms until irreversible damage is done
- Digital eye strain (computer vision syndrome) affects up to 90% of computer workers (AOA)

**Recommended schedule (American Optometric Association):**
- Adults 18-39: comprehensive exam every 2 years (or annually if at risk)
- Adults 40-64: every 1-2 years
- Adults 65+: annually
- At-risk populations (diabetes, family history of glaucoma, high myopia): annually regardless of age

### 7.2 What a Life Agent Should Track

| Metric | Target | Frequency | Data Source |
|---|---|---|---|
| Last eye exam | Per age-appropriate schedule | Every 1-2 years | Self-report / calendar |
| Screen time (continuous) | 20-20-20 rule: every 20 min, look 20 feet away, for 20 seconds | Real-time (optional) | Computer/phone usage |

### 7.3 Agent Behavior

- Track time since last eye exam; prompt when overdue per age category
- For heavy computer users: optional 20-20-20 rule reminders (disabled by default — could be extremely annoying)

---

## 8. Medication Adherence

### 8.1 What the Evidence Says

This is one of the highest-impact, most mechanically simple areas for an agent to help:

- **~50% of medications for chronic diseases are not taken as prescribed** (WHO, 2003 — this number has not meaningfully improved in 20+ years)
- **Non-adherence causes approximately 125,000 deaths and $100-300 billion in avoidable healthcare costs annually in the U.S.** (Viswanathan et al., 2012, AHRQ; Osterberg & Blaschke, 2005, NEJM)
- For hypertension: non-adherent patients have 80% higher risk of stroke (Herttua et al., 2013)
- For statins: non-adherence increases cardiovascular event risk by 25% (Chowdhury et al., 2013)
- For diabetes: non-adherence doubles hospitalization risk (Ho et al., 2006)

**Why people don't take their medications:**
1. **Forgetting** (most common — pure behavioral, not intentional)
2. Side effects causing intentional discontinuation
3. Cost barriers
4. Feeling better and assuming they don't need it anymore
5. Complex regimens (multiple pills at different times)

**What works (meta-analyses):**
- Simple reminders (SMS, app notifications) improve adherence by 10-20% (Thakkar et al., 2016 — Cochrane)
- Simplifying regimens (once-daily vs. multiple times) dramatically helps
- Social/caregiver support improves adherence
- The key is **consistency, not complexity** — a daily ping at the right time beats sophisticated intervention

### 8.2 What a Life Agent Should Track

| Metric | Target | Frequency | Data Source |
|---|---|---|---|
| Medication taken today | Y/N per medication | Daily | Self-report confirmation |
| Refill date approaching | 7-day advance warning | Per prescription | Calendar / user-entered |
| Consecutive missed days | Alert if 2+ missed | Daily | Derived |

### 8.3 Agent Behavior

- Daily reminder at user-configured time: simple, consistent, not patronizing
- Track streaks and celebrate consistency (positive reinforcement)
- If medications are missed 2+ days: escalate urgency, but gently: "You haven't confirmed your meds in 2 days — everything OK?"
- Never adjust, recommend, or comment on specific medications (liability boundary)
- Remind about refills before they run out
- If audio pipeline detects "I ran out of my prescription" or "I forgot my pills" — proactive check-in

---

## 9. Mental Health

### 9.1 What the Evidence Says

Mental health is not separate from physical health — it IS health.

**Prevalence:**
- 1 in 5 U.S. adults live with a mental illness (NAMI, 2024)
- Depression is the leading cause of disability worldwide (WHO)
- Anxiety disorders affect 31% of U.S. adults at some point (NIMH)
- USPSTF now recommends universal depression screening for all adults (Grade B, 2023) and anxiety screening for adults ≤64 (Grade B, 2023)

**Interventions with strong evidence:**
- **Physical activity**: 20-30% depression risk reduction; comparable to antidepressants for mild-moderate depression (Schuch et al., 2018)
- **Social connection**: loneliness doubles depression risk (Mann et al., 2022)
- **Sleep**: bidirectional — poor sleep causes anxiety/depression; treatment of insomnia reduces depression severity (JAMA Psychiatry, 2019)
- **Therapy/counseling**: CBT is highly effective for depression and anxiety (Hofmann et al., 2012)
- **Sunlight exposure**: 10-30 min/day of natural light helps regulate circadian rhythm and mood, especially for seasonal patterns
- **Mindfulness/meditation**: 8-week mindfulness programs reduce anxiety and depression symptoms (Goyal et al., 2014 — JAMA Internal Medicine meta-analysis)

**Therapy scheduling reality:**
- Average time from deciding to seek therapy to first appointment: 25+ days
- The agent's value: prompting the user to make the call, helping them follow through on scheduling, not letting it fall off their radar

### 9.2 What a Life Agent Should Track

| Metric | Target | Frequency | Data Source |
|---|---|---|---|
| Mood self-report | Subjective 1-5 scale (optional) | Daily or weekly | Self-report |
| Therapy appointments | Attending regularly if in therapy | Per schedule | Calendar |
| Days without leaving the house | Flag if 3+ consecutive days | Daily | Location / self-report |
| Patterns of distress language | Listening for persistent negative themes | Passive (audio) | Audio pipeline sentiment |

### 9.3 Agent Behavior

- **Never diagnose. Never play therapist.** The agent is a health-promoting companion, not a clinician.
- Track upstream indicators: exercise frequency, sleep quality, social contact, time outdoors
- If multiple upstream indicators deteriorate simultaneously (no exercise + poor sleep + no social contact for 1+ week): gentle check-in, not clinical language
- "You've been staying in a lot this week and sleeping less — anything going on? No pressure, just checking in."
- If user mentions feeling overwhelmed or anxious via audio: note the pattern, don't lecture
- Maintain a "last therapy visit" tracker if applicable, with the same gentle nudge cadence as medical appointments
- **Critical boundary**: if the user expresses suicidal ideation, the agent should provide crisis resources (988 Suicide & Crisis Lifeline) immediately and clearly, not attempt to counsel

---

## 10. Social Connection

### 10.1 What the Evidence Says

The U.S. Surgeon General declared loneliness and social isolation a public health epidemic in 2023. The data is stark:

**Health impacts of social isolation (Surgeon General's Advisory, 2023; CDC, 2024):**
- **29% increased risk of premature mortality** (comparable to smoking 15 cigarettes/day) (Holt-Lunstad et al., 2015)
- **29% increased risk of heart disease, 32% increased risk of stroke** (Valtorta et al., 2016)
- **~50% increased risk of dementia** in chronically lonely older adults (Lazzari & Rabottini, 2021)
- **2× risk of depression** in frequently lonely adults (Mann et al., 2022)
- Social isolation increases inflammation to the same degree as physical inactivity (Yang et al., 2016)
- People with strong community belonging are **2.6× more likely to report excellent health** (MHMC, 2018)
- Smaller social networks are associated with increased type 2 diabetes risk and diabetic complications (Brinkhues et al., 2017, 2018)

**Prevalence (CDC, 2024):**
- 1 in 3 U.S. adults report feeling lonely
- 1 in 4 U.S. adults report lacking social and emotional support
- Social connection has been declining steadily since 2003 (Surgeon General's data)

**What improves social connection:**
- Regular contact with friends (quality > quantity — 3-5 close relationships is sufficient)
- Family check-ins (even brief: a 5-minute phone call counts)
- Community participation (volunteering, clubs, religious services, sports leagues)
- Shared meals (the social effect of eating together is distinct from nutrition)

### 10.2 What a Life Agent Should Track

| Metric | Target | Frequency | Data Source |
|---|---|---|---|
| Social interactions this week | ≥2-3 meaningful in-person or video interactions | Weekly | Audio pipeline / calendar / self-report |
| Days since calling/visiting family | User-defined cadence (e.g., weekly call to parents) | Per cadence | Call log / audio / self-report |
| Days since seeing friends in person | Flag if >2 weeks with no social activity | Bi-weekly | Audio pipeline / calendar |
| Events/plans on calendar this week | At least 1 social event | Weekly | Calendar |

### 10.3 Agent Behavior

- This is the domain where the audio lifelogging pipeline adds the most value: the agent can passively detect social interactions from conversation transcripts without requiring manual logging
- Speaker identification reveals WHO the user is talking to and how often
- Frequency tracking: "You haven't caught up with [friend] in a while — want me to suggest reaching out?"
- Don't be creepy about it: track patterns, not individual conversations
- Family cadence: user configures "I want to call my parents every Sunday" → agent reminds if it doesn't detect a call by Sunday evening
- Loneliness detection is subtle — look for the compound signal: staying home + no social audio + declining mood self-reports + lower activity
- **The agent should not fabricate social interaction** (e.g., chatbot companionship is not a substitute for human connection)

---

## 11. Substance Use & Harm Reduction

### 11.1 What the Evidence Says

**Alcohol:**
- CDC: ≤2 drinks/day for men, ≤1 for women (moderate drinking guidelines)
- Heavy drinking: ≥15 drinks/week (men), ≥8/week (women)
- Even moderate alcohol consumption now under scrutiny: Lancet 2018 meta-analysis concluded "the safest level of drinking is none"
- USPSTF recommends screening for unhealthy alcohol use in all adults ≥18 (Grade B)
- Alcohol involved in 29% of all traffic crash fatalities (NHTSA)

**Tobacco:**
- Smoking is still the #1 preventable cause of death in the U.S. (CDC: 480,000 deaths/year)
- USPSTF: ask all adults about tobacco use, advise cessation, provide interventions (Grade A)
- Quitting at any age provides benefit; quitting before 40 reduces smoking-related death risk by ~90%

**Cannabis, recreational drugs:**
- As legalization spreads, monitoring frequency and impact (on productivity, mood, sleep) becomes relevant
- Agent role: pattern awareness, not moral judgment

### 11.2 Agent Behavior

- Track only if user opts in — substance use is a highly sensitive domain
- Pattern detection, not judgment: "You've mentioned drinking on 5 of the last 7 evenings — just making sure you're aware of the pattern"
- Never moralize or lecture
- If audio pipeline detects concerning patterns (daily heavy drinking mentions, substance dependence language), include in daily briefing as a neutral observation

---

## 12. Stress & Recovery

### 12.1 What the Evidence Says

Chronic stress is an independent risk factor for:
- Cardiovascular disease (Rosengren et al., 2004 — INTERHEART study: psychosocial stress accounts for ~33% of attributable MI risk)
- Immune suppression (Cohen et al., 2007)
- Accelerated aging at the cellular level (Epel et al., 2004 — telomere shortening)
- Cognitive decline and dementia risk (Johansson et al., 2010)

**Effective stress mitigation:**
- Physical activity (most powerful stress buffer)
- Sleep quality
- Social support (Cohen & Wills, 1985 — stress-buffering model)
- Nature exposure: 20+ minutes in nature significantly reduces cortisol (Hunter et al., 2019)
- Breaks from work: the Pomodoro technique and micro-breaks reduce cognitive fatigue
- **Vacation/time off**: people who take <10 vacation days/year have 30% higher risk of cardiac events vs. those taking ≥21 days (Framingham Heart Study subsidiary data)

### 12.2 What a Life Agent Should Track

| Metric | Target | Frequency | Data Source |
|---|---|---|---|
| Work hours/day | Awareness; flag if consistently >10 hours | Daily | Calendar / screen time |
| Days since a day off | Flag if >14 consecutive work days | Daily | Calendar |
| Time outdoors | ≥20 minutes/day | Daily | Wearable / self-report |
| Vacation days used this year | Encourage using allocated days | Quarterly | Calendar |

### 12.3 Agent Behavior

- Track work-life patterns, not just work patterns
- If user is consistently working >10-hour days for 2+ weeks: "You've been working long hours for 2 weeks straight. Your sleep has also dropped. Might be worth blocking a recovery day."
- Encourage nature exposure: "The weather is nice today — even a 20-minute walk helps reset your stress levels"
- Vacation nudging: "You have 12 unused vacation days and it's September — want to plan something before year-end?"

---

## 13. Cognitive Health & Lifelong Learning

### 13.1 What the Evidence Says

Cognitive decline is not inevitable. Modifiable risk factors account for **~40% of dementia cases** (Livingston et al., 2020 — Lancet Commission on Dementia Prevention):

**12 modifiable risk factors for dementia (Lancet 2020):**
1. Less education (early life)
2. Hearing loss (midlife)
3. Traumatic brain injury (midlife)
4. Hypertension (midlife)
5. Excessive alcohol (midlife)
6. Obesity (midlife)
7. Smoking (later life)
8. Depression (later life)
9. Social isolation (later life)
10. Physical inactivity (later life)
11. Air pollution (later life)
12. Diabetes (later life)

**What builds cognitive reserve:**
- Learning new skills (especially languages, musical instruments)
- Reading regularly
- Social engagement (conversation IS cognitive exercise)
- Physical activity (especially aerobic — increases BDNF and hippocampal volume)
- Treating hearing loss (untreated hearing loss is the #1 modifiable risk factor for dementia)

### 13.2 What a Life Agent Should Track

| Metric | Target | Frequency | Data Source |
|---|---|---|---|
| Reading / learning activity | At least some intellectual engagement weekly | Weekly | Self-report / audio |
| Hearing concerns | Flag if user asks people to repeat frequently | Passive | Audio pipeline |
| Novel experiences | Variety in routine (new places, people, activities) | Monthly | Calendar / audio |

### 13.3 Agent Behavior

- Suggest variety: "You've been in a routine groove — tried anything new lately?"
- The audio pipeline can detect hearing indicators: frequently asking others to repeat, high-volume playback
- Track and surface what the user is learning: "You mentioned wanting to learn Spanish two months ago — did you ever start?"
- This is a domain where the agent should gently inspire, not prescribe

---

## 14. Environmental & Ergonomic Factors

### 14.1 What the Evidence Says

Environment shapes health invisibly:

- **Indoor air quality**: WHO estimates 3.2 million deaths/year from household air pollution
- **Ergonomics**: musculoskeletal disorders account for 33% of workplace injuries (BLS); poor posture during desk work contributes to chronic back pain
- **Light exposure**: insufficient natural light disrupts circadian rhythms, contributing to poor sleep and mood disorders
- **Noise**: chronic noise exposure >70dB linked to cardiovascular disease (WHO Environmental Noise Guidelines)

### 14.2 Agent Behavior

- For desk workers: suggest movement breaks and ergonomic awareness
- Track if the user mentions back pain, headaches, eye strain: "You've mentioned back pain a few times — might be worth reviewing your desk setup"
- Encourage getting natural light in the morning (circadian alignment)

---

## 15. Recommended Tracking Schedule for a Life Agent

The complete wellness monitoring schedule, organized by cadence:

### Real-Time / Daily
- Sleep duration and consistency
- Medication reminders (if applicable)
- Step count / activity tracking
- Mood self-report (optional, user-initiated)

### Weekly
- Aerobic exercise minutes (rolling 7 days)
- Resistance training sessions (rolling 7 days)
- Social interactions count
- Days since calling family (if user configured)
- Work-life balance patterns

### Monthly
- Days since last social outing with friends
- Novel experiences / routine variety
- Stress pattern review

### Quarterly / Seasonal
- Vaccination reminders (flu season)
- Vacation days used / remaining
- Substance use pattern review (if opted in)

### Semi-Annual
- Dental cleaning reminder
- Medication refill review

### Annual
- Primary care visit
- Age-appropriate cancer screenings
- Eye exam
- Review of all preventive care calendar items

### Triggered (Event-Based)
- Audio pipeline detects spoken commitment ("I need to schedule a dentist appointment") → track follow-through
- Audio pipeline detects health complaint ("I've had this headache for a week") → log and suggest mentioning to provider
- Multiple correlated deteriorations detected (poor sleep + no exercise + no social contact) → elevated check-in
- Spoken distress or crisis language → immediate crisis resource delivery

---

## 16. Implementation Considerations

### 16.1 The Trust Paradox

From our existing research (§12 of design.md — Choose Your Agent study, N=243): users prefer the advisor mode (44%) even though the delegate mode produces better outcomes. The agent must:

- **Start as an observer** — collect data for 2+ weeks before suggesting behavioral changes
- **Prove value on easy wins first** — dental appointment reminders and medication pings before lifestyle coaching
- **Earn the right to nudge about sensitive domains** — never lead with "you should exercise more" on day 1
- **Always provide an off switch** — per-domain mute controls

### 16.2 The Notification Fatigue Problem

From our proactivity research (BAO, ProPerSim, ProMemAssist):
- Without regulation, agents demand attention 91% of the time (BAO: UR 0.9064)
- ProMemAssist achieved 2.6× engagement with 60% fewer notifications
- Optimal rate self-converges to ~6 notifications/hour (ProPerSim)
- **False-positive asymmetry**: missing a chance to help < interrupting unnecessarily

For wellness monitoring specifically:
- **Batch low-urgency items into the daily briefing** — don't send individual notifications for "you should drink water" and "time for your walk" and "call your mom"
- **Never nag** — a notification that was dismissed should not repeat for at least 24 hours
- **Use the daily briefing as the primary health surface**, not point notifications
- Reserve real-time alerts for: medication reminders, crisis detection, overdue deadlines

### 16.3 Privacy & Sensitivity

Wellness monitoring touches the most personal aspects of someone's life:
- **Never share health data** with any external system without explicit consent
- **Substance use tracking only with opt-in** 
- **Mood/mental health data should never be used to make autonomous decisions** — always surface to the user
- **Audio-detected health mentions should be logged privately**, not announced ("I noticed you mentioned a health concern — it's in your private notes if you want to bring it up with your doctor")
- **All wellness data stored locally** (SQLite on the user's infrastructure, not third-party clouds)

### 16.4 What NOT to Track

Intentional omissions — domains where agent monitoring would be counterproductive or harmful:

- **Weight/BMI** — body weight monitoring is counterproductive for many people and can trigger disordered eating
- **Calorie counting** — same issue; focus on patterns, not numbers
- **Brushing teeth** — crosses the dignity line
- **Sexual health** — too personal for passive monitoring
- **Religious/spiritual practice** — not the agent's domain
- **Specific food choices** — "you ate pizza" is surveillance, not wellness
- **Bathroom frequency** — obvious

The guiding principle: **track upstream behaviors and structural appointments, not intimate bodily details.**

### 16.5 Audio Pipeline Integration

The audio lifelogging pipeline (§8.4 of design.md) is the single most valuable data source for wellness monitoring:

| Audio Signal | Wellness Domain | Example |
|---|---|---|
| Social conversation detected | Social connection | User had 45-minute conversation with friend |
| Spoken commitment about health | Preventive care | "I need to schedule my physical" |
| Mentions of fatigue/exhaustion | Sleep / stress | "I'm so tired, I barely slept" |
| Repeated health complaint | Medical follow-up | "My knee has been killing me" |
| Distress language | Mental health | Patterns of negative self-talk |
| No social conversations in N days | Social isolation | Silence from the social detector |
| Mentions of medication | Medication adherence | "I forgot my pills again" |

This is the "ambient awareness" layer that makes the agent genuinely useful — it acts on signals the user doesn't have to manually enter.

---

## 17. References

### Physical Activity
- WHO. Physical activity fact sheet. June 2024.
- CDC. Benefits of Physical Activity. December 2024.
- Shailendra P, Baldock KL, Li LSK, Bennie JA, Boyle T. Resistance Training and Mortality Risk: A Systematic Review and Meta-Analysis. *Am J Prev Med*. 2022;63(2):277-285. PMID: 35599175.
- Strain T, et al. National, regional, and global trends in insufficient physical activity. *Lancet Global Health*. 2024.
- Ekelund U, et al. Does physical activity attenuate, or even eliminate, the detrimental association of sitting time with mortality? *Lancet*. 2016;388(10051):1302-1310.
- Schuch FB, et al. Physical Activity and Incident Depression: A Meta-Analysis. *Am J Psychiatry*. 2018;175(7):631-648.

### Sleep
- CDC. About Sleep. May 2024.
- Cappuccio FP, et al. Sleep Duration and All-Cause Mortality: A Systematic Review and Meta-Analysis. *Sleep*. 2010;33(5):585-592.
- Williamson AM, Feyer AM. Moderate sleep deprivation produces impairments in cognitive and motor performance equivalent to legally prescribed levels of alcohol intoxication. *Occup Environ Med*. 2000;57(10):649-655.

### Social Connection
- U.S. Surgeon General. Our Epidemic of Loneliness and Isolation: The U.S. Surgeon General's Advisory on the Healing Effects of Social Connection and Community. 2023.
- CDC. Health Effects of Social Isolation and Loneliness. May 2024.
- Holt-Lunstad J, Smith TB, Baker M, Harris T, Stephenson D. Loneliness and Social Isolation as Risk Factors for Mortality: A Meta-Analytic Review. *Perspect Psychol Sci*. 2015;10(2):227-237.
- Holt-Lunstad J, Smith TB, Layton JB. Social Relationships and Mortality Risk: A Meta-analytic Review. *PLoS Med*. 2010;7(7):e1000316.
- Valtorta NK, et al. Loneliness and social isolation as risk factors for coronary heart disease and stroke. *Heart*. 2016;102(13):1009-1016.
- Mann F, et al. Loneliness and the onset of new mental health problems in the general population. *Soc Psychiatry Psychiatr Epidemiol*. 2022;57:2161-2178.
- Yang YC, et al. Social relationships and physiological determinants of longevity across the human life span. *PNAS*. 2016;113(3):578-583.
- Lazzari C, Rabottini M. Social isolation and loneliness increase dementia risk. *Clin Neuropsychiatry*. 2021;18(4):215-223.

### Preventive Care
- U.S. Preventive Services Task Force. A and B Recommendations. 2025.
- Lockhart PB, et al. Periodontal Disease and Atherosclerotic Vascular Disease: Does the Evidence Support an Independent Association? AHA Scientific Statement. *Circulation*. 2012.

### Medication Adherence
- WHO. Adherence to Long-Term Therapies: Evidence for Action. 2003.
- Osterberg L, Blaschke T. Adherence to Medication. *NEJM*. 2005;353(5):487-497.
- Viswanathan M, et al. Interventions to Improve Adherence to Self-administered Medications for Chronic Diseases in the United States. AHRQ. 2012.
- Thakkar J, et al. Mobile Telephone Text Messaging for Medication Adherence in Chronic Disease: A Meta-analysis. *JAMA Intern Med*. 2016;176(3):340-349.

### Mental Health
- NAMI. Mental Health By the Numbers. 2024.
- Hofmann SG, et al. The Efficacy of Cognitive Behavioral Therapy. *Cogn Ther Res*. 2012;36(5):427-440.
- Goyal M, et al. Meditation Programs for Psychological Stress and Well-being: A Systematic Review and Meta-analysis. *JAMA Intern Med*. 2014;174(3):357-368.

### Nutrition
- GBD 2017 Diet Collaborators. Health effects of dietary risks in 195 countries. *Lancet*. 2019;393(10184):1958-1972.
- Estruch R, et al. Primary Prevention of Cardiovascular Disease with a Mediterranean Diet Supplemented with Extra-Virgin Olive Oil or Nuts (PREDIMED). *NEJM*. 2018.

### Stress & Cognitive Health
- Rosengren A, et al. Association of psychosocial risk factors with risk of acute myocardial infarction (INTERHEART). *Lancet*. 2004;364(9438):953-962.
- Hunter MR, et al. Urban Nature Experiences Reduce Stress. *Front Psychol*. 2019;10:722.
- Livingston G, et al. Dementia prevention, intervention, and care: 2020 report of the Lancet Commission. *Lancet*. 2020;396(10248):413-446.
- Epel ES, et al. Accelerated telomere shortening in response to life stress. *PNAS*. 2004;101(49):17312-17315.

### Agent Design References (from this repository)
- ProMemAssist (UIST 2025): 2.6× engagement with 60% fewer messages
- BAO (CMU/Salesforce/MIT, 2026): Behavioral regularization reduces user burden from UR 0.9064 to 0.2148
- ProPerSim (ICLR 2026): Optimal notification rate ~6/hour; preference-aligned learning
- Choose Your Agent (2026, N=243): Users prefer advisor mode (44%) despite delegate mode producing better outcomes
