# Audio Lifelogging: Continuous Recording, Transcription & Memory Augmentation

> **Research Date:** July 2025
> **Scope:** Products, open-source tools, academic research, and technical feasibility of always-on audio recording from personal devices (phones, watches, wearable pendants) with live transcription, speaker attribution, and integration into personal knowledge bases.

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [The Vision & Who Is Doing This](#2-the-vision--who-is-doing-this)
3. [Products & Open-Source Tools](#3-products--open-source-tools)
4. [Real-World User Experiences](#4-real-world-user-experiences)
5. [Academic Research](#5-academic-research)
6. [Speaker Diarization & Attribution](#6-speaker-diarization--attribution)
7. [iOS / Apple Watch Constraints](#7-ios--apple-watch-constraints)
8. [Hardware & Form Factor Realities](#8-hardware--form-factor-realities)
9. [Architecture Patterns for Always-On Audio](#9-architecture-patterns-for-always-on-audio)
10. [Privacy & Legal Considerations](#10-privacy--legal-considerations)
11. [Integration with Personal Knowledge Bases](#11-integration-with-personal-knowledge-bases)
12. [Cost Analysis](#12-cost-analysis)
13. [Open Problems & Research Gaps](#13-open-problems--research-gaps)
14. [References](#14-references)

---

## 1. Executive Summary

**Yes, people are doing this today.** Continuous 24/7 audio recording with live transcription is an active area of both consumer products and academic research. The most notable practitioner is u/8ta4 on Reddit, who created the open-source tool `say` and has been recording and transcribing everything they say 24/7 since ~2023. Multiple commercial wearable pendants (Omi, Limitless, Plaud NotePin) have shipped hardware for this use case, and the academic literature on "audio lifelogging" and "memory augmentation" spans hundreds of papers.

**Key findings:**

| Dimension | Current State (2025) |
|---|---|
| **Best open-source tool** | `say` (github.com/8ta4/say) — macOS, uses Deepgram API, ~$1/day |
| **Best open-source wearable** | Omi (omi.me) — 7.8k GitHub stars, BLE pendant, MIT license |
| **Leading commercial product** | Limitless was #1 but acquired by Meta (2025); Omi filling the gap |
| **Transcription accuracy** | Deepgram nova-3 is the current gold standard for streaming ASR |
| **Speaker attribution** | ECAPA-TDNN embeddings (0.8% EER), Pyannote, Deepgram diarization |
| **iOS background recording** | Technically possible but unreliable — iOS kills background apps |
| **Apple Watch** | AVAudioSession available since watchOS 2.0 but battery makes 24/7 impossible |
| **Daily cost (cloud ASR)** | ~$1/day via Deepgram ($0.0043/min, VAD reduces actual minutes) |
| **User-reported cognitive load** | *Decreased* — "can speak freely without stressing about remembering" |
| **Biggest unsolved problem** | Noisy environments (shower, outdoors, crowds) destroy accuracy |

---

## 2. The Vision & Who Is Doing This

The core idea: wear a device that continuously records ambient audio, streams it for transcription, identifies who is speaking, and feeds structured transcripts into a personal knowledge base. The result is a "prosthetic memory" — a searchable, structured log of everything you've said and heard.

### Who wants this

From Reddit r/QuantifiedSelf (active community of ~5.9K weekly visitors):
- **ADHD management** — people who lose thoughts seconds after having them
- **Attorneys** — transcribing case discussions and depositions in real-time
- **Therapists** — building patient management systems from session recordings
- **Knowledge workers** — capturing meeting decisions, action items, ideas
- **People with memory conditions** — both clinical (early dementia) and subclinical ("I have a terrible memory")
- **Journal keepers** — automatic daily journaling without the discipline of writing
- **Dream journalers** — speaking immediately upon waking before memories fade

### Pioneer projects

**MyLifeBits** (Gordon Bell, Microsoft Research, ~2001–2007) was the seminal project. Bell wore a wearable microphone and camera to capture every conversation, document, photo, and interaction. The project demonstrated feasibility but was limited by 2000s-era hardware and software.

**Ego4D** (Meta/FAIR, 2022+) is the largest egocentric perception dataset, with 3,670 hours of daily-life activity video from 931 participants across 74 locations worldwide. It includes audio and is the standard benchmark for episodic memory QA.

**EgoLife** (Yang et al., CVPR 2025, cited by 63) extends this toward an "Egocentric Life Assistant" — AI-powered wearable glasses with audio transcripts, visual-audio narrations, and EgoRAG memory banks for retrieval.

---

## 3. Products & Open-Source Tools

### 3.1 `say` by u/8ta4 (Open Source)

| Attribute | Detail |
|---|---|
| **URL** | github.com/8ta4/say |
| **License** | Open source |
| **Stars** | 89 |
| **Platform** | macOS (Apple Silicon only) |
| **Language** | Clojure (84.7%), runs as Electron app |
| **ASR Provider** | Deepgram nova-3 (streaming) |
| **VAD** | Silero ONNX model (on-device, pre-filters silence) |
| **Install** | `brew install 8ta4/say/say` |
| **Last Release** | v0.9.3 (Feb 2025) |
| **Always-on** | Yes — runs as launchd service, survives ⌘Q, restarts on boot |
| **Audio stored?** | No — discarded after transcription |
| **Transcript format** | Plain text, `~/.local/share/say/YYYY/MM/DD.txt` |
| **Cost** | Free software + Deepgram API (~$1/day for heavy user) |

**Key design decisions:**
- Uses VAD to avoid sending silence to Deepgram (saves cost)
- "Hideaway" mode: uses built-in mic when on designated home WiFi, requires external mic otherwise (to ensure quality)
- Prevents Mac from sleeping while running (captures voice round-the-clock)
- New paragraph per transcription request, one sentence per line (optimized for Neovim navigation)
- Transcript files are read-only by design

### 3.2 Omi (Open Source Hardware + Software)

| Attribute | Detail |
|---|---|
| **URL** | omi.me / github.com/BasedHardware/omi |
| **License** | MIT |
| **Stars** | 7,800+ |
| **Contributors** | 175 |
| **Releases** | 330+ |
| **Hardware** | BLE pendant (nRF chip) + Glass (ESP32-S3) |
| **App** | Flutter (iOS + Android) |
| **Backend** | Python, FastAPI, Firebase, Pinecone, Redis |
| **ASR Providers** | Deepgram, Speechmatics, Soniox |
| **VAD** | Silero |
| **Price** | Dev kit ~$24, Glass dev kit available separately |
| **Key Feature** | Install once, speak, auto-transcribe, create summaries/tasks |

Omi is the most actively developed open-source wearable AI platform. The pendant connects via BLE to the phone app, which streams audio to cloud ASR. The backend processes transcripts into memories, action items, and summaries. Has an app store ecosystem with "thousands of apps" for productivity, relationships, health, etc.

**Architecture stack:** nRF/ESP32 firmware (C) → BLE → Flutter app (Dart) → FastAPI backend (Python) → Deepgram/Speechmatics → OpenAI-compatible LLMs → Pinecone vector store.

### 3.3 Limitless (Acquired by Meta, 2025)

Previously Rewind.ai, Limitless was the leading commercial product in this space. Founded by Dan Siroker, it offered:
- **Pendant hardware** — dedicated wearable for always-on recording
- **Desktop app** (Rewind) — captured screen + audio on Mac
- **Unlimited transcription plan** — cloud-based with local fallback

**Current status (2025):** Acquired by Meta as part of Meta's "personal superintelligence" vision for AI-enabled wearables. Existing pendant customers get free Unlimited Plan for at least one more year. New sales halted. Desktop Rewind app being sunset.

**Lessons from Rewind/Limitless:**
- Local (on-device) transcription quality was described as "garbage" by users who tested it
- The pivot from screen recording (Rewind) to wearable pendant validated that audio-first is the right approach
- Cloud transcription quality was much better but raised privacy concerns
- Meta acquisition signals that major tech companies see this as strategically important

### 3.4 Plaud NotePin

Wearable AI recorder/transcription device. Physical pin form factor. Advertises meeting recording, transcription, and summarization. Website was unavailable during research (redirected to tracking infrastructure), suggesting potential product instability.

### 3.5 Other Notable Products

| Product | Status | Notes |
|---|---|---|
| **Bee AI** | Unclear (reviews 404'd) | Was a conversation recording pendant |
| **Scople.ai** | Active (2025) | Tracks diet, environment, and "attentiveness of people around you" via wearable |
| **Apple Intelligence** | iOS 26+ (2025) | New transcription APIs that "blow past Whisper in speed tests" — but not always-on |

---

## 4. Real-World User Experiences

The richest source of real-world data on continuous audio recording comes from u/8ta4's extensive posts on r/QuantifiedSelf. Key findings:

### 4.1 Cognitive Load Paradox

**Expected:** Recording everything would increase anxiety about what's captured.
**Actual:** u/8ta4 reports the opposite — "baseline cognitive load actually went down — can speak freely without stressing about remembering."

This aligns with the "extended mind" thesis (Clark & Chalmers, 1998) and suggests that offloading memory to an external system is genuinely liberating rather than burdensome.

### 4.2 Use Cases Discovered Through Practice

After using `say` daily for 2+ years, u/8ta4 identified these emergent use cases:
1. **Memory augmentation** — core use case, works well
2. **Automatic to-do capture** — speak tasks as they occur to you
3. **Meeting transcription** — works even when always-on (no manual start/stop)
4. **Journaling** — frictionless, just talk
5. **Dream journaling** — speak immediately upon waking
6. **Rubber duck debugging** — explain code problems aloud, transcripts capture insights
7. **ADHD management** — capture fleeting thoughts before they disappear
8. **Mindfulness** — reviewing transcripts reveals thought patterns
9. **Therapy replacement** — speaking thoughts aloud has therapeutic value
10. **Digital clone preparation** — long-term, the transcripts could train a personal AI

### 4.3 Community Interest

- u/fef5002: "I've been searching for a system for this for years!"
- u/Majestic_Kangaroo319: Building a patient management system for therapists using always-on local transcription
- u/Objection_Irrelevant: Attorney wanting to transcribe case discussions
- r/QuantifiedSelf post "7.3 years of voice diary data" — someone has been doing voice-based lifelogging since ~2017

### 4.4 Problems Encountered

| Problem | Detail |
|---|---|
| **Noisy environments** | Shower, outdoors, crowds destroy accuracy. iPhone 15 Pro Max mic couldn't handle shower noise |
| **Wet microphones** | Shokz OpenRun is IP67 but wet mic produces gibberish. No solution found |
| **Noise cancellation SW** | Krisp software noise cancellation didn't help meaningfully |
| **Local transcription** | Rewind.ai's on-device Whisper was "garbage" quality |
| **Multi-speaker** | Without a close-proximity mic, other voices get mixed in. Dedicated mic per person is ideal but impractical |
| **Battery anxiety** | External Bluetooth mics add another device to charge |
| **Social stigma** | "Living alone could be an option... if you're really serious about clean recordings" (u/8ta4, half-joking) |
| **Hardware cost** | u/8ta4 has "already dropped a few grand" testing different mic setups |

---

## 5. Academic Research

### 5.1 Foundational Work

**"Memory augmentation through lifelogging: opportunities and challenges"** (Dingler et al., 2021)
- Comprehensive review of multimedia lifelogging including audio, video, photos, activities, bio-signals
- Key insight: continuous wearable capture has advanced steadily but retrieval/structuring remains the hard problem
- Cited by 15

**"Lifelog retrieval from daily digital data: narrative review"** (Ribeiro et al., 2022, JMIR mHealth)
- Reviews personal lifelogs and lifelogging approaches
- Discusses Itou's audio lifelogs using wearable microphones (Japanese researcher, early pioneer)
- References MyLifeBits project extensively
- Cited by 43

### 5.2 Memory Augmentation Systems

**"Encode-Store-Retrieve: Augmenting Human Memory through Language-Encoded Egocentric Perception"** (Shen et al., 2024, IEEE)
- Memory augmentation agent using both audio and visual streams
- Tested on QA-Ego4D episodic memory benchmark
- Open-ended episodic memory queries within a wearable headset
- Key contribution: language encoding of multimodal perceptions for later retrieval
- Cited by 14

**"VIMES: A wearable memory assistance system for automatic information retrieval"** (Bermejo et al., 2020, ACM Multimedia)
- Wearable system that captures audio + visual data for automatic memory retrieval
- Iterative development based on interview transcripts
- Cited by 21

**"Wearable Affective Memory Augmentation"** (Pierce & Mann, 2021, arXiv:2112.01584)
- Proposes prioritizing memories using affective state of people the user interacts with
- Constantly records and extracts affective information from audio, video, and transcriptions
- Steve Mann is the pioneer of wearable computing (has worn cameras/computers since the 1990s)
- Cited by 5

**"AI Interfaces for Augmenting Episodic Memory"** (Zulfikar, 2024, PhD thesis, ProQuest)
- Records audio lifelogs using wearable microphones
- Encodes raw speech transcriptions into memory
- Experiments with different browsing interfaces for lifelogs

**"Towards physiologically-driven human memory augmentation"** (Laporte, 2025, PhD thesis)
- Uses wearable physiological signals to identify high-relevance moments
- Memory cues triggered by physiological state (heart rate, GSR) rather than content analysis

### 5.3 Egocentric AI & Daily Logging

**"EgoLife: Towards Egocentric Life Assistant"** (Yang et al., CVPR 2025)
- Describes AI-powered wearable glasses for improving daily life efficiency
- Audio transcript annotations + visual-audio narrations
- Constructs EgoRAG memory banks for retrieval-augmented generation
- **Cited by 63** — the most-cited recent paper in this space

**"EgoLog: Ego-Centric Fine-Grained Daily Log with Ubiquitous Wearables"** (He et al., 2025, arXiv:2504.02624)
- Audio-IMU fusion for daily activity logging
- Uses Ego4D episodic memory benchmark
- LLM integration for scenario recognition with knowledge transfer back to edge device
- Key contribution: fusing audio with motion sensors for richer context

**"EgoTrigger: Toward Audio-Driven Image Capture for Human Memory Enhancement in All-Day Energy-Efficient Smart Glasses"** (Paruchuri et al., 2025, IEEE VIS)
- Uses audio cues (e.g., drawer opening, medication bottle) to trigger image capture
- Solves the battery problem by only recording images when audio events are interesting
- Tested on QA-Ego4D benchmark
- Cited by 3

### 5.4 Speaker Diarization on Wearables

**"WearVox: An Egocentric Multichannel Voice Assistant Benchmark for Wearables"** (Lin et al., 2025, arXiv:2601.02391)
- Benchmark for wearable voice assistants including speaker diarization + speech translation
- Multichannel audio from egocentric perspective

**"SpeechCompass: Enhancing Mobile Captioning with Diarization and Directional Guidance via Multi-Microphone Localization"** (2025, ACM CHI)
- Integrates speaker diarization with sound source localization on mobile devices
- Uses multiple microphones to determine speaker direction
- Relevant for understanding *who* is speaking in ambient recordings

**"Automated detection of foreground speech with wearable sensing in everyday home environments"** (Liang et al., 2022, arXiv:2203.11294)
- Specifically studies detecting the wearer's speech vs. other speakers from a wristwatch
- Uses Pyannote speaker diarization toolkit
- Transfer learning approach for home environments

**"Privacy-Preserving Real-Time Conversation Summarization with Local AI on Wearable Devices"** (Pahari et al., 2025, IEEE)
- ESP32-based wearable captures ambient audio
- Streams to local Pyannote for speaker diarization
- Local LLM for conversation summarization (privacy-preserving — no cloud)
- Directly relevant architecture for on-device processing

### 5.5 Healthcare Applications

**"Affordable Audio Hardware and AI Can Transform the Dementia Care Pipeline"** (Potamitis, 2025, Algorithms/MDPI)
- VAD + speaker diarization + wearable audio for monitoring dementia patients
- Demonstrates clinical applicability of always-on audio monitoring

**"Machine learning-assisted speech analysis for early detection of Parkinson's disease"** (Di Cesare et al., 2024, Sensors)
- Uses speaker diarization to analyze speech patterns from wearable devices
- Cited by 51 — very high interest in medical applications

---

## 6. Speaker Diarization & Attribution

Speaker diarization — determining "who spoke when" — is a critical requirement for making continuous recordings useful. Two approaches dominate:

### 6.1 Cloud-Based Diarization

**Deepgram** offers diarization as an API feature alongside transcription. You send audio, get back transcript with speaker labels. Quality is good for meetings but degrades in noisy ambient scenarios. Used by `say` and Omi.

**AssemblyAI, Google Speech-to-Text, AWS Transcribe** all offer similar cloud diarization.

### 6.2 Embedding-Based Speaker Verification

**ECAPA-TDNN** (Emphasized Channel Attention, Propagation and Aggregation in TDNN) is the current state-of-the-art for speaker embedding generation:
- **0.8% Equal Error Rate** on VoxCeleb benchmark
- Generates fixed-length speaker embeddings from variable-length audio
- Can be used to build a "speaker gallery" — enroll known speakers, then match new audio against the gallery
- Runs on-device for privacy

u/8ta4's approach: record a short sample of each frequent contact, generate ECAPA-TDNN embeddings, then match incoming audio segments against the gallery. This enables labeling transcripts with actual names ("Jordan: ...", "Alex: ...").

### 6.3 Open-Source Toolkits

**Pyannote** (Python) is the most widely used open-source speaker diarization toolkit. Used by:
- Pahari et al. (2025) — on-device wearable summarization
- Liang et al. (2022) — foreground speech detection from smartwatch
- Multiple other academic systems

**SpeechBrain** — another open-source toolkit with ECAPA-TDNN implementations.

### 6.4 Spatial Audio for Speaker Separation

**SpeechCompass** (2025, ACM CHI) demonstrates using multiple microphones to localize speakers spatially, combining diarization with directional audio. This could enable future wearables to show not just who spoke but where they were.

---

## 7. iOS / Apple Watch Constraints

This section is critical for the user's specific question about recording from Apple Watch or iPhone.

### 7.1 iOS Background Audio Recording

**Technical possibility:** iOS does support background audio recording via the `AVAudioSession` class with the `.record` or `.playAndRecord` category and the "Audio, AirPlay, and Picture in Picture" background mode entitlement.

**Practical reality (from r/iOSProgramming, u/jayword):**
> "Audio Background Mode mostly works for this, but it is totally unreliable. If you quit an app doing it, that app is gone and no longer recording. Also, it doesn't come back after restart unless the user explicitly launches it. Basically, if the user starts something, it can continue in the background as long as many conditions remain true like nothing else wants to record, the app isn't exited or dumped due to memory, no power cycle, etc. And now we have the orange light in the status bar whenever it is happening."

**Key limitations:**
1. **Orange indicator dot** — iOS shows a prominent orange dot whenever the mic is active. Users and bystanders see this.
2. **App suspension** — iOS will kill background audio recording if memory is low, another app requests audio, or the device restarts.
3. **No auto-restart** — if the OS kills the recording, it does NOT restart automatically. User must manually relaunch.
4. **Competing audio** — if a phone call comes in, music plays, or another app requests audio, the recording may be interrupted.
5. **No "always-on" entitlement** — there is no equivalent to Android's foreground service for permanent background recording. Apple has not created this capability.
6. **App Store review** — Apple reviewers scrutinize apps that request background audio, and an "always-on recorder" would face significant review challenges.

### 7.2 Apple Watch Specifics

- `AVAudioSession` is available since watchOS 2.0+
- Apple Watch has a microphone capable of recording
- **Battery life is the hard constraint:** Apple Watch Ultra 2 has ~36 hours of battery in normal use, but continuous microphone recording would drain it in a fraction of that time
- watchOS is even more aggressive about killing background tasks than iOS
- No persistent background recording capability for third-party apps

### 7.3 What Apple IS Doing

- **iOS 26+ (2025):** New transcription APIs that reportedly "blow past Whisper in speed tests" — on-device transcription is improving
- **Apple Intelligence:** On-device neural engine processing, but focused on Siri/system features, not exposed for always-on recording
- **Call Recording (iOS 18+):** Apple added native call recording with automatic transcription, but only for phone calls
- **Live Voicemail:** Transcribes voicemail in real-time, showing Apple's ASR capabilities

### 7.4 Recommended Path for Apple Ecosystem

Given iOS limitations, the viable architecture for Apple users is:

1. **Dedicated BLE pendant** (Omi or similar) for always-on capture
2. **iPhone app** receives BLE audio stream from pendant
3. **iPhone app** uses background audio mode to process (more reliable with active audio stream than pure recording)
4. **Stream to cloud ASR** (Deepgram) for transcription + diarization
5. **Results stored** in personal knowledge base

This is exactly the architecture Omi uses. The pendant solves the battery and background-mode problems, while the phone provides connectivity and processing.

---

## 8. Hardware & Form Factor Realities

### 8.1 Tested Hardware (from u/8ta4's extensive real-world testing)

| Device | Pros | Cons | Verdict |
|---|---|---|---|
| **MacBook built-in mic** | No extra device, no battery worry | Poor for non-quiet environments | Best for home/office solo use |
| **Shokz OpenRun Pro** | IP67 waterproof, bone conduction (ears open) | Wet mic produces gibberish transcription | Fails for shower/rain |
| **Poly Voyager 5200** | Best noise cancellation, close to mouth | Not waterproof, ear fatigue | Best for noisy environments (not wet) |
| **Bose SoundLink Micro** | Speaker + mic combo | Background noise destroys accuracy | Not recommended |
| **iPhone 15 Pro Max** | Always with you, good mic array | Can't handle noisy environments, iOS background limits | Usable only in quiet + foreground |
| **Omi pendant** | Tiny, purpose-built, BLE to phone | Limited mic quality vs. headset | Best always-on form factor |

### 8.2 Battery Reality for Glasses/Headsets

From workspace/existing research:
- **RayNeo X2 Pro** smart glasses: ~20 minutes in continuous recording mode
- **Meta Ray-Ban** smart glasses: ~4 hours of listening, less with recording
- **Humane AI Pin / Rabbit R1**: both commercial failures partially due to battery and heat issues

### 8.3 Ideal Hardware Properties

Based on real-world testing and academic papers:
1. **Close proximity to mouth** — inverse square law means 2x distance = 4x worse signal
2. **Noise cancellation** — hardware-level, not software (Krisp software didn't help)
3. **Always-on without charging anxiety** — either integrated into always-worn device or tiny pendant
4. **Waterproof with functional microphone** — the unsolved problem (waterproofing kills mic sensitivity)
5. **Socially acceptable** — no one notices or cares (pendant wins over headset here)
6. **Wireless** — can't have cables catching on things during daily life

---

## 9. Architecture Patterns for Always-On Audio

### 9.1 Pattern A: Direct Cloud Streaming (say)

```
Microphone → On-device VAD → Deepgram Streaming API → Transcript Files
                 (Silero)                                    (local)
```

- Simplest architecture
- VAD filters silence locally (saves cost)
- No local audio storage
- Requires constant internet connection
- ~240ms latency (depends on proximity to Deepgram US servers)

### 9.2 Pattern B: BLE Pendant + Phone + Cloud (Omi)

```
Pendant Mic → BLE → Phone App → Cloud ASR → Cloud Backend → Vector Store
  (nRF)        ↓      (Flutter)   (Deepgram)    (FastAPI)     (Pinecone)
             On-device              ↓
               VAD           Diarization
```

- Pendant handles capture; phone handles connectivity
- Better battery management (pendant is tiny, optimized for audio capture)
- Phone can do some local processing
- Full cloud pipeline for transcription, diarization, LLM processing

### 9.3 Pattern C: Edge-First Privacy-Preserving (Pahari et al., 2025)

```
ESP32 Mic → WiFi → Local Server → Pyannote Diarization → Local LLM → Summary
                    (Raspberry Pi     (Speaker ID)         (Ollama)
                     or similar)
```

- No data leaves local network
- Privacy-preserving by design
- Lower accuracy than Deepgram but improving rapidly
- Requires home server / always-on local compute

### 9.4 Pattern D: Egocentric Multimodal (EgoLife/EgoLog)

```
Smart Glasses → Audio + Video + IMU → On-device VAD → Cloud/Edge LLM → EgoRAG Memory Bank
                                        + filtering      (GPT-4V)        (Vector DB)
```

- Richest context (visual + audio + motion)
- Most battery-intensive
- Future-oriented (current glasses can't sustain 24/7)
- EgoRAG enables retrieval-augmented queries over life history

### 9.5 Proposed Architecture for Life Agent Integration

```
                    ┌─────────────┐
                    │  Omi Pendant │ (BLE audio capture)
                    └──────┬──────┘
                           │ BLE
                    ┌──────▼──────┐
                    │  iPhone App  │ (Omi or custom Flutter app)
                    │  - Silero VAD│
                    │  - BLE mgmt  │
                    └──────┬──────┘
                           │ HTTPS/WSS
              ┌────────────▼────────────┐
              │   Life Agent Backend     │ (AKS)
              │                          │
              │  ┌─────────────────────┐ │
              │  │ Audio Pipeline       │ │
              │  │ - Deepgram nova-3   │ │
              │  │ - Speaker diarize   │ │
              │  │ - ECAPA-TDNN match  │ │
              │  └────────┬────────────┘ │
              │           │              │
              │  ┌────────▼────────────┐ │
              │  │ Structuring Pipeline │ │
              │  │ - LLM summarization │ │
              │  │ - Entity extraction │ │
              │  │ - Action item detect│ │
              │  │ - Topic tagging     │ │
              │  └────────┬────────────┘ │
              │           │              │
              │  ┌────────▼────────────┐ │
              │  │ Knowledge Base       │ │
              │  │ - SQLite WAL (raw)  │ │
              │  │ - Vector embeddings │ │
              │  │ - Temporal index    │ │
              │  └─────────────────────┘ │
              └──────────────────────────┘
                           │
                    ┌──────▼──────┐
                    │  Query API   │
                    │  "What did   │
                    │   I discuss  │
                    │   with Alex  │
                    │   on Tuesday?"│
                    └─────────────┘
```

---

## 10. Privacy & Legal Considerations

### 10.1 Recording Laws

- **One-party consent states (US):** You can record conversations you participate in without informing others (~38 states)
- **Two-party/all-party consent states:** California, Florida, Illinois, and ~10 others require all parties to consent
- **Europe (GDPR):** Recording others without consent is generally illegal; even personal recordings can fall under GDPR if shared
- **Workplace:** Many jurisdictions have stricter rules for workplace recording

u/8ta4's disclaimer from `say`: "Different places have different laws about recording conversations. `say` is meant to help with accessibility, not to act as a recording device. But if someone misuses it, there could be legal trouble."

### 10.2 Privacy Architecture Choices

| Approach | Privacy Level | Accuracy | Cost |
|---|---|---|---|
| Local-only (Whisper on-device) | Highest | Lower ("garbage" per user reports, improving) | Free (compute cost only) |
| Cloud ASR (Deepgram) | Medium (encrypted in transit, processed in US) | Highest | ~$1/day |
| Hybrid (local VAD + cloud ASR) | Medium-High (only speech sent to cloud) | High | ~$1/day |
| No audio stored (transcribe-and-delete) | Higher | Same as ASR choice | Same as ASR choice |

`say` stores no audio — only transcripts. Deepgram claims to hold "audio data for as long as necessary" which is vague. Omi's backend stores data in Firebase/Pinecone (cloud-hosted).

### 10.3 Social Acceptability

From user experiences:
- Wearing a pendant is socially neutral (most people don't notice or ask)
- Wearing a headset 24/7 draws questions
- The orange dot on iPhone is visible to anyone looking at your phone
- u/8ta4 suggests: "Practice verbalizing every internal monologue. It's like learning a new language!" — indicating some social adaptation is needed

---

## 11. Integration with Personal Knowledge Bases

### 11.1 From Transcript to Knowledge

Raw transcripts are barely useful on their own. The value chain:

```
Raw audio → ASR transcript → Speaker-labeled transcript → Summarized segments
    → Entity extraction → Topic classification → Knowledge graph entries
        → Vector embeddings → Temporal index → Queryable memory
```

### 11.2 What to Extract

Based on academic literature and user experiences:

| Extraction | Method | Example |
|---|---|---|
| **Speaker identity** | ECAPA-TDNN + speaker gallery | "Jordan said..." |
| **Action items** | LLM extraction | "TODO: Email the proposal to Sarah" |
| **Decisions** | LLM extraction | "Decided to use PostgreSQL instead of MongoDB" |
| **Topics/tags** | LLM classification | #meeting #project-alpha #architecture |
| **Entities** | NER | People, places, dates, organizations |
| **Emotional tone** | Sentiment analysis | "Heated discussion", "positive brainstorm" |
| **Key quotes** | LLM extraction | Verbatim quotes marked for later retrieval |
| **Temporal context** | Timestamp + calendar correlation | "During the 2pm standup" |

### 11.3 Query Patterns

Based on EgoLife's EgoRAG and VIMES research, the most valuable queries are:

- **"What did I discuss with [person] on [date]?"** — speaker + temporal
- **"When did I last mention [topic]?"** — semantic + temporal
- **"What were the action items from [event]?"** — structured extraction
- **"What was the name of that restaurant someone recommended?"** — entity recall
- **"Summarize my conversations this week"** — aggregation
- **"Did I already tell [person] about [thing]?"** — conversational state tracking

### 11.4 Relevance to Life Agent

This capability maps directly to several Life Agent scenarios:
- **S3 (Meeting Prep)** — auto-summarize past conversations with attendees
- **S5 (Task Capture)** — automatically create tasks from spoken commitments
- **S8 (Journal)** — auto-generate daily journal from conversation summaries
- **S12 (Knowledge Graph)** — continuous knowledge graph construction from conversations
- **S42 (Social Context)** — remember what you discussed with each person last

---

## 12. Cost Analysis

### 12.1 Deepgram Pricing (2025)

| Model | Price | Notes |
|---|---|---|
| Nova-3 (streaming) | $0.0043/min | Best accuracy, recommended |
| Nova-2 | $0.0036/min | Slightly cheaper, slightly worse |
| Whisper (cloud) | $0.0048/min | OpenAI model hosted by Deepgram |

### 12.2 Daily Cost Estimate

Assumptions: 16 waking hours, VAD filters ~60% silence, leaving ~6.4 hours of speech.

| Configuration | Daily Hours | Daily Cost | Monthly Cost |
|---|---|---|---|
| **Heavy talker** (8 hrs speech) | 8 | $2.06 | $62 |
| **Average** (4 hrs speech) | 4 | $1.03 | $31 |
| **Light talker** (2 hrs speech) | 2 | $0.52 | $16 |
| **With diarization** | Add ~30% | $0.67–$2.68 | $20–$80 |

u/8ta4 reports ~$1/day as a heavy user, which aligns with the "average" estimate above (VAD does significant filtering).

### 12.3 Deepgram Free Tier

New accounts get $200 credit, which equals approximately 775 hours of Nova-3 transcription — enough for ~4 months of average use.

### 12.4 Alternative: Local Whisper

Running Whisper large-v3 locally on an M-series Mac or GPU:
- **Cost:** $0 (after hardware)
- **Accuracy:** Worse than Deepgram Nova-3 for streaming/real-time
- **Latency:** Higher (batch processing vs. streaming)
- **Diarization:** Requires separate Pyannote pipeline
- **Quality improving:** whisper-large-v3-turbo is faster with near-v3 accuracy

---

## 13. Open Problems & Research Gaps

### 13.1 Unsolved Technical Problems

1. **Noisy ambient recording** — Current ASR degrades severely in high-noise environments (shower, street, crowd). No combination of hardware noise cancellation + software processing has solved this.

2. **Waterproof mic quality** — IP67/68 waterproofing exists but acoustically degrades microphone performance. "Wet mic = gibberish" (u/8ta4).

3. **Unknown speaker identification** — Diarization can separate speakers, but identifying *who* an unknown speaker is requires a pre-enrolled gallery. Zero-shot speaker identification from voice alone is an open problem.

4. **iOS always-on reliability** — Apple provides no mechanism for truly persistent background audio recording on iPhone/Apple Watch. The orange dot + app killing behavior makes iPhone-only solutions fragile.

5. **Consent at scale** — In two-party consent jurisdictions, an always-on recorder creates legal liability. No technical solution exists for automatic consent detection.

6. **Long-term storage & retrieval** — Years of transcripts create massive knowledge bases. Efficient retrieval over 5+ years of daily text requires more than simple vector search.

### 13.2 Research Gaps

1. **No longitudinal studies** — The longest documented personal use is u/8ta4 (~2 years) and "7.3 years of voice diary data" (unnamed user). No controlled academic study has followed always-on audio lifeloggers over years.

2. **Memory augmentation efficacy** — Does having searchable transcripts actually improve memory recall? The EgoLife/VIMES papers demonstrate technical feasibility but don't measure cognitive benefit.

3. **Social dynamics** — How does always-on recording change the behavior of people around the wearer? No systematic study.

4. **Information overload** — If you record everything, how do you avoid drowning in data? Priority/salience filtering is underexplored.

5. **On-device ASR parity** — When will on-device models (Apple's new APIs, Whisper) match cloud ASR quality? This determines when privacy-preserving architectures become viable.

---

## 14. References

### Products & Tools

- **say** — github.com/8ta4/say (open source, macOS, Deepgram)
- **Omi** — omi.me / github.com/BasedHardware/omi (open source, MIT, BLE pendant)
- **Limitless** — limitless.ai (acquired by Meta, 2025)
- **Deepgram** — deepgram.com (Nova-3 streaming ASR)
- **Pyannote** — github.com/pyannote/pyannote-audio (open source speaker diarization)

### Academic Papers

1. Ribeiro, R., Trifan, A., & Neves, A.J.R. (2022). "Lifelog retrieval from daily digital data: narrative review." JMIR mHealth and uHealth. Cited by 43.
2. Dingler, T., Agroudy, P.E., Rzayev, R., & Lischke, L. (2021). "Memory augmentation through lifelogging: opportunities and challenges." Springer.
3. Pierce, C., & Mann, S. (2021). "Wearable Affective Memory Augmentation." arXiv:2112.01584.
4. Bermejo, C., Braud, T., Yang, J., et al. (2020). "VIMES: A wearable memory assistance system for automatic information retrieval." ACM Multimedia. Cited by 21.
5. Shen, J. & Dudley, J.J. (2024). "Encode-Store-Retrieve: Augmenting Human Memory through Language-Encoded Egocentric Perception." IEEE. Cited by 14.
6. Yang, J., Liu, S., Guo, H., Dong, Y., et al. (2025). "EgoLife: Towards Egocentric Life Assistant." CVPR 2025. Cited by 63.
7. He, L., Yang, B., Duan, D., Yan, Z., & Xing, G. (2025). "EgoLog: Ego-Centric Fine-Grained Daily Log with Ubiquitous Wearables." arXiv:2504.02624.
8. Paruchuri, A., Hersek, S., & Aggarwal, L. (2025). "EgoTrigger: Toward Audio-Driven Image Capture for Human Memory Enhancement in All-Day Energy-Efficient Smart Glasses." IEEE VIS.
9. Lin, Z., Xu, Y., Sun, K., et al. (2025). "WearVox: An Egocentric Multichannel Voice Assistant Benchmark for Wearables." arXiv:2601.02391.
10. (2025). "SpeechCompass: Enhancing Mobile Captioning with Diarization and Directional Guidance via Multi-Microphone Localization." ACM CHI.
11. Liang, D., Xu, Z., Chen, Y., et al. (2022). "Automated detection of foreground speech with wearable sensing in everyday home environments." arXiv:2203.11294.
12. Pahari, A.K. & Bharathi, C. (2025). "Privacy-Preserving Real-Time Conversation Summarization with Local AI on Wearable Devices." IEEE.
13. Potamitis, I. (2025). "Affordable Audio Hardware and AI Can Transform the Dementia Care Pipeline." Algorithms (MDPI).
14. Di Cesare, M.G., et al. (2024). "Machine learning-assisted speech analysis for early detection of Parkinson's disease: speaker diarization and classification techniques." Sensors. Cited by 51.
15. Zulfikar, W.D. (2024). "AI Interfaces for Augmenting Episodic Memory." PhD thesis, ProQuest.
16. Laporte, M. (2025). "Towards physiologically-driven human memory augmentation." PhD thesis, University of Fribourg.
17. Ali, S., Khusro, S., & Khan, A. (2021). "Smartphone-based lifelogging: toward realization of personal big data." Springer.

### Community Sources

- r/QuantifiedSelf — "Why Transcribe Everything You Say 24/7?" (u/8ta4)
- r/QuantifiedSelf — "I created say: a 24/7 voice transcription tool" (u/8ta4)
- r/QuantifiedSelf — "Looking for a Shower-Proof, Noise-Cancelling Mic for 24/7 Life Transcription" (u/8ta4)
- r/iOSProgramming — "Has Apple allowed background audio recording?" (2024)
